using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IStorage
    {
        Stream Read();
        Task Write(byte[] data, int offset, int count);
        void Clear();
        long GetLength();
    }
}
