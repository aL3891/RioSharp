using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioTcpSocket : RioSocketBase
    {
        public RioTcpSocketStream Stream { get; }

        public RioTcpSocket(IntPtr socket, RioSocketPoolBase pool) : base(socket, pool)
        {
            Stream = new RioTcpSocketStream(this);
        }

        public override void Dispose()
        {
            Stream.Dispose();
            base.Dispose();
        }

    }
}
