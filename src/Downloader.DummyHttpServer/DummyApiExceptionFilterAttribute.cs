using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using static System.Console;

namespace Downloader.DummyHttpServer;

public class DummyApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is DummyApiException)
        {
            context.Result = new StatusCodeResult((int)HttpStatusCode.RequestedRangeNotSatisfiable);
        }
        else
        {
            WriteLine($"Exception on {context.ActionDescriptor.DisplayName}: {context.Exception.Message}");
        }
    }
}