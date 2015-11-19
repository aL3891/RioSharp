using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

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
        private RioSocketPool socketPool;

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

        public void Start(RequestDelegate requestDelegate)
        {
            var information = Features.Get<IRioSharpServerInformation>();

            sendPool = new RioFixedBufferPool(1000, 140 * information.PipeLineDepth);
            recivePool = new RioFixedBufferPool(1000, 64 * information.PipeLineDepth);
            socketPool = new RioSocketPool(sendPool, recivePool);
            listener = new RioTcpListener(socketPool);

            listener.Bind(new IPEndPoint(new IPAddress(new byte[] { 0, 0, 0, 0 }), 5000));
            listener.Listen(1024 * information.Connections);
            // do things

        }
    }
}
