using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace RioSharp
{
    public class RioTcpListener : RioConnectionOrientedSocketPool
    {
        internal IntPtr _listenerSocket;
        internal IntPtr _listenIocp;
        public Action<RioSocket> OnAccepted;

        public unsafe RioTcpListener(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount, uint maxOutstandingReceive = 2048, uint maxOutstandingSend = 2048)
            : base(sendPool, revicePool, socketCount, ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, maxOutstandingReceive, maxOutstandingSend)
        {
            if ((_listenerSocket = WinSock.WSASocket(adressFam, sockType, protocol, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            int True = 1;
            uint dwBytes = 0;

            if (WinSock.WSAIoctlGeneral2(_listenerSocket, WinSock.SIO_LOOPBACK_FAST_PATH, &True, sizeof(int), (void*)0, 0, out dwBytes, IntPtr.Zero, IntPtr.Zero) != 0)
                WinSock.ThrowLastWSAError();
            if (WinSock.setsockopt(_listenerSocket, WinSock.IPPROTO_TCP, WinSock.TCP_NODELAY, &True, 4) != 0)
                WinSock.ThrowLastWSAError();

            if ((_listenIocp = Kernel32.CreateIoCompletionPort(_listenerSocket, _listenIocp, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            Thread AcceptIocpThread = new Thread(AcceptIocpComplete);
            AcceptIocpThread.IsBackground = true;
            AcceptIocpThread.Start();
        }

        unsafe void BeginAccept(RioConnectionOrientedSocket acceptSocket)
        {
            int recived = 0;
            acceptSocket.ResetOverlapped();
            if (!RioStatic.AcceptEx(_listenerSocket, acceptSocket.Socket, acceptSocket._adressBuffer, 0, sizeof(sockaddr_in) + 16, sizeof(sockaddr_in) + 16, out recived, acceptSocket._overlapped))
            {
                WinSock.ThrowLastWSAError();
            }
            else
                OnAccepted(acceptSocket);
        }

        public void Listen(IPEndPoint localEP, int backlog)
        {
            in_addr inAddress = new in_addr();
            inAddress.s_b1 = localEP.Address.GetAddressBytes()[0];
            inAddress.s_b2 = localEP.Address.GetAddressBytes()[1];
            inAddress.s_b3 = localEP.Address.GetAddressBytes()[2];
            inAddress.s_b4 = localEP.Address.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = WinSock.htons((ushort)localEP.Port);
            sa.sin_addr = inAddress;

            unsafe
            {
                if (WinSock.bind(_listenerSocket, ref sa, sizeof(sockaddr_in)) == WinSock.SOCKET_ERROR)
                    WinSock.ThrowLastWSAError();
            }

            if (WinSock.listen(_listenerSocket, backlog) == WinSock.SOCKET_ERROR)
                WinSock.ThrowLastWSAError();

            foreach (var s in allSockets)
            {
                InitializeSocket(s);
                BeginAccept(s);
            }
        }

        unsafe void AcceptIocpComplete(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];

            while (true)
            {
                if (Kernel32.GetQueuedCompletionStatusRio(_listenIocp, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    var res = allSockets[lpOverlapped->SocketIndex];
                    activeSockets.TryAdd(res.GetHashCode(), res);
                    void* apa = _listenerSocket.ToPointer();
                    if (res.SetSocketOption(SOL_SOCKET_SocketOptions.SO_UPDATE_ACCEPT_CONTEXT, &apa, IntPtr.Size) != 0)
                        WinSock.ThrowLastWSAError();

                    OnAccepted(res);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == Kernel32.ERROR_ABANDONED_WAIT_0)
                        break;
                    else if (error == Kernel32.ERROR_NETNAME_DELETED)
                        BeginRecycle(allSockets[lpOverlapped->SocketIndex],false);
                    else
                        throw new Win32Exception(error);
                }
            }
        }

        internal override void InitializeSocket(RioConnectionOrientedSocket socket)
        {
            var t = TimeSpan.FromSeconds(30);
            socket.SetLoopbackFastPath(true);
            socket.SetTcpNoDelay(true);
            socket.SendTimeout = t;
            socket.ReciveTimeout = t;
        }

        internal override void FinalizeRecycle(RioConnectionOrientedSocket socket)
        {
            BeginAccept(socket);
        }
        
        protected override bool SocketIocpOk(RioConnectionOrientedSocket socket, byte status)
        {
            ThreadPool.QueueUserWorkItem(oo =>
            {
                EndRecycle((RioConnectionOrientedSocket)oo, true);
            }, socket);

            return false;
        }

        protected override bool SocketIocpError(int error, RioConnectionOrientedSocket socket, byte status)
        {

            if (error == Kernel32.ERROR_ABANDONED_WAIT_0)
                return true;
            else if (error == Kernel32.ERROR_NETNAME_DELETED)
                BeginRecycle(socket,false);
            else
                throw new Win32Exception(error);

            return false;
        }

        public override void Dispose()
        {
            Kernel32.CloseHandle(_listenIocp);
            WinSock.closesocket(_listenerSocket);
            base.Dispose();
        }
    }
}
