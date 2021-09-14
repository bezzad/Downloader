using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;

namespace Downloader.DummyHttpServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new Thread(CreateHostBuilder(args).Build().Run).Start();
            Console.ReadKey();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseUrls("http://localhost:5000")
                              .UseStartup<Startup>();
                });
    }
}
