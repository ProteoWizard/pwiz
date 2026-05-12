using System.IO.Compression;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzXml;
using Pwiz.Data.MsData.Sources;
using Pwiz.Util;
using Pwiz.Util.Misc;

namespace Pwiz.Data.MsData;

/// <summary>
/// File-level helpers for <see cref="MSData"/>. Port of the standalone functions in
/// <c>pwiz/data/msdata/MSDataFile.cpp</c> — checksum computation + format-dispatching
/// <see cref="Write(MSData, string, WriteConfig, IterationListenerRegistry?)"/>.
/// </summary>
public static class MSDataFile
{
    /// <summary>
    /// Computes and sets the <see cref="CVID.MS_SHA_1"/> cvParam on <paramref name="sourceFile"/>
    /// by hashing the file at <c>Location + "/" + Name</c>. Silently returns if the location is
    /// not a <c>file://</c> URI, the file is missing, or the cvParam is already set.
    /// </summary>
    public static void CalculateSourceFileSha1(SourceFile sourceFile)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        if (sourceFile.HasCVParam(CVID.MS_SHA_1)) return;

        const string uriPrefix = "file://";
        string location = sourceFile.Location;
        if (!location.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase)) return;
        location = location[uriPrefix.Length..].TrimStart('/');
        string path = Path.Combine(location, sourceFile.Name);

        if (!File.Exists(path)) return;
        sourceFile.Set(CVID.MS_SHA_1, Sha1Calculator.HashFile(path));
    }

    /// <summary>Computes SHA-1 for every <see cref="SourceFile"/> in <paramref name="msd"/>.</summary>
    public static void CalculateSha1Checksums(MSData msd)
    {
        ArgumentNullException.ThrowIfNull(msd);
        foreach (var sf in msd.FileDescription.SourceFiles)
            CalculateSourceFileSha1(sf);
    }

    /// <summary>
    /// Writes <paramref name="msd"/> to <paramref name="path"/> in the format selected by
    /// <paramref name="config"/>. Mirrors cpp <c>MSDataFile::write(MSData&amp;, path, WriteConfig, ...)</c>.
    /// Throws <see cref="NotImplementedException"/> for formats that don't have a pwiz-sharp
    /// writer yet (mz5 / mzMLb / text / MS1 / CMS1 / MS2 / CMS2).
    /// </summary>
    /// <param name="msd">The document to write.</param>
    /// <param name="path">Output file path; <see cref="WriteConfig.Gzip"/> appends .gz handling.</param>
    /// <param name="config">Writer config. <see cref="WriteConfig.Format"/> selects the dispatch.</param>
    /// <param name="ilr">Optional progress registry forwarded to the writer.</param>
    public static void Write(MSData msd, string path, WriteConfig config, IterationListenerRegistry? ilr = null)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(config);

        using Stream output = OpenOutputStream(path, config.Gzip);
        Write(msd, output, config, ilr);
    }

    /// <summary>Stream-shaped <see cref="Write(MSData, string, WriteConfig, IterationListenerRegistry?)"/>.
    /// Caller owns <paramref name="output"/>; gzip wrapping (when requested) is the caller's responsibility.</summary>
    public static void Write(MSData msd, Stream output, WriteConfig config, IterationListenerRegistry? ilr = null)
    {
        ArgumentNullException.ThrowIfNull(msd);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(config);

        switch (config.Format)
        {
            case WriteFormat.Mzml:
                new MzmlWriter(config.EncoderConfig)
                {
                    Indexed = config.Indexed,
                    IterationListenerRegistry = ilr,
                }.Write(msd, output);
                break;
            case WriteFormat.MzXml:
                new MzxmlWriter(config.EncoderConfig)
                {
                    Indexed = config.Indexed,
                    IterationListenerRegistry = ilr,
                }.Write(msd, output);
                break;
            case WriteFormat.Mgf:
                using (var tw = new StreamWriter(output, leaveOpen: true))
                    new MgfSerializer { IterationListenerRegistry = ilr }.Write(msd, tw);
                break;
            default:
                throw new NotImplementedException(
                    $"pwiz-sharp doesn't yet implement a writer for {config.Format}.");
        }
    }

    private static Stream OpenOutputStream(string path, bool gzipped)
    {
        var fs = File.Create(path);
        return gzipped ? new GZipStream(fs, CompressionLevel.Optimal, leaveOpen: false) : (Stream)fs;
    }
}
