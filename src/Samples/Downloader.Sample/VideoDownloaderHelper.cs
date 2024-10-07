using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Downloader.Sample;

/// <summary>
/// https://publer.io/
/// </summary>
public class VideoDownloaderHelper
{
    private const string CreateJobUrl = "https://app.publer.io/hooks/media";
    private const string JobStatusUrl = "https://app.publer.io/api/v1/job_status/{jobId}";
    private static HttpClient Client;

    class MediaResponse
    {
        [JsonProperty(PropertyName = "job_id")]
        public string JobId { get; set; }
    }

    class JobStatusResponse
    {
        [JsonProperty(PropertyName = "payload")]
        public List<Payload> Payloads { get; set; }

        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }

    class Payload
    {
        [JsonProperty(PropertyName = "error")] public string Error { get; set; }
        [JsonProperty(PropertyName = "path")] public string path { get; set; }
    }

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

        Client.DefaultRequestHeaders.Add("Referer", "https://publer.io/");
        Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 Gecko/20100101 Firefox/131.0");
    }

    /// <summary>
    /// Helper method to initiate the video processing job and wait for completion from https://publer.io/
    /// </summary>
    /// <param name="videoUrl"></param>
    /// <returns></returns>
    /// <exception cref="Exception">Link is invalid</exception>
    /// <exception cref="WebException">The given link video was removed from server</exception>
    public async Task<string> GetCookedUrlAsync(string videoUrl)
    {
        // 1. Initiate the video processing job
        var jobRequestPayload = new { url = videoUrl };
        StringContent requestBody = new(Newtonsoft.Json.JsonConvert.SerializeObject(jobRequestPayload),
            Encoding.UTF8, "application/json");

        HttpResponseMessage createJobResponse = await Client.PostAsync(CreateJobUrl, requestBody);
        createJobResponse.EnsureSuccessStatusCode();

        string resp = await createJobResponse.Content.ReadAsStringAsync();
        MediaResponse media = JsonConvert.DeserializeObject<MediaResponse>(resp);

        if (string.IsNullOrEmpty(media?.JobId))
        {
            throw new Exception("Failed to get job ID.");
        }

        // 2. Check the job status and wait for completion
        string statusUrl = JobStatusUrl.Replace("{jobId}", media.JobId);

        while (true)
        {
            HttpResponseMessage jobStatusResponse = await Client.GetAsync(statusUrl);
            jobStatusResponse.EnsureSuccessStatusCode();

            string jobStatusResponseBody = await jobStatusResponse.Content.ReadAsStringAsync();
            JobStatusResponse jobStatus = JsonConvert.DeserializeObject<JobStatusResponse>(jobStatusResponseBody);

            if (jobStatus?.Status?.Equals("complete", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Extract the download URL from the payload
                Payload data = jobStatus.Payloads?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(data?.Error))
                {
                    throw new WebException(data.Error);
                }

                if (!string.IsNullOrEmpty(data?.path))
                {
                    return data.path;
                }

                throw new Exception("Link is invalid: " + videoUrl);
            }

            // Wait for a few seconds before retrying
            await Task.Delay(3000); // 3 seconds delay
        }
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