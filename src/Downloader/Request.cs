using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    public class Request
    {
        private const string GetRequestMethod = "GET";
        private const string HeaderContentLengthKey = "Content-Length";
        private const string HeaderContentDispositionKey = "Content-Disposition";
        private readonly RequestConfiguration _configuration;
        private readonly Dictionary<string, string> _responseHeaders;

        public Request(string address, RequestConfiguration config = null)
        {
            if (Uri.TryCreate(address, UriKind.Absolute, out Uri uri) == false)
            {
                uri = new Uri(new Uri("http://localhost"), address);
            }

            Address = uri;
            _configuration = config ?? new RequestConfiguration();
            _responseHeaders = new Dictionary<string, string>();
        }

        public Uri Address { get; private set; }

        private HttpWebRequest GetRequest(string method)
        {
            HttpWebRequest request = WebRequest.CreateHttp(Address);
            request.Headers = _configuration.Headers;
            request.Accept = _configuration.Accept;
            request.AllowAutoRedirect = _configuration.AllowAutoRedirect;
            request.AuthenticationLevel = _configuration.AuthenticationLevel;
            request.AutomaticDecompression = _configuration.AutomaticDecompression;
            request.CachePolicy = _configuration.CachePolicy;
            request.ClientCertificates = _configuration.ClientCertificates;
            request.ConnectionGroupName =  _configuration.ConnectionGroupName;
            request.ContentType = _configuration.ContentType;
            request.CookieContainer = _configuration.CookieContainer;
            request.Credentials = _configuration.Credentials;
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
            request.UseDefaultCredentials = _configuration.UseDefaultCredentials;
            request.UserAgent = _configuration.UserAgent;

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

        private async Task FetchResponseHeaders()
        {
            try
            {
                if (_responseHeaders.Any())
                {
                    return;
                }

                HttpWebRequest request = GetRequest();
                WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);
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
                                           response.StatusCode == HttpStatusCode.MovedPermanently))
            {
                // https://github.com/dotnet/runtime/issues/23264
                var redirectLocation = exp.Response?.Headers["location"];
                if (string.IsNullOrWhiteSpace(redirectLocation) == false)
                {
                    Address = new Uri(redirectLocation);
                    await FetchResponseHeaders().ConfigureAwait(false);
                }
            }
        }

        public async Task<long> GetFileSize()
        {
            await FetchResponseHeaders().ConfigureAwait(false);
            if (_responseHeaders.TryGetValue(HeaderContentLengthKey, out string contentLengthText))
            {
                if (long.TryParse(contentLengthText, out long contentLength))
                {
                    return contentLength;
                }
            }

            return -1L;
        }

        public string GetFileName()
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