using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Downloader.DummyHttpServer.Controllers;

[ApiController]
[Route("[controller]")]
[DummyApiExceptionFilter]
public class DummyFileController(ILogger<DummyFileController> logger) : ControllerBase
{
    /// <summary>
    /// Return the ordered bytes array according to the size.
    /// </summary>
    /// <param name="size">Size of the data</param>
    /// <returns>File stream</returns>
    [HttpGet]
    [Route("file/size/{size}")]
    public IActionResult GetFile(long size)
    {
        logger.LogTrace($"file/size/{size}");
        DummyLazyStream data = new(DummyDataType.Order, size);
        return File(data, "application/octet-stream", true);
    }

    /// <summary>
    /// Return the file stream with header or not. Filename just used in URL.
    /// </summary>
    /// <param name="fileName">The file name</param>        
    /// <param name="size">Query param of the file size</param>
    /// <param name="fillByte">single byte value to fill all of file data</param>
    /// <returns>File stream</returns>
    [Route("noheader/file/{fileName}")]
    public IActionResult GetFileWithNameNoHeader(string fileName, [FromQuery] long size, [FromQuery] byte? fillByte = null)
    {
        DummyLazyStream result;
        if (fillByte.HasValue)
        {
            logger.LogTrace($"noheader/file/{fileName}?size={size}&fillByte={fillByte}");
            result = new DummyLazyStream(DummyDataType.Single, size, fillByte.Value);
        }
        else
        {
            logger.LogTrace($"noheader/file/{fileName}?size={size}");
            result = new DummyLazyStream(DummyDataType.Order, size);
        }

        return Ok(result); // return stream without header data
    }

    /// <summary>
    /// Return the file stream with header or not. Filename just used in URL.
    /// </summary>
    /// <param name="fileName">The file name</param>        
    /// <param name="size">Query param of the file size</param>
    /// <param name="fillByte">single byte value to fill all of file data</param>
    /// <returns>File stream</returns>
    [Route("file/{fileName}")]
    public IActionResult GetFileWithName(string fileName, [FromQuery] long size, [FromQuery] byte? fillByte = null)
    {
        DummyLazyStream fileData;
        if (fillByte.HasValue)
        {
            logger.LogTrace($"file/{fileName}?size={size}&fillByte={fillByte}");
            fileData = new DummyLazyStream(DummyDataType.Single, size, fillByte.Value);
        }
        else
        {
            logger.LogTrace($"file/{fileName}?size={size}");
            fileData = new DummyLazyStream(DummyDataType.Order, size);
        }

        return File(fileData, "application/octet-stream", true);
    }

    /// <summary>
    /// Return the file stream with header content-length and filename.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="size">Size of the File</param>
    /// <param name="fillByte">Byte of filling value</param>
    /// <returns>File stream</returns>
    [Route("file/{fileName}/size/{size}")]
    public IActionResult GetFileWithContentDisposition(string fileName, long size, [FromQuery] byte? fillByte = null)
    {
        DummyLazyStream fileData;
        if (fillByte.HasValue)
        {
            logger.LogTrace($"file/{fileName}/size/{size}?fillByte={fillByte}");
            fileData = new DummyLazyStream(DummyDataType.Single, size, fillByte.Value);
        }
        else
        {
            logger.LogTrace($"file/{fileName}/size/{size}");
            fileData = new DummyLazyStream(DummyDataType.Order, size);
        }

        return File(fileData, "application/octet-stream", fileName, true);
    }

    /// <summary>
    /// Return the file stream with header content-length and filename.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="size">Size of the File</param>
    /// <param name="fillByte">Byte value of filling</param>
    /// <returns>File stream</returns>
    [Route("file/{fileName}/size/{size}/norange")]
    public IActionResult GetFileWithNoAcceptRange(string fileName, long size, [FromQuery] byte? fillByte = null)
    {
        DummyLazyStream fileData;
        if (fillByte.HasValue)
        {
            logger.LogTrace($"file/{fileName}/size/{size}/norange?fillByte={fillByte}");
            fileData = new DummyLazyStream(DummyDataType.Single, size, fillByte.Value);
        }
        else
        {
            logger.LogTrace($"file/{fileName}/size/{size}/norange");
            fileData = new DummyLazyStream(DummyDataType.Order, size);
        }

        return File(fileData, "application/octet-stream", fileName, false);
    }

