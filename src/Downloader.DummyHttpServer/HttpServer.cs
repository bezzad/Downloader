using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class HttpServer
{
    private static IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());
    private static IWebHost Server;
    public static int Port { get; set; } = 3333;
    public static CancellationTokenSource CancellationToken { get; set; }

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
            IWebHost host = CreateHostBuilder(port);
            host.RunAsync(CancellationToken.Token).ConfigureAwait(false);
            return host;
        });

        if (port == 0) // dynamic port
            SetPort();
    }

    private static void SetPort()
    {
        IServerAddressesFeature feature = Server.ServerFeatures.Get<IServerAddressesFeature>();
        if (feature.Addresses.Any())
        {
            string address = feature.Addresses.First();
            Port = new Uri(address).Port;
        }
    }

    public static async Task Stop()
    {
        if (Server is not null)
        {
            CancellationToken?.Cancel();
            await Server.StopAsync();
            Server?.Dispose();
            Server = null;
        }
    }

    public static IWebHost CreateHostBuilder(int port)
    {
        IWebHostBuilder host = WebHost.CreateDefaultBuilder()
                      .UseStartup<Startup>();

        if (port > 0)
        {
            host = host.UseUrls($"http://localhost:{port}");
        }

        return host.Build();
    }
}
