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

        static void Main(string[] args)
        {
            _args = args;
            var Connections = GetArgument("Connections") ?? "8";
            var Iterations = GetArgument("Iterations") ?? "10000";
            var Target = GetArgument("Target") ?? "localhost";
            var Port = GetArgument("Port") ?? "4444";
            var Protocol = GetArgument("Protocol") ?? "TCP";
            var Verify = GetArgument("Verify") ?? "connection";
            var Pattern = GetArgument("Pattern") ?? "Push";
            var Transfer = GetArgument("Transfer") ?? "0x40000000";
            var BitsPerSecond = GetArgument("BitsPerSecond") ?? "16";
            var FrameRate = GetArgument("FrameRate") ?? "16";
            var StreamLength = GetArgument("StreamLength") ?? "10";
            var RateLimit = GetArgument("RateLimit") ?? "0";

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
