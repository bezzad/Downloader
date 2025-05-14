using System.Net.Http.Headers;

namespace Downloader.Test.HelperTests;

public class DummyFileControllerTest
{
    private readonly string _contentType = "application/octet-stream";
    private Dictionary<string, string> _headers;
    private const string Filename = "test-filename.dat";


    [Fact]
    public async Task GetFileTest()
    {
        // arrange
        const int size = 1024;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileUrl(size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
    }

    [Fact]
    public async Task GetFileWithNameTest()
    {
        // arrange
        const int size = 2048;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
    }

    [Fact]
    public async Task GetSingleByteFileWithNameTest()
    {
        // arrange
        const int size = 2048;
        const byte fillByte = 13;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithNameUrl(Filename, size, fillByte);
        byte[] dummyData = DummyData.GenerateSingleBytes(size, fillByte);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(bytes.All(i => i == fillByte));
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
    }

    [Fact]
    public async Task GetFileWithoutHeaderTest()
    {
        // arrange
        const int size = 2048;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithoutHeaderUrl(Filename, size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.False(_headers.ContainsKey("Content-Length"));
        Assert.False(_headers.ContainsKey("Content-Type"));
    }

    [Fact]
    public async Task GetSingleByteFileWithoutHeaderTest()
    {
        // arrange
        const int size = 2048;
        const byte fillByte = 13;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithoutHeaderUrl(Filename, size, fillByte);
        byte[] dummyData = DummyData.GenerateSingleBytes(size, fillByte);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(bytes.All(i => i == fillByte));
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.False(_headers.ContainsKey("Content-Length"));
        Assert.False(_headers.ContainsKey("Content-Type"));
    }

    [Fact]
    public async Task GetFileWithContentDispositionTest()
    {
        // arrange
        const int size = 1024;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(Filename, size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.Contains($"filename={Filename};", _headers["Content-Disposition"]);
    }

    [Fact]
    public async Task GetSingleByteFileWithContentDispositionTest()
    {
        // arrange
        const int size = 1024;
        const byte fillByte = 13;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(Filename, size, fillByte);
        byte[] dummyData = DummyData.GenerateSingleBytes(size, fillByte);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(bytes.All(i => i == fillByte));
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.Contains($"filename={Filename};", _headers["Content-Disposition"]);
    }

    [Fact]
    public async Task GetFileWithRangeTest()
    {
        // arrange
        const int size = 1024;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileUrl(size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

        // assert
        Assert.True(dummyData.Take(512).SequenceEqual(bytes.Take(512)));
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.Equal("512", _headers["Content-Length"]);
        Assert.Equal("bytes 0-511/1024", _headers["Content-Range"]);
        Assert.Equal("bytes", _headers["Accept-Ranges"]);
    }

    [Fact]
    public async Task GetFileWithNoAcceptRangeTest()
    {
        // arrange
        const int size = 1024;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl(Filename, size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.False(_headers.ContainsKey("Accept-Ranges"));
    }

    [Fact]
    public async Task GetSingleByteFileWithNoAcceptRangeTest()
    {
        // arrange
        int size = 1024;
        byte fillByte = 13;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl(Filename, size, fillByte);
        byte[] dummyData = DummyData.GenerateSingleBytes(size, fillByte);

        // act
        await ReadAndGetHeaders(url, bytes, justFirst512Bytes: true);

        // assert
        Assert.True(bytes.All(i => i == fillByte));
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.False(_headers.ContainsKey("Accept-Ranges"));
    }

    [Fact]
    public async Task GetFileWithNameOnRedirectTest()
    {
        // arrange
        const int size = 2048;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithNameOnRedirectUrl(Filename, size);
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // act
        await ReadAndGetHeaders(url, bytes);

        // assert
        Assert.True(dummyData.SequenceEqual(bytes));
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.NotEqual(url, _headers[nameof(WebResponse.ResponseUri)]);
    }

    [Fact]
    public async Task GetFileWithFailureAfterOffsetTest()
    {
        // arrange
        const int size = 10240;
        int failureOffset = size / 2;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithFailureAfterOffset(size, failureOffset);

        // act
        Task GetHeaders() => ReadAndGetHeaders(url, bytes);

        // assert
        await Assert.ThrowsAnyAsync<HttpIOException>(GetHeaders);
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.Equal(0, bytes[size - 1]);
    }

    [Fact]
    public async Task GetFileWithTimeoutAfterOffsetTest()
    {
        // arrange
        const int size = 10240;
        const int timeoutOffset = size / 2;
        byte[] bytes = new byte[size];
        string url = DummyFileHelper.GetFileWithTimeoutAfterOffset(size, timeoutOffset);

        // act
        Task GetHeaders() => ReadAndGetHeaders(url, bytes);

        // assert
        await Assert.ThrowsAnyAsync<HttpIOException>(GetHeaders);
        Assert.Equal(size.ToString(), _headers["Content-Length"]);
        Assert.Equal(_contentType, _headers["Content-Type"]);
        Assert.Equal(0, bytes[size - 1]);
    }

    private async Task ReadAndGetHeaders(string url, byte[] bytes,
        bool justFirst512Bytes = false)
    {
        try
        {
            HttpClient httpClient = new();
            HttpRequestMessage request = new(HttpMethod.Get, url);
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            if (justFirst512Bytes)
                request.Headers.Range = new RangeHeaderValue(0, 511);

            using HttpResponseMessage downloadResponse =
                await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // keep response headers
            _headers = new Dictionary<string, string> {
                { nameof(WebResponse.ResponseUri), downloadResponse.RequestMessage?.RequestUri?.ToString() }
            };
            foreach (var keyValuePair in downloadResponse.Content.Headers)
            {
                _headers[keyValuePair.Key] = keyValuePair.Value.FirstOrDefault();
            }

            foreach (var keyValuePair in downloadResponse.Headers)
            {
                _headers[keyValuePair.Key] = keyValuePair.Value.FirstOrDefault();
            }

            Stream respStream = await downloadResponse.Content.ReadAsStreamAsync();

            // read stream data
            int readCount = 1;
            int offset = 0;
            while (readCount > 0)
            {
                int count = bytes.Length - offset;
                if (count <= 0)
                    break;

                readCount = await respStream.ReadAsync(bytes.AsMemory(offset, count));
                offset += readCount;
            }
        }
        catch (Exception exp)
        {
            await Console.Error.WriteLineAsync(exp.Message);
            Debugger.Break();
            throw;
        }
    }
}