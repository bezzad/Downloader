using System.Linq;

namespace Downloader
{
    internal class ChunkHub
    {
        private readonly DownloadConfiguration _config;
        private int _chunkCount = 0;
        private long _chunkSize = 0;
        private long _startOffset = 0;

        public ChunkHub(DownloadConfiguration config)
        {
            _config = config;
        }

        public void SetFileChunks(DownloadPackage package)
        {
            Validate(package);
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

        private Chunk GetChunk(string id, long start, long end)
        {
            var chunk = new Chunk(start, end) {
                Id = id,
                MaxTryAgainOnFailover = _config.MaxTryAgainOnFailover,
                Timeout = _config.Timeout
            };

            return chunk;
        }
    }
}