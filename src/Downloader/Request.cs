using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Downloader
{
    public class Request
    {
        public Request(string address, RequestConfiguration config = null)
        {
            if (Uri.TryCreate(address, UriKind.Absolute, out var uri) == false)
                uri = new Uri(new Uri("http://localhost"), address);

            Address = uri;
            Configuration = config ?? new RequestConfiguration();
        }


        public const string HeadRequestMethod = "HEAD";
        public const string GetRequestMethod = "GET";
        public Uri Address { get; }
        public RequestConfiguration Configuration { get; }

        protected async Task<long> GetSafeContentLength(HttpWebRequest request)
        {
            try
            {
                using var response = await request.GetResponseAsync();
                if (response.SupportsHeaders)
                    return response.ContentLength;
            }
            catch (WebException exp)
                when (exp.Response is HttpWebResponse response &&
                      (response.StatusCode == HttpStatusCode.MethodNotAllowed
                       || response.StatusCode == HttpStatusCode.Forbidden))
            {
                // ignore WebException, Request method 'HEAD' not supported from host!
            }

            return -1L;
        }
        protected HttpWebRequest GetRequest(string method)
        {
            var request = (HttpWebRequest)WebRequest.CreateDefault(Address);
            request.Timeout = -1;
            request.Method = method;
            request.Accept = Configuration.Accept;
            request.KeepAlive = Configuration.KeepAlive;
            request.AllowAutoRedirect = Configuration.AllowAutoRedirect;
            request.AutomaticDecompression = Configuration.AutomaticDecompression;
            request.UserAgent = Configuration.UserAgent;
            request.ProtocolVersion = Configuration.ProtocolVersion;
            request.UseDefaultCredentials = Configuration.UseDefaultCredentials;
            request.SendChunked = Configuration.SendChunked;
            request.TransferEncoding = Configuration.TransferEncoding;
            request.Expect = Configuration.Expect;
            request.MaximumAutomaticRedirections = Configuration.MaximumAutomaticRedirections;
            request.MediaType = Configuration.MediaType;
            request.PreAuthenticate = Configuration.PreAuthenticate;
            request.Credentials = Configuration.Credentials;
            request.ClientCertificates = Configuration.ClientCertificates;
            request.Referer = Configuration.Referer;
            request.Pipelined = Configuration.Pipelined;
            request.Proxy = Configuration.Proxy;

            if (Configuration.IfModifiedSince.HasValue)
                request.IfModifiedSince = Configuration.IfModifiedSince.Value;

            return request;
        }

        public HttpWebRequest HeadRequest()
        {
            return GetRequest(HeadRequestMethod);
        }
        public HttpWebRequest GetRequest()
        {
            return GetRequest(GetRequestMethod);
        }
        public async Task<long> GetFileSize()
        {
            var size = await GetFileSizeWithHeadRequest();
            if (size <= 0)
                size = await GetFileSizeWithGetRequest();

            return size;
        }
        public async Task<long> GetFileSizeWithHeadRequest()
        {
            var request = HeadRequest();
            return await GetSafeContentLength(request);
        }
        public async Task<long> GetFileSizeWithGetRequest()
        {
            var request = GetRequest();
            return await GetSafeContentLength(request);
        }
        public string GetFileName()
        {
            return Path.GetFileName(Address.LocalPath);
        }
    }
}
