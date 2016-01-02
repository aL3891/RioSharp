using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public unsafe class RioBufferSegment : IDisposable
    {
        //internal IntPtr Pointer;
        internal uint Index;
        internal uint totalLength;
        internal uint CurrentContentLength;
        internal uint Offset;
        RioFixedBufferPool pool;
        internal RIO_BUFSEGMENT internalSegment;
        internal bool AutoFree;
        internal byte* rawPointer;
        internal RIO_BUFSEGMENT* segmentPointer;


        public RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, uint index, uint Length)
        {
            Index = index;
            totalLength = Length;
            this.pool = pool;
            AutoFree = true;
            CurrentContentLength = 0;

            Offset = index * Length;
            rawPointer = (byte*)(bufferStartPointer + (int)Offset).ToPointer();
            this.segmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + ((int)index * Marshal.SizeOf<RIO_BUFSEGMENT>())).ToPointer();

        }

        public RioBufferSegment(RioFixedBufferPool pool, IntPtr pointer, uint index, uint totalLength, uint offset)
        {
            //Pointer = pointer;
            rawPointer = (byte*)pointer.ToPointer();
            Index = index;
            this.totalLength = totalLength;
            CurrentContentLength = 0;
            Offset = offset;
            this.pool = pool;
            AutoFree = true;
        }

        public void SetBufferId(IntPtr id)
        {
            //segmentPointer->BufferId = id;
            internalSegment = new RIO_BUFSEGMENT(id, Offset, totalLength);
        }

        public void Dispose()
        {
            AutoFree = true;
            CurrentContentLength = 0;
            pool.ReleaseBuffer(this);
        }
    }
}
