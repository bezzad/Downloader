using Downloader.Serializer;
using System.Text;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Auto-resume must only continue a .download file that still matches the server file, and must
/// read its saved state even when an older, longer state is still on disk behind it.
/// </summary>
public class SaferResumeTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 4096;
    private const byte OldByte = 1;
    private const byte NewByte = 2;

    private static DownloadConfiguration NewConfig() => new() {
        ChunkCount = 1,
        EnableAutoResumeDownload = true,
        FileExistPolicy = FileExistPolicy.Delete
    };

    private static string NewFile() =>
        Path.Combine(Path.GetTempPath(), "safer-resume-" + Guid.NewGuid().ToString("N") + ".bin");

    /// <summary>JSON of a half-done single chunk, with an optional ETag (as the server sends it, in quotes).</summary>
    private static string HalfDoneJson(string etag)
    {
        PackageInfo info = new() {
            TotalFileSize = Size,
            Chunks = [new Chunk(0, Size - 1) { Position = Size / 2 }],
            ETag = etag is null ? null : $"\"{etag}\""
        };
        return Encoding.UTF8.GetString(new JsonBinarySerializer().Serialize(info));
    }

    private static async Task WriteDownloadingFile(string file, byte[] metadata)
    {
        byte[] content = new byte[Size + metadata.Length];
        Array.Fill(content, OldByte, 0, Size);
        metadata.CopyTo(content, Size);
        await File.WriteAllBytesAsync(file + ".download", content);
    }

    private async Task<byte[]> Download(string url, string file)
    {
        await using DownloadService service = new(NewConfig(), LogFactory);
        await service.DownloadFileTaskAsync(url, file);
        Assert.Equal(DownloadStatus.Completed, service.Status);
        return await File.ReadAllBytesAsync(file);
    }

    [Fact(Timeout = 60_000)]
    public async Task ChangedServerFileWithTheSameSizeStartsAgain()
    {
        // arrange
        string file = NewFile();
        await WriteDownloadingFile(file, Encoding.UTF8.GetBytes(HalfDoneJson("old")));
        string url = DummyFileHelper.GetFileWithETagUrl(Size, "new", NewByte);

        try
        {
            // act
            byte[] bytes = await Download(url, file);

            // assert: no old bytes are mixed in
            Assert.All(bytes, b => Assert.Equal(NewByte, b));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task SameETagResumes()
    {
        // arrange
        string file = NewFile();
        await WriteDownloadingFile(file, Encoding.UTF8.GetBytes(HalfDoneJson("same")));
        string url = DummyFileHelper.GetFileWithETagUrl(Size, "same", NewByte);

        try
        {
            // act
            byte[] bytes = await Download(url, file);

            // assert: the first half came from the disk, so the download was resumed
            Assert.All(bytes[..(Size / 2)], b => Assert.Equal(OldByte, b));
            Assert.All(bytes[(Size / 2)..], b => Assert.Equal(NewByte, b));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task OldMetadataWithoutETagStillResumes()
    {
        // arrange
        string file = NewFile();
        await WriteDownloadingFile(file, Encoding.UTF8.GetBytes(HalfDoneJson(null)));
        string url = DummyFileHelper.GetFileWithETagUrl(Size, "any", NewByte);

        try
        {
            // act
            byte[] bytes = await Download(url, file);

            // assert
            Assert.All(bytes[..(Size / 2)], b => Assert.Equal(OldByte, b));
            Assert.All(bytes[(Size / 2)..], b => Assert.Equal(NewByte, b));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task ShorterMetadataOverALongerOneIsRead()
    {
        // arrange: a long old state, then a shorter new state written over its start
        string file = NewFile();
        byte[] longState = Encoding.UTF8.GetBytes(HalfDoneJson("same") + new string(' ', 500) + "junk");
        byte[] shortState = ResumeMetadata.Pack(Encoding.UTF8.GetBytes(HalfDoneJson("same")));
        shortState.CopyTo(longState, 0);
        await WriteDownloadingFile(file, longState);
        string url = DummyFileHelper.GetFileWithETagUrl(Size, "same", NewByte);

        try
        {
            // act
            byte[] bytes = await Download(url, file);

            // assert: resumed from the saved position, not started again
            Assert.All(bytes[..(Size / 2)], b => Assert.Equal(OldByte, b));
            Assert.All(bytes[(Size / 2)..], b => Assert.Equal(NewByte, b));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
