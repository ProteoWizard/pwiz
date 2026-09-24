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
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Demultiplexes stepped overlapping-window (staggered) DIA, one MS2 spectrum at a time.
    /// </summary>
    /// <remarks>
    /// <para>For each target spectrum, a local block of neighboring windows is assembled. The
    /// fragment channels are the target's own centroids, matched in other spectra within a
    /// tolerance. Each neighboring window's channel values are interpolated to the target's
    /// retention time from that window's own acquisitions, and one small NNLS per channel
    /// recovers the channel's intensity in each narrow bin. That solution then assigns the
    /// target's measured peaks to its own bins. The structure follows pwiz's
    /// OverlapDemultiplexer, so results can be compared with msconvert, with two differences
    /// that are settings (<see cref="DemuxParams.BlockMode"/>,
    /// <see cref="DemuxParams.Interpolation"/>).</para>
    /// <para>Deterministic at any thread count: the block geometries and their factorizations
    /// are built once, before the parallel loop, and are read-only inside it. Each spectrum
    /// writes only its own output slot, and all scratch state is per thread.</para>
    /// </remarks>
    internal sealed class OverlapDemultiplexer
    {
        private readonly DemuxScheme _scheme;
        private readonly IReadOnlyList<Spectrum> _spectra;
        private readonly DemuxParams _params;
        private readonly int _samplesPerSide;
        private readonly int[][] _samplesOfWindow;
        private readonly double[][] _timesOfWindow;
        private readonly BlockGeometry[] _geometryOfWindow;
        private readonly int _geometryCount;
        private readonly int _maxRows;
        private readonly int _maxColumns;
        private readonly int _maxTargetBins;

        public OverlapDemultiplexer(DemuxScheme scheme, IReadOnlyList<Spectrum> spectra,
            DemuxParams parameters)
        {
            _scheme = scheme;
            _spectra = spectra;
            _params = parameters;
            _samplesPerSide = RtInterpolator.SamplesPerSide(parameters.Interpolation);
            BuildWindowSeries(out _samplesOfWindow, out _timesOfWindow);

            var solverOfSignature = new Dictionary<string, NnlsSolver>();
            _geometryOfWindow = new BlockGeometry[scheme.Windows.Count];
            foreach (var window in scheme.Windows)
            {
                var geometry = BuildGeometry(window, solverOfSignature);
                _geometryOfWindow[window.Index] = geometry;
                _maxRows = Math.Max(_maxRows, geometry.Rows.Length);
                _maxColumns = Math.Max(_maxColumns, geometry.Solver.Columns);
                _maxTargetBins = Math.Max(_maxTargetBins, geometry.TargetColumns.Length);
            }
            _geometryCount = solverOfSignature.Count;
        }

        public DemuxResult Run()
        {
            int n = _spectra.Count;
            var output = new Spectrum[n][];
            var totals = new DemuxStatistics();
            var totalsLock = new object();
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _params.Threads) };
            Parallel.For(0, n, options,
                () => new ThreadState(_maxRows, _maxColumns, _maxTargetBins, 2 * _samplesPerSide),
                (i, loopState, state) =>
                {
                    output[i] = DemuxSpectrum(i, state);
                    return state;
                },
                state =>
                {
                    lock (totalsLock)
                        totals.Add(state.Statistics);
                });

            var spectra = new List<Spectrum>();
            foreach (var group in output)
                spectra.AddRange(group);
            totals.SpectraIn = n;
            totals.SpectraOut = spectra.Count;
            totals.Geometries = _geometryCount;
            return new DemuxResult(_scheme, spectra, totals);
        }

        private Spectrum[] DemuxSpectrum(int index, ThreadState state)
        {
            var target = _spectra[index];
            var window = _scheme.Windows[_scheme.WindowOfSpectrum[index]];
            var geometry = _geometryOfWindow[window.Index];
            int channels = target.Mzs.Length;
            int rows = geometry.Rows.Length;
            int targetBins = geometry.TargetColumns.Length;
            state.EnsureChannels(channels);

            for (int j = 0; j < targetBins; j++)
            {
                state.OutMzs[j].Clear();
                state.OutIntensities[j].Clear();
            }

            if (channels > 0)
            {
                BuildChannelRanges(target.Mzs, state.ChannelLow, state.ChannelHigh);
                for (int r = 0; r < rows; r++)
                {
                    int rowOffset = r * channels;
                    if (geometry.Rows[r] == window.Index)
                    {
                        // The target's own window, at the target's own time: its measured peaks.
                        for (int c = 0; c < channels; c++)
                            state.Signal[rowOffset + c] = target.Intensities[c];
                    }
                    else
                    {
                        FillInterpolatedRow(geometry.Rows[r], target.RetentionTime, channels,
                            state, rowOffset);
                    }
                }
                SolveChannels(target, geometry, channels, state);
            }

            var result = new Spectrum[targetBins];
            for (int j = 0; j < targetBins; j++)
            {
                var bin = _scheme.Bins[window.FirstBin + j];
                result[j] = new Spectrum
                {
                    ScanNumber = target.ScanNumber,
                    RetentionTime = target.RetentionTime,
                    PrecursorMz = bin.Center,
                    IsolationWindow = bin.ToIsolationWindow(),
                    Mzs = state.OutMzs[j].ToArray(),
                    Intensities = state.OutIntensities[j].ToArray(),
                };
            }
            return result;
        }

        private void SolveChannels(Spectrum target, BlockGeometry geometry, int channels,
            ThreadState state)
        {
            int rows = geometry.Rows.Length;
            var solver = geometry.Solver;
            var b = state.RightHandSide;
            var x = state.Solution;
            var targetColumns = geometry.TargetColumns;
            bool apportion = _params.OutputMode == DemuxOutputMode.apportioned;
            for (int c = 0; c < channels; c++)
            {
                for (int r = 0; r < rows; r++)
                    b[r] = state.Signal[r * channels + c];
                state.Statistics.Count(solver.Solve(b, x, state.Workspace));

                double observed = target.Intensities[c];
                double sum = 0;
                if (apportion)
                {
                    if (observed <= 0)
                        continue;
                    foreach (int column in targetColumns)
                        sum += x[column];
                    if (sum <= 0)
                        continue;
                }
                for (int j = 0; j < targetColumns.Length; j++)
                {
                    double value = apportion ? observed * x[targetColumns[j]] / sum : x[targetColumns[j]];
                    if (value <= 0)
                        continue;
                    state.OutMzs[j].Add(target.Mzs[c]);
                    state.OutIntensities[j].Add((float)value);
                }
            }
        }

        /// <summary>
        /// Interpolates one neighboring window's channel values to the target time, from that
        /// window's acquisitions on either side of it.
        /// </summary>
        private void FillInterpolatedRow(int rowWindow, double targetTime, int channels,
            ThreadState state, int rowOffset)
        {
            var times = _timesOfWindow[rowWindow];
            var samples = _samplesOfWindow[rowWindow];
            int after = UpperBound(times, targetTime);
            int first = Math.Max(0, after - _samplesPerSide);
            int last = Math.Min(times.Length - 1, after + _samplesPerSide - 1);
            int count = last - first + 1;
            for (int s = 0; s < count; s++)
            {
                state.StencilTimes[s] = times[first + s];
                ExtractChannels(_spectra[samples[first + s]], state.ChannelLow, state.ChannelHigh,
                    channels, state.StencilValues, s * channels);
            }
            for (int c = 0; c < channels; c++)
            {
                for (int s = 0; s < count; s++)
                    state.Values[s] = state.StencilValues[s * channels + c];
                double value = RtInterpolator.Interpolate(_params.Interpolation, state.StencilTimes,
                    state.Values, count, targetTime);
                // Clamping an interpolated observation is harmless, unlike clamping a solution.
                state.Signal[rowOffset + c] = Math.Max(0, value);
            }
        }

        /// <summary>First index whose time is greater than <paramref name="t"/>.</summary>
        private static int UpperBound(double[] times, double t)
        {
            int lo = 0, hi = times.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (times[mid] <= t)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// One channel per target centroid, the tolerance on each side. Where two channels
        /// overlap they are split at the mean of their four edges, so a peak cannot count
        /// twice (pwiz SpectrumPeakExtractor).
        /// </summary>
        private void BuildChannelRanges(double[] mzs, double[] low, double[] high)
        {
            int n = mzs.Length;
            for (int c = 0; c < n; c++)
            {
                double delta = _params.ChannelToleranceIsPpm
                    ? Math.Abs(mzs[c]) * _params.ChannelTolerance * 1e-6
                    : _params.ChannelTolerance;
                low[c] = mzs[c] - delta;
                high[c] = mzs[c] + delta;
            }
            for (int c = 0; c + 1 < n; c++)
            {
                if (high[c] > low[c + 1])
                {
                    double center = (high[c] + low[c] + high[c + 1] + low[c + 1]) / 4.0;
                    high[c] = center;
                    low[c + 1] = center;
                }
            }
        }

        /// <summary>
        /// Sums a spectrum's peaks into the channels whose range contains them.
        /// </summary>
        private static void ExtractChannels(Spectrum spectrum, double[] low, double[] high,
            int channels, double[] destination, int offset)
        {
            Array.Clear(destination, offset, channels);
            var mzs = spectrum.Mzs;
            var intensities = spectrum.Intensities;
            int start = 0;
            for (int q = 0; q < mzs.Length; q++)
            {
                double mz = mzs[q];
                if (mz < low[0])
                    continue;
                if (mz > high[channels - 1])
                    break;
                while (start < channels && high[start] < mz)
                    start++;
                for (int c = start; c < channels && low[c] <= mz; c++)
                {
                    if (mz <= high[c])
                        destination[offset + c] += intensities[q];
                }
            }
        }

        /// <summary>
        /// Each window's spectra in time order. A spectrum at exactly the same time as the
        /// previous one of its window is left out of the series, since interpolation needs
        /// strictly increasing times; it is still demultiplexed as a target.
        /// </summary>
        private void BuildWindowSeries(out int[][] samplesOfWindow, out double[][] timesOfWindow)
        {
            int windowCount = _scheme.Windows.Count;
            var lists = new List<int>[windowCount];
            for (int w = 0; w < windowCount; w++)
                lists[w] = new List<int>();
            for (int i = 0; i < _spectra.Count; i++)
                lists[_scheme.WindowOfSpectrum[i]].Add(i);

            samplesOfWindow = new int[windowCount][];
            timesOfWindow = new double[windowCount][];
            for (int w = 0; w < windowCount; w++)
            {
                var list = lists[w];
                list.Sort((a, b) => // Array.Sort OK: the final key is the unique spectrum index
                {
                    int c = _spectra[a].RetentionTime.CompareTo(_spectra[b].RetentionTime);
                    return c != 0 ? c : a.CompareTo(b);
                });
                var samples = new List<int>(list.Count);
                var times = new List<double>(list.Count);
                foreach (int i in list)
                {
                    double t = _spectra[i].RetentionTime;
                    if (times.Count > 0 && t <= times[times.Count - 1])
                        continue;
                    samples.Add(i);
                    times.Add(t);
                }
                samplesOfWindow[w] = samples.ToArray();
                timesOfWindow[w] = times.ToArray();
            }
        }

        private BlockGeometry BuildGeometry(AcquisitionWindow target,
            Dictionary<string, NnlsSolver> solverOfSignature)
        {
            var windows = _scheme.Windows;
            int binTotal = _scheme.Bins.Count;
            int blockBins = Math.Min(Math.Max(1, _params.BlockBins), binTotal);
            double center = target.BinIndexCenter;
            // Math.Round rounds half to even, as pwiz's does, so both choose the same slice.
            int sliceFirst = (int)Math.Round(center - blockBins / 2.0);
            sliceFirst = Math.Max(0, Math.Min(sliceFirst, binTotal - blockBins));
            int sliceLast = sliceFirst + blockBins - 1;

            // Nearest windows first; ties by signed distance, then by index.
            var candidates = new List<int>(windows.Count);
            for (int w = 0; w < windows.Count; w++)
                candidates.Add(w);
            candidates.Sort((a, b) => // Array.Sort OK: the final key is the unique window index
            {
                double da = windows[a].BinIndexCenter - center;
                double db = windows[b].BinIndexCenter - center;
                int c = Math.Abs(da).CompareTo(Math.Abs(db));
                if (c != 0)
                    return c;
                c = da.CompareTo(db);
                return c != 0 ? c : a.CompareTo(b);
            });

            var rows = new List<int>();
            int columnFirst, columnLast;
            if (_params.BlockMode == DemuxBlockMode.truncated_slice)
            {
                for (int i = 0; i < Math.Min(blockBins, candidates.Count); i++)
                    rows.Add(candidates[i]);
                columnFirst = sliceFirst;
                columnLast = sliceLast;
            }
            else
            {
                columnFirst = int.MaxValue;
                columnLast = int.MinValue;
                foreach (int w in candidates)
                {
                    if (windows[w].LastBin < sliceFirst || windows[w].FirstBin > sliceLast)
                        continue;
                    rows.Add(w);
                    columnFirst = Math.Min(columnFirst, windows[w].FirstBin);
                    columnLast = Math.Max(columnLast, windows[w].LastBin);
                }
            }
            rows.Sort((a, b) => // Array.Sort OK: the final key is the unique window index
            {
                int c = windows[a].BinIndexCenter.CompareTo(windows[b].BinIndexCenter);
                return c != 0 ? c : a.CompareTo(b);
            });
            if (target.FirstBin < columnFirst || target.LastBin > columnLast)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    @"Window [{0}, {1}] does not fit in a demux block of {2} bins.",
                    target.LowerBound, target.UpperBound, blockBins));
            }

            int columns = columnLast - columnFirst + 1;
            var mask = new double[rows.Count, columns];
            var signature = new StringBuilder();
            signature.Append(columns.ToString(CultureInfo.InvariantCulture));
            for (int r = 0; r < rows.Count; r++)
            {
                var window = windows[rows[r]];
                int lo = Math.Max(window.FirstBin, columnFirst) - columnFirst;
                int hi = Math.Min(window.LastBin, columnLast) - columnFirst;
                for (int c = lo; c <= hi; c++)
                    mask[r, c] = 1;
                signature.Append(';').Append(lo.ToString(CultureInfo.InvariantCulture))
                    .Append('-').Append(hi.ToString(CultureInfo.InvariantCulture));
            }
            if (!solverOfSignature.TryGetValue(signature.ToString(), out var solver))
            {
                solver = new NnlsSolver(mask);
                solverOfSignature.Add(signature.ToString(), solver);
            }

            var targetColumns = new int[target.BinCount];
            for (int j = 0; j < targetColumns.Length; j++)
                targetColumns[j] = target.FirstBin + j - columnFirst;
            return new BlockGeometry(rows.ToArray(), targetColumns, solver);
        }

        private sealed class BlockGeometry
        {
            public BlockGeometry(int[] rows, int[] targetColumns, NnlsSolver solver)
            {
                Rows = rows;
                TargetColumns = targetColumns;
                Solver = solver;
            }

            /// <summary>Window indices, one per block row, in m/z order.</summary>
            public int[] Rows { get; }

            /// <summary>Block columns holding the target window's own bins, in bin order.</summary>
            public int[] TargetColumns { get; }

            public NnlsSolver Solver { get; }
        }

        private sealed class ThreadState
        {
            private readonly int _maxRows;
            private readonly int _maxStencil;

            public ThreadState(int maxRows, int maxColumns, int maxTargetBins, int maxStencil)
            {
                _maxRows = maxRows;
                _maxStencil = maxStencil;
                Workspace = new NnlsSolver.Workspace(maxColumns);
                RightHandSide = new double[maxRows];
                Solution = new double[maxColumns];
                StencilTimes = new double[maxStencil];
                Values = new double[maxStencil];
                OutMzs = new List<double>[maxTargetBins];
                OutIntensities = new List<float>[maxTargetBins];
                for (int j = 0; j < maxTargetBins; j++)
                {
                    OutMzs[j] = new List<double>();
                    OutIntensities[j] = new List<float>();
                }
                EnsureChannels(0);
            }

            public NnlsSolver.Workspace Workspace { get; }
            public double[] RightHandSide { get; }
            public double[] Solution { get; }
            public double[] StencilTimes { get; }
            public double[] Values { get; }
            public List<double>[] OutMzs { get; }
            public List<float>[] OutIntensities { get; }
            public DemuxStatistics Statistics { get; } = new DemuxStatistics();

            public double[] ChannelLow { get; private set; }
            public double[] ChannelHigh { get; private set; }
            public double[] Signal { get; private set; }
            public double[] StencilValues { get; private set; }

            public void EnsureChannels(int channels)
            {
                if (ChannelLow != null && ChannelLow.Length >= channels)
                    return;
                int capacity = Math.Max(channels, 256);
                ChannelLow = new double[capacity];
                ChannelHigh = new double[capacity];
                Signal = new double[_maxRows * capacity];
                StencilValues = new double[_maxStencil * capacity];
            }
        }
    }
}
