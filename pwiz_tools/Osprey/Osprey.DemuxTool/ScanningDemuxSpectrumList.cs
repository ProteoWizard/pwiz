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
        private bool _surveyCalibrated;
        private TofGrid _surveyGrid;                          // MS1's own grid: it is not the MS2 grid
        private JointDemuxParams _surveyParameters;           // the joint settings with MS1's peak width
        private double _surveyCountsPerIon = double.NaN;      // of the last MS1 spectrum whose low levels fitted
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
                if (getBinaryData && _options.JointMs1)
                    CentroidSurvey(survey);
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
        /// Replaces a profile MS1 spectrum's points by its centroids from the joint solve with one bin and one
        /// position, on MS1's grid and at the spectrum's own counts per ion, close centroids merged within the
        /// peak's sigma. Left as read when MS1 is not on one TOF grid or has no ion scale to estimate.
        /// </summary>
        private void CentroidSurvey(Spectrum survey)
        {
            var mzArray = survey.GetMZArray();
            var countArray = survey.GetIntensityArray();
            if (mzArray == null || countArray == null || mzArray.Data.Count == 0)
                return;
            var mz = mzArray.Data;
            var counts = countArray.Data;
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
                return;
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
            survey.SetMZIntensityArrays(centroidMz, centroidCounts, CVID.MS_number_of_detector_counts);
            MarkCentroid(survey);
        }

        /// <summary>
        /// MS1's grid, from the first MS1 spectrum centroided, and its peak shape, from the strong isolated peaks of
        /// 30 MS1 spectra from the middle of the run (MS1 peaks are wider in samples than MS2's).
        /// </summary>
        private void CalibrateSurvey(IList<double> mz, IList<double> counts, double countsPerIon)
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
                var spectrum = Inner.GetSpectrum(_ms1OfCycle[CycleCount / 2 + c], true);
                var sm = spectrum.GetMZArray();
                var si = spectrum.GetIntensityArray();
                if (sm == null || si == null)
                    continue;
                double q = IonCalibration.CountsPerIon(si.Data, out double fit);
                if (fit < IonCalibration.MIN_FIT)
                    q = countsPerIon;
                shape.Add(sm.Data, si.Data, _surveyGrid, 400 * q);
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
            // hold copies of their peaks, and the source spectra are read from this thread and its read workers.
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
                var spectra = new (double[], double[])[_plan.Count];
                for (int s = 0; s < _plan.Count; s++)
                {
                    var planned = _plan[s];
                    var own = Enumerable.Range(planned.FirstBin, planned.LastBin - planned.FirstBin + 1)
                        .SelectMany(b => through.TryGetValue((cycle, b), out var list) ? list : none);
                    var sources = Enumerable.Range(planned.FirstSourceBin, planned.LastSourceBin - planned.FirstSourceBin + 1)
                        .SelectMany(b => demuxed.TryGetValue((cycle, b), out var list) ? list : none);
                    ScanningLayout.Assemble(own, sources, out double[] mz, out double[] ions, _options.MergePpm, mergeWithin);
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
            var headers = new Spectrum[_centers.Length];
            // The headers are kept after the reads, on this thread: it holds the list's lock.
            Parallel.For(0, _centers.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.ReadThreads) }, b =>
            {
                if (b < readLo || b > readHi || b >= indices.Length)
                {
                    bins[b] = (Array.Empty<double>(), Array.Empty<double>());
                    return;
                }
                var spectrum = Inner.GetSpectrum(indices[b], true);
                var mzArray = spectrum.GetMZArray();
                var intensityArray = spectrum.GetIntensityArray();
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
                bins[b] = (mzs.ToArray(), values.ToArray());
                spectrum.BinaryDataArrays.Clear();
                spectrum.IntegerDataArrays.Clear();
                headers[b] = spectrum;
            });
            for (int b = 0; b < headers.Length; b++)
            {
                if (headers[b] != null)
                    KeepHeader(indices[b], headers[b]);
            }
            var sweep = bins.ToList();
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
