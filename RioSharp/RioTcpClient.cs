using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClient : RioTcpSocketPool
    {
        ConcurrentQueue<RioSocket> freeSockets = new ConcurrentQueue<RioSocket>();

        public RioTcpClient(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, int socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool, socketCount, maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {

        }
        
        public override unsafe void CompleteDisConnect(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];

            while (true)
            {
                if (Imports.GetQueuedCompletionStatusRio(DisconnectCompletionPort, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                    freeSockets.Enqueue(allSockets[lpOverlapped->SocketIndex]);
                else
                    Imports.ThrowLastError();
            }
        }
        
        public unsafe RioSocket Connect(Uri adress)
        {



            var apa = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily == AddressFamily.InterNetwork);

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = apa.GetAddressBytes()[0];
            inAddress.s_b2 = apa.GetAddressBytes()[1];
            inAddress.s_b3 = apa.GetAddressBytes()[2];
            inAddress.s_b4 = apa.GetAddressBytes()[3];


            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)adress.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            RioSocket s;

            freeSockets.TryDequeue(out s);
            
            unsafe
            {
                s.ResetOverlapped();
                if (!RioStatic.ConnectEx(s._socket, sa, sizeof(sockaddr_in), IntPtr.Zero, 0, 0, s._overlapped))
                    Imports.ThrowLastWSAError();
            }

            //var connection = new RioSocket(sock, this);
            //connections.TryAdd(connection.GetHashCode(), connection);
            //connection.ReciveInternal();
            //return connection;

            return null;
        }
    }
}
