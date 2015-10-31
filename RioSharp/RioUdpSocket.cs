using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RioSharp
{
    public class RioUdpSocket : RioSocketBase, IDisposable
    {

        public RioUdpSocket(IntPtr socket, RioSocketPoolBase pool) : base(socket, pool)
        {

            IntPtr p;

            
        }       
    }
}