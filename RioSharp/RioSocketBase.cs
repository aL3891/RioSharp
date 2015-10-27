using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RioSharp
{
    public class RioSocketBase : IDisposable
    {
        internal IntPtr _socket;
        internal RioSocketPoolBase _pool;
        internal IntPtr _requestQueue;
        internal BufferBlock<BufferSegment> incommingSegments = new BufferBlock<BufferSegment>();
        
        public RioSocketBase(IntPtr socket, RioSocketPoolBase pool)
        {
            _socket = socket;
            _pool = pool;
            _requestQueue = RioStatic.CreateRequestQueue(_socket, _pool.MaxOutstandingReceive, 1, _pool.MaxOutstandingSend, 1, _pool.ReceiveCompletionQueue, _pool.SendCompletionQueue, GetHashCode());
            Imports.ThrowLastWSAError();

        }


        public void WritePreAllocated(RIO_BUFSEGMENT Segment)
        {
            _pool.WritePreAllocated(Segment, _requestQueue);
        }

        public void WriteFixed(byte[] buffer)
        {
            _pool.WriteFixed(buffer, _requestQueue);
        }

        public virtual void Dispose()
        {
            incommingSegments.Complete();
            IList<BufferSegment> segments;
            incommingSegments.TryReceiveAll(out segments);
            if (segments != null)
                foreach (var s in segments)
                    _pool.ReciveBufferPool.ReleaseBuffer(s.Segment);

            _pool.Recycle(this);
        }
    }
}
