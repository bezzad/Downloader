using Downloader.Extensions.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

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
    private bool _isDisposed;
    private bool? _isSupportDownloadInRange;
    private ConcurrentDictionary<string, string> ResponseHeaders { get; set; } = new();
    private HttpClient Client { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketClient"/> class with the specified configuration.
    /// </summary>
    public SocketClient(DownloadConfiguration config)
    {
        Client = GetHttpClientWithSocketHandler(config);
    }

    private SocketsHttpHandler GetSocketsHttpHandler(RequestConfiguration config)
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
            ConnectTimeout = TimeSpan.FromMilliseconds(config.ConnectTimeout)
        };

        // Set up the SslClientAuthenticationOptions for custom certificate validation
        if (config.ClientCertificates?.Count > 0)
        {
            handler.SslOptions.ClientCertificates = config.ClientCertificates;
        }

        handler.SslOptions.EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
        handler.SslOptions.RemoteCertificateValidationCallback = ExceptionHelper.CertificateValidationCallBack;

        // Configure keep-alive
        if (config.KeepAlive)
        {
            handler.KeepAlivePingTimeout = config.KeepAliveTimeout;
            handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests;
        }

        // Configure credentials
        if (config.Credentials != null)
        {
            handler.Credentials = config.Credentials;
            handler.PreAuthenticate = config.PreAuthenticate;
        }

        // Configure cookies
        if (handler.UseCookies && config.CookieContainer != null)
        {
            handler.CookieContainer = config.CookieContainer;
        }

        // Configure proxy
        if (handler.UseProxy && config.Proxy != null)
        {
            handler.Proxy = config.Proxy;
        }
        
        // Add expect header
        if (!string.IsNullOrWhiteSpace(config.Expect))
        {
            handler.Expect100ContinueTimeout = TimeSpan.FromSeconds(1);
        }
        
        return handler;
    }
    
    private HttpClient GetHttpClientWithSocketHandler(DownloadConfiguration downloadConfig)
    {
        RequestConfiguration requestConfig = downloadConfig.RequestConfiguration;
        SocketsHttpHandler handler = GetSocketsHttpHandler(requestConfig);
        HttpClient client = new(handler);

        // Apply HTTPClientTimeout
        client.Timeout = TimeSpan.FromMilliseconds(downloadConfig.HTTPClientTimeout);

        client.DefaultRequestHeaders.Clear();

        // Add standard headers
        AddHeaderIfNotEmpty(client.DefaultRequestHeaders, "Accept", requestConfig.Accept);
        AddHeaderIfNotEmpty(client.DefaultRequestHeaders, "User-Agent", requestConfig.UserAgent);
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        client.DefaultRequestHeaders.Add("Connection", requestConfig.KeepAlive ? "keep-alive" : "close");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

        // Add custom headers
        if (requestConfig.Headers?.Count > 0)
        {
            foreach (string key in requestConfig.Headers.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                AddHeaderIfNotEmpty(client.DefaultRequestHeaders, key, requestConfig.Headers[key]);
            }
        }

        // Add optional headers
        if (!string.IsNullOrWhiteSpace(requestConfig.Referer))
            client.DefaultRequestHeaders.Referrer = new Uri(requestConfig.Referer);

        if (!string.IsNullOrWhiteSpace(requestConfig.ContentType))
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(requestConfig.ContentType));

        if (!string.IsNullOrWhiteSpace(requestConfig.TransferEncoding))
        {
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue(requestConfig.TransferEncoding));
            client.DefaultRequestHeaders.TransferEncoding.Add(new TransferCodingHeaderValue(requestConfig.TransferEncoding));
        }

        AddHeaderIfNotEmpty(client.DefaultRequestHeaders, "Expect", requestConfig.Expect);

        return client;
    }

    private void AddHeaderIfNotEmpty(HttpRequestHeaders headers, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            headers.Add(key, value);
    }

    /// <summary>
    /// Fetches the response headers asynchronously.
    /// </summary>
    /// <param name="addRange">Indicates whether to add a range header to the request.</param>
    /// <param name="request">The request of client</param>
    /// <param name="cancelToken">Cancel request token</param>
    private async Task FetchResponseHeaders(Request request, bool addRange, CancellationToken cancelToken = default)
    {
        try
        {
            if (!ResponseHeaders.IsEmpty) return;

            var requestMsg = request.GetRequest();
            if (addRange) 
                requestMsg.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await SendRequestAsync(requestMsg, cancelToken).ConfigureAwait(false);

            if (response.StatusCode.IsRedirectStatus() && request.Configuration.AllowAutoRedirect) return;

            var redirectUrl = response.Headers.Location?.ToString();
            if (!string.IsNullOrWhiteSpace(redirectUrl) &&
                !request.Address.ToString().Equals(redirectUrl, StringComparison.OrdinalIgnoreCase))
            {
                request.Address = new Uri(redirectUrl);
                await FetchResponseHeaders(request, true, cancelToken).ConfigureAwait(false);
                return;
            }

            EnsureResponseAddressIsSameWithOrigin(request, response);
        }
        catch (HttpRequestException exp) when (addRange && exp.IsRequestedRangeNotSatisfiable())
        {
            await FetchResponseHeaders(request, false, cancelToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exp) when (request.Configuration.AllowAutoRedirect &&
                                               exp.IsRedirectError() &&
                                               ResponseHeaders.TryGetValue("location", out var redirectedUrl) &&
                                               !string.IsNullOrWhiteSpace(redirectedUrl) &&
                                               !request.Address.ToString().Equals(redirectedUrl, StringComparison.OrdinalIgnoreCase))
        {
            request.Address = new Uri(redirectedUrl);
            await FetchResponseHeaders(request, true, cancelToken).ConfigureAwait(false);
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
    internal Uri GetRedirectUrl(HttpResponseMessage response)
    {
        // https://github.com/dotnet/runtime/issues/23264
        Uri redirectLocation = response?.Headers.Location;
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
            return GetTotalSizeFromContentRange(ResponseHeaders.ToDictionary());
        }

        return GetTotalSizeFromContentLength(ResponseHeaders.ToDictionary());
    }

    internal long GetTotalSizeFromContentLength(Dictionary<string, string> headers)
    {
        // gets the total size from the content length headers.
        if (headers.TryGetValue(HeaderContentLengthKey, out string contentLengthText) &&
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

        await FetchResponseHeaders(request, addRange: true).ConfigureAwait(false);

        // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.5
        if (ResponseHeaders.TryGetValue(HeaderAcceptRangesKey, out string acceptRanges) &&
            acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            _isSupportDownloadInRange = false;
            return false;
        }

        // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.16
        if (ResponseHeaders.TryGetValue(HeaderContentRangeKey, out string contentRange))
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
    internal long GetTotalSizeFromContentRange(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue(HeaderContentRangeKey, out string contentRange) &&
            string.IsNullOrWhiteSpace(contentRange) == false &&
            _contentRangePattern.IsMatch(contentRange))
        {
            Match match = _contentRangePattern.Match(contentRange);
            string size = match.Groups["size"].Value;
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
    internal async Task<string> GetUrlDispositionFilenameAsync(Request request)
    {
        try
        {
            // Validate URL format
            if (request.Address?.IsAbsoluteUri != true ||
                !(request.Address.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                  request.Address.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) ||
                string.IsNullOrWhiteSpace(request.Address.Host) ||
                string.IsNullOrWhiteSpace(request.Address.AbsolutePath) ||
                request.Address.AbsolutePath == "/" ||
                request.Address.Segments.Length <= 1)
            {
                return null;
            }

            // Fetch headers if validations pass
            await FetchResponseHeaders(request, true).ConfigureAwait(false);

            if (ResponseHeaders.TryGetValue(HeaderContentDispositionKey, out string disposition))
            {
                string filename = request.ToUnicode(disposition)
                    ?.Split(';')
                    .FirstOrDefault(part => part.Trim().StartsWith(FilenameStartPointKey, StringComparison.OrdinalIgnoreCase))
                    ?.Replace(FilenameStartPointKey, "")
                    .Replace("\"", "")
                    .Trim();

                return string.IsNullOrWhiteSpace(filename) ? null : filename;
            }
        }
        catch
        {
            // Ignore exceptions
        }

        return null;
    }

    /// <summary>
    /// Gets the response stream asynchronously.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancelToken"></param>
    /// <exception cref="HttpRequestException"></exception>
    public async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request,
        CancellationToken cancelToken = default)
    {
        HttpResponseMessage response = await Client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken)
            .ConfigureAwait(false);

        // Copy all response headers to our dictionary
        ResponseHeaders.Clear();
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
        {
            ResponseHeaders.TryAdd(header.Key, header.Value.FirstOrDefault());
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
        {
            ResponseHeaders.TryAdd(header.Key, header.Value.FirstOrDefault());
        }

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