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
        static RioBufferSegment currentSegment;
        static RioFixedBufferPool sendPool, recivePool;
        private static RioTcpListener listener;
        private static int pipeLineDeph;
        private static byte[] responseBytes;

        static byte[][] reqz;


        public static byte[] GetResponse()
        {
            var responseStr = "HTTP/1.1 200 OK\r\n" +
                              "Content-Type: text/plain\r\n" +
                              "Content-Length: 13\r\n" +
                              "Date: " + DateTime.UtcNow.ToString("r") + "\r\n" + //"Connection: keep-alive\r\n" +
                              "Server: Dummy\r\n" +
                              "\r\n" +
                              "Hello, World!";

            return Encoding.ASCII.GetBytes(responseStr);
        }

        static void UpdateResponse()
        {
            responseBytes = GetResponse();
            //var newSegment = socketPool.PreAllocateWrite(responseBytes);
            //var oldSegment = currentSegment;
            //currentSegment = newSegment;
            //oldSegment.Dispose();
        }

        static void Main(string[] args)
        {
            pipeLineDeph = int.Parse(args.FirstOrDefault(f => f.StartsWith("-p"))?.Substring(2) ?? "16");
            int connections = int.Parse(args.FirstOrDefault(f => f.StartsWith("-c"))?.Substring(2) ?? "1024");

            Console.WriteLine("RioSharp http server");
            Console.WriteLine("Optimizing for " + connections + " connections");
            Console.WriteLine("Optimizing for pipeline depth of: " + pipeLineDeph);

            sendPool = new RioFixedBufferPool(10 * connections, 256 * pipeLineDeph);
            recivePool = new RioFixedBufferPool(10 * connections, 256 * pipeLineDeph);

            listener = new RioTcpListener(sendPool, recivePool, (uint)connections);
            currentSegment = listener.PreAllocateWrite(GetResponse());
            responseBytes = GetResponse();

            reqz = new byte[pipeLineDeph + 1][];

            for (int i = 0; i < reqz.Length; i++)
            {
                reqz[i] = Enumerable.Repeat(responseBytes, i).SelectMany(b => b).ToArray();
            }

            //Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        UpdateResponse();
            //        await Task.Delay(60000);
            //    }
            //});

            //listener.OnAccepted = new Action<RioSocket>(s => ThreadPool.QueueUserWorkItem(o => Servebuff((RioSocket)o), s));
            listener.OnAccepted = new Action<RioSocket>(s => ServeSegment(s));
            listener.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000), 1024 * connections);
            Console.WriteLine("Listening on : http://localhost:5000");
            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
            listener.Dispose();
        }


        static unsafe void ProcessSocket(RioBufferSegment s, ServeState state) {
            uint current = 0;
            var r = s.CurrentContentLength;
            var reqs = 0;

            if (r == 0)
            {
                state.reader.Dispose();
                state.socket.Dispose();
                return;
            }

            for (int i = 0; state.leftoverLength != 0 && i < 4 - state.leftoverLength; i++)
            {
                current += s.Datapointer[i];
                current = current << 8;
                if (current == endOfRequest)
                    reqs++;
            }

            state.leftoverLength = r % 4;
            var length = r - state.leftoverLength;

            byte* currentPtr = s.Datapointer + state.oldleftoverLength;

            var start = currentPtr;
            var end = currentPtr + length;

            for (; start <= end; start++)
            {
                if (*(uint*)start == endOfRequest)
                    reqs++;
            }
            
            state.oldleftoverLength = state.leftoverLength;

            for (int i = r - state.leftoverLength; i < r; i++)
            {
                current += s.Datapointer[i];
                current = current << 4;
            }

            state.socket.Send(reqz[reqs]);
        }

        static uint endOfRequest = 0x0a0d0a0d;

        class ServeState
        {
            internal int leftoverLength;
            internal int oldleftoverLength;
            internal RioSegmentReader<ServeState> reader;
            internal RioSocket socket;
        }

        static void ServeSegment(RioSocket socket)
        {
            var reader = new RioSegmentReader<ServeState>(socket);
            reader.State = new ServeState();
            reader.State.leftoverLength = 0;
            reader.State.oldleftoverLength = 0;
            reader.State.reader = reader;
            reader.State.socket = socket;
            reader.OnIncommingSegment = ProcessSocket;
            reader.Start();
        }

        static async Task ServeFixed(RioSocket socket)
        {
            try
            {
                var buffer = new byte[128 * pipeLineDeph];
                var leftoverLength = 0;
                var oldleftoverLength = 0;
                uint endOfRequest = 0x0a0d0a0d;
                uint current = 0;
                var stream = new RioStream(socket);

                while (true)
                {
                    int r = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (r == 0)
                        break;
                    
                    for (int i = 0; leftoverLength != 0 && i < 4 - leftoverLength; i++)
                    {
                        current += buffer[i];
                        current = current << 8;
                        if (current == endOfRequest)
                            socket.Send(currentSegment);
                    }

                    leftoverLength = r % 4;
                    var length = r - leftoverLength;

                    unsafe
                    {
                        fixed (byte* currentPtr = &buffer[oldleftoverLength])
                        {
                            var start = currentPtr;
                            var end = currentPtr + length;

                            for (; start <= end; start++)
                            {
                                if (*(uint*)start == endOfRequest)
                                    socket.Send(currentSegment);
                            }
                        }
                    }

                    oldleftoverLength = leftoverLength;

                    for (int i = r - leftoverLength; i < r; i++)
                    {
                        current += buffer[i];
                        current = current << 4;
                    }
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                socket.Dispose();
            }
        }

        static async Task Servebuff(RioSocket socket)
        {
            try
            {
                var buffer = new byte[128 * pipeLineDeph];
                var leftoverLength = 0;
                var oldleftoverLength = 0;
                uint endOfRequest = 0x0a0d0a0d;
                uint current = 0;
                var stream = new RioStream(socket);

                while (true)
                {
                    int r = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (r == 0)
                        break;

                    for (int i = 0; leftoverLength != 0 && i < 4 - leftoverLength; i++)
                    {
                        current += buffer[i];
                        current = current << 8;
                        if (current == endOfRequest)
                        {
                            stream.Write(responseBytes, 0, responseBytes.Length);
                        }
                    }

                    leftoverLength = r % 4;
                    var length = r - leftoverLength;

                    unsafe
                    {
                        fixed (byte* currentPtr = &buffer[oldleftoverLength])
                        {
                            var start = currentPtr;
                            var end = currentPtr + length;

                            for (; start <= end; start++)
                            {
                                if (*(uint*)start == endOfRequest)
                                {
                                    stream.Write(responseBytes, 0, responseBytes.Length);
                                }
                            }
                        }
                    }

                    stream.Flush();
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
                socket.Dispose();
            }
        }
    }
}
