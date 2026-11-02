using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Downloader.Test.Helper;

/// <summary>
/// A very small HTTPS server with a self-signed certificate. It answers every request with
/// 200 and the same body, and closes the connection.
/// </summary>
public sealed class SelfSignedHttpsServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly X509Certificate2 _certificate = CreateCertificate();
    private readonly CancellationTokenSource _cts = new();

    public SelfSignedHttpsServer(byte[] body)
    {
        Body = body;
        _listener.Start();
        _ = AcceptLoopAsync();
    }

    public byte[] Body { get; }
    public string Url => $"https://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}/file.bin";

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder san = new();
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        using X509Certificate2 cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Export and load again so the private key works with SslStream on every OS.
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

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
                await using SslStream ssl = new(client.GetStream());
                await ssl.AuthenticateAsServerAsync(_certificate);
                await ReadHeadersAsync(ssl);
                string head = $"HTTP/1.1 200 OK\r\nContent-Length: {Body.Length}\r\n" +
                              "Content-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
                await ssl.WriteAsync(Encoding.ASCII.GetBytes(head));
                await ssl.WriteAsync(Body);
                await ssl.FlushAsync();
            }
            catch
            {
                // the client refused the certificate or closed the connection
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
        _certificate.Dispose();
        _cts.Dispose();
    }
}
