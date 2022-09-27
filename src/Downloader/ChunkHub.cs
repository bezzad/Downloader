using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

namespace Downloader
{
    internal class ChunkHub : IDisposable
    {
        private readonly DownloadConfiguration _config;
        private readonly string _memoryMappedFileName;
        private StreamProvider _streamProvider;
        private MemoryMappedFile _memoryMappedFile;
        private int _chunkCount = 0;
        private long _chunkSize = 0;
        private long _startOffset = 0;
        public Stream _result;

        public ChunkHub(DownloadConfiguration config)
        {
            _config = config;
            _memoryMappedFileName = Guid.NewGuid().ToString("N");
            _result = null;
        }

        public void SetFileChunks(DownloadPackage package)
        {
            Validate(package);
            CreateStreamProvider(package);
            if (package.Chunks is null)
            {
                package.Chunks = new Chunk[_chunkCount];
                for (int i = 0; i < _chunkCount; i++)
                {
                    long startPosition = _startOffset + (i * _chunkSize);
                    long endPosition = startPosition + _chunkSize - 1;
                    package.Chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
                }
                package.Chunks.Last().End += package.TotalFileSize % _chunkCount; // add remaining bytes to last chunk
            }
            else
            {
                package.Validate();
            }
        }

        private void Validate(DownloadPackage package)
        {
            _chunkCount = _config.ChunkCount;
            _startOffset = _config.RangeLow;

            if (_startOffset < 0)
            {
                _startOffset = 0;
            }

            if (package.TotalFileSize < _chunkCount)
            {
                _chunkCount = (int)package.TotalFileSize;
            }

            if (_chunkCount < 1)
            {
                _chunkCount = 1;
            }

            _chunkSize = package.TotalFileSize / _chunkCount;
        }

        public Stream GetResult()
        {
            if (_result?.CanSeek == true)
            {
                _result.Seek(0, SeekOrigin.Begin);
            }

            return _result;
        }

        private void CreateStreamProvider(DownloadPackage package)
        {
            if (_chunkCount > 1)
            {
                _memoryMappedFile = package.InMemoryStream
                        ? MemoryMappedFile.CreateNew(_memoryMappedFileName, package.TotalFileSize, MemoryMappedFileAccess.ReadWrite)
                        : MemoryMappedFile.CreateFromFile(package.FileName, FileMode.OpenOrCreate, _memoryMappedFileName, package.TotalFileSize, MemoryMappedFileAccess.ReadWrite);

                _streamProvider = _memoryMappedFile.CreateViewStream;
                _result = _memoryMappedFile.CreateViewStream(0, package.TotalFileSize, MemoryMappedFileAccess.Read);
            }
            else
            {
                _streamProvider = package.InMemoryStream
                    ? (offset, size) => _result = new MemoryStream()
                    : (offset, size) => _result = new FileStream(package.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
        }

        private Chunk GetChunk(string id, long start, long end)
        {
            var chunk = new Chunk(start, end) {
                Id = id,
                MaxTryAgainOnFailover = _config.MaxTryAgainOnFailover,
                Timeout = _config.Timeout,
                StorageProvider = _streamProvider
            };

            return chunk;
        }

        public void Dispose()
        {
            _memoryMappedFile?.Dispose();
        }
    }
}