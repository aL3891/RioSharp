using System;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.AspNet.Server.Features;

namespace RioSharp.Aspnet.Host
{
    internal class RioSharpServerInformation : IServerAddressesFeature, IRioSharpServerInformation
    {
        public ICollection<string> Addresses { get; } = new List<string>();

        public bool NoDelay { get; set; }

        public int Connections { get; set; }

        public uint PipeLineDepth { get; set; }

        public void Initialize(IConfiguration configuration)
        {
            var urls = configuration["server.urls"] ?? string.Empty;
            foreach (var url in urls.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Addresses.Add(url);
            }
        }

    }
}