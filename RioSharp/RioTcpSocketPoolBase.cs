using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpSocketPoolBase : RioSocketPoolBase
    {
        public RioTcpSocketPoolBase(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool) : base(sendPool, revicePool)
        {
        }
    }
}
