using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Downloader.DummyHttpServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DummyFileController : ControllerBase
    {
        private readonly ILogger<DummyFileController> _logger;

        public DummyFileController(ILogger<DummyFileController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Return the ordered bytes array according to the size.
        /// </summary>
        /// <param name="size">Size of the data</param>
        /// <returns></returns>
        [HttpGet]
        [Route("file/size/{size}")]
        public IActionResult GetFile(int size)
        {
            _logger.Log(LogLevel.Information, $"file/size/{size}");
            var data = DummyData.GenerateOrderedBytes(size);
            return File(data, "application/octet-stream", true);
        }

        /// <summary>
        /// Return the file stream with header or not. Filename just used in URL.
        /// </summary>
        /// <param name="fileName">The file name</param>        
        /// <param name="size">Query param of the file size</param>
        /// <returns></returns>
        [Route("file/{fileName}")]
        public IActionResult GetFileWithName(string fileName, [FromQuery]int size, [FromQuery]bool noheader)
        {
            _logger.Log(LogLevel.Information, $"file/{fileName}?size={size}&noheader={noheader}");
            if (noheader)
            {
                var stream = new MemoryStream(DummyData.GenerateOrderedBytes(size));
                return Ok(stream); // return stream without header data
            }
            else
            {
                byte[] fileData = DummyData.GenerateOrderedBytes(size);
                return File(fileData, "application/octet-stream", true);
            }
        }

        /// <summary>
        /// Return the file stream with header content-length and filename.
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [Route("file/{fileName}/size/{size}")]
        public IActionResult GetFileWithContentDisposition(string fileName, int size)
        {
            _logger.Log(LogLevel.Information, $"file/{fileName}/size/{size}");
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", fileName, true);
        }

        /// <summary>
        /// Return the file stream with header content-length and filename.
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [Route("file/{fileName}/size/{size}/norange")]
        public IActionResult GetFileWithNoAcceptRange(string fileName, int size)
        {
            _logger.Log(LogLevel.Information, $"file/{fileName}/size/{size}/norange");
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", fileName, false);
        }
    }
}
