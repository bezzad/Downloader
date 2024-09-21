using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a class for making HTTP requests and handling response headers.
/// </summary>
public class Request
{
    private const string GetRequestMethod = "GET";
    private const string HeaderContentLengthKey = "Content-Length";
    private const string HeaderContentDispositionKey = "Content-Disposition";
    private const string HeaderContentRangeKey = "Content-Range";
    private const string HeaderAcceptRangesKey = "Accept-Ranges";
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

        Address = uri;
        _configuration = config ?? new RequestConfiguration();
        _responseHeaders = new Dictionary<string, string>();
        _contentRangePattern = new Regex(@"bytes\s*((?<from>\d*)\s*-\s*(?<to>\d*)|\*)\s*\/\s*(?<size>\d+|\*)",
            RegexOptions.Compiled);
    }

    /// <summary>
    /// Creates an HTTP request with the specified method.
    /// </summary>
    /// <param name="method">The HTTP method to use for the request.</param>
    /// <returns>An instance of <see cref="HttpWebRequest"/> representing the HTTP request.</returns>
    private HttpWebRequest GetRequest(string method)
    {
#pragma warning disable SYSLIB0014
        HttpWebRequest request = WebRequest.CreateHttp(Address);
#pragma warning restore SYSLIB0014
        request.UseDefaultCredentials = _configuration.UseDefaultCredentials; // Note: set default before other configs
        request.Headers = _configuration.Headers;
        request.Accept = _configuration.Accept;
        request.AllowAutoRedirect = _configuration.AllowAutoRedirect;
        request.AuthenticationLevel = _configuration.AuthenticationLevel;
        request.AutomaticDecompression = _configuration.AutomaticDecompression;
        request.CachePolicy = _configuration.CachePolicy;
        request.ClientCertificates = _configuration.ClientCertificates;
        request.ConnectionGroupName = _configuration.ConnectionGroupName;
        request.ContentType = _configuration.ContentType;
        request.CookieContainer = _configuration.CookieContainer;
        request.Expect = _configuration.Expect;
        request.ImpersonationLevel = _configuration.ImpersonationLevel;
        request.KeepAlive = _configuration.KeepAlive;
        request.MaximumAutomaticRedirections = _configuration.MaximumAutomaticRedirections;
        request.MediaType = _configuration.MediaType;
        request.Method = method;
        request.Pipelined = _configuration.Pipelined;
        request.PreAuthenticate = _configuration.PreAuthenticate;
        request.ProtocolVersion = _configuration.ProtocolVersion;
        request.Proxy = _configuration.Proxy;
        request.Referer = _configuration.Referer;
        request.SendChunked = _configuration.SendChunked;
        request.Timeout = _configuration.Timeout;
        request.TransferEncoding = _configuration.TransferEncoding;
        request.UserAgent = _configuration.UserAgent;

        if (_configuration.Credentials != null)
        {
            request.Credentials = _configuration.Credentials;
        }

        if (_configuration.IfModifiedSince.HasValue)
        {
            request.IfModifiedSince = _configuration.IfModifiedSince.Value;
        }

        return request;
    }

    /// <summary>
    /// Creates an HTTP GET request.
    /// </summary>
    /// <returns>An instance of <see cref="HttpWebRequest"/> representing the HTTP GET request.</returns>
    public HttpWebRequest GetRequest()
    {
        return GetRequest(GetRequestMethod);
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

            HttpWebRequest request = GetRequest();

            if (addRange) // to check the content range supporting
                request.AddRange(0, 0); // first byte

            using WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);
            EnsureResponseAddressIsSameWithOrigin(response);
            if (response.SupportsHeaders)
            {
                foreach (string headerKey in response.Headers.AllKeys)
                {
                    string headerValue = response.Headers[headerKey];
                    _responseHeaders.Add(headerKey, headerValue);
                }
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
            else if (EnsureResponseAddressIsSameWithOrigin(exp.Response) == false)
            {
                // Read the response to see if we have the redirected url
                await FetchResponseHeaders().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Ensures that the response address is the same as the original address.
    /// </summary>
    /// <param name="response">The web response to check.</param>
    /// <returns>True if the response address is the same as the original address; otherwise, false.</returns>
    private bool EnsureResponseAddressIsSameWithOrigin(WebResponse response)
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
    public Uri GetRedirectUrl(WebResponse response)
    {
        // https://github.com/dotnet/runtime/issues/23264
        var redirectLocation = response?.Headers["location"];
        if (string.IsNullOrWhiteSpace(redirectLocation) == false)
        {
            return new Uri(redirectLocation);
        }
        else if (response?.ResponseUri != null)
        {
            return response.ResponseUri;
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