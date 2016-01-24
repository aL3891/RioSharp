using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace RioSharp
{
    public class RioTcpListener : RioTcpSocketPool
    {
        internal IntPtr _listenerSocket;
        internal IntPtr AcceptCompletionPort;
        IntPtr acceptOverlapped;

        
        public unsafe RioTcpListener(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, int socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool, socketCount, maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {

            if ((_listenerSocket = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            int True = -1;
            UInt32 dwBytes = 0;

            Imports.setsockopt(_listenerSocket, Imports.IPPROTO_TCP, Imports.TCP_NODELAY, (char*)&True, 4);
            Imports.WSAIoctlGeneral(_listenerSocket, Imports.SIO_LOOPBACK_FAST_PATH,
                                &True, 4, null, 0,
                                out dwBytes, IntPtr.Zero, IntPtr.Zero);


            if ((AcceptCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();


            if ((Imports.CreateIoCompletionPort(_listenerSocket, AcceptCompletionPort, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();

            

            Thread sendThread = new Thread(CompleteConnect);
            sendThread.IsBackground = true;
            sendThread.Start();

            
        }

        public void StartAccepting()
        {
            foreach (var s in allSockets)
            {
                AcceptEx(s);
            }
        }

        public unsafe void AcceptEx(RioSocket acceptSocket)
        {
            int recived = 0;
            acceptSocket.ResetOverlapped();
            if (!RioStatic.AcceptEx(_listenerSocket, acceptSocket._socket, acceptSocket._adressBuffer, 0, sizeof(sockaddr_in) + 16, sizeof(sockaddr_in) + 16, out recived, acceptSocket._overlapped))
            {
                if (Imports.WSAGetLastError() != 997) // error_io_pending
                    Imports.ThrowLastWSAError();
            }
            else
                OnAccepted(acceptSocket);
        }


        public unsafe void CompleteConnect(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            int lpcbTransfer;
            int lpdwFlags;

            while (true)
            {
                if (Imports.GetQueuedCompletionStatusRio(AcceptCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    if (Imports.WSAGetOverlappedResult(_listenerSocket, lpOverlapped, out lpcbTransfer, false, out lpdwFlags))
                    {
                        var res = allSockets[lpOverlapped->SocketIndex];
                        connections.TryAdd(res.GetHashCode(), res);
                        res.ReciveInternal();
                        OnAccepted(res);
                    }
                    else {
                        //recycle socket
                    }
                }
                else {
                    var error = Marshal.GetLastWin32Error();

                    if (error != 0 && error != 64) //connection no longer available
                        throw new Win32Exception(error);

                }
            }
        }


        public void Bind(IPEndPoint localEP)
        {
            in_addr inAddress = new in_addr();
            inAddress.s_b1 = localEP.Address.GetAddressBytes()[0];
            inAddress.s_b2 = localEP.Address.GetAddressBytes()[1];
            inAddress.s_b3 = localEP.Address.GetAddressBytes()[2];
            inAddress.s_b4 = localEP.Address.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)localEP.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            unsafe
            {
                if (Imports.bind(_listenerSocket, ref sa, sizeof(sockaddr_in)) == Imports.SOCKET_ERROR)
                    Imports.ThrowLastWSAError();
            }
        }



        public override unsafe void CompleteDisConnect(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];

            while (true)
            {
                if (Imports.GetQueuedCompletionStatusRio(DisconnectCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                    AcceptEx(allSockets[lpOverlapped->SocketIndex]);
                else
                    Imports.ThrowLastError();
            }
        }

        public void Listen(int backlog)
        {
            if (Imports.listen(_listenerSocket, backlog) == Imports.SOCKET_ERROR)
                Imports.ThrowLastWSAError();
        }

        //public RioSocket Accept()
        //{
        //    unsafe
        //    {
        //        sockaddr_in sa = new sockaddr_in();
        //        int len = sizeof(sockaddr_in);
        //        IntPtr accepted = Imports.accept(_listenerSocket, ref sa, ref len);
        //        if (accepted == new IntPtr(-1))
        //            Imports.ThrowLastWSAError();

        //        var res = new RioSocket(accepted, _pool);
        //        _pool.connections.TryAdd(res.GetHashCode(), res);
        //        res.ReciveInternal();
        //        return res;
        //    }
        //}

        public void Dispose()
        {
            //stop listening? 
        }
    }
}
