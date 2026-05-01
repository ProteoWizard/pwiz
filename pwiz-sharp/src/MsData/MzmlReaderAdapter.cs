using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mzml;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads mzML files.</summary>
/// <remarks>Port of pwiz::msdata::Reader_mzML.</remarks>
public sealed class MzmlReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "mzML";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_mzML_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".mzML", ".mzml", ".mzML.gz" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);

        // Sniff the content if we have it. mzML can be wrapped as indexedmzML or bare.
        if (head is not null)
        {
            if (head.Contains("<mzML", StringComparison.Ordinal) ||
                head.Contains("<indexedmzML", StringComparison.Ordinal))
                return CvType;
        }

        // Fall back to extension match.
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
        var parsed = new MzmlReader().Read(stream);
        CopyInto(parsed, result);
    }

    internal static void CopyInto(MSData source, MSData dest)
    {
        dest.Accession = source.Accession;
        dest.Id = source.Id;
        dest.Version = source.Version;
        dest.CVs.AddRange(source.CVs);
        dest.FileDescription = source.FileDescription;
        dest.ParamGroups.AddRange(source.ParamGroups);
        dest.Samples.AddRange(source.Samples);
        dest.Software.AddRange(source.Software);
        dest.ScanSettings.AddRange(source.ScanSettings);
        dest.InstrumentConfigurations.AddRange(source.InstrumentConfigurations);
        dest.DataProcessings.AddRange(source.DataProcessings);
        dest.Run = source.Run;
    }
}
