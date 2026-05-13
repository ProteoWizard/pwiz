using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzMlb;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzMLb files (mzML wrapped in HDF5).</summary>
/// <remarks>
/// Port of <c>pwiz::msdata::Reader_mzMLb</c>. An mzMLb file is an HDF5
/// container holding the mzML XML in a chunked <c>mzML</c> dataset plus one
/// named HDF5 dataset per binary array (m/z, intensity, time, etc.). We
/// recognize it by the leading HDF5 magic bytes (<c>89 48 44 46 0D 0A 1A 0A</c>)
/// — same as cpp's identify path.
/// </remarks>
public sealed class MzMlbReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzMLb";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mzMLb_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzMLb", ".mzmlb" };

    // HDF5 file signature. Every HDF5 file starts with these 8 bytes regardless
    // of compression / chunking settings; the magic is HDF5's format-version
    // hook. An mzMLb file is always an HDF5 file, but not every HDF5 file is
    // mzMLb — the secondary check is the presence of a named "mzML" dataset
    // (validated inside MzMlbConnection's identifyOnly ctor).
    private static readonly byte[] Hdf5Magic = { 0x89, 0x48, 0x44, 0x46, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        if (head is not null && head.Length >= Hdf5Magic.Length)
        {
            bool magicMatches = true;
            for (int i = 0; i < Hdf5Magic.Length; i++)
            {
                if ((byte)head[i] != Hdf5Magic[i]) { magicMatches = false; break; }
            }
            if (magicMatches)
            {
                // Open in identify mode to confirm there's an actual "mzML"
                // dataset (else this is just a generic HDF5 file). Swallow
                // failures — the cpp identify path is silent on non-mzMLb.
                try
                {
                    using var _ = new MzMlbConnection(filename, identifyOnly: true);
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

        // The MzMlbConnection owns the HDF5 handles for the file (the mzML
        // dataset + every binary dataset we touch during this read). It has
        // to outlive the MzmlReader.Read call because the reader pulls
        // external binary arrays through it. We dispose at the end.
        using var conn = MzMlbConnection.OpenForRead(filename);
        using var stream = conn.OpenMzMlStream();
        var reader = new MzmlReader { ExternalBinarySource = conn };
        var parsed = reader.Read(stream);
        MzmlReaderAdapter.CopyInto(parsed, result);
    }
}
