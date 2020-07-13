[![Codacy Badge](https://api.codacy.com/project/badge/Grade/5671a06c62514c44a58aea24bf7865d9)](https://app.codacy.com/manual/behzad.khosravifar/Downloader?utm_source=github.com&utm_medium=referral&utm_content=bezzad/Downloader&utm_campaign=Badge_Grade_Settings)
[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/downloader?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/downloader) 
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader) 
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)

# Downloader

:rocket: Fast and reliable multipart downloader with **.Net Core 3.1+** supprting :rocket:

Downloader is a modern, fluent, asynchronous, testable and portable library for .NET. This is a multipart downloader with asynchronous progress events.
This library written in `.Net Standard 2` and you can add that in your `.Net Core` or `.Net Framework` projects.

### Sample Console Application
![sample-project](https://github.com/bezzad/Downloader/raw/master/sample.png)

### How to use

Get it on [NuGet](https://www.nuget.org/packages/Downloader):

    PM> Install-Package Downloader

Or via the .NET Core command line interface:

    dotnet add package Downloader

Create your custom configuration:
```csharp
var downloadOpt = new DownloadConfiguration()
{
    MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
    ParallelDownload = true, // download parts of file as parallel or notm default value is false
    ChunkCount = 8, // file parts to download, default value is 1
    Timeout = 1000, // timeout (millisecond) per stream block reader, default valuse is 1000
    OnTheFlyDownload = false, // caching in-memory or not? default valuse is true
    BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes, default valuse is 8000
    MaximumBytesPerSecond = 1024 * 1024, // download speed limited to 1MB/s, default valuse is zero or unlimited
    RequestConfiguration = // config and customize request headers
    {
        Accept = "*/*",
        UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}",
        ProtocolVersion = HttpVersion.Version11,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        KeepAlive = false,
        UseDefaultCredentials = false
    }
};
```

So, declare download service instance per download and pass config:
```csharp
var downloader = new DownloadService(downloadOpt);
```

Then handle download progress and completed events:
```csharp
downloader.DownloadProgressChanged += OnDownloadProgressChanged;
downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;
downloader.DownloadFileCompleted += OnDownloadFileCompleted;    
```

Finally, start the download asynchronously. For example, download a .zip file:
```csharp
var file = @"Your_Path\fileName.zip";
var url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileAsync(url, file);
```

For resume from last download, store `downloader.Package` object and execute like this: (For more detail see [StopResumeOnTheFlyDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/DownloadTest.cs#L88) test method)
```csharp
var pack = downloader.Package;
download.CancelAsync(); // Stopping after some second from the start of downloading.
await downloader.DownloadFileAsync(pack); // Resume download from stopped point.
```

> Note: for complete sample see `Downloader.Sample` project from this repository.


### Features at a glance

- Download files async and non-blocking.
- Cross-platform library to download any files with any size.
- Get real-time progress info of each block.
- Download file multipart as parallel.
- Handle any client-side or server-side exception none-stopping the downloads.
- Config your `ChunkCount` to define the parts count of the download file.
- Download file multipart as `in-memory` or `in-temp files` cache mode.
- Store download package object to resume the download when you want.
- Get download speed or progress percentage in each progress event.
- Get download progress events per chunk downloads
- Stop and Resume your downloads with package object
- Set a speed limit on downloads
