using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
#if !NO_VENDOR_SUPPORT
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Sources;
#endif

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="IReader"/> for Sciex / ABI WIFF files. Identifies <c>.wiff</c> and <c>.wiff2</c>;
/// <c>AbstractWiffFile.Open</c> dispatches to the legacy AnalystDataProvider wrapper for
/// <c>.wiff</c> or to the side-by-side wiff2 plugin for <c>.wiff2</c>, then a single
/// metadata-fill path emits the same shape of MSData for both.
/// </summary>
/// <remarks>
/// Initial port covers Q-TOF / TripleTOF / TripleQuad data: full-scan MS1 / MSn (Product),
/// SIM/MRM as chromatograms (or as spectra when the matching config flag is set). Skipped:
/// multi-sample wiffs, instrument-model translation table beyond a generic
/// <c>MS_SCIEX_instrument_model</c> term.
/// </remarks>
public sealed class Reader_Sciex : IReader, IMultiSampleReader
{
    /// <inheritdoc/>
    public string TypeName => "Sciex";

    /// <inheritdoc/>
    public CVID CvType => CVID.MS_ABI_WIFF_format;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".wiff", ".wiff2" };

    /// <inheritdoc/>
    public CVID Identify(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        return IsWiffFile(filename) && File.Exists(filename) ? CvType : CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>cpp splits these across Reader_ABI ("Sciex WIFF") and Reader_ABI_WIFF2
    /// ("Sciex WIFF2"); this reader covers both, so it reports which one the file is. Callers
    /// match the answer by equality - Skyline's DataSourceUtil is one - so the folded name
    /// "Sciex" would read as an unknown format.</remarks>
    public string IdentifyType(string filename, string? head)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (Identify(filename, head) == CVID.CVID_Unknown)
            return string.Empty;
        return filename.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase)
            ? "Sciex WIFF2"
            : "Sciex WIFF";
    }

    /// <inheritdoc/>
    public string[] EnumerateSampleNames(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Sciex WIFF sample enumeration requires the vendor SDK.");
#else
        // .wiff and .wiff2 do NOT share a sample-info API: WiffFile.EnumerateSampleNames goes
        // through AnalystWiffDataProvider, which cannot open a .wiff2 at all. Sending .wiff2
        // here made this report a single sample for a 12-sample file, so msconvert-sharp
        // converted only the first one where msconvert wrote all 12. Dispatch the same way
        // AbstractWiffFile.Open does; the .wiff path stays on the cheap no-sample-open route.
        if (filename.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase))
        {
            using var wiff2 = AbstractWiffFile.Open(filename);
            return wiff2.AllSampleNames;
        }
        return WiffFile.EnumerateSampleNames(filename);
#endif
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);
        if (!IsWiffFile(filename))
            throw new InvalidDataException($"Not a WIFF file: {filename}");
        if (!File.Exists(filename))
            throw new FileNotFoundException("WIFF file not found", filename);

#if NO_VENDOR_SUPPORT
        throw new VendorSupportNotEnabledException(
            "Sciex WIFF reading requires the vendor SDK. Rebuild pwiz-sharp with --i-agree-to-the-vendor-licenses to enable.");
#else
        result.CVs.AddRange(MSData.DefaultCVList);

        // Multi-sample WIFF1 inputs (Enolase_repeats_AQv1.4.2.wiff has 10 samples) need
        // the caller to select which sample to load. cpp ports this as the runIndex
        // parameter on Reader::read; ReaderConfig.RunIndex round-trips it through the
        // pwiz-sharp call surface. Default 0 = first sample, matching cpp.
        int sampleIndex0 = config?.RunIndex ?? 0;
        var wiff = AbstractWiffFile.Open(filename, sampleIndex0);
        try
        {
            FillMetadata(result, filename, wiff, config);
        }
        catch
        {
            wiff.Dispose();
            throw;
        }
#endif
    }

    private static bool IsWiffFile(string path)
        => path.EndsWith(".wiff", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".wiff2", StringComparison.OrdinalIgnoreCase);

