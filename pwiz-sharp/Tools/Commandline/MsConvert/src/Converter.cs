using System.Globalization;
using Pwiz.Analysis;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Util.Misc;
using Pwiz.Vendor.Bruker;
using Pwiz.Vendor.Thermo;
using Pwiz.Vendor.Waters;

namespace Pwiz.Tools.MsConvert;

/// <summary>Runs the msconvert-sharp conversion pipeline: read → filter → write.</summary>
public sealed class Converter
{
    private readonly MsConvertConfig _config;
    private readonly TextWriter _log;
    private readonly ReaderList _readers;
    private readonly IterationListenerRegistry? _progressRegistry;

    /// <summary>Creates a converter with the given configuration. Output goes to <paramref name="log"/> (stderr-style).</summary>
    public Converter(MsConvertConfig config, TextWriter? log = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _log = log ?? TextWriter.Null;
        // In verbose mode, one shared progress registry drives both the filter chain
        // (e.g. diaUmpire's "[step N of M]" messages during its lazy pull) and the
        // writer's per-spectrum progress. VerboseProgressPeriod sets the reporting
        // interval; a host with a slow progress sink can raise it to avoid a flood.
        if (_config.Verbose)
        {
            _progressRegistry = new IterationListenerRegistry();
            _progressRegistry.AddListener(new ConsoleProgressListener(_log),
                iterationPeriod: Math.Max(1, _config.VerboseProgressPeriod));
        }
        // Include Thermo + Bruker + Waters + Agilent + Sciex alongside the built-in mzML/MGF
        // readers so vendor files auto-detect by extension/identity. Vendor projects always
        // build, so Reader.Identify() works in every configuration. When the build was made
        // without --i-agree-to-the-vendor-licenses, the encrypted vendor SDKs aren't extracted
        // and Reader.Read() throws a clear "vendor support not enabled" error.
        _readers = ThermoReaderRegistration.CreateDefaultWithThermo();
        var brukerReader = new Reader_Bruker { CombineIonMobilitySpectra = _config.CombineIonMobilitySpectra };
        _readers.Add(brukerReader);
        _readers.Add(new Reader_Waters());
        _readers.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
        _readers.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
        _readers.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
        _readers.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
        _readers.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
        _readers.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
    }

    /// <summary>Processes every configured input file. Returns the count that succeeded.</summary>
    public int Run()
    {
        Directory.CreateDirectory(_config.OutputPath);
        WarnAboutUnimplementedOptions();

        // --runIndexSet limits which runs of multi-run inputs get converted. cpp builds an
        // msdList per input (one MSData per run) then filters by index (msconvert.cpp:1030).
        // Sharp's IReader.Read returns a single MSData per call, so we honor the set by
        // iterating: each index in the set drives one read with ReaderConfig.RunIndex = i.
        // For single-run readers (mzML, Thermo, etc.) only index 0 is valid; other indices
        // produce identical outputs (or fail), and outputs after the first collide on
        // filename unless --outfile / run suffixing kicks in.
        var runIndexSet = ParseRunIndexSet(_config.RunIndexSet);

        if (_config.Merge)
        {
            try { ConvertMerged(runIndexSet); return 1; }
            catch (Pwiz.Util.Misc.EnumerationException ex)
            {
                _log.WriteLine($"error merging files (aborting): {ex.Message}");
                _log.WriteLine("  To skip problematic spectra and write the remaining data, re-run with --continueOnError.");
                return 0;
            }
            catch (Exception ex)
            {
                _log.WriteLine($"error merging files (aborting): {ex.Message}");
                return 0;
            }
        }

        int successCount = 0;
        foreach (var input in _config.InputFiles)
        {
            try
            {
                ConvertInputAllRuns(input, runIndexSet);
                successCount++;
            }
            catch (Pwiz.Util.Misc.EnumerationException ex)
            {
                // Per-spectrum/per-chromatogram fetch failure surfaced by the
                // writer (cpp msconvert.cpp catch on pwiz::util::enumeration_error).
                // This is the only failure mode where --continueOnError would let
                // the conversion proceed, so print the hint here and only here.
                _log.WriteLine($"error converting {input} (aborting conversion of this file): {ex.Message}");
                _log.WriteLine("  To skip problematic spectra and write the remaining data, re-run with --continueOnError.");
                if (_config.Verbose) _log.WriteLine(ex.ToString());
                if (!_config.ContinueOnError) break;
            }
            catch (Exception ex)
            {
                _log.WriteLine($"error converting {input} (aborting conversion of this file): {ex.Message}");
                if (_config.Verbose) _log.WriteLine(ex.ToString());
                if (!_config.ContinueOnError) break;
            }
        }
        return successCount;
    }

