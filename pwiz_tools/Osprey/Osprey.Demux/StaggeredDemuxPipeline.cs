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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.Osprey.Centroid;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// The stepped staggered scheme's part of <see cref="DemuxPipeline"/> (Orbitrap, Astral; see
    /// <see cref="StaggeredDemuxPipeline"/>): the same fragment channels and weighted per-channel solve as the
    /// scanning demultiplexer, the other windows interpolated to each spectrum's time. Each MS2 spectrum becomes one
    /// spectrum per narrow bin its window covers; MS1 spectra pass through.
    /// </summary>
    internal sealed class StaggeredDemuxPlan : DemuxPlan
    {
        private readonly IDemuxSource _source;
        private readonly ScanningDemuxOptions _options;
        private readonly int[] _ms2Spectra;           // input index of each MS2, in acquisition order
        private readonly double[] _ms2Time;           // its time, in minutes
        private readonly double[] _intensityPerIon;   // intensity units of one ion, by input index
        private readonly int[] _cycleOfMs2;           // the cycle it belongs to
        private readonly int[] _passOfMs2;            // the pass it belongs to: one set of windows tiling the range
        private readonly Dictionary<int, double> _passPerIon = new Dictionary<int, double>();   // joint: a pass's ion scale
        private readonly double _referencePerIon;     // per-channel, fill-time data: the run's one ion scale, or NaN
        private readonly TextWriter _log;
        private LinearGrid _grid;                     // joint: the profile grid, from the first batch
        private CentrixCentroider _centrix;           // joint: centroids each bin's share, on the grid
        private readonly Dictionary<int, int> _ms2OfSpectrum = new Dictionary<int, int>();
        private readonly HashSet<int> _thinnedOut = new HashSet<int>();   // MS2 spectra dropped by ThinCycles
        private readonly List<(int Spectrum, int Bin)> _output = new List<(int, int)>();
        private readonly Dictionary<(int, int), int> _outputOf = new Dictionary<(int, int), int>();
        private readonly int _cycles;

        public StaggeredDemuxPlan(IDemuxSource source, ScanningDemuxOptions options, TextWriter log, double minimumBinWidth)
        {
            _source = source;
            _options = options;
            _log = log;
            var ms2 = new List<int>();
            var times = new List<double>();
            var windows = new List<IsolationWindow>();
            _intensityPerIon = new double[source.Count];
            // Described on the read threads (a source serializes what its reader cannot serve at once), then used in order.
            var infos = new DemuxSpectrumInfo[source.Count];
            Parallel.For(0, source.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.ReadThreads) },
                k => infos[k] = source.Describe(k));
            for (int k = 0; k < source.Count; k++)
            {
                var info = infos[k];
                // One ion is alpha x 1000 / fill time (ms) intensity units, alpha the analyzer's ion response
                // (IonResponse: about 1 for an ion trap or an Astral, 10 for an Orbitrap); else a constant.
                double alpha = info.IonResponse > 0 ? info.IonResponse : options.DefaultIonResponse;
                double perIon = options.IonsFromFillTime ? IonResponse.IntensityPerIon(alpha, info.InjectionTime) : double.NaN;
                _intensityPerIon[k] = perIon > 0 ? perIon : options.CountsPerIon;
                if (info.MsLevel == 1)
                    continue;
                windows.Add(info.Window);
                ms2.Add(k);
                times.Add(info.Time);
            }
            if (options.ThinCycles > 1)
                Thin(ms2, times, windows, options.ThinCycles, minimumBinWidth);
            _ms2Spectra = ms2.ToArray();
            _ms2Time = times.ToArray();
            for (int i = 0; i < _ms2Spectra.Length; i++)
                _ms2OfSpectrum[_ms2Spectra[i]] = i;
            Scheme = DemuxSchemeDetector.Detect(windows, minimumBinWidth);

            // A pass is one set of windows tiling the range, acquired in ascending order: a window below the one
            // before it starts the next pass. The joint solve demultiplexes pass by pass, each in ions at the
            // median fill time of its spectra (the ion trap's intensities are rates).
            _passOfMs2 = new int[_ms2Spectra.Length];
            int pass = 0;
            for (int i = 1; i < _ms2Spectra.Length; i++)
            {
                if (windows[i].Center < windows[i - 1].Center)
                    pass++;
                _passOfMs2[i] = pass;
            }
            // The joint solve takes whole passes into a block, so its cycles end only where a pass starts: a pass
            // that a window's repeat would split (an input that starts mid-pass) would otherwise be solved, and
            // written, by two blocks.
            _cycleOfMs2 = CycleNumbers(Scheme.WindowOfSpectrum, options.Joint ? _passOfMs2 : null, out _cycles);
            if (options.Joint)
            {
                foreach (var group in Enumerable.Range(0, _ms2Spectra.Length).GroupBy(i => _passOfMs2[i]))
                {
                    var scales = group.Select(i => _intensityPerIon[_ms2Spectra[i]]).ToArray();
                    _passPerIon[group.Key] = MedianMath.SelectInPlace(scales, scales.Length / 2);
                }
            }
            // The per-channel solve combines spectra, interpolating each window's channels between its acquisitions and
            // solving them together, so with fill times that vary it must see them all on one scale: rates times the
            // run's median fill time (as the joint solve uses its pass's). Each spectrum's peaks are put on it as read,
            // each acquisition's exposure (its own ions per unit on that scale) sets its weights (MakeUnit), and each
            // output spectrum's peaks are taken back to its own scale (LayOut). A constant counts per ion needs no
            // conversion, and none is made.
            _referencePerIon = double.NaN;
            if (options.IonsFromFillTime && !options.Joint && _ms2Spectra.Length > 0)
            {
                var scales = _ms2Spectra.Select(k => _intensityPerIon[k]).ToArray();
                _referencePerIon = MedianMath.SelectInPlace(scales, scales.Length / 2);
            }
            log.WriteLine(@"Scheme: {0}, {1} windows into {2} bins, {3}-fold", Scheme.Kind, Scheme.Windows.Count,
                Scheme.Bins.Count, Scheme.OverlapFactor);

            for (int k = 0; k < source.Count; k++)
            {
                if (_thinnedOut.Contains(k))
                    continue;
                if (!_ms2OfSpectrum.TryGetValue(k, out int i))
                {
                    _output.Add((k, -1));
                    continue;
                }
                var w = Scheme.Windows[Scheme.WindowOfSpectrum[i]];
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    _outputOf[(k, b)] = _output.Count;
                    _output.Add((k, b));
                }
            }
        }

        public DemuxScheme Scheme { get; }

        public override int OutputCount
        {
            get { return _output.Count; }
        }

        public override int LastCycle
        {
            get { return _cycles - 1; }
        }

        public override int BatchCycles
        {
            get { return Math.Max(1, _options.Threads / 2) * _options.BlockCycles; }
        }

        /// <summary>The acquired spectrum an output spectrum is built on (for a demultiplexed one, its own MS2).</summary>
        public int BaseIndex(int output)
        {
            return _output[output].Spectrum;
        }

        /// <summary>Whether an output spectrum is passed through as read (an MS1 spectrum).</summary>
        public bool IsPassThrough(int output)
        {
            return _output[output].Bin < 0;
        }

        /// <summary>A demultiplexed spectrum's narrow bin.</summary>
        public DemuxBin BinOf(int output)
        {
            return Scheme.Bins[_output[output].Bin];
        }

        /// <summary>The intensity of one ion in an output spectrum: its acquired spectrum's.</summary>
        public double IntensityPerIon(int output)
        {
            return _intensityPerIon[_output[output].Spectrum];
        }

        /// <summary>A demultiplexed spectrum's place among its window's bins, from 0.</summary>
        public int PartOf(int output)
        {
            var (k, bin) = _output[output];
            return bin - Scheme.Windows[Scheme.WindowOfSpectrum[_ms2OfSpectrum[k]]].FirstBin;
        }

        public override int CycleOfOutput(int output)
        {
            var (k, bin) = _output[output];
            return bin < 0 ? -1 : _cycleOfMs2[_ms2OfSpectrum[k]];
        }

        public override string DescribeBatch(int firstCycle, int lastCycle)
        {
            return string.Format(CultureInfo.InvariantCulture, @"  cycles {0}-{1} of {2} written", firstCycle, lastCycle, _cycles);
        }

        /// <summary>Nothing is kept between batches: each reads its own cycles and context.</summary>
        public override void Forget(int firstCycle)
        {
        }

        /// <summary>Reads every MS2 spectrum of a batch's padded cycles and makes one solve per block and group of bins.</summary>
        public override IReadOnlyList<DemuxWork> MakeWork(int firstCycle, int lastCycle, out double readSeconds)
        {
            var clock = Stopwatch.StartNew();
            int padLo = Math.Max(0, firstCycle - _options.CyclePad), padHi = Math.Min(_cycles - 1, lastCycle + _options.CyclePad);
            // Every MS2 spectrum of the padded cycles, its nonzero peaks in ions.
            var inBlock = Enumerable.Range(0, _ms2Spectra.Length)
                .Where(i => _cycleOfMs2[i] >= padLo && _cycleOfMs2[i] <= padHi).ToArray();
            // Read on the read threads (a source serializes what its reader cannot serve at once), each into its own slot.
            var read = new (double[] Mz, double[] Ions)[inBlock.Length];
            Parallel.For(0, inBlock.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.ReadThreads) },
                n => read[n] = ReadPeaks(_ms2Spectra[inBlock[n]]));
            var peaks = new Dictionary<int, (double[] Mz, double[] Ions)>();
            for (int n = 0; n < inBlock.Length; n++)
                peaks[inBlock[n]] = read[n];
            readSeconds = clock.Elapsed.TotalSeconds;

            var work = new List<DemuxWork>();
            int binCount = Scheme.Bins.Count, contextBins = Math.Max(2, Scheme.OverlapFactor * 2);
            if (_options.Joint)
            {
                var grid = GridOf(peaks);
                foreach (var (unit, observed) in MakeJointUnits(inBlock, peaks, grid, firstCycle, lastCycle, padLo, padHi, contextBins))
                {
                    if (unit == null)
                        continue;
                    var parameters = _options.JointParameters;
                    var centrix = _options.JointCentroid ? _centrix : null;
                    var passPerIon = _passPerIon;
                    work.Add(new DemuxWork(_options.JointApportion
                        ? (Func<ScanningUnitResult>)(() => SolveJointApportioned(unit, observed, parameters, grid, centrix, passPerIon))
                        : () => JointDemultiplexer.DemuxUnit(unit, parameters, grid), unit.Mz.Length));
                }
                return work;
            }
            for (int k0 = firstCycle; k0 <= lastCycle; k0 += _options.BlockCycles)
            {
                int k1 = Math.Min(lastCycle, k0 + _options.BlockCycles - 1);
                int lo = Math.Max(padLo, k0 - _options.CyclePad), hi = Math.Min(padHi, k1 + _options.CyclePad);
                for (int g0 = 0; g0 < binCount; g0 += _options.GroupBins)
                {
                    int g1 = Math.Min(binCount - 1, g0 + _options.GroupBins - 1);
                    var unit = MakeUnit(inBlock, peaks, lo, hi, k0, k1, g0, g1, contextBins);
                    // The solves capture only these locals: one referring to the plan would keep the batch's whole
                    // read (captured above, with the plan) alive until the batch's last solve.
                    var elution = _options.Elution;
                    var parameters = _options.Parameters;
                    work.Add(new DemuxWork(elution != null
                        ? (Func<ScanningUnitResult>)(() => ElutionDemultiplexer.DemuxUnit(unit, parameters, elution))
                        : () => ScanningDemultiplexer.DemuxInterpolatedUnit(unit, parameters),
                        unit.Mz.Length));
                }
            }
            return work;
        }

        /// <summary>Each MS2 spectrum of the batch as one spectrum per bin of its window.</summary>
        public override void LayOut(int firstCycle, int lastCycle, ScanningUnitResult[] results,
            IDictionary<int, (double[] Mz, double[] Ions)> peaks)
        {
            if (_options.Joint)
            {
                LayOutJoint(firstCycle, lastCycle, results, peaks);
                return;
            }
            var demuxed = new Dictionary<(int, int), List<ScanningPeak>>();
            var through = new Dictionary<int, List<ScanningPeak>>();
            foreach (var result in results)
            {
                foreach (var p in result.Demultiplexed)
                    Add(demuxed, (p.Cycle, p.Bin), p);
                foreach (var p in result.PassedThrough)
                    Add(through, p.Cycle, p);
            }
            var none = new List<ScanningPeak>();
            for (int i = 0; i < _ms2Spectra.Length; i++)
            {
                if (_cycleOfMs2[i] < firstCycle || _cycleOfMs2[i] > lastCycle)
                    continue;
                int k = _ms2Spectra[i];
                var w = Scheme.Windows[Scheme.WindowOfSpectrum[i]];
                // A pass-through peak is shared equally among the spectrum's bins.
                var shared = (through.TryGetValue(k, out var list) ? list : none)
                    .Select(p => new ScanningPeak(p.Bin, p.Cycle, p.Mz, p.Ions / w.BinCount)).ToList();
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    ScanningLayout.Assemble(shared, demuxed.TryGetValue((k, b), out var dem) ? dem : none,
                        out double[] mz, out double[] ions);
                    if (!double.IsNaN(_referencePerIon))
                    {
                        // From the run's ion scale to this spectrum's own.
                        double scale = _referencePerIon / _intensityPerIon[k];
                        for (int p = 0; p < ions.Length; p++)
                            ions[p] *= scale;
                    }
                    peaks[_outputOf[(k, b)]] = (mz, ions);
                }
            }
        }

        /// <summary>
        /// The joint solve's output: each MS2 spectrum's bins take their centroids from its own pass, in ions at the
        /// pass's fill time, converted to the spectrum's own.
        /// </summary>
        private void LayOutJoint(int firstCycle, int lastCycle, ScanningUnitResult[] results,
            IDictionary<int, (double[] Mz, double[] Ions)> peaks)
        {
            var byPassBin = new Dictionary<(int, int), List<ScanningPeak>>();
            foreach (var result in results)
            {
                foreach (var p in result.Demultiplexed)
                    Add(byPassBin, (p.Cycle, p.Bin), p);
            }
            var none = new List<ScanningPeak>();
            for (int i = 0; i < _ms2Spectra.Length; i++)
            {
                if (_cycleOfMs2[i] < firstCycle || _cycleOfMs2[i] > lastCycle)
                    continue;
                int k = _ms2Spectra[i], pass = _passOfMs2[i];
                var w = Scheme.Windows[Scheme.WindowOfSpectrum[i]];
                for (int b = w.FirstBin; b <= w.LastBin; b++)
                {
                    int output = _outputOf[(k, b)];
                    var list = byPassBin.TryGetValue((pass, b), out var found) ? found : none;
                    var ordered = list.OrderBy(p => p.Mz).ToArray(); // Array.Sort OK: OrderBy is stable
                    double scale = _passPerIon[pass] / IntensityPerIon(output);
                    peaks[output] = (ordered.Select(p => p.Mz).ToArray(), ordered.Select(p => p.Ions * scale).ToArray());
                }
            }
        }

        /// <summary>
        /// The batch's joint units, block of cycles by group of bins, null where a block has no core pass in the
        /// group's bins. Made on up to <see cref="ScanningDemuxOptions.Threads"/> threads, each into its own slot, so
        /// they come back in the order one thread would make them: each reads only the batch's read and the plan.
        /// </summary>
        private (ScanningUnit Unit, bool[] Observed)[] MakeJointUnits(int[] inBlock,
            Dictionary<int, (double[] Mz, double[] Ions)> peaks, LinearGrid grid, int firstCycle, int lastCycle,
            int padLo, int padHi, int contextBins)
        {
            int binCount = Scheme.Bins.Count;
            var blocks = new List<(int K0, int K1, int G0, int G1)>();
            for (int k0 = firstCycle; k0 <= lastCycle; k0 += _options.BlockCycles)
            {
                int k1 = Math.Min(lastCycle, k0 + _options.BlockCycles - 1);
                for (int g0 = 0; g0 < binCount; g0 += _options.GroupBins)
                    blocks.Add((k0, k1, g0, Math.Min(binCount - 1, g0 + _options.GroupBins - 1)));
            }
            var units = new (ScanningUnit Unit, bool[] Observed)[blocks.Count];
            Parallel.For(0, blocks.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.Threads) }, b =>
            {
                var (k0, k1, g0, g1) = blocks[b];
                int lo = Math.Max(padLo, k0 - _options.CyclePad), hi = Math.Min(padHi, k1 + _options.CyclePad);
                var unit = MakeJointUnit(inBlock, peaks, grid, lo, hi, k0, k1, g0, g1, contextBins, out var observed);
                units[b] = (unit, observed);
            });
            return units;
        }

        /// <summary>
        /// One joint solve's input: for each pass of the block, every window covering the unit's bins, those of the
        /// pass as acquired and the others interpolated (makima, sample by sample, in rates) to the pass's time: a
        /// pseudo-sweep in which every window is seen at once, as a ZT Scan sweep is. All in ions at the pass's fill
        /// time. Null when the block has no core pass in these bins.
        /// </summary>
        private ScanningUnit MakeJointUnit(int[] inBlock, Dictionary<int, (double[] Mz, double[] Ions)> peaks, LinearGrid grid,
            int lo, int hi, int k0, int k1, int g0, int g1, int contextBins, out bool[] observed)
        {
            observed = null;
            var a = BlockLayout(g0, g1, contextBins, out var columnBins, out var rowWindows, out var rowOfWindow);

            // Each row's acquisitions in the padded block, in time order; the core passes and their times.
            var acquisitions = rowWindows.Select(w => new List<int>()).ToArray();
            var passTimes = new SortedDictionary<int, List<double>>();
            foreach (int i in inBlock)
            {
                if (_cycleOfMs2[i] < lo || _cycleOfMs2[i] > hi || !rowOfWindow.TryGetValue(Scheme.WindowOfSpectrum[i], out int r))
                    continue;
                acquisitions[r].Add(i);
                if (_cycleOfMs2[i] < k0 || _cycleOfMs2[i] > k1)
                    continue;
                if (!passTimes.TryGetValue(_passOfMs2[i], out var times))
                {
                    times = new List<double>();
                    passTimes[_passOfMs2[i]] = times;
                }
                times.Add(_ms2Time[i]);
            }
            if (passTimes.Count == 0)
                return null;
            var passes = passTimes.Keys.ToArray();
            var passTime = passes.Select(p => MedianMath.SelectInPlace(passTimes[p].ToArray(), passTimes[p].Count / 2)).ToArray();

            // Each row's rates by grid sample, over its acquisitions.
            var rowTimes = new double[rowWindows.Length][];
            var rowRates = new Dictionary<long, double[]>[rowWindows.Length];
            for (int r = 0; r < rowWindows.Length; r++)
            {
                var list = acquisitions[r];
                rowTimes[r] = list.Select(i => _ms2Time[i]).ToArray();
                rowRates[r] = new Dictionary<long, double[]>();
                for (int q = 0; q < list.Count; q++)
                {
                    var (m, v) = peaks[list[q]];
                    double perIon = _intensityPerIon[_ms2Spectra[list[q]]];
                    for (int p = 0; p < m.Length; p++)
                    {
                        long sample = grid.Index(m[p]);
                        if (!rowRates[r].TryGetValue(sample, out var series))
                        {
                            series = new double[list.Count];
                            rowRates[r][sample] = series;
                        }
                        series[q] += v[p] * perIon;
                    }
                }
            }

            // Each pass's rows' exposure: the ions an acquisition counted per unit of its values on the pass's scale,
            // the row's own acquisition's in the pass, else interpolated in time between the row's acquisitions.
            double[][] rowExposure = null;
            if (_options.IonsFromFillTime)
            {
                rowExposure = new double[passes.Length][];
                for (int c = 0; c < passes.Length; c++)
                {
                    double perIonRef = _passPerIon[passes[c]];
                    rowExposure[c] = new double[rowWindows.Length];
                    for (int r = 0; r < rowWindows.Length; r++)
                    {
                        var exposures = acquisitions[r].Select(i => perIonRef / _intensityPerIon[_ms2Spectra[i]]).ToArray();
                        int own = acquisitions[r].FindIndex(i => _passOfMs2[i] == passes[c]);
                        rowExposure[c][r] = own >= 0
                            ? exposures[own]
                            : ScanningDemultiplexer.InterpolateLinear(rowTimes[r], exposures, passTime[c]);
                    }
                }
            }

            // Each row's samples in grid order, sorted once for every pass.
            var rowSamples = rowRates.Select(rates => rates.OrderBy(e => e.Key).ToArray()).ToArray();
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var cycle = new List<int>();
            var acquiredPoint = new List<bool>();
            for (int c = 0; c < passes.Length; c++)
            {
                double perIonRef = _passPerIon[passes[c]];
                for (int r = 0; r < rowWindows.Length; r++)
                {
                    var times = rowTimes[r];
                    if (times.Length == 0)
                        continue;
                    int acquired = acquisitions[r].FindIndex(i => _passOfMs2[i] == passes[c]);
                    foreach (var entry in rowSamples[r])
                    {
                        double rate = acquired >= 0
                            ? entry.Value[acquired]
                            : RtInterpolator.Interpolate(RtInterpolation.makima, times, entry.Value, times.Length, passTime[c]);
                        // Not negative, and not a value made non-finite by a malformed series (equal times).
                        if (!(rate > 0) || double.IsInfinity(rate))
                            continue;
                        mz.Add(grid.Mz(entry.Key));
                        ions.Add(rate / perIonRef);
                        row.Add(r);
                        cycle.Add(c);
                        acquiredPoint.Add(acquired >= 0);
                    }
                }
            }
            observed = acquiredPoint.ToArray();
            return new ScanningUnit(a, rowWindows.Select(w => Scheme.Windows[w].FirstBin).ToArray(), columnBins, passes, g0, g1,
                passes[0], passes[passes.Length - 1])
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Cycle = cycle.ToArray(),
                RowExposure = rowExposure,
            };
        }

        /// <summary>
        /// The joint solve with the measurements kept: the pseudo-sweeps are fitted for each bin's peaks (coefficients
        /// on the grid); each acquired spectrum's profile is split among the unit's own bins its window covers by those
        /// bins' fitted profiles at each sample (equally where none is fitted); and each bin's share in each pass is
        /// centroided by Centrix, or kept as profile when there is no centroider. Peaks are keyed by bin and pass.
        /// <para>The fit decides the split but is not written itself: the fitted peaks carry the errors of the windows
        /// interpolated to the pass (scored 0.944 against truth at k=2, where the split measurements centroided by
        /// Centrix score 0.982). Centrix, rather than the joint solve's own one-bin lasso, centroids the shares: it is
        /// the more stable of the two on demultiplexed spectra (0.979 against 0.944 on the same makima splits). Run
        /// in-process this gives exactly what writing the shares as profile and centroiding them with Rust Centrix
        /// gave (k=2 truth benchmark, median cosine 0.982 either way).</para>
        /// </summary>
        private static ScanningUnitResult SolveJointApportioned(ScanningUnit unit, bool[] observed, JointDemuxParams parameters,
            LinearGrid grid, CentrixCentroider centrix, IReadOnlyDictionary<int, double> passPerIon)
        {
            var coefficients = parameters.Copy();
            coefficients.KeepCoefficients = true;
            var fit = JointDemultiplexer.DemuxUnit(unit, coefficients, grid);

            // The core bins, one row and one position each, and the acquired points apportioned among them.
            var coreBins = unit.ColumnBins.Where(b => b >= unit.FirstCoreBin && b <= unit.LastCoreBin).ToArray();
            var coreIndex = new Dictionary<int, int>();
            for (int i = 0; i < coreBins.Length; i++)
                coreIndex[coreBins[i]] = i;
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var cycle = new List<int>();
            JointApportioner.Split(unit, fit, grid, parameters, p => observed[p], (p, j, fraction) =>
            {
                if (!coreIndex.TryGetValue(unit.ColumnBins[j], out int core))
                    return;
                mz.Add(unit.Mz[p]);
                ions.Add(unit.Ions[p] * fraction);
                row.Add(core);
                cycle.Add(unit.Cycle[p]);
            });
            // The ions in are the measured ions split among the unit's own bins: the fit's would count each pass's
            // interpolated windows too.
            var result = new ScanningUnitResult
            {
                Channels = fit.Channels,
                ChannelsSolved = fit.ChannelsSolved,
                IonsIn = ions.Sum(),
            };
            if (centrix == null)
            {
                for (int n = 0; n < mz.Count; n++)
                    result.Demultiplexed.Add(new ScanningPeak(coreBins[row[n]], unit.Cycles[cycle[n]], mz[n], ions[n]));
                return result;
            }
            // Each bin's share in each pass, as the profile spectrum it would be written as: in m/z order, in the pass's
            // intensity units, since Centrix's noise model is of an instrument's intensities, not of ions (its noise
            // sigma is floored at 1). The shares hold only the positive points the unit was built from; Centrix reads
            // them as the profile-writing path wrote them, which is what the benchmark numbers above were measured on.
            // Centroid intensities go back to ions at the pass's scale for the layout.
            var shares = new SortedDictionary<(int, int), List<int>>();
            for (int n = 0; n < mz.Count; n++)
            {
                if (!shares.TryGetValue((row[n], cycle[n]), out var points))
                {
                    points = new List<int>();
                    shares[(row[n], cycle[n])] = points;
                }
                points.Add(n);
            }
            foreach (var ((core, c), points) in shares)
            {
                int pass = unit.Cycles[c];
                double perIon = passPerIon[pass];
                var ordered = points.OrderBy(n => mz[n]).ToArray(); // Array.Sort OK: OrderBy is stable
                var x = ordered.Select(n => mz[n]).ToArray();
                var y = ordered.Select(n => ions[n] * perIon).ToArray();
                foreach (var (m, v) in centrix.Centroid(x, y))
                    result.Demultiplexed.Add(new ScanningPeak(coreBins[core], pass, m, v / perIon));
            }
            return result;
        }

        /// <summary>
        /// The joint solve's profile grid, taken once, from the first batch's densest spectrum, and the peak sigma
        /// that both the joint solve's Gaussian and Centrix use: <see cref="ScanningDemuxOptions.LinearPeakSigmaTh"/>
        /// when given, else from the run (<see cref="MeasurePeakSigma"/>). Also makes the Centrix centroider of
        /// each bin's share, on this grid's spacing.
        /// </summary>
        private LinearGrid GridOf(Dictionary<int, (double[] Mz, double[] Ions)> peaks)
        {
            if (_grid != null)
                return _grid;
            // The grid of the batch's densest spectrum (the first of the densest), whose most common gap is the grid
            // step itself: a sparse spectrum, its zero points dropped, can show a multiple of it.
            double[] densest = null;
            foreach (var (mz, _) in peaks.OrderBy(e => e.Key).Select(e => e.Value))
            {
                if (densest == null || mz.Length > densest.Length)
                    densest = mz;
            }
            _grid = densest != null ? LinearGrid.Detect(densest) : null;
            if (_grid == null)
                throw new InvalidOperationException(@"The joint solve needs profile spectra on one linear grid.");
            double sigmaTh = _options.LinearPeakSigmaTh > 0 ? _options.LinearPeakSigmaTh : MeasurePeakSigma();
            double sigma = sigmaTh / _grid.Step;
            _options.JointParameters.PeakSigmaSamples = new[] { sigma };
            _options.JointParameters.PeakSigmaMz = new[] { 500.0 };
            var centrix = _options.Centrix.Copy();
            centrix.SigmaTh = sigmaTh;
            centrix.GridSpacingTh = _grid.Step;
            _centrix = new CentrixCentroider(centrix);
            _log.WriteLine(@"Profile grid: step {0:F4} Th; peak sigma {1:F3} Th = {2:F2} samples", _grid.Step,
                sigmaTh, sigma);
            return _grid;
        }

        /// <summary>
        /// The MS2 profile peak sigma, Th, from the run's MS2 spectra (<see cref="CentrixCalibration"/>): 1.1 point
        /// spacings when the spacing identifies a linear ion trap scan rate (0.22 Th on the Stellar at 200 kTh/s), else
        /// one species' peak width measured from spectra sampled across the run. Those are the acquired, multiplexed
        /// spectra, whose peaks merge more species than a narrow window's, so a measured width reads a little wider than
        /// the narrow-window run's would.
        /// </summary>
        private double MeasurePeakSigma()
        {
            var calibrator = new CentrixSigmaCalibrator();
            calibrator.Sample(_ms2Spectra.Length, false, true, n =>
            {
                int k = _ms2Spectra[n];
                _source.Read(k, out var mz, out var intensity);
                if (mz != null && intensity != null)
                    calibrator.Add(2, mz, intensity);
                _source.Release(k);
            });
            _log.WriteLine(@"Peak sigma: {0}", calibrator.Describe(2));
            return calibrator.Sigma(2);
        }

        private InterpolatedUnit MakeUnit(int[] inBlock, Dictionary<int, (double[] Mz, double[] Ions)> peaks, int lo, int hi,
            int k0, int k1, int g0, int g1, int contextBins)
        {
            var a = BlockLayout(g0, g1, contextBins, out var columnBins, out var rowWindows, out var rowOfWindow);

            var acquisitions = rowWindows.Select(w => new List<int>()).ToArray();
            foreach (int i in inBlock)
            {
                if (_cycleOfMs2[i] < lo || _cycleOfMs2[i] > hi)
                    continue;
                if (rowOfWindow.TryGetValue(Scheme.WindowOfSpectrum[i], out int r))
                    acquisitions[r].Add(i);
            }
            var rowTimes = acquisitions.Select(list => list.Select(i => _ms2Time[i]).ToArray()).ToArray();
            var rowSpectra = acquisitions.Select(list => list.Select(i => _ms2Spectra[i]).ToArray()).ToArray();
            // Each acquisition's exposure: the ions it counted per unit of its values on the run's scale.
            var rowExposure = double.IsNaN(_referencePerIon)
                ? null
                : rowSpectra.Select(list => list.Select(k => _referencePerIon / _intensityPerIon[k]).ToArray()).ToArray();

            var outputRow = new List<int>();
            var outputAcquisition = new List<int>();
            var outputTime = new List<double>();
            var outputSpectrum = new List<int>();
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var acquisition = new List<int>();
            for (int r = 0; r < rowWindows.Length; r++)
            {
                var w = Scheme.Windows[rowWindows[r]];
                bool coversCore = w.LastBin >= g0 && w.FirstBin <= g1;
                for (int q = 0; q < acquisitions[r].Count; q++)
                {
                    int i = acquisitions[r][q];
                    if (coversCore && _cycleOfMs2[i] >= k0 && _cycleOfMs2[i] <= k1)
                    {
                        outputRow.Add(r);
                        outputAcquisition.Add(q);
                        outputTime.Add(_ms2Time[i]);
                        outputSpectrum.Add(_ms2Spectra[i]);
                    }
                    var (m, v) = peaks[i];
                    // This spectrum's ions on the run's scale, when fill times vary (see _referencePerIon).
                    double toReference = double.IsNaN(_referencePerIon) ? 1 : _intensityPerIon[_ms2Spectra[i]] / _referencePerIon;
                    for (int p = 0; p < m.Length; p++)
                    {
                        mz.Add(m[p]);
                        ions.Add(v[p] * toReference);
                        row.Add(r);
                        acquisition.Add(q);
                    }
                }
            }
            return new InterpolatedUnit(a, columnBins, rowTimes, rowSpectra, outputRow.ToArray(), outputAcquisition.ToArray(),
                outputTime.ToArray(), outputSpectrum.ToArray(), g0, g1)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Acquisition = acquisition.ToArray(),
                RowExposure = rowExposure,
            };
        }

        private (double[] Mz, double[] Ions) ReadPeaks(int k)
        {
            _source.Read(k, out var mzData, out var intensityData);
            var mzs = new List<double>();
            var values = new List<double>();
            double perIon = _intensityPerIon[k];
            if (mzData != null && intensityData != null)
            {
                int count = Math.Min(mzData.Count, intensityData.Count);
                for (int i = 0; i < count; i++)
                {
                    if (!(intensityData[i] > 0))
                        continue;
                    mzs.Add(mzData[i]);
                    values.Add(intensityData[i] / perIon);
                }
            }
            _source.Release(k);
            return (mzs.ToArray(), values.ToArray());
        }

        /// <summary>
        /// A unit's bins and windows: its columns, bins g0 to g1 and the context bins either side; its rows, the windows
        /// that cover any of the columns (with each row's index by window); and the 0/1 matrix of which row covers
        /// which column.
        /// </summary>
        private double[,] BlockLayout(int g0, int g1, int contextBins, out int[] columnBins, out int[] rowWindows,
            out Dictionary<int, int> rowOfWindow)
        {
            int col0 = Math.Max(0, g0 - contextBins), col1 = Math.Min(Scheme.Bins.Count - 1, g1 + contextBins);
            columnBins = Enumerable.Range(col0, col1 - col0 + 1).ToArray();
            rowWindows = Enumerable.Range(0, Scheme.Windows.Count)
                .Where(w => Scheme.Windows[w].LastBin >= col0 && Scheme.Windows[w].FirstBin <= col1).ToArray();
            rowOfWindow = new Dictionary<int, int>();
            for (int r = 0; r < rowWindows.Length; r++)
                rowOfWindow[rowWindows[r]] = r;
            var a = new double[rowWindows.Length, columnBins.Length];
            for (int r = 0; r < rowWindows.Length; r++)
            {
                for (int j = 0; j < columnBins.Length; j++)
                    a[r, j] = Scheme.Windows[rowWindows[r]].Covers(columnBins[j]) ? 1 : 0;
            }
            return a;
        }

        /// <summary>
        /// Keeps the MS2 spectra of every <paramref name="every"/>-th cycle only (cycles as the plan counts them: a
        /// window's repeat ends one), and records the rest as dropped.
        /// </summary>
        private void Thin(List<int> ms2, List<double> times, List<IsolationWindow> windows, int every,
            double minimumBinWidth)
        {
            var cycleOf = CycleNumbers(DemuxSchemeDetector.Detect(windows, minimumBinWidth).WindowOfSpectrum, null, out _);
            int n = 0;
            for (int i = 0; i < ms2.Count; i++)
            {
                if (cycleOf[i] % every != 0)
                {
                    _thinnedOut.Add(ms2[i]);
                    continue;
                }
                ms2[n] = ms2[i];
                times[n] = times[i];
                windows[n] = windows[i];
                n++;
            }
            ms2.RemoveRange(n, ms2.Count - n);
            times.RemoveRange(n, times.Count - n);
            windows.RemoveRange(n, windows.Count - n);
        }

        /// <summary>
        /// The cycle of each MS2 spectrum: a cycle ends when a window repeats, so a staggered pair of window sets is
        /// one cycle and a file without survey scans still has cycles. With each spectrum's pass given, a cycle ends
        /// only where a pass starts, so every pass lies whole in one cycle.
        /// </summary>
        private static int[] CycleNumbers(int[] windowOfSpectrum, int[] passOfSpectrum, out int cycles)
        {
            var cycleOf = new int[windowOfSpectrum.Length];
            var seen = new HashSet<int>();
            int cycle = 0;
            for (int start = 0, end; start < cycleOf.Length; start = end)
            {
                // Spectra start to end - 1 are taken together: one spectrum, or one pass.
                end = start + 1;
                while (passOfSpectrum != null && end < cycleOf.Length && passOfSpectrum[end] == passOfSpectrum[start])
                    end++;
                bool repeats = false;
                for (int i = start; i < end && !repeats; i++)
                    repeats = seen.Contains(windowOfSpectrum[i]);
                if (repeats)
                {
                    cycle++;
                    seen.Clear();
                }
                for (int i = start; i < end; i++)
                {
                    seen.Add(windowOfSpectrum[i]);
                    cycleOf[i] = cycle;
                }
            }
            cycles = cycleOf.Length > 0 ? cycle + 1 : 0;
            return cycleOf;
        }

        private static void Add<TKey>(Dictionary<TKey, List<ScanningPeak>> byKey, TKey key, ScanningPeak peak)
        {
            if (!byKey.TryGetValue(key, out var list))
            {
                list = new List<ScanningPeak>();
                byKey[key] = list;
            }
            list.Add(peak);
        }
    }

    /// <summary>
    /// Demultiplexes a stepped staggered acquisition (Orbitrap, Astral) on the shared <see cref="DemuxPipeline"/>
    /// with the same fragment channels and weighted per-channel solve as the scanning demultiplexer, the other
    /// windows interpolated to each spectrum's time. Each MS2 spectrum becomes one spectrum per narrow bin its
    /// window covers, under that bin's window, as msconvert's demultiplex filter writes them; MS1 spectra pass
    /// through.
    /// </summary>
    public sealed class StaggeredDemuxPipeline : DemuxPipeline
    {
        private readonly StaggeredDemuxPlan _staggered;

        /// <param name="source">The acquisition.</param>
        /// <param name="options">The solve's settings.</param>
        /// <param name="log">Where the scheme and each batch are logged.</param>
        /// <param name="minimumBinWidth">Window boundaries closer than this (Th) are one boundary.</param>
        public StaggeredDemuxPipeline(IDemuxSource source, ScanningDemuxOptions options, TextWriter log,
            double minimumBinWidth = DemuxSchemeDetector.DEFAULT_MINIMUM_BIN_WIDTH)
            : this(new StaggeredDemuxPlan(source, options, log, minimumBinWidth), options, log)
        {
        }

        private StaggeredDemuxPipeline(StaggeredDemuxPlan plan, ScanningDemuxOptions options, TextWriter log)
            : base(plan, options, log)
        {
            _staggered = plan;
        }

        public DemuxScheme Scheme
        {
            get { return _staggered.Scheme; }
        }

        /// <summary>The acquired spectrum an output spectrum is built on (for a demultiplexed one, its own MS2).</summary>
        public int BaseIndex(int index)
        {
            return _staggered.BaseIndex(index);
        }

        /// <summary>Whether an output spectrum is passed through as read (an MS1 spectrum).</summary>
        public bool IsPassThrough(int index)
        {
            return _staggered.IsPassThrough(index);
        }

        /// <summary>A demultiplexed spectrum's narrow bin: its isolation window.</summary>
        public DemuxBin BinOf(int index)
        {
            return _staggered.BinOf(index);
        }

        /// <summary>The intensity of one ion in an output spectrum, to write its peaks in the input's units.</summary>
        public double IntensityPerIon(int index)
        {
            return _staggered.IntensityPerIon(index);
        }

        /// <summary>A demultiplexed spectrum's place among its window's bins, from 0 (its id's demux= suffix).</summary>
        public int PartOf(int index)
        {
            return _staggered.PartOf(index);
        }
    }
}
