using System.IO.Compression;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.MSn;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.MzMlb;
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
    /// Mirrors cpp <c>fillInCommonMetadata</c> (<c>DefaultReaderList.cpp:86</c>): appends a
    /// <see cref="SourceFile"/> entry for the input file, ensures a <c>pwiz_&lt;version&gt;</c>
    /// software entry exists (de-duped by version), and installs a <c>pwiz_Reader_conversion</c>
    /// <see cref="Processing.DataProcessing"/> stamped with <see cref="CVID.MS_Conversion_to_mzML"/>
    /// on both the spectrum and chromatogram list. Called by the format-reader adapters
    /// (mzML, mzMLb, mzXML, MGF, MSn) — vendor readers handle their own provenance.
    /// </summary>
    /// <remarks>
    /// Idempotent on the software-entry side: a second call with the same pwiz version reuses
    /// the existing <see cref="Software"/> entry rather than appending a duplicate.
    /// The DataProcessing assignment uses the <c>Dp</c> setters on
    /// <see cref="Pwiz.Data.MsData.Spectra.SpectrumListSimple"/> /
    /// <see cref="Pwiz.Data.MsData.Spectra.ChromatogramListSimple"/> when the lists are simple;
    /// for non-simple list types (e.g. the lazy <c>SpectrumList_Mzml</c>) the caller passes
    /// the returned DataProcessing to the list constructor — call this helper BEFORE installing
    /// the lazy list.
    /// </remarks>
    /// <returns>The new <c>pwiz_Reader_conversion</c> DataProcessing — pass to lazy-list
    /// constructors so the spectrum/chromatogram list reports it via
    /// <c>defaultDataProcessingRef</c> on write.</returns>
    public static Processing.DataProcessing FillInCommonMetadata(string filename, MSData msd)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        ArgumentNullException.ThrowIfNull(msd);

        // 1. Append a SourceFile entry for the input file. cpp uses BFS_COMPLETE on the parent
        //    path which canonicalizes to an absolute path; Path.GetFullPath does the same on .NET.
        string fileName = Path.GetFileName(filename);
        string parentDir = Path.GetDirectoryName(Path.GetFullPath(filename)) ?? string.Empty;
        // Pwiz cpp emits "file:///" + path. On Windows the path already starts with the drive,
        // so the URI looks like "file:///C:\dev\..." (three slashes followed by drive letter).
        string location = "file:///" + parentDir;
        msd.FileDescription.SourceFiles.Add(new SourceFile(fileName, fileName, location));

        // 2. Default CV list (no-op if already populated — MSData.CVs uses a list, but the
        //    default CV's id is unique so duplicates are detectable. cpp resets unconditionally;
        //    we mirror that for round-trip stability.
        if (msd.CVs.Count == 0)
            msd.CVs.AddRange(MSData.DefaultCVList);

        // 3. pwiz software entry, de-duped by (MS_pwiz, version).
        Software? pwizSoftware = null;
        foreach (var sw in msd.Software)
        {
            if (sw.HasCVParam(CVID.MS_pwiz) && sw.Version == MSData.PwizVersion)
            {
                pwizSoftware = sw;
                break;
            }
        }
        if (pwizSoftware is null)
        {
            pwizSoftware = new Software("pwiz_" + MSData.PwizVersion)
            {
                Version = MSData.PwizVersion,
            };
            pwizSoftware.Set(CVID.MS_pwiz);
            msd.Software.Add(pwizSoftware);
        }

        // 4. pwiz_Reader_conversion DataProcessing with MS_Conversion_to_mzML cvParam.
        var dpPwiz = new Processing.DataProcessing("pwiz_Reader_conversion");
        var pm = new Processing.ProcessingMethod { Order = 0, Software = pwizSoftware };
        pm.Set(CVID.MS_Conversion_to_mzML);
        dpPwiz.ProcessingMethods.Add(pm);
        msd.DataProcessings.Add(dpPwiz);

        // 5. Assign DataProcessing to the spectrum and chromatogram lists. For SpectrumListSimple
        //    / ChromatogramListSimple we set the Dp directly; for other list types (e.g. lazy
        //    SpectrumList_Mzml) the caller passed Dp through the constructor — we can't mutate
        //    the existing list, so leave it. cpp's setDataProcessingPtr is virtual on the base
        //    class; sharp's lazy lists construct with the Dp baked in.
        if (msd.Run.SpectrumList is Pwiz.Data.MsData.Spectra.SpectrumListSimple sls)
            sls.Dp = dpPwiz;
        if (msd.Run.ChromatogramList is Pwiz.Data.MsData.Spectra.ChromatogramListSimple cls)
            cls.Dp = dpPwiz;

        // 6. Fill in run-level ids from the basename when missing. cpp's bfs::basename drops the
        //    extension — Path.GetFileNameWithoutExtension matches that.
        if (string.IsNullOrEmpty(msd.Id) || string.IsNullOrEmpty(msd.Run.Id))
        {
            string basename = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(msd.Id)) msd.Id = basename;
            if (string.IsNullOrEmpty(msd.Run.Id)) msd.Run.Id = basename;
        }

        return dpPwiz;
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

        // mzMLb is path-bound: it's an HDF5 container that needs random-access
        // file I/O. The Stream-shaped Write overload can't serve it, so
        // dispatch here before the stream is opened.
        if (config.Format == WriteFormat.MzMLb)
        {
            new MzMlbWriter(config.EncoderConfig)
            {
                ChunkSize = (ulong)Math.Max(1, config.MzMLbChunkSize),
                CompressionLevel = config.MzMLbCompressionLevel,
                IterationListenerRegistry = ilr,
            }.Write(msd, path);
            return;
        }

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
            case WriteFormat.Ms1:
                new SerializerMSn(MSnType.Ms1) { IterationListenerRegistry = ilr }.Write(msd, output);
                break;
            case WriteFormat.Bms1:
                new SerializerMSn(MSnType.Bms1) { IterationListenerRegistry = ilr }.Write(msd, output);
                break;
            case WriteFormat.Cms1:
                new SerializerMSn(MSnType.Cms1) { IterationListenerRegistry = ilr }.Write(msd, output);
                break;
            case WriteFormat.Ms2:
                new SerializerMSn(MSnType.Ms2) { IterationListenerRegistry = ilr }.Write(msd, output);
                break;
            case WriteFormat.Bms2:
                new SerializerMSn(MSnType.Bms2) { IterationListenerRegistry = ilr }.Write(msd, output);
                break;
            case WriteFormat.Cms2:
                new SerializerMSn(MSnType.Cms2) { IterationListenerRegistry = ilr }.Write(msd, output);
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
