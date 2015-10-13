using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RioSharp
{
    public class RioSocketPoolBase : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool;
        internal RioFixedBufferPool ReciveBufferPool;
        internal IntPtr _sendBufferId;
        internal IntPtr _reciveBufferId;

        internal IntPtr SendCompletionPort;
        internal IntPtr SendCompletionQueue;
        internal IntPtr ReceiveCompletionPort;
        internal IntPtr ReceiveCompletionQueue;

        public int MaxOutsandingCompletions = 1024 * 128;
        public uint MaxOutstandingReceive = 512;
        public uint MaxOutstandingSend = 512;
        public uint MaxConnections = 512;

        internal ConcurrentDictionary<long, RioTcpConnection> connections = new ConcurrentDictionary<long, RioTcpConnection>();
        public static long dontFree = 1 << 63;

        public unsafe RioSocketPoolBase(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool)
        {
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

            _reciveBufferId = RioStatic.RegisterBuffer(ReciveBufferPool.BufferPointer, ReciveBufferPool.TotalLength);
            Imports.ThrowLastWSAError();

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


        public unsafe void WriteFixed(byte[] buffer, IntPtr _requestQueue)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, (byte*)SendBufferPool.BufferPointer.ToPointer() + currentSegment, SendBufferPool.SegmentLength, buffer.Length);
            }

            SendInternal(currentSegment, (uint)buffer.Length, RIO_SEND_FLAGS.NONE, _requestQueue);
        }

        public unsafe RIO_BUFSEGMENT PreAllocateWrite(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, (byte*)SendBufferPool.BufferPointer.ToPointer() + currentSegment, SendBufferPool.SegmentLength, buffer.Length);
            }
            return new RIO_BUFSEGMENT(_sendBufferId, currentSegment, (uint)buffer.Length);
        }

        public void FreePreAllocated(RIO_BUFSEGMENT segment) {
            SendBufferPool.ReleaseBuffer(segment.Offset);
        }

        public unsafe void WritePreAllocated(RIO_BUFSEGMENT Segment, IntPtr _requestQueue)
        {
            var currentBuffer = Segment;
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, RIO_SEND_FLAGS.NONE, dontFree | Segment.Offset))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void CommitSend(IntPtr _requestQueue)
        {
            if (!RioStatic.Send(_requestQueue, null, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void SendInternal(uint segment, uint length, RIO_SEND_FLAGS flags, IntPtr _requestQueue)
        {
            var currentBuffer = new RIO_BUFSEGMENT(_sendBufferId, segment, length);
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, flags, segment))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void ReciveInternal(IntPtr _requestQueue)
        {
            var currentBuffer = new RIO_BUFSEGMENT(_reciveBufferId, ReciveBufferPool.GetBuffer(), ReciveBufferPool.SegmentLength);
            if (!RioStatic.Receive(_requestQueue, ref currentBuffer, 1, RIO_RECEIVE_FLAGS.NONE, currentBuffer.Offset))
                Imports.ThrowLastWSAError();
        }

        unsafe void ProcessReceiveCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            RioTcpConnection connection;
            uint count, key, bytes;
            NativeOverlapped* overlapped;
            RIO_RESULT result;


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
                            if (connections.TryGetValue(result.ConnectionCorrelation, out connection))
                                connection.incommingSegments.Post(new BufferSegment((uint)result.RequestCorrelation, result.BytesTransferred));
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
                            if ((results[i].RequestCorrelation & dontFree) != dontFree)
                                SendBufferPool.ReleaseBuffer((uint)results[i].RequestCorrelation);
                        }


                    } while (count > 0);
                }
                else
                    Imports.ThrowLastError();
            }
        }

        internal void Recycle(IntPtr socket)
        {

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
