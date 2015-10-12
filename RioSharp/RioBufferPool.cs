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
    public class RioFixedBufferPool : IDisposable
    {
        public IntPtr BufferPointer;
        public uint SegmentLength;
        public uint TotalLength;
        BufferBlock<uint> _availableSegments = new BufferBlock<uint>();

        public RioFixedBufferPool(uint segmentCount, uint segmentLength)
        {
            SegmentLength = segmentLength;
            TotalLength = segmentCount * segmentLength;
            BufferPointer = Marshal.AllocHGlobal(new IntPtr(TotalLength));

            for (uint i = 0; i < segmentCount; i++)
                _availableSegments.Post(i * SegmentLength);
        }

        public uint GetBuffer()
        {
            uint bufferNo;
            if (_availableSegments.TryReceive(out bufferNo))
                return bufferNo;
            else
                return _availableSegments.ReceiveAsync().Result;
        }


        public uint GetBuffer(int requestedBufferSize, out uint actualBufferSize)
        {
            uint bufferNo;
            actualBufferSize = SegmentLength;
            if (_availableSegments.TryReceive(out bufferNo))
                return bufferNo;
            else
                return _availableSegments.ReceiveAsync().Result;
        }


        public void ReleaseBuffer(uint bufferIndex)
        {
            _availableSegments.Post(bufferIndex);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(BufferPointer);
        }
    }
}
