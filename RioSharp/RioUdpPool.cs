using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioUdpPool : RioSocketPoolBase
    {
        
        public RioUdpPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool) : base(sendPool, revicePool)
        {
        }

        public RioUdpSocket GetSocket()
        {


            return null;
        }
    }
}
