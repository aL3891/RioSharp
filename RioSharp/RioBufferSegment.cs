using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public sealed  unsafe class RioBufferSegment : IDisposable
    {
        internal int Index;
        internal int TotalLength;
        internal int CurrentContentLength => segmentPointer->Length;       
        RioFixedBufferPool pool;
        internal bool AutoFree;
        internal byte* rawPointer;
        internal RIO_BUFSEGMENT* segmentPointer;
        
        public RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            this.pool = pool;
            AutoFree = true;

            var offset = index * Length;
            rawPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            segmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUFSEGMENT>()).ToPointer();

            segmentPointer->BufferId = IntPtr.Zero;
            segmentPointer->Offset = offset;
            segmentPointer->Length = TotalLength;
            
        }

        public void SetBufferId(IntPtr id)
        {
            segmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            AutoFree = true;
            segmentPointer->Length = TotalLength;
            pool.ReleaseBuffer(this);
        }
    }
}
