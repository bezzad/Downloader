using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer
{
    public class ExceptionHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is HttpRequestException { StatusCode: not null } requestException)
            {
                httpContext.Response.StatusCode = (int)requestException.StatusCode;
            }
            else
            {
                httpContext.Response.StatusCode = 504;
            }

            return ValueTask.FromResult(true);
        }
    }
}