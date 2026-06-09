using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.MSn;
using Pwiz.Data.MsData.Sources;

namespace Pwiz.Data.MsData.Readers;

/// <summary>Identifies and reads MSn-family files (MS1/MS2/BMS1/BMS2/CMS1/CMS2).</summary>
/// <remarks>Port of pwiz::msdata::Reader_MSn / Reader_MS1 / Reader_MS2.</remarks>
public sealed class MSnReaderAdapter : IReader
{
    /// <inheritdoc/>
    public string TypeName => "MSn";

    /// <inheritdoc/>
    /// <remarks>The CvType is the MS2 format CVID by default — cpp returns either
    /// <see cref="CVID.MS_MS1_format"/> or <see cref="CVID.MS_MS2_format"/> depending on which
    /// subclass identifies the file. <see cref="IdentifyMSnType"/> exposes the file-type bucket.</remarks>
    public CVID CvType => CVID.MS_MS2_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".ms1", ".cms1", ".bms1", ".ms2", ".cms2", ".bms2" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        var t = IdentifyMSnType(filename);
        return t switch
        {
            MSnType.Ms1 or MSnType.Bms1 or MSnType.Cms1 => CVID.MS_MS1_format,
            MSnType.Ms2 or MSnType.Bms2 or MSnType.Cms2 => CVID.MS_MS2_format,
            _ => CVID.CVID_Unknown,
        };
    }

    /// <summary>Returns the MSn-family file type implied by <paramref name="filename"/>'s extension.</summary>
    public static MSnType IdentifyMSnType(string filename)
    {
        if (filename.EndsWith(".ms1", StringComparison.OrdinalIgnoreCase)) return MSnType.Ms1;
        if (filename.EndsWith(".cms1", StringComparison.OrdinalIgnoreCase)) return MSnType.Cms1;
        if (filename.EndsWith(".bms1", StringComparison.OrdinalIgnoreCase)) return MSnType.Bms1;
        if (filename.EndsWith(".ms2", StringComparison.OrdinalIgnoreCase)) return MSnType.Ms2;
        if (filename.EndsWith(".cms2", StringComparison.OrdinalIgnoreCase)) return MSnType.Cms2;
        if (filename.EndsWith(".bms2", StringComparison.OrdinalIgnoreCase)) return MSnType.Bms2;
        return MSnType.Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);
        var type = IdentifyMSnType(filename);
        if (type == MSnType.Unknown)
            throw new InvalidDataException($"MSn reader: unable to identify file type for {filename}.");

        using var stream = File.OpenRead(filename);
        new SerializerMSn(type).Read(stream, result);

        // Append the input file as a SourceFile + pwiz software + pwiz_Reader_conversion DP.
        // Then stamp the native-id-format and file-format CVs on the just-added SourceFile —
        // matches cpp Reader_MSn::read (DefaultReaderList.cpp:345-347).
        MSDataFile.FillInCommonMetadata(filename, result);
        var addedSourceFile = result.FileDescription.SourceFiles[^1];
        addedSourceFile.Set(CVID.MS_scan_number_only_nativeID_format);
        addedSourceFile.Set(type.IsMs1() ? CVID.MS_MS1_format : CVID.MS_MS2_format);
    }
}
