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
        public Action<RioSocket> OnAccepted;
        internal IntPtr  DisconnectCompletionPort;
        internal RioSocket[] allSockets;
        
        public unsafe  RioTcpSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, int socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool,  maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {
            var adrSize = (sizeof(sockaddr_in) + 16) * 2;
            var overlapped = Marshal.AllocHGlobal(new IntPtr(socketCount * Marshal.SizeOf<RioNativeOverlapped>()));
            var adressBuffer = Marshal.AllocHGlobal(new IntPtr(socketCount * adrSize));

            allSockets = new RioSocket[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                allSockets[i] = new RioSocket(overlapped + (i * Marshal.SizeOf<RioNativeOverlapped>()), adressBuffer + (i * adrSize), this);
                allSockets[i]._overlapped->SocketIndex = i;
            }

            foreach (var s in allSockets)
            {
                if ((Imports.CreateIoCompletionPort(s._socket, DisconnectCompletionPort, 0, 1)) == IntPtr.Zero)
                    Imports.ThrowLastError();
            }

            if ((DisconnectCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();

            Thread DisThread = new Thread(CompleteDisConnect);
            DisThread.IsBackground = true;
            DisThread.Start();

        }

        public abstract unsafe void CompleteDisConnect(object o);



        internal unsafe virtual void Recycle(RioSocketBase socket)
        {
            RioSocket c;
            connections.TryRemove(socket.GetHashCode(), out c);
            c.ResetOverlapped();
            
            if (!RioStatic.DisconnectEx(c._socket, c._overlapped, 0x02, 0)) //TF_REUSE_SOCKET
                if (Imports.WSAGetLastError() != 997) // error_io_pending
                    Imports.ThrowLastWSAError();
            //else
            //    AcceptEx(socket);
        }
    }
}
