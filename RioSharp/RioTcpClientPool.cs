using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClientPool : RioSocketPoolBase
    {
        public unsafe RioTcpClientPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool) : base(sendPool, revicePool)
        {

        }

        public RioTcpConnection Connect(Uri adress)
        {
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            var apa = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily== AddressFamily.InterNetwork);
            
            in_addr inAddress = new in_addr();
            inAddress.s_b1 = apa.GetAddressBytes()[0];
            inAddress.s_b2 = apa.GetAddressBytes()[1];
            inAddress.s_b3 = apa.GetAddressBytes()[2];
            inAddress.s_b4 = apa.GetAddressBytes()[3];


            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)adress.Port);
            Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            unsafe
            {
                if (Imports.connect(sock, ref sa, sizeof(sockaddr_in)) == Imports.SOCKET_ERROR)
                    Imports.ThrowLastWSAError();
            }

            return new RioTcpConnection(sock, this);
        }
    }
}
