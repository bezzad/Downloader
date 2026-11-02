using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Downloader.Test.Helper;

/// <summary>
/// A very small HTTP proxy for tests. It does not forward anything: it answers every request
/// with 200 and the same body, and counts the requests.
/// </summary>
public sealed class FakeProxyServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private int _requests;

    public FakeProxyServer(byte[] body)
    {
        Body = body;
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public byte[] Body { get; }
    public int Requests => _requests;
    public Uri Url => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                return;
            }

            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                await ReadHeadersAsync(stream);
                Interlocked.Increment(ref _requests);
                string head = $"HTTP/1.1 200 OK\r\nContent-Length: {Body.Length}\r\n" +
                              "Content-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                await stream.WriteAsync(Body);
                await stream.FlushAsync();
            }
            catch
            {
                // the client closed the connection
            }
        }
    }

    private static async Task ReadHeadersAsync(Stream stream)
    {
        byte[] one = new byte[1];
        int matched = 0; // counts "\r\n\r\n"
        while (matched < 4 && await stream.ReadAsync(one) == 1)
        {
            matched = one[0] == (matched % 2 == 0 ? '\r' : '\n') ? matched + 1 : (one[0] == '\r' ? 1 : 0);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