#if !NO_VENDOR_SUPPORT
    private static void FillMetadata(MSData result, string wiffPath, AbstractWiffFile wiff, ReaderConfig? config)
    {
        // Mirror pwiz cpp Reader_ABI: msd.id starts at the wiff stem, then is reconciled with
        // the per-sample name. If the only-sample's name already contains the stem, prefer the
        // sample name (more specific). Otherwise, when there are multiple samples or the stem
        // and sample name don't share a substring, append "-<sampleName>" so the run id matches
        // the reference mzML naming used by VendorReaderTestHarness.
        string id = Path.GetFileNameWithoutExtension(wiffPath);
        string sampleName = wiff.SampleName ?? string.Empty;
        if (!string.IsNullOrEmpty(sampleName))
        {
            if (wiff.SampleCount == 1 && sampleName.Contains(id, StringComparison.Ordinal))
                id = sampleName;
            else if (wiff.SampleCount > 1 || !id.Contains(sampleName, StringComparison.Ordinal))
                id = $"{id}-{sampleName}";
        }
        result.Id = id;
        result.Run.Id = result.Id;

        // fileDescription/fileContent — one CV term per distinct experiment kind in the run.
        // Mirrors cpp Reader_ABI: non-MRM experiments get translateAsSpectrumType(); MRM
        // experiments emit MS_SRM_chromatogram (because pwiz exposes MRMs as chromatograms,
        // not spectra, in the default config).
        for (int e = 0; e < wiff.ExperimentCount; e++)
        {
            try
            {
                var experimentType = wiff.GetExperiment(e).ExperimentType;
                if (experimentType == WiffExperimentType.MRM)
                    result.FileDescription.FileContent.Set(CVID.MS_SRM_chromatogram);
                else
                    result.FileDescription.FileContent.Set(Reader_Sciex_Detail.TranslateAsSpectrumType(experimentType));
            }
            catch { /* skip corrupt experiment */ }
        }

        // SourceFile entries: cpp emits "WIFF" (the .wiff itself) and, when present, "WIFFSCAN"
        // (the .wiff.scan companion file). Mirror that pair.
        // cpp emits sourceFile.location with native separators (backslashes on Windows via
        // boost::filesystem::path::string()); keep the same form for msdiff parity.
        var sf = new SourceFile("WIFF", Path.GetFileName(wiffPath),
            "file://" + Path.GetDirectoryName(Path.GetFullPath(wiffPath))!);
        sf.Set(CVID.MS_WIFF_nativeID_format);
        sf.Set(CVID.MS_ABI_WIFF_format);
        result.FileDescription.SourceFiles.Add(sf);
        result.Run.DefaultSourceFile = sf;

        string wiffScanPath = wiffPath + ".scan";
        if (File.Exists(wiffScanPath))
        {
            var sfScan = new SourceFile("WIFFSCAN", Path.GetFileName(wiffScanPath),
                "file://" + Path.GetDirectoryName(Path.GetFullPath(wiffScanPath))!);
            sfScan.Set(CVID.MS_WIFF_nativeID_format);
            sfScan.Set(CVID.MS_ABI_WIFF_format);
            result.FileDescription.SourceFiles.Add(sfScan);
        }

        // Software entries: matches cpp shapes.
        var analyst = new Software("Analyst") { Version = "unknown" };
        analyst.Set(CVID.MS_Analyst);
        result.Software.Add(analyst);
        var pwizSoftware = new Software("pwiz_Reader_ABI") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        // Single DataProcessing entry; cpp uses "pwiz_Reader_ABI_conversion" — match that.
        var dpReader = new DataProcessing("pwiz_Reader_ABI_conversion");
        var pmReader = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmReader.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmReader);
        result.DataProcessings.Add(dpReader);

        // InstrumentConfiguration: pull the model from the SDK's instrument-name string,
        // hand it to the cpp-port detail translator. cpp hardcodes the ion source as IonSpray
        // (= MS_electrospray_ionization), which always wins over the SDK's IonSourceType.
        var metaConfig = config ?? new ReaderConfig();
        var instrumentModel = Reader_Sciex_Detail.ParseInstrumentName(wiff.InstrumentModelName);
        // cpp Reader_ABI.cpp:149-156: getInstrumentModel() THROWS for a name it cannot map and
        // fillInMetadata routes that through instrumentMetadataError, so an unrecognized
        // instrument fails the conversion unless --ignoreUnknownInstrumentError is passed. The
        // literal "UNKNOWN" is not an error in cpp and is not one here either.
        if (instrumentModel == Reader_Sciex_Detail.SciexInstrumentModel.Unknown &&
            !Reader_Sciex_Detail.IsExplicitlyUnknown(wiff.InstrumentModelName))
        {
            metaConfig.InstrumentMetadataError(
                "[Reader_Sciex.FillInMetadata] unable to determine instrument model " +
                $"(unknown instrument type: {wiff.InstrumentModelName})");
        }
        var ic = Reader_Sciex_Detail.TranslateAsInstrumentConfiguration(
            instrumentModel, CVID.MS_electrospray_ionization);
        ic.Software = analyst;
        // cpp Reader_ABI.cpp:165 — instrument serial number when the SDK exposes one. cpp wraps
        // the SDK call in try/catch + instrumentMetadataError; the managed SDK surfaces the same
        // failures as exceptions, so mirror the handling rather than letting them escape as an
        // unrelated-looking read error.
        try
        {
            var serial = wiff.InstrumentSerialNumber;
            if (!string.IsNullOrEmpty(serial))
                ic.Set(CVID.MS_instrument_serial_number, serial);
        }
        catch (Exception ex) when (ex is not IOException)
        {
            metaConfig.InstrumentMetadataError(
                $"[Reader_Sciex.FillInMetadata] unable to read instrument serial number ({ex.Message})");
        }
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        // Sciex is one of the three readers whose host-zone shift cpp gates on the config flag
        // (Reader_ABI.cpp: getSampleAcquisitionTime(sample, adjustUnknownTimeZonesToHostTimeZone)).
        // cpp Reader_ABI.cpp:177-184 guards this one too.
        try
        {
            string? startTime = ReaderConfig.FormatStartTimeStamp(
                wiff.StartTimestampRaw, metaConfig.AdjustUnknownTimeZonesToHostTimeZone);
            if (!string.IsNullOrEmpty(startTime))
                result.Run.StartTimeStamp = startTime;
        }
        catch (Exception ex) when (ex is not IOException)
        {
            metaConfig.InstrumentMetadataError(
                $"[Reader_Sciex.FillInMetadata] unable to read sample acquisition time ({ex.Message})");
        }

        bool simAsSpectra = config?.SimAsSpectra ?? false;
        bool srmAsSpectra = config?.SrmAsSpectra ?? false;
        bool globalChromsAreMs1Only = config?.GlobalChromatogramsAreMs1Only ?? false;
        // cpp SpectrumList_ABI.cpp:254/:261 — ignoreZeroIntensityPoints reaches both getData
        // and getDataSize, so it changes defaultArrayLength as well as the arrays.
        bool ignoreZeroIntensityPoints = config?.IgnoreZeroIntensityPoints ?? false;
        // cpp SpectrumList_ABI.cpp:240/:298 — acceptZeroLengthSpectra changes both which cycles
        // get indexed and whether the base-peak cvParams are emitted.
        bool acceptZeroLengthSpectra = config?.AcceptZeroLengthSpectra ?? false;
        // Not a cpp config field - cpp always probes. See ReaderConfig.VerifyNonEmptySpectraAtIndex.
        bool verifyNonEmptySpectra = config?.VerifyNonEmptySpectraAtIndex ?? false;
        // SpectrumList owns the underlying wiff handle; ChromatogramList shares the same
        // reference so we can iterate both lists during conversion. cpp uses the same pattern
        // (one shared WiffFilePtr).
        result.Run.SpectrumList = new SpectrumList_Sciex(wiff, ownsWiff: true, ic, simAsSpectra, srmAsSpectra, ignoreZeroIntensityPoints, acceptZeroLengthSpectra, verifyNonEmptySpectra)
        {
            Dp = dpReader,
        };
        result.Run.ChromatogramList = new ChromatogramList_Sciex(wiff, ownsWiff: false, globalChromsAreMs1Only)
        {
            Dp = dpReader,
        };
    }
#endif
}
