using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Waters;

/// <summary>
/// <see cref="IReader"/> for Waters MassLynx <c>.raw</c> directories. Phase 1 of the port:
/// non-IMS, non-DDA, non-lockmass paths only. Mirrors pwiz C++ <c>Reader_Waters</c>.
/// </summary>
public sealed class Reader_Waters : IReader
{
    /// <inheritdoc/>
    public string TypeName => "Waters";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_Waters_raw_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".raw" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        // Waters .raw is a directory containing one or more _FUNCnnn.DAT files. pwiz C++ uses
        // exactly this glob to identify Waters; we mirror it.
        if (!Directory.Exists(filename)) return CVID.CVID_Unknown;
        try
        {
            using var e = Directory.EnumerateFiles(filename, "_FUNC*.DAT").GetEnumerator();
            return e.MoveNext() ? CvType : CVID.CVID_Unknown;
        }
        catch
        {
            return CVID.CVID_Unknown;
        }
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);

        int preferOnlyMsLevel = config?.PreferOnlyMsLevel ?? 0;
        bool srmAsSpectra = false; // Phase 1 doesn't expose this through ReaderConfig.
        bool ddaProcessing = config?.DdaProcessing ?? false;

        // Waters paths can't contain unicode (the SDK rejects them), but we let the OS open
        // any path we can find on disk — mirror the pwiz C++ short-path workaround only when
        // we hit failures down the road.
        string fullPath = Path.GetFullPath(filename);
        if (!Directory.Exists(fullPath))
            throw new FileNotFoundException("Waters .raw directory not found: " + fullPath, fullPath);

        var data = new WatersRawFile(fullPath);
        try
        {
            ReadImpl(result, data, fullPath, preferOnlyMsLevel, srmAsSpectra, ddaProcessing);
        }
        catch
        {
            data.Dispose();
            throw;
        }
    }

    private static void ReadImpl(MSData result, WatersRawFile data, string analysisDir,
        int preferOnlyMsLevel, bool srmAsSpectra, bool ddaProcessing)
    {
        // Identifier is the directory name minus the .raw extension (matches pwiz C++:
        // bfs::basename(p) drops the trailing extension component).
        string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(analysisDir));
        result.Id = string.IsNullOrEmpty(Path.GetExtension(folderName))
            ? folderName
            : Path.GetFileNameWithoutExtension(folderName);

        AddSourceFiles(result, analysisDir);
        AddFileContent(result, data, preferOnlyMsLevel);

        // Software entries: MassLynx (the SDK that produced the data) + pwiz_Reader_Waters
        // (the conversion tool). Versions match pwiz C++ exactly so the diff stays clean.
        var swMassLynx = new Software { Id = "MassLynx", Version = "4.1" };
        swMassLynx.Set(CVID.MS_MassLynx);
        result.Software.Add(swMassLynx);

        var swPwiz = new Software { Id = "pwiz_Reader_Waters", Version = "1.0" };
        swPwiz.Set(CVID.MS_pwiz);
        result.Software.Add(swPwiz);

        var dpReader = new DataProcessing("pwiz_Reader_Waters_conversion");
        var pmConvert = new ProcessingMethod { Order = 0, Software = swPwiz };
        pmConvert.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmConvert);
        result.DataProcessings.Add(dpReader);

        var ic = new InstrumentConfiguration("IC");
        ic.Set(CVID.MS_Waters_instrument_model);
        result.InstrumentConfigurations.Add(ic);

        result.Run.Id = result.Id;
        result.Run.DefaultSourceFile = result.FileDescription.SourceFiles.FirstOrDefault();
        result.Run.DefaultInstrumentConfiguration = ic;
        result.Run.StartTimeStamp = ConvertHeaderTimestamp(data);

        var spectrumList = new SpectrumList_Waters(data, owns: true, preferOnlyMsLevel, srmAsSpectra, ddaProcessing)
        { Dp = dpReader };
        result.Run.SpectrumList = spectrumList;

        var chromatogramList = new ChromatogramList_Waters(data, preferOnlyMsLevel)
        { Dp = dpReader };
        result.Run.ChromatogramList = chromatogramList;
    }

    private static void AddSourceFiles(MSData result, string analysisDir)
    {
        // pwiz C++ behavior: list every _FUNCnnn.DAT as a source file with Waters native id +
        // raw format CV; everything else (including header.txt, lmgt.inf, ...) is listed as
        // "no native id format" — except lmgt.inf (lockmass garbage trap log) which is skipped.
        var location = "file://" + analysisDir;
        var funcFiles = Directory.GetFiles(analysisDir, "_FUNC*.DAT").OrderBy(p => p).ToArray();

        foreach (var path in funcFiles)
        {
            string fname = Path.GetFileName(path);
            var sf = new SourceFile { Id = fname, Name = fname, Location = location };
            sf.Set(CVID.MS_Waters_nativeID_format);
            sf.Set(CVID.MS_Waters_raw_format);
            result.FileDescription.SourceFiles.Add(sf);
        }

        foreach (var path in Directory.EnumerateFiles(analysisDir))
        {
            string fname = Path.GetFileName(path);
            // Skip files we already listed as function sources; skip lmgt.inf (lockmass log).
            if (fname.StartsWith("_FUNC", StringComparison.OrdinalIgnoreCase)
                && fname.EndsWith(".DAT", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(fname, "lmgt.inf", StringComparison.OrdinalIgnoreCase))
                continue;

            var sf = new SourceFile { Id = fname, Name = fname, Location = location };
            sf.Set(CVID.MS_no_nativeID_format);
            result.FileDescription.SourceFiles.Add(sf);
        }
    }

    private static void AddFileContent(MSData result, WatersRawFile data, int preferOnlyMsLevel)
    {
        // The fileContent block lists which spectrumType CVs the spectrum list will produce.
        // pwiz C++ deduplicates by adding each translated spectrum type once.
        var seen = new HashSet<CVID>();
        foreach (int function in data.FunctionIndices)
        {
            int rawType;
            try { rawType = data.GetFunctionType(function); }
            catch { continue; }
            var ft = WatersDetail.FromMassLynxFunctionType(rawType);
            if (!WatersDetail.TranslateFunctionType(ft, out int msLevel, out CVID spectrumType)) continue;
            if (preferOnlyMsLevel > 0 && msLevel != preferOnlyMsLevel) continue;
            if (seen.Add(spectrumType))
                result.FileDescription.FileContent.Set(spectrumType);
        }
    }

    private static string ConvertHeaderTimestamp(WatersRawFile data)
    {
        // pwiz C++ format: "%d-%b-%Y %H:%M:%S" e.g. "04-Dec-2009 13:45:00". The MassLynx header
        // reports a wall-clock acquisition time without timezone info; pwiz C++ encodes it
        // verbatim as a UTC-marked ISO timestamp (no zone conversion). We mirror that exactly.
        string dateStr = data.GetHeaderProp("Acquired Date");
        if (string.IsNullOrEmpty(dateStr)) return string.Empty;
        string timeStr = data.GetHeaderProp("Acquired Time");
        string combined = string.IsNullOrEmpty(timeStr) ? dateStr : dateStr + " " + timeStr;

        var formats = new[]
        {
            "d-MMM-yyyy H:mm:ss",
            "dd-MMM-yyyy HH:mm:ss",
            "d-MMM-yyyy H:mm",
            "dd-MMM-yyyy HH:mm",
            "d-MMM-yyyy",
        };
        if (DateTime.TryParseExact(combined, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        return string.Empty;
    }
}
