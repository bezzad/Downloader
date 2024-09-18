using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class DummyApiException : WebException
{
    public DummyApiException(string message)
        : base(message, WebExceptionStatus.Timeout)
    {
    }
}