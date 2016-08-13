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
        private static RioTcpClientPool clientPool;
        private static ManualResetEvent e;
        private static RioTcpListener listener;
        private static bool running = true;
        private static int operations;

        static void Main(string[] args)
        {

            clientPool = new RioTcpClientPool(new RioFixedBufferPool(100, 100), new RioFixedBufferPool(10, 100), 4096);
            listener = new RioTcpListener(new RioFixedBufferPool(100, 100), new RioFixedBufferPool(100, 100), 4096);
            listener.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000), 1024);
            e = new ManualResetEvent(false);

            var task = Task.Run((Action)clientDisconnect);
            Log();
            Console.ReadLine();
            running = false;
            task.Wait();
            clientPool.Dispose();
            listener.Dispose();
        }

        public static async Task Log()
        {
            while (running)
            {

                await Task.Delay(1000);
                Console.WriteLine(operations);
                operations = 0;
            }
        }

        public static void clientDisconnect()
        {
            listener.OnAccepted = (RioSocket s) =>
            {
                while (true)
                    s.BeginReceive().GetResult().Dispose();

                s.Dispose();
            };
            while (running)
            {
                try
                {
                    //    Thread.Sleep(100);
                    var socket = clientPool.Connect(new Uri("http://localhost:5000/")).Result;
                    //socket.Dispose();
                    while (true)
                        socket.Send(new byte[] { 1, 2, 3 });

                    operations++;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }


        }


    }
}
