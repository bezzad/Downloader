[![Windows x64](https://github.com/bezzad/Downloader/workflows/Windows%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-windows.yml)
[![Ubuntu x64](https://github.com/bezzad/Downloader/workflows/Ubuntu%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-ubuntu.yml)
[![codecov](https://codecov.io/gh/bezzad/downloader/branch/master/graph/badge.svg)](https://codecov.io/gh/bezzad/downloader)
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader)
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/f7cd6e24f75c45c28e5e6fab2ef8d219)](https://www.codacy.com/gh/bezzad/Downloader/dashboard?utm_source=github.com&utm_medium=referral&utm_content=bezzad/Downloader&utm_campaign=Badge_Grade)
[![License](https://img.shields.io/github/license/bezzad/downloader.svg)](https://github.com/bezzad/downloader/blob/master/LICENSE)
[![Generic badge](https://img.shields.io/badge/support-.Net_Framework-blue.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_Core-blue.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_Standard-blue.svg)](https://github.com/bezzad/Downloader)

# Downloader

:rocket: Fast, cross-platform and reliable multipart downloader with **.Net Core** supporting :rocket:

Downloader is a modern, fluent, asynchronous, testable and portable library for .NET. This is a multipart downloader with asynchronous progress events.
This library can be added to your `.Net Core v2` and later or `.Net Framework v4.5` or later projects.

Downloader is compatible with .NET Standard 2.0 and above, running on Windows, Linux, and macOS, in full .NET Framework or .NET Core. 

> For a complete example see [Downloader.Sample](https://github.com/bezzad/Downloader/blob/master/src/Samples/Downloader.Sample/Program.cs) project from this repository.

## Sample Console Application

![sample-project](https://github.com/bezzad/Downloader/raw/master/sample.gif)

# Features at a glance

- Simple interface to make download requests.
- Download files async and non-blocking.
- Download any file like image, video, pdf, apk, etc.
- Cross-platform library to download any files of any size.
- Get real-time progress info on each block.
- Download file multipart as parallel.
- Handle all the client-side and server-side exceptions non-stopping.
- Config your `ChunkCount` to define the parts count of the download file.
- Download file multipart as `in-memory` or `on-disk` mode.
- Chunks are saved in parallel on the final file, not the temp files.
- The file size is pre-allocated before the download starts.
- Store the download package object to resume the download when you want.
- Get download speed or progress percentage in each progress event.
- Get download progress events per chunk downloads.
- Fast Pause and Resume download asynchronously.
- Stop and Resume downloads whenever you want with the package object.
- Supports large file download.
- Set a dynamic speed limit on downloads (changeable speed limitation on the go).
- Download files without storing them on disk and get a memory stream for each downloaded file.
- Serializable download package (to/from `JSON` or `Binary`)
- Live streaming support, suitable for playing music at the same time as downloading.
- Ability to download just a certain range of bytes of a large file.
- Code is tiny, fast and does not depend on external libraries.
- Control the amount of system memory (RAM) the Downloader consumes during downloading.

---

## Installing via [NuGet](https://www.nuget.org/packages/Downloader)

    PM> Install-Package Downloader

## Installing via the .NET Core command line interface

    dotnet add package Downloader

# How to use

## **Step 1**: Create your custom configuration

### Simple Configuration

```csharp
var downloadOpt = new DownloadConfiguration()
{
    ChunkCount = 8, // file parts to download, the default value is 1
    ParallelDownload = true // download parts of the file as parallel or not. The default value is false
};
```

### Complex Configuration


> **Note**: *Do not use all of the below options in your applications, just add which one you need.*

```csharp
var downloadOpt = new DownloadConfiguration()
{
    // usually, hosts support max to 8000 bytes, default value is 8000
    BufferBlockSize = 10240,
    // file parts to download, the default value is 1
    ChunkCount = 8,             
    // download speed limited to 2MB/s, default values is zero or unlimited
    MaximumBytesPerSecond = 1024*1024*2, 
    // the maximum number of times to fail
    MaxTryAgainOnFailover = 5,    
    // release memory buffer after each 50 MB
    MaximumMemoryBufferBytes = 1024 * 1024 * 50, 
    // download parts of the file as parallel or not. The default value is false
    ParallelDownload = true,
    // number of parallel downloads. The default value is the same as the chunk count
    ParallelCount = 4,    
    // timeout (millisecond) per stream block reader, default values is 1000
    Timeout = 1000,      
    // set true if you want to download just a specific range of bytes of a large file
    RangeDownload = false,
    // floor offset of download range of a large file
    RangeLow = 0,
    // ceiling offset of download range of a large file
    RangeHigh = 0, 
    // clear package chunks data when download completed with failure, default value is false
    ClearPackageOnCompletionWithFailure = true, 
    // minimum size of chunking to download a file in multiple parts, the default value is 512
    MinimumSizeOfChunking = 1024, 
    // Before starting the download, reserve the storage space of the file as file size, the default value is false
    ReserveStorageSpaceBeforeStartingDownload = true; 
    // config and customize request headers
    RequestConfiguration = 
    {        
        Accept = "*/*",
        CookieContainer = cookies,
        Headers = new WebHeaderCollection(), // { your custom headers }
        KeepAlive = true, // default value is false
        ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
        UseDefaultCredentials = false,
        // your custom user agent or your_app_name/app_version.
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        Proxy = new WebProxy() {
           Address = new Uri("http://YourProxyServer/proxy.pac"),
           UseDefaultCredentials = false,
           Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
           BypassProxyOnLocal = true
        }
    }
};
```

## **Step 2**: Create a download service instance per download and pass your config

```csharp
var downloader = new DownloadService(downloadOpt);
```

## **Step 3**: Handle download events

```csharp
// Provide `FileName` and `TotalBytesToReceive` at the start of each download
downloader.DownloadStarted += OnDownloadStarted;

// Provide any information about chunker downloads, 
// like progress percentage per chunk, speed, 
// total received bytes and received bytes array to live streaming.
downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;

// Provide any information about download progress, 
// like progress percentage of sum of chunks, total speed, 
// average speed, total received bytes and received bytes array 
// to live streaming.
downloader.DownloadProgressChanged += OnDownloadProgressChanged;

// Download completed event that can include errors or 
// canceled or download completed successfully.
downloader.DownloadFileCompleted += OnDownloadFileCompleted;
```

## **Step 4**: Start the download with the URL and file name

```csharp
string file = @"Your_Path\fileName.zip";
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileTaskAsync(url, file);
```

## **Step 4b**: Start the download without file name

```csharp
DirectoryInfo path = new DirectoryInfo("Your_Path");
string url = @"https://file-examples.com/fileName.zip";
// download into "Your_Path\fileName.zip"
await downloader.DownloadFileTaskAsync(url, path); 
```

## **Step 4c**: Download in MemoryStream

```csharp
// After download completion, it gets a MemoryStream
Stream destinationStream = await downloader.DownloadFileTaskAsync(url); 
```

---
## How to **pause** and **resume** downloads quickly

When you want to resume a download quickly after pausing a few seconds. You can call the `Pause` function of the downloader service. This way, streams stay alive and are only suspended by a locker to be released and resumed whenever you want.

```csharp
// Pause the download
DownloadService.Pause();

// Resume the download
DownloadService.Resume();
```

---
## How to **stop** and **resume** downloads other time

The ‍`DownloadService` class has a property called `Package` that stores each step of the download. To stop the download you must call the `CancelAsync` method. Now, if you want to continue again, you must call the same `DownloadFileTaskAsync` function with the `Package` parameter to resume your download. For example:

```csharp
// At first, keep and store the Package file to resume 
// your download from the last download position:
DownloadPackage pack = downloader.Package;
```

**Stop or cancel download:**

```csharp
// This function breaks your stream and cancels progress.
downloader.CancelAsync();
```

**Resuming download after cancelation:**

```csharp
await downloader.DownloadFileTaskAsync(pack);
```

So that you can even save your large downloads with a very small amount in the Package and after restarting the program, restore it and start continuing your download. 
The packages are your snapshot of the download instance. Only the downloaded file addresses will be included in the package and you can resume it whenever you want. 
For more detail see [StopResumeDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/IntegrationTests/DownloadIntegrationTest.cs#L115) method

> Note: Sometimes a server does not support downloading in a specific range. That time, we can't resume downloads after canceling. So, the downloader starts from the beginning.

---

## Fluent download builder usage

For easy and fluent use of the downloader, you can use the `DownloadBuilder` class. Consider the following examples:

Simple usage:

```csharp
await DownloadBuilder.New()
    .WithUrl(@"https://host.com/test-file.zip")
    .WithDirectory(@"C:\temp")
    .Build()
    .StartAsync();
```

Complex usage:

```csharp
IDownload download = DownloadBuilder.New()
    .WithUrl(@"https://host.com/test-file.zip")
    .WithDirectory(@"C:\temp")
    .WithFileName("test-file.zip")
    .WithConfiguration(new DownloadConfiguration())
    .Build();

download.DownloadProgressChanged += DownloadProgressChanged;
download.DownloadFileCompleted += DownloadFileCompleted;
download.DownloadStarted += DownloadStarted;
download.ChunkDownloadProgressChanged += ChunkDownloadProgressChanged;

await download.StartAsync();

download.Stop(); // cancel current download
```

Resume the existing download package:

```csharp
await DownloadBuilder.Build(package).StartAsync();
```

Resume the existing download package with a new configuration:

```csharp
await DownloadBuilder.Build(package, config).StartAsync();
```

[Pause and Resume quickly](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/UnitTests/DownloadBuilderTest.cs#L110):

```csharp
var download = DownloadBuilder.New()
     .Build()
     .WithUrl(url)
     .WithFileLocation(path);
await download.StartAsync();

download.Pause(); // pause current download quickly

download.Resume(); // continue current download quickly
```

---

## When does the Downloader fail to download in multiple chunks?

### Content-Length:
If your URL server does not provide the file size in the response header (`Content-Length`). 
The Downloader cannot split the file into multiple parts and continues its work with one chunk.

### Accept-Ranges:
If the server returns `Accept-Ranges: none` in the responses header then that means the server does not support download in range and 
the Downloader cannot use multiple chunking and continues its work with one chunk.

### Content-Range:
At first, the Downloader sends a GET request to the server to fetch the file's size in the range. 
If the server does not provide `Content-Range` in the header then that means the server does not support download in range. 
Therefore, the Downloader has to continue its work with one chunk.

---

## How to serialize and deserialize the downloader package

### **What is Serialization?**

Serialization is the process of converting an object's state into information that can be stored for later retrieval or that can be sent to another system. For example, you may have an object that represents a document that you wish to save. This object could be serialized to a stream of binary information and stored as a file on disk. Later the binary data can be retrieved from the file and deserialized into objects that are exact copies of the original information. As a second example, you may have an object containing the details of a transaction that you wish to send to another type of system. This information could be serialized to XML before being transmitted. The receiving system would convert the XML into a format that it could understand.

In this section, we want to show how to serialize download packages to `JSON` text or `Binary`, after stopping the download to keep downloading data and resuming that every time you want.
You can serialize packages even using memory storage for caching download data which is used `MemoryStream`.

### **JSON Serialization**

Serializing the package to [`JSON`](https://www.newtonsoft.com) is very simple like this:

```csharp
var packageJson = JsonConvert.SerializeObject(package);
```

Deserializing into the new package:

```csharp
var newPack = JsonConvert.DeserializeObject<DownloadPackage>(packageJson);
```

For more detail see [PackageSerializationTest](https://github.com/bezzad/Downloader/blob/46167082b8de99d8e6ad21329c3a32a6e26cfd3e/src/Downloader.Test/DownloadPackageTest.cs#L34) method

### **Binary Serialization**


To serialize or deserialize the package into a binary file, first, you need to serialize it to JSON and next save it with [BinaryWriter](https://learn.microsoft.com/en-us/dotnet/api/system.io.binarywriter).

> **NOTE**: 
The [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) type is dangerous and is not recommended for data processing. 
Applications should stop using [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) as soon as possible, even if they believe the data they're processing to be trustworthy. 
[BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) is insecure and can't be made secure. 
So, [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) is deprecated and we can no longer support it. 
[Reference](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide)

# Instructions for Contributing

Welcome to contribute, feel free to change and open a [**PullRequest**](http://help.github.com/pull-requests/) to develop the branch.
You can use either the latest version of Visual Studio or Visual Studio Code and .NET CLI for Windows, Mac and Linux.

For GitHub workflow, check out our Git workflow below this paragraph. We are following the excellent GitHub Flow process, and would like to make sure you have all of the information needed to be a world-class contributor!

## Git Workflow

The general process for working with Downloader is:

1. [Fork](http://help.github.com/forking/) on GitHub
1. Make sure your line endings are correctly configured and fix your line endings!
1. Clone your fork locally
1. Configure the upstream repo (`git remote add upstream git://github.com/bezzad/downloader`)
1. Switch to the latest development branch (eg vX.Y.Z, using `git checkout vX.Y.Z`)
1. Create a local branch from that (`git checkout -b myBranch`).
1. Work on your feature
1. Rebase if required
1. Push the branch up to GitHub (`git push origin myBranch`)
1. Send a Pull Request on GitHub - the PR should target (have as a base branch) the latest development branch (eg `vX.Y.Z`) rather than `master`.

We accept pull requests from the community. But, you should **never** work on a clone of the master, and you should **never** send a pull request from the master - always from a branch. Please be sure to branch from the head of the latest vX.Y.Z `develop` branch (rather than `master`) when developing contributions.

## You can run tests with the Docker Compose file with the following command:
> `docker-compose -p downloader up`

## Or with docker file:
> `docker build -f ./dockerfile -t downloader-linux .`
> `docker run --name downloader-linux-container -d downloader-linux --env=ASPNETCORE_ENVIRONMENT=Development .`

## Or run the following command to call docker directly:
> `docker run --rm -v ${pwd}:/app --env=ASPNETCORE_ENVIRONMENT=Development -w /app/tests mcr.microsoft.com/dotnet/sdk:6.0 dotnet test ../ --logger:trx`

# License
Licensed under the terms of the [MIT License](https://raw.githubusercontent.com/bezzad/Downloader/master/LICENSE)

[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fbezzad%2FDownloader.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fbezzad%2FDownloader?ref=badge_large)

# Contributors
Thanks go to these wonderful people (List made with [contrib.rocks](https://contrib.rocks)):

<a href="https://github.com/bezzad/downloader/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=bezzad/downloader" />
</a>
