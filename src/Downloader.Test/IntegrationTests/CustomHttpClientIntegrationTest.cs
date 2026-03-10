using Downloader.Extensions;
using System.Net.Http;

namespace Downloader.Test.IntegrationTests;

[Collection("Sequential")]
public class CustomHttpClientIntegrationTest : BaseTestClass, IDisposable
{
    private string Filename { get; }
    private string FilePath { get; }
    private int FileSize { get; }
    private static byte[] FileData { get; set; }
    private string Url { get; }

    public CustomHttpClientIntegrationTest(ITestOutputHelper output) : base(output)
    {
        Filename = Path.GetRandomFileName();
        FilePath = Path.Combine(Path.GetTempPath(), Filename);
        FileSize = DummyFileHelper.FileSize16Kb;
        FileData ??= DummyFileHelper.File16Kb;
        Url = DummyFileHelper.GetFileWithNameUrl(Filename, FileSize);
    }

    public void Dispose()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    [Fact]
    public async Task DownloadWithCustomHttpClientFactoryTest()
    {
        // arrange
        bool httpClientFactoryCalled = false;
        HttpClient createdClient = null;
        var config = new DownloadConfiguration {
            ChunkCount = 1,
            ParallelDownload = false,
            CustomHttpClientFactory = () => {
                httpClientFactoryCalled = true;
                var handler = new SocketsHttpHandler {
                    MaxConnectionsPerServer = 100,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };
                createdClient = new HttpClient(handler) {
                    Timeout = TimeSpan.FromSeconds(60)
                };
                return createdClient;
            }
        };
        var downloader = new DownloadService(config, LogFactory);
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            // act
            await using Stream memoryStream = await downloader.DownloadFileTaskAsync(Url);

            // assert
            Assert.True(httpClientFactoryCalled, "Custom HttpClient factory should have been called");
            Assert.True(downloadCompletedSuccessfully, resultMessage);
            Assert.NotNull(memoryStream);
            Assert.Equal(FileSize, memoryStream.Length);
            Assert.Equal(FileSize, downloader.Package.TotalFileSize);
            Assert.True(FileData.AreEqual(memoryStream));
        }
        finally
        {
            createdClient?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithCustomHttpMessageHandlerFactoryTest()
    {
        // arrange
        bool handlerFactoryCalled = false;
        SocketsHttpHandler createdHandler = null;
        var config = new DownloadConfiguration {
            ChunkCount = 1,
            ParallelDownload = false,
            CustomHttpMessageHandlerFactory = () => {
                handlerFactoryCalled = true;
                createdHandler = new SocketsHttpHandler {
                    MaxConnectionsPerServer = 100,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };
                return createdHandler;
            }
        };
        var downloader = new DownloadService(config, LogFactory);
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            // act
            await using Stream memoryStream = await downloader.DownloadFileTaskAsync(Url);

            // assert
            Assert.True(handlerFactoryCalled, "Custom HttpMessageHandler factory should have been called");
            Assert.True(downloadCompletedSuccessfully, resultMessage);
            Assert.NotNull(memoryStream);
            Assert.Equal(FileSize, memoryStream.Length);
            Assert.Equal(FileSize, downloader.Package.TotalFileSize);
            Assert.True(FileData.AreEqual(memoryStream));
        }
        finally
        {
            createdHandler?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithCustomHttpClientFactoryToFileTest()
    {
        // arrange
        HttpClient createdClient = null;
        var config = new DownloadConfiguration {
            ChunkCount = 4,
            ParallelDownload = true,
            CustomHttpClientFactory = () => {
                var handler = new SocketsHttpHandler {
                    MaxConnectionsPerServer = 100,
                };
                createdClient = new HttpClient(handler) {
                    Timeout = TimeSpan.FromSeconds(60)
                };
                return createdClient;
            }
        };
        var downloader = new DownloadService(config, LogFactory);
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            // act
            await downloader.DownloadFileTaskAsync(Url, FilePath);

            // assert
            Assert.True(downloadCompletedSuccessfully, resultMessage);
            Assert.True(File.Exists(FilePath));
            Assert.Equal(FileSize, new FileInfo(FilePath).Length);
            Assert.Equal(FileSize, downloader.Package.TotalFileSize);
        }
        finally
        {
            createdClient?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithCustomHttpClientFactoryPrecedenceOverHandlerFactoryTest()
    {
        // arrange  
        // When both factories are set, CustomHttpClientFactory takes precedence
        bool httpClientFactoryCalled = false;
        bool handlerFactoryCalled = false;
        HttpClient createdClient = null;
        var config = new DownloadConfiguration {
            ChunkCount = 1,
            ParallelDownload = false,
            CustomHttpClientFactory = () => {
                httpClientFactoryCalled = true;
                createdClient = new HttpClient(new SocketsHttpHandler()) {
                    Timeout = TimeSpan.FromSeconds(60)
                };
                return createdClient;
            },
            CustomHttpMessageHandlerFactory = () => {
                handlerFactoryCalled = true;
                return new SocketsHttpHandler();
            }
        };
        var downloader = new DownloadService(config, LogFactory);
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            // act
            await using Stream memoryStream = await downloader.DownloadFileTaskAsync(Url);

            // assert
            Assert.True(httpClientFactoryCalled, "Custom HttpClient factory should have been called");
            Assert.False(handlerFactoryCalled, "Custom HttpMessageHandler factory should NOT have been called when HttpClient factory is set");
            Assert.True(downloadCompletedSuccessfully, resultMessage);
            Assert.NotNull(memoryStream);
            Assert.Equal(FileSize, memoryStream.Length);
        }
        finally
        {
            createdClient?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithCustomHandlerMultiChunkParallelTest()
    {
        // arrange
        int handlerCallCount = 0;
        SocketsHttpHandler createdHandler = null;
        var config = new DownloadConfiguration {
            ChunkCount = 4,
            ParallelDownload = true,
            ParallelCount = 4,
            CustomHttpMessageHandlerFactory = () => {
                Interlocked.Increment(ref handlerCallCount);
                createdHandler = new SocketsHttpHandler {
                    MaxConnectionsPerServer = 500,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10)
                };
                return createdHandler;
            }
        };
        var downloader = new DownloadService(config, LogFactory);
        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        downloader.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            // act
            await using Stream memoryStream = await downloader.DownloadFileTaskAsync(Url);

            // assert
            Assert.True(downloadCompletedSuccessfully, resultMessage);
            Assert.NotNull(memoryStream);
            Assert.Equal(FileSize, memoryStream.Length);
            Assert.True(FileData.AreEqual(memoryStream));
            Assert.Equal(1, handlerCallCount);
        }
        finally
        {
            createdHandler?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithBuilderAndCustomHttpClientTest()
    {
        // arrange
        bool httpClientFactoryCalled = false;
        HttpClient createdClient = null;

        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(Url)
            .WithHttpClient(() => {
                httpClientFactoryCalled = true;
                createdClient = new HttpClient(new SocketsHttpHandler()) {
                    Timeout = TimeSpan.FromSeconds(60)
                };
                return createdClient;
            })
            .Build();

        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        download.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            await download.StartAsync();

            // assert
            Assert.True(httpClientFactoryCalled, "Custom HttpClient factory should have been called via builder");
            Assert.True(downloadCompletedSuccessfully, resultMessage);
        }
        finally
        {
            createdClient?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadWithBuilderAndCustomHandlerTest()
    {
        // arrange
        bool handlerFactoryCalled = false;
        SocketsHttpHandler createdHandler = null;

        // act
        IDownload download = DownloadBuilder.New()
            .WithUrl(Url)
            .WithHttpMessageHandler(() => {
                handlerFactoryCalled = true;
                createdHandler = new SocketsHttpHandler {
                    MaxConnectionsPerServer = 100
                };
                return createdHandler;
            })
            .Build();

        bool downloadCompletedSuccessfully = false;
        string resultMessage = "";
        download.DownloadFileCompleted += (_, e) => {
            if (e.Cancelled == false && e.Error == null)
                downloadCompletedSuccessfully = true;
            else
                resultMessage = e.Error?.Message;
        };

        try
        {
            await download.StartAsync();

            // assert
            Assert.True(handlerFactoryCalled, "Custom handler factory should have been called via builder");
            Assert.True(downloadCompletedSuccessfully, resultMessage);
        }
        finally
        {
            createdHandler?.Dispose();
        }
    }
}
