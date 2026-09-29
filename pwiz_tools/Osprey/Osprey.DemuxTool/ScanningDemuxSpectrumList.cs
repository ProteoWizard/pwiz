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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;
using pwiz.Osprey.Demux;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>Settings for <see cref="ScanningDemuxSpectrumList"/>.</summary>
    internal sealed class ScanningDemuxOptions
    {
        public ScanningLayout Layout { get; set; } = new ScanningLayout(ScanningLayoutKind.centered, 5);
        public ScanningDemuxParams Parameters { get; set; } = new ScanningDemuxParams();
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int FirstCycle { get; set; }
        public int LastCycle { get; set; } = int.MaxValue;
        public double MinMz { get; set; }
        public double MaxMz { get; set; } = double.MaxValue;

        /// <summary>The acquired spectra of the selected bins, zeros dropped: the control arm.</summary>
        public bool Raw { get; set; }

        /// <summary>Detector counts per ion (about 100 on a ZenoTOF 8600).</summary>
        public double CountsPerIon { get; set; } = 100;

        /// <summary>
        /// Demultiplex and centroid profile spectra in one solve (<see cref="JointDemultiplexer"/>) instead of
        /// demultiplexing centroids channel by channel. Needs profile input on one TOF grid.
        /// </summary>
        public bool Joint { get; set; }

        /// <summary>Settings for <see cref="Joint"/>.</summary>
        public JointDemuxParams JointParameters { get; set; } = new JointDemuxParams();

        /// <summary>Sweeps whose output one block owns.</summary>
        public int BlockCycles { get; set; } = 12;

        /// <summary>Sweeps read beyond a block on each side, so its edge sweeps see their neighbors.</summary>
        public int CyclePad { get; set; } = 4;

        /// <summary>Encoded bins whose output one block owns.</summary>
        public int GroupBins { get; set; } = 16;

        /// <summary>Source positions solved beyond a block's own bins on each side.</summary>
        public int ContextBins { get; set; } = 10;

        /// <summary>Encoded bins a precursor's transmission reaches on each side.</summary>
        public int ReachBins { get; set; } = 9;
    }

    /// <summary>
    /// A spectrum list that demultiplexes a scanning-quadrupole acquisition (SCIEX ZT Scan) and
    /// lays it out as <see cref="ScanningDemuxOptions.Layout"/> says. MS1 spectra pass through;
    /// each output MS2 spectrum is built on the acquired spectrum of the middle bin of its window,
    /// with the window and peaks replaced.
    /// </summary>
    /// <remarks>
    /// Blocks of sweeps are read in order, a batch at a time, and demultiplexed in parallel, one
    /// task per block of encoded bins, while the next batch is read. The writer asks for spectra
    /// in index order, so only the current batch and the next one's sweeps are resident.
    /// </remarks>
    internal sealed class ScanningDemuxSpectrumList : SpectrumListWrapper
    {
        private readonly ScanningKernel _kernel;
        private readonly ScanningDemuxOptions _options;
        private readonly TextWriter _log;
        private readonly List<int> _ms1OfCycle = new List<int>();
        private readonly List<int[]> _ms2OfCycle = new List<int[]>();
        private readonly double[] _centers;
        private readonly int _firstOutBin;
        private readonly int _lastOutBin;
        private readonly List<ScanningOutputSpectrum> _plan;
        private readonly List<(int Cycle, int Slot)> _output = new List<(int, int)>();
        private readonly Dictionary<int, (double[] Mz, double[] Ions)[]> _built = new Dictionary<int, (double[], double[])[]>();
        private readonly Dictionary<int, List<(double[] Mz, double[] Ions)>> _peaks = new Dictionary<int, List<(double[], double[])>>();
        private readonly Dictionary<int, double[,]> _transmission = new Dictionary<int, double[,]>();
        private readonly object _lock = new object();
        private readonly HashSet<int> _isLayoutBase = new HashSet<int>();                // acquired spectra layout spectra are built on
        private readonly Dictionary<int, Spectrum> _headers = new Dictionary<int, Spectrum>(); // by acquired index, peaks dropped
        private TofGrid _grid;

        public ScanningDemuxSpectrumList(ISpectrumList inner, ScanningKernel kernel, ScanningDemuxOptions options,
            TextWriter log)
            : base(inner)
        {
            _kernel = kernel;
            _options = options;
            _log = log;

            // A cycle is a survey scan and the sweep after it. SCIEX native ids name the experiment,
            // experiment 1 being the survey scan; for any other id form, read each MS level.
            var isSurvey = new bool[inner.Count];
            if (!TryReadSurveyScansFromIds(inner, isSurvey))
            {
                for (int k = 0; k < inner.Count; k++)
                    isSurvey[k] = inner.GetSpectrum(k).Params.CvParamValueOrDefault(CVID.MS_ms_level, 0) == 1;
            }
            var sweep = new List<int>();
            for (int k = 0; k < inner.Count; k++)
            {
                if (isSurvey[k])
                {
                    if (_ms1OfCycle.Count > 0)
                        _ms2OfCycle.Add(sweep.ToArray());
                    sweep.Clear();
                    _ms1OfCycle.Add(k);
                }
                else if (_ms1OfCycle.Count > 0)
                {
                    sweep.Add(k);
                }
            }
            if (_ms1OfCycle.Count > 0)
                _ms2OfCycle.Add(sweep.ToArray());
            CycleCount = _ms1OfCycle.Count;

            // Encoded bins are the sweep positions; their centers are the same in every sweep.
            var first = _ms2OfCycle[0];
            _centers = first.Select(k => TargetMz(inner.GetSpectrum(k))).ToArray();
            _firstOutBin = Array.FindIndex(_centers, c => c >= options.MinMz);
            _lastOutBin = Array.FindLastIndex(_centers, c => c < options.MaxMz);
            if (_firstOutBin < 0 || _lastOutBin < _firstOutBin)
                throw new ArgumentException(@"No encoded bin lies in the selected m/z range.");
            _plan = options.Raw
                ? new ScanningLayout(ScanningLayoutKind.tiled, 1).Plan(_firstOutBin, _lastOutBin)
                : options.Layout.Plan(_firstOutBin, _lastOutBin);

            int lastCycle = Math.Min(options.LastCycle, CycleCount - 1);
            for (int c = Math.Max(0, options.FirstCycle); c <= lastCycle; c++)
            {
                _output.Add((c, -1));
                for (int s = 0; s < _plan.Count; s++)
                    _output.Add((c, s));
            }
            FirstCycle = Math.Max(0, options.FirstCycle);
            LastCycle = lastCycle;
            for (int i = 0; i < _output.Count; i++)
            {
                if (_output[i].Slot >= 0)
                    _isLayoutBase.Add(InnerIndex(i));
            }
        }

        public int CycleCount { get; }
        public int FirstCycle { get; }
        public int LastCycle { get; }
        public long Channels { get; private set; }
        public long ChannelsSolved { get; private set; }
        public double IonsIn { get; private set; }
        public double IonsPassedThrough { get; private set; }

        public override int Count
        {
            get { return _output.Count; }
        }

        public override SpectrumIdentity SpectrumIdentity(int index)
        {
            var identity = Inner.SpectrumIdentity(InnerIndex(index));
            return new SpectrumIdentity
            {
                Index = index,
                Id = identity.Id,
                SpotId = identity.SpotId,
                SourceFilePosition = identity.SourceFilePosition,
            };
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            var (cycle, slot) = _output[index];
            if (slot < 0)
            {
                var survey = Inner.GetSpectrum(InnerIndex(index), getBinaryData);
                survey.Index = index;
                return survey;
            }

            // A layout spectrum's peaks are replaced. Its header is the one kept when its bin was read for
            // demultiplexing: asking the source again decodes the spectrum, and centroids it, a second time.
            Spectrum spectrum = null;
            (double[] Mz, double[] Ions) peaks = default;
            if (getBinaryData)
            {
                peaks = Built(cycle)[slot];
                spectrum = TakeHeader(InnerIndex(index));
            }
            spectrum ??= Inner.GetSpectrum(InnerIndex(index));
            spectrum.Index = index;
            MarkCentroid(spectrum);

            var planned = _plan[slot];
            var window = spectrum.Precursors[0].IsolationWindow;
            double lo = _centers[planned.FirstBin] - HalfWidth(planned.FirstBin);
            double hi = _centers[planned.LastBin] + HalfWidth(planned.LastBin);
            window.Set(CVID.MS_isolation_window_target_m_z, 0.5 * (lo + hi), CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_lower_offset, 0.5 * (hi - lo), CVID.MS_m_z);
            window.Set(CVID.MS_isolation_window_upper_offset, 0.5 * (hi - lo), CVID.MS_m_z);
            if (!getBinaryData)
                return spectrum;
            var counts = new double[peaks.Ions.Length];
            for (int i = 0; i < peaks.Ions.Length; i++)
                counts[i] = peaks.Ions[i] * _options.CountsPerIon;
            spectrum.SetMZIntensityArrays(peaks.Mz, counts, CVID.MS_number_of_detector_counts);
            return spectrum;
        }

        /// <summary>
        /// The header of an acquired spectrum read for demultiplexing, its peaks dropped; each is handed out
        /// once, so no caller shares one. Null if it was not read, or was already taken.
        /// </summary>
        private Spectrum TakeHeader(int innerIndex)
        {
            lock (_lock)
                return _headers.Remove(innerIndex, out var header) ? header : null;
        }

        /// <summary>
        /// Labels a layout spectrum centroid: its peaks are the demultiplexed centroids, whether the source
        /// was centroided or, for the joint solve, profile.
        /// </summary>
        private static void MarkCentroid(Spectrum spectrum)
        {
            var terms = spectrum.Params;
            for (int i = terms.CVParams.Count - 1; i >= 0; i--)
            {
                if (terms.CVParams[i].Cvid == CVID.MS_profile_spectrum)
                    terms.CVParams.RemoveAt(i);
            }
            foreach (var group in terms.ParamGroups.ToList())
            {
                if (group.CVParams.All(p => p.Cvid != CVID.MS_profile_spectrum))
                    continue;
                // The term comes from a shared group: copy the group's other terms onto the spectrum.
                foreach (var p in group.CVParams.Where(p => p.Cvid != CVID.MS_profile_spectrum && !terms.HasCVParam(p.Cvid)))
                    terms.CVParams.Add(p);
                terms.UserParams.AddRange(group.UserParams);
                terms.ParamGroups.Remove(group);
            }
            terms.Set(CVID.MS_centroid_spectrum);
        }

        /// <summary>The acquired spectrum an output spectrum is built on: the MS1, or its window's middle bin.</summary>
        private int InnerIndex(int index)
        {
            var (cycle, slot) = _output[index];
            if (slot < 0)
                return _ms1OfCycle[cycle];
            var planned = _plan[slot];
            return _ms2OfCycle[cycle][(planned.FirstBin + planned.LastBin) / 2];
        }

        private double HalfWidth(int bin)
        {
            double left = bin > 0 ? _centers[bin] - _centers[bin - 1] : _centers[bin + 1] - _centers[bin];
            double right = bin + 1 < _centers.Length ? _centers[bin + 1] - _centers[bin] : left;
            return 0.25 * (left + right);
        }

        /// <summary>The output peaks of a cycle's layout spectra, demultiplexing its batch if needed.</summary>
        private (double[] Mz, double[] Ions)[] Built(int cycle)
        {
            lock (_lock)
            {
                if (_built.TryGetValue(cycle, out var cached))
                    return cached;
                foreach (int old in _built.Keys.Where(c => c < cycle).ToList())
                    _built.Remove(old);
                int batchLast = Math.Min(LastCycle, cycle + BatchCycles - 1);
                if (_options.Raw)
                    BuildRaw(cycle, batchLast);
                else
                    BuildDemux(cycle, batchLast);
                _log.WriteLine(@"  sweeps {0}-{1} of {2} written", cycle, batchLast, CycleCount);
                return _built[cycle];
            }
        }

        /// <summary>Sweeps demultiplexed together: enough blocks to keep every thread busy.</summary>
        private int BatchCycles
        {
            get
            {
                int groups = Math.Max(1, (_lastOutBin - _firstOutBin + _options.GroupBins) / _options.GroupBins);
                return Math.Max(1, 2 * _options.Threads / groups) * _options.BlockCycles;
            }
        }

        private void BuildRaw(int firstCycle, int lastCycle)
        {
            foreach (int old in _peaks.Keys.Where(c => c < firstCycle).ToList())
                DropSweep(old);
            for (int c = firstCycle; c <= lastCycle; c++)
            {
                var peaks = SweepPeaks(c);
                _built[c] = _plan.Select(p => peaks[p.FirstBin]).ToArray();
            }
        }

        private void BuildDemux(int firstCycle, int lastCycle)
        {
            int readLo = Math.Max(0, _firstOutBin - _options.ContextBins - _options.ReachBins);
            int readHi = Math.Min(_centers.Length - 1, _lastOutBin + _options.ContextBins + _options.ReachBins);
            int padLo = Math.Max(0, firstCycle - _options.CyclePad);
            int padHi = Math.Min(CycleCount - 1, lastCycle + _options.CyclePad);
            foreach (int old in _peaks.Keys.Where(c => c < padLo).ToList())
                DropSweep(old);
            var clock = Stopwatch.StartNew();
            var sweeps = new Dictionary<int, List<(double[] Mz, double[] Ions)>>();
            for (int c = padLo; c <= padHi; c++)
                sweeps[c] = SweepPeaks(c);
            double readSeconds = clock.Elapsed.TotalSeconds;
            var grid = _options.Joint ? TofGridOf(sweeps) : null;

            // One unit per block of sweeps and group of encoded bins.
            var units = new List<ScanningUnit>();
            for (int k0 = firstCycle; k0 <= lastCycle; k0 += _options.BlockCycles)
            {
                int k1 = Math.Min(lastCycle, k0 + _options.BlockCycles - 1);
                var cycles = Enumerable.Range(Math.Max(padLo, k0 - _options.CyclePad),
                    Math.Min(padHi, k1 + _options.CyclePad) - Math.Max(padLo, k0 - _options.CyclePad) + 1).ToArray();
                for (int g0 = _firstOutBin; g0 <= _lastOutBin; g0 += _options.GroupBins)
                {
                    int g1 = Math.Min(_lastOutBin, g0 + _options.GroupBins - 1);
                    int col0 = Math.Max(readLo, g0 - _options.ContextBins), col1 = Math.Min(readHi, g1 + _options.ContextBins);
                    int row0 = Math.Max(readLo, col0 - _options.ReachBins), row1 = Math.Min(readHi, col1 + _options.ReachBins);
                    var rowBins = Enumerable.Range(row0, row1 - row0 + 1).ToArray();
                    var columnBins = Enumerable.Range(col0, col1 - col0 + 1).ToArray();
                    if (!_transmission.TryGetValue(g0, out var a))
                    {
                        a = ScanningDemultiplexer.TransmissionMatrix(_kernel, rowBins.Select(b => _centers[b]).ToArray(),
                            columnBins.Select(b => _centers[b]).ToArray(), _centers[g0 + 1] - _centers[g0]);
                        _transmission[g0] = a;
                    }
                    var unit = MakeUnit(a, rowBins, columnBins, cycles, g0, g1, k0, k1, sweeps);
                    // Source positions evaluate the kernel at exact positions, in the matrix's scale.
                    unit.RowCenters = rowBins.Select(b => _centers[b]).ToArray();
                    unit.ColumnCenters = columnBins.Select(b => _centers[b]).ToArray();
                    unit.Kernel = _kernel;
                    unit.KernelScale = ScanningDemultiplexer.KernelScale(_kernel, _centers[g0 + 1] - _centers[g0]);
                    units.Add(unit);
                }
            }

            double unitSeconds = clock.Elapsed.TotalSeconds - readSeconds;

            // Solve on worker threads while this thread reads the next batch's sweeps. The units
            // hold copies of their peaks, and the source spectra are read from this thread only.
            var results = new ScanningUnitResult[units.Count];
            Exception solveException = null;
            var solver = new Thread(() =>
            {
                try
                {
                    Parallel.For(0, units.Count, new ParallelOptions { MaxDegreeOfParallelism = _options.Threads },
                        i => results[i] = _options.Joint
                            ? JointDemultiplexer.DemuxUnit(units[i], _options.JointParameters, grid)
                            : ScanningDemultiplexer.DemuxUnit(units[i], _options.Parameters));
                }
                catch (Exception e)
                {
                    solveException = e;
                }
            });
            solver.IsBackground = true;
            solver.Name = @"ScanningDemuxSolve";
            solver.Start();
            if (lastCycle < LastCycle)
            {
                int nextPadHi = Math.Min(CycleCount - 1, Math.Min(LastCycle, lastCycle + BatchCycles) + _options.CyclePad);
                for (int c = padHi + 1; c <= nextPadHi; c++)
                    SweepPeaks(c);
            }
            double readAheadSeconds = clock.Elapsed.TotalSeconds - readSeconds - unitSeconds;
            solver.Join();
            if (solveException != null)
                throw new AggregateException(@"Exception while demultiplexing sweeps", solveException);
            double solveSeconds = clock.Elapsed.TotalSeconds - readSeconds - unitSeconds;

            // Peaks by (cycle, bin), then each layout spectrum from its bins and source positions.
            var through = new Dictionary<(int, int), List<ScanningPeak>>();
            var demuxed = new Dictionary<(int, int), List<ScanningPeak>>();
            foreach (var result in results)
            {
                Channels += result.Channels;
                ChannelsSolved += result.ChannelsSolved;
                IonsIn += result.IonsIn;
                IonsPassedThrough += result.IonsPassedThrough;
                Bucket(result.PassedThrough, through);
                Bucket(result.Demultiplexed, demuxed);
            }
            var none = new List<ScanningPeak>();
            for (int c = firstCycle; c <= lastCycle; c++)
            {
                int cycle = c;
                var spectra = new (double[], double[])[_plan.Count];
                for (int s = 0; s < _plan.Count; s++)
                {
                    var planned = _plan[s];
                    var own = Enumerable.Range(planned.FirstBin, planned.LastBin - planned.FirstBin + 1)
                        .SelectMany(b => through.TryGetValue((cycle, b), out var list) ? list : none);
                    var sources = Enumerable.Range(planned.FirstSourceBin, planned.LastSourceBin - planned.FirstSourceBin + 1)
                        .SelectMany(b => demuxed.TryGetValue((cycle, b), out var list) ? list : none);
                    ScanningLayout.Assemble(own, sources, out double[] mz, out double[] ions);
                    spectra[s] = (mz, ions);
                }
                _built[cycle] = spectra;
            }
            _log.WriteLine(@"  read {0:F1} s, units {1:F1} s, solve {2:F1} s (read ahead {3:F1} s), layout {4:F1} s ({5} units)",
                readSeconds, unitSeconds, solveSeconds, readAheadSeconds,
                clock.Elapsed.TotalSeconds - readSeconds - unitSeconds - solveSeconds, units.Count);
        }

        private static ScanningUnit MakeUnit(double[,] a, int[] rowBins, int[] columnBins, int[] cycles, int g0, int g1,
            int k0, int k1, Dictionary<int, List<(double[] Mz, double[] Ions)>> sweeps)
        {
            var mz = new List<double>();
            var ions = new List<double>();
            var row = new List<int>();
            var cycle = new List<int>();
            for (int ci = 0; ci < cycles.Length; ci++)
            {
                var sweep = sweeps[cycles[ci]];
                for (int ri = 0; ri < rowBins.Length; ri++)
                {
                    var (m, v) = sweep[rowBins[ri]];
                    for (int p = 0; p < m.Length; p++)
                    {
                        mz.Add(m[p]);
                        ions.Add(v[p]);
                        row.Add(ri);
                        cycle.Add(ci);
                    }
                }
            }
            return new ScanningUnit(a, rowBins, columnBins, cycles, g0, g1, k0, k1)
            {
                Mz = mz.ToArray(),
                Ions = ions.ToArray(),
                Row = row.ToArray(),
                Cycle = cycle.ToArray(),
            };
        }

        /// <summary>
        /// The TOF grid of the profile spectra, taken once from the first spectrum with enough points: every
        /// spectrum of a ZenoTOF 8600 run lies on the same grid.
        /// </summary>
        private TofGrid TofGridOf(Dictionary<int, List<(double[] Mz, double[] Ions)>> sweeps)
        {
            if (_grid != null)
                return _grid;
            foreach (var sweep in sweeps.Values)
            {
                foreach (var (mz, _) in sweep)
                {
                    if (mz.Length < 1000)
                        continue;
                    _grid = TofGrid.Detect(mz);
                    if (_grid == null)
                        throw new InvalidOperationException(@"The joint solve needs profile spectra on one TOF grid.");
                    _log.WriteLine(@"TOF grid: step {0:E6} in sqrt(m/z)", _grid.Step);
                    return _grid;
                }
            }
            throw new InvalidOperationException(@"The joint solve found no profile spectrum to take the TOF grid from.");
        }

        private static void Bucket(List<ScanningPeak> peaks, Dictionary<(int, int), List<ScanningPeak>> byCell)
        {
            foreach (var peak in peaks)
            {
                if (!byCell.TryGetValue((peak.Cycle, peak.Bin), out var list))
                {
                    list = new List<ScanningPeak>();
                    byCell[(peak.Cycle, peak.Bin)] = list;
                }
                list.Add(peak);
            }
        }

        /// <summary>
        /// Every encoded bin's nonzero peaks in one sweep, in ions, read once and kept while the
        /// sweep can still be a neighbor of a block. Only the bins demultiplexing reads are filled.
        /// </summary>
        private List<(double[] Mz, double[] Ions)> SweepPeaks(int cycle)
        {
            if (_peaks.TryGetValue(cycle, out var cached))
                return cached;
            int readLo = Math.Max(0, _firstOutBin - _options.ContextBins - _options.ReachBins);
            int readHi = Math.Min(_centers.Length - 1, _lastOutBin + _options.ContextBins + _options.ReachBins);
            var sweep = new List<(double[], double[])>();
            var indices = _ms2OfCycle[cycle];
            for (int b = 0; b < _centers.Length; b++)
            {
                if (b < readLo || b > readHi || b >= indices.Length)
                {
                    sweep.Add((Array.Empty<double>(), Array.Empty<double>()));
                    continue;
                }
                var spectrum = Inner.GetSpectrum(indices[b], true);
                var mzArray = spectrum.GetMZArray();
                var intensityArray = spectrum.GetIntensityArray();
                KeepHeader(indices[b], spectrum);
                var mzs = new List<double>();
                var values = new List<double>();
                if (mzArray != null && intensityArray != null)
                {
                    int count = Math.Min(mzArray.Data.Count, intensityArray.Data.Count);
                    for (int i = 0; i < count; i++)
                    {
                        if (intensityArray.Data[i] <= 0)
                            continue;
                        mzs.Add(mzArray.Data[i]);
                        values.Add(intensityArray.Data[i] / _options.CountsPerIon);
                    }
                }
                sweep.Add((mzs.ToArray(), values.ToArray()));
                spectrum.BinaryDataArrays.Clear();
                spectrum.IntegerDataArrays.Clear();
            }
            _peaks[cycle] = sweep;
            return sweep;
        }

        /// <summary>
        /// Keeps the header of an acquired spectrum that is some layout spectrum's base, for
        /// <see cref="TakeHeader"/>; the caller drops its peaks once read.
        /// </summary>
        private void KeepHeader(int innerIndex, Spectrum spectrum)
        {
            if (!_isLayoutBase.Contains(innerIndex))
                return;
            lock (_lock)
                _headers[innerIndex] = spectrum;
        }

        /// <summary>Forgets a sweep no block needs any more: its peaks, and its headers not handed out.</summary>
        private void DropSweep(int cycle)
        {
            _peaks.Remove(cycle);
            foreach (int index in _ms2OfCycle[cycle])
                _headers.Remove(index);
        }

        private static double TargetMz(Spectrum spectrum)
        {
            return spectrum.Precursors.Count > 0
                ? spectrum.Precursors[0].IsolationWindow.CvParamValueOrDefault(CVID.MS_isolation_window_target_m_z, 0.0)
                : 0.0;
        }

        /// <summary>
        /// Marks the survey scans from SCIEX native ids ("sample=1 period=1 cycle=N experiment=E"),
        /// experiment 1 being the survey scan. False if any id is in another form.
        /// </summary>
        private static bool TryReadSurveyScansFromIds(ISpectrumList inner, bool[] isSurvey)
        {
            const string token = @"experiment=";
            for (int k = 0; k < inner.Count; k++)
            {
                string id = inner.SpectrumIdentity(k).Id;
                int at = id.IndexOf(token, StringComparison.Ordinal);
                if (at < 0 || id.IndexOf(@"cycle=", StringComparison.Ordinal) < 0)
                    return false;
                int start = at + token.Length, end = start;
                while (end < id.Length && char.IsDigit(id[end]))
                    end++;
                if (end == start)
                    return false;
                isSurvey[k] = id.Substring(start, end - start) == @"1";
            }
            return true;
        }
    }
}
