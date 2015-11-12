using RioSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApplication2
{
    public class Program
    {
        static readonly string responseStr = "GET / HTTP/1.1\r\n" +
            "Host: localhost:5000\r\n" +
            "Connection: Keep-Alive\r\n" +
            "\r\n";

        static byte[] _requestBytes = Encoding.UTF8.GetBytes(responseStr);
        private static RioSocketPool clientPool;
        private static Uri uri;
        private static bool keepAlive;
        private static int pipeLineDeph;
        private static Stopwatch timer;
        private static TimeSpan span;
        private static byte[] rb;

        static void Main(string[] args)
        {
            pipeLineDeph = int.Parse(args.FirstOrDefault(f => f.StartsWith("-p"))?.Substring(2) ?? "1");
            clientPool = new RioSocketPool(new RioFixedBufferPool(1000, (uint)(64 * pipeLineDeph)), new RioFixedBufferPool(1000, (uint)(140 * pipeLineDeph)));
            int connections = int.Parse(args.FirstOrDefault(f => f.StartsWith("-c"))?.Substring(2) ?? "1");
            timer = new Stopwatch();
            span = TimeSpan.FromSeconds(int.Parse(args.FirstOrDefault(f => f.StartsWith("-d"))?.Substring(2) ?? "10"));
            timer.Start();
            uri = new Uri(args.First(a => !a.StartsWith("-")));
            keepAlive = true;

            rb = Enumerable.Repeat(_requestBytes, pipeLineDeph).SelectMany(b => b).ToArray();
            var tasks = Enumerable.Range(0, connections).Select(t => Task.Run(doit));

            var ss = tasks.Sum(t => t.Result);
            Console.WriteLine(ss / span.TotalSeconds);
        }

        public async static Task<int> doit()
        {
            {
                var buffer = new byte[140 * pipeLineDeph];
                var leftoverLength = 0;
                var oldleftoverLength = 0;
                uint endOfRequest = 0x0a0d0a0d;
                uint current = 0;
                int responses = 0;
                int total = 0;

                var connection = clientPool.Connect(uri);
                var stream = new RioStream(connection);
                while (timer.Elapsed < span)
                {
                    //check if connection is valid?                
                    connection.WriteFixed(rb);

                    while (responses < pipeLineDeph)
                    {
                        int r = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (r == 0)
                            break;

                        for (int i = 0; leftoverLength != 0 && i < 4 - leftoverLength; i++)
                        {
                            current += buffer[i];
                            current = current << 8;
                            if (current == endOfRequest)
                                responses++;
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
                                        responses++;
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
                    total += responses;
                    responses = 0;

                    if (!keepAlive)
                        connection.Dispose();
                }
                connection.Dispose();
                return total;
            }
        }
    }
}
