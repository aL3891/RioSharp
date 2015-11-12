using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioBufferSegment : IDisposable
    {
        internal IntPtr Pointer;
        internal uint Index;
        internal uint totalLength;
        internal uint CurrentLength;
        internal uint Offset;
        RioFixedBufferPool pool;
        internal RIO_BUFSEGMENT internalSegment;
        internal bool AutoFree;

        public RioBufferSegment(RioFixedBufferPool pool, IntPtr pointer, uint index, uint totalLength, uint offset)
        {
            Pointer = pointer;
            Index = index;
            this.totalLength = totalLength;
            CurrentLength = 0;
            Offset = offset;
            this.pool = pool;
            AutoFree = true;
        }

        public void SetBufferId(IntPtr id)
        {
            internalSegment = new RIO_BUFSEGMENT(id, Offset, totalLength);
        }

        public void Dispose()
        {
            pool.ReleaseBuffer(this);
        }
    }
}
