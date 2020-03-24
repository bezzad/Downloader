using System;
using System.ComponentModel;

namespace Downloader.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("DownloadService started...");

            var ds = new DownloadService();
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            ds.DownloadFileAsync("https://download.taaghche.com/download/DBXP126H5eLD7avDHjMQp02IVVpnPnTO", "D:\\test.pdf");
            Console.WriteLine("Downloading...");
            Console.ReadKey();
        }

        private static void OnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
        {
        }

        private static void OnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine(e.ProgressPercentage);
        }
    }
}
