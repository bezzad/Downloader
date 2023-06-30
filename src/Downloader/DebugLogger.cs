using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    internal class DebugLogger : IDisposable
    {
        private StreamWriter _log;
        private bool _disposed;
        private string _path;
        private string _logFileExt = ".log";
        private string _logDefaultFolder = "downloader";
        private string _logLogFolder = "logs";

        public DebugLogger(string path = null)
        {
#if DEBUG
            SafeCreateFile(path);
            _log = File.AppendText(_path);
            _log.AutoFlush = true;
#endif
        }

        private void SafeCreateFile(string path)
        {
            _path = path;
            if (string.IsNullOrWhiteSpace(_path))
            {
                var temp = Path.GetTempPath();
                var filename = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-ffff");
                _path = Path.Combine(temp, _logDefaultFolder, _logLogFolder, filename + _logFileExt);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.Create(_path).Dispose();
        }

        public void WriteLine(string message)
        {
#if DEBUG
            if (Debugger.IsLogging())
                _log.WriteLine(message);
#endif
        }

        public Task WriteLineAsync(string message)
        {
#if DEBUG
            if (Debugger.IsLogging())
                return _log.WriteLineAsync(message);
#endif
            return Task.FromResult(0);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _log?.Dispose();
        }
    }
}
