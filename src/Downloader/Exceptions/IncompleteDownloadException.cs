using System;

namespace Downloader.Exceptions;

/// <summary>
/// Thrown when the server ends the response stream before the whole chunk has been received
/// (a premature EOF / dropped connection that does not raise a transport error). Surfacing
/// this prevents a partially downloaded chunk from being silently accepted as complete and
/// left as an unfinished <c>.download</c> file with no error and no retry (issue #231).
/// </summary>
public class IncompleteDownloadException : Exception
{
    public IncompleteDownloadException() { }

    public IncompleteDownloadException(string message) : base(message) { }

    public IncompleteDownloadException(string message, Exception innerException)
        : base(message, innerException) { }
}
