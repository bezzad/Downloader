using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer
{
    public static class HttpServer
    {
        private static Task Server;
        public static int Port { get; set; } = 3333;
        public static CancellationTokenSource CancellationToken { get; set; }

        public static void Main()
        {
            Run(Port);
            Console.ReadKey();
            Stop();
        }

        public static void Run(int port)
        {
            CancellationToken ??= new CancellationTokenSource();
            if(CancellationToken.IsCancellationRequested) 
                return;

            Server ??= CreateHostBuilder(port).Build().RunAsync(CancellationToken.Token);

            if (Server.Status != TaskStatus.Running &&
                Server.Status != TaskStatus.WaitingToRun)
                Server.ConfigureAwait(false);
        }

        public static void Stop()
        {
            if (Server?.Status == TaskStatus.Running)
            {
                CancellationToken?.Cancel();
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
