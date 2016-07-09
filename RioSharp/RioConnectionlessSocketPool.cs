using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioConnectionlessSocketPool : RioSocketPool
    {
        public RioConnectionlessSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxSockets = 128, int adressBufferSize = 1024)
            : base(sendPool, revicePool, adressFam, sockType, protocol, maxOutstandingReceive, maxOutstandingSend, maxSockets, adressBufferSize)
        {

        }

        public RioConnectionlessSocket Create()
        {
            return new RioConnectionlessSocket(this, SendBufferPool, ReceiveBufferPool, adressBufferPool, MaxOutstandingReceive, MaxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue, adressFam, sockType, protocol);
        }

        public RioConnectionlessSocket Bind(IPEndPoint localEP)
        {
            var socket = Create();

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = localEP.Address.GetAddressBytes()[0];
            inAddress.s_b2 = localEP.Address.GetAddressBytes()[1];
            inAddress.s_b3 = localEP.Address.GetAddressBytes()[2];
            inAddress.s_b4 = localEP.Address.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = WinSock.htons((ushort)localEP.Port);
            sa.sin_addr = inAddress;

            unsafe
            {
                if (WinSock.bind(socket.Socket, ref sa, sizeof(sockaddr_in)) == WinSock.SOCKET_ERROR)
                    WinSock.ThrowLastWSAError();
            }

            return socket;
        }
    }
}
