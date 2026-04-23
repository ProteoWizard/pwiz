using System.IO.Compression;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Mgf;
using Pwiz.Data.MsData.Mzml;
using Pwiz.Data.MsData.Readers;
using Pwiz.Vendor.Bruker;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Tools.MsConvert;

/// <summary>Runs the msconvert-sharp conversion pipeline: read → filter → write.</summary>
public sealed class Converter
{
    private readonly MsConvertConfig _config;
    private readonly TextWriter _log;
    private readonly ReaderList _readers;

    /// <summary>Creates a converter with the given configuration. Output goes to <paramref name="log"/> (stderr-style).</summary>
    public Converter(MsConvertConfig config, TextWriter? log = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _log = log ?? TextWriter.Null;
        // Include Thermo + Bruker alongside the built-in mzML/MGF readers so vendor files auto-detect.
        _readers = ThermoReaderRegistration.CreateDefaultWithThermo();
        var brukerReader = new Reader_Bruker { CombineIonMobilitySpectra = _config.CombineIonMobilitySpectra };
        _readers.Add(brukerReader);
    }

    /// <summary>Processes every configured input file. Returns the count that succeeded.</summary>
    public int Run()
    {
        Directory.CreateDirectory(_config.OutputPath);
        WarnAboutUnimplementedOptions();

        if (_config.Merge)
        {
            try { ConvertMerged(); return 1; }
            catch (Exception ex)
            {
                _log.WriteLine($"error converting merged output: {ex.Message}");
                return 0;
            }
        }

        int successCount = 0;
        foreach (var input in _config.InputFiles)
        {
            try
            {
                ConvertOne(input);
                successCount++;
            }
            catch (Exception ex)
            {
                _log.WriteLine($"error converting {input}: {ex.Message}");
                if (!_config.ContinueOnError) break;
            }
        }
        return successCount;
    }

    private void ConvertOne(string input)
    {
        if (_config.Verbose) _log.WriteLine($"reading {input}");

        var msd = ReadAndProcess(input);
        WriteOutput(msd, BuildOutputPath(input));
    }

    private void ConvertMerged()
    {
        if (_config.InputFiles.Count == 0)
            throw new InvalidOperationException("--merge requires at least one input.");
        // Start from the first file; subsequent files contribute their spectra/chromatograms in order.
        var merged = ReadAndProcess(_config.InputFiles[0]);
        foreach (var extra in _config.InputFiles.Skip(1))
        {
            if (_config.Verbose) _log.WriteLine($"merging {extra}");
            var next = ReadAndProcess(extra);
            MergeRun(merged, next);
        }
        // Choose an output name: --outfile wins, otherwise the first input's basename.
        string outputFile = BuildOutputPath(_config.InputFiles[0]);
        WriteOutput(merged, outputFile);
    }

    private MSData ReadAndProcess(string input)
    {
        var msd = new MSData();
        _readers.Read(input, msd);
        MSDataFile.CalculateSha1Checksums(msd);

        if (!string.IsNullOrEmpty(_config.ContactInfo))
            AttachContactInfo(msd, _config.ContactInfo);

        if (_config.StripLocationFromSourceFiles)
            foreach (var sf in msd.FileDescription.SourceFiles) sf.Location = string.Empty;
        if (_config.StripVersionFromSoftware)
            foreach (var sw in msd.Software) sw.Version = string.Empty;

        if (_config.Filters.Count > 0 && msd.Run.SpectrumList is not null)
        {
            if (_config.Verbose)
                foreach (var f in _config.Filters) _log.WriteLine($"  filter: {f}");
            msd.Run.SpectrumList = SpectrumListFactory.Wrap(msd.Run.SpectrumList, _config.Filters);
        }

        // Chromatogram filters aren't implemented yet; warn once rather than silently drop.
        if (_config.ChromatogramFilters.Count > 0)
            _log.WriteLine("warning: --chromatogramFilter not implemented; filters ignored");

        return msd;
    }

