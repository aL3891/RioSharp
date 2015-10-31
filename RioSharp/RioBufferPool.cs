using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

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

    public class RioFixedBufferPool : IDisposable
    {
        internal IntPtr BufferPointer;
        internal uint SegmentLength;
        internal uint TotalLength;
        ConcurrentStack<RioBufferSegment> _availableSegments = new ConcurrentStack<RioBufferSegment>();
        internal RioBufferSegment[] allSegments;

        public RioFixedBufferPool(uint segmentCount, uint segmentLength)
        {
            allSegments = new RioBufferSegment[segmentCount];
            SegmentLength = segmentLength;
            TotalLength = segmentCount * segmentLength;
            BufferPointer = Marshal.AllocHGlobal(new IntPtr(TotalLength));

            for (uint i = 0; i < segmentCount; i++)
            {
                var b = new RioBufferSegment(this, BufferPointer + (int)(i * SegmentLength), i, SegmentLength, (i * SegmentLength));
                allSegments[i] = b;
                _availableSegments.Push(b);
            }
        }

        public void SetBufferId(IntPtr id)
        {
            for (int i = 0; i < allSegments.Length; i++)
                allSegments[i].SetBufferId(id);
        }

        public RioBufferSegment GetBuffer()
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryPop(out buf))
                    return buf;
            } while (true);
        }

        public RioBufferSegment GetBuffer(int requestedBufferSize)
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryPop(out buf))
                    return buf;
            } while (true);
        }

        public void ReleaseBuffer(RioBufferSegment bufferIndex)
        {
            _availableSegments.Push(bufferIndex);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(BufferPointer);
        }
    }
}
