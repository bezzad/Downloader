using Downloader.Extensions.Helpers;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    /// <summary>
    /// Represents a client for making HTTP requests.
    /// </summary>
    public class SocketClient : IDisposable
    {
        /// <summary>
        /// The HTTP client for the download service.
        /// </summary>
        private HttpClient Client { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class with the specified configuration.
        /// </summary>
        public SocketClient(RequestConfiguration config)
        {
            Client = GetHttpClientWithSocketHandler(config);
        }

        private HttpClient GetHttpClientWithSocketHandler(RequestConfiguration config)
        {
            SocketsHttpHandler handler = new() {
                AllowAutoRedirect = config.AllowAutoRedirect,
                MaxAutomaticRedirections = config.MaximumAutomaticRedirections,
                AutomaticDecompression = config.AutomaticDecompression,
                PreAuthenticate = config.PreAuthenticate,
                UseCookies = config.CookieContainer != null,
                UseProxy = config.Proxy != null,
                MaxConnectionsPerServer = 1000,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromMilliseconds(config.Timeout)
            };

            // Set up the SslClientAuthenticationOptions for custom certificate validation
            if (config.ClientCertificates?.Count > 0)
            {
                handler.SslOptions.ClientCertificates = config.ClientCertificates;
            }
#pragma warning disable SYSLIB0039
            handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12 |
                                                     SslProtocols.Tls11 | SslProtocols.Tls;
#pragma warning restore SYSLIB0039
            handler.SslOptions.RemoteCertificateValidationCallback = ExceptionHelper.CertificateValidationCallBack;

            HttpClient client = new(handler);
            client.DefaultRequestHeaders.Add("Accept", config.Accept);
            client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Connection", config.KeepAlive ? "keep-alive" : "close");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(config.MediaType));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue(config.TransferEncoding));
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            client.DefaultRequestHeaders.TransferEncoding.Add(new TransferCodingHeaderValue(config.TransferEncoding));
            client.DefaultRequestHeaders.Referrer = new Uri(config.Referer);

            if (config.Authorization is not null)
            {
                client.DefaultRequestHeaders.Authorization = config.Authorization;
            }

            if (config.Headers?.Count > 0)
            {
                foreach (string key in config.Headers.AllKeys)
                {
                    client.DefaultRequestHeaders.Add(key, config.Headers[key]);
                }
            }

            if (!string.IsNullOrWhiteSpace(config.Expect))
            {
                client.DefaultRequestHeaders.Add("Expect", config.Expect);
                handler.Expect100ContinueTimeout = TimeSpan.FromSeconds(1);
            }

            if (config.KeepAlive)
            {
                handler.KeepAlivePingTimeout = config.KeepAliveTimeout;
                handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests;
            }

            if (config.Credentials != null)
            {
                handler.Credentials = config.Credentials;
            }

            if (handler.UseCookies && config.CookieContainer != null)
            {
                handler.CookieContainer = config.CookieContainer;
            }

            if (handler.UseProxy)
            {
                handler.Proxy = config.Proxy;
            }

            return client;
        }

        /// <summary>
        /// Disposes of the resources (if any) used by the <see cref="SocketClient"/>.
        /// </summary>
        public void Dispose()
        {
            Client?.Dispose();
        }
    }
}