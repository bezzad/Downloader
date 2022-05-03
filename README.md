[![Windows x64](https://github.com/bezzad/Downloader/workflows/Windows%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet.yml)
[![Ubuntu x64](https://github.com/bezzad/Downloader/workflows/Ubuntu%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-core.yml)
[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/downloader?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/downloader)
[![codecov](https://codecov.io/gh/bezzad/downloader/branch/master/graph/badge.svg)](https://codecov.io/gh/bezzad/downloader)
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader)
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/f7cd6e24f75c45c28e5e6fab2ef8d219)](https://www.codacy.com/gh/bezzad/Downloader/dashboard?utm_source=github.com&utm_medium=referral&utm_content=bezzad/Downloader&utm_campaign=Badge_Grade)
[![License](https://img.shields.io/github/license/bezzad/downloader.svg)](https://github.com/bezzad/downloader/blob/master/LICENSE)
[![Generic badge](https://img.shields.io/badge/support-.Net%20Framework_&_.Net%20Core-blue.svg)](https://github.com/bezzad/Downloader)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fbezzad%2FDownloader.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2Fbezzad%2FDownloader?ref=badge_shield)

# Downloader

:rocket: Fast and reliable multipart downloader with **.Net Core 3.1+** supporting :rocket:

Downloader is a modern, fluent, asynchronous, testable and portable library for .NET. This is a multipart downloader with asynchronous progress events.
This library can added in your `.Net Core v3.1` and later or `.Net Framework v4.5` or later projects.

Downloader is compatible with .NET Standard 2.0 and above, running on Windows, Linux, and macOS, in full .NET Framework or .NET Core.

## Sample Console Application

![sample-project](https://github.com/bezzad/Downloader/raw/master/sample.gif)

# Features at a glance

- Simple interface to make download request.
- Download files async and non-blocking.
- Download any type of files like image, video, pdf, apk and etc.
- Cross-platform library to download any files with any size.
- Get real-time progress info of each block.
- Download file multipart as parallel.
- Handle all the client-side and server-side exceptions non-stopping.
- Config your `ChunkCount` to define the parts count of the download file.
- Download file multipart as `in-memory` or `in-temp files` cache mode.
- Store download package object to resume the download when you want.
- Get download speed or progress percentage in each progress event.
- Get download progress events per chunk downloads.
- Pause and Resume your downloads with package object.
- Supports large file download.
- Set a dynamic speed limit on downloads (changeable speed limitation on the go).
- Download files without storing on disk and get a memory stream for each downloaded file.
- Serializable download package (to/from `JSON` or `Binary`)
- Live streaming support, suitable for playing music at the same time as downloading.
- Ability to download just a certain range of bytes of a large file.

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
    ChunkCount = 8, // file parts to download, default value is 1
    OnTheFlyDownload = true, // caching in-memory or not? default values is true
    ParallelDownload = true // download parts of file as parallel or not. Default value is false
};
```

### Complex Configuration

```csharp
var downloadOpt = new DownloadConfiguration()
{
    BufferBlockSize = 10240, // usually, hosts support max to 8000 bytes, default values is 8000
    ChunkCount = 8, // file parts to download, default value is 1
    MaximumBytesPerSecond = 1024 * 1024, // download speed limited to 1MB/s, default values is zero or unlimited
    MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail
    OnTheFlyDownload = false, // caching in-memory or not? default values is true
    ParallelDownload = true, // download parts of file as parallel or not. Default value is false
    TempDirectory = "C:\\temp", // Set the temp path for buffering chunk files, the default path is Path.GetTempPath()
    Timeout = 1000, // timeout (millisecond) per stream block reader, default values is 1000
    RangeDownload = true, // set true if you want to download just a certain range of bytes of a large file
    RangeLow = 273618157, // floor offset of download range of a large file
    RangeHigh = 305178560, // ceiling offset of download range of a large file
    RequestConfiguration = // config and customize request headers
    {
        Accept = "*/*",
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        CookieContainer =  new CookieContainer(), // Add your cookies
        Headers = new WebHeaderCollection(), // Add your custom headers
        KeepAlive = false, // default value is false
        ProtocolVersion = HttpVersion.Version11, // Default value is HTTP 1.1
        UseDefaultCredentials = false,
        UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}"
    }
};
```

## **Step 2**: Create download service instance per download and pass your config

```csharp
var downloader = new DownloadService(downloadOpt);
```

## **Step 3**: Handle download events

```csharp
// Provide `FileName` and `TotalBytesToReceive` at the start of each downloads
downloader.DownloadStarted += OnDownloadStarted;

// Provide any information about chunker downloads, like progress percentage per chunk, speed, total received bytes and received bytes array to live streaming.
downloader.ChunkDownloadProgressChanged += OnChunkDownloadProgressChanged;

// Provide any information about download progress, like progress percentage of sum of chunks, total speed, average speed, total received bytes and received bytes array to live streaming.
downloader.DownloadProgressChanged += OnDownloadProgressChanged;

// Download completed event that can include occurred errors or cancelled or download completed successfully.
downloader.DownloadFileCompleted += OnDownloadFileCompleted;
```

## **Step 4**: Start the download with the url and file name

```csharp
string file = @"Your_Path\fileName.zip";
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileTaskAsync(url, file);
```

## **Step 4b**: Start the download without file name

```csharp
DirectoryInfo path = new DirectoryInfo("Your_Path");
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileTaskAsync(url, path); // download into "Your_Path\fileName.zip"
```

## **Step 4c**: Download in MemoryStream

```csharp
Stream destinationStream = await downloader.DownloadFileTaskAsync(url);
```

---

## How to stop and resume downloads

The ‍`DownloadService` class has a property called `Package` that stores each step of the download. To stopping or pause the download you must call the `CancelAsync` method, and if you want to continue again, you must call the same `DownloadFileTaskAsync` function with the `Package` parameter to resume your download!
For example:

Keep `Package` file to resume from last download positions:

```csharp
DownloadPackage pack = downloader.Package;
```

**Stop or Pause Download:**

```csharp
downloader.CancelAsync();
```

**Resume Download:**

```csharp
await downloader.DownloadFileTaskAsync(pack);
```

So that you can even save your large downloads with a very small amount in the Package and after restarting the program, restore it again and start continuing your download. In fact, the packages are your instant download snapshots. If your download config has OnTheFlyDownload, the downloaded bytes ​​will be stored in the package itself, but otherwise, only the downloaded file address will be included and you can resume it whenever you like.
For more detail see [StopResumeDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/IntegrationTests/DownloadIntegrationTest.cs#L114) method

> Note: for complete sample see `Downloader.Sample` project from this repository.

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
```

Resume the existing download package:

```csharp
await DownloadBuilder.Build(package);
```

Resume the existing download package with a new configuration:

```csharp
await DownloadBuilder.Build(package, new DownloadConfiguration())
    .StartAsync();
```

---

## How to serialize and deserialize downloader package

### **What is Serialization?**

Serialization is the process of converting an object's state into information that can be stored for later retrieval or that can be sent to another system. For example, you may have an object that represents a document that you wish to save. This object could be serialized to a stream of binary information and stored as a file on disk. Later the binary data can be retrieved from the file and deserialized into objects that are exact copies of the original information. As a second example, you may have an object containing the details of a transaction that you wish to send to a non-.NET system. This information could be serialised to XML before being transmitted. The receiving system would convert the XML into a format that it could understand.

In this section, we want to show how to serialize download packages to `JSON` text or `Binary`, after stopping download to keep download data and resuming that every time you want.
You can serialize packages even using memory storage for caching download data which is used `MemoryStream`.

### **Binary Serialization**

To serialize or deserialize the package into a binary file, just you need to a [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter) of [IFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.iformatter) and then create a stream to write bytes on that:

```csharp
DownloadPackage pack = downloader.Package;
IFormatter formatter = new BinaryFormatter();
Stream serializedStream = new MemoryStream();
```

Serializing package:

```csharp
formatter.Serialize(serializedStream, pack);
```

Deserializing into the new package:

```csharp
var newPack = formatter.Deserialize(serializedStream) as DownloadPackage;
```

For more detail see [PackageSerializationTest](https://github.com/bezzad/Downloader/blob/46167082b8de99d8e6ad21329c3a32a6e26cfd3e/src/Downloader.Test/DownloadPackageTest.cs#L51) method.

### **JSON Serialization**

Serializing the package to [`JSON`](https://www.newtonsoft.com) is very simple like this:

```csharp
var packageJson = JsonConvert.SerializeObject(package);
```

But to deserializing the [IStorage Storage](https://github.com/bezzad/Downloader/blob/e4ab807a2e107c9ae4902257ba82f71b33494d91/src/Downloader/Chunk.cs#L28) property of chunks you need to declare a [JsonConverter](https://github.com/bezzad/Downloader/blob/78085b7fb418e6160de444d2e97a5d2fa6ed8da0/src/Downloader.Test/StorageConverter.cs#L7) to override the Read method of `JsonConverter`. So you should add the below converter to your application:

```csharp
public class StorageConverter : Newtonsoft.Json.JsonConverter<IStorage>
{
    public override void WriteJson(JsonWriter writer, IStorage value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override IStorage ReadJson(JsonReader reader, Type objectType, IStorage existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        var obj = JObject.Load(reader); // Throws an exception if the current token is not an object.
        if (obj.ContainsKey(nameof(FileStorage.FileName)))
        {
            var filename = obj[nameof(FileStorage.FileName)]?.Value<string>();
            return new FileStorage(filename);
        }

        if (obj.ContainsKey(nameof(MemoryStorage.Data)))
        {
            var data = obj[nameof(MemoryStorage.Data)]?.Value<string>();
            return new MemoryStorage() { Data = data };
        }

        return null;
    }
}
```

Then you can deserialize your packages from `JSON`:

```csharp
var settings = new Newtonsoft.Json.JsonSerializerSettings();
settings.Converters.Add(new StorageConverter());
var newPack = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(serializedJson, settings);
```

For more detail see [PackageSerializationTest](https://github.com/bezzad/Downloader/blob/46167082b8de99d8e6ad21329c3a32a6e26cfd3e/src/Downloader.Test/DownloadPackageTest.cs#L34) method

# Instructions for Contributing

Welcome to contribute, feel free to change and open a [**PullRequest**](http://help.github.com/pull-requests/) to develop branch.
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
1. Send a Pull Request on GitHub - the PR should target (have as base branch) the latest development branch (eg `vX.Y.Z`) rather than `master`.

We accept pull requests from the community. But, you should **never** work on a clone of master, and you should **never** send a pull request from master - always from a branch. Please be sure to branch from the head of the latest vX.Y.Z `develop` branch (rather than `master`) when developing contributions.

# License

Licensed under the terms of the [MIT License](https://raw.githubusercontent.com/bezzad/Downloader/master/LICENSE)

[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2Fbezzad%2FDownloader.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2Fbezzad%2FDownloader?ref=badge_large)
