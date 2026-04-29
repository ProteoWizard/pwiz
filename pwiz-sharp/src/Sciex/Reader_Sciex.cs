using System.Globalization;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Processing;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Sources;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Sciex;

/// <summary>
/// <see cref="IReader"/> for Sciex / ABI WIFF files. Identifies <c>.wiff</c> and <c>.wiff2</c>
/// files; opens the first sample via <see cref="WiffData"/> and emits one MSData per
/// invocation. Multi-sample wiff support is a follow-up.
/// </summary>
/// <remarks>
/// Initial port covers Q-TOF / TripleTOF / TripleQuad data: full-scan MS1 / MSn (Product),
/// SIM/MRM as chromatograms (or as spectra when the matching config flag is set). Skipped:
/// ADC traces, multi-sample wiffs, instrument-model translation table beyond a generic
/// <c>MS_SCIEX_instrument_model</c> term.
/// </remarks>
public sealed class Reader_Sciex : IReader
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
        return WiffData.IsWiffFile(filename) && File.Exists(filename) ? CvType : CVID.CVID_Unknown;
    }

    /// <inheritdoc/>
    public void Read(string filename, MSData result, ReaderConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(filename);
        ArgumentNullException.ThrowIfNull(result);
        if (!WiffData.IsWiffFile(filename))
            throw new InvalidDataException($"Not a WIFF file: {filename}");
        if (!File.Exists(filename))
            throw new FileNotFoundException("WIFF file not found", filename);

        result.CVs.AddRange(MSData.DefaultCVList);
        var wiff = new WiffData(filename, sampleIndex0: 0);
        try
        {
            FillMetadata(result, filename, wiff, config);
        }
        catch
        {
            wiff.Dispose();
            throw;
        }
    }

    private static void FillMetadata(MSData result, string wiffPath, WiffData wiff, ReaderConfig? config)
    {
        string baseName = Path.GetFileNameWithoutExtension(wiffPath);
        // Cpp run id: "<baseName> (sample N)" when multi-sample, else baseName. We default to
        // the simple case until multi-sample support lands.
        result.Id = wiff.SampleCount > 1 ? $"{baseName} (sample {wiff.SampleNumber})" : baseName;
        result.Run.Id = result.Id;

        var sf = new SourceFile(Path.GetFileName(wiffPath), Path.GetFileName(wiffPath),
            "file:///" + Path.GetDirectoryName(Path.GetFullPath(wiffPath))!.Replace('\\', '/'));
        sf.Set(CVID.MS_ABI_WIFF_format);
        sf.Set(CVID.MS_WIFF_nativeID_format);
        result.FileDescription.SourceFiles.Add(sf);
        result.Run.DefaultSourceFile = sf;

        // fileContent: scan the experiments to figure out which spectrum types we'll emit.
        bool hasMs1 = false, hasMsn = false;
        for (int e = 0; e < wiff.ExperimentCount; e++)
        {
            try
            {
                var exp = wiff.GetExperiment(e);
                var info = exp.Details;
                int level = info.ExperimentType switch
                {
                    Clearcore2.Data.DataAccess.SampleData.ExperimentType.MS => 1,
                    Clearcore2.Data.DataAccess.SampleData.ExperimentType.SIM => 1,
                    _ => 2,
                };
                if (level == 1) hasMs1 = true;
                else hasMsn = true;
            }
            catch { /* corrupt experiment — skip */ }
        }
        if (hasMs1) result.FileDescription.FileContent.Set(CVID.MS_MS1_spectrum);
        if (hasMsn) result.FileDescription.FileContent.Set(CVID.MS_MSn_spectrum);

        // Software entries.
        var analyst = new Software("Analyst") { Version = string.Empty };
        analyst.Set(CVID.MS_Analyst);
        result.Software.Add(analyst);
        var pwizSoftware = new Software("pwiz") { Version = MSData.PwizVersion };
        pwizSoftware.Set(CVID.MS_pwiz);
        result.Software.Add(pwizSoftware);

        var dpReader = new DataProcessing("pwiz_Reader_Sciex_conversion");
        var pmReader = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmReader.Set(CVID.MS_Conversion_to_mzML);
        dpReader.ProcessingMethods.Add(pmReader);
        result.DataProcessings.Add(dpReader);
        var dpCommon = new DataProcessing("pwiz_Reader_conversion");
        var pmCommon = new ProcessingMethod { Order = 0, Software = pwizSoftware };
        pmCommon.Set(CVID.MS_Conversion_to_mzML);
        dpCommon.ProcessingMethods.Add(pmCommon);
        result.DataProcessings.Add(dpCommon);

        // Single instrument config: cpp emits per-instrument-model components, but we go with
        // the generic SCIEX model term + an ESI/MicroESI source + Q-TOF analyzer chain. A full
        // table can land later when we have parity tests.
        var ic = new InstrumentConfiguration("IC1");
        var common = new ParamGroup("CommonInstrumentParams");
        common.Set(CVID.MS_SCIEX_instrument_model);
        if (!string.IsNullOrEmpty(wiff.MsSample.InstrumentName))
            common.UserParams.Add(new UserParam("instrument model", wiff.MsSample.InstrumentName, "xsd:string"));
        result.ParamGroups.Add(common);
        ic.ParamGroups.Add(common);
        ic.ComponentList.Add(new Component(CVID.MS_microelectrospray, 1));
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 2));
        ic.ComponentList.Add(new Component(CVID.MS_quadrupole, 3));
        ic.ComponentList.Add(new Component(CVID.MS_time_of_flight, 4));
        ic.ComponentList.Add(new Component(CVID.MS_microchannel_plate_detector, 5));
        result.InstrumentConfigurations.Add(ic);
        result.Run.DefaultInstrumentConfiguration = ic;

        try
        {
            // Sample acquisition timestamp — SDK exposes BasicSampleInfo with AcquisitionDateTime.
            var details = wiff.Sample.Details;
            result.Run.StartTimeStamp = details.AcquisitionDateTime
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        catch { }

        bool simAsSpectra = config?.SimAsSpectra ?? false;
        bool srmAsSpectra = config?.SrmAsSpectra ?? false;
        bool globalChromsAreMs1Only = config?.GlobalChromatogramsAreMs1Only ?? false;
        // SpectrumList owns the underlying wiff handle; ChromatogramList shares the same
        // reference so we can iterate both lists during conversion. cpp uses the same pattern
        // (one shared MassHunterDataPtr / WiffFilePtr).
        var list = new SpectrumList_Sciex(wiff, ownsWiff: true, ic, simAsSpectra, srmAsSpectra)
        {
            Dp = dpReader,
        };
        result.Run.SpectrumList = list;
        var chromList = new ChromatogramList_Sciex(wiff, ownsWiff: false, globalChromsAreMs1Only)
        {
            Dp = dpReader,
        };
        result.Run.ChromatogramList = chromList;
    }
}
