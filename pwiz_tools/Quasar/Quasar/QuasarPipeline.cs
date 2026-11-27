/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Quasar.Core.Clustering;
using pwiz.Quasar.Core.Elution;
using pwiz.Quasar.Core.Output;
using pwiz.Quasar.Core.Traces;
using pwiz.Quasar.Features;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.IO;

namespace pwiz.Quasar
{
    /// <summary>What one run of the pipeline reads, writes and processes.</summary>
    public sealed class PipelineSettings
    {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }

        /// <summary>First and last window index to process (inclusive); null for all windows.</summary>
        public int? FirstWindow { get; set; }
        public int? LastWindow { get; set; }

        public int Threads { get; set; } = Environment.ProcessorCount;

        /// <summary>Windows sampled, evenly across the run, to measure the run's peak shape (spec 2a).</summary>
        public int PriorWindowCount { get; set; } = 6;

        /// <summary>The command line, recorded in the output.</summary>
        public string CommandLine { get; set; }

        /// <summary>When set, each window's Stage 1 traces are written here (<see cref="TraceFile"/>).</summary>
        public string SaveTracesDirectory { get; set; }

        /// <summary>Stop after Stage 1: build (and save) the traces, cluster nothing.</summary>
        public bool TracesOnly { get; set; }

        /// <summary>
        /// Osprey's blib for the experiment: this run's passing precursors become known templates,
        /// so only the signal they leave unexplained forms new clusters (<c>--features
        /// unassigned</c>). Null clusters all of it (<c>--features all</c>).
        /// </summary>
        public string OspreyBlibPath { get; set; }
    }

    /// <summary>
    /// Stages 1 to 4 for one run: read it, measure its peak shape, then for each isolation window
    /// find isotope envelopes (Hardklor), build traces, cluster them template first, and write one
    /// pseudo-spectrum per accepted cluster to mzML, searched with the isolation window as the
    /// precursor tolerance. Clusters with precursor candidates (tiers 1 and 2) are also written,
    /// one copy per candidate, to a second mzML (<see cref="PrecursorPath"/>) for a search with a
    /// narrow precursor tolerance: Sage reads only a spectrum's first precursor.
    /// </summary>
    public sealed class QuasarPipeline
    {
        /// <summary>tau / sigma assumed for the run's prior until it is measured from pooled fits.</summary>
        private const double PRIOR_TAU_OVER_SIGMA = 0.5;

        /// <summary>Gaussian FWHM / sigma.</summary>
        private const double FWHM_PER_SIGMA = 2.3548;

        /// <summary>Anchor traces with fewer non-zero scans do not measure the peak width.</summary>
        private const int MIN_WIDTH_SCANS = 5;

        /// <summary>Isolation targets closer than this (m/z) are the same window.</summary>
        private const double SCHEME_TOLERANCE = 0.01;

        private readonly PipelineSettings _settings;
        private readonly TextWriter _log;
        private readonly object _logLock = new object();
        private int _scansWithoutInjectionTime;

        public QuasarPipeline(PipelineSettings settings, TextWriter log)
        {
            _settings = settings;
            _log = log;
        }

        public void Run()
        {
            var total = Stopwatch.StartNew();
            Log(QuasarResources.Pipeline_Reading__0_, _settings.InputPath);
            var read = Stopwatch.StartNew();

            // Learn the isolation scheme first, so only the windows being processed are held. MS1
            // is read for the precursor tiers.
            var scheme = RunReader.ReadWindowTargets(_settings.InputPath);
            var selectedTargets = scheme
                .Where((_, i) => i >= (_settings.FirstWindow ?? 0) && i <= (_settings.LastWindow ?? int.MaxValue))
                .ToArray();
            var run = RunReader.Read(_settings.InputPath,
                target => selectedTargets.Any(t => Math.Abs(t - target) < SCHEME_TOLERANCE), !_settings.TracesOnly);
            run = WithSchemeIndices(run, scheme);
            Log(QuasarResources.Pipeline_Read__0__MS1_and__1__MS2_spectra_in__2__isolation_windows___3_F0__s_,
                run.Ms1.Count, run.Windows.Sum(w => w.Scans.Count), run.Windows.Count, read.Elapsed.TotalSeconds);

            var windows = run.Windows.ToList();
            var finder = new AnalyzerEnvelopeFinder(new PwizEnvelopeModel());
            var traceSettings = new TraceBuilderSettings();

            var (prior, cycle, fwhm) = MeasureRunShape(run, finder, traceSettings);
            Log(QuasarResources.Pipeline_Run_shape__FWHM__0_F2__s__sigma__1_F4__min__tau__2_F4__min___3_F2__s_between_scans_of_a_window,
                fwhm * 60.0, prior.Sigma, prior.Tau, cycle * 60.0);
            var clusterSettings = new WindowClusterSettings(prior, new NoiseModel(1.0, 1.0), cycle);
            string stem = Path.GetFileNameWithoutExtension(_settings.InputPath);
            var known = ReadKnownPrecursors(stem);

            var ms1 = _settings.TracesOnly ? new List<WindowScan>() : BuildMs1Scans(run);
            var ms1Rt = ms1.Select(s => s.RetentionTime).ToArray();
            var precursorSpectra = new ConcurrentBag<(int Window, int Template, int Order, PseudoSpectrum Spectrum)>();
            int clustersWithCandidate = 0, tier1Count = 0, tier2Count = 0;

            var spectra = new ConcurrentBag<(int Window, int Template, PseudoSpectrum Spectrum)>();
            Parallel.ForEach(windows, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _settings.Threads) },
                window =>
                {
                    var watch = Stopwatch.StartNew();
                    var scans = BuildScans(run, window, finder);
                    double hardklorSeconds = watch.Elapsed.TotalSeconds;
                    var traces = TraceBuilder.Build(scans, traceSettings);
                    double traceSeconds = watch.Elapsed.TotalSeconds - hardklorSeconds;
                    var rt = scans.Select(s => s.RetentionTime).ToArray();
                    if (_settings.SaveTracesDirectory != null)
                    {
                        string name = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            @"{0}.w{1:D3}.traces.bin", stem, window.Index);
                        TraceFile.Write(Path.Combine(_settings.SaveTracesDirectory, name), window.Target,
                            window.LowerOffset, window.UpperOffset, rt, traces);
                    }

                    if (_settings.TracesOnly)
                    {
                        Log(QuasarResources.Pipeline_Window__0__time__Hardklor__1_F1__s__traces__2_F1__s__clustering___3_,
                            window.Index, hardklorSeconds, traceSeconds, string.Empty);
                        return;
                    }
                    double low = window.Target - window.LowerOffset;
                    double high = window.Target + window.UpperOffset;
                    var clusterer = new WindowClusterer(clusterSettings);
                    var knownShapes = KnownTemplates.Fit(rt, traces, known.Where(p => low <= p.Mz && p.Mz < high),
                        clusterer, clusterSettings);
                    var result = clusterer.Run(rt, traces, knownShapes);
                    Log(QuasarResources.Pipeline_Window__0__time__Hardklor__1_F1__s__traces__2_F1__s__clustering___3_,
                        window.Index, hardklorSeconds, traceSeconds, result.Profile);
                    var ms1Traces = Ms1Traces(ms1, low, high, traceSettings);
                    int kept = 0;
                    foreach (var cluster in result.Clusters.Where(c => c.IsAccepted))
                    {
                        spectra.Add((window.Index, cluster.Template.Id, ToPseudoSpectrum(stem, window, rt, cluster, null, null)));
                        kept++;

                        // Sage reads only a spectrum's first precursor, so each candidate gets its own
                        // copy of the cluster (spec, Phase 1).
                        var tier1 = PrecursorFinder.FromMs1(cluster, ms1Rt, ms1Traces, low, high, clusterer, clusterSettings);
                        var tier2 = PrecursorFinder.UnfragmentedInMs2(cluster, low, high);
                        int order = 0;
                        foreach (var candidate in tier1)
                            precursorSpectra.Add((window.Index, cluster.Template.Id, order++,
                                ToPseudoSpectrum(stem, window, rt, cluster, candidate, null)));
                        foreach (var (member, candidate) in tier2)
                            precursorSpectra.Add((window.Index, cluster.Template.Id, order++,
                                ToPseudoSpectrum(stem, window, rt, cluster, candidate, member)));
                        if (order > 0)
                            Interlocked.Increment(ref clustersWithCandidate);
                        Interlocked.Add(ref tier1Count, tier1.Count);
                        Interlocked.Add(ref tier2Count, tier2.Count);
                    }

                    Log(QuasarResources.Pipeline_Window__0____1_F2__m_z___2__scans____3__anchor_and__4__centroid_traces___5__templates___6__clusters_kept___7_F1__s_,
                        window.Index, window.Target, scans.Count, traces.Count(t => t.IsAnchor),
                        traces.Count(t => !t.IsAnchor), result.Templates.Count, kept, watch.Elapsed.TotalSeconds);
                });

            if (_scansWithoutInjectionTime > 0)
                Log(QuasarResources.Pipeline_Scans_without_injection_time__0_, _scansWithoutInjectionTime);

            var ordered = spectra.OrderBy(s => s.Window).ThenBy(s => s.Template).Select(s => s.Spectrum).ToList();
            PseudoSpectrumMzmlWriter.Write(_settings.OutputPath, ordered, _settings.InputPath, Program.Version,
                _settings.CommandLine ?? string.Empty);
            if (!_settings.TracesOnly)
            {
                var withPrecursors = precursorSpectra.OrderBy(s => s.Window).ThenBy(s => s.Template).ThenBy(s => s.Order)
                    .Select(s => s.Spectrum).ToList();
                string precursorPath = PrecursorPath(_settings.OutputPath);
                PseudoSpectrumMzmlWriter.Write(precursorPath, withPrecursors, _settings.InputPath, Program.Version,
                    _settings.CommandLine ?? string.Empty);
                Log(QuasarResources.Pipeline_Wrote__0__precursor_spectra_to__1____2__of__3__clusters_have_a_candidate___4__tier_1___5__tier_2__,
                    withPrecursors.Count, precursorPath, clustersWithCandidate, ordered.Count, tier1Count, tier2Count);
            }
            Log(QuasarResources.Pipeline_Wrote__0__pseudo_spectra_to__1____2_F0__s_in_all_, ordered.Count,
                _settings.OutputPath, total.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// Spec 2a: the run's typical peak, from the anchor traces of a few windows spread over the
        /// run: the median FWHM of the anchors' apex peaks, and the median scan spacing.
        /// </summary>
        private (EmgShapePrior Prior, double Cycle, double Fwhm) MeasureRunShape(RunData run, AnalyzerEnvelopeFinder finder,
            TraceBuilderSettings traceSettings)
        {
            var sample = Enumerable.Range(0, _settings.PriorWindowCount)
                .Select(i => run.Windows[(int)((i + 0.5) * run.Windows.Count / _settings.PriorWindowCount)])
                .Distinct()
                .ToList();
            var widths = new ConcurrentBag<double>();
            var cycles = new ConcurrentBag<double>();
            Parallel.ForEach(sample, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _settings.Threads) },
                window =>
                {
                    var scans = BuildScans(run, window, finder);
                    var rt = scans.Select(s => s.RetentionTime).ToArray();
                    for (int i = 1; i < rt.Length; i++)
                        cycles.Add(rt[i] - rt[i - 1]);
                    foreach (var trace in TraceBuilder.Build(scans, traceSettings).Where(t => t.IsAnchor))
                    {
                        double width = HalfHeightWidth(rt, trace.Intensities);
                        if (!double.IsNaN(width))
                            widths.Add(width);
                    }
                });

            double cycle = Median(cycles.ToList());
            double fwhm = widths.Count > 0 ? Median(widths.ToList()) : 4.0 * cycle;
            double sigma = fwhm / FWHM_PER_SIGMA;
            return (new EmgShapePrior(sigma, PRIOR_TAU_OVER_SIGMA * sigma), cycle, fwhm);
        }

        /// <summary>
        /// The run with each window numbered by its place in the full isolation scheme, so window
        /// numbers in logs and spectrum ids do not depend on which windows were processed.
        /// </summary>
        private static RunData WithSchemeIndices(RunData run, IReadOnlyList<double> scheme)
        {
            var windows = run.Windows.Select(w =>
            {
                int index = Enumerable.Range(0, scheme.Count).OrderBy(i => Math.Abs(scheme[i] - w.Target)).FirstOrDefault();
                return new WindowScans(index, w.Target, w.LowerOffset, w.UpperOffset, w.Scans);
            }).ToList();
            return new RunData(run.SourcePath, run.InstrumentModel, run.Ms1, windows);
        }

        /// <summary>Hardklor on each scan of a window, and the ion conversion of its intensities.</summary>
        private List<WindowScan> BuildScans(RunData run, WindowScans window, AnalyzerEnvelopeFinder finder)
        {
            return window.Scans.Select(spectrum => ToWindowScan(run, spectrum, finder)).ToList();
        }

        /// <summary>This run's precursors from <see cref="PipelineSettings.OspreyBlibPath"/>; none in <c>--features all</c>.</summary>
        private List<KnownPrecursor> ReadKnownPrecursors(string runName)
        {
            if (_settings.OspreyBlibPath == null)
                return new List<KnownPrecursor>();
            var known = OspreyRunReader.Read(_settings.OspreyBlibPath, runName);
            if (known == null)
                throw new InvalidDataException(string.Format(QuasarResources.Pipeline__0__has_no_run__1__, _settings.OspreyBlibPath, runName));
            Log(QuasarResources.Pipeline_Known_templates___0__Osprey_precursors_at_1__in_this_run__from__1__, known.Count,
                _settings.OspreyBlibPath);
            return known;
        }

        /// <summary>The MS1 scans in ions, for the precursor tiers; no envelopes are searched (see <see cref="PrecursorFinder.FromMs1"/>).</summary>
        private List<WindowScan> BuildMs1Scans(RunData run)
        {
            var watch = Stopwatch.StartNew();
            var scans = run.Ms1.Select(spectrum => ToWindowScan(run, spectrum, null)).ToList();
            Log(QuasarResources.Pipeline_MS1___0__scans_for_the_precursor_tiers___1_F0__s__, scans.Count, watch.Elapsed.TotalSeconds);
            return scans;
        }

        /// <summary>The MS1 centroid traces of the peaks that can belong to a precursor candidate for the isolation window [<paramref name="low"/>, <paramref name="high"/>].</summary>
        private static List<Core.Clustering.Trace> Ms1Traces(IReadOnlyList<WindowScan> ms1, double low, double high,
            TraceBuilderSettings traceSettings)
        {
            var (from, to) = PrecursorFinder.Ms1Range(low, high);
            var scans = ms1.Select(s =>
            {
                int first = LowerBound(s.Mzs, from);
                int count = LowerBound(s.Mzs, to) - first;
                var mzs = new double[count];
                var intensities = new double[count];
                Array.Copy(s.Mzs, first, mzs, 0, count);
                Array.Copy(s.Intensities, first, intensities, 0, count);
                return new WindowScan(s.RetentionTime, s.IonsPerIntensity, mzs, intensities, Array.Empty<IsotopeEnvelope>());
            }).ToList();
            return TraceBuilder.Build(scans, traceSettings);
        }

        /// <summary>A scan in ions, with Hardklor's envelopes when <paramref name="finder"/> is given.</summary>
        private WindowScan ToWindowScan(RunData run, SpectrumData spectrum, AnalyzerEnvelopeFinder finder)
        {
            var header = spectrum.Header;
            var analyzer = header.Analyzer == MassAnalyzer.unknown ? MassAnalyzer.orbitrap : header.Analyzer;
            var envelopes = finder != null && finder.CanSearch(analyzer)
                ? finder.Find(spectrum.Mz, spectrum.Intensity, analyzer)
                : Array.Empty<IsotopeEnvelope>();
            double ionsPerIntensity = IonCalibration.IonsPerIntensity(header.InjectionTimeMs,
                IonCalibration.Alpha(run.InstrumentModel, analyzer));
            if (double.IsNaN(ionsPerIntensity))
            {
                Interlocked.Increment(ref _scansWithoutInjectionTime);
                ionsPerIntensity = 1.0;
            }

            return new WindowScan(header.RetentionTime, ionsPerIntensity, spectrum.Mz, spectrum.Intensity, envelopes);
        }

        /// <summary>The precursor-candidate mzML beside <paramref name="outputPath"/>: X.quasar.mzML gives X.quasar.precursors.mzML.</summary>
        public static string PrecursorPath(string outputPath)
        {
            return Path.ChangeExtension(outputPath, @".precursors.mzML");
        }

        /// <summary>
        /// One accepted cluster as a pseudo-spectrum (spec Stages 3e and 4): its members' areas at
        /// their m/z, with boundaries from the median CWT of the members near the template's apex.
        /// </summary>
        private static PseudoSpectrum ToPseudoSpectrum(string stem, WindowScans window, double[] rt, Cluster cluster,
            PrecursorCandidate candidate, ClusterMember precursorMember)
        {
            var shape = cluster.Template.Shape;
            var (start, end) = Boundaries(rt, cluster);

            // A tier 2 precursor is a member, not a fragment.
            var members = cluster.Members.Where(m => !ReferenceEquals(m, precursorMember)).ToList();
            var userParams = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>(@"start_rt", start),
                new KeyValuePair<string, double>(@"end_rt", end),
                new KeyValuePair<string, double>(@"emg_mu", shape.Mu),
                new KeyValuePair<string, double>(@"emg_sigma", shape.Sigma),
                new KeyValuePair<string, double>(@"emg_tau", shape.Tau),
                new KeyValuePair<string, double>(@"n_members", members.Count),
                new KeyValuePair<string, double>(@"n_anchors", cluster.AnchorCount),
                new KeyValuePair<string, double>(@"n_shared", cluster.SharedCount)
            };
            string id = string.Format(System.Globalization.CultureInfo.InvariantCulture, @"run={0} window={1} cluster={2}",
                stem, window.Index, cluster.Template.Id);
            var precursors = new List<PrecursorCandidate>();
            if (candidate != null)
            {
                id += string.Format(System.Globalization.CultureInfo.InvariantCulture, @" precursor={0:F4}/{1}",
                    candidate.Mz, candidate.Charge);
                precursors.Add(candidate);
            }

            return new PseudoSpectrum(id, shape.Mode, window.Target, window.LowerOffset, window.UpperOffset,
                precursors, members.Select(m => m.Trace.Mz).ToArray(),
                members.Select(m => m.Amplitude).ToArray(), userParams);
        }

        /// <summary>
        /// The median CWT boundaries of the cluster's members (Osprey's consensus detector), keeping
        /// the consensus peak whose apex is within half a peak width of the template's apex; the
        /// template's own 5% height crossings when there is none.
        /// </summary>
        private static (double Start, double End) Boundaries(double[] rt, Cluster cluster)
        {
            var shape = cluster.Template.Shape;
            var (left, right) = shape.HeightCrossings(TemplateFit.SUPPORT_FRACTION);
            var (halfLeft, halfRight) = shape.HeightCrossings(0.5);
            double fwhm = halfRight - halfLeft;
            double span = right - left;
            int from = Math.Max(0, LowerBound(rt, left - span));
            int to = Math.Min(rt.Length - 1, LowerBound(rt, right + span));
            int n = to - from + 1;
            if (n >= 5 && cluster.Members.Count >= 2)
            {
                var times = new double[n];
                Array.Copy(rt, from, times, 0, n);
                var xics = cluster.Members.Select((m, i) =>
                {
                    var values = new double[n];
                    Array.Copy(m.Trace.Intensities, from, values, 0, n);
                    return new XicData(i, times, values);
                }).ToList();
                double mode = shape.Mode;
                var peak = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0)
                    .Where(p => Math.Abs(p.ApexRt - mode) <= 0.5 * fwhm)
                    .OrderBy(p => Math.Abs(p.ApexRt - mode))
                    .FirstOrDefault();
                if (peak != null)
                    return (peak.StartRt, peak.EndRt);
            }

            return (left, right);
        }

        private void Log(string format, params object[] args)
        {
            lock (_logLock)
            {
                _log.WriteLine(format, args);
                _log.Flush();
            }
        }

        /// <summary>Width at half height of the highest peak of a trace, interpolated; NaN when not measurable.</summary>
        private static double HalfHeightWidth(double[] rt, double[] values)
        {
            if (values.Count(v => v > 0.0) < MIN_WIDTH_SCANS)
                return double.NaN;
            int apex = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[apex])
                    apex = i;
            }

            double half = values[apex] / 2.0;
            int l = apex;
            while (l > 0 && values[l - 1] >= half)
                l--;
            int r = apex;
            while (r < values.Length - 1 && values[r + 1] >= half)
                r++;
            if (l == 0 || r == values.Length - 1)
                return double.NaN;
            double left = rt[l - 1] + (half - values[l - 1]) / (values[l] - values[l - 1]) * (rt[l] - rt[l - 1]);
            double right = rt[r] + (values[r] - half) / (values[r] - values[r + 1]) * (rt[r + 1] - rt[r]);
            return right - left;
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0)
                return double.NaN;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : 0.5 * (values[mid - 1] + values[mid]);
        }

        private static int LowerBound(double[] values, double x)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (values[mid] < x)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
