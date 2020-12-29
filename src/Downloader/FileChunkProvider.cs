using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileChunkProvider : ChunkProvider
    {
        public FileChunkProvider(DownloadConfiguration config) : base(config)
        { }

        protected override Chunk Factory(long startPosition, long endPosition)
        {
            return new FileChunk(startPosition, endPosition);
        }

        public override async Task MergeChunks(Chunk[] chunks, string fileName)
        {
            using Stream destinationStream = CreateFile(fileName);
            foreach (Chunk chunk in chunks.OrderBy(c => c.Start))
            {
                if (chunk is FileChunk fileChunk)
                {
                    using FileStream reader = File.OpenRead(fileChunk.FileName);
                    await reader.CopyToAsync(destinationStream);
                }
            }
        }

        public override ChunkDownloader GetChunkDownloader(Chunk chunk)
        {
            return new FileChunkDownloader((FileChunk)chunk, Configuration.BufferBlockSize, Configuration.TempDirectory,
                Configuration.TempFilesExtension);
        }
    }
}