using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioSocketPoolBase : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool, ReciveBufferPool;
        internal IntPtr _sendBufferId, _reciveBufferId;
        internal IntPtr SendCompletionPort, SendCompletionQueue, ReceiveCompletionPort, ReceiveCompletionQueue;
        internal uint MaxOutstandingReceive, MaxOutstandingSend, MaxConnections, MaxOutsandingCompletions;

        internal ConcurrentDictionary<long, RioSocketBase> connections = new ConcurrentDictionary<long, RioSocketBase>();
        public static long dontFree = 1 << 63;

        public unsafe RioSocketPoolBase(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
        {
            MaxOutstandingReceive = maxOutstandingReceive;
            MaxOutstandingSend = maxOutstandingSend;
            MaxConnections = maxConnections;
            MaxOutsandingCompletions = (MaxOutstandingReceive + MaxOutstandingSend) * MaxConnections;

            SendBufferPool = sendPool;
            ReciveBufferPool = revicePool;

            var version = new Version(2, 2);
            WSAData data;
            var result = Imports.WSAStartup((short)version.Raw, out data);
            if (result != 0)
                Imports.ThrowLastWSAError();

            RioStatic.Initalize();

            if ((ReceiveCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();

            if ((SendCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();


            _sendBufferId = RioStatic.RegisterBuffer(SendBufferPool.BufferPointer, SendBufferPool.TotalLength);
            Imports.ThrowLastWSAError();
            SendBufferPool.SetBufferId(_sendBufferId);

            _reciveBufferId = RioStatic.RegisterBuffer(ReciveBufferPool.BufferPointer, ReciveBufferPool.TotalLength);
            Imports.ThrowLastWSAError();
            ReciveBufferPool.SetBufferId(_reciveBufferId);

            var sendCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = SendCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((SendCompletionQueue = RioStatic.CreateCompletionQueue((uint)MaxOutsandingCompletions, sendCompletionMethod)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            var receiveCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = ReceiveCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((ReceiveCompletionQueue = RioStatic.CreateCompletionQueue((uint)MaxOutsandingCompletions, receiveCompletionMethod)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();


            Thread reciveThread = new Thread(ProcessReceiveCompletes);
            reciveThread.IsBackground = true;
            reciveThread.Start();
            Thread sendThread = new Thread(ProcessSendCompletes);
            sendThread.IsBackground = true;
            sendThread.Start();
        }
        
        public unsafe RioBufferSegment PreAllocateWrite(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, (byte*)currentSegment.Pointer.ToPointer(), SendBufferPool.SegmentLength, buffer.Length);
            }
            currentSegment.AutoFree = false;
            return currentSegment;
        }
        
        unsafe void ProcessReceiveCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            RioSocketBase connection;
            uint count, key, bytes;
            NativeOverlapped* overlapped;
            RIO_RESULT result;
            RioBufferSegment buf;

            while (true)
            {
                RioStatic.Notify(ReceiveCompletionQueue);
                Imports.ThrowLastWSAError();

                if (Imports.GetQueuedCompletionStatus(ReceiveCompletionPort, out bytes, out key, out overlapped, -1))
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(ReceiveCompletionQueue, (IntPtr)results, maxResults);
                        Imports.ThrowLastWSAError();

                        for (var i = 0; i < count; i++)
                        {
                            result = results[i];
                            buf = ReciveBufferPool.allSegments[result.RequestCorrelation];
                            if (connections.TryGetValue(result.ConnectionCorrelation, out connection))
                            {
                                buf.CurrentLength = result.BytesTransferred;
                                connection.incommingSegments.Enqueue(buf);
                                connection.ReciveInternal();
                            }
                            else
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Imports.ThrowLastError();
            }
        }

        unsafe void ProcessSendCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            uint count, key, bytes;
            NativeOverlapped* overlapped;

            while (true)
            {
                RioStatic.Notify(SendCompletionQueue);
                if (Imports.GetQueuedCompletionStatus(SendCompletionPort, out bytes, out key, out overlapped, -1))
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(SendCompletionQueue, (IntPtr)results, maxResults);
                        Imports.ThrowLastWSAError();
                        for (var i = 0; i < count; i++)
                        {
                            var buf = SendBufferPool.allSegments[results[i].RequestCorrelation];
                            if (buf.AutoFree)
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Imports.ThrowLastError();
            }
        }

        internal void Recycle(RioSocketBase socket)
        {
            Imports.closesocket(socket._socket);
            Imports.ThrowLastWSAError();

            RioSocketBase c;
            connections.TryRemove(socket.GetHashCode(), out c);
        }

        public virtual void Dispose()
        {
            RioStatic.DeregisterBuffer(_sendBufferId);
            RioStatic.DeregisterBuffer(_reciveBufferId);

            Imports.WSACleanup();

            SendBufferPool.Dispose();
            ReciveBufferPool.Dispose();
        }
    }
}
