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

            this.segmentPointer->BufferId = IntPtr.Zero;
            this.segmentPointer->Offset = Offset;
            this.segmentPointer->Length = totalLength;
            
        }

        public void SetBufferId(IntPtr id)
        {
            segmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            AutoFree = true;
            CurrentContentLength = 0;
            this.segmentPointer->Length = totalLength;
            pool.ReleaseBuffer(this);
        }
    }
}
