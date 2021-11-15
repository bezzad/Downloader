using System;
using System.Linq;

namespace Downloader
{
    [Serializable]
    public class DownloadPackage
    {
        public bool IsSaving { get; set; }
        public bool IsSaveComplete { get; set; }
        public double SaveProgress { get; set; }
        public string Address { get; set; }
        public long TotalFileSize { get; set; }
        public string FileName { get; set; }
        public Chunk[] Chunks { get; set; }
        public long ReceivedBytesSize => Chunks?.Sum(chunk => chunk.Position) ?? 0;

        public void Clear()
        {
            if (Chunks != null)
            {
                foreach (Chunk chunk in Chunks)
                {
                    chunk.Clear();
                }
            }
            Chunks = null;
        }

        public void Flush()
        {
            if (Chunks != null)
            {
                foreach (Chunk chunk in Chunks)
                {
                    chunk?.Flush();
                }
            }
        }

        public void Validate()
        {
            foreach (var chunk in Chunks)
            {
                if (chunk.IsValidPosition() == false)
                {
                    var realLength = chunk.Storage?.GetLength() ?? 0;
                    if (realLength - chunk.Position <= 0)
                    {
                        chunk.Clear();
                    }
                    chunk.SetValidPosition();
                }
            }
        }
    }
}