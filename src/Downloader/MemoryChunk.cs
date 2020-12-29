namespace Downloader
{
    public class MemoryChunk : Chunk
    {
        public MemoryChunk(long start, long end) :
            base(start, end)
        { }

        public byte[] Data { get; set; }

        public override void Clear()
        {
            base.Clear();
            Data = null;
        }
    }
}