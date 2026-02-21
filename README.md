[![Windows x64](https://github.com/bezzad/Downloader/workflows/Windows%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-windows.yml)
[![Ubuntu x64](https://github.com/bezzad/Downloader/workflows/Ubuntu%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-ubuntu.yml)
[![MacOS](https://github.com/bezzad/Downloader/workflows/MacOS/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-macos.yml)
[![codecov](https://codecov.io/github/bezzad/Downloader/graph/badge.svg?token=CnLljCB3zO)](https://codecov.io/github/bezzad/Downloader)
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader)
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)
[![License](https://img.shields.io/github/license/bezzad/downloader.svg)](https://github.com/bezzad/downloader/blob/master/LICENSE)
[![Generic badge](https://img.shields.io/badge/support-.Net_8.0-purple.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_9.0-purple.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_10.0-purple.svg)](https://github.com/bezzad/Downloader)
[![Generic badge](https://img.shields.io/badge/support-.Net_Standard_2.1_on_v3.x.x-blue.svg)](https://github.com/bezzad/Downloader)


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
- Always downloads to a temporary `.download` file, then renames to the final name on completion.
- Always pre-allocates file size before download begins.
- Resume downloads manually by saving and restoring the `DownloadPackage` object.
- Automatic resume: when enabled, download metadata is embedded inside the `.download` file — no extra files or manual serialization needed.
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
    BufferBlockSize = 10240, // 10KB
    // file parts to download, the default value is 1
    ChunkCount = 8,             
    // download speed limited to MaximumBytesPerSecond, default values is zero or unlimited
    MaximumBytesPerSecond = 1024*1024*2, // 2MB/s
    // the maximum number of times to fail
    MaxTryAgainOnFailure = 5,    
    // release memory buffer after each MaximumMemoryBufferBytes 
    MaximumMemoryBufferBytes = 1024 * 1024 * 50, // 50MB
    // download parts of the file as parallel or not. The default value is false
    ParallelDownload = true,
    // number of parallel downloads. The default value is the same as the chunk count
    ParallelCount = 4,    
    // timeout (millisecond) per stream block reader, default values is 1000
    BlockTimeout = 1000,
    // timeout (millisecond) per HttpClientRequest, default values is 100 Seconds
    HTTPClientTimeout = 100 * 1000,
    // set true if you want to download just a specific range of bytes of a large file
    RangeDownload = false,
    // floor offset of download range of a large file
    RangeLow = 0,
    // ceiling offset of download range of a large file
    RangeHigh = 0, 
    // clear package chunks data when download completed with failure, default value is false
    ClearPackageOnCompletionWithFailure = true, 
    // the minimum size of file to chunking or download a file in multiple parts, the default value is 512
    MinimumSizeOfChunking = 102400, // 100KB
    // the minimum size of a single chunk, default value is 0 equal unlimited
    MinimumChunkSize = 10240, // 10KB
    // Get on demand downloaded data with ReceivedBytes on downloadProgressChanged event 
    EnableLiveStreaming = false,
    // How to handle existing filename when starting to download?
    FileExistPolicy = FileExistPolicy.Delete,
    // When enabled, the Downloader appends package metadata to the end of the
    // .download file. On the next download attempt, if metadata is found in an
    // existing .download file, the download resumes automatically.
    EnableAutoResumeDownload = true,
    // A temporary extension appended to the real filename while downloading.
    // e.g., "file.zip" becomes "file.zip.download" during download.
    // The Downloader always uses this extension regardless of EnableAutoResumeDownload.
    // When the download completes, the file is renamed back to its final name.
    DownloadFileExtension = ".download",
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
### How to **stop** and **resume** downloads (manual approach)

The `DownloadService` class has a property called `Package` that holds a live snapshot of the download state (chunk positions, URL, file path, etc.). While the download is in progress, this object is updated continuously.

To stop and later resume a download **manually**, you are responsible for keeping the `Package` object yourself — either in memory or serialized to disk. The Downloader does not store it for you in this approach.

```csharp
// 1. Keep a reference to the package before or after stopping:
DownloadPackage pack = downloader.Package;
```

**Stop or cancel the download:**

```csharp
await downloader.CancelAsync();
```

**Resume later — even after restarting the application:**

```csharp
// Pass the same (or deserialized) package to resume from the last position:
await downloader.DownloadFileTaskAsync(pack);
```

The `Package` object is lightweight — it contains only the URL, file path, and the position of each chunk (not the downloaded bytes). You can serialize it to JSON or binary (see [Serialization section](#how-to-serialize-and-deserialize-the-downloader-package)) and restore it at any time.

For more details see the [StopResumeDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/IntegrationTests/DownloadIntegrationTest.cs#L210) method.

> **Note:** If the server does not support HTTP range requests, the download cannot be resumed and will restart from the beginning.

---
### How to **automatically resume** downloads (recommended)

If you don't want to manage `DownloadPackage` serialization yourself, enable `EnableAutoResumeDownload`. The Downloader will then handle everything automatically — no manual serialization or package management is needed.

**Configuration:**

```csharp
var downloadOpt = new DownloadConfiguration()
{
    EnableAutoResumeDownload = true,
    DownloadFileExtension = ".download" // optional, this is the default
};

var downloader = new DownloadService(downloadOpt);
```

**How `.download` files work:**

The Downloader **always** uses a temporary file with the `.download` extension (configurable via `DownloadFileExtension`) — this behavior is independent of `EnableAutoResumeDownload`. For example, downloading `report.pdf` creates `report.pdf.download` on disk. While the download is in progress, only the `.download` file exists — the user does not see the final filename. When the download completes successfully, the file is renamed to `report.pdf`.

**What `EnableAutoResumeDownload` adds:**

When this option is `true`, the Downloader appends package metadata to the **end** of the `.download` file, immediately after the file data. This metadata enables automatic resume without any work from the caller.

The file structure during download looks like this:

```
report.pdf.download:
|<---------- File Data (TotalFileSize) ----------><-- Metadata -->|
```

- **File Data region**: The actual file content. The file size for this region is pre-allocated at the start.
- **Metadata region**: The `DownloadPackage` state (chunk positions, URL, etc.) is serialized to JSON and then written as binary at the **end** of the same file.

The metadata grows as the download progresses (more chunk positions are recorded), so the on-disk file size is always `TotalFileSize + current metadata size`. The metadata only grows — it never shrinks — so no padding or extra management is needed.

**On interruption:**

If the download is interrupted (crash, network failure, app restart), the `.download` file remains on disk with both the partial file data and the latest metadata embedded at the end.

**On resume:**

When you call `DownloadFileTaskAsync` for the same file again, the Downloader:
1. Detects the existing `.download` file
2. Reads the metadata from the end of the file to restore the `DownloadPackage` state
3. Verifies the server still supports range requests and that the file size has not changed
4. Resumes downloading from where each chunk left off
5. Falls back to a fresh download if any validation fails

**On completion:**

When the download finishes successfully:
1. The file stream is truncated to `TotalFileSize` using `SetLength(TotalFileSize)`, which removes the appended metadata
2. The file is renamed from `report.pdf.download` to `report.pdf`

The result is a clean final file with no metadata artifacts.

> **Note:** If the server does not support range requests or the remote file size has changed, the Downloader will discard the partial data and start a fresh download.

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

> **Tip:** If you use `EnableAutoResumeDownload = true`, you do **not** need to serialize the package yourself — the Downloader handles it automatically by embedding metadata in the `.download` file. The section below is only relevant if you use the **manual resume** approach.

The `DownloadPackage` object holds the download state (URL, file path, chunk positions). You can serialize it to JSON or binary so that you can restore and resume a stopped download later — even after the application restarts.

### **JSON Serialization**

```csharp
// Serialize
var packageJson = JsonConvert.SerializeObject(package);

// Deserialize
var restoredPack = JsonConvert.DeserializeObject<DownloadPackage>(packageJson);

// Resume
await downloader.DownloadFileTaskAsync(restoredPack);
```

For more details see the [PackageSerializationTest](https://github.com/bezzad/Downloader/blob/46167082b8de99d8e6ad21329c3a32a6e26cfd3e/src/Downloader.Test/DownloadPackageTest.cs#L34) method.

### **Binary Serialization**

To save the package as a binary file, serialize it to JSON first and then write it with [BinaryWriter](https://learn.microsoft.com/en-us/dotnet/api/system.io.binarywriter).

> **NOTE**: Do not use [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) — it is deprecated and insecure. [Reference](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide)

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