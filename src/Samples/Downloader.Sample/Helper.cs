using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Downloader.Sample;

[ExcludeFromCodeCoverage]
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

    public static string UpdateTitleInfo(this DownloadProgressChangedEventArgs e, bool isPaused)
    {
        int estimateTime = (int)Math.Ceiling((e.TotalBytesToReceive - e.ReceivedBytesSize) / e.AverageBytesPerSecondSpeed);
        string timeLeftUnit = "seconds";

        if (estimateTime >= 60) // isMinutes
        {
            timeLeftUnit = "minutes";
            estimateTime /= 60;
        }
        else if (estimateTime < 0)
        {
            estimateTime = 0;
        }

        string avgSpeed = e.AverageBytesPerSecondSpeed.CalcMemoryMensurableUnit();
        string speed = e.BytesPerSecondSpeed.CalcMemoryMensurableUnit();
        string bytesReceived = e.ReceivedBytesSize.CalcMemoryMensurableUnit();
        string totalBytesToReceive = e.TotalBytesToReceive.CalcMemoryMensurableUnit();
        string progressPercentage = $"{e.ProgressPercentage:F3}".Replace("/", ".");
        string usedMemory = GC.GetTotalMemory(false).CalcMemoryMensurableUnit();

         return $"{estimateTime} {timeLeftUnit} left   -  " +
                $"{speed}/s (avg: {avgSpeed}/s)  -  " +
                $"{progressPercentage}%  -  " +
                $"[{bytesReceived} of {totalBytesToReceive}]   " +
                $"Active Chunks: {e.ActiveChunks}   -   " +
                $"[{usedMemory} memory]   " +
                (isPaused ? " - Paused" : "");
    }

    public static async Task HttpClientDownload(string url, string filename, string proxyAddress)
    {
        // Define the proxy address
        Uri proxyUri = new(proxyAddress);

        // Create a WebProxy instance
        WebProxy proxy = new(proxyUri) {
            // If your proxy requires credentials, set them here
            // Credentials = new NetworkCredential("username", "password")
        };

        // Create an HttpClientHandler and set the proxy
        HttpClientHandler handler = new() {
            Proxy = proxy,
            UseProxy = true
        };

        HttpClient client = new(handler);
        HttpRequestMessage request = new(HttpMethod.Get, url);
        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Stream stream = await response.Content.ReadAsStreamAsync();
        FileStream fileStream = File.OpenWrite(filename);
        await stream.CopyToAsync(fileStream);
    }
}
