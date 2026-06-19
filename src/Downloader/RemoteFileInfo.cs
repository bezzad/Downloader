using System;

namespace Downloader;

/// <summary>
/// Lightweight metadata about a remote file, resolved from the server's response headers
/// (and the URL) <b>without downloading the file content</b>. Produced by
/// <see cref="RemoteFileResolver"/>.
/// </summary>
/// <remarks>
/// This is the same information the downloader resolves internally before it starts a download
/// (filename from <c>Content-Disposition</c> then the URL path, size from <c>Content-Range</c>
/// then <c>Content-Length</c>), exposed so callers can preview a file's name and size — e.g. for
/// queued items waiting on a slot — without spinning up and tearing down a download.
/// </remarks>
public class RemoteFileInfo
{
    /// <summary>
    /// The final address of the file after any redirects were followed. May differ from the URL
    /// originally supplied when the server issued a redirect.
    /// </summary>
    public Uri Address { get; init; }

    /// <summary>
    /// The resolved file name. Taken from the <c>Content-Disposition</c> header when present,
    /// otherwise from the (final) URL path, and as a last resort a generated GUID. Never null or
    /// empty.
    /// </summary>
    public string FileName { get; init; }

    /// <summary>
    /// The total size of the file in bytes, or <c>-1</c> when the server does not advertise a
    /// length (e.g. no <c>Content-Length</c>/<c>Content-Range</c>, or the probe failed).
    /// </summary>
    public long FileSize { get; init; } = -1L;

    /// <summary>
    /// <c>true</c> when the server advertises support for ranged (resumable / multipart) downloads.
    /// </summary>
    public bool SupportsRange { get; init; }
}