    private void WriteOutput(MSData msd, string outputFile)
    {
        if (_config.Gzip && !outputFile.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            outputFile += ".gz";
        if (_config.Verbose) _log.WriteLine($"writing {outputFile}");

        using Stream output = OpenOutputStream(outputFile);
        switch (_config.Format)
        {
            case OutputFormat.Mzml:
                new MzmlWriter(_config.EncoderConfig) { Indexed = !_config.NoIndex }.Write(msd, output);
                break;
            case OutputFormat.Mgf:
                using (var tw = new StreamWriter(output))
                    new MgfSerializer().Write(msd, tw);
                break;
            default:
                throw new NotImplementedException(
                    $"Output format {_config.Format} is accepted by the CLI but not yet implemented in msconvert-sharp.");
        }
    }

    private Stream OpenOutputStream(string outputFile)
    {
        var fs = File.Create(outputFile);
        return _config.Gzip
            ? new GZipStream(fs, CompressionLevel.Optimal, leaveOpen: false)
            : fs;
    }

    private static void MergeRun(MSData dest, MSData src)
    {
        // Source files + software pile up; spectra/chromatograms concat.
        foreach (var sf in src.FileDescription.SourceFiles) dest.FileDescription.SourceFiles.Add(sf);
        foreach (var s in src.Software)
        {
            if (!dest.Software.Any(x => x.Id == s.Id)) dest.Software.Add(s);
        }
        if (src.Run.SpectrumList is not null && dest.Run.SpectrumList is not null)
        {
            // Naive merge: re-wrap via SpectrumListSimple so the Writer iterates both.
            var combined = new Pwiz.Data.MsData.Spectra.SpectrumListSimple
            {
                Dp = (dest.Run.SpectrumList.DataProcessing ?? src.Run.SpectrumList.DataProcessing),
            };
            for (int i = 0; i < dest.Run.SpectrumList.Count; i++)
                combined.Spectra.Add(dest.Run.SpectrumList.GetSpectrum(i, getBinaryData: true));
            for (int i = 0; i < src.Run.SpectrumList.Count; i++)
            {
                var spec = src.Run.SpectrumList.GetSpectrum(i, getBinaryData: true);
                spec.Index = combined.Spectra.Count;
                combined.Spectra.Add(spec);
            }
            dest.Run.SpectrumList = combined;
        }
    }

    private static void AttachContactInfo(MSData msd, string contactFile)
    {
        if (!File.Exists(contactFile)) return;
        string contents = File.ReadAllText(contactFile).Trim();
        if (string.IsNullOrEmpty(contents)) return;
        // pwiz C++ accepts a free-form file; we attach it as a userParam on fileDescription.
        msd.FileDescription.FileContent.UserParams.Add(
            new UserParam("contact info", contents, "xsd:string"));
    }

    private string BuildOutputPath(string input)
    {
        string ext = _config.OutputExtension ?? DefaultExtension(_config.Format);
        if (!ext.StartsWith('.')) ext = "." + ext;

        string name = !string.IsNullOrEmpty(_config.OutFile)
            ? _config.OutFile
            : Path.GetFileNameWithoutExtension(input) + ext;
        return Path.Combine(_config.OutputPath, name);
    }

    private static string DefaultExtension(OutputFormat format) => format switch
    {
        OutputFormat.Mzml => ".mzML",
        OutputFormat.MzXml => ".mzXML",
        OutputFormat.Mz5 => ".mz5",
        OutputFormat.MzMLb => ".mzMLb",
        OutputFormat.Mgf => ".mgf",
        OutputFormat.Text => ".txt",
        OutputFormat.Ms1 => ".ms1",
        OutputFormat.Cms1 => ".cms1",
        OutputFormat.Ms2 => ".ms2",
        OutputFormat.Cms2 => ".cms2",
        _ => ".out",
    };

    /// <summary>
    /// Emits a single warning line per unimplemented switch the user requested. Accepting these
    /// switches (and warning) is friendlier than rejecting — users scripting msconvert invocations
    /// via Skyline / workflows should be able to point them at msconvert-sharp without surprises.
    /// </summary>
    private void WarnAboutUnimplementedOptions()
    {
        if (_config.MzTruncation != 0) _log.WriteLine("warning: --mzTruncation not implemented");
        if (_config.IntenTruncation != 0) _log.WriteLine("warning: --intenTruncation not implemented");
        if (_config.MzDelta) _log.WriteLine("warning: --mzDelta not implemented");
        if (_config.IntenDelta) _log.WriteLine("warning: --intenDelta not implemented");
        if (_config.MzLinear) _log.WriteLine("warning: --mzLinear not implemented");
        if (_config.IntenLinear) _log.WriteLine("warning: --intenLinear not implemented");
        if (_config.SimAsSpectra) _log.WriteLine("warning: --simAsSpectra not implemented (SIMs emit as chromatograms)");
        if (_config.SrmAsSpectra) _log.WriteLine("warning: --srmAsSpectra not implemented (SRMs emit as chromatograms)");
        // --combineIonMobilitySpectra is honored for Bruker; silently ignored for Thermo (no IMS).
        if (_config.DdaProcessing) _log.WriteLine("warning: --ddaProcessing not implemented");
        if (_config.IgnoreCalibrationScans) _log.WriteLine("warning: --ignoreCalibrationScans not implemented");
        if (!string.IsNullOrEmpty(_config.RunIndexSet)) _log.WriteLine("warning: --runIndexSet not implemented");
        if (_config.SingleThreaded > 0) _log.WriteLine("note: --singleThreaded is a no-op (msconvert-sharp is single-threaded today)");
    }
}
