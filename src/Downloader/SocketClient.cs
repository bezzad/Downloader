using Downloader.Extensions.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    /// <summary>
    /// Represents a client for making HTTP requests.
    /// </summary>
    public partial class SocketClient : IDisposable
    {
        private const string HeaderContentLengthKey = "Content-Length";
        private const string HeaderContentDispositionKey = "Content-Disposition";
        private const string HeaderContentRangeKey = "Content-Range";
        private const string HeaderAcceptRangesKey = "Accept-Ranges";
        private const string FilenameStartPointKey = "filename=";

        [GeneratedRegex(@"bytes\s*((?<from>\d*)\s*-\s*(?<to>\d*)|\*)\s*\/\s*(?<size>\d+|\*)", RegexOptions.Compiled)]
        private static partial Regex RangePatternRegex();

        private readonly Regex _contentRangePattern = RangePatternRegex();
        private readonly Dictionary<string, string> _responseHeaders;
        private HttpClient Client { get; }
        private bool _isDisposed;
        private bool? _isSupportDownloadInRange;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketClient"/> class with the specified configuration.
        /// </summary>
        public SocketClient(RequestConfiguration config)
        {
            _responseHeaders = new Dictionary<string, string>();
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
                PooledConnectionIdleTimeout = config.KeepAliveTimeout,
                PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromMilliseconds(config.Timeout)
            };

            // Set up the SslClientAuthenticationOptions for custom certificate validation
            if (config.ClientCertificates?.Count > 0)
            {
                handler.SslOptions.ClientCertificates = config.ClientCertificates;
            }

            handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
            handler.SslOptions.RemoteCertificateValidationCallback = ExceptionHelper.CertificateValidationCallBack;

            HttpClient client = new(handler);
            client.DefaultRequestHeaders.Add("Accept", config.Accept);
            client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Connection", config.KeepAlive ? "keep-alive" : "close");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            if (!string.IsNullOrWhiteSpace(config.Referer))
            {
                client.DefaultRequestHeaders.Referrer = new Uri(config.Referer);
            }

            if (config.MediaType is not null)
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(config.MediaType));
            }

            if (config.TransferEncoding is not null)
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(
                    new StringWithQualityHeaderValue(config.TransferEncoding));
                client.DefaultRequestHeaders.TransferEncoding.Add(
                    new TransferCodingHeaderValue(config.TransferEncoding));
            }

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
        /// Fetches the response headers asynchronously.
        /// </summary>
        /// <param name="addRange">Indicates whether to add a range header to the request.</param>
        /// <param name="request">The request of client</param>
        /// <param name="cancelToken">Cancel request token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task FetchResponseHeaders(Request request, bool addRange, CancellationToken cancelToken = default)
        {
            try
            {
                if (_responseHeaders.Count > 0)
                {
                    return;
                }

                HttpRequestMessage requestMsg = request.GetRequest();
                if (addRange) // to check the content range supporting
                    requestMsg.Headers.Range = new RangeHeaderValue(0, 0); // first byte

                using HttpResponseMessage response =
                    await SendRequestAsync(requestMsg, cancelToken).ConfigureAwait(false);

                EnsureResponseAddressIsSameWithOrigin(request, response);
            }
            catch (HttpRequestException exp) when (exp.IsRequestedRangeNotSatisfiable())
            {
                await FetchResponseHeaders(request, false, cancelToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exp) when (request.Configuration.AllowAutoRedirect &&
                                                   exp.IsRedirectError())
            {
                if (_responseHeaders.TryGetValue("location", out string redirectedUrl) &&
                    !string.IsNullOrWhiteSpace(redirectedUrl) &&
                    request.Address.ToString().Equals(redirectedUrl, StringComparison.OrdinalIgnoreCase) == false)
                {
                    request.Address = new Uri(redirectedUrl);
                    await FetchResponseHeaders(request, true, cancelToken).ConfigureAwait(false);
                    return;
                }

                throw;
            }
        }

        /// <summary>
        /// Ensures that the response address is the same as the original address.
        /// </summary>
        /// <param name="request">The request of client</param>
        /// <param name="response">The web response to check.</param>
        /// <returns>True if the response address is the same as the original address; otherwise, false.</returns>
        private void EnsureResponseAddressIsSameWithOrigin(Request request, HttpResponseMessage response)
        {
            Uri redirectUri = GetRedirectUrl(response);
            if (redirectUri != null)
            {
                request.Address = redirectUri;
            }
        }

        /// <summary>
        /// Gets the redirect URL from the web response.
        /// </summary>
        /// <param name="response">The web response to get the redirect URL from.</param>
        /// <returns>The redirect URL.</returns>
        public Uri GetRedirectUrl(HttpResponseMessage response)
        {
            // https://github.com/dotnet/runtime/issues/23264
            var redirectLocation = response?.Headers.Location;
            if (redirectLocation != null)
            {
                return redirectLocation;
            }

            return response?.RequestMessage?.RequestUri;
        }

        /// <summary>
        /// Gets the file size asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the file size.</returns>
        public async ValueTask<long> GetFileSizeAsync(Request request)
        {
            if (await IsSupportDownloadInRange(request).ConfigureAwait(false))
            {
                return GetTotalSizeFromContentRange(_responseHeaders);
            }

            // gets the total size from the content length headers.
            if (_responseHeaders.TryGetValue(HeaderContentLengthKey, out string contentLengthText) &&
                long.TryParse(contentLengthText, out long contentLength))
            {
                return contentLength;
            }

            return -1L;
        }

        /// <summary>
        /// Throws an exception if the download in range is not supported.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async ValueTask ThrowIfIsNotSupportDownloadInRange(Request request)
        {
            bool isSupport = await IsSupportDownloadInRange(request).ConfigureAwait(false);
            if (isSupport == false)
            {
                throw new NotSupportedException(
                    "The downloader cannot continue downloading because the network or server failed to download in range.");
            }
        }

        /// <summary>
        /// Checks if the download in range is supported.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the download in range is supported.</returns>
        public async ValueTask<bool> IsSupportDownloadInRange(Request request)
        {
            if (_isSupportDownloadInRange.HasValue)
            {
                return _isSupportDownloadInRange.Value;
            }

            if (_responseHeaders.Count == 0)
            {
                await FetchResponseHeaders(request, addRange: true).ConfigureAwait(false);
            }

            // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.5
            if (_responseHeaders.TryGetValue(HeaderAcceptRangesKey, out string acceptRanges) &&
                acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                _isSupportDownloadInRange = false;
                return false;
            }

            // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.16
            if (_responseHeaders.TryGetValue(HeaderContentRangeKey, out string contentRange))
            {
                if (string.IsNullOrWhiteSpace(contentRange) == false)
                {
                    _isSupportDownloadInRange = true;
                    return true;
                }
            }

            _isSupportDownloadInRange = false;
            return false;
        }

        /// <summary>
        /// Gets the total size from the content range headers.
        /// </summary>
        /// <param name="headers">The headers to get the total size from.</param>
        /// <returns>The total size of the content.</returns>
        private long GetTotalSizeFromContentRange(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderContentRangeKey, out string contentRange) &&
                string.IsNullOrWhiteSpace(contentRange) == false &&
                _contentRangePattern.IsMatch(contentRange))
            {
                var match = _contentRangePattern.Match(contentRange);
                var size = match.Groups["size"].Value;
                //var from = match.Groups["from"].Value;
                //var to = match.Groups["to"].Value;

                return long.TryParse(size, out long totalSize) ? totalSize : -1L;
            }

            return -1L;
        }

        /// <summary>
        /// Gets the file name asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the file name.</returns>
        public async Task<string> SetRequestFileNameAsync(Request request)
        {
            if (!string.IsNullOrWhiteSpace(request.FileName))
            {
                return request.FileName;
            }

            string filename = await GetUrlDispositionFilenameAsync(request).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = request.GetFileNameFromUrl();
                if (string.IsNullOrWhiteSpace(filename))
                {
                    filename = Guid.NewGuid().ToString("N");
                }
            }

            request.FileName = filename;
            return filename;
        }

        /// <summary>
        /// Gets the file name from the URL disposition header asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation. The task result contains the file name.</returns>
        private async Task<string> GetUrlDispositionFilenameAsync(Request request)
        {
            try
            {
                if (request.Address?.IsWellFormedOriginalString() == true
                    && request.Address?.Segments.Length > 1)
                {
                    await FetchResponseHeaders(request, true).ConfigureAwait(false);
                    if (_responseHeaders.TryGetValue(HeaderContentDispositionKey, out string disposition))
                    {
                        string unicodeDisposition = request.ToUnicode(disposition);
                        if (string.IsNullOrWhiteSpace(unicodeDisposition) == false)
                        {
                            string[] dispositionParts = unicodeDisposition.Split(';');
                            string filenamePart = dispositionParts.FirstOrDefault(part => part.Trim()
                                .StartsWith(FilenameStartPointKey, StringComparison.OrdinalIgnoreCase));
                            if (string.IsNullOrWhiteSpace(filenamePart) == false)
                            {
                                string filename = filenamePart.Replace(FilenameStartPointKey, "")
                                    .Replace("\"", "").Trim();

                                return filename;
                            }
                        }
                    }
                }
            }
            catch (WebException)
            {
                // No matter in this point
            }

            return null;
        }

        /// <summary>
        /// Gets the response stream asynchronously.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        /// <exception cref="WebException"></exception>
        public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request,
            CancellationToken cancelToken)
        {
            HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken)
                .ConfigureAwait(false);

            response.Content.Headers.ToList()
                .ForEach(header => _responseHeaders[header.Key] = header.Value.FirstOrDefault());
            
            // throws an HttpRequestException error if the response status code isn't within the 200-299 range.
            response.EnsureSuccessStatusCode();
            
            return response;
        }

        /// <summary>
        /// Disposes of the resources (if any) used by the <see cref="SocketClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Client?.Dispose();
            }
        }
    }
}