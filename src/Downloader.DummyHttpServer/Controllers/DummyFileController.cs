using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

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

        [HttpGet]
        public IEnumerable<object> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
            })
            .ToArray();
        }

        [HttpGet]
        [Route("bytes/{size}")]
        public IActionResult GetBytes(int size)
        {
            var data = DummyData.GenerateOrderedBytes(size);
            return Ok(data);
        }

        /// <summary>
        /// Return memory stream of the File
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="ext">Extension of the file name</param>
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [Route("filename/{fileName}/size/{size}")]
        public IActionResult GetFileWithContentDisposition(string fileName, int size)
        {
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", fileName, true);
        }

        /// <summary>
        /// Return memory stream of the File
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="ext">Extension of the file name</param>
        /// <param name="size">Size of the File</param>
        /// <returns></returns>
        [Route("filename/{fileName}")]
        public IActionResult GetFile(string fileName, int size)
        {
            byte[] fileData = DummyData.GenerateOrderedBytes(size);
            return File(fileData, "application/octet-stream", true);
        }
    }
}
