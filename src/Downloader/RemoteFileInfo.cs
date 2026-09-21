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

    /// <summary>
    /// The media type the server reported in the <c>Content-Type</c> header (e.g.
    /// <c>video/mp4</c>), verbatim and including any parameters such as <c>; charset=utf-8</c>.
    /// <c>null</c> when the server sent no such header or the probe failed.
    /// </summary>
    /// <remarks>
    /// Read from the same header probe that resolves the name and size, so it costs no extra
    /// request. Useful for callers that need to classify a file whose name carries no usable
    /// extension.
    /// </remarks>
    public string ContentType { get; init; }
}
