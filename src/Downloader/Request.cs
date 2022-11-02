using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader
{
    internal class Request
    {
        private const string GetRequestMethod = "GET";
        private const string HeaderContentLengthKey = "Content-Length";
        private const string HeaderContentDispositionKey = "Content-Disposition";
        private const string HeaderContentRangeKey = "Content-Range";
        private const string HeaderAcceptRangesKey = "Accept-Ranges";
        private readonly RequestConfiguration _configuration;
        private readonly Dictionary<string, string> _responseHeaders;
        private readonly Regex _contentRangePattern;
        public Uri Address { get; private set; }

        public Request(string address) : this(address, new RequestConfiguration())
        { }

        public Request(string address, RequestConfiguration config)
        {
            if (Uri.TryCreate(address, UriKind.Absolute, out Uri uri) == false)
            {
                uri = new Uri(new Uri("http://localhost"), address);
            }

            Address = uri;
            _configuration = config ?? new RequestConfiguration();
            _responseHeaders = new Dictionary<string, string>();
            _contentRangePattern = new Regex(@"bytes\s*((?<from>\d*)\s*-\s*(?<to>\d*)|\*)\s*\/\s*(?<size>\d+|\*)", RegexOptions.Compiled);
        }

        private HttpWebRequest GetRequest(string method)
        {
            HttpWebRequest request = WebRequest.CreateHttp(Address);
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
        public HttpWebRequest GetRequest()
        {
            return GetRequest(GetRequestMethod);
        }

        private async Task FetchResponseHeaders(bool addRange = true)
        {
            try
            {
                if (_responseHeaders.Any())
                {
                    return;
                }

                HttpWebRequest request = GetRequest();

                if (addRange) // to check the content range supporting
                    request.AddRange(0, 0); // first byte

                using WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);
                EnsureResponseAddressIsSameWithOrigin(response);
                if (response?.SupportsHeaders == true)
                {
                    foreach (string headerKey in response.Headers.AllKeys)
                    {
                        string headerValue = response.Headers[headerKey];
                        _responseHeaders.Add(headerKey, headerValue);
                    }
                }
            }
            catch (WebException exp) when (_configuration.AllowAutoRedirect &&
                                           exp.Response is HttpWebResponse response &&
                                           response.SupportsHeaders &&
                                           (response.StatusCode == HttpStatusCode.Found ||
                                           response.StatusCode == HttpStatusCode.Moved ||
                                           response.StatusCode == HttpStatusCode.MovedPermanently ||
                                           response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable))
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

        public async Task<long> GetFileSize()
        {
            if (await IsSupportDownloadInRange())
            {
                return GetTotalSizeFromContentRange(_responseHeaders);
            }

            return GetTotalSizeFromContentLength(_responseHeaders);
        }

        public async Task ThrowIfIsNotSupportDownloadInRange()
        {
            var isSupport = await IsSupportDownloadInRange().ConfigureAwait(false);
            if (isSupport == false)
            {
                throw new NotSupportedException("The downloader cannot continue downloading because the network or server failed to download in range.");
            }
        }

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

        public long GetTotalSizeFromContentLength(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderContentLengthKey, out string contentLengthText) &&
                long.TryParse(contentLengthText, out long contentLength))
            {
                return contentLength;
            }

            return -1L;
        }

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

        public string GetFileNameFromUrl()
        {
            string filename = Path.GetFileName(Address.LocalPath);
            int queryIndex = filename.IndexOf("?", StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                filename = filename.Substring(0, queryIndex);
            }

            return filename;
        }

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
            catch (WebException e)
            {
                Debug.WriteLine(e);
                // No matter in this point
            }

            return null;
        }

        public string ToUnicode(string otherEncodedText)
        {
            // decode 'latin-1' to 'utf-8'
            string unicode = Encoding.UTF8.GetString(Encoding.GetEncoding("iso-8859-1").GetBytes(otherEncodedText));
            return unicode;
        }
    }
}