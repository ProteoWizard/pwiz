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

namespace pwiz.Osprey.Demux
{
    /// <summary>Settings for <see cref="ScanningDemultiplexer"/>.</summary>
    public sealed class ScanningDemuxParams
    {
        /// <summary>How far a peak may lie from its fragment channel's center, in ppm.</summary>
        public double ChannelTolerancePpm { get; set; } = 10;

        /// <summary>A channel with fewer ions over its block passes through undemultiplexed.</summary>
        public double MinChannelIons { get; set; } = 8;

        /// <summary>A channel seen in fewer (encoded bin, sweep) cells passes through.</summary>
        public int MinChannelCells { get; set; } = 3;

        /// <summary>Demultiplexed intensities below this many ions are not written.</summary>
        public double MinOutputIons { get; set; } = 0.2;

        /// <summary>
        /// Refit each solve with Poisson row weights 1 / max(mu, <see cref="WeightFloorIons"/>),
        /// mu from the unweighted fit (spec 6.2). It cuts the counting noise demultiplexing adds,
        /// most at low abundance.
        /// </summary>
        public bool PoissonWeights { get; set; } = true;

        /// <summary>The expected count below which a row's weight stops growing, in ions.</summary>
        public double WeightFloorIons { get; set; } = 0.5;
    }

    /// <summary>
    /// One block of a scanning-quadrupole acquisition: a run of encoded bins over a run of sweeps,
    /// its transmission matrix, and its peaks.
    /// </summary>
    public sealed class ScanningUnit
    {
        /// <param name="transmission">
        /// Rows: the encoded bins whose spectra are in the block (<paramref name="rowBins"/>).
        /// Columns: the source positions solved for (<paramref name="columnBins"/>).
        /// Entry: the transmission of a precursor at that position into that bin's spectrum.
        /// </param>
        /// <param name="rowBins">Encoded bin index of each row.</param>
        /// <param name="columnBins">Encoded bin index of each column.</param>
        /// <param name="cycles">The sweeps in the block, in acquisition order.</param>
        /// <param name="firstCoreBin">First encoded bin whose output the block owns.</param>
        /// <param name="lastCoreBin">Last encoded bin whose output the block owns.</param>
        /// <param name="firstCoreCycle">First sweep whose output the block owns.</param>
        /// <param name="lastCoreCycle">Last sweep whose output the block owns.</param>
        public ScanningUnit(double[,] transmission, int[] rowBins, int[] columnBins, int[] cycles,
            int firstCoreBin, int lastCoreBin, int firstCoreCycle, int lastCoreCycle)
        {
            if (transmission.GetLength(0) != rowBins.Length || transmission.GetLength(1) != columnBins.Length)
                throw new ArgumentException(@"The transmission matrix does not match the row and column bins.");
            Transmission = transmission;
            RowBins = rowBins;
            ColumnBins = columnBins;
            Cycles = cycles;
            FirstCoreBin = firstCoreBin;
            LastCoreBin = lastCoreBin;
            FirstCoreCycle = firstCoreCycle;
            LastCoreCycle = lastCoreCycle;
        }

        public double[,] Transmission { get; }
        public int[] RowBins { get; }
        public int[] ColumnBins { get; }
        public int[] Cycles { get; }
        public int FirstCoreBin { get; }
        public int LastCoreBin { get; }
        public int FirstCoreCycle { get; }
        public int LastCoreCycle { get; }

        /// <summary>Peak m/z values.</summary>
        public double[] Mz { get; set; }

        /// <summary>Peak intensities in ions.</summary>
        public double[] Ions { get; set; }

        /// <summary>Each peak's row (an index into <see cref="RowBins"/>).</summary>
        public int[] Row { get; set; }

        /// <summary>Each peak's sweep (an index into <see cref="Cycles"/>).</summary>
        public int[] Cycle { get; set; }
    }

