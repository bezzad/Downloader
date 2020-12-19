using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader
{
    public class FileChunkProvider : ChunkProvider
    {
        public FileChunkProvider(DownloadConfiguration config) : base(config)
        { }

        public override Chunk Factory(long startPosition, long endPosition)
        {
            return new FileChunk(startPosition, endPosition);
        }

        public override async Task MergeChunks(Chunk[] chunks, string targetFileName)
        {
            using var destinationStream = CreateFile(targetFileName);
            foreach (var chunk in chunks.OrderBy(c => c.Start))
            {
                if (chunk is FileChunk fileChunk)
                {
                    using var reader = File.OpenRead(fileChunk.FileName);
                    await reader.CopyToAsync(destinationStream);
                }
            }
        }
    }
}
