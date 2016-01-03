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
        internal int Index;
        internal int totalLength;
        internal int CurrentContentLength => segmentPointer->Length;
        internal int Offset;
        RioFixedBufferPool pool;
        internal bool AutoFree;
        internal byte* rawPointer;
        internal RIO_BUFSEGMENT* segmentPointer;


        public RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            totalLength = Length;
            this.pool = pool;
            AutoFree = true;

            Offset = index * Length;
            rawPointer = (byte*)(bufferStartPointer + Offset).ToPointer();
            segmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUFSEGMENT>()).ToPointer();

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
