using RioSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sandbox
{
    class Program
    {
        private static ManualResetEvent e;

        static void Main(string[] args)
        {

            var clientPool = new RioTcpClientPool(new RioFixedBufferPool(1, 100), new RioFixedBufferPool(1, 100), 1);
            var listener = new RioTcpListener(new RioFixedBufferPool(1, 100), new RioFixedBufferPool(1, 100), 1);
            listener.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000), 10);
            e = new ManualResetEvent(false);
            listener.OnAccepted = doit;

            var socket = clientPool.Connect(new Uri("http://localhost:5000/")).Result;
            var segment = socket.BeginReceive();
            socket.Dispose();
            //Thread.Sleep(500);
            //socket = clientPool.Connect(new Uri("http://localhost:5000/")).Result;
            //e.Set();
            segment.GetResult();


            //Socket s = null;
            //var se = new SocketAsyncEventArgs { };
            //se.
            //s.ReceiveAsync(se);
        }

        public static void doit(RioSocket s)
        {
            e.WaitOne();
            s.Send(new byte[] { 1, 2, 3 });
        }

    }
}
