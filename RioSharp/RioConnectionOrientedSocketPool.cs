using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public abstract class RioConnectionOrientedSocketPool : RioSocketPool
    {
        protected IntPtr socketIocp;
        internal RioConnectionOrientedSocket[] allSockets;
        internal ConcurrentDictionary<long, RioConnectionOrientedSocket> activeSockets = new ConcurrentDictionary<long, RioConnectionOrientedSocket>();
        ConcurrentDictionary<long, RioConnectionOrientedSocket> disconnectingSockets = new ConcurrentDictionary<long, RioConnectionOrientedSocket>();

        public unsafe RioConnectionOrientedSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024)
            : base(sendPool, revicePool, adressFam, sockType, protocol, maxOutstandingReceive, maxOutstandingSend, socketCount)
        {
            var adrSize = (sizeof(sockaddr_in) + 16) * 2;
            var overlapped = Marshal.AllocHGlobal(new IntPtr(socketCount * Marshal.SizeOf<RioNativeOverlapped>()));
            var adressBuffer = Marshal.AllocHGlobal(new IntPtr(socketCount * adrSize));

            allSockets = new RioConnectionOrientedSocket[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                allSockets[i] = new RioConnectionOrientedSocket(overlapped + (i * Marshal.SizeOf<RioNativeOverlapped>()), adressBuffer + (i * adrSize), this, SendBufferPool, ReceiveBufferPool, adressBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue, adressFam, sockType, protocol);
                allSockets[i]._overlapped->SocketIndex = i;
            }

            if ((socketIocp = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            foreach (var s in allSockets)
            {
                if ((Kernel32.CreateIoCompletionPort(s.Socket, socketIocp, 0, 1)) == IntPtr.Zero)
                    Kernel32.ThrowLastError();
            }

            Thread SocketIocpThread = new Thread(SocketIocpComplete);
            SocketIocpThread.IsBackground = true;
            SocketIocpThread.Start();

            Timeout();
        }

        private async void Timeout()
        {
            while (true)
            {
                await Task.Delay(1000);
                foreach (var s in activeSockets.Values)
                {
                    if (((s.StartedReceives - s.FinishdedReceives) > 0 && (CurrentTime - s.lastReceiveStart > s.reciveTimeout)) || ((s.StartedSends - s.FinishdedSends) > 0 && (CurrentTime - s.lastSendStart > s.sendTimeout)))
                        s.Dispose();
                }

                foreach (var s in disconnectingSockets.Values)
                {
                    if (CurrentTime - s.disconnectStartTime > Stopwatch.Frequency * 5)
                        EndRecycle(s, true);
                }
            }
        }


        protected abstract void SocketIocpComplete(object o);

        internal void EndRecycle(RioConnectionOrientedSocket socket, bool async)
        {
            RioConnectionOrientedSocket s;
            disconnectingSockets.TryRemove(socket.GetHashCode(), out s);

            bool gotRest = false;
            int attempts = 0;
            while ((socket.StartedReceives - socket.FinishdedReceives) > 0 || (socket.StartedSends - socket.FinishdedSends) > 0)
            {
                Thread.Sleep(100);
                attempts++;
                if (attempts > 10 && !gotRest)
                {
                    gotRest = true;
                    socket.ResetSocket();
                    if ((Kernel32.CreateIoCompletionPort(socket.Socket, socketIocp, 0, 1)) == IntPtr.Zero)
                        Kernel32.ThrowLastError();
                }
            }

            if (gotRest)
                InitializeSocket(socket);

            socket.StartedReceives = 0;
            socket.StartedSends = 0;
            socket.FinishdedReceives = 0;
            socket.FinishdedSends = 0;

            FinalizeRecycle(socket);
        }




        internal abstract void FinalizeRecycle(RioConnectionOrientedSocket socket);

        internal abstract void InitializeSocket(RioConnectionOrientedSocket socket);

        internal unsafe virtual void BeginRecycle(RioConnectionOrientedSocket socket)
        {
            disconnectingSockets.TryAdd(socket.GetHashCode(), socket);
            RioConnectionOrientedSocket c;
            activeSockets.TryRemove(socket.GetHashCode(), out c);
            socket.ResetOverlapped();
            socket.disconnectStartTime = RioSocketPool.CurrentTime;

            socket._overlapped->Status = 1;
            if (!RioStatic.DisconnectEx(socket.Socket, socket._overlapped, WinSock.TF_REUSE_SOCKET, 0))
            {
                if (WinSock.WSAGetLastError() == WinSock.WSAENOTCONN)
                    EndRecycle(socket, false);
                else
                    WinSock.ThrowLastWSAError();
            }
        }

        public override void Dispose()
        {
            Kernel32.CloseHandle(socketIocp);
            for (int i = 0; i < allSockets.Length; i++)
                allSockets[i].Close();

            base.Dispose();
        }
    }
}