    /// <summary>
    /// One block of a stepped staggered acquisition (Orbitrap, Astral): its windows (rows), the
    /// narrow bins they tile (columns), each window's acquisitions in the block, the spectra
    /// whose output it owns, and their peaks.
    /// </summary>
    public sealed class InterpolatedUnit
    {
        /// <param name="transmission">Rows: windows. Columns: bins. Entry: the share of the bin the window isolates.</param>
        /// <param name="columnBins">Bin index of each column.</param>
        /// <param name="rowTimes">Each window's acquisition times in the block, increasing.</param>
        /// <param name="rowSpectra">The spectrum of each of those acquisitions.</param>
        /// <param name="outputRow">For each output spectrum, its window (row).</param>
        /// <param name="outputAcquisition">For each output spectrum, its index among its row's acquisitions.</param>
        /// <param name="outputTime">For each output spectrum, its acquisition time.</param>
        /// <param name="outputSpectrum">For each output spectrum, its index in the input.</param>
        /// <param name="firstCoreBin">First bin whose output the block owns.</param>
        /// <param name="lastCoreBin">Last bin whose output the block owns.</param>
        public InterpolatedUnit(double[,] transmission, int[] columnBins, double[][] rowTimes, int[][] rowSpectra,
            int[] outputRow, int[] outputAcquisition, double[] outputTime, int[] outputSpectrum, int firstCoreBin,
            int lastCoreBin)
        {
            Transmission = transmission;
            ColumnBins = columnBins;
            RowTimes = rowTimes;
            RowSpectra = rowSpectra;
            OutputRow = outputRow;
            OutputAcquisition = outputAcquisition;
            OutputTime = outputTime;
            OutputSpectrum = outputSpectrum;
            FirstCoreBin = firstCoreBin;
            LastCoreBin = lastCoreBin;
        }

        public double[,] Transmission { get; }
        public int[] ColumnBins { get; }
        public double[][] RowTimes { get; }
        public int[][] RowSpectra { get; }
        public int[] OutputRow { get; }
        public int[] OutputAcquisition { get; }
        public double[] OutputTime { get; }
        public int[] OutputSpectrum { get; }
        public int FirstCoreBin { get; }
        public int LastCoreBin { get; }

        /// <summary>Peak m/z values.</summary>
        public double[] Mz { get; set; }

        /// <summary>Peak intensities in ions.</summary>
        public double[] Ions { get; set; }

        /// <summary>Each peak's window (row).</summary>
        public int[] Row { get; set; }

        /// <summary>Each peak's acquisition (an index into its row's <see cref="RowTimes"/>).</summary>
        public int[] Acquisition { get; set; }
    }

    /// <summary>One output peak: an encoded bin, a sweep, an m/z and an intensity in ions.</summary>
    public struct ScanningPeak
    {
        public ScanningPeak(int bin, int cycle, double mz, double ions)
        {
            Bin = bin;
            Cycle = cycle;
            Mz = mz;
            Ions = ions;
        }

        public int Bin { get; }
        public int Cycle { get; }
        public double Mz { get; }
        public double Ions { get; }
    }

    /// <summary>What one block produced for the encoded bins and sweeps it owns.</summary>
    public sealed class ScanningUnitResult
    {
        /// <summary>Peaks of channels too weak to fit, or near no channel, as acquired.</summary>
        public List<ScanningPeak> PassedThrough { get; } = new List<ScanningPeak>();

        /// <summary>Demultiplexed channel intensities, by the source position they belong to.</summary>
        public List<ScanningPeak> Demultiplexed { get; } = new List<ScanningPeak>();

        public int Channels { get; set; }
        public int ChannelsSolved { get; set; }
        public double IonsIn { get; set; }
        public double IonsPassedThrough { get; set; }
    }

    /// <summary>
    /// Demultiplexes a scanning-quadrupole acquisition (SCIEX ZT Scan) one fragment m/z channel at
    /// a time: in each sweep, the channel's intensities over the encoded bins are y = A x, x &gt;= 0,
    /// with A the measured transmission of each source position into each bin's spectrum.
    /// </summary>
    /// <remarks>
    /// <para>This is the same equation as stepped staggered demultiplexing, one product-ion
    /// channel at a time. Only the rows of A differ: a sloped transmission about 11 Th wide at
    /// half height, stepped 1.18 Th, rather than sharp window bounds. A sweep's spectra are 2 ms
    /// apart, so no retention-time interpolation is needed.</para>
    /// <para>Per sweep, a channel gets one NNLS on the rows and columns its signal reaches, then
    /// a refit with Poisson row weights from that first fit. Placement into a single 1.18 Th bin is
    /// not reliable from one sweep's counts; the output layout (<see cref="ScanningLayout"/>)
    /// carries several neighboring positions per spectrum.</para>
    /// <para>Deterministic: channels, rows, columns and sweeps are processed in index order, and
    /// the solver breaks ties to the lowest index. A block's result depends only on its own
    /// input, so the result does not depend on the thread count.</para>
    /// </remarks>
    public static class ScanningDemultiplexer
    {
        /// <summary>
        /// Shares of an observed staggered peak below this are not written: they are round-off of
        /// the solve, not signal (as in <see cref="DemuxParams"/>'s share floor).
        /// </summary>
        public const double SHARE_FLOOR = 1e-6;

