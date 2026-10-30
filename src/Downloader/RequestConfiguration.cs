using System;
using System.Net;
using System.Net.Cache;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace Downloader;

/// <summary>
/// Represents the configuration settings for an HTTP request.
/// </summary>
public class RequestConfiguration
{
    private static readonly Version ZeroVersion = new(0, 0, 0, 0);

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestConfiguration"/> class with default settings.
    /// </summary>
    public RequestConfiguration()
    {
        Headers = ["Accept-Encoding: identity"];
        AllowAutoRedirect = true;
        AutomaticDecompression = DecompressionMethods.None;
        ClientCertificates = [];
        // A non-null container enables cookie handling on the HttpClient handler so that
        // session/challenge cookies (e.g. ArvanCloud/Cloudflare "307 to self" cookie walls)
        // issued mid-redirect are stored and replayed on the follow-up request.
        CookieContainer = new CookieContainer();
        ImpersonationLevel = TokenImpersonationLevel.Delegation;
        KeepAlive = false; // Please keep this in false. Because of an error (An existing connection was forcibly closed by the remote host)
        KeepAliveTimeout = TimeSpan.FromMinutes(30); // Keep connections alive for 30 minutes (Pause Download maybe far more than 30 minutes)
        MaximumAutomaticRedirections = 50;
        Pipelined = true;
        ProtocolVersion = HttpVersion.Version11;
        ConnectTimeout = 30 * 1000; // 30 seconds
        UserAgent = BuildDefaultUserAgent();
    }

    private static string BuildDefaultUserAgent()
    {
        const string fallbackVersion = "5.0";
        string version = ResolveProductVersion();
        return $"{nameof(Downloader)}/{version ?? fallbackVersion}";
    }

