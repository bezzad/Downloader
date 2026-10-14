using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Downloader.Test.IntegrationTests;

[Collection("Sequential")]
public class DynamicRangeSchedulingTest
{
    private const int FileSize = 64 * 1024;
    private const int ChunkCount = 4;
    private const int InitialChunkSize = FileSize / ChunkCount;
    private static readonly byte[] Expected = DummyData.GenerateOrderedBytes(FileSize);

    [Theory]
    [InlineData(0)]
    [InlineData(3 * InitialChunkSize)]
    public async Task RepeatedStealsKeepFinalBytesCorrectForDifferentCompletionOrders(long slowRangeStart)
    {
        using ControlledRangeHandler handler = new(Expected, slowRangeStart, TimeSpan.FromMilliseconds(20));
        using HttpClient client = new(handler);
        using DownloadService service = CreateService(client);

        await using Stream result = await service.DownloadFileTaskAsync("https://example.test/video.bin");

        Assert.True(Expected.AreEqual(result));
        Assert.True(handler.DynamicRanges.Count >= 2,
            $"Expected repeated tail steals, observed {handler.DynamicRanges.Count} dynamic range(s).");
        Assert.NotEqual(slowRangeStart, handler.CompletedRanges.First().Start);
    }

    [Fact]
    public async Task PauseAndResumeAfterSplitKeepsFinalBytesCorrect()
    {
        using ControlledRangeHandler handler = new(Expected, 0, TimeSpan.FromMilliseconds(20));
        using HttpClient client = new(handler);
        using DownloadService service = CreateService(client);

        Task<Stream> download = service.DownloadFileTaskAsync("https://example.test/video.bin");
        await handler.FirstDynamicRange.Task.WaitAsync(TimeSpan.FromSeconds(10));

        service.Pause();
        Assert.True(service.IsPaused);
        await Task.Delay(50);
        service.Resume();

        await using Stream result = await download;
        Assert.True(Expected.AreEqual(result));
    }

    [Fact]
    public async Task CancellationImmediatelyAfterSplitCanResumeToExactBytes()
    {
        using ControlledRangeHandler handler = new(Expected, 0, TimeSpan.FromMilliseconds(20));
        using HttpClient client = new(handler);
        using DownloadService service = CreateService(client);
        bool cancellationReported = false;
        service.DownloadFileCompleted += (_, args) => cancellationReported |= args.Cancelled;

        Task<Stream> firstAttempt = service.DownloadFileTaskAsync("https://example.test/video.bin");
        await handler.FirstDynamicRange.Task.WaitAsync(TimeSpan.FromSeconds(10));
        service.CancelAsync();
        await firstAttempt;

        Assert.True(cancellationReported);
        Assert.Equal(DownloadStatus.Stopped, service.Package.Status);
        DownloadPackage resumable = service.Package;

        await using Stream result = await service.DownloadFileTaskAsync(
            resumable,
            "https://example.test/video.bin");

        Assert.True(Expected.AreEqual(result));
    }

    private static DownloadService CreateService(HttpClient client)
    {
        DownloadConfiguration configuration = new() {
            ChunkCount = ChunkCount,
            ParallelCount = ChunkCount,
            ParallelDownload = true,
            MinimumSizeOfChunking = 0,
            MinimumChunkSize = 2 * 1024,
            BufferBlockSize = 1024,
            BlockTimeout = 5_000,
            CustomHttpClientFactory = () => client
        };
        return new DownloadService(configuration);
    }

    private sealed class ControlledRangeHandler(
        byte[] data,
        long slowRangeStart,
        TimeSpan slowReadDelay) : HttpMessageHandler
    {
        private readonly HashSet<long> _initialStarts = Enumerable.Range(0, ChunkCount)
            .Select(index => (long)index * InitialChunkSize)
            .ToHashSet();

        internal ConcurrentQueue<(long Start, long End)> DynamicRanges { get; } = new();
        internal ConcurrentQueue<(long Start, long End)> CompletedRanges { get; } = new();
        internal TaskCompletionSource FirstDynamicRange { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RangeItemHeaderValue requested = request.Headers.Range?.Ranges.SingleOrDefault();
            long start = requested?.From ?? 0;
            long end = requested?.To ?? data.LongLength - 1;
            bool isProbe = start == 0 && end == 0;

            HttpResponseMessage response = new(HttpStatusCode.PartialContent) {
                RequestMessage = request
            };
            response.Headers.AcceptRanges.Add("bytes");

            if (isProbe)
            {
                response.Content = new ByteArrayContent([data[0]]);
            }
            else
            {
                if (!_initialStarts.Contains(start))
                {
                    DynamicRanges.Enqueue((start, end));
                    FirstDynamicRange.TrySetResult();
                }

                TimeSpan delay = start == slowRangeStart ? slowReadDelay : TimeSpan.Zero;
                response.Content = new StreamContent(new ControlledRangeStream(
                    data,
                    start,
                    end,
                    delay,
                    () => CompletedRanges.Enqueue((start, end))));
            }

            response.Content.Headers.ContentLength = end - start + 1;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, data.LongLength);
            return Task.FromResult(response);
        }
    }

    private sealed class ControlledRangeStream : Stream
    {
        private readonly byte[] _data;
        private readonly long _start;
        private readonly long _end;
        private readonly TimeSpan _readDelay;
        private readonly Action _completedCallback;
        private long _position;
        private int _completed;

        internal ControlledRangeStream(
            byte[] data,
            long start,
            long end,
            TimeSpan readDelay,
            Action completed)
        {
            _data = data;
            _start = start;
            _end = end;
            _readDelay = readDelay;
            _completedCallback = completed;
            _position = start;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _end - _start + 1;
        public override long Position
        {
            get => _position - _start;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_readDelay > TimeSpan.Zero)
                Thread.Sleep(_readDelay);
            return CopyTo(buffer.AsSpan(offset, count));
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_readDelay > TimeSpan.Zero)
                await Task.Delay(_readDelay, cancellationToken);
            return CopyTo(buffer.Span);
        }

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _completed, 1) == 0)
                _completedCallback();
            base.Dispose(disposing);
        }

        private int CopyTo(Span<byte> destination)
        {
            int count = (int)Math.Min(destination.Length, _end - _position + 1);
            if (count <= 0)
                return 0;

            _data.AsSpan((int)_position, count).CopyTo(destination);
            _position += count;
            return count;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}