        /// <summary>
        /// The transmission matrix for a block: A[i, j] is the transmission of a precursor spread
        /// uniformly over source bin j into the spectrum of bin i, averaged over the source bin
        /// and scaled so a precursor at the center of its own bin averages 1.
        /// </summary>
        /// <param name="kernel">Transmission as a function of (bin center - precursor m/z).</param>
        /// <param name="rowCenters">Center m/z of each row's encoded bin.</param>
        /// <param name="columnCenters">Center m/z of each source bin.</param>
        /// <param name="binWidth">Width of a source bin, in Th.</param>
        public static double[,] TransmissionMatrix(ScanningKernel kernel, double[] rowCenters, double[] columnCenters,
            double binWidth)
        {
            const int points = 21;
            double center = 0;
            for (int p = 0; p < points; p++)
                center += kernel.Evaluate(Offset(p, points, binWidth));
            center /= points;
            if (center <= 0)
                throw new ArgumentException(@"The kernel does not transmit at zero offset.");
            var a = new double[rowCenters.Length, columnCenters.Length];
            for (int i = 0; i < rowCenters.Length; i++)
            {
                for (int j = 0; j < columnCenters.Length; j++)
                {
                    double sum = 0;
                    for (int p = 0; p < points; p++)
                        sum += kernel.Evaluate(rowCenters[i] - (columnCenters[j] + Offset(p, points, binWidth)));
                    a[i, j] = sum / points / center;
                }
            }
            return a;
        }

        /// <summary>Demultiplexes one block.</summary>
        public static ScanningUnitResult DemuxUnit(ScanningUnit unit, ScanningDemuxParams parameters)
        {
            var result = new ScanningUnitResult();
            int peaks = unit.Mz.Length;
            int rows = unit.RowBins.Length, cycles = unit.Cycles.Length, columns = unit.ColumnBins.Length;
            var coreRow = new bool[rows];
            for (int r = 0; r < rows; r++)
                coreRow[r] = unit.RowBins[r] >= unit.FirstCoreBin && unit.RowBins[r] <= unit.LastCoreBin;
            var coreCycle = new bool[cycles];
            for (int c = 0; c < cycles; c++)
                coreCycle[c] = unit.Cycles[c] >= unit.FirstCoreCycle && unit.Cycles[c] <= unit.LastCoreCycle;
            var coreColumn = new bool[columns];
            for (int j = 0; j < columns; j++)
                coreColumn[j] = unit.ColumnBins[j] >= unit.FirstCoreBin && unit.ColumnBins[j] <= unit.LastCoreBin;

            var channel = new int[peaks];
            int channels = FragmentChannelFinder.Find(unit.Mz, unit.Ions, parameters.ChannelTolerancePpm,
                parameters.MinChannelIons, channel);
            result.Channels = channels;

            // Peaks grouped by channel, in peak order within each; unassigned peaks first.
            var order = new long[peaks];
            for (int i = 0; i < peaks; i++)
                order[i] = (long)(channel[i] + 1) * peaks + i;
            Array.Sort(order); // Array.Sort OK: keys are unique ((channel + 1) * count + index)

            var y = new double[rows * cycles];
            var touched = new List<int>();
            var solver = new ChannelSolver(unit.Transmission, parameters);
            int k = 0;
            while (k < peaks)
            {
                int ch = (int)(order[k] / peaks) - 1;
                int end = k;
                while (end < peaks && (int)(order[end] / peaks) - 1 == ch)
                    end++;

                double ions = 0, weightedMz = 0;
                touched.Clear();
                for (int m = k; m < end; m++)
                {
                    int i = (int)(order[m] % peaks);
                    int cell = unit.Row[i] * cycles + unit.Cycle[i];
                    if (y[cell] == 0)
                        touched.Add(cell);
                    y[cell] += unit.Ions[i];
                    ions += unit.Ions[i];
                    weightedMz += unit.Ions[i] * unit.Mz[i];
                    if (coreRow[unit.Row[i]] && coreCycle[unit.Cycle[i]])
                        result.IonsIn += unit.Ions[i];
                }

                bool strong = ch >= 0 && ions >= parameters.MinChannelIons && touched.Count >= parameters.MinChannelCells;
                if (!strong)
                {
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        if (!coreRow[unit.Row[i]] || !coreCycle[unit.Cycle[i]])
                            continue;
                        result.PassedThrough.Add(new ScanningPeak(unit.RowBins[unit.Row[i]], unit.Cycles[unit.Cycle[i]],
                            unit.Mz[i], unit.Ions[i]));
                        result.IonsPassedThrough += unit.Ions[i];
                    }
                }
                else
                {
                    double mz = weightedMz / ions;
                    solver.Solve(y, cycles, coreCycle, (c, nc, x, cols) =>
                    {
                        for (int jj = 0; jj < nc; jj++)
                        {
                            int j = cols[jj];
                            if (coreColumn[j] && x[jj] >= parameters.MinOutputIons)
                                result.Demultiplexed.Add(new ScanningPeak(unit.ColumnBins[j], unit.Cycles[c], mz, x[jj]));
                        }
                    });
                    result.ChannelsSolved++;
                }
                foreach (int cell in touched)
                    y[cell] = 0;
                k = end;
            }
            return result;
        }

