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
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();


            return null;
        }
    }
}
