using System;

namespace Downloader.Exceptions;

/// <summary>
/// Thrown when a server does not answer a range request with the asked part of the file
/// (for example it sends <c>200</c> and the whole file). Writing that answer would break the file.
/// </summary>
public class InvalidRangeResponseException(string message) : Exception(message);