        /// <summary>
        /// Demultiplexes one block of a stepped staggered acquisition with the same channels and
        /// the same weighted per-channel solve. The windows are acquired at different times, so at
        /// each output spectrum's time every window's channel intensity is interpolated from that
        /// window's own acquisitions (the spectrum's own window is exact at its own time), and the
        /// spectrum keeps the bins of its window.
        /// </summary>
        /// <remarks>
        /// Demultiplexed peaks are labeled with their bin and the spectrum they belong to;
        /// pass-through peaks with bin -1 and their spectrum, for the caller to share among the
        /// spectrum's bins.
        /// </remarks>
        public static ScanningUnitResult DemuxInterpolatedUnit(InterpolatedUnit unit, ScanningDemuxParams parameters,
            RtInterpolation interpolation = RtInterpolation.makima)
        {
            var result = new ScanningUnitResult();
            int peaks = unit.Mz.Length;
            int rows = unit.RowTimes.Length, outputs = unit.OutputRow.Length, columns = unit.ColumnBins.Length;
            var a = unit.Transmission;

            // Which bins each output spectrum keeps (its window's, within the block's own bins), and
            // which spectra this block owns the pass-through peaks of: those whose window's first
            // bin is one of the block's own, so exactly one block passes each through.
            var keep = new bool[outputs * columns];
            var ownOutput = new Dictionary<int, bool>();
            for (int s = 0; s < outputs; s++)
            {
                int r = unit.OutputRow[s];
                int firstBin = int.MaxValue;
                for (int j = 0; j < columns; j++)
                {
                    if (a[r, j] < 0.5)
                        continue;
                    firstBin = Math.Min(firstBin, unit.ColumnBins[j]);
                    keep[s * columns + j] = unit.ColumnBins[j] >= unit.FirstCoreBin && unit.ColumnBins[j] <= unit.LastCoreBin;
                }
                ownOutput[unit.OutputSpectrum[s]] = firstBin >= unit.FirstCoreBin && firstBin <= unit.LastCoreBin;
            }

            var channel = new int[peaks];
            result.Channels = FragmentChannelFinder.Find(unit.Mz, unit.Ions, parameters.ChannelTolerancePpm,
                parameters.MinChannelIons, channel);
            var order = new long[peaks];
            for (int i = 0; i < peaks; i++)
                order[i] = (long)(channel[i] + 1) * peaks + i;
            Array.Sort(order); // Array.Sort OK: keys are unique ((channel + 1) * count + index)

            var series = new double[rows][];
            var outputOf = new int[rows][];  // the output spectrum of each acquisition, or -1
            for (int r = 0; r < rows; r++)
            {
                series[r] = new double[unit.RowTimes[r].Length];
                outputOf[r] = new int[unit.RowTimes[r].Length];
                for (int q = 0; q < outputOf[r].Length; q++)
                    outputOf[r][q] = -1;
            }
            for (int s = 0; s < outputs; s++)
                outputOf[unit.OutputRow[s]][unit.OutputAcquisition[s]] = s;
            var rowHasSignal = new bool[rows];
            var y = new double[rows * outputs];
            var observed = new List<int>[outputs];  // the channel's peaks in each output spectrum
            for (int s = 0; s < outputs; s++)
                observed[s] = new List<int>();
            var solveObserved = new bool[outputs];
            var solver = new ChannelSolver(a, parameters);
            int k = 0;
            while (k < peaks)
            {
                int ch = (int)(order[k] / peaks) - 1;
                int end = k;
                while (end < peaks && (int)(order[end] / peaks) - 1 == ch)
                    end++;

                double ions = 0, weightedMz = 0;
                int cells = 0;
                for (int m = k; m < end; m++)
                {
                    int i = (int)(order[m] % peaks);
                    var rowSeries = series[unit.Row[i]];
                    if (rowSeries[unit.Acquisition[i]] == 0)
                        cells++;
                    rowSeries[unit.Acquisition[i]] += unit.Ions[i];
                    rowHasSignal[unit.Row[i]] = true;
                    ions += unit.Ions[i];
                    weightedMz += unit.Ions[i] * unit.Mz[i];
                    if (OwnsPassThrough(unit, ownOutput, i))
                        result.IonsIn += unit.Ions[i];
                }

                bool strong = ch >= 0 && ions >= parameters.MinChannelIons && cells >= parameters.MinChannelCells;
                if (!strong)
                {
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        if (!OwnsPassThrough(unit, ownOutput, i))
                            continue;
                        result.PassedThrough.Add(new ScanningPeak(-1, unit.RowSpectra[unit.Row[i]][unit.Acquisition[i]],
                            unit.Mz[i], unit.Ions[i]));
                        result.IonsPassedThrough += unit.Ions[i];
                    }
                }
                else
                {
                    // Only spectra where the channel was observed are solved: their observed peaks
                    // are what gets apportioned.
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        int s = outputOf[unit.Row[i]][unit.Acquisition[i]];
                        if (s < 0)
                            continue;
                        observed[s].Add(i);
                        solveObserved[s] = true;
                    }
                    for (int r = 0; r < rows; r++)
                    {
                        if (!rowHasSignal[r])
                            continue;
                        var times = unit.RowTimes[r];
                        for (int s = 0; s < outputs; s++)
                        {
                            if (!solveObserved[s])
                                continue;
                            y[r * outputs + s] = Math.Max(0, RtInterpolator.Interpolate(interpolation, times, series[r],
                                times.Length, unit.OutputTime[s]));
                        }
                    }
                    solver.Solve(y, outputs, solveObserved, (s, nc, x, cols) =>
                        Apportion(unit, keep, observed[s], s, nc, x, cols, result.Demultiplexed));
                    result.ChannelsSolved++;
                    for (int r = 0; r < rows; r++)
                    {
                        if (rowHasSignal[r])
                            Array.Clear(y, r * outputs, outputs);
                    }
                    for (int s = 0; s < outputs; s++)
                    {
                        if (!solveObserved[s])
                            continue;
                        observed[s].Clear();
                        solveObserved[s] = false;
                    }
                }
                for (int r = 0; r < rows; r++)
                {
                    if (!rowHasSignal[r])
                        continue;
                    Array.Clear(series[r], 0, series[r].Length);
                    rowHasSignal[r] = false;
                }
                k = end;
            }
            return result;
        }

        /// <summary>
        /// Splits each observed peak of output spectrum <paramref name="s"/> among the bins of its
        /// window by the solution's shares, (transmission x solved intensity) over their sum, at the
        /// peak's own m/z, as pwiz's demultiplexer does. A spectrum's bins then sum to what was
        /// acquired, no peak is invented, and no m/z moves; a solution of zero over the window
        /// drops the peak.
        /// </summary>
        private static void Apportion(InterpolatedUnit unit, bool[] keep, List<int> observed, int s, int nc, double[] x,
            int[] cols, List<ScanningPeak> output)
        {
            int r = unit.OutputRow[s], columns = unit.ColumnBins.Length;
            double total = 0;
            for (int jj = 0; jj < nc; jj++)
                total += unit.Transmission[r, cols[jj]] * x[jj];
            if (total <= 0)
                return;
            foreach (int i in observed)
            {
                for (int jj = 0; jj < nc; jj++)
                {
                    int j = cols[jj];
                    if (!keep[s * columns + j] || x[jj] <= 0)
                        continue;
                    double share = unit.Transmission[r, j] * x[jj] / total;
                    if (share >= SHARE_FLOOR)
                        output.Add(new ScanningPeak(unit.ColumnBins[j], unit.OutputSpectrum[s], unit.Mz[i], unit.Ions[i] * share));
                }
            }
        }

        private static bool OwnsPassThrough(InterpolatedUnit unit, Dictionary<int, bool> ownOutput, int peak)
        {
            int spectrum = unit.RowSpectra[unit.Row[peak]][unit.Acquisition[peak]];
            return ownOutput.TryGetValue(spectrum, out bool own) && own;
        }

        private static double Offset(int point, int points, double binWidth)
        {
            return ((point + 0.5) / points - 0.5) * binWidth;
        }

        /// <summary>
        /// Solves one channel sweep by sweep, reusing its buffers from channel to channel within a
        /// block (one per block, so not shared across threads).
        /// </summary>
        private sealed class ChannelSolver
        {
            private readonly double[,] _a;
            private readonly int _rows;
            private readonly int _columns;
            private readonly ScanningDemuxParams _parameters;
            private readonly int[] _rowIndex;
            private readonly int[] _columnIndex;
            private readonly double[] _gram;
            private readonly double[] _weighted;
            private readonly double[] _atb;
            private readonly double[] _x;
            private readonly double[] _mu;
            private readonly double[] _start;
            private readonly bool[] _rowSignal;
            private readonly int[] _rowStart;      // used row rr's entries are [_rowStart[rr], _rowStart[rr + 1])
            private readonly int[] _nzColumn;      // an entry's column, as an index into the channel's columns
            private readonly double[] _nzValue;
            private readonly NnlsSolver.Workspace _workspace;

            public ChannelSolver(double[,] a, ScanningDemuxParams parameters)
            {
                _a = a;
                _rows = a.GetLength(0);
                _columns = a.GetLength(1);
                _parameters = parameters;
                _rowIndex = new int[_rows];
                _columnIndex = new int[_columns];
                _gram = new double[_columns * _columns];
                _weighted = new double[_columns * _columns];
                _atb = new double[_columns];
                _x = new double[_columns];
                _mu = new double[_rows];
                _start = new double[_columns];
                _rowSignal = new bool[_rows];
                _rowStart = new int[_rows + 1];
                _nzColumn = new int[_rows * _columns];
                _nzValue = new double[_rows * _columns];
                _workspace = new NnlsSolver.Workspace(_columns);
            }

            /// <summary>
            /// Solves the channel whose counts are <paramref name="y"/> (rows x times, row-major)
            /// at each time <paramref name="solveTime"/> selects, and hands each solution to
            /// <paramref name="solved"/> as (time, column count, solution, the solution's columns).
            /// </summary>
            public void Solve(double[] y, int cycles, bool[] solveTime, Action<int, int, double[], int[]> solved)
            {
                // The columns any of the channel's signal-bearing rows could come from, and the
                // rows that see any of those columns (their zeros are information too).
                for (int r = 0; r < _rows; r++)
                    _rowSignal[r] = RowHasSignal(y, r, cycles);
                int nc = 0;
                for (int j = 0; j < _columns; j++)
                {
                    for (int r = 0; r < _rows; r++)
                    {
                        if (_rowSignal[r] && _a[r, j] > 0)
                        {
                            _columnIndex[nc++] = j;
                            break;
                        }
                    }
                }
                // Each used row's nonzero entries over those columns, compactly: a row sees only the
                // positions within the transmission's reach, about half of the columns.
                int nr = 0, nnz = 0;
                for (int r = 0; r < _rows; r++)
                {
                    int start = nnz;
                    for (int jj = 0; jj < nc; jj++)
                    {
                        double value = _a[r, _columnIndex[jj]];
                        if (value <= 0)
                            continue;
                        _nzColumn[nnz] = jj;
                        _nzValue[nnz] = value;
                        nnz++;
                    }
                    if (nnz == start)
                        continue;
                    _rowIndex[nr] = r;
                    _rowStart[nr] = start;
                    nr++;
                    _rowStart[nr] = nnz;
                }

                // A^T A over those rows and columns: the same for every sweep of the channel.
                Array.Clear(_gram, 0, nc * nc);
                for (int rr = 0; rr < nr; rr++)
                    AddOuterProduct(_gram, nc, rr, 1);

                for (int c = 0; c < cycles; c++)
                {
                    if (!solveTime[c] || !CycleHasSignal(y, c, cycles))
                        continue;
                    ComputeAtb(y, c, cycles, nr, nc, null);
                    NnlsSolver.SolveNormal(_gram, _atb, nc, _x, _workspace);
                    if (_parameters.PoissonWeights)
                        SolveWeighted(y, c, cycles, nr, nc);
                    solved(c, nc, _x, _columnIndex);
                }
            }

            /// <summary>
            /// The Poisson-weighted refit. Rows whose expected count from the unweighted fit is at
            /// or below the floor all share the weight 1 / floor, so the weighted A^T W A is that
            /// weight times A^T A, corrected only for the few rows above the floor.
            /// </summary>
            private void SolveWeighted(double[] y, int c, int cycles, int nr, int nc)
            {
                double floor = _parameters.WeightFloorIons;
                double baseWeight = 1 / floor;
                for (int i = 0; i < nc * nc; i++)
                    _weighted[i] = baseWeight * _gram[i];
                for (int rr = 0; rr < nr; rr++)
                {
                    double mu = 0;
                    for (int e = _rowStart[rr]; e < _rowStart[rr + 1]; e++)
                        mu += _nzValue[e] * _x[_nzColumn[e]];
                    _mu[rr] = mu;
                    if (mu > floor)
                        AddOuterProduct(_weighted, nc, rr, 1 / mu - baseWeight);
                }
                ComputeAtb(y, c, cycles, nr, nc, _mu);
                // The weighted solution usually has the unweighted one's support: start there.
                Array.Copy(_x, _start, nc);
                NnlsSolver.SolveNormal(_weighted, _atb, nc, _x, _workspace, 0, _start);
            }

            /// <summary>A^T y for one sweep, or A^T W y with weights 1 / max(mu, floor) when mu is given.</summary>
            private void ComputeAtb(double[] y, int c, int cycles, int nr, int nc, double[] mu)
            {
                Array.Clear(_atb, 0, nc);
                for (int rr = 0; rr < nr; rr++)
                {
                    double value = y[_rowIndex[rr] * cycles + c];
                    if (value == 0)
                        continue;
                    if (mu != null)
                        value /= Math.Max(mu[rr], _parameters.WeightFloorIons);
                    for (int e = _rowStart[rr]; e < _rowStart[rr + 1]; e++)
                        _atb[_nzColumn[e]] += _nzValue[e] * value;
                }
            }

            /// <summary>Adds weight times the outer product of used row rr with itself to an nc x nc matrix.</summary>
            private void AddOuterProduct(double[] matrix, int nc, int rr, double weight)
            {
                int start = _rowStart[rr], end = _rowStart[rr + 1];
                for (int e = start; e < end; e++)
                {
                    double scaled = weight * _nzValue[e];
                    int offset = _nzColumn[e] * nc;
                    for (int f = start; f < end; f++)
                        matrix[offset + _nzColumn[f]] += scaled * _nzValue[f];
                }
            }

            private static bool RowHasSignal(double[] y, int r, int cycles)
            {
                for (int c = 0; c < cycles; c++)
                {
                    if (y[r * cycles + c] > 0)
                        return true;
                }
                return false;
            }

            private bool CycleHasSignal(double[] y, int c, int cycles)
            {
                for (int r = 0; r < _rows; r++)
                {
                    if (y[r * cycles + c] > 0)
                        return true;
                }
                return false;
            }
        }
    }
}