    private static Pwiz.Util.Misc.IntegerSet? ParseRunIndexSet(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        var set = new Pwiz.Util.Misc.IntegerSet();
        set.Parse(spec);
        return set;
    }

    private void ConvertInputAllRuns(string input, Pwiz.Util.Misc.IntegerSet? runIndexSet)
    {
        // No set → single conversion with the reader's default RunIndex (0).
        if (runIndexSet is null || runIndexSet.IsEmpty)
        {
            ConvertOne(input, runIndex: 0, runIndexSuffixed: false);
            return;
        }

        // Iterate the requested run indices. We can't cheaply enumerate "how many runs does this
        // file have?" without opening it, so we just attempt each index and let the reader
        // throw or silently produce identical output for out-of-range indices. Suffix outputs
        // when the set has more than one index so files don't collide.
        bool multi = runIndexSet.Intervals.Count > 1 || runIndexSet.Count > 1;
        int matched = 0;
        foreach (int idx in runIndexSet)
        {
            ConvertOne(input, runIndex: idx, runIndexSuffixed: multi);
            matched++;
        }
        if (matched == 0)
            throw new InvalidOperationException("No runs correspond to the specified runIndexSet");
    }

    private void ConvertOne(string input, int runIndex, bool runIndexSuffixed)
    {
        if (_config.Verbose) _log.WriteLine($"reading {input} (runIndex={runIndex})");

        // `using` releases native vendor handles (Thermo IRawFileThreadManager, Bruker timsdata,
        // etc.) once the output is written.
        using var msd = ReadAndProcess(input, runIndex);
        string outPath = BuildOutputPath(input, msd);
        if (runIndexSuffixed)
        {
            // Insert "-<runIndex>" before the extension so multiple runs don't overwrite each
            // other. cpp does similar with its own outputFilename helper.
            string dir = Path.GetDirectoryName(outPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(outPath);
            string ext = Path.GetExtension(outPath);
            outPath = Path.Combine(dir, $"{name}-{runIndex}{ext}");
        }
        WriteOutput(msd, outPath);
    }

    private void ConvertMerged(Pwiz.Util.Misc.IntegerSet? runIndexSet)
    {
        if (_config.InputFiles.Count == 0)
            throw new InvalidOperationException("--merge requires at least one input.");
        // For --merge, runIndexSet (if any) selects which run of EACH input contributes; the
        // simplest sane interpretation is to use the first index (default 0). cpp's merge path
        // doesn't define a per-input runIndex semantic either, so we don't try to enumerate.
        int runIndex = runIndexSet is not null && !runIndexSet.IsEmpty ? runIndexSet.First() : 0;

        // Start from the first file; subsequent files contribute their spectra/chromatograms in order.
        using var merged = ReadAndProcess(_config.InputFiles[0], runIndex);
        foreach (var extra in _config.InputFiles.Skip(1))
        {
            if (_config.Verbose) _log.WriteLine($"merging {extra}");
            // MergeRun copies the source's spectra into a SpectrumListSimple on `merged`, so once
            // the merge completes we can release `next`'s vendor handle immediately.
            using var next = ReadAndProcess(extra, runIndex);
            MergeRun(merged, next);
        }
        // Choose an output name: --outfile wins, otherwise the first input's basename.
        string outputFile = BuildOutputPath(_config.InputFiles[0], merged);
        WriteOutput(merged, outputFile);
    }

    private MSData ReadAndProcess(string input, int runIndex = 0)
    {
        var msd = new MSData();
        var rcfg = BuildReaderConfig();
        rcfg.RunIndex = runIndex;
        _readers.Read(input, msd, rcfg);
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
            // MSData-shaped overload threads the run context through to filters that need it
            // (mzRefiner, turbocharger, precursorRefine, titleMaker) AND promotes new
            // DataProcessing records to msd.DataProcessings.
            SpectrumListFactory.Wrap(msd, _config.Filters, _progressRegistry);
        }

        if (_config.ChromatogramFilters.Count > 0 && msd.Run.ChromatogramList is not null)
        {
            if (_config.Verbose)
                foreach (var f in _config.ChromatogramFilters) _log.WriteLine($"  chromatogramFilter: {f}");
            ChromatogramListFactory.Wrap(msd, _config.ChromatogramFilters);
        }

        // Stamp the invocation onto the output's first DataProcessing for round-trip
        // provenance (cpp msconvert.cpp:1059-1060 does the same: first DP, first method).
        // Skipped when there's no DataProcessing to attach to — that only happens for
        // truly empty inputs (e.g. an empty mzML test fixture).
        if (!string.IsNullOrEmpty(_config.CommandLineParameters)
            && msd.DataProcessings.Count > 0
            && msd.DataProcessings[0].ProcessingMethods.Count > 0)
        {
            msd.DataProcessings[0].ProcessingMethods[0]
                .Set(Pwiz.Data.Common.Cv.CVID.MS_command_line_parameters, _config.CommandLineParameters);
        }

        return msd;
    }

