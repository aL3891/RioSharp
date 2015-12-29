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
        ConcurrentQueue<RioBufferSegment> _availableSegments = new ConcurrentQueue<RioBufferSegment>();
        internal RioBufferSegment[] allSegments;

        public RioFixedBufferPool(uint segmentCount, uint segmentLength)
        {
            
            allSegments = new RioBufferSegment[segmentCount];
            SegmentLength = segmentLength;
            TotalLength = segmentCount * segmentLength;
            BufferPointer = Marshal.AllocHGlobal((int)TotalLength);

            for (uint i = 0; i < segmentCount; i++)
            {
                var b = new RioBufferSegment(this, BufferPointer + (int)(i * SegmentLength), i, SegmentLength, (i * SegmentLength));
                allSegments[i] = b;
                _availableSegments.Enqueue(b);
            }
        }

        public void SetBufferId(IntPtr id)
        {
            for (int i = 0; i < allSegments.Length; i++)
                allSegments[i].SetBufferId(id);
        }

        public bool TryGetBuffer(out RioBufferSegment buf)
        {
            return _availableSegments.TryDequeue(out buf);          
        }

        public RioBufferSegment GetBuffer()
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryDequeue(out buf))
                    return buf;
            } while (true);
        }

        public RioBufferSegment GetBuffer(int requestedBufferSize)
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryDequeue(out buf))
                    return buf;
            } while (true);
        }

        public void ReleaseBuffer(RioBufferSegment bufferIndex)
        {
            _availableSegments.Enqueue(bufferIndex);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(BufferPointer);
        }
    }
}
