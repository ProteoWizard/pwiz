using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads MGF (Mascot Generic Format) files.</summary>
/// <remarks>Port of pwiz::msdata::Reader_MGF.</remarks>
public sealed class MgfReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "MGF";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Mascot_MGF_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mgf" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        if (head is not null)
        {
            // MGF files start with optional comments then "BEGIN IONS" or a MASCOT header.
            foreach (var line in head.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] is '#' or ';' or '!' or '/') continue;
                if (trimmed.StartsWith("BEGIN IONS", StringComparison.OrdinalIgnoreCase)) return CvType;
                break; // first non-comment line isn't BEGIN IONS → not MGF
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

        using var stream = File.OpenRead(filename);
        using var reader = new StreamReader(stream);
        var parsed = new MgfSerializer().Read(reader);
        MzmlReaderAdapter.CopyInto(parsed, result);
    }
}
