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

namespace RioSharp.Aspnet.Host
{
    public class RioSharpServer : IServer
    {

        private ILogger _logger;
        private IApplicationLifetime _appLifetime;
        private IHttpContextFactory _httpContextFactory;

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
            
        }
    }
}
