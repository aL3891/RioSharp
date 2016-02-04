using RioSharp;
using System;
using System.Collections.Generic;
using System.Linq;
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

        static void Main(string[] args)
        {
            _args = args;
            Connections = int.Parse(GetArgument("Connections") ?? "8");
            Iterations = int.Parse(GetArgument("Iterations") ?? "10000");
            Target = GetArgument("Target") ?? "localhost";
            Port = int.Parse(GetArgument("Port") ?? "4444");
            var Protocol = GetArgument("Protocol") ?? "TCP";
            var Verify = GetArgument("Verify") ?? "connection";
            Pattern = GetArgument("Pattern") ?? "Push";
            Transfer = int.Parse(GetArgument("Transfer") ?? "0x40000000");
            var BitsPerSecond = GetArgument("BitsPerSecond") ?? "16";
            var FrameRate = GetArgument("FrameRate") ?? "16";
            var StreamLength = GetArgument("StreamLength") ?? "10";
            var RateLimit = GetArgument("RateLimit") ?? "0";
            var Listen = GetArgument("Listen") ?? "0";
        }

        public void ListenTcp()
        {
            RioTcpListener l = new RioTcpListener(new RioFixedBufferPool(Connections, Transfer), new RioFixedBufferPool(Connections, Transfer), (uint)Connections);
            l.OnAccepted = s =>
            {
                if (Pattern == "PushPull")
                {
                    int apa = 0;
                    s.OnIncommingSegment = seg =>
                    {
                        apa += seg.CurrentContentLength;
                        if (apa >= Transfer)
                            s.WriteFixed(new byte[Transfer]);
                    };
                }
                else if(Pattern == "Push")
                    s.WriteFixed(new byte[Transfer]);
                else if (Pattern == "Pull")
                    s.WriteFixed(new byte[Transfer]);
                else if (Pattern == "Duplex")
                    s.WriteFixed(new byte[Transfer/2]);
            };

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
