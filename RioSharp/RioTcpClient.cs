using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClient : RioTcpSocketPool
    {
        ConcurrentQueue<RioTcpSocket> freeSockets = new ConcurrentQueue<RioTcpSocket>();
        ConcurrentDictionary<RioTcpSocket, TaskCompletionSource<RioTcpSocket>> ongoingConnections = new ConcurrentDictionary<RioTcpSocket, TaskCompletionSource<RioTcpSocket>>();

        public RioTcpClient(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024)
            : base(sendPool, revicePool, socketCount, maxOutstandingReceive, maxOutstandingSend, (maxOutstandingReceive + maxOutstandingSend) * socketCount)
        {
            foreach (var s in allSockets)
            {
                freeSockets.Enqueue(s);

                in_addr inAddress = new in_addr();
                inAddress.s_b1 = 0;
                inAddress.s_b2 = 0;
                inAddress.s_b3 = 0;
                inAddress.s_b4 = 0;

                sockaddr_in sa = new sockaddr_in();
                sa.sin_family = ADDRESS_FAMILIES.AF_INET;
                sa.sin_port = 0;
                //Imports.ThrowLastWSAError();
                sa.sin_addr = inAddress;

                unsafe
                {
                    if (Imports.bind(s._socket, ref sa, sizeof(sockaddr_in)) == Imports.SOCKET_ERROR)
                        Imports.ThrowLastWSAError();
                }
            }
        }

        unsafe void _CompleteConnect(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            int lpcbTransfer;
            int lpdwFlags;
            TaskCompletionSource<RioTcpSocket> r;
            RioTcpSocket res;

            while (true)
            {
                if (Imports.GetQueuedCompletionStatusRio(AcceptCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    if (Imports.WSAGetOverlappedResult(allSockets[lpOverlapped->SocketIndex]._socket, lpOverlapped, out lpcbTransfer, false, out lpdwFlags))
                    {
                        res = allSockets[lpOverlapped->SocketIndex];
                        connections.TryAdd(res.GetHashCode(), res);
                        if (ongoingConnections.TryRemove(res, out r))
                            r.SetResult(res);
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

        protected override unsafe void CompleteDisConnect(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            TaskCompletionSource<RioTcpSocket> r;
            RioTcpSocket res;
            int lpcbTransfer;
            int lpdwFlags;

            while (true)
            {
                if (Imports.GetQueuedCompletionStatusRio(DisconnectCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    if (lpOverlapped->Status == 1)
                    {
                        freeSockets.Enqueue(allSockets[lpOverlapped->SocketIndex]);
                    }
                    else if (lpOverlapped->Status == 2)
                    {
                        if (Imports.WSAGetOverlappedResult(allSockets[lpOverlapped->SocketIndex]._socket, lpOverlapped, out lpcbTransfer, false, out lpdwFlags))
                        {
                            res = allSockets[lpOverlapped->SocketIndex];
                            connections.TryAdd(res.GetHashCode(), res);
                            if (ongoingConnections.TryRemove(res, out r))
                                r.SetResult(res);
                        }
                        else {
                            //recycle socket
                        }
                    }
                } //1225
                else
                {
                    var error = Marshal.GetLastWin32Error();

                    if (error != 0 && error != 64 & error != 1225) //connection no longer available
                        throw new Win32Exception(error);
                    else
                    {
                        res = allSockets[lpOverlapped->SocketIndex];
                        freeSockets.Enqueue(allSockets[lpOverlapped->SocketIndex]);
                        if (ongoingConnections.TryRemove(res, out r))
                            r.SetException(new Win32Exception(error));
                    }
                }
            }
        }

        public unsafe Task<RioTcpSocket> Connect(Uri adress)
        {
            var adr = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily == AddressFamily.InterNetwork);

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = adr.GetAddressBytes()[0];
            inAddress.s_b2 = adr.GetAddressBytes()[1];
            inAddress.s_b3 = adr.GetAddressBytes()[2];
            inAddress.s_b4 = adr.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)adress.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            RioTcpSocket s;
            freeSockets.TryDequeue(out s);
            var tcs = new TaskCompletionSource<RioTcpSocket>();
            ongoingConnections.TryAdd(s, tcs);

            uint gurka;

            unsafe
            {
                s.ResetOverlapped();
                s._overlapped->Status = 2;
                if (!RioStatic.ConnectEx(s._socket, sa, sizeof(sockaddr_in), IntPtr.Zero, 0, out gurka, s._overlapped))
                    if (Imports.WSAGetLastError() != 997) // error_io_pending
                        Imports.ThrowLastWSAError();
            }

            return tcs.Task;
        }
    }
}
