using System;
using System.Collections.Generic;
using System.IO;

namespace Downloader;

/// <summary>
/// The temp files (<c>name.download</c>) that a download in this process is writing now. Two downloads
/// to the same file (the same link added twice) must not write one temp file: the second one would
/// delete the first one's finished file and leave no file at all.
/// </summary>
internal static class TempFileClaims
{
    private static readonly HashSet<string> Claimed = new(
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Claims the file for one download. False when another download has it.</summary>
    public static bool TryClaim(string path)
    {
        lock (Claimed)
            return Claimed.Add(Path.GetFullPath(path));
    }

    public static bool IsClaimed(string path)
    {
        lock (Claimed)
            return Claimed.Contains(Path.GetFullPath(path));
    }

    public static void Release(string path)
    {
        lock (Claimed)
            Claimed.Remove(Path.GetFullPath(path));
    }
}
