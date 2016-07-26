using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClientPool : RioConnectionOrientedSocketPool
    {
        ConcurrentQueue<RioConnectionOrientedSocket> _freeSockets = new ConcurrentQueue<RioConnectionOrientedSocket>();
        ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioSocket>> _ongoingConnections = new ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioSocket>>();

        public RioTcpClientPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024)
            : base(sendPool, revicePool, socketCount, ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, maxOutstandingReceive, maxOutstandingSend)
        {
            foreach (var s in allSockets)
            {
                InitializeSocket(s);
                _freeSockets.Enqueue(s);
            }
        }


        internal override void InitializeSocket(RioConnectionOrientedSocket socket)
        {
            socket.SetLoopbackFastPath(true);
            socket.SetTcpNoDelay(true);
            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = adressFam;

            unsafe
            {
                if (WinSock.bind(socket.Socket, ref sa, sizeof(sockaddr_in)) == WinSock.SOCKET_ERROR)
                    WinSock.ThrowLastWSAError();
            }
        }

        internal override void FinalizeRecycle(RioConnectionOrientedSocket socket)
        {
            _freeSockets.Enqueue(socket);
        }

        protected override unsafe void SocketIocpComplete(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            TaskCompletionSource<RioSocket> r;
            RioConnectionOrientedSocket res;

            while (true)
            {
                if (Kernel32.GetQueuedCompletionStatusRio(socketIocp, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    if (lpOverlapped->Status == 1)
                    {
                        EndRecycle(allSockets[lpOverlapped->SocketIndex],true);
                    }
                    else if (lpOverlapped->Status == 2)
                    {
                        res = allSockets[lpOverlapped->SocketIndex];
                        activeSockets.TryAdd(res.GetHashCode(), res);
                        if (res.SetSocketOption(SOL_SOCKET_SocketOptions.SO_UPDATE_CONNECT_CONTEXT, (void*)0, 0) != 0)
                            WinSock.ThrowLastWSAError();

                        if (_ongoingConnections.TryRemove(res, out r))
                            ThreadPool.QueueUserWorkItem(oo =>
                            {
                                var rr = (Tuple<TaskCompletionSource<RioSocket>, RioConnectionOrientedSocket>)oo;
                                rr.Item1.SetResult(rr.Item2);
                            }, Tuple.Create(r, res));
                    }
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();

                    if (error == Kernel32.ERROR_ABANDONED_WAIT_0)
                        break;
                    else if (error == Kernel32.ERROR_NETNAME_DELETED || error == Kernel32.ERROR_CONNECTION_REFUSED)
                    {
                        res = allSockets[lpOverlapped->SocketIndex];
                        BeginRecycle(res);
                        if (_ongoingConnections.TryRemove(res, out r))
                            ThreadPool.QueueUserWorkItem(oo =>
                            {
                                var rr = (Tuple<TaskCompletionSource<RioSocket>, int>)oo;
                                rr.Item1.SetException(new Win32Exception(rr.Item2));
                            }, Tuple.Create(r, error));
                    }
                    else
                        throw new Win32Exception(error);
                }
            }
        }

        public async Task<RioSocket> Connect(Uri adress)
        {
            var ip = (await Dns.GetHostAddressesAsync(adress.Host)).First(i => i.AddressFamily == AddressFamily.InterNetwork);

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = adressFam;
            sa.sin_port = WinSock.htons((ushort)adress.Port);

            var ipBytes = ip.GetAddressBytes();

            unsafe
            {
                fixed (byte* a = ipBytes)
                    Unsafe.CopyBlock(sa.sin_addr.Address, a, (uint)ipBytes.Length);
            }

            RioConnectionOrientedSocket s;
            if (_freeSockets.TryDequeue(out s))
            {
                var tcs = new TaskCompletionSource<RioSocket>();
                _ongoingConnections.TryAdd(s, tcs);
                uint bytesSent;
                unsafe
                {
                    s.ResetOverlapped();
                    s._overlapped->Status = 2;
                    if (!RioStatic.ConnectEx(s.Socket, sa, sizeof(sockaddr_in), IntPtr.Zero, 0, out bytesSent, s._overlapped))
                        WinSock.ThrowLastWSAError();
                }

                return await tcs.Task;
            }
            else
                return await Task.FromException<RioConnectionOrientedSocket>(new ArgumentException("No sockets available in pool"));

        }
    }
}
