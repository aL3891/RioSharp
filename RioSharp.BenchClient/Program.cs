using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RioSharp.BenchClient
{
    public class Program
    {
        static string requestString = null;

        static byte[] _requestBytes;
        private static RioTcpClientPool clientPool;
        private static Uri uri;
        private static bool keepAlive;
        private static int pipeLineDeph;
        private static Stopwatch timer;
        private static TimeSpan span;
        private static byte[] requestBytes;

        static byte[][] reqz;

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


            requestString = $"GET {uri.PathAndQuery} HTTP/1.1\r\nHost: {uri.Host}:{uri.Port}\r\n\r\n";
            _requestBytes = Encoding.ASCII.GetBytes(requestString);
            requestBytes = Enumerable.Repeat(_requestBytes, pipeLineDeph).SelectMany(b => b).ToArray();
            




            reqz = new byte[pipeLineDeph + 1][];

            for (int i = 0; i < reqz.Length; i++)
            {
                reqz[i] = Enumerable.Repeat(_requestBytes, i).SelectMany(b => b).ToArray();
            }

            sendPool = new RioFixedBufferPool(10 * connections, requestBytes.Length);
            clientPool = new RioTcpClientPool(sendPool, new RioFixedBufferPool(10 * connections, (256 * pipeLineDeph)), (uint)connections);
            Console.WriteLine("Benchmarking...");




            timer.Start();
            var tasks = Enumerable.Range(0, connections).Select(t => Task.Run(ExecuteStream)).ToList();

            var totalRequests = tasks.Sum(t => t.Result.Requests);
            Console.WriteLine($"Made {totalRequests } requests over {span.TotalSeconds} seconds ({totalRequests / span.TotalSeconds} Rps)");
            clientPool.Dispose();
        }

        public class ConnectionState
        {
            public int leftoverLength;
            public int oldleftoverLength;
            public RioSegmentReader<ConnectionState> reader;
            public RioSocket socket;
            public TaskCompletionSource<ConnectionState> tcs = new TaskCompletionSource<ConnectionState>();
            internal int failedConnections;
            internal int Requests;
        }

        static uint endOfRequest = 0x0a0d0a0d;
        private static RioFixedBufferPool sendPool;

        static unsafe void ProcessSocket(RioBufferSegment s, ConnectionState state)
        {
            uint current = 0;
            var r = s.CurrentContentLength;
            var responses = 0;

            if (r == 0)
            {
                ResetConnection(state);
                return;
            }

            for (int i = 0; state.leftoverLength != 0 && i < 4 - state.leftoverLength; i++)
            {
                current += s.Datapointer[i];
                current = current << 8;
                if (current == endOfRequest)
                    responses++;
            }

            state.leftoverLength = r % 4;
            var length = r - state.leftoverLength;

            byte* currentPtr = s.Datapointer + state.oldleftoverLength;

            var start = currentPtr;
            var end = currentPtr + length;

            for (; start <= end; start++)
            {
                if (*(uint*)start == endOfRequest)
                    responses++;
            }

            state.oldleftoverLength = state.leftoverLength;

            for (int i = r - state.leftoverLength; i < r; i++)
            {
                current += s.Datapointer[i];
                current = current << 4;
            }

            state.Requests += responses;

            if (timer.Elapsed < span)
                state.socket.Send(reqz[responses]);
            else
            {
                state.reader.Dispose();
                state.socket.Dispose();
                state.tcs.SetResult(state);
            }
        }

        public async static void ResetConnection(ConnectionState state)
        {
            while (timer.Elapsed < span)
            {
                try
                {
                    state.socket = await clientPool.Connect(uri);
                    state.reader.Socket = state.socket;

                    state.reader.Start();
                    state.socket.Send(requestBytes);
                    return;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    state.failedConnections++;
                    continue;
                }
            }

            state.tcs.SetResult(state);
        }


        public unsafe static void apaaaa()
        {
            byte v = 0x20;

            fixed (byte* ff = &_requestBytes[0])
            {

                var ix = new byte[Vector<byte>.Count];

                for (int i = 0; i < ix.Length; i++)
                    ix[i] = (byte)i;
                
                Vector<byte> apaa = new Vector<byte>(_requestBytes), index = new Vector<byte>(ix), space = new Vector<byte>((byte)' ');

                var apaapa = Vector.Equals(apaa, space);
                
                var fdas = Vector.ConditionalSelect(apaapa, index ,Vector<byte>.Zero); 

                byte*[] apa = new byte*[16];

                var apa2 = Vector.IsHardwareAccelerated;

                var bajs = Vector<byte>.Count;
            }






        }

        public static Task<ConnectionState> ExecuteSegment()
        {
            ConnectionState state = new ConnectionState();
            state.reader = new RioSegmentReader<ConnectionState>(null);
            state.reader.State = state;
            state.reader.OnIncommingSegment = ProcessSocket;
            ResetConnection(state);
            return state.tcs.Task;
        }

        public async static Task<ConnectionState> ExecuteStream()
        {
            var buffer = new byte[256 * pipeLineDeph];
            var leftoverLength = 0;
            var oldleftoverLength = 0;
            uint endOfRequest = 0x0a0d0a0d;
            uint current = 0;
            int responses = 0;

            ConnectionState state = new ConnectionState();

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
                state.Requests += responses;
                responses = 0;

                if (!keepAlive)
                {
                    stream.Dispose();
                    connection.Dispose();
                    connection = null;
                }
            }

            connection?.Dispose();
            return state;
        }
    }
}
