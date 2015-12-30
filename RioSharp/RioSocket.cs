using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioSocket : IDisposable
    {
        internal IntPtr _socket;
        internal RioSocketPool _pool;
        internal IntPtr _requestQueue;
        public AwaitableQueue2<RioBufferSegment> incommingSegments = new AwaitableQueue2<RioBufferSegment>();


        public RioSocket(IntPtr socket, RioSocketPool pool)
        {
            _socket = socket;
            _pool = pool;
            _requestQueue = RioStatic.CreateRequestQueue(_socket, _pool.MaxOutstandingReceive, 1, _pool.MaxOutstandingSend, 1, _pool.ReceiveCompletionQueue, _pool.SendCompletionQueue, GetHashCode());
            Imports.ThrowLastWSAError();
        }

        public unsafe void WritePreAllocated(RioBufferSegment Segment)
        {
            var currentBuffer = Segment.internalSegment;
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, RIO_SEND_FLAGS.NONE, Segment.Index))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void CommitSend()
        {
            if (!RioStatic.Send(_requestQueue, RIO_BUFSEGMENT.NullSegment, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void SendInternal(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            var currentBuffer = segment.internalSegment;
            currentBuffer.Length = segment.ContentLength;
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, flags, segment.Index))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void ReciveInternal()
        {
            RioBufferSegment buf;
            if (_pool.ReciveBufferPool.TryGetBuffer(out buf))
            {
                var currentBuffer = buf.internalSegment;
                if (!RioStatic.Receive(_requestQueue, &currentBuffer, 1, RIO_RECEIVE_FLAGS.NONE, buf.Index))
                    Imports.ThrowLastWSAError();
            }
            else
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var b = _pool.ReciveBufferPool.GetBuffer();
                    var currentBuffer = buf.internalSegment;
                    if (!RioStatic.Receive(_requestQueue, &currentBuffer, 1, RIO_RECEIVE_FLAGS.NONE, b.Index))
                        Imports.ThrowLastWSAError();
                }, null);

        }

        public unsafe void WriteFixed(byte[] buffer)
        {
            var currentSegment = _pool.SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, (byte*)currentSegment.Pointer.ToPointer(), currentSegment.totalLength, buffer.Length);
            }

            SendInternal(currentSegment, RIO_SEND_FLAGS.NONE);
        }

        public virtual void Dispose()
        {
            incommingSegments.Clear(s => s?.Dispose());
            _pool.Recycle(this);
        }
    }
}

