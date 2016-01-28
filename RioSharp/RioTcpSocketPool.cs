using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public abstract class RioTcpSocketPool : RioSocketPool
    {
        public Action<RioTcpSocket> OnAccepted;
        internal IntPtr DisconnectCompletionPort;
        internal IntPtr AcceptCompletionPort;
        internal RioTcpSocket[] allSockets;

        public unsafe RioTcpSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool, maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {
            var adrSize = (sizeof(sockaddr_in) + 16) * 2;
            var overlapped = Marshal.AllocHGlobal(new IntPtr(socketCount * Marshal.SizeOf<RioNativeOverlapped>()));
            var adressBuffer = Marshal.AllocHGlobal(new IntPtr(socketCount * adrSize));

            allSockets = new RioTcpSocket[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                allSockets[i] = new RioTcpSocket(overlapped + (i * Marshal.SizeOf<RioNativeOverlapped>()), adressBuffer + (i * adrSize), this);
                allSockets[i]._overlapped->SocketIndex = i;
            }


            if ((DisconnectCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();

            foreach (var s in allSockets)
            {
                if ((Imports.CreateIoCompletionPort(s._socket, DisconnectCompletionPort, 0, 1)) == IntPtr.Zero)
                    Imports.ThrowLastError();
            }


            if ((AcceptCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();


            Thread DisThread = new Thread(CompleteDisConnect);
            DisThread.IsBackground = true;
            DisThread.Start();

        }

        protected abstract unsafe void CompleteDisConnect(object o);

        internal unsafe virtual void Recycle(RioTcpSocket socket)
        {
            RioSocketBase c;
            connections.TryRemove(socket.GetHashCode(), out c);
            socket.ResetOverlapped();
            socket._overlapped->Status = 1;
            if (!RioStatic.DisconnectEx(c._socket, socket._overlapped, 0x02, 0)) //TF_REUSE_SOCKET
                if (Imports.WSAGetLastError() != 997) // error_io_pending
                    Imports.ThrowLastWSAError();
            //else
            //    AcceptEx(socket);
        }
    }
}
