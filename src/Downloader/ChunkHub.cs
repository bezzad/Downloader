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

        public Chunk[] ChunkFile(long fileSize, string filename)
        {
            Validate(fileSize);
            CreateStreamProvider(fileSize, filename);
            Chunk[] chunks = new Chunk[_chunkCount];
            for (int i = 0; i < _chunkCount; i++)
            {
                long startPosition = _startOffset + (i * _chunkSize);
                long endPosition = startPosition + _chunkSize - 1;
                chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
            }
            chunks.Last().End += fileSize % _chunkCount; // add remaining bytes to last chunk

            return chunks;
        }

        private void Validate(long fileSize)
        {
            _chunkCount = _config.ChunkCount;
            _startOffset = _config.RangeLow;

            if (_startOffset < 0)
            {
                _startOffset = 0;
            }

            if (fileSize < _chunkCount)
            {
                _chunkCount = (int)fileSize;
            }

            if (_chunkCount < 1)
            {
                _chunkCount = 1;
            }

            _chunkSize = fileSize / _chunkCount;
        }

        public Stream GetResult()
        {
            if (_result?.CanSeek == true)
            {
                _result.Seek(0, SeekOrigin.Begin);
            }

            return _result;
        }

        private void CreateStreamProvider(long size, string filename)
        {
            if (_chunkCount > 1)
            {
                _memoryMappedFile = string.IsNullOrEmpty(filename)
                        ? MemoryMappedFile.CreateNew(_memoryMappedFileName, size, MemoryMappedFileAccess.ReadWrite)
                        : MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, _memoryMappedFileName, size, MemoryMappedFileAccess.ReadWrite);

                _streamProvider = _memoryMappedFile.CreateViewStream;
                _result = _memoryMappedFile.CreateViewStream(0, size, MemoryMappedFileAccess.Read);
            }
            else
            {
                _streamProvider = string.IsNullOrEmpty(filename)
                    ? (offset, size) => _result = new MemoryStream()
                    : (offset, size) => _result = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
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