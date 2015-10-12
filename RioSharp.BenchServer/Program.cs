using RioSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        static readonly string responseStr = "HTTP/1.1 200 OK\r\n" +
    "Content-Type: text/plain;charset=UTF-8\r\n" +
    "Content-Length: 10\r\n" +
    //"Connection: keep-alive\r\n" +
    "Server: Dummy\r\n" +
    "\r\n" +
    "HelloWorld";


        static byte[] _responseBytes = Encoding.UTF8.GetBytes(responseStr);

        static void Main()
        {
            ThreadPool.SetMinThreads(100, 100);
            var l = new RioTcpListener(new RioFixedBufferPool(100, 512), new RioFixedBufferPool(100, 512));
            l.Bind(new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 5000));
            l.Listen(500);
            while (true)
            {
                var socket = l.Accept();
                Task.Run(() => Serve(socket));
            }
        }

        static async Task Serve(RioTcpConnection socket)
        {
            try
            {
                var buffer = new byte[512];
                var leftoverLength = 0;
                var oldleftoverLength = 0;
                uint endOfRequest = 0x0a0d0a0d;
                uint current = 0;

                while (true)
                {
                    int r = await socket.ReadAsync(buffer, 0, buffer.Length);
                    if (r == 0)
                    {
                        Console.WriteLine("quitting");
                        break;
                    }

                    for (int i = 0; leftoverLength != 0 && i < 4 - leftoverLength; i++)
                    {
                        current += buffer[i];
                        current = current << 8;
                        if (current == endOfRequest)
                            socket.WriteFixed(_responseBytes);
                    }

                    leftoverLength = r % 4;
                    var length = r - leftoverLength;

                    unsafe
                    {
                        fixed (byte* apa = &buffer[oldleftoverLength])
                        {
                            var start = apa;
                            var end = apa + length;

                            for (; start <= end; start++)
                            {
                                if (*(uint*)start == endOfRequest)
                                    socket.WriteFixed(_responseBytes);
                            }
                        }
                    }

                    oldleftoverLength = leftoverLength;

                    for (int i = r - leftoverLength; i < r; i++)
                    {
                        current += buffer[i];
                        current = current << 4;
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                socket.Close();
            }
        }

    }
}
