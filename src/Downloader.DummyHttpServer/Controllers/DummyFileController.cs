using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
        catch (DummyApiException)
        {
            return new StatusCodeResult(500);
        }
    }
}
