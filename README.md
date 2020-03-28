[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/downloader?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/downloader) 
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader) 
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)

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
    ParallelDownload = true, // download parts of file as parallel or not
    BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes
    ChunkCount = 8, // file parts to download
    MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
    OnTheFlyDownload = true, // caching in-memory mode
    Timeout = 1000 // timeout (millisecond) per stream block reader
};
```

So, declare download service instance per download and pass config:
```csharp
var downloader = new DownloadService(downloadOpt);
```

Then handle download progress and completed events:
```csharp
downloader.DownloadProgressChanged += OnDownloadProgressChanged;
downloader.DownloadFileCompleted += OnDownloadFileCompleted;    
```

Finally, start the download asynchronously. For example, download a .zip file:
```csharp
var file = "file_fullname.zip";
downloader.DownloadFileAsync("https://file-examples.com/file_fullname.zip", file); 

// or

await downloader.DownloadFileTaskAsync("https://file-examples.com/file_fullname.", file);
```

For resume from last download, store `downloader.Package` object and execute like this:
```csharp
downloader.DownloadFileAsync(package);

// or

await downloader.DownloadFileTaskAsync(package);
```

> Note: for complete sample see `Downloader.Sample` project from this repository.


### Features at a glance

- Download files async and non-blocking.
- Cross-platform library to download any files with any size.
- Get real-time progress info of each block.
- Download file multipart as parallel.
- Handle any client-side or server-side exception none-stopping the downloads.
- Config your `ChunkCount` to define the parts count of the download file.
- Download file multipart as in-memory mode or cache in temp.
- Store download package object to resume the download when you want.
- Get download speed or progress percentage in each progress event.