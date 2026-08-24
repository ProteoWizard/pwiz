using System;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mz5;

namespace Pwiz.Data.MsData.Readers;

/// <summary>
/// Identifies and reads mz5 files (HDF5-backed pwiz-native binary mzML
/// equivalent from 2011). Port of pwiz cpp's <c>Reader_mz5</c>.
/// </summary>
/// <remarks>
/// mz5 was largely superseded by mzMLb (also ported); kept for legacy file
/// support. The identify path is fully functional; the full <c>Read</c> path
/// requires the <c>ReferenceRead_mz5</c> walker which is still WIP — see
/// <c>src/MsData/Mz5/README.md</c> for the remaining work.
/// </remarks>
public sealed class Mz5ReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mz5";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mz5_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mz5" };

    // Same HDF5 magic as mzMLb (every HDF5 file starts with these bytes); the
    // distinguishing check is whether the file has an mz5-shaped FileInformation
    // dataset (we don't open mz5 files that lack that, which Reader_MzMlb's
    // sibling identify already does for the "mzML" dataset).
    private static readonly byte[] Hdf5Magic = { 0x89, 0x48, 0x44, 0x46, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        if (head is not null && head.Length >= Hdf5Magic.Length)
        {
            bool magic = true;
            for (int i = 0; i < Hdf5Magic.Length; i++)
                if ((byte)head[i] != Hdf5Magic[i]) { magic = false; break; }
            if (magic)
            {
                // HDF5 file: open under mz5 rules to see if it has a
                // FileInformation dataset. Mz5Connection's ctor throws if not.
                try
                {
                    using var _ = new Mz5Connection(filename);
                    return CvType;
                }
                catch
                {
                    return CVID.CVID_Unknown;
                }
            }
        }

        foreach (var ext in FileExtensions)
            if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return CvType;

        return CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        // Open and walk the document-level metadata tables. The connection
        // is refcounted: Mz5SpectrumList and Mz5ChromatogramList each AddRef
        // it during construction (their lazy reads need it alive), then call
        // Dispose() from their own DisposeCore. After Fill returns we drop
        // our initial hold — the connection survives as long as at least one
        // list still references it.
        var conn = new Mz5Connection(filename);
        try
        {
            new Mz5ReferenceRead(result).Fill(conn);
        }
        catch
        {
            conn.Dispose();
            throw;
        }
        conn.Dispose(); // release adapter's initial hold; lists keep it alive
    }
}
