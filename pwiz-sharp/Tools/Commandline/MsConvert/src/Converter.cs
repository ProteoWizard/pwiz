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
        // cpp builds one MSData per run of the input and writes every one of them
        // (msconvert.cpp:1030-1066); --runIndexSet only narrows that list. Converting just run 0
        // silently dropped every sample after the first of a multi-sample WIFF.
        int runCount = GetRunCount(input);

        if (runIndexSet is null || runIndexSet.IsEmpty)
        {
            for (int i = 0; i < runCount; i++)
                ConvertOne(input, runIndex: i);
            return;
        }

        // cpp keeps only the requested indices that actually exist and fails when none do
        // ("No runs correspond to the specified indices"); it does NOT invent a suffix for the
        // survivors, because run ids are already unique per run.
        int matched = 0;
        foreach (int idx in runIndexSet)
        {
            if (idx < 0 || idx >= runCount) continue;
            ConvertOne(input, runIndex: idx);
            matched++;
        }
        if (matched == 0)
            throw new InvalidOperationException("No runs correspond to the specified runIndexSet");
    }

    /// <summary>
    /// Number of runs (samples) in <paramref name="input"/>. Only multi-sample containers
    /// (Sciex WIFF/WIFF2, Shimadzu multi-run .lcd) have more than one.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>ReaderList.ReadIds</c>: for a reader that is not
    /// <see cref="IMultiSampleReader"/> that method does a full read and returns every SPECTRUM
    /// id, which would be both wrong (spectra are not runs) and ruinously expensive here.
    /// A file whose reader cannot be identified is left to fail later in the read, where the
    /// error message is about the actual problem.
    /// </remarks>
    private int GetRunCount(string input)
    {
        try
        {
            if (_readers.IdentifyReader(input, null) is IMultiSampleReader multi)
                return Math.Max(1, multi.EnumerateSampleNames(input).Length);
        }
        catch (Exception ex) when (_config.Verbose)
        {
            _log.WriteLine($"could not enumerate runs in {input}, assuming 1: {ex.Message}");
        }
        catch { /* fall through to the single-run default */ }
        return 1;
    }

    private void ConvertOne(string input, int runIndex)
    {
        if (_config.Verbose) _log.WriteLine($"reading {input} (runIndex={runIndex})");

        // `using` releases native vendor handles (Thermo IRawFileThreadManager, Bruker timsdata,
        // etc.) once the output is written.
        using var msd = ReadAndProcess(input, runIndex);
        WriteOutput(msd, BuildOutputPath(input, msd));
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

        // Ported from cpp Config::outputFilename (msconvert.cpp:82-117); the ORDER matters, so
        // keep the three steps in cpp's sequence: pick the run id, normalize a known extension
        // off it, then sanitize. cpp msconvert names the output by the run id, which vendor
        // readers populate as <input-stem>-<sample-name> for multi-sample formats (Sciex WIFF /
        // WIFF2) and <input-stem> otherwise, so multi-sample WIFFs get one disambiguated mzML
        // per sample rather than colliding across runs.
        string runId = msd.Run.Id ?? string.Empty;
        if (!string.IsNullOrEmpty(_config.OutFile))
            runId = _config.OutFile;

        if (string.IsNullOrEmpty(runId))
            runId = Path.GetFileNameWithoutExtension(input);
        else if (KnownOutputExtensions.Contains(Path.GetExtension(runId)))
            runId = Path.GetFileNameWithoutExtension(runId);

        return Path.Combine(_config.OutputPath, SanitizeRunId(runId) + ext);
    }

    /// <summary>
    /// Extensions cpp strips off a run id before appending the real output extension
    /// (msconvert.cpp:95-106), so a run id that already looks like an output file does not end
    /// up doubled. Compared case-insensitively, as cpp lower-cases before matching.
    /// </summary>
    private static readonly HashSet<string> KnownOutputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mzML", ".mzXML", ".xml", ".mgf", ".ms1", ".cms1", ".ms2", ".cms2", ".mzMLb", ".mz5"
    };

    /// <summary>
    /// Replaces characters that cannot appear in a filename with <c>_</c>, mirroring cpp
    /// <c>msconvert.cpp:109-113</c>. Without this a Sciex sample name such as
    /// <c>4kSV 0.25/141 CV</c> is read by Windows as a missing subdirectory and the conversion
    /// dies with "Could not find a part of the path"; a sample name containing a newline fails
    /// with "The filename, directory name, or volume label syntax is incorrect".
    /// </summary>
    /// <remarks>
    /// cpp's list is the Windows one, a superset of POSIX, and it is applied on every platform
    /// so a given input yields the same output name everywhere. cpp iterates <c>char</c>, where
    /// the <c>c &gt;= 0</c> guard leaves UTF-8 continuation bytes (negative as signed char)
    /// alone; iterating C# UTF-16 chars is equivalent, since every non-ASCII char exceeds 0x7F
    /// and so fails both the control-character and the illegal-character tests.
    /// </remarks>
    internal static string SanitizeRunId(string runId)
    {
        const string illegal = "\\/*:?<>|\"";
        var chars = runId.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] < 0x20 || chars[i] == 0x7F || illegal.Contains(chars[i]))
                chars[i] = '_';
        return new string(chars);
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

    /// <summary>
    /// Projects the parsed command line onto the <see cref="ReaderConfig"/> the vendor readers
    /// actually consult. Internal (not private) so the mapping is unit-testable: a flag that
    /// parses fine but never lands here is silently dead for every vendor, which is what
    /// happened to <c>--ignoreMissingZeroSamples</c>.
    /// </summary>
    internal ReaderConfig BuildReaderConfig() => new()
    {
        SimAsSpectra = _config.SimAsSpectra,
        SrmAsSpectra = _config.SrmAsSpectra,
        CombineIonMobilitySpectra = _config.CombineIonMobilitySpectra,
        DdaProcessing = _config.DdaProcessing,
        IgnoreCalibrationScans = _config.IgnoreCalibrationScans,
        // cpp msconvert.cpp:453-454 binds --ignoreMissingZeroSamples straight onto
        // Reader::Config::ignoreZeroIntensityPoints; the two names differ, which is exactly
        // why this copy was missing and the option was dead wiring for every vendor.
        IgnoreZeroIntensityPoints = _config.IgnoreMissingZeroSamples,
        // cpp msconvert's Config derives from Reader::Config, so --acceptZeroLengthSpectra binds
        // straight onto the reader's field (msconvert.cpp:450-451). Here the two configs are
        // separate types, so the copy has to be explicit; without it the flag Skyline passes on
        // every Hardklor / msconvert-DDA / DIA-Umpire / EncyclopeDIA conversion was inert.
        AcceptZeroLengthSpectra = _config.AcceptZeroLengthSpectra,
    };
}
