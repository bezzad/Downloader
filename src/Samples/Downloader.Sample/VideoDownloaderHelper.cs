using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Downloader.Sample;

public class VideoDownloaderHelper
{
    private static HttpClient Client;

    public VideoDownloaderHelper(IWebProxy proxy = null)
    {
        if (Client is not null)
            return;

        if (proxy is null)
        {
            Client = new HttpClient();
        }
        else
        {
            // Now create a client handler which uses that proxy
            HttpClientHandler httpClientHandler = new() { Proxy = proxy };
            Client = new HttpClient(httpClientHandler, disposeHandler: true);
        }

        Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 Gecko/20100101 Firefox/131.0");
    }


    public async Task DownloadM3U8File(string m3U8Url, string outputFilePath)
    {
        // Step 1: Download the .m3u8 file
        string m3U8Content = await Client.GetStringAsync(m3U8Url);

        // Step 2: Parse the .m3u8 file and extract the media segment URLs
        string[] segmentUrls = ParseM3U8(m3U8Content, m3U8Url);

        // Step 3: Download and combine the segments into a single file
        await DownloadAndCombineSegments(segmentUrls, outputFilePath);
    }

    // Helper method to parse the .m3u8 file
    static string[] ParseM3U8(string m3U8Content, string baseUrl)
    {
        string[] lines = m3U8Content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        List<string> segmentUrls = new();

        foreach (string line in lines)
        {
            if (!line.StartsWith("#") && !string.IsNullOrWhiteSpace(line))
            {
                // If the line is not a comment, treat it as a media segment URL
                string segmentUrl = line;

                // Handle relative URLs
                if (!Uri.IsWellFormedUriString(segmentUrl, UriKind.Absolute))
                {
                    Uri baseUri = new(baseUrl);
                    Uri segmentUri = new(baseUri, segmentUrl);
                    segmentUrl = segmentUri.ToString();
                }

                segmentUrls.Add(segmentUrl);
            }
        }

        return segmentUrls.ToArray();
    }

    // Helper method to download and combine segments
    static async Task DownloadAndCombineSegments(string[] segmentUrls, string outputFilePath)
    {
        await using FileStream output = new(outputFilePath, FileMode.Create);
        for (int i = 0; i < segmentUrls.Length; i++)
        {
            string segmentUrl = segmentUrls[i];
            Console.WriteLine($"Downloading segment {i + 1} of {segmentUrls.Length}");

            byte[] segmentData = await Client.GetByteArrayAsync(segmentUrl);
            await output.WriteAsync(segmentData, 0, segmentData.Length);
        }
    }
}