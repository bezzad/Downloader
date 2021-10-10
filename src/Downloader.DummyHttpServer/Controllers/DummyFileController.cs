using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [HttpGet]
        [Route("bytes/{size}")]
        public IActionResult GetBytes(int size)
        {
            var data = DummyData.GenerateOrderedBytes(size);
            return Ok(data);
        }

        /// <summary>
        /// Return the file stream with header content-length. Filename just used in URL.
        /// </summary>
        /// <param name="fileName">The file name</param>        
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [Route("file/{fileName}")]
        public IActionResult GetFile(string fileName, int size)
        {
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", true);
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
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", fileName, true);
        }


    }
}
