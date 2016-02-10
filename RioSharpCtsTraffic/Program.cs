using RioSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RioSharpCtsTraffic
{
    class Program
    {
        static string[] _args;

        public static string Pattern { get; private set; }
        public static int Connections { get; private set; }
        public static int Iterations { get; private set; }
        public static string Target { get; private set; }
        public static int Port { get; private set; }
        public static int Transfer { get; private set; }
        public static int PushBytes { get; private set; }
        public static int PullBytes { get; private set; }

        static void Main(string[] args)
        {
            _args = args;
            Connections = int.Parse(GetArgument("Connections") ?? "8");
            Iterations = int.Parse(GetArgument("Iterations") ?? "1");
            Target = GetArgument("Target") ?? "localhost";
            Port = int.Parse(GetArgument("Port") ?? "4444");
            PushBytes = int.Parse(GetArgument("PushBytes") ?? "1048576");
            PullBytes = int.Parse(GetArgument("PullBytes") ?? "1048576");
            var Protocol = GetArgument("Protocol") ?? "TCP";
            var Verify = GetArgument("Verify") ?? "connection";
            Pattern = GetArgument("Pattern") ?? "Push";
            Transfer = int.Parse((GetArgument("Transfer") ?? "0x40000000").Substring(2), NumberStyles.HexNumber);
            var BitsPerSecond = GetArgument("BitsPerSecond") ?? "16";
            var FrameRate = GetArgument("FrameRate") ?? "16";
            var StreamLength = GetArgument("StreamLength") ?? "10";
            var RateLimit = GetArgument("RateLimit") ?? "0";
            var Listen = GetArgument("Listen") ?? "0.0.0.0";

            if (Protocol == "TCP")
            {
                if (HasArgument("Target"))
                    ClientTcp().Wait();
                else
                    ListenTcp();
            }
            Console.ReadLine();
        }

        public static void ListenTcp()
        {
            RioTcpListener l = new RioTcpListener(new RioFixedBufferPool(512, 65536), new RioFixedBufferPool(512, 65536), (uint)Connections);
            l.OnAccepted = s =>
            {
                int totalRecived = 0;
                if (Pattern == "PushPull")
                {

                    s.OnIncommingSegment = seg =>
                    {
                        totalRecived += seg.CurrentContentLength;
                        if (totalRecived < Transfer)
                            s.WriteFixed(new byte[PullBytes]);

                        if (seg.CurrentContentLength > 0)
                            s.BeginRecive();
                        else
                            s.Dispose();
                        seg.Dispose();
                    };
                }
                else if (Pattern == "Pull")
                    s.WriteFixed(new byte[Transfer]);
                else if (Pattern == "Push")
                {
                    s.OnIncommingSegment = seg =>
                    {
                        totalRecived += seg.CurrentContentLength;
                        if (seg.CurrentContentLength > 0)
                            s.BeginRecive();
                        else
                            s.Dispose();
                        seg.Dispose();

                    };
                    s.BeginRecive();
                }
                else if (Pattern == "Duplex")
                {
                    s.WriteFixed(new byte[Transfer / 2]);
                    s.OnIncommingSegment = seg =>
                    {
                        totalRecived += seg.CurrentContentLength;
                        //if (apa >= Transfer / 2)
                        //    tcs.SetResult(null);
                    };
                }
            };
            l.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), Port), 1024);
        }

        public static async Task ClientTcp()
        {
            RioTcpClientPool l = new RioTcpClientPool(new RioFixedBufferPool(Connections, Transfer), new RioFixedBufferPool(Connections, Transfer), (uint)Connections);
            int totalBytesRecived = 0;
            TaskCompletionSource<object> tcs;

            for (int i = 0; i < Iterations; i++)
            {
                var s = await l.Connect(new Uri(Target));

                if (Pattern == "PushPull")
                {
                    tcs = new TaskCompletionSource<object>();
                    s.WriteFixed(new byte[PushBytes]);
                    s.OnIncommingSegment = seg =>
                    {
                        totalBytesRecived += seg.CurrentContentLength;
                        if (totalBytesRecived >= Transfer)
                            tcs.SetResult(null);
                    };
                    await tcs.Task;
                }
                else if (Pattern == "Push")
                    s.WriteFixed(new byte[Transfer]);
                else if (Pattern == "Pull")
                {
                    tcs = new TaskCompletionSource<object>();

                    s.OnIncommingSegment = seg =>
                    {
                        totalBytesRecived += seg.CurrentContentLength;
                        if (totalBytesRecived >= Transfer)
                            tcs.SetResult(null);
                    };
                }
                else if (Pattern == "Duplex")
                {
                    tcs = new TaskCompletionSource<object>();
                    s.WriteFixed(new byte[Transfer / 2]);
                    s.OnIncommingSegment = seg =>
                    {
                        totalBytesRecived += seg.CurrentContentLength;
                        if (totalBytesRecived >= Transfer / 2)
                            tcs.SetResult(null);
                    };
                }
            }

        }

        public static bool HasArgument(string arg)
        {
            return _args.Any(f => f.StartsWith("-" + arg));
        }

        public static string GetArgument(string arg)
        {
            return _args.FirstOrDefault(f => f.StartsWith("-" + arg))?.Split(':').ElementAtOrDefault(1);
        }

    }
}
