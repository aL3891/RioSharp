using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpListener : IDisposable
    {
        internal IntPtr _listenerSocket;
        private RioSocketPool _pool;
        internal IntPtr AcceptCompletionPort;
        IntPtr acceptOverlapped;

        public unsafe RioTcpListener(RioSocketPool pool)
        {
            _pool = pool;

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



        }

        public unsafe RioSocket AcceptEx()
        {

            var ao = stackalloc NativeOverlapped[1];
            IntPtr a = Marshal.AllocHGlobal((sizeof(sockaddr_in)+16) * 2);

            Imports.MemSet(a, 0, sizeof(sockaddr_in) * 2);

            sockaddr_in* sock = (sockaddr_in*)a.ToPointer();

            sock->sin_family = ADDRESS_FAMILIES.AF_INET;
            sock->sin_port = Imports.htons(5000);
            //Imports.ThrowLastWSAError();
            sock->sin_addr.s_b1 = 0;
            sock->sin_addr.s_b2 = 0;
            sock->sin_addr.s_b3 = 0;
            sock->sin_addr.s_b4 = 0;


            IntPtr acceptSocket;

            if ((acceptSocket = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            //if ((AcceptCompletionPort = Imports.CreateIoCompletionPort(_listenerSocket, IntPtr.Zero, 0, 1)) == IntPtr.Zero)
            //    Imports.ThrowLastError();

            ao->InternalHigh = IntPtr.Zero;
            ao->InternalLow = IntPtr.Zero;
            ao->OffsetHigh = 0;
            ao->OffsetLow = 0;

            ao->EventHandle = Imports.CreateEvent(IntPtr.Zero, false, false, null);

            int recived = 0;

            if (!RioStatic.AcceptEx(_listenerSocket, acceptSocket, a, 0, sizeof(sockaddr_in)+16, sizeof(sockaddr_in)+16, ref recived, ao))
            {
                if (Imports.WSAGetLastError() != 997)
                    Imports.ThrowLastWSAError();
            }
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            NativeOverlapped* lpOverlapped = stackalloc NativeOverlapped[1];
            int tt;

            if ((tt = Imports.GetQueuedCompletionStatus(AcceptCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1)) == 0)
                Imports.ThrowLastError();
            

            int lpcbTransfer;
            int lpdwFlags;

            Imports.WSAGetOverlappedResult(_listenerSocket, lpOverlapped, out lpcbTransfer, false, out lpdwFlags);

            return new RioSocket(acceptSocket, null);
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

        public void Listen(int backlog)
        {
            if (Imports.listen(_listenerSocket, backlog) == Imports.SOCKET_ERROR)
                Imports.ThrowLastWSAError();
        }

        public RioSocket Accept()
        {
            unsafe
            {
                sockaddr_in sa = new sockaddr_in();
                int len = sizeof(sockaddr_in);
                IntPtr accepted = Imports.accept(_listenerSocket, ref sa, ref len);
                if (accepted == new IntPtr(-1))
                    Imports.ThrowLastWSAError();

                var res = new RioSocket(accepted, _pool);
                _pool.connections.TryAdd(res.GetHashCode(), res);
                res.ReciveInternal();
                return res;
            }
        }

        public void Dispose()
        {
            //stop listening? 
        }
    }
}
