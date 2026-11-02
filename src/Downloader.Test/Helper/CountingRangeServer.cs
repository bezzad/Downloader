using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Downloader.Test.Helper;

/// <summary>
/// A very small HTTP server with range support. It sends each answer slowly and counts how many
/// requests are open at the same time, so a test can see how many connections a download uses.
/// </summary>
public sealed partial class CountingRangeServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private int _open;
    private int _maxOpen;

    public CountingRangeServer(byte[] body)
    {
        Body = body;
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public byte[] Body { get; }
    public int MaxOpenRequests => Volatile.Read(ref _maxOpen);
    public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/file.bin";

    [GeneratedRegex(@"^Range:\s*bytes=(\d+)-(\d*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex RangeHeader();

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
                string headers = await ReadHeadersAsync(stream);
                await AnswerAsync(stream, headers);
            }
            catch
            {
                // the client closed the connection
            }
        }
    }

    private async Task AnswerAsync(Stream stream, string headers)
    {
        Match range = RangeHeader().Match(headers);
        int from = 0, to = Body.Length - 1;
        string status = "200 OK", contentRange = "";
        if (range.Success)
        {
            from = int.Parse(range.Groups[1].Value);
            if (range.Groups[2].Value.Length > 0)
                to = Math.Min(to, int.Parse(range.Groups[2].Value));
            status = "206 Partial Content";
            contentRange = $"Content-Range: bytes {from}-{to}/{Body.Length}\r\n";
        }

        int length = to - from + 1;
        string head = $"HTTP/1.1 {status}\r\nContent-Length: {length}\r\n{contentRange}" +
                      "Accept-Ranges: bytes\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));

        // A request counts as open until its LAST part is sent: a client that waits for one answer
        // before it asks again can never be counted twice, so the count has no timing gap.
        int open = Interlocked.Increment(ref _open);
        InterlockedMax(ref _maxOpen, open);
        try
        {
            // Slowly, in a few parts, so that requests running at the same time really overlap.
            const int parts = 5;
            int partSize = Math.Max(1, (length + parts - 1) / parts);
            int offset = 0;
            for (; offset + partSize < length; offset += partSize)
            {
                await stream.WriteAsync(Body.AsMemory(from + offset, partSize));
                await stream.FlushAsync();
                await Task.Delay(40);
            }

            Interlocked.Decrement(ref _open);
            open = 0;
            await stream.WriteAsync(Body.AsMemory(from + offset, length - offset));
            await stream.FlushAsync();
        }
        finally
        {
            if (open != 0)
                Interlocked.Decrement(ref _open);
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) &&
               Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    private static async Task<string> ReadHeadersAsync(Stream stream)
    {
        StringBuilder text = new();
        byte[] one = new byte[1];
        while (await stream.ReadAsync(one) == 1)
        {
            text.Append((char)one[0]);
            if (text.Length >= 4 && text.ToString(text.Length - 4, 4) == "\r\n\r\n")
                break;
        }

        return text.ToString();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
