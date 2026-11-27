/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on skyline-glimpse (https://github.com/maccoss/skyline-glimpse)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Quasar.Features.Data;
using pwiz.Quasar.Features.Scans;

namespace pwiz.Quasar.Features
{
    /// <summary>Finds the isotope envelopes in one centroided spectrum (Hardklor).</summary>
    public interface IEnvelopeFinder
    {
        /// <summary>
        /// The envelopes in a spectrum with ascending m/z, recorded by <paramref name="analyzer"/> (its peak widths
        /// differ). Called from several threads at once.
        /// </summary>
        IReadOnlyList<IsotopeEnvelope> Find(double[] mz, double[] intensity, MassAnalyzer analyzer);

        /// <summary>False for scans the finder should not search (an ion trap's, which cannot resolve the isotopes of multiply charged ions).</summary>
        bool CanSearch(MassAnalyzer analyzer);
    }

    /// <summary>
    /// Rides on the scan pass: hands every MS1 scan, and every MS2 scan of a DIA run, to worker threads that find
    /// its isotope envelopes while the file is still being read; after the pass, links envelopes across scans
    /// (MS1 scans together, each DIA isolation window on its own) into persistent features. Their peaks are the
    /// peptide-like signal.
    /// </summary>
    /// <remarks>
    /// <para>Profile (not centroided) spectra are left out, as are scans the finder cannot search (an ion trap cannot resolve the isotopes of multiply charged ions).</para>
    /// <para>MS2 scans count only in DIA, where each isolation window repeats every cycle. A window seen for the
    /// first time is held until it comes back; once more than <see cref="MAX_DIA_WINDOWS"/> distinct windows have
    /// appeared the run is DDA (a window per precursor) and MS2 scans are left out.</para>
    /// <para>The pass never waits for the envelope search beyond a bounded queue: <see cref="End"/> returns at
    /// once, and <see cref="Completion"/> finishes when the last scan is searched and linked.</para>
    /// </remarks>
    public sealed class PeptideLikeObserver : IScanObserver, IDisposable
    {
        /// <summary>More distinct MS2 isolation windows than this: DDA, no MS2 envelopes.</summary>
        public const int MAX_DIA_WINDOWS = 1000;

        private const int QUEUE_DEPTH = 256;

        private readonly IEnvelopeFinder _finder;
        private readonly KronikSettings _linking;
        private readonly int _workerCount;
        private readonly CancellationTokenSource _cancel;
        private readonly TaskCompletionSource<PeptideLikeSignal> _completion = new TaskCompletionSource<PeptideLikeSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<int, ScanEnvelopes> _found = new ConcurrentDictionary<int, ScanEnvelopes>();
        private int _failed;
        private readonly Dictionary<(long, long), int> _windowCounts = new Dictionary<(long, long), int>();
        private readonly Dictionary<(long, long), SpectrumData> _firstOfWindow = new Dictionary<(long, long), SpectrumData>();
        private BlockingCollection<SpectrumData> _queue;
        private Task[] _workers = Array.Empty<Task>();
        private readonly List<int> _ms1 = new List<int>();
        private readonly Dictionary<(long, long), List<int>> _ms2ByWindow = new Dictionary<(long, long), List<int>>();
        private ScanHeader[] _headers = Array.Empty<ScanHeader>();
        private bool _dda;

        /// <param name="finder">Finds the envelopes of one spectrum.</param>
        /// <param name="linking">How envelopes are linked across scans.</param>
        /// <param name="workers">Threads searching spectra (default: half the logical processors).</param>
        /// <param name="cancellation">Stops the search when the pass is abandoned.</param>
        public PeptideLikeObserver(IEnvelopeFinder finder, KronikSettings linking = null, int workers = 0, CancellationToken cancellation = default)
        {
            _finder = finder;
            _linking = linking ?? KronikSettings.Default;
            _workerCount = workers > 0 ? workers : Math.Max(1, Environment.ProcessorCount / 2);
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        }

        /// <summary>The peptide-like signal, once every scan has been searched and linked.</summary>
        public Task<PeptideLikeSignal> Completion => _completion.Task;

        public void Begin(int spectrumCount, double rtFirst, double rtLast)
        {
            _headers = new ScanHeader[spectrumCount];
            _queue = new BlockingCollection<SpectrumData>(QUEUE_DEPTH);
            var token = _cancel.Token;
            var queue = _queue;
            _workers = Enumerable.Range(0, _workerCount).Select(_ => Task.Run(() => Search(queue, token), token)).ToArray();
        }

        public void Observe(SpectrumData spectrum)
        {
            var h = spectrum.Header;
            if (h.Index >= 0 && h.Index < _headers.Length)
                _headers[h.Index] = h;
            if (!_finder.CanSearch(h.Analyzer))
                return;   // an ion trap: multiply charged isotopes not resolved, no peptide-like signal for this scan
            if (!h.Centroided)
                return;   // profile points: Hardklor works on centroided peaks
            if (h.MsLevel == 1)
            {
                _ms1.Add(h.Index);
                Enqueue(spectrum);
            }
            else if (h.MsLevel >= 2 && h.HasIsolationWindow && !_dda)
            {
                var window = WindowKey(h);
                if (!_ms2ByWindow.TryGetValue(window, out var series))
                {
                    if (_ms2ByWindow.Count >= MAX_DIA_WINDOWS)
                    {
                        // A window per precursor: DDA. Forget the MS2 scans held or searched so far.
                        _dda = true;
                        _firstOfWindow.Clear();
                        _ms2ByWindow.Clear();
                        return;
                    }
                    _ms2ByWindow[window] = series = new List<int>();
                }
                series.Add(h.Index);
                var seen = _windowCounts[window] = _windowCounts.GetValueOrDefault(window) + 1;
                if (seen == 1)
                {
                    _firstOfWindow[window] = spectrum;   // held until the window comes back
                    return;
                }
                if (seen == 2 && _firstOfWindow.Remove(window, out var first))
                    Enqueue(first);
                Enqueue(spectrum);
            }
        }

        public void End()
        {
            _queue?.CompleteAdding();
            var workers = _workers;
            var ms1 = _ms1.ToArray();
            var dda = _dda;
            // DIA windows: the ones that came back (a window seen once cannot persist anyway).
            var windows = dda ? new List<int[]>() : _ms2ByWindow.Where(p => p.Value.Count > 1).Select(p => p.Value.ToArray()).ToList();
            var headers = _headers;
            _ = Task.Run(() =>
            {
                try
                {
                    // Waits as an await would: the first worker exception unwrapped, or an
                    // OperationCanceledException when the workers were cancelled.
                    Task.WhenAll(workers).GetAwaiter().GetResult();
                    _completion.TrySetResult(Link(headers, ms1, windows, dda));
                }
                catch (OperationCanceledException)
                {
                    _completion.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    _completion.TrySetException(ex);
                }
            });
        }

        /// <summary>Stops the search (an abandoned pass never calls <see cref="End"/>).</summary>
        public void Dispose()
        {
            _cancel.Cancel();
            _queue?.CompleteAdding();
            _completion.TrySetCanceled();
            _cancel.Dispose();
        }

        private void Enqueue(SpectrumData spectrum)
        {
            try
            {
                _queue.Add(spectrum, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                // Abandoned: the pass is stopping too.
            }
        }

        private void Search(BlockingCollection<SpectrumData> queue, CancellationToken token)
        {
            foreach (var spectrum in queue.GetConsumingEnumerable(token))
            {
                // A spectrum the finder cannot handle counts as having no envelopes: a worker that died would leave
                // the bounded queue to fill and the scan pass waiting forever.
                try
                {
                    var envelopes = spectrum.Mz.Length == 0 ? Array.Empty<IsotopeEnvelope>() : _finder.Find(spectrum.Mz, spectrum.Intensity, spectrum.Header.Analyzer);
                    _found[spectrum.Header.Index] = ScanEnvelopes.From(envelopes, spectrum.Intensity);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _found[spectrum.Header.Index] = ScanEnvelopes.Empty;
                    Interlocked.Increment(ref _failed);
                }
            }
        }

        private PeptideLikeSignal Link(ScanHeader[] headers, int[] ms1, List<int[]> windows, bool dda)
        {
            var series = new List<int[]> { ms1 };
            series.AddRange(windows);
            var featureOf = new Dictionary<int, int[]>();
            var features = new List<(int Series, PersistentFeature Feature)>();
            for (var s = 0; s < series.Count; s++)
            {
                var scans = series[s];
                var keys = scans.Select(i => _found.TryGetValue(i, out var e) ? e.Keys : Array.Empty<EnvelopeKey>()).ToArray();
                var result = Kronik.Link(keys, _linking);
                var offset = features.Count;
                features.AddRange(result.Features.Select(f => (s, f)));
                for (var k = 0; k < scans.Length; k++)
                    featureOf[scans[k]] = result.FeatureOf[k].Select(id => id < 0 ? -1 : id + offset).ToArray();
            }
            return new PeptideLikeSignal(headers, new Dictionary<int, ScanEnvelopes>(_found), featureOf, features, series, dda)
            {
                FailedScans = Volatile.Read(ref _failed),
            };
        }

        /// <summary>An isolation window to 0.01 m/z: DIA repeats it exactly.</summary>
        private static (long, long) WindowKey(in ScanHeader h) =>
            ((long)Math.Round(h.IsolationLower * 100), (long)Math.Round(h.IsolationUpper * 100));
    }

    /// <summary>
    /// The envelopes found in one scan, kept compact: what linking needs, and which of the scan's peaks each
    /// envelope covers (as positions in <see cref="PeakIntensity"/>, the envelopes' distinct peaks).
    /// </summary>
    public sealed class ScanEnvelopes
    {
        public static ScanEnvelopes Empty { get; } = new ScanEnvelopes(Array.Empty<EnvelopeKey>(), Array.Empty<EnvelopeDetail>(),
            Array.Empty<int>(), Array.Empty<float>(), new[] { 0 }, Array.Empty<ushort>());

        public static ScanEnvelopes From(IReadOnlyList<IsotopeEnvelope> envelopes, double[] intensity)
        {
            if (envelopes.Count == 0)
                return Empty;
            var distinct = envelopes.SelectMany(e => e.PeakIndices).Distinct().Order().ToArray();
            return Build(
                envelopes.Select(e => new EnvelopeKey(e.MonoMass, e.Charge, e.Intensity)).ToArray(),
                envelopes.Select(e => new EnvelopeDetail(e.BasePeakMz, (float)e.Correlation, e.LowMz, e.HighMz)).ToArray(),
                envelopes.Select(e => e.PeakIndices).ToArray(),
                distinct, distinct.Select(i => (float)intensity[i]).ToArray());
        }

        /// <summary>The compact form, from each envelope's peak indices and the scan's distinct envelope peaks.</summary>
        internal static ScanEnvelopes Build(EnvelopeKey[] keys, EnvelopeDetail[] details, int[][] peaksOfEach, int[] distinct, float[] distinctIntensity)
        {
            if (distinct.Length > ushort.MaxValue)
                throw new InvalidOperationException($@"{distinct.Length} envelope peaks in one spectrum.");
            var start = new int[keys.Length + 1];
            var members = new ushort[peaksOfEach.Sum(p => p.Length)];
            var k = 0;
            for (var e = 0; e < keys.Length; e++)
            {
                start[e] = k;
                foreach (var p in peaksOfEach[e])
                    members[k++] = (ushort)Array.BinarySearch(distinct, p);
            }
            start[^1] = k;
            return new ScanEnvelopes(keys, details, distinct, distinctIntensity, start, members);
        }

        internal ScanEnvelopes(EnvelopeKey[] keys, EnvelopeDetail[] details, int[] peakIndex, float[] peakIntensity, int[] start, ushort[] members)
        {
            Keys = keys;
            Details = details;
            PeakIndex = peakIndex;
            PeakIntensity = peakIntensity;
            Start = start;
            Members = members;
        }

        /// <summary>Mass, charge and intensity of each envelope, in the order found.</summary>
        public EnvelopeKey[] Keys { get; }

        /// <summary>The rest of each envelope's Hardklor record (base peak, correlation, m/z range).</summary>
        public EnvelopeDetail[] Details { get; }

        /// <summary>The distinct peaks the envelopes cover: their index in the spectrum, ascending.</summary>
        public int[] PeakIndex { get; }

        /// <summary>The intensity of each of those peaks.</summary>
        public float[] PeakIntensity { get; }

        /// <summary>Where each envelope's peaks start in <see cref="Members"/> (one more entry than envelopes).</summary>
        internal int[] Start { get; }

        /// <summary>Each envelope's peaks, as positions in <see cref="PeakIndex"/>.</summary>
        internal ushort[] Members { get; }

        /// <summary>Positions (in <see cref="PeakIndex"/>) of envelope <paramref name="envelope"/>'s peaks.</summary>
        public ReadOnlySpan<ushort> PeaksOf(int envelope) => Members.AsSpan(Start[envelope], Start[envelope + 1] - Start[envelope]);
    }

    /// <summary>An envelope's base peak m/z, correlation with its model, and m/z range (Hardklor's report).</summary>
    public readonly record struct EnvelopeDetail(double BasePeakMz, float Correlation, double LowMz, double HighMz);

    /// <summary>
    /// The peptide-like signal of a run: per scan, the intensity of the peaks belonging to persistent isotope
    /// envelopes, and the features themselves.
    /// </summary>
    public sealed class PeptideLikeSignal
    {
        private readonly ScanHeader[] _headers;
        private readonly IReadOnlyDictionary<int, ScanEnvelopes> _envelopes;
        private readonly IReadOnlyDictionary<int, int[]> _featureOf;
        private readonly Dictionary<int, double[]> _intensityByMinCharge = new Dictionary<int, double[]>();

        internal PeptideLikeSignal(
            ScanHeader[] headers, IReadOnlyDictionary<int, ScanEnvelopes> envelopes, IReadOnlyDictionary<int, int[]> featureOf,
            IReadOnlyList<(int Series, PersistentFeature Feature)> features, IReadOnlyList<int[]> series, bool dda,
            double[] savedAll = null, double[] savedMultiplyCharged = null)
        {
            _headers = headers;
            _envelopes = envelopes;
            _featureOf = featureOf;
            Features = features;
            Series = series;
            IsDda = dda;
            if (savedAll != null && savedMultiplyCharged != null)
            {
                // Read from a .hk file, which keeps each scan's totals but not the envelopes' peaks.
                HasPeaks = false;
                _intensityByMinCharge[1] = savedAll;
                _intensityByMinCharge[2] = savedMultiplyCharged;
            }
        }

        /// <summary>
        /// True when each envelope's peaks are known (a search made in this session). A signal read from a .hk file
        /// has each scan's totals for MS1 1+ and 2+ only, and no peaks.
        /// </summary>
        public bool HasPeaks { get; } = true;

        /// <summary>The persistent features and the series (0: MS1; then each DIA window) each was found in.</summary>
        public IReadOnlyList<(int Series, PersistentFeature Feature)> Features { get; }

        /// <summary>Scan indices of each series: MS1 first, then each DIA isolation window.</summary>
        public IReadOnlyList<int[]> Series { get; }

        /// <summary>True when MS2 scans were left out because the run is DDA.</summary>
        public bool IsDda { get; }

        /// <summary>Scans the envelope finder failed on (counted as having no envelopes).</summary>
        public int FailedScans { get; internal init; }

        internal ScanHeader[] Headers => _headers;

        internal IReadOnlyDictionary<int, ScanEnvelopes> Envelopes => _envelopes;

        internal IReadOnlyDictionary<int, int[]> FeatureOf => _featureOf;

        /// <summary>Envelopes found, and isotope peaks they cover, over all searched scans.</summary>
        public (long Envelopes, long PeakRefs) Counts =>
            (_envelopes.Values.Sum(e => (long)e.Keys.Length), _envelopes.Values.Sum(e => (long)Enumerable.Range(0, e.Keys.Length).Sum(k => e.PeaksOf(k).Length)));

        /// <summary>True when the scan was searched for envelopes (MS1 scans, and DIA MS2 scans, not from an ion trap).</summary>
        public bool Covers(int scan) => _featureOf.ContainsKey(scan);

        /// <summary>True when any scan was searched (false for a run read entirely in an ion trap).</summary>
        public bool CoversAny => _featureOf.Count > 0;

        /// <summary>
        /// Per scan of the run, the summed intensity of its peaks in persistent envelopes (each peak once); NaN for
        /// a scan not searched. MS1 envelopes below <paramref name="ms1MinCharge"/> do not count; MS2 envelopes of
        /// any charge do (fragments are mostly 1+).
        /// </summary>
        public double[] IntensityByScan(int ms1MinCharge = 1)
        {
            lock (_intensityByMinCharge)
            {
                if (_intensityByMinCharge.TryGetValue(ms1MinCharge, out var cached))
                    return cached;
                if (!HasPeaks)
                    throw new InvalidOperationException($@"A saved search keeps MS1 totals for 1+ and 2+ only, not {ms1MinCharge}+.");
                var result = new double[_headers.Length];
                Array.Fill(result, double.NaN);
                foreach (var (scan, ids) in _featureOf)
                    result[scan] = PeptideLikeIntensity(scan, ids, _headers[scan].MsLevel == 1 ? ms1MinCharge : 1);
                return _intensityByMinCharge[ms1MinCharge] = result;
            }
        }

        /// <summary>Indices (in the scan's spectrum) of its peaks in persistent envelopes, ascending; empty if none.</summary>
        public int[] PeptideLikePeaks(int scan, int ms1MinCharge = 1)
        {
            if (!_featureOf.TryGetValue(scan, out var ids) || !_envelopes.TryGetValue(scan, out var env))
                return Array.Empty<int>();
            var minCharge = _headers[scan].MsLevel == 1 ? ms1MinCharge : 1;
            var covered = Covered(env, ids, minCharge);
            return Enumerable.Range(0, covered.Length).Where(i => covered[i]).Select(i => env.PeakIndex[i]).ToArray();
        }

        private double PeptideLikeIntensity(int scan, int[] ids, int minCharge)
        {
            if (!_envelopes.TryGetValue(scan, out var env))
                return 0;
            var covered = Covered(env, ids, minCharge);
            var sum = 0.0;
            for (var i = 0; i < covered.Length; i++)
            {
                if (covered[i])
                    sum += env.PeakIntensity[i];
            }
            return sum;
        }

        private static bool[] Covered(ScanEnvelopes env, int[] ids, int minCharge)
        {
            var covered = new bool[env.PeakIndex.Length];
            for (var e = 0; e < ids.Length; e++)
            {
                if (ids[e] < 0 || env.Keys[e].Charge < minCharge)
                    continue;
                foreach (var p in env.PeaksOf(e))
                    covered[p] = true;
            }
            return covered;
        }
    }
}
