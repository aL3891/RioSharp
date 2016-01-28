using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioUdpClient : RioSocketPool
    {
        public RioUdpClient(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, int socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxOutsandingCompletions = 1024)
            : base(sendPool, revicePool, maxOutstandingReceive, maxOutstandingSend, maxOutsandingCompletions)
        {

        }

        public RioTcpSocket BindUdpSocket()
        {
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            //var res = new RioSocket(sock, this);
            //res.ReciveInternal();
            //return res;
            return null;
        }

        internal void Recycle(RioTcpSocket socket)
        {
            Imports.closesocket(socket._socket);
        }
    }
}
