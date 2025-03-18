using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a class for making HTTP requests and handling response headers.
/// </summary>
public class Request
{
    private const string HeaderContentLengthKey = "Content-Length";
    private const string HeaderContentDispositionKey = "Content-Disposition";
    private const string HeaderContentRangeKey = "Content-Range";
    private const string HeaderAcceptRangesKey = "Accept-Ranges";
    private readonly HttpClient _client = new();
    private readonly RequestConfiguration _configuration;
    private readonly Dictionary<string, string> _responseHeaders;
    private readonly Regex _contentRangePattern;

    /// <summary>
    /// Gets the URI address of the request.
    /// </summary>
    public Uri Address { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class with the specified address.
    /// </summary>
    /// <param name="address">The URL address to create the request for.</param>
    public Request(string address) : this(address, new RequestConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class with the specified address and configuration.
    /// </summary>
    /// <param name="address">The URL address to create the request for.</param>
    /// <param name="config">The configuration for the request.</param>
    public Request(string address, RequestConfiguration config)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out Uri uri) == false)
        {
            uri = new Uri(new Uri("http://localhost"), address);
        }

        c = uri;
        _configuration = config ?? new RequestConfiguration();
        _responseHeaders = new Dictionary<string, string>();
        _contentRangePattern = new Regex(@"bytes\s*((?<from>\d*)\s*-\s*(?<to>\d*)|\*)\s*\/\s*(?<size>\d+|\*)",
            RegexOptions.Compiled);
    }

    /// <summary>
    /// Creates an HTTP request with the specified method.
    /// </summary>
    /// <returns>An instance of <see cref="SocketsHttpHandler"/> representing the HTTP request.</returns>
    public HttpRequestMessage GetRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, Address);
        request.RequestUri = Address;
        request.Version = _configuration.ProtocolVersion;

        return request;
    }

    /// <summary>
    /// Fetches the response headers asynchronously.
    /// </summary>
    /// <param name="addRange">Indicates whether to add a range header to the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task FetchResponseHeaders(bool addRange = true)
    {
        try
        {
            if (_responseHeaders.Count > 0)
            {
                return;
            }

            HttpRequestMessage request = GetRequest();
            if (addRange) // to check the content range supporting
                request.Headers.Range = new RangeHeaderValue(0, 0); // first byte

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.EnsureSuccessStatusCode().IsSuccessStatusCode)
            {
                response.Headers.ToList()
                    .ForEach(header => _responseHeaders.Add(header.Key, header.Value.FirstOrDefault()));
            }

            EnsureResponseAddressIsSameWithOrigin(response);
            foreach (var header in response.Headers)
            {
                _responseHeaders.Add(header.Key, header.Value.ToString());
            }
        }
        catch (WebException exp) when (_configuration.AllowAutoRedirect &&
                                       exp.Response is HttpWebResponse {
                                           SupportsHeaders: true,
                                           StatusCode:
                                           HttpStatusCode.Found or
                                           HttpStatusCode.Moved or
                                           HttpStatusCode.MovedPermanently or
                                           HttpStatusCode.RequestedRangeNotSatisfiable
                                       } response)
        {
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                await FetchResponseHeaders(addRange: false).ConfigureAwait(false);
            }
            else
            {
                var redirectedUrl = response?.Headers["location"];
                if (!string.IsNullOrWhiteSpace(redirectedUrl) &&
                    Address.ToString().Equals(redirectedUrl, StringComparison.OrdinalIgnoreCase) == false)
                {
                    Address = new Uri(redirectedUrl);
                    await FetchResponseHeaders().ConfigureAwait(false);
                    return;
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Ensures that the response address is the same as the original address.
    /// </summary>
    /// <param name="response">The web response to check.</param>
    /// <returns>True if the response address is the same as the original address; otherwise, false.</returns>
    private bool EnsureResponseAddressIsSameWithOrigin(HttpResponseMessage response)
    {
        var redirectUri = GetRedirectUrl(response);
        if (redirectUri.Equals(Address) == false)
        {
            Address = redirectUri;
            return false;
        }

        return true;
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

        return Address;
    }

    /// <summary>
    /// Gets the file size asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file size.</returns>
    public async Task<long> GetFileSize()
    {
        if (await IsSupportDownloadInRange().ConfigureAwait(false))
        {
            return GetTotalSizeFromContentRange(_responseHeaders);
        }

        return GetTotalSizeFromContentLength(_responseHeaders);
    }

    /// <summary>
    /// Throws an exception if the download in range is not supported.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ThrowIfIsNotSupportDownloadInRange()
    {
        var isSupport = await IsSupportDownloadInRange().ConfigureAwait(false);
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
    public async Task<bool> IsSupportDownloadInRange()
    {
        await FetchResponseHeaders().ConfigureAwait(false);

        // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.5
        if (_responseHeaders.TryGetValue(HeaderAcceptRangesKey, out string acceptRanges) &&
            acceptRanges.ToLower() == "none")
        {
            return false;
        }

        // https://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.16
        if (_responseHeaders.TryGetValue(HeaderContentRangeKey, out string contentRange))
        {
            if (string.IsNullOrWhiteSpace(contentRange) == false)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the total size from the content range headers.
    /// </summary>
    /// <param name="headers">The headers to get the total size from.</param>
    /// <returns>The total size of the content.</returns>
    public long GetTotalSizeFromContentRange(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue(HeaderContentRangeKey, out string contentRange) &&
            string.IsNullOrWhiteSpace(contentRange) == false &&
            _contentRangePattern.IsMatch(contentRange))
        {
            var match = _contentRangePattern.Match(contentRange);
            var size = match.Groups["size"].Value;
            //var from = match.Groups["from"].Value;
            //var to = match.Groups["to"].Value;

            return long.TryParse(size, out var totalSize) ? totalSize : -1L;
        }

        return -1L;
    }

    /// <summary>
    /// Gets the total size from the content length headers.
    /// </summary>
    /// <param name="headers">The headers to get the total size from.</param>
    /// <returns>The total size of the content.</returns>
    public long GetTotalSizeFromContentLength(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue(HeaderContentLengthKey, out string contentLengthText) &&
            long.TryParse(contentLengthText, out long contentLength))
        {
            return contentLength;
        }

        return -1L;
    }

    /// <summary>
    /// Gets the file name asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file name.</returns>
    public async Task<string> GetFileName()
    {
        var filename = await GetUrlDispositionFilenameAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(filename))
        {
            filename = GetFileNameFromUrl();
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = Guid.NewGuid().ToString("N");
            }
        }

        return filename;
    }

    /// <summary>
    /// Gets the file name from the URL.
    /// </summary>
    /// <returns>The file name extracted from the URL.</returns>
    public string GetFileNameFromUrl()
    {
        string filename = Path.GetFileName(Address.LocalPath);
        int queryIndex = filename.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            filename = filename[..queryIndex];
        }

        return filename;
    }

    /// <summary>
    /// Gets the file name from the URL disposition header asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the file name.</returns>
    public async Task<string> GetUrlDispositionFilenameAsync()
    {
        try
        {
            if (Address?.IsWellFormedOriginalString() == true
                && Address?.Segments.Length > 1)
            {
                await FetchResponseHeaders().ConfigureAwait(false);
                if (_responseHeaders.TryGetValue(HeaderContentDispositionKey, out string disposition))
                {
                    string unicodeDisposition = ToUnicode(disposition);
                    if (string.IsNullOrWhiteSpace(unicodeDisposition) == false)
                    {
                        string filenameStartPointKey = "filename=";
                        string[] dispositionParts = unicodeDisposition.Split(';');
                        string filenamePart = dispositionParts.FirstOrDefault(part => part.Trim()
                            .StartsWith(filenameStartPointKey, StringComparison.OrdinalIgnoreCase));
                        if (string.IsNullOrWhiteSpace(filenamePart) == false)
                        {
                            string filename = filenamePart.Replace(filenameStartPointKey, "")
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
    /// Converts the specified text from 'latin-1' encoding to 'utf-8' encoding.
    /// </summary>
    /// <param name="otherEncodedText">The text to convert.</param>
    /// <returns>The converted text in 'utf-8' encoding.</returns>
    public string ToUnicode(string otherEncodedText)
    {
        // decode 'latin-1' to 'utf-8'
        return Encoding.UTF8.GetString(Encoding.GetEncoding("iso-8859-1").GetBytes(otherEncodedText));
    }
}