    private static string ResolveProductVersion()
    {
        Assembly assembly = typeof(RequestConfiguration).Assembly;

        string version = NormalizeVersion(assembly.GetName().Version?.ToString(3));
        if (!string.IsNullOrWhiteSpace(version))
            return version;

        version = NormalizeVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        if (!string.IsNullOrWhiteSpace(version))
            return version;

        version = NormalizeVersion(assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        if (!string.IsNullOrWhiteSpace(version))
            return version;

        return null;
    }

    private static string NormalizeVersion(string versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
            return null;

        string candidate = versionText.Trim();
        int metadataSeparatorIndex = candidate.IndexOf('+');
        if (metadataSeparatorIndex >= 0)
            candidate = candidate[..metadataSeparatorIndex];

        int preReleaseSeparatorIndex = candidate.IndexOf('-');
        if (preReleaseSeparatorIndex >= 0)
            candidate = candidate[..preReleaseSeparatorIndex];

        if (!Version.TryParse(candidate, out Version parsed) || parsed == ZeroVersion)
            return null;

        return parsed.Build >= 0 ? parsed.ToString(3) : parsed.ToString(2);
    }

    /// <summary>
    /// A <see cref="string"/> containing the value of the HTTP Accept header. The default value of this property is null.
    /// When it is null or empty, <c>*/*</c> is sent.
    /// </summary>
    public string Accept { get; set; }

    /// <summary>
    /// Set to true to allow the requests to automatically follow HTTP redirection headers to the new
    /// location of a resource. Default value is true.
    /// The maximum number of redirections to follow is set by the
    /// <see cref="MaximumAutomaticRedirections"/> property.
    /// </summary>
    public bool AllowAutoRedirect { get; set; }

    /// <summary>
    /// Gets or sets the <c>Authorization</c> header that is sent on every request.
    /// When set, it is used instead of the header built from <see cref="Credentials"/>.
    /// </summary>
    public AuthenticationHeaderValue Authorization { get; set; }

    /// <summary>
    /// A <see cref="DecompressionMethods"/> object that indicates the type of decompression that is used.
    /// Default value is None;
    /// </summary>
    /// <exception cref="InvalidOperationException">The object's current state does not allow this property to be set.</exception>
    public DecompressionMethods AutomaticDecompression { get; set; }

    /// <summary>
    /// Gets or sets the cache policy for this request.
    /// </summary>
    [Obsolete("This property is obsolete and has no effect.")]
    public RequestCachePolicy CachePolicy { get; set; }

    /// <summary>
    /// When overridden in a descendant class, gets or sets the name of the connection group for the request.
    /// </summary>
    [Obsolete("This property is obsolete and has no effect.")]
    public string ConnectionGroupName { get; set; }

    /// <summary>
    /// The <see cref="X509CertificateCollection"/> that contains the security certificates associated with this request.
    /// </summary>
    /// <remarks>
    /// The Framework caches SSL sessions as they are created and attempts to reuse a cached session for a new request, if
    /// possible.
    /// When attempting to reuse an SSL session, the Framework uses the first element of <see cref="ClientCertificates"/>
    /// (if there is one),
    /// or tries to reuse an anonymous sessions if <see cref="ClientCertificates"/> is empty.
    /// For performance reasons, you shouldn't add a client certificate to a
    /// <see cref="HttpWebRequest"/> unless you know the server will ask for it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value specified for a set operation is null.</exception>
    public X509CertificateCollection ClientCertificates { get; set; }

    /// <summary>
    /// Gets or sets an extra media type that is added to the request's <c>Accept</c> header.
    /// A download request is a GET without a body, so no <c>Content-Type</c> header is sent.
    /// The default value is null.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the cookies associated with the request. Defaults to a new, empty
    /// <see cref="CookieContainer"/> so that session/challenge cookies issued by the server
    /// during a redirect are captured and sent on subsequent requests. Set to <c>null</c>
    /// to disable cookie handling entirely.
    /// </summary>
    public CookieContainer CookieContainer { get; set; }

    /// <summary>
    /// Gets or sets the credentials for the request. The default is null.
    /// </summary>
    /// <remarks>
    /// The credentials are given to the HTTP handler. When this is a <see cref="NetworkCredential"/> (or a
    /// <see cref="CredentialCache"/> with a "Basic" entry for the address), and <see cref="Authorization"/>
    /// is not set, they are also sent as a Basic <c>Authorization</c> header on every request.
    /// </remarks>
    public ICredentials Credentials { get; set; }

    /// <summary>
    /// Gets or sets the value of the HTTP <c>Expect</c> header. The default value is null.
    /// </summary>
    public string Expect { get; set; }

    /// <summary>
    /// Gets or sets extra headers that are added to every request.
    /// The default list contains <c>Accept-Encoding: identity</c>, so servers send the file uncompressed.
    /// </summary>
    public WebHeaderCollection Headers { get; set; }

    /// <summary>
    /// Gets or sets the value of the HTTP <c>If-Modified-Since</c> header.
    /// The default value is null, so the header is not sent.
    /// </summary>
    public DateTime? IfModifiedSince { get; set; }

    /// <summary>
    /// Gets or sets the impersonation level for the current request.
    /// </summary>
    /// <remarks>Not used by the downloader; this property has no effect.</remarks>
    public TokenImpersonationLevel ImpersonationLevel { get; set; }

    /// <summary>
    /// Gets or sets whether connections are kept open. When true, the requests send
    /// <c>Connection: keep-alive</c> and HTTP/2 keep-alive pings are enabled. When false, the requests send
    /// <c>Connection: close</c>. The default value is false.
    /// </summary>
    public bool KeepAlive { get; set; }
    
    /// <summary>
    /// Gets or sets how long an idle connection stays in the pool, and the keep-alive ping timeout when
    /// <see cref="KeepAlive"/> is true. The default value is 30 minutes.
    /// </summary>
    public TimeSpan KeepAliveTimeout { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of redirects to follow. Must be greater than zero.
    /// The default value is 50.
    /// </summary>
    public int MaximumAutomaticRedirections { get; set; }

    /// <summary>
    /// Gets or sets a preference for pipelined connections.
    /// </summary>
    /// <remarks>Not used by the downloader; this property has no effect.</remarks>
    public bool Pipelined { get; set; }

    /// <summary>
    /// Gets or sets whether the HTTP handler sends the <see cref="Credentials"/> with the first request,
    /// without waiting for an authentication challenge from the server. The default is false.
    /// </summary>
    public bool PreAuthenticate { get; set; }

    /// <summary>
    /// Gets or sets the HTTP version to ask for. The default is HTTP/1.1.
    /// A lower version is used when the server does not support the requested one.
    /// </summary>
    public Version ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets the proxy to use. The default value is null, which means no proxy is used
    /// (the system proxy settings are not used either).
    /// </summary>
    public IWebProxy Proxy { get; set; }

    /// <summary>
    /// A <see cref="string"/> containing the value of the HTTP Referer header. The default value is null.
    /// Note: For additional information see section 14.36 of IETF RFC 2616 - HTTP/1.1.
    /// </summary>
    public string Referer { get; set; }

    /// <summary>
    /// When System.Net.HttpWebRequest.SendChunked is true, the request sends data to the destination in segments.
    /// The destination server is required to support receiving chunked data. The default value is false.
    /// Set this property to true only if the server specified by the System.Net.HttpWebRequest.
    /// Address property of the current instance accepts chunked data (i.e. is HTTP/1.1 or greater in compliance).
    /// If the server does not accept chunked data, buffer all data to be written and send a HTTP Content-Length header
    /// with the buffered data.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     A set operation was requested but data has already been written to the request data stream.
    /// </exception>
    [Obsolete("This property is obsolete and has no effect.")]
    public bool SendChunked { get; set; }

    /// <summary>
    /// Gets or sets the timeout value in milliseconds for the the connection to be established
    /// </summary>
    public int ConnectTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value that is added to the request's <c>Accept-Encoding</c> and
    /// <c>Transfer-Encoding</c> headers. The default value is null.
    /// </summary>
    public string TransferEncoding { get; set; }

    /// <summary>
    /// Gets or sets whether the credentials of the current user are used.
    /// </summary>
    /// <remarks>Not used by the downloader; this property has no effect. Set <see cref="Credentials"/> instead.</remarks>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// A <see cref="string"/> containing the value of the HTTP User-agent header.
    /// The default value is "<seealso cref="Downloader"/>/{<seealso cref="Version"/>}".
    /// </summary>
    public string UserAgent { get; set; }
}
