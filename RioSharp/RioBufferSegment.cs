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
        internal uint CurrentContentLength => segmentPointer->Length;
        internal uint Offset;
        RioFixedBufferPool pool;
        internal bool AutoFree;
        internal byte* rawPointer;
        internal RIO_BUFSEGMENT* segmentPointer;


        public RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, uint index, uint Length)
        {
            Index = index;
            totalLength = Length;
            this.pool = pool;
            AutoFree = true;

            Offset = index * Length;
            rawPointer = (byte*)(bufferStartPointer + (int)Offset).ToPointer();
            segmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + ((int)index * Marshal.SizeOf<RIO_BUFSEGMENT>())).ToPointer();

            segmentPointer->BufferId = IntPtr.Zero;
            segmentPointer->Offset = Offset;
            segmentPointer->Length = totalLength;
            
        }

        public void SetBufferId(IntPtr id)
        {
            segmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            AutoFree = true;
            segmentPointer->Length = totalLength;
            pool.ReleaseBuffer(this);
        }
    }
}
