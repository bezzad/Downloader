using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;

namespace Downloader.DummyHttpServer;

[ExcludeFromCodeCoverage]
public class DummyApiException(string message) : HttpRequestException(message, null, HttpStatusCode.GatewayTimeout);