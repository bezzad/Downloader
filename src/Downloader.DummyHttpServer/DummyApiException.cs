using System.Net;

namespace Downloader.DummyHttpServer;

public class DummyApiException : WebException
{
    public DummyApiException(string message)
        : base(message, WebExceptionStatus.Timeout)
    {
    }
}