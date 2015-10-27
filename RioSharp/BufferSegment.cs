using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class BufferSegment
    {
        public BufferSegment(uint segment, uint length)
        {
            Segment = segment;
            Length = length;
        }

        internal uint Segment;
        internal uint Length;
    }
}
