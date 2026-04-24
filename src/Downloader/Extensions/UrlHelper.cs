using System;
using System.Text;

namespace Downloader.Extensions;

/// <summary>
/// Normalizes URL strings before they reach <see cref="Uri"/> or the HTTP stack.
/// Motivating case (issue #223): download URLs whose path contains characters
/// that are illegal there per RFC 3986 — most commonly square brackets and
/// unencoded spaces in release-group filenames such as
/// "[SubGroup] Show - 01 [1080p].mkv". Windows' URI parser is permissive and
/// tends to hide the problem; Linux' parser is stricter and rejects or
/// mishandles the URL, causing the downloader to 404 or fail outright.
/// </summary>
internal static class UrlHelper
{
    private const string HexChars = "0123456789ABCDEF";

    /// <summary>
    /// Returns the given URL with any path-segment characters that are not
    /// valid pchar per RFC 3986 percent-encoded as UTF-8. Idempotent: existing
    /// valid <c>%XX</c> triplets are passed through unchanged, so encoding an
    /// already-encoded URL yields the same string. Only the path is modified —
    /// scheme, userinfo, host (including IPv6 literal brackets like
    /// <c>[::1]</c>), port, query, and fragment are preserved byte-for-byte.
    /// Relative URLs (no scheme) and empty input are returned unchanged.
    /// </summary>
    public static string EnsurePathEncoded(string address)
    {
        if (string.IsNullOrEmpty(address))
            return address;

        // Locate component boundaries manually. We can't use Uri here — Uri is
        // exactly what rejects these URLs on Linux.
        int schemeEnd = address.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
            return address;

        int authorityStart = schemeEnd + 3;

        // Authority ends at the first '/', '?', or '#' after it starts.
        int authorityEnd = address.Length;
        for (int i = authorityStart; i < address.Length; i++)
        {
            char c = address[i];
            if (c == '/' || c == '?' || c == '#')
            {
                authorityEnd = i;
                break;
            }
        }

        // No path to encode if authority ran to the end or the next component
        // isn't a path (i.e. '?' or '#' directly follow authority).
        if (authorityEnd == address.Length || address[authorityEnd] != '/')
            return address;

        int pathStart = authorityEnd;

        // Path ends at the first '?' or '#', or at end of string.
        int pathEnd = address.Length;
        for (int i = pathStart; i < address.Length; i++)
        {
            char c = address[i];
            if (c == '?' || c == '#')
            {
                pathEnd = i;
                break;
            }
        }

        string path = address.Substring(pathStart, pathEnd - pathStart);
        string encoded = EncodePath(path);
        if (ReferenceEquals(encoded, path))
            return address;

        return address.Substring(0, pathStart) + encoded + address.Substring(pathEnd);
    }

    private static string EncodePath(string path)
    {
        // Fast path: if every character is already safe (or a valid %XX
        // triplet), return the input unchanged and avoid allocating.
        if (!NeedsEncoding(path))
            return path;

        var sb = new StringBuilder(path.Length + 16);
        int i = 0;
        while (i < path.Length)
        {
            char c = path[i];

            if (c == '/' || IsSafeInPathSegment(c))
            {
                sb.Append(c);
                i++;
                continue;
            }

            // Pass through already-valid percent-encoded triplets (idempotency).
            // Normalize hex digits to uppercase so the output is canonical.
            if (c == '%' && i + 2 < path.Length && IsHex(path[i + 1]) && IsHex(path[i + 2]))
            {
                sb.Append('%');
                sb.Append(ToUpperHex(path[i + 1]));
                sb.Append(ToUpperHex(path[i + 2]));
                i += 3;
                continue;
            }

            // Encode as UTF-8, preserving surrogate pairs for astral code points.
            string piece;
            if (char.IsHighSurrogate(c) && i + 1 < path.Length && char.IsLowSurrogate(path[i + 1]))
            {
                piece = path.Substring(i, 2);
                i += 2;
            }
            else
            {
                piece = c.ToString();
                i++;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(piece);
            for (int b = 0; b < bytes.Length; b++)
            {
                sb.Append('%');
                sb.Append(HexChars[bytes[b] >> 4]);
                sb.Append(HexChars[bytes[b] & 0xF]);
            }
        }
        return sb.ToString();
    }

    private static bool NeedsEncoding(string path)
    {
        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            if (c == '/' || IsSafeInPathSegment(c))
                continue;
            if (c == '%' && i + 2 < path.Length && IsHex(path[i + 1]) && IsHex(path[i + 2]))
            {
                // Also require canonical (uppercase) form; lowercase triplets
                // are valid but we rewrite them for consistency.
                if (IsUpperHex(path[i + 1]) && IsUpperHex(path[i + 2]))
                {
                    i += 2;
                    continue;
                }
            }
            return true;
        }
        return false;
    }

    private static bool IsSafeInPathSegment(char c)
    {
        // RFC 3986 pchar = unreserved / pct-encoded / sub-delims / ":" / "@"
        //   unreserved  = ALPHA / DIGIT / "-" / "." / "_" / "~"
        //   sub-delims  = "!" / "$" / "&" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="
        if ((uint)(c - 'A') <= 'Z' - 'A') return true;
        if ((uint)(c - 'a') <= 'z' - 'a') return true;
        if ((uint)(c - '0') <= '9' - '0') return true;
        return c switch
        {
            '-' or '.' or '_' or '~' => true,
            '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=' => true,
            ':' or '@' => true,
            _ => false,
        };
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');

    private static bool IsUpperHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

    private static char ToUpperHex(char c) =>
        (c >= 'a' && c <= 'f') ? (char)(c - 32) : c;
}
