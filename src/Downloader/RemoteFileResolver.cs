using System;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Resolves a remote file's name and size from its URL <b>without starting a download</b>.
///
/// <para>
/// This exposes the exact methodology the downloader uses internally when the caller does not
/// supply a file name: it sends a lightweight header probe (a <c>Range: 0-0</c> GET, following
/// redirects) and reads the file name from the <c>Content-Disposition</c> header, falling back to
/// the URL path and finally to a generated GUID; the size comes from <c>Content-Range</c> then
/// <c>Content-Length</c>. The underlying primitives are <see cref="SocketClient"/>.
/// <c>SetRequestFileNameAsync</c> and <c>GetFileSizeAsync</c>.
/// </para>
///
/// <para>
/// Use this to preview a file's name/size — for example for queued downloads that are waiting on a
/// slot — instead of starting and immediately stopping a real download just to learn its name.
/// Each call owns and disposes its own <see cref="SocketClient"/>; callers that resolve many URLs
/// should add their own concurrency limiting / timeouts around these calls.
/// </para>
/// </summary>
public static class RemoteFileResolver
{
    /// <summary>
    /// Resolves the file name for <paramref name="url"/> using default configuration.
    /// </summary>
    /// <param name="url">The file URL to probe.</param>
    /// <param name="cancelToken">A token to cancel the probe.</param>
    /// <returns>
    /// The resolved file name (from <c>Content-Disposition</c>, else the URL path, else a generated
    /// GUID). This call is resilient: on a network/server error it falls back to the URL-derived
    /// name rather than throwing.
    /// </returns>
    public static Task<string> GetFileNameAsync(string url, CancellationToken cancelToken = default)
    {
        return GetFileNameAsync(url, null, cancelToken);
    }

    /// <summary>
    /// Resolves the file name for <paramref name="url"/> using the supplied configuration
    /// (headers, proxy, credentials, redirect policy, cookies, …).
    /// </summary>
    /// <param name="url">The file URL to probe.</param>
    /// <param name="configuration">
    /// The configuration whose <see cref="DownloadConfiguration.RequestConfiguration"/> shapes the
    /// HTTP probe. When <c>null</c>, a default <see cref="DownloadConfiguration"/> is used.
    /// </param>
    /// <param name="cancelToken">A token to cancel the probe.</param>
    /// <returns>The resolved file name. See the single-argument overload for fallback behavior.</returns>
    public static async Task<string> GetFileNameAsync(string url, DownloadConfiguration configuration,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A URL is required.", nameof(url));

        configuration ??= new DownloadConfiguration();
        using SocketClient client = new(configuration);
        Request request = new(url, configuration.RequestConfiguration);
        return await client.SetRequestFileNameAsync(request, cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the file name, size, range support and final (post-redirect) address for
    /// <paramref name="url"/> using default configuration.
    /// </summary>
    /// <param name="url">The file URL to probe.</param>
    /// <param name="cancelToken">A token to cancel the probe.</param>
    /// <returns>A <see cref="RemoteFileInfo"/> describing the remote file.</returns>
    public static Task<RemoteFileInfo> GetFileInfoAsync(string url, CancellationToken cancelToken = default)
    {
        return GetFileInfoAsync(url, null, cancelToken);
    }

    /// <summary>
    /// Resolves the file name, size, range support and final (post-redirect) address for
    /// <paramref name="url"/> using the supplied configuration.
    /// </summary>
    /// <param name="url">The file URL to probe.</param>
    /// <param name="configuration">
    /// The configuration whose <see cref="DownloadConfiguration.RequestConfiguration"/> shapes the
    /// HTTP probe. When <c>null</c>, a default <see cref="DownloadConfiguration"/> is used.
    /// </param>
    /// <param name="cancelToken">A token to cancel the probe.</param>
    /// <returns>
    /// A <see cref="RemoteFileInfo"/> describing the remote file. The file name is always resolved;
    /// <see cref="RemoteFileInfo.FileSize"/> is <c>-1</c> and
    /// <see cref="RemoteFileInfo.SupportsRange"/> is <c>false</c> when the server does not advertise
    /// a size (the name probe never fails the whole call for that reason).
    /// </returns>
    public static async Task<RemoteFileInfo> GetFileInfoAsync(string url, DownloadConfiguration configuration,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A URL is required.", nameof(url));

        configuration ??= new DownloadConfiguration();
        using SocketClient client = new(configuration);
        Request request = new(url, configuration.RequestConfiguration);

        // Resolve the name first: this triggers (and caches) the header probe and is resilient to
        // network/server errors, falling back to the URL-derived name.
        string fileName = await client.SetRequestFileNameAsync(request, cancelToken).ConfigureAwait(false);

        long fileSize = -1L;
        bool supportsRange = false;
        try
        {
            // Reuses the cached headers from the name probe — no extra round-trip in the happy path.
            supportsRange = await client.IsSupportDownloadInRange(request, cancelToken).ConfigureAwait(false);
            fileSize = await client.GetFileSizeAsync(request, cancelToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Best-effort: a server that won't reveal a size still yields a usable file name above.
        }

        return new RemoteFileInfo {
            Address = request.Address,
            FileName = fileName,
            FileSize = fileSize,
            SupportsRange = supportsRange,
        };
    }
}
