using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp.Aspnet.Host
{
    public class Program
    {
        public void Main(string[] args)
        {
            var mergedArgs = new[] { "--server", "RioSharp.Aspnet.Host" }.Concat(args).ToArray();
            Microsoft.AspNet.Hosting.Program.Main(mergedArgs);
        }
    }
}
