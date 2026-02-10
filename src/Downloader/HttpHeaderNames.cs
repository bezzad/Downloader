namespace Downloader;

/// <summary>
/// Provides standard HTTP header name constants.
/// Header names are case-insensitive per RFC 7230, but these values
/// follow the canonical casing used in the HTTP specification.
/// </summary>
public static class HttpHeaderNames
{
    // General Headers
    public const string CacheControl = "Cache-Control";
    public const string Connection = "Connection";
    public const string Date = "Date";
    public const string Pragma = "Pragma";
    public const string Trailer = "Trailer";
    public const string TransferEncoding = "Transfer-Encoding";
    public const string Upgrade = "Upgrade";
    public const string Via = "Via";
    public const string Warning = "Warning";

    // Request Headers
    public const string Accept = "Accept";
    public const string AcceptCharset = "Accept-Charset";
    public const string AcceptEncoding = "Accept-Encoding";
    public const string AcceptLanguage = "Accept-Language";
    public const string Authorization = "Authorization";
    public const string Cookie = "Cookie";
    public const string Expect = "Expect";
    public const string From = "From";
    public const string Host = "Host";
    public const string IfMatch = "If-Match";
    public const string IfModifiedSince = "If-Modified-Since";
    public const string IfNoneMatch = "If-None-Match";
    public const string IfRange = "If-Range";
    public const string IfUnmodifiedSince = "If-Unmodified-Since";
    public const string MaxForwards = "Max-Forwards";
    public const string ProxyAuthorization = "Proxy-Authorization";
    public const string Range = "Range";
    public const string Referer = "Referer";
    public const string TE = "TE";
    public const string UserAgent = "User-Agent";

    // Response Headers
    public const string AcceptRanges = "Accept-Ranges";
    public const string Age = "Age";
    public const string ETag = "ETag";
    public const string Location = "Location";
    public const string ProxyAuthenticate = "Proxy-Authenticate";
    public const string RetryAfter = "Retry-After";
    public const string Server = "Server";
    public const string SetCookie = "Set-Cookie";
    public const string Vary = "Vary";
    public const string WWWAuthenticate = "WWW-Authenticate";

    // Content Headers
    public const string ContentDisposition = "Content-Disposition";
    public const string ContentEncoding = "Content-Encoding";
    public const string ContentLanguage = "Content-Language";
    public const string ContentLength = "Content-Length";
    public const string ContentLocation = "Content-Location";
    public const string ContentRange = "Content-Range";
    public const string ContentType = "Content-Type";
    public const string Expires = "Expires";
    public const string LastModified = "Last-Modified";
}