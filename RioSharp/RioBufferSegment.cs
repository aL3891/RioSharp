using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public sealed unsafe class RioBufferSegment : IDisposable
    {
        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;
        internal bool AutoFree;
        internal byte* RawPointer;
        internal RIO_BUFSEGMENT* SegmentPointer;
        private bool complete;
        private Action _continuation;
        internal bool trackCompletion;

        public byte* Datapointer => RawPointer;

        public int GetData(byte[] data, int offset)
        {
            var l = Math.Min((data.Length - offset), CurrentContentLength);
            Marshal.Copy(new IntPtr(RawPointer), data, offset, l);
            return l;
        }

        internal RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            _pool = pool;
            AutoFree = true;

            var offset = index * Length;
            RawPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            SegmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUFSEGMENT>()).ToPointer();

            SegmentPointer->BufferId = IntPtr.Zero;
            SegmentPointer->Offset = offset;
            SegmentPointer->Length = TotalLength;
        }

        internal void SetBufferId(IntPtr id)
        {
            SegmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            AutoFree = true;
            _continuation = null;
            complete = false;
            trackCompletion = false;


            SegmentPointer->Length = TotalLength;
            _pool.ReleaseBuffer(this);
        }



        public bool IsCompleted
        {
            get
            {
                return complete;
            }
        }

        public void Set(RioBufferSegment item)
        {
            _continuation?.Invoke();
        }

        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
        }
        
        public RioBufferSegment GetResult()
        {
            return this;
        }

        public RioBufferSegment GetAwaiter() => this;

    }
}