    /// <summary>
    /// Return the file stream with header or not. Filename just used in URL.
    /// </summary>
    /// <param name="fileName">The file name</param>        
    /// <param name="size">Query param of the file size</param>
    /// <returns>File stream</returns>
    [Route("file/{fileName}/redirect")]
    public IActionResult GetFileWithNameOnRedirectUrl(string fileName, [FromQuery] long size)
    {
        logger.LogTrace($"file/{fileName}/redirect?size={size}");
        return LocalRedirectPermanent($"/dummyfile/file/{fileName}?size={size}");
    }

    /// <summary>
    /// Return the filled stream according to the size and failure after specific offset.
    /// </summary>
    /// <param name="size">Size of the data</param>
    /// <param name="offset">failure offset</param>
    /// <returns>File stream</returns>
    [HttpGet]
    [Route("file/size/{size}/failure/{offset}")]
    public IActionResult GetOverflowedFile(long size, int offset = 0)
    {
        try
        {
            logger.LogTrace($"file/size/{size}/failure/{offset}");
            return File(new MockMemoryStream(size, offset), "application/octet-stream", true);
        }
        catch (DummyApiException)
        {
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Return the filled stream according to the size and timeout after specific offset.
    /// </summary>
    /// <param name="size">Size of the data</param>
    /// <param name="offset">timeout offset</param>
    /// <returns>File stream</returns>
    [HttpGet]
    [Route("file/size/{size}/timeout/{offset}")]
    public IActionResult GetSlowFile(long size, int offset = 0)
    {
        try
        {
            logger.LogTrace($"file/size/{size}/timeout/{offset}");
            return File(new MockMemoryStream(size, offset, true), "application/octet-stream", true);
        }
        catch (HttpRequestException exp)
        {
            return new StatusCodeResult((int)(exp.StatusCode ?? HttpStatusCode.InternalServerError));
        }
    }

    /// <summary>
    /// Simulates a server that advertises a larger size on the range probe than it actually
    /// delivers on the body GET (issue #231). The range probe (<c>Range: bytes=0-0</c>) reports
    /// the full <paramref name="size"/> via <c>Content-Range</c>, but the body GET returns only
    /// <paramref name="actualSize"/> bytes with a matching <c>Content-Length</c> and a clean EOF —
    /// so HttpClient raises no transport error even though the chunk is left incomplete.
    /// </summary>
    /// <param name="fileName">The file name (used only in the URL).</param>
    /// <param name="size">The size advertised to the client on the range probe.</param>
    /// <param name="actualSize">The number of bytes actually delivered on the body GET.</param>
    [HttpGet]
    [Route("file/{fileName}/size/{size}/truncate/{actualSize}")]
    public async Task GetTruncatedFile(string fileName, long size, long actualSize)
    {
        logger.LogTrace($"file/{fileName}/size/{size}/truncate/{actualSize}");
        Response.ContentType = "application/octet-stream";
        Response.Headers.AcceptRanges = "bytes";

        string range = Request.Headers.Range.ToString();
        if (!string.IsNullOrEmpty(range))
        {
            // Range probe: advertise the full size and range support so the client believes
            // the file is `size` bytes long and builds its chunks accordingly.
            Response.StatusCode = (int)HttpStatusCode.PartialContent;
            Response.Headers.ContentRange = $"bytes 0-0/{size}";
            Response.ContentLength = 1;
            await Response.Body.WriteAsync(new byte[1]);
            return;
        }

        // Body GET: deliver only `actualSize` bytes with a matching Content-Length and a clean
        // EOF. The response is valid HTTP, so the client receives no error, yet the chunk ends
        // short of the advertised `size`.
        Response.StatusCode = (int)HttpStatusCode.OK;
        Response.ContentLength = actualSize;
        await Response.Body.WriteAsync(DummyData.GenerateOrderedBytes((int)actualSize));
    }

    /// <summary>
    /// Simulates a server that enforces a valid User-Agent header (issue #226).
    /// Returns HTTP 428 (Precondition Required) when the User-Agent is missing,
    /// empty, ends with '/', or matches the zero-version strings that AOT builds
    /// produce (Downloader/0.0.0 or Downloader/0.0.0.0).
    /// </summary>
    [Route("file/{fileName}/check-useragent")]
    public IActionResult GetFileWithUserAgentCheck(string fileName, [FromQuery] long size)
    {
        logger.LogTrace($"file/{fileName}/check-useragent?size={size}");
        string userAgent = Request.Headers.UserAgent.ToString();

        if (string.IsNullOrWhiteSpace(userAgent) ||
            userAgent.TrimEnd().EndsWith('/') ||
            string.Equals(userAgent, "Downloader/0.0.0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(userAgent, "Downloader/0.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(428); // Precondition Required
        }

        DummyLazyStream fileData = new(DummyDataType.Order, size);
        return File(fileData, "application/octet-stream", true);
    }
}