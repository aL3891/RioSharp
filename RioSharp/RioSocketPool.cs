using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RioSharp
{
    public abstract class RioSocketPool : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool, ReciveBufferPool;
        internal IntPtr _sendBufferId, _reciveBufferId;
        internal IntPtr SendCompletionPort, SendCompletionQueue, ReceiveCompletionPort, ReceiveCompletionQueue;
        internal uint MaxOutstandingReceive, MaxOutstandingSend, MaxOutsandingCompletions;
        internal ConcurrentDictionary<long, RioSocketBase> connections = new ConcurrentDictionary<long, RioSocketBase>();

        public unsafe RioSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxOutsandingCompletions = 2048)
        {
            MaxOutstandingReceive = maxOutstandingReceive;
            MaxOutstandingSend = maxOutstandingSend;
            MaxOutsandingCompletions = maxOutsandingCompletions;
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


            _sendBufferId = RioStatic.RegisterBuffer(SendBufferPool.BufferPointer, (uint)SendBufferPool.TotalLength);
            Imports.ThrowLastWSAError();
            SendBufferPool.SetBufferId(_sendBufferId);

            _reciveBufferId = RioStatic.RegisterBuffer(ReciveBufferPool.BufferPointer, (uint)ReciveBufferPool.TotalLength);
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

            if ((SendCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutsandingCompletions, sendCompletionMethod)) == IntPtr.Zero)
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

            if ((ReceiveCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutsandingCompletions, receiveCompletionMethod)) == IntPtr.Zero)
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
                Buffer.MemoryCopy(p, currentSegment.RawPointer, currentSegment.TotalLength, buffer.Length);
            }

            currentSegment.SegmentPointer->Length = buffer.Length;
            currentSegment.AutoFree = false;
            return currentSegment;
        }

        unsafe void ProcessReceiveCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            RioSocketBase connection;
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];
            RIO_RESULT result;
            RioBufferSegment buf;

            while (true)
            {
                RioStatic.Notify(ReceiveCompletionQueue);
                Imports.ThrowLastWSAError();

                if (Imports.GetQueuedCompletionStatus(ReceiveCompletionPort, out bytes, out key, out overlapped, -1) != 0)
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
                                buf.SegmentPointer->Length = (int)result.BytesTransferred;
                                connection.OnIncommingSegment(buf);
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
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];

            while (true)
            {
                RioStatic.Notify(SendCompletionQueue);
                if (Imports.GetQueuedCompletionStatus(SendCompletionPort, out bytes, out key, out overlapped, -1) != 0)
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
