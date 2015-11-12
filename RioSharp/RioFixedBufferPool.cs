using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;


namespace RioSharp
{



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
