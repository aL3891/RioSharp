using RioSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp.BenchClient
{
    public class Program
    {
        static string responseStr = "GET / HTTP/1.1\r\n" +
            "Host: localhost:5000\r\n" +
            "\r\n";

        static byte[] _requestBytes;
        private static RioTcpClientPool clientPool;
        private static Uri uri;
        private static bool keepAlive;
        private static int pipeLineDeph;
        private static Stopwatch timer;
        private static TimeSpan span;
        private static byte[] requestBytes;

        static void Main(string[] args)
        {
            pipeLineDeph = int.Parse(args.FirstOrDefault(f => f.StartsWith("-p"))?.Substring(2) ?? "16");
            int connections = int.Parse(args.FirstOrDefault(f => f.StartsWith("-c"))?.Substring(2) ?? "512");
            timer = new Stopwatch();
            span = TimeSpan.FromSeconds(int.Parse(args.FirstOrDefault(f => f.StartsWith("-d"))?.Substring(2) ?? "25"));
            uri = new Uri(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "http://localhost:5000/plaintext");
            keepAlive = bool.Parse(args.FirstOrDefault(f => f.StartsWith("-k"))?.Substring(2) ?? "false"); ;

            Console.WriteLine("RioSharp http benchmark");
            Console.WriteLine("Connections: " + connections);
            Console.WriteLine("Duration: " + span.TotalSeconds + " seconds");
            Console.WriteLine("Pipeline depth: " + pipeLineDeph);
            Console.WriteLine("Target: " + uri);

            responseStr = $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
            $"Host: {uri.Host}:{uri.Port}\r\n" +
            "\r\n";

            _requestBytes = Encoding.ASCII.GetBytes(responseStr);
            requestBytes = Enumerable.Repeat(_requestBytes, pipeLineDeph).SelectMany(b => b).ToArray();

            clientPool = new RioTcpClientPool(new RioFixedBufferPool(10 * connections, requestBytes.Length), new RioFixedBufferPool(10 * connections, (256 * pipeLineDeph)), (uint)connections);
            Console.WriteLine("Benchmarking...");

            timer.Start();
            var tasks = Enumerable.Range(0, connections).Select(t => Task.Run(ExecuteStream)).ToList();

            var totalRequests = tasks.Sum(t => t.Result);
            Console.WriteLine($"Made {totalRequests } requests over {span.TotalSeconds} seconds ({totalRequests / span.TotalSeconds} Rps)");
            clientPool.Dispose();
        }

        public async static Task<int> ExecuteStream()
        {
            var buffer = new byte[256 * pipeLineDeph];
            var leftoverLength = 0;
            var oldleftoverLength = 0;
            uint endOfRequest = 0x0a0d0a0d;
            uint current = 0;
            int responses = 0;
            int total = 0;

            RioSocket connection = null;
            RioStream stream = null;

            while (timer.Elapsed < span)
            {
                if (connection == null)
                {
                    try
                    {
                        connection = await clientPool.Connect(uri);
                        stream = new RioStream(connection);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        continue;
                    }
                }

                //check if connection is valid?                
                connection.Send(requestBytes);

                while (responses < pipeLineDeph)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    for (int i = 0; leftoverLength != 0 && i < 4 - leftoverLength; i++)
                    {
                        current += buffer[i];
                        current = current << 8;
                        if (current == endOfRequest)
                            responses++;
                    }

                    leftoverLength = bytesRead % 4;
                    var length = bytesRead - leftoverLength;

                    unsafe
                    {
                        fixed (byte* data = &buffer[oldleftoverLength])
                        {
                            var start = data;
                            var end = data + length;

                            for (; start <= end; start++)
                            {
                                if (*(uint*)start == endOfRequest)
                                    responses++;
                            }
                        }
                    }

                    oldleftoverLength = leftoverLength;

                    for (int i = bytesRead - leftoverLength; i < bytesRead; i++)
                    {
                        current += buffer[i];
                        current = current << 4;
                    }

                }
                total += responses;
                responses = 0;

                if (!keepAlive)
                {
                    stream.Dispose();
                    connection.Dispose();
                    connection = null;
                }
            }

            connection?.Dispose();
            return total;
        }
    }
}
