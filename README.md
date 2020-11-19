[![Downloader Build and Test](https://github.com/bezzad/Downloader/workflows/Downloader%20Build%20and%20Test/badge.svg)](https://github.com/bezzad/Downloader/actions?query=workflow%3A%22Downloader+Build+and+Test%22)
[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/downloader?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/downloader) 
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader) 
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)

# Downloader

:rocket: Fast and reliable multipart downloader with **.Net Core 3.1+** supporting :rocket:

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
    AllowedHeadRequest = false, // Can fetch file size by HEAD request or must be used GET method to support host
    MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
    ParallelDownload = true, // download parts of file as parallel or notm default value is false
    ChunkCount = 8, // file parts to download, default value is 1
    Timeout = 1000, // timeout (millisecond) per stream block reader, default values is 1000
    OnTheFlyDownload = false, // caching in-memory or not? default values is true
    BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes, default values is 8000
    MaximumBytesPerSecond = 1024 * 1024, // download speed limited to 1MB/s, default values is zero or unlimited
    TempDirectory = "C:\\temp", // Set the temp path for buffering chunk files, the default path is Path.GetTempPath().
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

The ‍DownloadService class has a property called `Package` that stores each step of the download. You must call the `CancelAsync` method to stop or pause the download, and if you continue again, you must call the same `DownloadFileAsync` function with the Package parameter to continue your download! 
For example:

__Start the download asynchronously and keep package file:__
```csharp
var file = @"Your_Path\fileName.zip";
var url = @"https://file-examples.com/fileName.zip";
// To resume from last download, keep downloader.Package object
var pack = downloader.Package; 
await downloader.DownloadFileAsync(url, file);
```

__Stop or Pause Download:__
```csharp
downloader.CancelAsync(); 
```

__Resume Download:__
```csharp
await downloader.DownloadFileAsync(pack); 
```

So that you can even save your large downloads with a very small amount in the Package and after restarting the program, restore it again and start continuing your download. In fact, the packages are your instant download snapshots. If your download config has OnTheFlyDownload, the downloaded bytes ​​will be stored in the package itself, but otherwise, only the address of the downloaded files will be included and you can resume it whenever you like. 
For more detail see [StopResumeOnTheFlyDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/DownloadTest.cs#L88) method


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
