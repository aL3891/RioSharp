using RioSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

namespace UdpSample
{
    public class Program
    {
        public static unsafe void Main(string[] args)
        {

            var sendPool = new RioFixedBufferPool(10, 256);
            var recivePool = new RioFixedBufferPool(10, 256);

            var pool = new RioConnectionlessSocketPool(sendPool, recivePool, ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP);

            var sock = pool.Bind(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 3000));


            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            var bajskorv = nics.Where(n => n.Supports(NetworkInterfaceComponent.IPv4)).Select(n => n.GetIPProperties().GetIPv4Properties().Index) ;


            sock.JoinMulticastGroup(new IPAddress(new byte[] { 224, 0, 3, 15 }),0);

            var name = Guid.NewGuid().ToString();

            RioSegmentReader r = new RioSegmentReader(sock);
            r.OnIncommingSegment = segment => Console.WriteLine(Encoding.ASCII.GetString(segment.Datapointer, segment.CurrentContentLength));
            r.Start();

            while (true)
            {
                sock.WriteFixed(Encoding.ASCII.GetBytes("Hi, my name is " + name));
            }

        }
    }
}
