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

namespace pwiz.Osprey.Demux
{
    /// <summary>Settings for <see cref="ScanningDemuxPipeline"/>.</summary>
    public sealed class ScanningDemuxOptions
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

        /// <summary>
        /// Threads reading one sweep's spectra from the source, for a source that serves concurrent requests:
        /// the vendor readers do (SCIEX .wiff2: 4x on four threads, peaks identical). 1 reads serially.
        /// </summary>
        public int ReadThreads { get; set; } = 1;

        /// <summary>
        /// Demultiplexed peaks of neighbouring positions closer than this are summed into one in an output
        /// spectrum. The channel solve gives one fragment nearly the same m/z in every position; the joint solve
        /// centroids each position on its own, so the same fragment's centroids can lie a grid sample apart.
        /// </summary>
        public double MergePpm { get; set; } = ScanningLayout.MERGE_PPM;

        /// <summary>
        /// For the joint solve: neighbouring positions' centroids closer than this many TOF peak sigmas (at their
        /// m/z, <see cref="JointDemuxParams.SigmaAt"/>) are summed into one, in place of <see cref="MergePpm"/>;
        /// 0 uses <see cref="MergePpm"/>. Two Gaussians closer than sigma are not resolved (Centrix's merge); on
        /// the ZT Scan slice two sigmas left about 60% of one sigma's close doublets and found 3-4% more peptides.
        /// </summary>
        public double JointMergeSigmas { get; set; } = 2;

        /// <summary>
        /// Centroid MS1 with the joint solve (one bin, one position) instead of passing it through: needs profile
        /// MS1, which is on a TOF grid of its own, and takes each spectrum's own counts per ion (SCIEX MS1
        /// intensities are rates over an accumulation time that varies from spectrum to spectrum).
        /// </summary>
        public bool JointMs1 { get; set; }

