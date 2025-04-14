using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class DummyApiException(string message) : WebException(message, WebExceptionStatus.Timeout);