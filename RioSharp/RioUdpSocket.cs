using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioUdpSocket : RioSocketBase
    {
        public RioUdpSocket(RioTcpSocketPool pool) :
            base(pool.SendBufferPool, pool.ReciveBufferPool, pool.MaxOutstandingReceive, pool.MaxOutstandingSend,
                pool.SendCompletionQueue, pool.ReceiveCompletionQueue,
                ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP)
        {

        }
    }
}
