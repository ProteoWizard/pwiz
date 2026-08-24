using System.Globalization;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable CA5350 // SHA-1 is mandated by the mzML spec for file checksums, not a security hash.

namespace Pwiz.Util;

/// <summary>
/// SHA-1 helpers for files and byte buffers. Port of <c>pwiz::util::SHA1Calculator</c>.
/// </summary>
/// <remarks>
/// Used for <c>MS_SHA_1</c> source-file checksums in mzML — not a security primitive. SHA-1 is
/// the algorithm the mzML spec pins, so we use it here even though it's deprecated for crypto.
/// </remarks>
public static class Sha1Calculator
{
    /// <summary>Returns the lowercase hex SHA-1 digest of <paramref name="filename"/>.</summary>
    /// <remarks>
    /// Opens with <see cref="FileShare.ReadWrite"/> so the hasher can coexist with a SQLite or
    /// vendor-native reader that already has the file open (the Bruker .d case).
    /// </remarks>
    public static string HashFile(string filename)
    {
        using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var sha1 = SHA1.Create();
        return ToHex(sha1.ComputeHash(stream));
    }

    /// <summary>Returns the lowercase hex SHA-1 digest of <paramref name="bytes"/>.</summary>
    public static string Hash(ReadOnlySpan<byte> bytes) => ToHex(SHA1.HashData(bytes));

    /// <summary>Returns the lowercase hex SHA-1 digest of <paramref name="text"/> (UTF-8 encoded).</summary>
    public static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));

    private static string ToHex(ReadOnlySpan<byte> hash)
    {
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
