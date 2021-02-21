using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Downloader
{
    [Serializable]
    public class FileStorage : IStorage, IDisposable, ISerializable
    {
        [NonSerialized] private FileStream _stream;
        [NonSerialized] private string _fileName;
        public string FileName
        {
            get => _fileName ??= FileHelper.GetTempFile();
            set => _fileName = value;
        }

        public FileStorage() { }

        public FileStorage(string fileName)
        {
            if (File.Exists(fileName) == false)
            {
                var directory = Path.GetDirectoryName(fileName);
                var extension = Path.GetExtension(fileName);
                FileName= FileHelper.GetTempFile(directory, extension);
            }
            else
            {
                FileName = fileName;
            }
        }

        public FileStorage(string directory, string fileExtension = "")
        {
            FileName = FileHelper.GetTempFile(directory, fileExtension);
        }

        /// <summary>
        ///     The special constructor is used to deserialize values.
        /// </summary>
        public FileStorage(SerializationInfo info, StreamingContext context)
        {
            // Reset the property value using the GetValue method.
            FileName = (string)info.GetValue(nameof(FileName), typeof(string));
        }


        public Stream OpenRead()
        {
            if (_stream?.CanWrite == true)
            {
                _stream.Flush();
                _stream.Dispose();
            }
            return File.Open(FileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite);
        }

        public async Task WriteAsync(byte[] data, int offset, int count)
        {
            if (_stream?.CanWrite != true)
            {
                _stream = new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite);
            }
            await _stream.WriteAsync(data, offset, count);
        }

        public void Clear()
        {
            Close();
            if (File.Exists(FileName))
            {
                File.Delete(FileName);
            }
        }

        public void Close()
        {
            _stream?.Dispose();
        }

        public long GetLength()
        {
            return OpenRead()?.Length ?? 0;
        }
        
        public void Dispose()
        {
            Clear();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(FileName), FileName, typeof(string));
        }
    }
}