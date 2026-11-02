/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using pwiz.Common.Pipeline;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// One run's fragment mass-error summary for the --model-diagnostics report,
    /// <c>&lt;stem&gt;.fragment-mass-errors.json</c>: for every target the first pass accepted
    /// at run q &lt;= the run FDR, the signed error of the peak closest to each of its most
    /// intense library fragments, in the apex scan and the scan either side, binned into a
    /// fixed histogram per observed-intensity tercile (<see cref="ModelDiagnosticsData.FragmentMassErrorRun"/>).
    ///
    /// <para>The closest peak is looked for in a window WIDER than the search tolerance
    /// (<see cref="HalfWidth"/>), so a too-narrow extraction tolerance shows as a distribution
    /// cut off at its edges. The spectra are the run's calibrated spectra, the same ones the
    /// search scored, so the errors are after the calibration shift.</para>
    ///
    /// <para>A product of <c>PerFileRescoring</c> (P17): only that task holds a run's spectra
    /// after the first pass has decided which targets are confident. It reads, and changes,
    /// nothing any score is computed from - it is written after the run's rescore and from the
    /// run's durable artifacts alone (first-pass q-value sidecar, calibration, spectra cache and
    /// library), so a summary written while re-scoring and one written later, when the flag is
    /// added to a finished analysis, are the same.</para>
    /// </summary>
    internal static class FragmentMassErrorWriter
    {
        /// <summary>Extension of the per-run summary.</summary>
        public const string EXT = @".fragment-mass-errors.json";

        /// <summary>Bumped whenever what the summary measures changes, which redoes every summary.</summary>
        public const int FORMAT_VERSION = 1;

        /// <summary>Library fragments measured per precursor, the most intense first.</summary>
        public const int TOP_FRAGMENTS = 6;

        /// <summary>Scans measured either side of the apex scan.</summary>
        public const int SCAN_RADIUS = 1;

        /// <summary>Histogram bins across the whole window.</summary>
        public const int BIN_COUNT = 200;

        /// <summary>The narrowest half-window, in ppm, measured for HRAM data.</summary>
        private const double MIN_HALF_WIDTH_PPM = 25;

        /// <summary><c>&lt;stem&gt;.fragment-mass-errors.json</c> in the run's output directory.</summary>
        public static string PathFor(string inputFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(inputFile) + EXT;
            return Path.Combine(ArtifactPaths.ResolveOutputDir(inputFile), fileName);
        }

        /// <summary>
        /// Half-width of the measurement window, in the configured unit: three times the
        /// configured tolerance, and never under 25 ppm for HRAM data - the search's calibrated
        /// tolerance is narrower than the configured one, so this is always well beyond it. A
        /// function of the configuration alone, so every run of an analysis bins alike.
        /// </summary>
        public static double HalfWidth(FragmentToleranceConfig configured)
        {
            double threeTimes = 3 * configured.Tolerance;
            if (configured.Unit != ToleranceUnit.Ppm)
                return threeTimes;
            return Math.Max(MIN_HALF_WIDTH_PPM, Math.Ceiling(threeTimes / 5) * 5);
        }

        /// <summary>
        /// The fragment tolerance the main search extracts with for a run calibrated as
        /// <paramref name="ms2Cal"/>, mirroring <c>ScoringPipeline.RunCoelutionScoring</c>: the
        /// calibrated tolerance (3 SD) when the run calibrated, floored by
        /// OSPREY_MIN_FRAGMENT_TOLERANCE in ppm, and the configured tolerance otherwise.
        /// </summary>
        public static FragmentToleranceConfig SearchTolerance(MzCalibrationResult ms2Cal, FragmentToleranceConfig configured)
        {
            if (!ms2Cal.Calibrated)
                return configured;
            MzCalibration.CalibratedTolerance(ms2Cal, configured.Tolerance, configured.Unit,
                out double tolerance, out ToleranceUnit unit);
            if (unit == ToleranceUnit.Ppm && OspreyEnvironment.MinFragmentTolerancePpm.HasValue)
                tolerance = Math.Max(tolerance, OspreyEnvironment.MinFragmentTolerancePpm.Value);
            return new FragmentToleranceConfig { Tolerance = tolerance, Unit = unit };
        }

        /// <summary>
        /// One run: select its confident targets from the first-pass q-value sidecar, measure
        /// them one isolation window at a time on up to <paramref name="maxThreads"/> threads,
        /// and write the summary with <paramref name="stamp"/>. <paramref name="spectra"/> and
        /// <paramref name="ms2Cal"/> are the run's, already loaded by the caller, or null to
        /// load them here.
        /// </summary>
        public static ModelDiagnosticsData.FragmentMassErrorRun WriteRun(string input, string output,
            IReadOnlyDictionary<uint, LibraryEntry> library, HashSet<uint> retainedBaseIds, PipelineContext ctx,
            int maxThreads, ArtifactStamp stamp, SpectraWindowIndex spectra = null, MzCalibrationResult ms2Cal = null)
        {
            var config = ctx.Config;
            string stem = Path.GetFileNameWithoutExtension(input);
            string consumer = OspreyArgNames.Text(OspreyArgNames.MODEL_DIAGNOSTICS);
            var targets = ReadConfidentTargets(input, library, retainedBaseIds, config.RunFdr);
            if (ms2Cal == null)
                ScoringTaskShared.LoadMassCalibrations(input, consumer, out ms2Cal, out _, out _);
            var index = spectra ?? ScoringTaskShared.LoadSpectraForRescore(input, stem, consumer, false, ctx);

            var configured = config.FragmentTolerance;
            var search = SearchTolerance(ms2Cal, configured);
            var measure = new FragmentToleranceConfig { Tolerance = HalfWidth(configured), Unit = configured.Unit };
            var provider = new StreamingWindowSpectraProvider(index, ms2Cal, serialBlockReads: spectra == null);
            var windows = index.IsolationWindows;
            var members = AssignToWindows(targets, windows);
            var perWindow = new (List<double> Errors, List<double> Intensities, int Precursors)[windows.Count];
            Parallel.For(0, windows.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxThreads) }, w =>
            {
                perWindow[w] = members[w].Count == 0
                    ? (new List<double>(), new List<double>(), 0)
                    : MeasureWindow(provider.GetCalibratedWindow((int)Math.Round(windows[w].Center * 10.0)), members[w], measure);
            });

            var header = new ModelDiagnosticsData.FragmentMassErrorRun
            {
                File = stem,
                Unit = configured.Unit == ToleranceUnit.Ppm ? MzCalibration.UNIT_PPM : MzCalibration.UNIT_TH,
                HalfWidth = measure.Tolerance,
                BinCount = BIN_COUNT,
                ConfiguredTolerance = configured.Tolerance,
                SearchTolerance = search.Unit == configured.Unit ? search.Tolerance : double.NaN,
                CalibrationMean = ms2Cal.Calibrated ? ms2Cal.Mean : double.NaN,
                CalibrationSd = ms2Cal.Calibrated ? ms2Cal.SD : double.NaN,
                MaxQ = config.RunFdr,
                TopFragments = TOP_FRAGMENTS,
                ScanRadius = SCAN_RADIUS,
                Precursors = perWindow.Sum(p => p.Precursors),
            };
            var summary = ModelDiagnosticsData.BuildFragmentMassErrorRun(header,
                perWindow.SelectMany(p => p.Errors).ToList(), perWindow.SelectMany(p => p.Intensities).ToList());
            ModelDiagnosticsReport.WriteJson(output, summary, stamp);

            var all = summary.Classes[summary.Classes.Count - 1];
            ctx.LogInfo(LogTag.MODEL_DIAGNOSTICS, string.Format(
                OspreyTasksResources.FragmentMassErrorWriter_WriteRun__0___fragment_mass_error_of__1_N0__target_precursors,
                stem, summary.Precursors, all.Total, all.Mean, all.Sd, all.RobustSd, Math.Abs(all.Mean) + 3 * all.Sd, header.Unit,
                header.SearchTolerance));
            return summary;
        }

        /// <summary>The summary at <paramref name="path"/>, or null when it is absent or unreadable.</summary>
        public static ModelDiagnosticsData.FragmentMassErrorRun ReadRun(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return ModelDiagnosticsReport.ReadJson<ModelDiagnosticsData.FragmentMassErrorRun>(path);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The report's fragment mass-error view: every input's summary that is on disk, in input
        /// order. Null when none is. Each summary is a few kilobytes, so the view is
        /// O(runs x bins) however large the runs.
        /// </summary>
        public static ModelDiagnosticsData.FragmentMassErrorData ReadAll(OspreyConfig config)
        {
            if (config.InputFiles == null)
                return null;
            var runs = config.InputFiles.Select(input => ReadRun(PathFor(input))).Where(r => r != null).ToList();
            if (runs.Count == 0)
                return null;
            return new ModelDiagnosticsData.FragmentMassErrorData { Files = runs };
        }

        /// <summary>
        /// The identities of the per-run artifacts one run's summary reads - its first-pass
        /// q-value sidecar, calibration and spectra cache - so a rewritten input redoes that
        /// run's summary and no other.
        /// </summary>
        public static string RunInputIdentities(string input)
        {
            return @";fragmasserr=" + FORMAT_VERSION.ToString(CultureInfo.InvariantCulture)
                + @";runq1=" + IdentityOrAbsent(FdrScoresSidecar.Pass1Path(input))
                + @";calib=" + IdentityOrAbsent(CalibrationIO.CalibrationPathForInput(input, ArtifactPaths.ResolveOutputDir(input)))
                + @";spectra=" + IdentityOrAbsent(SpectraCache.GetCachePath(input))
                + (File.Exists(SpectraCache.GetDemuxCachePath(input))
                    ? @";demuxspectra=" + IdentityOrAbsent(SpectraCache.GetDemuxCachePath(input))
                    : string.Empty);
        }

        /// <summary>
        /// The run's first-pass observations of library targets at run precursor q &lt;=
        /// <paramref name="maxQ"/> whose spectrum the library still holds. When the first pass's
        /// retained summary is readable, only its base_ids: the library is loaded with fragments
        /// for those alone when the summary is written from disk, so applying it on every route
        /// keeps the in-flight and the later summary the same.
        /// </summary>
        private static List<(LibraryEntry Entry, double ApexRt)> ReadConfidentTargets(string input,
            IReadOnlyDictionary<uint, LibraryEntry> library, HashSet<uint> retainedBaseIds, double maxQ)
        {
            string path = FdrScoresSidecar.Pass1Path(input);
            var targets = new List<(LibraryEntry Entry, double ApexRt)>();
            bool read = FdrScoresSidecar.ReadRecords(path, FdrScoresSidecar.Pass.FirstPass, record =>
            {
                if (!(record.RunPrecursorQvalue <= maxQ) || (record.EntryId & LibraryEntry.DECOY_ID_BIT) != 0)
                    return;
                if (retainedBaseIds != null && !retainedBaseIds.Contains(record.EntryId))
                    return;
                if (!library.TryGetValue(record.EntryId, out var entry) || entry == null || entry.IsDecoy ||
                    entry.IsSpectrumReleased)
                {
                    return;
                }
                targets.Add((entry, record.ApexRt));
            });
            if (!read)
            {
                throw new InvalidDataException(string.Format(
                    OspreyTasksResources.TrainingExportWriter_ReadRunQ_The_intermediate_file___0___is_missing_or_unreadable_, path));
            }
            return targets;
        }

        /// <summary>
        /// Each target placed in the isolation window that holds its precursor m/z, the one
        /// centred nearest it where windows overlap.
        /// </summary>
        private static List<(LibraryEntry Entry, double ApexRt)>[] AssignToWindows(
            List<(LibraryEntry Entry, double ApexRt)> targets, IReadOnlyList<IsolationWindow> windows)
        {
            var members = windows.Select(_ => new List<(LibraryEntry Entry, double ApexRt)>()).ToArray();
            foreach (var target in targets)
            {
                int best = -1;
                for (int w = 0; w < windows.Count; w++)
                {
                    if (!windows[w].Contains(target.Entry.PrecursorMz))
                        continue;
                    if (best < 0 || Math.Abs(windows[w].Center - target.Entry.PrecursorMz) <
                        Math.Abs(windows[best].Center - target.Entry.PrecursorMz))
                    {
                        best = w;
                    }
                }
                if (best >= 0)
                    members[best].Add(target);
            }
            return members;
        }

        /// <summary>
        /// One window's measurements: for each target, the scan nearest its apex RT and the
        /// <see cref="SCAN_RADIUS"/> scans either side, and in each the peak closest to each of
        /// its <see cref="TOP_FRAGMENTS"/> most intense library fragments within the
        /// <paramref name="measure"/> window - its signed error and log10 intensity.
        /// </summary>
        private static (List<double> Errors, List<double> Intensities, int Precursors) MeasureWindow(
            List<Spectrum> windowSpectra, List<(LibraryEntry Entry, double ApexRt)> members,
            FragmentToleranceConfig measure)
        {
            var errors = new List<double>();
            var intensities = new List<double>();
            var spectra = windowSpectra.Where(s => !s.IsEmpty).OrderBy(s => s.RetentionTime).ToArray();
            var rts = spectra.Select(s => s.RetentionTime).ToArray();
            int precursors = 0;
            if (spectra.Length == 0)
                return (errors, intensities, precursors);
            foreach (var (entry, apexRt) in members)
            {
                int apex = NearestIndex(rts, apexRt);
                var fragments = entry.Fragments.OrderByDescending(f => f.RelativeIntensity).ThenBy(f => f.Mz)
                    .Take(TOP_FRAGMENTS).ToArray();
                precursors++;
                for (int s = Math.Max(0, apex - SCAN_RADIUS); s <= Math.Min(spectra.Length - 1, apex + SCAN_RADIUS); s++)
                {
                    var spectrum = spectra[s];
                    foreach (var fragment in fragments)
                    {
                        int peak = ClosestPeak(spectrum, fragment.Mz, measure.ToleranceDa(fragment.Mz));
                        if (peak < 0)
                            continue;
                        float intensity = spectrum.Intensities[peak];
                        if (intensity <= 0)
                            continue;
                        errors.Add(measure.MassError(fragment.Mz, spectrum.Mzs[peak]));
                        intensities.Add(Math.Log10(intensity));
                    }
                }
            }
            return (errors, intensities, precursors);
        }

        /// <summary>The index of the value in sorted <paramref name="rts"/> nearest <paramref name="rt"/>.</summary>
        private static int NearestIndex(double[] rts, double rt)
        {
            int i = Array.BinarySearch(rts, rt);
            if (i >= 0)
                return i;
            i = ~i;
            if (i == 0)
                return 0;
            if (i >= rts.Length)
                return rts.Length - 1;
            return rt - rts[i - 1] <= rts[i] - rt ? i - 1 : i;
        }

        /// <summary>The peak nearest <paramref name="mz"/> within <paramref name="halfWidthDa"/>, or -1.</summary>
        private static int ClosestPeak(Spectrum spectrum, double mz, double halfWidthDa)
        {
            var mzs = spectrum.Mzs;
            int best = -1;
            double bestDelta = double.MaxValue;
            for (int i = spectrum.MzLowerBound(mz - halfWidthDa); i < mzs.Length && mzs[i] <= mz + halfWidthDa; i++)
            {
                double delta = Math.Abs(mzs[i] - mz);
                if (delta < bestDelta)
                {
                    best = i;
                    bestDelta = delta;
                }
            }
            return best;
        }

        private static string IdentityOrAbsent(string path)
        {
            return File.Exists(path) ? SearchIdentity.FileIdentityTerm(path) : @"absent";
        }
    }
}
