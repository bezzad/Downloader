using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class HttpServer
{
    private static readonly IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());
    private static IHost Server;
    private static CancellationTokenSource CancellationToken { get; set; }
    public static int Port { get; private set; } = 3333;

    public static async Task Main()
    {
        Run(Port);    
        Console.ReadKey();
        await Stop();
    }

    public static void Run(int port)
    {
        CancellationToken ??= new CancellationTokenSource();
        if (CancellationToken.IsCancellationRequested)
            return;

        Server ??= Cache.GetOrCreate("DownloaderWebHost", e => {
            IHost host = CreateHostBuilder(port);
            host.RunAsync(CancellationToken.Token).ConfigureAwait(false);
            return host;
        });

        if (port == 0) // dynamic port
            SetPort();
    }

    private static void SetPort()
    {
        var server = Server.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as Microsoft.AspNetCore.Hosting.Server.IServer;
        IServerAddressesFeature feature = server?.Features.Get<IServerAddressesFeature>();
        if (feature?.Addresses.Any() == true)
        {
            string address = feature.Addresses.First();
            Port = new Uri(address).Port;
        }
    }

    public static async Task Stop()
    {
        if (Server is not null)
        {
            await CancellationToken?.CancelAsync()!;
            await Server.StopAsync();
            Server?.Dispose();
            Server = null;
        }
    }

    private static IHost CreateHostBuilder(int port)
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                if (port > 0)
                {
                    webBuilder.UseUrls($"http://localhost:{port}");
                }
            });

        return hostBuilder.Build();
    }
}
