using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
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
        
        /// <summary>
        /// Binds a socket to the default ip and port
        /// </summary>
        /// <returns></returns>
        public RioConnectionlessSocket Bind()
        {
            return Bind(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 0));
        }

        /// <summary>
        /// Binds a socket to a local ip and port, 
        /// for multicast applications sending to a destinations on the same machine, use port 0
        /// </summary>
        /// <param name="localEP"></param>
        /// <returns></returns>
        public unsafe RioConnectionlessSocket Bind(IPEndPoint localEP)
        {
            var socket = new RioConnectionlessSocket(this, SendBufferPool, ReceiveBufferPool, adressBufferPool, MaxOutstandingReceive, MaxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue, adressFam, sockType, protocol); 
            
            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = WinSock.htons((ushort)localEP.Port);
            var ipBytes = localEP.Address.GetAddressBytes();
            fixed (byte* a = ipBytes)
                Unsafe.CopyBlock(sa.sin_addr.Address, a, (uint)ipBytes.Length);

            unsafe
            {
                if (WinSock.bind(socket.Socket, ref sa, sizeof(sockaddr_in)) == WinSock.SOCKET_ERROR)
                    WinSock.ThrowLastWSAError();
            }

            socket.SetInUse(true);
            return socket;
        }
    }
}
