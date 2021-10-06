using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;

namespace Downloader.DummyHttpServer
{
    public static class HttpServer
    {
        private static Thread Server;

        public static void Main(string[] args)
        {
            Run(3333);
            Console.ReadKey();
        }

        public static void Run(int port)
        {
            Server ??= new Thread(CreateHostBuilder(port).Build().Run) {
                IsBackground = true
            };
            if (Server.IsAlive == false)
                Server.Start();
        }

        public static IHostBuilder CreateHostBuilder(int port) =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseUrls($"http://localhost:{port}")
                              .UseStartup<Startup>();
                });
    }
}
