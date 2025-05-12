[![Windows x64](https://github.com/bezzad/Downloader/workflows/Windows%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-windows.yml)
[![Ubuntu x64](https://github.com/bezzad/Downloader/workflows/Ubuntu%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-ubuntu.yml)
[![MacOS](https://github.com/bezzad/Downloader/workflows/MacOS/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-macos.yml)
[![Build Status](https://ci.appveyor.com/api/projects/status/dsghbc9nj1in2l6f?svg=true)](https://ci.appveyor.com/project/bezzad/downloader)
[![codecov](https://codecov.io/github/bezzad/Downloader/graph/badge.svg?token=CnLljCB3zO)](https://codecov.io/github/bezzad/Downloader)
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader)
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)
[![License](https://img.shields.io/github/license/bezzad/downloader.svg)](https://github.com/bezzad/downloader/blob/master/LICENSE)
[![Generic badge](https://img.shields.io/badge/support-.Net_Framework-blue.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_8.0-purple.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_Standard_2.1-blue.svg)](https://github.com/bezzad/Downloader)

# Downloader

:rocket: Fast, cross-platform, and reliable multipart downloader in `.Net` :rocket:

**Downloader** is a modern, fluent, asynchronous, and portable library for .NET, built with testability in mind. It supports multipart downloads with real-time asynchronous progress events. The library is compatible with projects targeting `.NET Standard 2.1`, `.NET 8`, and later versions.

Downloader works on Windows, Linux, and macOS.
> **Note**: Support for older versions of .NET was removed in Downloader `v3.2.0`. From this version onwards, only `.Net 8.0` and higher versions are supported.  
> If you need compatibility with older .NET versions (e.g., `.NET Framework 4.6.1`), use Downloader `v3.1.*`.

> For a complete example, see the [Downloader.Sample](https://github.com/bezzad/Downloader/blob/master/src/Samples/Downloader.Sample/Program.cs) project in this repository.

## Sample Console Application

![sample-project](https://github.com/bezzad/Downloader/raw/master/sample.gif)

---

## Key Features

- Simple interface for download requests.
- Asynchronous, non-blocking file downloads.
- Supports all file types (e.g., images, videos, PDFs, APKs).
- Cross-platform support for files of any size.
- Real-time progress updates for each download chunk.
- Downloads files in multiple parts (parallel download).
- Resilient to client-side and server-side errors.
- Configurable `ChunkCount` to control download segmentation.
- Supports both in-memory and on-disk multipart downloads.
- Parallel saving of chunks directly into the final file (no temporary files).
- Pre-allocates file size before download begins.
- Ability to resume downloads with a saved package object.
- Provides real-time speed and progress data.
- Asynchronous pause and resume functionality.
- Download files with dynamic speed limits.
- Supports downloading to memory streams (without saving to disk).
- Supports large file downloads and live-streaming (e.g., music playback during download).
- Download a specific byte range from a large file.
- Lightweight, fast codebase with no external dependencies.
- Manage RAM usage during downloads.

---

## Installation via [NuGet](https://www.nuget.org/packages/downloader)

    PM> Install-Package Downloader

## Installation via the .NET CLI

    dotnet add package Downloader

---

## Usage

### **Step 1**: Create a Custom Configuration

#### Simple Configuration

```csharp
var downloadOpt = new DownloadConfiguration()
{
    ChunkCount = 8, // Number of file parts, default is 1
    ParallelDownload = true // Download parts in parallel (default is false)
};
```

### Complex Configuration


> **Note**: Only include the options you need in your application.

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
    MaxTryAgainOnFailure = 5,    
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
    ReserveStorageSpaceBeforeStartingDownload = true,
    // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 
    EnableLiveStreaming = false, 
    
    // config and customize request headers
    RequestConfiguration = 
    {        
        Accept = "*/*",
        CookieContainer = cookies,
        Headers = ["Accept-Encoding: gzip, deflate, br"], // { your custom headers }
        KeepAlive = true, // default value is false
        ProtocolVersion = HttpVersion.Version11, // default value is HTTP 1.1
        // your custom user agent or your_app_name/app_version.
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        Proxy = new WebProxy() {
           Address = new Uri("http://YourProxyServer/proxy.pac"),
           UseDefaultCredentials = false,
           Credentials = System.Net.CredentialCache.DefaultNetworkCredentials,
           BypassProxyOnLocal = true
        },
        Authorization = new AuthenticationHeaderValue("Bearer", "token");
    }
};
```

### **Step 2**: Create the Download Service

```csharp
var downloader = new DownloadService(downloadOpt);
```

### **Step 3**: Handle Download Events

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

### **Step 4**: Start the Download

```csharp
string file = @"Your_Path\fileName.zip";
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileTaskAsync(url, file);
```

### **Step 4b**: Start the download without file name

```csharp
DirectoryInfo path = new DirectoryInfo("Your_Path");
string url = @"https://file-examples.com/fileName.zip";
// download into "Your_Path\fileName.zip"
await downloader.DownloadFileTaskAsync(url, path); 
```

### **Step 4c**: Download in MemoryStream

```csharp
// After download completion, it gets a MemoryStream
Stream destinationStream = await downloader.DownloadFileTaskAsync(url); 
```

---
### How to **pause** and **resume** downloads quickly

When you want to resume a download quickly after pausing a few seconds. You can call the `Pause` function of the downloader service. This way, streams stay alive and are only suspended by a locker to be released and resumed whenever you want.

```csharp
// Pause the download
DownloadService.Pause();

// Resume the download
DownloadService.Resume();
```

---
### How to **stop** and **resume** downloads other time

The `DownloadService` class has a property called `Package` that stores each step of the download. To stop the download you must call the `CancelAsync` method. Now, if you want to continue again, you must call the same `DownloadFileTaskAsync` function with the `Package` parameter to resume your download. For example:

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

**Resuming download after cancellation:**

```csharp
await downloader.DownloadFileTaskAsync(pack);
```

So that you can even save your large downloads with a very small amount in the Package and after restarting the program, restore it and start continuing your download. 
The packages are your snapshot of the download instance. Only the downloaded file addresses will be included in the package, and you can resume it whenever you want. 
For more detail see [StopResumeDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/IntegrationTests/DownloadIntegrationTest.cs#L210) method

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

## 🚀 Building a Native AOT Version

This project supports **Ahead-of-Time (AOT)** compilation, which generates a standalone native executable with faster startup times and reduced memory usage. Follow these steps to build the AOT version.

---

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
- A supported platform (Windows, Linux, or macOS).

---

### Build Instructions

#### 1. Clone the Repository
```bash
git clone https://github.com/bezzad/downloader.git
cd downloader
```

#### 2. Build the Native AOT Executable
Run the following command to compile the project for your target platform:

Windows (x64):
```bash
dotnet publish -r win-x64 -f net8.0 -c Release
```

Linux (x64):
```bash
dotnet publish -r linux-x64 -f net8.0 -c Release
```

macOS (x64):
```bash
dotnet publish -r osx-x64 -f net8.0 -c Release
```

#### 3. Find the Output
The compiled executable will be located in:
```bash
bin/Release/net8.0/<RUNTIME_IDENTIFIER>/publish/
```

Example for Windows:
```bash
bin/Release/net8.0/win-x64/publish/
```











# Instructions for Contributing

Welcome to contribute, feel free to change and open a [**PullRequest**](http://help.github.com/pull-requests/) to develop the branch.
You can use either the latest version of Visual Studio or Visual Studio Code and .NET CLI for Windows, Mac and Linux.

For GitHub workflow, check out our Git workflow below this paragraph. We are following the excellent GitHub Flow process, and would like to make sure you have all the information needed to be a world-class contributor!

## Git Workflow

The general process for working with Downloader is:

1. [Fork](http://help.github.com/forking/) on GitHub
2. Make sure your line endings are correctly configured and fix your line endings!
3. Clone your fork locally
4. Configure the upstream repo (`git remote add upstream git://github.com/bezzad/downloader`)
5. Switch to the latest development branch (e.g. vX.Y.Z, using `git checkout vX.Y.Z`)
6. Create a local branch from that (`git checkout -b myBranch`).
7. Work on your feature
8. Rebase if required
9. Push the branch up to GitHub (`git push origin myBranch`)
10. Send a Pull Request on GitHub - the PR should target (have as a base branch) the latest development branch (eg `vX.Y.Z`) rather than `master`.

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
Thanks go to these wonderful people (List made with [contrib. rocks](https://contrib.rocks)):

<a href="https://github.com/bezzad/downloader/graphs/contributors">
  <img alt="downloader contributors" src="https://contrib.rocks/image?repo=bezzad/downloader" />
</a>
