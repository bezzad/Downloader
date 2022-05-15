using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer
{
    public static class HttpServer
    {
        private static Task Server;
        public static int Port { get; set; } = 3333;

        public static void Main()
        {
            Run(Port);
            Console.ReadKey();
            Stop();
        }

        public static void Run(int port)
        {
            Server ??= new Task(CreateHostBuilder(port).Build().Run);
            
            if (Server.Status != TaskStatus.Running &&
                Server.Status != TaskStatus.WaitingToRun)
                Server.Start();
        }

        public static void Stop()
        {
            if (Server?.Status == TaskStatus.Running)
            {
                Server.Dispose();
                Server = null; 
            }
        }

        public static IHostBuilder CreateHostBuilder(int port) =>
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseUrls($"http://localhost:{port}")
                              .UseStartup<Startup>();
                });
    }
}
