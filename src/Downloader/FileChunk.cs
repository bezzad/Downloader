using System.IO;

namespace Downloader
{
    public class FileChunk : Chunk
    {
        public FileChunk(long start, long end) :
            base(start, end)
        { }

        public string FileName { get; set; }

        public override void Clear()
        {
            base.Clear();
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }
        }
    }
}