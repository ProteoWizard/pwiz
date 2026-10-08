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

        /// <summary>
        /// Scanning data: -1 writes the solved intensities by source position. Zero or more
        /// apportions instead: each observed peak of an encoded bin's spectrum is scaled by the
        /// share of its signal the solution places within this many positions of that bin, at its
        /// own m/z, and written to that bin. Nothing is invented and no m/z moves.
        /// </summary>
        public int ApportionHalfWidth { get; set; } = -1;

        /// <summary>
        /// Scanning data: write each solved intensity at the m/z of the observed peaks it was
        /// solved from, in its own sweep. Each row's peaks count by the share of the row's modeled
        /// signal the position explains. False writes every value of a channel at the channel's
        /// mean m/z over the whole block.
        /// </summary>
        public bool PositionMz { get; set; }

        /// <summary>
        /// Scanning data: a non-negative L1 (lasso) weight on every per-sweep solve, in the weighted
        /// fit's units (0: off). With x &gt;= 0 the penalty is linear, so each solve is NNLS on
        /// A^T y - SweepL1 / 2: a position stays at zero unless it lowers the fit's residual faster
        /// than SweepL1 per ion, and the positions kept are shrunk by it. Not used with
        /// <see cref="SourcePositions"/>, whose source-finding fit has <see cref="SourceL1"/>.
        /// </summary>
        public double SweepL1 { get; set; }

        /// <summary>
        /// Scanning data: a lasso on every per-sweep weighted solve whose weight differs per position:
        /// SweepL1Z standard deviations of the position's score under Poisson noise, sqrt((A^T W A)_jj)
        /// (0: off). A position then stays at zero unless the evidence for it, given the others,
        /// exceeds that many standard deviations, wherever the background is. A fixed
        /// <see cref="SweepL1"/> is instead a threshold that tightens as the background grows. Needs
        /// <see cref="PoissonWeights"/>; the unweighted fit that sets the weights is not penalized.
        /// </summary>
        public double SweepL1Z { get; set; }

        /// <summary>
        /// With <see cref="SweepL1"/> or <see cref="SweepL1Z"/>: refit each sweep without the penalty
        /// over the positions the lasso kept (a relaxed lasso), so the lasso chooses the support and
        /// the quantities are not shrunk.
        /// </summary>
        public bool SweepL1Refit { get; set; }

        /// <summary>
        /// Scanning data: choose each channel's positions once per block instead of per sweep (0: off).
        /// The channel's counts summed over the block's sweeps are fitted with the z-scaled lasso of
        /// <see cref="SweepL1Z"/> at this many standard deviations; each sweep is then solved without
        /// a penalty over only the positions that fit kept. Per-sweep selection lets a weak position
        /// pass in one sweep and fail in the next, which jitters the fragment's chromatogram; one
        /// selection per block does not, and it is made on the block's summed evidence.
        /// </summary>
        public double BlockSupportZ { get; set; }

        /// <summary>
        /// Scanning data: place each channel's sources once from the whole block instead of per
        /// sweep on fixed positions. The channel's profile summed over the block's sweeps is fitted
        /// on the bin columns; runs of adjacent solved columns are sources, merged when closer than
        /// <see cref="SourceMergeTh"/> and dropped when small; each source's position is refined
        /// against the kernel at exact positions; then each sweep is solved over just those sources
        /// and each source is written to the bin nearest its position. Needs the unit's centers and
        /// kernel (<see cref="ScanningUnit.Kernel"/>).
        /// </summary>
        public bool SourcePositions { get; set; }

        /// <summary>Sources of one channel closer than this (Th) are one source.</summary>
        public double SourceMergeTh { get; set; } = 0.8;

        /// <summary>Sources smaller than this many ions over the block are dropped.</summary>
        public double MinSourceIons { get; set; } = 2.0;

        /// <summary>Sources smaller than this fraction of the channel's solved total are dropped.</summary>
        public double MinSourceFraction { get; set; } = 0.05;

        /// <summary>
        /// A non-negative L1 (lasso) weight for the summed-profile fit that finds the sources, in the
        /// weighted fit's units (0: off). With x &gt;= 0 the penalty is linear, so the fit is NNLS on
        /// A^T y - L1 / 2. The per-sweep quantities are refitted without it.
        /// </summary>
        public double SourceL1 { get; set; }

        /// <summary>How far (Th) each source's position is searched either side of its start.</summary>
        public double RefineHalfWidthTh { get; set; } = 0.6;

        /// <summary>The position search's step, Th.</summary>
        public double RefineStepTh { get; set; } = 0.1;

        /// <summary>Most sources kept per channel; the largest are kept.</summary>
        public int MaxSources { get; set; } = 12;
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

        /// <summary>Center m/z of each row's encoded bin; needed by <see cref="ScanningDemuxParams.SourcePositions"/>.</summary>
        public double[] RowCenters { get; set; }

        /// <summary>Center m/z of each column's source bin; needed by <see cref="ScanningDemuxParams.SourcePositions"/>.</summary>
        public double[] ColumnCenters { get; set; }

        /// <summary>The kernel the transmission was built from; needed by <see cref="ScanningDemuxParams.SourcePositions"/>.</summary>
        public ScanningKernel Kernel { get; set; }

        /// <summary>The kernel's scale in <see cref="Transmission"/> (see <see cref="ScanningDemultiplexer.KernelScale"/>).</summary>
        public double KernelScale { get; set; } = 1;
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

        /// <summary>Sources placed, with <see cref="ScanningDemuxParams.SourcePositions"/>.</summary>
        public int Sources { get; set; }
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

        /// <summary>The one time of a solve over counts summed across a block's sweeps.</summary>
        private static readonly bool[] ONE_TIME = { true };

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
            double center = KernelScale(kernel, binWidth);
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

        /// <summary>
        /// The kernel's average over a source bin centered on its encoded bin: the scale that makes
        /// <see cref="TransmissionMatrix"/> 1 for a precursor at its own bin's center.
        /// </summary>
        public static double KernelScale(ScanningKernel kernel, double binWidth)
        {
            const int points = 21;
            double center = 0;
            for (int p = 0; p < points; p++)
                center += kernel.Evaluate(Offset(p, points, binWidth));
            center /= points;
            if (center <= 0)
                throw new ArgumentException(@"The kernel does not transmit at zero offset.");
            return center;
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
            var mzSum = new double[rows * cycles];  // ions x m/z of the channel's peaks in each cell
            var touched = new List<int>();
            var observedCells = new Dictionary<int, List<int>>();
            var solver = new ChannelSolver(unit.Transmission, parameters);
            var positionMz = parameters.PositionMz ? new double[columns] : null;
            var mzNumerator = parameters.PositionMz ? new double[columns] : null;
            var mzDenominator = parameters.PositionMz ? new double[columns] : null;
            var sourceFitter = parameters.SourcePositions ? new SourceFitter(unit, parameters, solver) : null;
            if (sourceFitter == null)
            {
                // Source positions set the solver's penalty themselves, for their summed fit only.
                solver.L1 = parameters.SweepL1;
                solver.L1Z = parameters.SweepL1Z;
                solver.RefitSupport = (parameters.SweepL1 > 0 || parameters.SweepL1Z > 0) && parameters.SweepL1Refit;
            }
            var blockTotal = parameters.BlockSupportZ > 0 && sourceFitter == null ? new double[rows] : null;
            var blockSupport = blockTotal != null ? new bool[columns] : null;
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
                    mzSum[cell] += unit.Ions[i] * unit.Mz[i];
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
                else if (parameters.ApportionHalfWidth >= 0)
                {
                    // The channel's observed peaks in the block's own bins and sweeps, by cell.
                    observedCells.Clear();
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        if (!coreRow[unit.Row[i]] || !coreCycle[unit.Cycle[i]])
                            continue;
                        int cell = unit.Row[i] * cycles + unit.Cycle[i];
                        if (!observedCells.TryGetValue(cell, out var list))
                        {
                            list = new List<int>();
                            observedCells[cell] = list;
                        }
                        list.Add(i);
                    }
                    solver.Solve(y, cycles, coreCycle, (c, nc, x, cols) =>
                        ApportionScanning(unit, observedCells, cycles, c, nc, x, cols, parameters.ApportionHalfWidth,
                            result.Demultiplexed));
                    result.ChannelsSolved++;
                }
                else if (sourceFitter != null)
                {
                    result.Sources += sourceFitter.Solve(y, mzSum, cycles, coreCycle, coreColumn, weightedMz / ions,
                        result.Demultiplexed);
                    result.ChannelsSolved++;
                }
                else
                {
                    double mz = weightedMz / ions;
                    if (blockTotal != null)
                        SelectBlockSupport(solver, y, cycles, blockTotal, blockSupport, parameters.BlockSupportZ);
                    solver.Solve(y, cycles, coreCycle, (c, nc, x, cols) =>
                    {
                        if (positionMz != null)
                        {
                            AttributedMz(unit.Transmission, y, mzSum, cycles, c, nc, x, cols, mz, mzNumerator,
                                mzDenominator, positionMz);
                        }
                        for (int jj = 0; jj < nc; jj++)
                        {
                            int j = cols[jj];
                            if (coreColumn[j] && x[jj] >= parameters.MinOutputIons)
                            {
                                result.Demultiplexed.Add(new ScanningPeak(unit.ColumnBins[j], unit.Cycles[c],
                                    positionMz != null ? positionMz[jj] : mz, x[jj]));
                            }
                        }
                    });
                    solver.ColumnMask = null;
                    result.ChannelsSolved++;
                }
                foreach (int cell in touched)
                {
                    y[cell] = 0;
                    mzSum[cell] = 0;
                }
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
        /// The positions a channel may use in every sweep of its block: those the z-scaled lasso keeps
        /// in a fit of the channel's counts summed over the block's sweeps. Leaves them as the
        /// solver's column mask for the per-sweep solves, which carry no penalty.
        /// </summary>
        private static void SelectBlockSupport(ChannelSolver solver, double[] y, int cycles, double[] total,
            bool[] support, double z)
        {
            for (int r = 0; r < total.Length; r++)
            {
                double sum = 0;
                for (int c = 0; c < cycles; c++)
                    sum += y[r * cycles + c];
                total[r] = sum;
            }
            Array.Clear(support, 0, support.Length);
            solver.ColumnMask = null;
            double l1 = solver.L1, l1Z = solver.L1Z;
            bool refit = solver.RefitSupport;
            solver.L1 = 0;
            solver.L1Z = z;
            solver.RefitSupport = false;
            solver.Solve(total, 1, ONE_TIME, (c, nc, x, cols) =>
            {
                for (int jj = 0; jj < nc; jj++)
                {
                    if (x[jj] > 0)
                        support[cols[jj]] = true;
                }
            });
            solver.L1 = l1;
            solver.L1Z = l1Z;
            solver.RefitSupport = refit;
            solver.ColumnMask = support;
        }

        /// <summary>
        /// Scales each observed peak of sweep <paramref name="c"/> in each of the block's own rows by
        /// the share of that row's modeled signal, (transmission x solved intensity), that comes
        /// from source positions within <paramref name="halfWidth"/> of the row's own bin, and
        /// writes it to that bin at the peak's own m/z. A solution of zero in the row drops the peak.
        /// </summary>
        private static void ApportionScanning(ScanningUnit unit, Dictionary<int, List<int>> observedCells, int cycles,
            int c, int nc, double[] x, int[] cols, int halfWidth, List<ScanningPeak> output)
        {
            var a = unit.Transmission;
            for (int r = 0; r < unit.RowBins.Length; r++)
            {
                if (!observedCells.TryGetValue(r * cycles + c, out var observed))
                    continue;
                double total = 0, near = 0;
                for (int jj = 0; jj < nc; jj++)
                {
                    double part = a[r, cols[jj]] * x[jj];
                    total += part;
                    if (Math.Abs(unit.ColumnBins[cols[jj]] - unit.RowBins[r]) <= halfWidth)
                        near += part;
                }
                if (total <= 0 || near <= 0)
                    continue;
                double share = near / total;
                if (share < SHARE_FLOOR)
                    continue;
                foreach (int i in observed)
                    output.Add(new ScanningPeak(unit.RowBins[r], unit.Cycles[c], unit.Mz[i], unit.Ions[i] * share));
            }
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

        /// <summary>
        /// The m/z of each solved position in sweep <paramref name="c"/>: the intensity-weighted m/z
        /// of the channel's observed peaks in each row, counted by the share of that row's modeled
        /// signal, (transmission x solved intensity) over the row's sum, the position explains. A
        /// position no row attributes signal to keeps the channel's block mean.
        /// </summary>
        private static void AttributedMz(double[,] a, double[] y, double[] mzSum, int cycles, int c, int nc, double[] x,
            int[] cols, double blockMz, double[] numerator, double[] denominator, double[] result)
        {
            Array.Clear(numerator, 0, nc);
            Array.Clear(denominator, 0, nc);
            int rows = a.GetLength(0);
            for (int r = 0; r < rows; r++)
            {
                int cell = r * cycles + c;
                if (y[cell] <= 0)
                    continue;
                double modeled = 0;
                for (int jj = 0; jj < nc; jj++)
                    modeled += a[r, cols[jj]] * x[jj];
                if (modeled <= 0)
                    continue;
                for (int jj = 0; jj < nc; jj++)
                {
                    double share = a[r, cols[jj]] * x[jj] / modeled;
                    if (share <= 0)
                        continue;
                    numerator[jj] += share * mzSum[cell];
                    denominator[jj] += share * y[cell];
                }
            }
            for (int jj = 0; jj < nc; jj++)
                result[jj] = denominator[jj] > 0 ? numerator[jj] / denominator[jj] : blockMz;
        }

        private static double Offset(int point, int points, double binWidth)
        {
            return ((point + 0.5) / points - 0.5) * binWidth;
        }

        /// <summary>
        /// Places a channel's sources once per block (<see cref="ScanningDemuxParams.SourcePositions"/>):
        /// the summed profile's bin-level fit gives the sources, a 1-D search refines each position
        /// against the kernel at exact positions, and each sweep is solved over just those sources.
        /// One per block, reusing its buffers from channel to channel.
        /// </summary>
        private sealed class SourceFitter
        {
            private readonly ScanningUnit _unit;
            private readonly ScanningDemuxParams _parameters;
            private readonly ChannelSolver _solver;
            private readonly double[] _total;
            private readonly bool[] _one = { true };
            private readonly double[] _binX;
            private readonly int[] _binCols;
            private int _binCount;
            private readonly List<(double Position, double Amount)> _sources = new List<(double, double)>();
            private readonly double[] _position;
            private readonly int[] _column;       // the column nearest each source
            private readonly double[] _s;         // rows x sources, the kernel at the sources' positions
            private readonly double[] _weight;    // per row, from the summed fit
            private readonly double[] _gram;
            private readonly double[] _rhs;
            private readonly double[] _amount;
            private readonly double[] _start;
            private readonly double[] _counts;    // one sweep's counts, by row
            private readonly NnlsSolver.Workspace _workspace;

            public SourceFitter(ScanningUnit unit, ScanningDemuxParams parameters, ChannelSolver solver)
            {
                if (unit.Kernel == null || unit.RowCenters == null || unit.ColumnCenters == null)
                    throw new ArgumentException(@"Source positions need the unit's kernel and bin centers.");
                _unit = unit;
                _parameters = parameters;
                _solver = solver;
                int rows = unit.RowBins.Length, columns = unit.ColumnBins.Length, k = parameters.MaxSources;
                _total = new double[rows];
                _binX = new double[columns];
                _binCols = new int[columns];
                _position = new double[k];
                _column = new int[k];
                _s = new double[rows * k];
                _weight = new double[rows];
                _gram = new double[k * k];
                _rhs = new double[k];
                _amount = new double[k];
                _start = new double[k];
                _counts = new double[rows];
                _workspace = new NnlsSolver.Workspace(k);
            }

            /// <summary>
            /// Solves one channel and adds its peaks for the block's own bins and sweeps to
            /// <paramref name="output"/>; returns the number of sources placed.
            /// </summary>
            public int Solve(double[] y, double[] mzSum, int cycles, bool[] coreCycle, bool[] coreColumn, double blockMz,
                List<ScanningPeak> output)
            {
                int rows = _unit.RowBins.Length;
                for (int r = 0; r < rows; r++)
                {
                    double sum = 0;
                    for (int c = 0; c < cycles; c++)
                        sum += y[r * cycles + c];
                    _total[r] = sum;
                }
                _binCount = 0;
                _solver.L1 = _parameters.SourceL1;
                _solver.Solve(_total, 1, _one, (c, nc, x, cols) =>
                {
                    Array.Copy(x, _binX, nc);
                    Array.Copy(cols, _binCols, nc);
                    _binCount = nc;
                });
                _solver.L1 = 0;
                int k = FindSources();
                if (k == 0)
                    return 0;

                // Weights for the position search, from the bin-level fit: 1 / max(mu, floor).
                var a = _unit.Transmission;
                for (int r = 0; r < rows; r++)
                {
                    double mu = 0;
                    for (int jj = 0; jj < _binCount; jj++)
                        mu += a[r, _binCols[jj]] * _binX[jj];
                    _weight[r] = 1 / Math.Max(mu, _parameters.WeightFloorIons);
                }
                Refine(k);

                // The column nearest each source; only the block's own columns are written.
                var centers = _unit.ColumnCenters;
                for (int s = 0; s < k; s++)
                {
                    int best = 0;
                    for (int j = 1; j < centers.Length; j++)
                    {
                        if (Math.Abs(centers[j] - _position[s]) < Math.Abs(centers[best] - _position[s]))
                            best = j;
                    }
                    _column[s] = best;
                }
                FillColumns(k);
                for (int c = 0; c < cycles; c++)
                {
                    if (!coreCycle[c])
                        continue;
                    bool signal = false;
                    for (int r = 0; r < rows && !signal; r++)
                        signal = y[r * cycles + c] > 0;
                    if (!signal)
                        continue;
                    SolveSweep(y, c, cycles, k);
                    for (int s = 0; s < k; s++)
                    {
                        if (!coreColumn[_column[s]] || _amount[s] < _parameters.MinOutputIons)
                            continue;
                        double mz = _parameters.PositionMz ? SourceMz(y, mzSum, c, cycles, k, s, blockMz) : blockMz;
                        output.Add(new ScanningPeak(_unit.ColumnBins[_column[s]], _unit.Cycles[c], mz, _amount[s]));
                    }
                }
                return k;
            }

            /// <summary>
            /// Sources from the bin-level fit: each run of adjacent solved columns, at its
            /// intensity-weighted center; merged when closer than the merge distance; small ones
            /// dropped; at most MaxSources kept, the largest. Positions ascending in _position.
            /// </summary>
            private int FindSources()
            {
                _sources.Clear();
                int jj = 0;
                while (jj < _binCount)
                {
                    if (_binX[jj] <= 0)
                    {
                        jj++;
                        continue;
                    }
                    double sum = 0, weighted = 0;
                    int start = jj;
                    while (jj < _binCount && _binX[jj] > 0 && (jj == start || _binCols[jj] == _binCols[jj - 1] + 1))
                    {
                        sum += _binX[jj];
                        weighted += _binX[jj] * _unit.ColumnCenters[_binCols[jj]];
                        jj++;
                    }
                    var run = (Position: weighted / sum, Amount: sum);
                    int last = _sources.Count - 1;
                    if (last >= 0 && run.Position - _sources[last].Position <= _parameters.SourceMergeTh)
                    {
                        double amount = _sources[last].Amount + run.Amount;
                        _sources[last] = ((_sources[last].Position * _sources[last].Amount + run.Position * run.Amount) / amount,
                            amount);
                    }
                    else
                    {
                        _sources.Add(run);
                    }
                }
                double total = 0;
                foreach (var source in _sources)
                    total += source.Amount;
                double floor = Math.Max(_parameters.MinSourceIons, _parameters.MinSourceFraction * total);
                _sources.RemoveAll(source => source.Amount < floor);
                if (_sources.Count > _parameters.MaxSources)
                {
                    // The largest, ties to the lower position; then back in position order.
                    var kept = new List<(double Position, double Amount)>(_sources);
                    kept.Sort((p, q) => p.Amount != q.Amount ? q.Amount.CompareTo(p.Amount) : p.Position.CompareTo(q.Position)); // Array.Sort OK: ties broken by position
                    kept.RemoveRange(_parameters.MaxSources, kept.Count - _parameters.MaxSources);
                    kept.Sort((p, q) => p.Position.CompareTo(q.Position)); // Array.Sort OK: positions of one channel's sources are distinct
                    _sources.Clear();
                    _sources.AddRange(kept);
                }
                for (int s = 0; s < _sources.Count; s++)
                    _position[s] = _sources[s].Position;
                return _sources.Count;
            }

            /// <summary>
            /// Two passes of a 1-D search per source over +/- RefineHalfWidthTh in RefineStepTh
            /// steps: the position whose weighted fit of the summed profile, over all sources, has
            /// the smallest residual. Only a strictly smaller residual moves a source.
            /// </summary>
            private void Refine(int k)
            {
                double step = _parameters.RefineStepTh, half = _parameters.RefineHalfWidthTh;
                if (step <= 0 || half <= 0)
                    return;
                int steps = (int)Math.Round(half / step);
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int s = 0; s < k; s++)
                    {
                        double start = _position[s], best = start;
                        double bestResidual = double.MaxValue;
                        for (int d = -steps; d <= steps; d++)
                        {
                            _position[s] = start + d * step;
                            FillColumns(k);
                            double residual = FitTotal(k);
                            if (residual < bestResidual)
                            {
                                bestResidual = residual;
                                best = _position[s];
                            }
                        }
                        _position[s] = best;
                    }
                }
            }

            /// <summary>The kernel at each source's exact position, for every row, in the transmission's scale.</summary>
            private void FillColumns(int k)
            {
                int rows = _unit.RowBins.Length;
                var kernel = _unit.Kernel;
                for (int r = 0; r < rows; r++)
                {
                    for (int s = 0; s < k; s++)
                        _s[r * k + s] = kernel.Evaluate(_unit.RowCenters[r] - _position[s]) / _unit.KernelScale;
                }
            }

            /// <summary>The weighted NNLS of the summed profile over the sources; returns its weighted residual.</summary>
            private double FitTotal(int k)
            {
                int rows = _unit.RowBins.Length;
                Normal(k, _total, _weight);
                NnlsSolver.SolveNormal(_gram, _rhs, k, _amount, _workspace);
                double residual = 0;
                for (int r = 0; r < rows; r++)
                {
                    double model = 0;
                    for (int s = 0; s < k; s++)
                        model += _s[r * k + s] * _amount[s];
                    double d = _total[r] - model;
                    residual += _weight[r] * d * d;
                }
                return residual;
            }

            /// <summary>One sweep over the sources: NNLS, then the Poisson-weighted refit, into _amount.</summary>
            private void SolveSweep(double[] y, int c, int cycles, int k)
            {
                int rows = _unit.RowBins.Length;
                for (int r = 0; r < rows; r++)
                    _counts[r] = y[r * cycles + c];
                Normal(k, _counts, null);
                NnlsSolver.SolveNormal(_gram, _rhs, k, _amount, _workspace);
                if (!_parameters.PoissonWeights)
                    return;
                for (int r = 0; r < rows; r++)
                {
                    double model = 0;
                    for (int s = 0; s < k; s++)
                        model += _s[r * k + s] * _amount[s];
                    _weight[r] = 1 / Math.Max(model, _parameters.WeightFloorIons);
                }
                Normal(k, _counts, _weight);
                Array.Copy(_amount, _start, k);
                NnlsSolver.SolveNormal(_gram, _rhs, k, _amount, _workspace, 0, _start);
            }

            /// <summary>S^T W S and S^T W b over the rows, W the given weights or 1.</summary>
            private void Normal(int k, double[] b, double[] weight)
            {
                int rows = _unit.RowBins.Length;
                Array.Clear(_gram, 0, k * k);
                Array.Clear(_rhs, 0, k);
                for (int r = 0; r < rows; r++)
                {
                    double w = weight == null ? 1 : weight[r];
                    for (int s = 0; s < k; s++)
                    {
                        double ws = w * _s[r * k + s];
                        if (ws == 0)
                            continue;
                        _rhs[s] += ws * b[r];
                        for (int t = 0; t < k; t++)
                            _gram[s * k + t] += ws * _s[r * k + t];
                    }
                }
            }

            /// <summary>
            /// The m/z of source s in sweep c: the channel's observed peaks in each row, counted by
            /// the share of the row's modeled signal the source explains.
            /// </summary>
            private double SourceMz(double[] y, double[] mzSum, int c, int cycles, int k, int s, double blockMz)
            {
                int rows = _unit.RowBins.Length;
                double numerator = 0, denominator = 0;
                for (int r = 0; r < rows; r++)
                {
                    int cell = r * cycles + c;
                    if (y[cell] <= 0)
                        continue;
                    double model = 0;
                    for (int t = 0; t < k; t++)
                        model += _s[r * k + t] * _amount[t];
                    if (model <= 0)
                        continue;
                    double share = _s[r * k + s] * _amount[s] / model;
                    numerator += share * mzSum[cell];
                    denominator += share * y[cell];
                }
                return denominator > 0 ? numerator / denominator : blockMz;
            }
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
            private readonly int[] _support;       // the refit's columns, as indices into the channel's columns
            private readonly double[] _supportGram;
            private readonly double[] _supportAtb;
            private readonly double[] _supportX;
            private readonly double[] _supportStart;

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
                _support = new int[_columns];
                _supportGram = new double[_columns * _columns];
                _supportAtb = new double[_columns];
                _supportX = new double[_columns];
                _supportStart = new double[_columns];
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
                    if (ColumnMask != null && !ColumnMask[j])
                        continue;
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
                    Penalize(nc);
                    NnlsSolver.SolveNormal(_gram, _atb, nc, _x, _workspace);
                    if (_parameters.PoissonWeights)
                        SolveWeighted(y, c, cycles, nr, nc);
                    if (RefitSupport)
                        Refit(y, c, cycles, nr, nc);
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
                Penalize(nc);
                PenalizeZ(nc);
                // The weighted solution usually has the unweighted one's support: start there.
                Array.Copy(_x, _start, nc);
                NnlsSolver.SolveNormal(_weighted, _atb, nc, _x, _workspace, 0, _start);
            }

            /// <summary>
            /// A non-negative L1 (lasso) weight for the next solves (0: none). With x &gt;= 0 the
            /// penalty is linear, so it enters the normal equations as A^T y - L1 / 2.
            /// </summary>
            public double L1 { get; set; }

            /// <summary>
            /// A lasso on the weighted solve only, per column: this many standard deviations of the
            /// column's score under Poisson noise, sqrt((A^T W A)_jj) (0: none).
            /// </summary>
            public double L1Z { get; set; }

            /// <summary>
            /// Refit each penalized solve without the penalty over the columns it kept, with the
            /// same row weights (a relaxed lasso).
            /// </summary>
            public bool RefitSupport { get; set; }

            /// <summary>The columns the next solves may use (null: every column).</summary>
            public bool[] ColumnMask { get; set; }

            private void Penalize(int nc)
            {
                if (L1 <= 0)
                    return;
                for (int jj = 0; jj < nc; jj++)
                    _atb[jj] -= 0.5 * L1;
            }

            /// <summary>
            /// The weighted normal equations become A^T W A x = A^T W y - L1Z sqrt((A^T W A)_jj): a zero
            /// column stays zero while its score, A^T W (y - A x), is within L1Z standard deviations.
            /// </summary>
            private void PenalizeZ(int nc)
            {
                if (L1Z <= 0)
                    return;
                for (int jj = 0; jj < nc; jj++)
                    _atb[jj] -= L1Z * Math.Sqrt(_weighted[jj * nc + jj]);
            }

            /// <summary>
            /// The relaxed lasso's second step: the last solve's positive columns refitted without the
            /// penalty, with the weights of <see cref="SolveWeighted"/> (none when Poisson weights are
            /// off), starting from the penalized solution.
            /// </summary>
            private void Refit(double[] y, int c, int cycles, int nr, int nc)
            {
                int ns = 0;
                for (int jj = 0; jj < nc; jj++)
                {
                    if (_x[jj] > 0)
                        _support[ns++] = jj;
                }
                if (ns == 0)
                    return;
                bool weighted = _parameters.PoissonWeights;
                ComputeAtb(y, c, cycles, nr, nc, weighted ? _mu : null);
                var gram = weighted ? _weighted : _gram;
                for (int s = 0; s < ns; s++)
                {
                    _supportAtb[s] = _atb[_support[s]];
                    _supportStart[s] = _x[_support[s]];
                    for (int t = 0; t < ns; t++)
                        _supportGram[s * ns + t] = gram[_support[s] * nc + _support[t]];
                }
                NnlsSolver.SolveNormal(_supportGram, _supportAtb, ns, _supportX, _workspace, 0, _supportStart);
                Array.Clear(_x, 0, nc);
                for (int s = 0; s < ns; s++)
                    _x[_support[s]] = _supportX[s];
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