    private void WriteOutput(MSData msd, string outputFile)
    {
        if (_config.WriteConfig.Gzip && !outputFile.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            outputFile += ".gz";
        if (_config.Verbose) _log.WriteLine($"writing {outputFile}");

        // Write to <outputFile>.partial, then rename. Mirrors cpp msconvert.cpp's
        // writeAtomically — prevents an incomplete file from being mistaken for a
        // valid conversion when the vendor library throws partway through (e.g.
        // a corrupted scan that the reader can't centroid). MSDataFile.Write owns
        // the gzip-stream wrapping + per-format writer dispatch; we've already
        // adjusted the filename for the .gz suffix above so callers see the
        // on-disk name in the log.
        string partial = outputFile + ".partial";
        // Clear any leftover .partial from a previous failed run; File.Move below
        // wouldn't overwrite the final on Windows if the partial is around.
        if (File.Exists(partial)) File.Delete(partial);
        try
        {
            MSDataFile.Write(msd, partial, _config.WriteConfig, _progressRegistry);
        }
        catch
        {
            // Best-effort cleanup; preserve original exception.
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            throw;
        }
        // File.Move(overwrite: true) ≈ bfs::rename — atomic on the same volume,
        // which is the case since .partial sits next to the final name.
        File.Move(partial, outputFile, overwrite: true);
    }

    /// <summary>
    /// Console progress listener used by msconvert-sharp's <c>-v</c> mode. Writes one line per
    /// delivery to the configured log sink (typically stderr).
    /// </summary>
    private sealed class ConsoleProgressListener : IIterationListener
    {
        private readonly TextWriter _out;

        public ConsoleProgressListener(TextWriter logSink) => _out = logSink;

        public IterationStatus Update(IterationUpdate message)
        {
            string line = message.IterationCount > 0
                ? string.Format(CultureInfo.InvariantCulture, "  {0}: {1}/{2}",
                    message.Message, message.IterationIndex + 1, message.IterationCount)
                : string.Format(CultureInfo.InvariantCulture, "  {0}: {1}",
                    message.Message, message.IterationIndex + 1);
            _out.WriteLine(line);
            return IterationStatus.Ok;
        }
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

    private string BuildOutputPath(string input, MSData msd)
    {
        string ext = _config.OutputExtension ?? DefaultExtension(_config.WriteConfig.Format);
        if (!ext.StartsWith('.')) ext = "." + ext;

        string name;
        if (!string.IsNullOrEmpty(_config.OutFile))
        {
            name = _config.OutFile;
        }
        else
        {
            // cpp msconvert names the output by the run id, which vendor readers populate as
            // <input-stem>-<sample-name> for multi-sample formats (Sciex WIFF / WIFF2) and
            // <input-stem> otherwise. Mirror that so multi-sample WIFFs get one
            // disambiguated mzML per sample (e.g. PressureTrace1-6500SysSuit1269.mzML)
            // rather than colliding on PressureTrace1.mzML across runs.
            string baseName = !string.IsNullOrEmpty(msd.Run.Id)
                ? msd.Run.Id
                : Path.GetFileNameWithoutExtension(input);
            name = baseName + ext;
        }
        return Path.Combine(_config.OutputPath, name);
    }

    private static string DefaultExtension(WriteFormat format) => format switch
    {
        WriteFormat.Mzml => ".mzML",
        WriteFormat.MzXml => ".mzXML",
        WriteFormat.Mz5 => ".mz5",
        WriteFormat.MzMLb => ".mzMLb",
        WriteFormat.Mgf => ".mgf",
        WriteFormat.Text => ".txt",
        WriteFormat.Ms1 => ".ms1",
        WriteFormat.Bms1 => ".bms1",
        WriteFormat.Cms1 => ".cms1",
        WriteFormat.Ms2 => ".ms2",
        WriteFormat.Bms2 => ".bms2",
        WriteFormat.Cms2 => ".cms2",
        _ => ".out",
    };

    /// <summary>
    /// Emits a single warning line per unimplemented switch the user requested. Accepting these
    /// switches (and warning) is friendlier than rejecting — users scripting msconvert invocations
    /// via Skyline / workflows should be able to point them at msconvert-sharp without surprises.
    /// </summary>
    private void WarnAboutUnimplementedOptions()
    {
        // --mzTruncation / --intenTruncation / --mz*Delta / --mz*Linear apply to mzMLb output
        // only — cpp's BinaryDataEncoder doesn't honor them for plain mzML (IO.cpp:1932-1958
        // emits the cvParam but doesn't transform the data; sharp matches that). Warn the
        // user when these flags are set but the output isn't mzMLb.
        bool hasLossy = _config.MzTruncation != 0 || _config.IntenTruncation != 0
                        || _config.MzDelta || _config.IntenDelta
                        || _config.MzLinear || _config.IntenLinear;
        if (hasLossy && _config.WriteConfig.Format != WriteFormat.MzMLb)
            _log.WriteLine("warning: --mz/intenTruncation/Delta/Linear take effect only with --mzMLb output; "
                + "for plain mzML the cvParam is recorded but the array is not transformed (matches cpp).");
        // --simAsSpectra and --srmAsSpectra are honored for Thermo (Reader_Thermo passes them
        // through to ChromatogramList_Thermo + SpectrumList_Thermo).
        // --combineIonMobilitySpectra is honored for Bruker; silently ignored for Thermo (no IMS).
        // --ddaProcessing and --ignoreCalibrationScans are honored for Waters (Reader_Waters
        // plumbs them into SpectrumList_Waters).
        // --runIndexSet is honored: each index in the set drives one read with
        // ReaderConfig.RunIndex = i, with outputs suffixed when the set has more than one index.
        if (_config.SingleThreaded > 0) _log.WriteLine("note: --singleThreaded is a no-op (msconvert-sharp is single-threaded today)");
    }

    private ReaderConfig BuildReaderConfig() => new()
    {
        SimAsSpectra = _config.SimAsSpectra,
        SrmAsSpectra = _config.SrmAsSpectra,
        CombineIonMobilitySpectra = _config.CombineIonMobilitySpectra,
        DdaProcessing = _config.DdaProcessing,
        IgnoreCalibrationScans = _config.IgnoreCalibrationScans,
    };
}