        /// <summary>
        /// Fit the joint solve's MS2 with the TOF peak kernels measured from the file's first batch of sweeps
        /// (<see cref="TofPeakShape"/>) instead of the Gaussian of <see cref="JointDemuxParams.PeakSigmaSamples"/>.
        /// MS1, with <see cref="JointMs1"/>, is always fitted with its own measured kernels.
        /// </summary>
        public bool MeasuredPeakShape { get; set; }
    }

    /// <summary>
    /// The scanning-quadrupole scheme's part of <see cref="DemuxPipeline"/> (see <see cref="ScanningDemuxPipeline"/>):
    /// demultiplexes a scanning-quadrupole acquisition (SCIEX ZT Scan) and lays it out as
    /// <see cref="ScanningDemuxOptions.Layout"/> says. MS1 spectra pass through, or are centroided by the joint
    /// solve; each output MS2 spectrum is built on the acquired spectrum of the middle bin of its window, with the
    /// window and peaks replaced.
    /// </summary>
    /// <remarks>
    /// Blocks of sweeps are read in order, a batch at a time, and demultiplexed in parallel, one
    /// solve per block of encoded bins.
    /// </remarks>
    internal sealed class ScanningDemuxPlan : DemuxPlan
    {
        private readonly IDemuxSource _source;
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
        private readonly Dictionary<int, List<(double[] Mz, double[] Ions)>> _peaks = new Dictionary<int, List<(double[], double[])>>();
        private readonly Dictionary<int, double[,]> _transmission = new Dictionary<int, double[,]>();
        private readonly object _lock = new object();
        private readonly HashSet<int> _isLayoutBase = new HashSet<int>();                // acquired spectra layout spectra are built on
        private bool _surveyCalibrated;
        private TofGrid _surveyGrid;                          // MS1's own grid: it is not the MS2 grid
        private JointDemuxParams _surveyParameters;           // the joint settings with MS1's peak width
        private double _surveyCountsPerIon = double.NaN;      // of the last MS1 spectrum whose low levels fitted
        private TofGrid _grid;

        public ScanningDemuxPlan(IDemuxSource source, ScanningKernel kernel, ScanningDemuxOptions options,
            TextWriter log)
        {
            _source = source;
            _kernel = kernel;
            _options = options;
            _log = log;

            // A cycle is a survey scan and the sweep after it.
            DemuxSchemeDetector.FindCycles(source, _ms1OfCycle, _ms2OfCycle);
            CycleCount = _ms1OfCycle.Count;

            // Encoded bins are the sweep positions; their centers are the same in every sweep.
            var first = _ms2OfCycle[0];
            _centers = first.Select(k => source.Describe(k).Target).ToArray();
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
                    _isLayoutBase.Add(SourceIndexOf(i));
            }
        }

        public int CycleCount { get; }
        public int FirstCycle { get; }
        public override int LastCycle { get; }

        /// <summary>Output spectra: each cycle's survey scan, then its layout spectra.</summary>
        public override int OutputCount
        {
            get { return _output.Count; }
        }

        /// <summary>A layout spectrum's cycle; -1 for a survey scan, which is not demultiplexed.</summary>
        public override int CycleOfOutput(int output)
        {
            var (cycle, slot) = _output[output];
            return slot < 0 ? -1 : cycle;
        }

        public override string DescribeBatch(int firstCycle, int lastCycle)
        {
            return string.Format(CultureInfo.InvariantCulture, @"  sweeps {0}-{1} of {2} written", firstCycle, lastCycle, CycleCount);
        }

        /// <summary>Whether an output spectrum is a survey scan (passed through, or centroided by <see cref="CentroidSurvey"/>).</summary>
        public bool IsSurvey(int index)
        {
            return _output[index].Slot < 0;
        }

        /// <summary>The acquired spectrum an output spectrum is built on: the MS1, or its window's middle bin.</summary>
        public int BaseIndex(int index)
        {
            return SourceIndexOf(index);
        }

        /// <summary>Whether an acquired spectrum is some layout spectrum's base (its header is worth keeping).</summary>
        public bool IsLayoutBase(int sourceIndex)
        {
            return _isLayoutBase.Contains(sourceIndex);
        }

        /// <summary>A layout spectrum's isolation window: the bins it reports.</summary>
        public void WindowOf(int index, out double low, out double high)
        {
            var planned = _plan[_output[index].Slot];
            low = _centers[planned.FirstBin] - HalfWidth(planned.FirstBin);
            high = _centers[planned.LastBin] + HalfWidth(planned.LastBin);
        }

        /// <summary>
        /// A profile MS1 spectrum's centroids from the joint solve with one bin and one position, on MS1's grid and
        /// at the spectrum's own counts per ion, close centroids merged within the peak's sigma, in counts. Null to
        /// leave it as read: when MS1 is not on one TOF grid, or has no ion scale to estimate.
        /// </summary>
        public (double[] Mz, double[] Counts)? CentroidSurvey(IReadOnlyList<double> mz, IReadOnlyList<double> counts)
        {
            if (mz == null || counts == null || mz.Count == 0)
                return null;
            double q = IonCalibration.CountsPerIon(counts, out double fit);
            lock (_lock)
            {
                if (!_surveyCalibrated)
                    CalibrateSurvey(mz, counts, q);
                if (fit >= IonCalibration.MIN_FIT)
                    _surveyCountsPerIon = q;
                else if (!double.IsNaN(_surveyCountsPerIon))
                    q = _surveyCountsPerIon;
            }
            var grid = _surveyGrid;
            var parameters = _surveyParameters;
            if (grid == null || double.IsNaN(q) || q <= 0)
                return null;
            var pointMz = new List<double>();
            var pointIons = new List<double>();
            for (int i = 0; i < mz.Count; i++)
            {
                if (counts[i] <= 0)
                    continue;
                pointMz.Add(mz[i]);
                pointIons.Add(counts[i] / q);
            }
            var unit = new ScanningUnit(new double[,] { { 1 } }, new[] { 0 }, new[] { 0 }, new[] { 0 }, 0, 0, 0, 0)
            {
                Mz = pointMz.ToArray(),
                Ions = pointIons.ToArray(),
                Row = new int[pointMz.Count],
                Cycle = new int[pointMz.Count],
            };
            var result = JointDemultiplexer.DemuxUnit(unit, parameters, grid);
            double sigmas = _options.JointMergeSigmas;
            Func<double, double> mergeWithin = null;
            if (sigmas > 0)
                mergeWithin = m => sigmas * parameters.SigmaAt(m) * 2 * Math.Sqrt(m) * grid.Step;
            ScanningLayout.Assemble(Array.Empty<ScanningPeak>(), result.Demultiplexed, out double[] centroidMz,
                out double[] centroidIons, _options.MergePpm, mergeWithin);
            var centroidCounts = new double[centroidIons.Length];
            for (int i = 0; i < centroidIons.Length; i++)
                centroidCounts[i] = centroidIons[i] * q;
            return (centroidMz, centroidCounts);
        }

        /// <summary>
        /// MS1's grid, from the first MS1 spectrum centroided, and its peak shape, from the strong isolated peaks of
        /// 30 MS1 spectra from the middle of the run (MS1 peaks are wider in samples than MS2's).
        /// </summary>
        private void CalibrateSurvey(IReadOnlyList<double> mz, IReadOnlyList<double> counts, double countsPerIon)
        {
            _surveyCalibrated = true;
            _surveyGrid = TofGrid.Detect(mz.ToList());
            if (_surveyGrid == null)
            {
                _log.WriteLine(@"MS1: not on one TOF grid; passed through as read");
                return;
            }
            // The kernel from strong peaks only (400 ions or more at the top): MS1 is dense, and the top samples of
            // weaker ones are too noisy to centre and scale a peak by. So few MS1 peaks are strong and isolated (about
            // 13 a spectrum on ZT Scan) that it takes 30 spectra and 200 m/z bins.
            var shape = new TofPeakShape(Enumerable.Range(0, 11).Select(i => 200.0 * i).ToArray());
            for (int c = 0; c < 30 && CycleCount / 2 + c < _ms1OfCycle.Count; c++)
            {
                int index = _ms1OfCycle[CycleCount / 2 + c];
                _source.Read(index, out var sm, out var si);
                if (sm != null && si != null)
                {
                    double q = IonCalibration.CountsPerIon(si, out double fit);
                    if (fit < IonCalibration.MIN_FIT)
                        q = countsPerIon;
                    shape.Add(sm, si, _surveyGrid, 400 * q);
                }
                _source.Release(index);
            }
            _surveyParameters = _options.JointParameters.Copy();
            var kernels = shape.Kernels(_surveyParameters.PeakHalfWidth, 50);
            if (kernels.HasValue)
            {
                _surveyParameters.PeakShapeMz = kernels.Value.Mz;
                _surveyParameters.PeakShapes = kernels.Value.Kernels;
            }
            _log.WriteLine(@"MS1: own TOF grid, step {0:E6}; peak shape {1} ({2} isolated peaks); counts per ion from each spectrum",
                _surveyGrid.Step, kernels.HasValue ? DescribeKernels(_surveyParameters) : @"the Gaussian of the sigma table (too few peaks)",
                shape.Peaks);
        }

        /// <summary>The acquired spectrum an output spectrum is built on: the MS1, or its window's middle bin.</summary>
        private int SourceIndexOf(int index)
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

        /// <summary>Sweeps demultiplexed together: enough blocks to keep every thread busy.</summary>
        public override int BatchCycles
        {
            get
            {
                int groups = Math.Max(1, (_lastOutBin - _firstOutBin + _options.GroupBins) / _options.GroupBins);
                return Math.Max(1, 2 * _options.Threads / groups) * _options.BlockCycles;
            }
        }

        /// <summary>
        /// The sweeps before this batch's context are written: no batch still to be solved needs them (raw
        /// output: the sweeps before the batch, which reads no context).
        /// </summary>
        public override void Forget(int firstCycle)
        {
            int keep = _options.Raw ? firstCycle : Math.Max(0, firstCycle - _options.CyclePad);
            foreach (int old in _peaks.Keys.Where(c => c < keep).ToList())
                DropSweep(old);
        }

        /// <summary>
        /// Reads a batch's sweeps and makes one solve per block of sweeps and group of encoded bins; raw output
        /// solves nothing.
        /// </summary>
        public override IReadOnlyList<DemuxWork> MakeWork(int firstCycle, int lastCycle, out double readSeconds)
        {
            var work = new List<DemuxWork>();
            readSeconds = 0;
            if (_options.Raw)
                return work;
            int readLo = Math.Max(0, _firstOutBin - _options.ContextBins - _options.ReachBins);
            int readHi = Math.Min(_centers.Length - 1, _lastOutBin + _options.ContextBins + _options.ReachBins);
            int padLo = Math.Max(0, firstCycle - _options.CyclePad);
            int padHi = Math.Min(CycleCount - 1, lastCycle + _options.CyclePad);
            var clock = Stopwatch.StartNew();
            var sweeps = new Dictionary<int, List<(double[] Mz, double[] Ions)>>();
            for (int c = padLo; c <= padHi; c++)
                sweeps[c] = SweepPeaks(c);
            readSeconds = clock.Elapsed.TotalSeconds;
            var grid = _options.Joint ? TofGridOf(sweeps) : null;

            // One unit per block of sweeps and group of encoded bins.
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
                    // The units hold copies of their peaks, so they solve on the solve threads while the pipeline
                    // goes on reading, laying out and writing.
                    work.Add(new DemuxWork(() => _options.Joint
                        ? JointDemultiplexer.DemuxUnit(unit, _options.JointParameters, grid)
                        : ScanningDemultiplexer.DemuxUnit(unit, _options.Parameters), unit.Mz.Length));
                }
            }
            return work;
        }

        /// <summary>Peaks by (cycle, bin), then each layout spectrum from its bins and source positions.</summary>
        public override void LayOut(int firstCycle, int lastCycle, ScanningUnitResult[] results,
            IDictionary<int, (double[] Mz, double[] Ions)> peaks)
        {
            if (_options.Raw)
            {
                for (int c = firstCycle; c <= lastCycle; c++)
                {
                    var sweep = SweepPeaks(c);
                    for (int s = 0; s < _plan.Count; s++)
                        peaks[OutputIndex(c, s)] = sweep[_plan[s].FirstBin];
                }
                return;
            }
            var grid = _options.Joint ? _grid : null;
            var through = new Dictionary<(int, int), List<ScanningPeak>>();
            var demuxed = new Dictionary<(int, int), List<ScanningPeak>>();
            foreach (var result in results)
            {
                Bucket(result.PassedThrough, through);
                Bucket(result.Demultiplexed, demuxed);
            }
            var none = new List<ScanningPeak>();
            // The joint solve centroids each position on its own, so one fragment's centroids from neighbouring
            // positions can lie a grid sample apart: merged when closer than the TOF peak's sigma, in m/z
            // (dm/dk = 2 sqrt(m/z) step on the grid).
            Func<double, double> mergeWithin = null;
            if (grid != null && _options.JointMergeSigmas > 0)
            {
                var joint = _options.JointParameters;
                double sigmas = _options.JointMergeSigmas, step = grid.Step;
                mergeWithin = m => sigmas * joint.SigmaAt(m) * 2 * Math.Sqrt(m) * step;
            }
            for (int c = firstCycle; c <= lastCycle; c++)
            {
                int cycle = c;
                for (int s = 0; s < _plan.Count; s++)
                {
                    var planned = _plan[s];
                    var own = Enumerable.Range(planned.FirstBin, planned.LastBin - planned.FirstBin + 1)
                        .SelectMany(b => through.TryGetValue((cycle, b), out var list) ? list : none);
                    var sources = Enumerable.Range(planned.FirstSourceBin, planned.LastSourceBin - planned.FirstSourceBin + 1)
                        .SelectMany(b => demuxed.TryGetValue((cycle, b), out var list) ? list : none);
                    ScanningLayout.Assemble(own, sources, out double[] mz, out double[] ions, _options.MergePpm, mergeWithin);
                    peaks[OutputIndex(cycle, s)] = (mz, ions);
                }
            }
        }

        /// <summary>The output index of a cycle's layout spectrum: after the cycle's survey scan.</summary>
        private int OutputIndex(int cycle, int slot)
        {
            return (cycle - FirstCycle) * (_plan.Count + 1) + 1 + slot;
        }

        private static ScanningUnit MakeUnit(double[,] a, int[] rowBins, int[] columnBins, int[] cycles, int g0, int g1,
            int k0, int k1, Dictionary<int, List<(double[] Mz, double[] Ions)>> sweeps)
        {
            // Sized once: while the solve threads run, growing lists point by point costs more than the copy.
            int count = 0;
            for (int ci = 0; ci < cycles.Length; ci++)
            {
                var sweep = sweeps[cycles[ci]];
                for (int ri = 0; ri < rowBins.Length; ri++)
                    count += sweep[rowBins[ri]].Mz.Length;
            }
            var mz = new double[count];
            var ions = new double[count];
            var row = new int[count];
            var cycle = new int[count];
            int at = 0;
            for (int ci = 0; ci < cycles.Length; ci++)
            {
                var sweep = sweeps[cycles[ci]];
                for (int ri = 0; ri < rowBins.Length; ri++)
                {
                    var (m, v) = sweep[rowBins[ri]];
                    Array.Copy(m, 0, mz, at, m.Length);
                    Array.Copy(v, 0, ions, at, v.Length);
                    Array.Fill(row, ri, at, m.Length);
                    Array.Fill(cycle, ci, at, m.Length);
                    at += m.Length;
                }
            }
            return new ScanningUnit(a, rowBins, columnBins, cycles, g0, g1, k0, k1)
            {
                Mz = mz,
                Ions = ions,
                Row = row,
                Cycle = cycle,
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
                    if (_options.MeasuredPeakShape)
                        MeasurePeakShape(sweeps);
                    return _grid;
                }
            }
            throw new InvalidOperationException(@"The joint solve found no profile spectrum to take the TOF grid from.");
        }

        /// <summary>
        /// The MS2 kernels measured from the first batch's profile sweeps (their points in ions), strong isolated
        /// peaks of 50 ions or more, for the joint settings every block of the run then solves with.
        /// </summary>
        private void MeasurePeakShape(Dictionary<int, List<(double[] Mz, double[] Ions)>> sweeps)
        {
            var shape = new TofPeakShape(new[] { 100.0, 300, 450, 600, 800, 1100, 2000 });
            foreach (var sweep in sweeps.Values)
            {
                foreach (var (mz, ions) in sweep)
                    shape.Add(mz, ions, _grid, 50);
            }
            var parameters = _options.JointParameters;
            var kernels = shape.Kernels(parameters.PeakHalfWidth, 100);
            if (!kernels.HasValue)
            {
                _log.WriteLine(@"TOF peak shape: {0} isolated peaks, too few in every m/z range; fitting with the Gaussian", shape.Peaks);
                return;
            }
            parameters.PeakShapeMz = kernels.Value.Mz;
            parameters.PeakShapes = kernels.Value.Kernels;
            _log.WriteLine(@"TOF peak shape (MS2, {0} isolated peaks): {1}", shape.Peaks, DescribeKernels(parameters));
        }

        /// <summary>Each measured kernel as its m/z, its centre value and its second moment.</summary>
        private static string DescribeKernels(JointDemuxParams parameters)
        {
            int half = parameters.PeakHalfWidth;
            return string.Join(@"; ", parameters.PeakShapeMz.Select((mz, i) => string.Format(CultureInfo.InvariantCulture,
                @"{0:F0} m/z B[0] {1:F3}, sigma {2:F2}", mz, parameters.PeakShapes[i][half], parameters.SigmaAt(mz))));
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
            var indices = _ms2OfCycle[cycle];
            var bins = new (double[], double[])[_centers.Length];
            Parallel.For(0, _centers.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.ReadThreads) }, b =>
            {
                if (b < readLo || b > readHi || b >= indices.Length)
                {
                    bins[b] = (Array.Empty<double>(), Array.Empty<double>());
                    return;
                }
                _source.Read(indices[b], out var mzData, out var intensityData);
                var mzs = Array.Empty<double>();
                var values = Array.Empty<double>();
                if (mzData != null && intensityData != null)
                {
                    int count = Math.Min(mzData.Count, intensityData.Count), kept = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (intensityData[i] > 0)
                            kept++;
                    }
                    mzs = new double[kept];
                    values = new double[kept];
                    for (int i = 0, k = 0; i < count; i++)
                    {
                        if (intensityData[i] <= 0)
                            continue;
                        mzs[k] = mzData[i];
                        values[k++] = intensityData[i] / _options.CountsPerIon;
                    }
                }
                bins[b] = (mzs, values);
                _source.Release(indices[b]);
            });
            var sweep = bins.ToList();
            _peaks[cycle] = sweep;
            return sweep;
        }

        /// <summary>Forgets a sweep no block needs any more: its peaks, and whatever the source kept of it.</summary>
        private void DropSweep(int cycle)
        {
            _peaks.Remove(cycle);
            foreach (int index in _ms2OfCycle[cycle])
                _source.Forget(index);
        }

    }

    /// <summary>
    /// Demultiplexes a scanning-quadrupole acquisition (SCIEX ZT Scan) and lays it out as
    /// <see cref="ScanningDemuxOptions.Layout"/> says, on the shared <see cref="DemuxPipeline"/>. MS1 spectra pass
    /// through, or are centroided by the joint solve; each output MS2 spectrum is built on the acquired spectrum of the
    /// middle bin of its window, with the window and peaks replaced.
    /// </summary>
    public sealed class ScanningDemuxPipeline : DemuxPipeline
    {
        private readonly ScanningDemuxPlan _scanning;

        public ScanningDemuxPipeline(IDemuxSource source, ScanningKernel kernel, ScanningDemuxOptions options,
            TextWriter log)
            : this(new ScanningDemuxPlan(source, kernel, options, log), options, log)
        {
        }

        private ScanningDemuxPipeline(ScanningDemuxPlan plan, ScanningDemuxOptions options, TextWriter log)
            : base(plan, options, log)
        {
            _scanning = plan;
        }

        public int CycleCount
        {
            get { return _scanning.CycleCount; }
        }

        public int FirstCycle
        {
            get { return _scanning.FirstCycle; }
        }

        public int LastCycle
        {
            get { return _scanning.LastCycle; }
        }

        /// <summary>Whether an output spectrum is a survey scan (passed through, or centroided by <see cref="CentroidSurvey"/>).</summary>
        public bool IsSurvey(int index)
        {
            return _scanning.IsSurvey(index);
        }

        /// <summary>The acquired spectrum an output spectrum is built on: the MS1, or its window's middle bin.</summary>
        public int BaseIndex(int index)
        {
            return _scanning.BaseIndex(index);
        }

        /// <summary>Whether an acquired spectrum is some layout spectrum's base (its header is worth keeping).</summary>
        public bool IsLayoutBase(int sourceIndex)
        {
            return _scanning.IsLayoutBase(sourceIndex);
        }

        /// <summary>A layout spectrum's isolation window: the bins it reports.</summary>
        public void WindowOf(int index, out double low, out double high)
        {
            _scanning.WindowOf(index, out low, out high);
        }

        /// <summary>A profile MS1 spectrum's centroids from the joint solve; null to leave it as read.</summary>
        public (double[] Mz, double[] Counts)? CentroidSurvey(IReadOnlyList<double> mz, IReadOnlyList<double> counts)
        {
            return _scanning.CentroidSurvey(mz, counts);
        }
    }
}
