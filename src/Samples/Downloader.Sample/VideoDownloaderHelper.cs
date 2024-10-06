using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
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
        Client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:131.0) Gecko/20100101 Firefox/131.0");
    }

    // Helper method to initiate the video processing job and wait for completion
    public async Task<string> GetUrlAsync(string videoUrl)
    {
        // 1. Initiate the video processing job
        var jobRequestPayload = new { url = videoUrl };
        StringContent requestBody = new(Newtonsoft.Json.JsonConvert.SerializeObject(jobRequestPayload),
            Encoding.UTF8, "application/json");

        HttpResponseMessage createJobResponse = await Client.PostAsync(CreateJobUrl, requestBody);
        createJobResponse.EnsureSuccessStatusCode();

        string resp = await createJobResponse.Content.ReadAsStringAsync();
        var media = JsonConvert.DeserializeObject<MediaResponse>(resp);

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
            var jobStatus = JsonConvert.DeserializeObject<JobStatusResponse>(jobStatusResponseBody);

            if (jobStatus?.Status?.Equals("complete", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Extract the download URL from the payload
                var data = jobStatus.Payloads?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(data?.Error))
                {
                    throw new WebException(data.Error);
                }

                if (!string.IsNullOrEmpty(data?.path))
                {
                    return data.path;
                }

                throw new WebException("Link is invalid: " + videoUrl);
            }

            // Wait for a few seconds before retrying
            await Task.Delay(3000); // 3 seconds delay
        }
    }
}