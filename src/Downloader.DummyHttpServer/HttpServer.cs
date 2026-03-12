using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class HttpServer
{
    private static IHost Server;
    private static CancellationTokenSource CancellationToken { get; set; }
    private static readonly SemaphoreSlim StartLock = new(1, 1);
    public static int Port { get; private set; } = 3333;

    public static async Task Main()
    {
        Run(Port);
        Console.ReadKey();
        await Stop();
    }

    public static void Run(int port)
    {
        StartLock.Wait();
        try
        {
            if (Server is not null)
                return;

            CancellationToken = new CancellationTokenSource();

            Server ??= CreateHostBuilder(port);
            // Use Task.Run to avoid potential sync-over-async deadlock in thread-constrained contexts
            Server.Start();

            if (port == 0) // dynamic port
                SetPort();
        }
        finally
        {
            StartLock.Release();
        }
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
        await StartLock.WaitAsync();
        try
        {
            if (Server is not null)
            {
                await CancellationToken?.CancelAsync()!;
                await Server.StopAsync();
                Server?.Dispose();
                Server = null;
                CancellationToken?.Dispose();
                CancellationToken = null;
            }
        }
        finally
        {
            StartLock.Release();
        }
    }

    private static IHost CreateHostBuilder(int port)
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => {
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureWebHostDefaults(webBuilder => {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls($"http://127.0.0.1:{port}");
            });

        return hostBuilder.Build();
    }
}
