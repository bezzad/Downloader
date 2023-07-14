using System;

namespace Downloader.Sample
{
    public static class Helper
    {
        public static string CalcMemoryMensurableUnit(this long bytes)
        {
            return CalcMemoryMensurableUnit((double)bytes);
        }

        public static string CalcMemoryMensurableUnit(this double bytes)
        {
            double kb = bytes / 1024; // · 1024 Bytes = 1 Kilobyte 
            double mb = kb / 1024;    // · 1024 Kilobytes = 1 Megabyte 
            double gb = mb / 1024;    // · 1024 Megabytes = 1 Gigabyte 
            double tb = gb / 1024;    // · 1024 Gigabytes = 1 Terabyte 

            string result =
                tb > 1 ? $"{tb:0.##}TB" :
                gb > 1 ? $"{gb:0.##}GB" :
                mb > 1 ? $"{mb:0.##}MB" :
                kb > 1 ? $"{kb:0.##}KB" :
                $"{bytes:0.##}B";

            result = result.Replace("/", ".");
            return result;
        }

        public static void UpdateTitleInfo(this DownloadProgressChangedEventArgs e, bool isPaused)
        {
            double nonZeroSpeed = e.BytesPerSecondSpeed + 0.0001;
            int estimateTime = (int)((e.TotalBytesToReceive - e.ReceivedBytesSize) / nonZeroSpeed);
            bool isMinutes = estimateTime >= 60;
            string timeLeftUnit = "seconds";

            if (isMinutes)
            {
                timeLeftUnit = "minutes";
                estimateTime /= 60;
            }

            if (estimateTime < 0)
            {
                estimateTime = 0;
                timeLeftUnit = "unknown";
            }

            string avgSpeed = e.AverageBytesPerSecondSpeed.CalcMemoryMensurableUnit();
            string speed = e.BytesPerSecondSpeed.CalcMemoryMensurableUnit();
            string bytesReceived = e.ReceivedBytesSize.CalcMemoryMensurableUnit();
            string totalBytesToReceive = e.TotalBytesToReceive.CalcMemoryMensurableUnit();
            string progressPercentage = $"{e.ProgressPercentage:F3}".Replace("/", ".");
            string usedMemory = GC.GetTotalMemory(false).CalcMemoryMensurableUnit();

            Console.Title = $"{progressPercentage}%  -  " +
                            $"{speed}/s (avg: {avgSpeed}/s)  -  " +
                            $"{estimateTime} {timeLeftUnit} left   -  " +
                            $"Active Chunks: {e.ActiveChunks}   -   " +
                            $"[{bytesReceived} of {totalBytesToReceive}]   " +
                            $"[{usedMemory} memory buffer]   " +
                            (isPaused ? " - Paused" : "");
        }
    }
}
