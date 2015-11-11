using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNet.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Server.Features;

namespace RioSharp.Aspnet.Host
{
    public class ServerFactory : IServerFactory
    {

        private readonly IApplicationLifetime _appLifetime;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpContextFactory _httpContextFactory;

        public ServerFactory(IApplicationLifetime appLifetime, ILoggerFactory loggerFactory, IHttpContextFactory httpContextFactory)
        {
            _appLifetime = appLifetime;
            _loggerFactory = loggerFactory;
            _httpContextFactory = httpContextFactory;
        }

        public IServer CreateServer(IConfiguration configuration)
        {
            var information = new RioSharpServerInformation();
            information.Initialize(configuration);

            var serverFeatures = new FeatureCollection();
            serverFeatures.Set<IRioSharpServerInformation>(information);
            serverFeatures.Set<IServerAddressesFeature>(information);

            return new RioSharpServer(serverFeatures, _appLifetime, _loggerFactory.CreateLogger("Microsoft.AspNet.Server.Kestrel"), _httpContextFactory);
        }
    }
}
