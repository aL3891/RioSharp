using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Threading;

namespace RioSharp.Aspnet.Host
{
    public class RioSharpServer : IServer
    {
        private ILogger _logger;
        private IApplicationLifetime _appLifetime;
        private IHttpContextFactory _httpContextFactory;
        private RioFixedBufferPool sendPool;
        private RioFixedBufferPool recivePool;
        private RioTcpListener listener;

        public RioSharpServer(FeatureCollection features, IApplicationLifetime appLifetime, ILogger logger, IHttpContextFactory httpContextFactory)
        {
            Features = features;
            _appLifetime = appLifetime;
            _logger = logger;
            _httpContextFactory = httpContextFactory;
        }

        public IFeatureCollection Features
        {
            get;
        }

        public void Dispose()
        {

        }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            var information = Features.Get<IRioSharpServerInformation>();
            sendPool = new RioFixedBufferPool(1000, 140 * information.PipeLineDepth);
            recivePool = new RioFixedBufferPool(1000, 64 * information.PipeLineDepth);
            listener = new RioTcpListener(sendPool, recivePool, 1024);

            listener.OnAccepted = new Action<RioSocket>(s => ThreadPool.QueueUserWorkItem(o => Servebuff((RioSocket)o), s));
            listener.Listen(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000), 1024 * information.Connections);
            // do things
        }

        async Task Servebuff(RioSocket socket)
        {
        }
    }
}
