using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer
{
    public static class HttpServer
    {
        private static int _port = 3333;
        private static Task Server;
        public static int Port => _port;

        public static void Main(string[] args)
        {
            if (args?.Length>1)
            {
                var portSymbol = args[0].ToLower();
                if (portSymbol == "-p" || portSymbol == "--port")
                {
                    int.TryParse(args[1], out _port);
                }
            }
            Run(Port);
            Console.ReadKey();
        }

        public static void Run(int port)
        {
            Server ??= new Task(CreateHostBuilder(port).Build().Run);
            
            if (Server.Status != TaskStatus.Running)
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
