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

        bool running = true;
        TaskCompletionSource<object> timouttcs = new TaskCompletionSource<object>();

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

        private async Task Timeout()
        {
            while (running)
            {
                await Task.Delay(1000);
                foreach (var s in activeSockets.Values)
                {
                    if ((s.pendingRecives > 0 && CurrentTime - s.lastReceiveStart > s.reciveTimeout) || (s.pendingRecives > 0 && CurrentTime - s.lastSendStart > s.sendTimeout))
                    {
                        WinSock.closesocket(s.Socket);
                        s.Socket = IntPtr.Zero;
                    }
                }

                foreach (var s in disconnectingSockets.Values)
                {
                    if (CurrentTime - s.disconnectStartTime > Stopwatch.Frequency * 5)
                        BeginRecycle(s, true);
                }
            }

            timouttcs.SetResult(null);
        }

        protected unsafe void SocketIocpComplete(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            RioNativeOverlapped* lpOverlappedNull = (RioNativeOverlapped*)0;

            while (true)
            {
                if (Kernel32.GetQueuedCompletionStatusRio(socketIocp, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                    SocketIocpOk(allSockets[lpOverlapped->SocketIndex], lpOverlapped->Status);
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    RioConnectionOrientedSocket socket = null;
                    byte status = 0;

                    if (lpOverlapped != lpOverlappedNull)
                    {
                        socket = allSockets[lpOverlapped->SocketIndex];
                        status = lpOverlapped->Status;
                    }

                    if (SocketIocpError(error, socket, status))
                        break;
                }
            }
        }

        protected abstract bool SocketIocpOk(RioConnectionOrientedSocket socket, byte status);

        protected abstract bool SocketIocpError(int error, RioConnectionOrientedSocket socket, byte status);

        bool disconnectInProgress = false;

        internal void EndRecycle(RioConnectionOrientedSocket socket, bool async)
        {
            //lock (this)
            //{
            //    if (disconnectInProgress)
            //        return;
            //    else
            //        disconnectInProgress = true;
            //}

            RioConnectionOrientedSocket s;
            disconnectingSockets.TryRemove(socket.GetHashCode(), out s);

            //RioStatic.Notify(ReceiveCompletionQueue);
            //unsafe
            //{
            //    uint maxResults = Math.Min(MaxOutstandingSend, int.MaxValue);
            //    RIO_RESULT* results = stackalloc RIO_RESULT[(int)maxResults];
            //    var count = RioStatic.DequeueCompletion(ReceiveCompletionQueue, results, maxResults);
            //}
            //socket.ResetSocket();

            //bool gotRest = false;
            //int attempts = 0;
            //while ((socket.StartedReceives - socket.FinishdedReceives) > 0 || (socket.StartedSends - socket.FinishdedSends) > 0)
            //{
            //    await Task.Delay(100);
            //    attempts++;
            //    if (attempts > 10 && !gotRest)
            //    {
            //        gotRest = true;
            //        socket.ResetSocket();
            //        if ((Kernel32.CreateIoCompletionPort(socket.Socket, socketIocp, 0, 1)) == IntPtr.Zero)
            //            Kernel32.ThrowLastError();
            //        break;
            //    }
            //}

            //if (gotRest)
            //    InitializeSocket(socket);

            //socket.StartedReceives = 0;
            //socket.StartedSends = 0;
            //socket.FinishdedReceives = 0;
            //socket.FinishdedSends = 0;

            socket.pendingRecives = 0;
            socket.pendingSends = 0;
            FinalizeRecycle(socket);

            //lock (this)
            //{
            //    disconnectInProgress = false;
            //}
        }




        internal abstract void FinalizeRecycle(RioConnectionOrientedSocket socket);

        internal abstract void InitializeSocket(RioConnectionOrientedSocket socket);


        bool disconnectstarted = false;


        internal unsafe virtual void BeginRecycle(RioConnectionOrientedSocket socket, bool force)
        {
            lock (this)
            {
                if (disconnectInProgress || disconnectstarted)
                    return;
                else
                    disconnectstarted = true;
            }

            //var res = Kernel32.CancelIoEx(socket.Socket, (NativeOverlapped*)0);
            //Kernel32.ThrowLastError();

            //socket.SetLinger(0);

            RioConnectionOrientedSocket c;
            activeSockets.TryRemove(socket.GetHashCode(), out c);


            if (force || socket.Socket == IntPtr.Zero || socket.pendingRecives > 0 || socket.pendingSends > 0)
            {
                socket.ResetSocket();
                if ((Kernel32.CreateIoCompletionPort(socket.Socket, socketIocp, 0, 1)) == IntPtr.Zero)
                    Kernel32.ThrowLastError();
                InitializeSocket(socket);

                Stopwatch s = new Stopwatch();
                while (socket.pendingRecives > 0 || socket.pendingSends > 0)
                {
                    if (s.ElapsedMilliseconds > 2000)
                    {
                        break;
                    }
                }

                EndRecycle(socket, false);
            }
            else
            {
                disconnectingSockets.TryAdd(socket.GetHashCode(), socket);

                //socket.ResetOverlapped();
                socket.disconnectStartTime = RioSocketPool.CurrentTime;

                socket._overlapped->Status = 1;
                if (!RioStatic.DisconnectEx(socket.Socket, socket._overlapped, WinSock.TF_REUSE_SOCKET, 0))
                {
                    var error = WinSock.WSAGetLastError();
                    if (error == WinSock.WSAENOTCONN || error == 10038)
                        BeginRecycle(socket, true);

                    else
                        WinSock.ThrowLastWSAError();
                }
            }


            lock (this)
            {
                disconnectstarted = false;
            }
        }

        public override void Dispose()
        {
            running = false;
            timouttcs.Task.Wait();

            for (int i = 0; i < allSockets.Length; i++)
                allSockets[i].Close();
            Kernel32.CloseHandle(socketIocp);

            base.Dispose();
        }
    }
}
