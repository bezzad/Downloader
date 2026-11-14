using System;
using System.IO;

namespace Downloader.Exceptions;

/// <summary>
/// Thrown when writing the downloaded bytes to the storage fails (disk full, drive removed, network
/// folder lost). It is not a network error, so the download is not tried again: it fails at once.
/// The original error is the <see cref="Exception.InnerException"/>.
/// </summary>
public class StorageException : IOException
{
    public StorageException() { }

    public StorageException(string message) : base(message) { }

    public StorageException(string message, Exception innerException)
        : base(message, innerException) { }
}
