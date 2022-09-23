using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    internal class ChunkHub
    {
        private readonly DownloadConfiguration _configuration;
        public event EventHandler<ChunkMergeProgressChangedEventArgs> ChunkMergeProgressChanged;

        public ChunkHub(DownloadConfiguration config)
        {
            _configuration = config;
        }

        public Chunk[] ChunkFile(long fileSize, long parts, long start = 0)
        {
            if (start < 0)
            {
                start = 0;
            }

            if (fileSize < parts)
            {
                parts = fileSize;
            }

            if (parts < 1)
            {
                parts = 1;
            }

            long chunkSize = fileSize / parts;
            Chunk[] chunks = new Chunk[parts];
            for (int i = 0; i < parts; i++)
            {
                long startPosition = start + (i * chunkSize);
                long endPosition = startPosition + chunkSize - 1;
                chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
            }
            chunks.Last().End += fileSize % parts; // add remaining bytes to last chunk

            return chunks;
        }

        private Chunk GetChunk(string id, long start, long end)
        {
            var chunk = new Chunk(start, end) {
                Id = id,
                MaxTryAgainOnFailover = _configuration.MaxTryAgainOnFailover,
                Timeout = _configuration.Timeout
            };
            return GetStorableChunk(chunk);
        }

        private Chunk GetStorableChunk(Chunk chunk)
        {
            if (_configuration.OnTheFlyDownload)
            {
                chunk.Storage = new MemoryStorage();
            }
            else
            {
                chunk.Storage = new FileStorage(_configuration.TempDirectory, _configuration.TempFilesExtension);
            }

            return chunk;
        }
        
        private void OnChunkMergeProgressChanged(ChunkMergeProgressChangedEventArgs e)
        {
            ChunkMergeProgressChanged?.Invoke(this, e);
        }

        public async Task MergeChunks(IEnumerable<Chunk> chunks, Stream destinationStream, CancellationToken cancellationToken)
        {
            var chunkList = chunks.ToList(); //Convert to list to prevent multiple enumeration
            long totalWrittenLength = 0; //Store number of total written bytes
            long totalLength = chunkList.Sum(x => x.Storage.GetLength()); //Calculate total length for progress notifications
            var bandwidth = new Bandwidth(); //Make a new bandwidth object to calculate copy speeds 
            
            foreach (Chunk chunk in chunkList.OrderBy(c => c.Start))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Stream reader = chunk.Storage.OpenRead();

                //Implementation of CopyToAsync with progress notifications
                var buffer = new byte[_configuration.BufferBlockSize];

                long chunkWrittenLength = 0;
                int count;
                while ((count = await reader.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    totalWrittenLength += count;
                    chunkWrittenLength += count;
                    await destinationStream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                    
                    bandwidth.CalculateSpeed(count);
                    OnChunkMergeProgressChanged(new ChunkMergeProgressChangedEventArgs(chunk.Id) {
                        TotalBytesToCopy = totalLength,
                        TotalCopiedBytesSize = totalWrittenLength,
                        ChunkSize = chunk.Length,
                        ChunkCopiedBytesSize = chunkWrittenLength,
                        ProgressedByteSize = count,
                        BytesPerSecondSpeed = bandwidth.Speed,
                        AverageBytesPerSecondSpeed = bandwidth.AverageSpeed,
                    });
                }
            }
        }
    }
}