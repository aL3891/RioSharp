using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioSocket : IDisposable
    {
        internal IntPtr _socket;
        internal RioSocketPool _pool;
        internal IntPtr _requestQueue;
        public AwaitableQueue<RioBufferSegment> incommingSegments = new AwaitableQueue<RioBufferSegment>();

        public RioSocket(IntPtr socket, RioSocketPool pool)
        {
            _socket = socket;
            _pool = pool;
            _requestQueue = RioStatic.CreateRequestQueue(_socket, _pool.MaxOutstandingReceive, 1, _pool.MaxOutstandingSend, 1, _pool.ReceiveCompletionQueue, _pool.SendCompletionQueue, GetHashCode());
            Imports.ThrowLastWSAError();
            ReciveInternal(); ReciveInternal();
        }

        public unsafe void WritePreAllocated(RioBufferSegment Segment)
        {
            var currentBuffer = Segment.internalSegment;
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, RIO_SEND_FLAGS.NONE, Segment.Index))
                Imports.ThrowLastWSAError();
        }
        
        internal unsafe void CommitSend()
        {
            if (!RioStatic.Send(_requestQueue, null, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void SendInternal(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            var currentBuffer = segment.internalSegment;
            if (!RioStatic.Send(_requestQueue, &currentBuffer, 1, flags, segment.Index))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void ReciveInternal()
        {
            var buf = _pool.ReciveBufferPool.GetBuffer();
            var currentBuffer = buf.internalSegment;
            if (!RioStatic.Receive(_requestQueue, &currentBuffer, 1, RIO_RECEIVE_FLAGS.NONE, buf.Index))
                Imports.ThrowLastWSAError();
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

            RioBufferSegment s;
            while (incommingSegments.TryDequeue(out s))
                s.Dispose();


            _pool.Recycle(this);
        }
    }
}

