using Downloader.Exceptions;
using System.Net;
using System.Net.Sockets;

namespace Downloader.Extensions;

internal static class ExceptionHelper
{
    internal static bool IsRedirectStatus(this HttpStatusCode statusCode)
    {
        return statusCode is
            HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    extension(Exception error)
    {
        internal bool IsRequestedRangeNotSatisfiable()
        {
            return error is HttpRequestException { StatusCode: HttpStatusCode.RequestedRangeNotSatisfiable };
        }

        internal bool IsMomentumError()
        {
            // A storage error (disk full, drive removed) does not fix itself by asking the server again.
            if (error is StorageException)
                return false;

            // Classify retry-ability by exception type and HTTP status code only — never by
            // Exception.Source. Source is derived from stack/reflection metadata that is empty
            // under AOT/trimming, which previously made identical errors fatal only in AOT builds
            // (issue #226). Type/status classification is deterministic across JIT and AOT.
            bool isMomentum = error switch {
                // Transient transport / timeout / stream-teardown failures.
                SocketException => true,
                IOException => true, // includes HttpIOException (connection reset / premature end)
                TaskCanceledException => true, // request/read timeout (user cancellation is checked before this is consulted)
                ObjectDisposedException => true, // stream closed on timeout/cancel
                WebException { Status: WebExceptionStatus.Timeout } => true,

                // HTTP responses: retry only transient / overload / redirect statuses.
                HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError } => false, // a bad certificate does not fix itself
                HttpRequestException { StatusCode: null } => true, // no response received → transport failure
                HttpRequestException {
                    StatusCode:
                        HttpStatusCode.RequestTimeout or       // 408
                        HttpStatusCode.PreconditionRequired or // 428 — some CDNs (e.g. BunnyCDN) use it as a concurrency throttle (#226)
                        HttpStatusCode.TooManyRequests or      // 429
                        HttpStatusCode.ServiceUnavailable or   // 503
                        HttpStatusCode.GatewayTimeout or       // 504
                        HttpStatusCode.Ambiguous or            // 300
                        HttpStatusCode.Moved or                // 301
                        HttpStatusCode.Redirect or             // 302
                        HttpStatusCode.RedirectMethod or       // 303
                        HttpStatusCode.TemporaryRedirect or    // 307
                        HttpStatusCode.PermanentRedirect       // 308
                } => true,

                // Permanent client errors (400/401/403/404/...) and server errors such as
                // 500/502 are not worth retrying.
                _ => false
            };

            if (isMomentum)
                return true;

            return error.InnerException?.IsMomentumError() ?? false;
        }

        internal bool IsRedirectError()
        {
            return error is HttpRequestException { StatusCode: not null } responseException &&
                   responseException.StatusCode.Value.IsRedirectStatus();
        }
    }
}
