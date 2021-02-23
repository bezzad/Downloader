[![Windows x64](https://github.com/bezzad/Downloader/workflows/Windows%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet.yml)
[![Ubuntu x64](https://github.com/bezzad/Downloader/workflows/Ubuntu%20x64/badge.svg)](https://github.com/bezzad/Downloader/actions/workflows/dotnet-core.yml)
[![Build Status](https://ci.appveyor.com/api/projects/status/github/bezzad/downloader?branch=master&svg=true)](https://ci.appveyor.com/project/bezzad/downloader) 
[![NuGet](https://img.shields.io/nuget/dt/downloader.svg)](https://www.nuget.org/packages/downloader) 
[![NuGet](https://img.shields.io/nuget/vpre/downloader.svg)](https://www.nuget.org/packages/downloader)
[![CodeFactor](https://www.codefactor.io/repository/github/bezzad/downloader/badge/master)](https://www.codefactor.io/repository/github/bezzad/downloader/overview/master)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/aa77095a38f84d98877434c2d73d288c)](https://app.codacy.com/gh/bezzad/Downloader?utm_source=github.com&utm_medium=referral&utm_content=bezzad/Downloader&utm_campaign=Badge_Grade_Settings)
[![License](https://img.shields.io/github/license/bezzad/downloader.svg)](https://github.com/bezzad/downloader/blob/master/LICENSE)

# Downloader

:rocket: Fast and reliable multipart downloader with **.Net Core 3.1+** supporting :rocket:

Downloader is a modern, fluent, asynchronous, testable and portable library for .NET. This is a multipart downloader with asynchronous progress events.
This library can added in your `.Net Core v3.1` and later or `.Net Framework v4.5` or later projects.

----------------------------------------------------

## Sample Console Application
![sample-project](https://github.com/bezzad/Downloader/raw/master/sample.gif)

----------------------------------------------------

## How to use

Get it on [NuGet](https://www.nuget.org/packages/Downloader):

    PM> Install-Package Downloader

Or via the .NET Core command line interface:

    dotnet add package Downloader

Create your custom configuration:
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
    RequestConfiguration = // config and customize request headers
    {
        Accept = "*/*",
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        CookieContainer =  new CookieContainer(), // Add your cookies
        Headers = new WebHeaderCollection(), // Add your custom headers
        KeepAlive = false,
        ProtocolVersion = HttpVersion.Version11, // Default value is HTTP 1.1
        UseDefaultCredentials = false,
        UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}"
    }
};
```

So, declare download service instance per download and pass your config:
```csharp
var downloader = new DownloadService(downloadOpt);
```

Then handle download progress and completed events:
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

__Start the download asynchronously__
```csharp
string file = @"Your_Path\fileName.zip";
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileAsync(url, file);
```

__Download into a folder without file name__
```csharp
DirectoryInfo path = new DirectoryInfo("Your_Path");
string url = @"https://file-examples.com/fileName.zip";
await downloader.DownloadFileAsync(url, path); // download into "Your_Path\fileName.zip"
```

__Download on MemoryStream__
```csharp
Stream destinationStream = await downloader.DownloadFileAsync(url);
```

The ‍`DownloadService` class has a property called `Package` that stores each step of the download. To stopping or pause the download you must call the `CancelAsync` method, and if you want to continue again, you must call the same `DownloadFileAsync` function with the `Package` parameter to resume your download! 
For example:

Keep `Package` file to resume from last download positions:
```csharp
DownloadPackage pack = downloader.Package; 
```

__Stop or Pause Download:__
```csharp
downloader.CancelAsync(); 
```

__Resume Download:__
```csharp
await downloader.DownloadFileAsync(pack); 
```

So that you can even save your large downloads with a very small amount in the Package and after restarting the program, restore it again and start continuing your download. In fact, the packages are your instant download snapshots. If your download config has OnTheFlyDownload, the downloaded bytes ​​will be stored in the package itself, but otherwise, only the downloaded file address will be included and you can resume it whenever you like. 
For more detail see [StopResumeDownloadTest](https://github.com/bezzad/Downloader/blob/master/src/Downloader.Test/DownloadIntegrationTest.cs#L79) method


> Note: for complete sample see `Downloader.Sample` project from this repository.

----------------------------------------------------

## How to serialize and deserialize downloader package

Serialize and deserialize download packages to/from `JSON` text or `Binary`, after stopping download to keep download data and resuming that every time you want. You can serialize packages even using memory storage for caching download data which is used `MemoryStream`.

__Serialize and Deserialize into Binary with [BinaryFormatter](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.formatters.binary.binaryformatter)__

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

For more detail see [PackageSerializationTest](https://github.com/bezzad/Downloader/blob/46167082b8de99d8e6ad21329c3a32a6e26cfd3e/src/Downloader.Test/DownloadPackageTest.cs#L51) method


__Serialize and Deserialize into `JSON` text with [Newtonsoft.Json](https://www.newtonsoft.com)__

Serializing the package to `JSON` is very simple like this:
```csharp
var serializedJson = Newtonsoft.Json.JsonConvert.SerializeObject(pack);
```

But to deserializing the `IStorage Storage` property of chunks you need to declare a [JsonConverter](https://github.com/bezzad/Downloader/blob/78085b7fb418e6160de444d2e97a5d2fa6ed8da0/src/Downloader.Test/StorageConverter.cs#L7) to override the Read method of `JsonConverter`. So you should add the below converter to your application:

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
----------------------------------------------------

## Features at a glance

- Download files async and non-blocking.
- Cross-platform library to download any files with any size.
- Get real-time progress info of each block.
- Download file multipart as parallel.
- Handle any client-side or server-side exception none-stopping the downloads.
- Config your `ChunkCount` to define the parts count of the download file.
- Download file multipart as `in-memory` or `in-temp files` cache mode.
- Store download package object to resume the download when you want.
- Get download speed or progress percentage in each progress event.
- Get download progress events per chunk downloads.
- Stop and Resume your downloads with package object.
- Set a speed limit on downloads.
- Live streaming support, suitable for playing music at the same time as downloading.
- Download files without storing on disk and get a memory stream for each downloaded file.
- Serialize and deserialize download packages to/from `JSON` text or `Binary`.