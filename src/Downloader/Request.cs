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
        private readonly Dictionary<string, string> _responseHeaders;
        private readonly RequestConfiguration _configuration;
        public Uri Address { get; }

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
        
        private HttpWebRequest GetRequest(string method)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.CreateDefault(Address);
            request.Timeout = -1;
            request.Method = method;
            request.ContentType = "application/x-www-form-urlencoded;charset=utf-8;";
            request.Accept = _configuration.Accept;
            request.KeepAlive = _configuration.KeepAlive;
            request.AllowAutoRedirect = _configuration.AllowAutoRedirect;
            request.AutomaticDecompression = _configuration.AutomaticDecompression;
            request.UserAgent = _configuration.UserAgent;
            request.ProtocolVersion = _configuration.ProtocolVersion;
            request.UseDefaultCredentials = _configuration.UseDefaultCredentials;
            request.SendChunked = _configuration.SendChunked;
            request.TransferEncoding = _configuration.TransferEncoding;
            request.Expect = _configuration.Expect;
            request.MaximumAutomaticRedirections = _configuration.MaximumAutomaticRedirections;
            request.MediaType = _configuration.MediaType;
            request.PreAuthenticate = _configuration.PreAuthenticate;
            request.Credentials = _configuration.Credentials;
            request.ClientCertificates = _configuration.ClientCertificates;
            request.Referer = _configuration.Referer;
            request.Pipelined = _configuration.Pipelined;
            request.Proxy = _configuration.Proxy;

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
            if (_responseHeaders.Any())
            {
                return;
            }

            WebResponse response = await GetRequest().GetResponseAsync();
            if (response?.SupportsHeaders == true)
            {
                foreach (string headerKey in response.Headers.AllKeys)
                {
                    string headerValue = response.Headers[headerKey];
                    _responseHeaders.Add(headerKey, headerValue);
                }
            }
        }

        public async Task<long> GetFileSize()
        {
            await FetchResponseHeaders();
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
                    await FetchResponseHeaders();
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