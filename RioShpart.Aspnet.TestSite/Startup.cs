using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.Extensions.DependencyInjection;

namespace RioShpart.Aspnet.TestSite
{
    public class Startup
    {

        public void ConfigureServices(IServiceCollection services)
        {

        }


        public void Configure(IApplicationBuilder app)
        {

            app.Run(async (context) =>
            {
                context.Response.ContentLength = 12;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Hello World!");
            });
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
