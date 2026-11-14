using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// The temp files (<c>name.download</c>) that a download in this process is writing now. Two downloads
/// to the same file (the same link added twice) must not write one temp file: the second one would
/// delete the first one's finished file and leave no file at all.
/// </summary>
internal static class TempFileClaims
{
    // The value is the cancel token of the download that holds the file: it tells a stopping holder
    // (the file is free in a moment) from a running one.
    private static readonly Dictionary<string, CancellationToken> Claimed = new(
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>Claims the file for one download. False when another download has it.</summary>
    public static bool TryClaim(string path, CancellationToken owner)
    {
        lock (Claimed)
            return Claimed.TryAdd(Path.GetFullPath(path), owner);
    }

    public static bool IsClaimed(string path)
    {
        lock (Claimed)
            return Claimed.ContainsKey(Path.GetFullPath(path));
    }

    public static void Release(string path)
    {
        lock (Claimed)
            Claimed.Remove(Path.GetFullPath(path));
    }

    /// <summary>
    /// Waits while a cancelled download still holds the file. A user who clicks Stop and then Start
    /// must get the same file back, not an error or a second name.
    /// </summary>
    public static async Task WaitWhileHolderStopsAsync(string path, CancellationToken cancel)
    {
        while (IsHolderStopping(path))
            await Task.Delay(20, cancel).ConfigureAwait(false);
    }

    private static bool IsHolderStopping(string path)
    {
        lock (Claimed)
            return Claimed.TryGetValue(Path.GetFullPath(path), out CancellationToken owner) &&
                   owner.IsCancellationRequested;
    }
}
