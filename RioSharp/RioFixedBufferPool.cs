using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;


namespace RioSharp
{
    public class RioFixedBufferPool : IDisposable
    {
        ConcurrentQueue<RioBufferSegment> _availableSegments = new ConcurrentQueue<RioBufferSegment>();
        IntPtr _segmentpointer;

        internal IntPtr BufferPointer { get; set; }
        internal int TotalLength { get; set; }
        internal RioBufferSegment[] AllSegments;

        public RioFixedBufferPool(int segmentCount, int segmentLength)
        {
            AllSegments = new RioBufferSegment[segmentCount];
            TotalLength = segmentCount * segmentLength;
            //BufferPointer = Marshal.AllocHGlobal(TotalLength);
            BufferPointer = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)TotalLength, 0x00001000 | 0x00002000, 0x04);

            //_segmentpointer = Marshal.AllocHGlobal(Marshal.SizeOf<RIO_BUFSEGMENT>() * segmentCount);
            _segmentpointer = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)(Marshal.SizeOf<RIO_BUFSEGMENT>() * segmentCount), 0x00001000 | 0x00002000, 0x04);

            for (int i = 0; i < segmentCount; i++)
            {
                var b = new RioBufferSegment(this, BufferPointer ,_segmentpointer, i, segmentLength );
                AllSegments[i] = b;
                _availableSegments.Enqueue(b);
            }
        }

        public void SetBufferId(IntPtr id)
        {
            for (int i = 0; i < AllSegments.Length; i++)
                AllSegments[i].SetBufferId(id);
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

        public void ReleaseBuffer(RioBufferSegment buffer)
        {
            _availableSegments.Enqueue(buffer);
        }

        public void Dispose()
        {
            //Marshal.FreeHGlobal(BufferPointer);
            //Marshal.FreeHGlobal(_segmentpointer);

            Kernel32.VirtualFree(BufferPointer, 0, 0x8000);
            Kernel32.VirtualFree(_segmentpointer, 0, 0x8000);
        }
    }
}
