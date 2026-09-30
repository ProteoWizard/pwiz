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
using System.Runtime.Intrinsics;
using System.Threading;

namespace pwiz.Osprey.Demux
{
    /// <summary>Settings for <see cref="JointDemultiplexer"/>.</summary>
    public sealed class JointDemuxParams
    {
        /// <summary>
        /// The TOF peak's sigma, in grid samples, at the m/z of <see cref="PeakSigmaMz"/>: linear between
        /// them, constant beyond. Measured on a ZenoTOF 8600 from strong isolated MS2 profile peaks.
        /// </summary>
        public double[] PeakSigmaSamples { get; set; } = { 1.17, 1.35, 1.51, 1.52 };

        /// <summary>The m/z of each <see cref="PeakSigmaSamples"/> value.</summary>
        public double[] PeakSigmaMz { get; set; } = { 300, 550, 850, 1200 };

        /// <summary>
        /// With the Gaussian, the sigmas at the m/z of <see cref="PeakSigmaMz"/> that <see cref="SigmaAt"/> gives, and
        /// so what neighbouring positions' centroids are merged within; null for the Gaussian's own. For fitting with
        /// a narrower or wider peak without changing the merge.
        /// </summary>
        public double[] MergeSigmaSamples { get; set; }

        /// <summary>The TOF peak's support: this many samples either side of its center.</summary>
        public int PeakHalfWidth { get; set; } = 5;

        /// <summary>
        /// The m/z of each measured kernel in <see cref="PeakShapes"/> (<see cref="TofPeakShape"/>), ascending; null
        /// to fit with the Gaussian of <see cref="PeakSigmaSamples"/>.
        /// </summary>
        public double[] PeakShapeMz { get; set; }

        /// <summary>
        /// Measured TOF peak kernels, one per <see cref="PeakShapeMz"/>: 2 <see cref="PeakHalfWidth"/> + 1 values
        /// for the offsets -h..h of a peak centred on a grid point, summing to 1.
        /// </summary>
        public double[][] PeakShapes { get; set; }

        /// <summary>
        /// The lasso weight on each coefficient, in standard deviations of its score under Poisson noise
        /// (0: none). A coefficient stays at zero unless the evidence for it exceeds this.
        /// </summary>
        public double L1Z { get; set; } = 2;

        /// <summary>Refit the coefficients the lasso kept without the penalty.</summary>
        public bool Relaxed { get; set; }

        /// <summary>
        /// Solve a second time with Poisson weights from the first solution's expected counts. The first
        /// solve's weights come from the data itself, each row's profile smoothed by the TOF peak.
        /// </summary>
        public bool Reweight { get; set; } = true;

        /// <summary>The expected count below which a sample's Poisson weight stops growing, in ions.</summary>
        public double WeightFloorIons { get; set; } = 0.5;

        /// <summary>Centroids below this many ions are not written.</summary>
        public double MinOutputIons { get; set; } = 0.2;

        /// <summary>Grid samples whose coefficients one chunk owns; chunks overlap by a margin.</summary>
        public int ChunkSamples { get; set; } = 2048;

        /// <summary>
        /// After each pass, drop the coefficients at zero from the active set; the next round's gradient check
        /// brings back any that should not be. The first check activates every position that transmits into a
        /// strong peak's bins, most of which the block solves then set to zero.
        /// </summary>
        public bool PruneActive { get; set; } = true;

        /// <summary>
        /// After reweighting, a grid point is solved again only where some position's lambda moved by more than
        /// this fraction (0: every point), as Centrix refits only the regions whose lambda moved by more than
        /// 20%; elsewhere the first solution stands, and a refitted point's moves still reach its neighbours.
        /// Where the counts are below the weight floor both weightings agree.
        /// </summary>
        public double RefitLambdaChange { get; set; } = 0.2;

        /// <summary>
        /// Over-relaxation of each block step (1: none, below 2): the step from the block's old coefficients to
        /// its optimum is lengthened by this factor, or less where a coefficient would go negative. Along m/z
        /// neighbouring coefficients are nearly interchangeable, and plain coordinate descent moves peak mass
        /// between them only slowly. Against the converged solve of 12 sweeps, 1.5 came closest for the fewest
        /// block solves with either peak shape (1.7 and above overshoot; 1.4 and below converge more slowly).
        /// </summary>
        public double Relaxation { get; set; } = 1.5;

        /// <summary>Coordinate-descent passes over the active set in one round.</summary>
        public int MaxPasses { get; set; } = 20;

        /// <summary>Rounds of adding the coefficients the full gradient says should be active.</summary>
        public int MaxRounds { get; set; } = 5;

        /// <summary>A round stops when no coefficient moves by more than this many ions.</summary>
        public double ToleranceIons { get; set; } = 1e-3;

        /// <summary>
        /// A round also stops when a pass lowers the objective by less than this fraction of it. Along m/z
        /// the coefficients of neighbouring grid points are nearly interchangeable (the TOF peak is about
        /// three samples wide), so single coefficients keep moving long after the fit, each position's peak
        /// totals and their m/z have settled.
        /// </summary>
        public double RelativeTolerance { get; set; } = 1e-4;

        /// <summary>
        /// Skip, in a descent's later gradient checks, the coefficients whose gradient from the data alone cannot
        /// beat their lambda (they can never violate; the solution is the same).
        /// </summary>
        public bool Screen { get; set; } = true;

        /// <summary>
        /// The neighbouring grid points whose active positions each block step solves together (1 to
        /// <see cref="JointDemultiplexer.MAX_BLOCK_POINTS"/>), the blocks' boundaries moving from pass to pass: with
        /// more than one, a peak's mass can move between grid points within one step.
        /// </summary>
        public int BlockPoints { get; set; } = 1;

        /// <summary>A copy, to change without changing these (arrays are replaced, not changed, so shared).</summary>
        public JointDemuxParams Copy()
        {
            return (JointDemuxParams)MemberwiseClone();
        }

        /// <summary>
        /// The kernel the solve fits with at an m/z, offsets -h..h, summing to 1: the measured kernels
        /// interpolated linearly between their m/z (the nearest beyond them), else the Gaussian of
        /// <see cref="GaussianSigmaAt"/>.
        /// </summary>
        public double[] PeakAt(double mz)
        {
            int half = PeakHalfWidth;
            var kernel = new double[2 * half + 1];
            if (PeakShapes == null || PeakShapes.Length == 0)
            {
                double sigma = GaussianSigmaAt(mz), sum = 0;
                for (int d = -half; d <= half; d++)
                {
                    double v = Math.Exp(-0.5 * d * d / (sigma * sigma));
                    kernel[d + half] = v;
                    sum += v;
                }
                for (int d = 0; d < kernel.Length; d++)
                    kernel[d] /= sum;
                return kernel;
            }
            var at = PeakShapeMz;
            int upper = 0;
            while (upper < at.Length && at[upper] < mz)
                upper++;
            int lower = Math.Max(0, upper - 1);
            upper = Math.Min(upper, at.Length - 1);
            double t = upper == lower ? 0 : (mz - at[lower]) / (at[upper] - at[lower]);
            double total = 0;
            for (int d = 0; d < kernel.Length; d++)
            {
                kernel[d] = (1 - t) * PeakShapes[lower][d] + t * PeakShapes[upper][d];
                total += kernel[d];
            }
            for (int d = 0; d < kernel.Length; d++)
                kernel[d] /= total;
            return kernel;
        }

        /// <summary>
        /// The TOF peak's width at an m/z in samples: the second moment of the measured kernel when there is
        /// one, else <see cref="MergeSigmaSamples"/> when set, else <see cref="GaussianSigmaAt"/>. What
        /// neighbouring positions' centroids are merged within.
        /// </summary>
        public double SigmaAt(double mz)
        {
            if (PeakShapes == null || PeakShapes.Length == 0)
                return MergeSigmaSamples != null ? Interpolate(PeakSigmaMz, MergeSigmaSamples, mz) : GaussianSigmaAt(mz);
            var kernel = PeakAt(mz);
            int half = PeakHalfWidth;
            double moment = 0;
            for (int d = -half; d <= half; d++)
                moment += d * d * kernel[d + half];
            return Math.Sqrt(moment);
        }

        /// <summary>The sigma of the Gaussian kernel at an m/z, from <see cref="PeakSigmaSamples"/>.</summary>
        public double GaussianSigmaAt(double mz)
        {
            return Interpolate(PeakSigmaMz, PeakSigmaSamples, mz);
        }

        /// <summary>Linear between the values at the m/z of <paramref name="at"/>, constant beyond them.</summary>
        private static double Interpolate(double[] at, double[] values, double mz)
        {
            if (mz <= at[0])
                return values[0];
            for (int i = 1; i < at.Length; i++)
            {
                if (mz <= at[i])
                    return values[i - 1] + (values[i] - values[i - 1]) * (mz - at[i - 1]) / (at[i] - at[i - 1]);
            }
            return values[values.Length - 1];
        }
    }

    /// <summary>
    /// Where the joint solve's time goes, summed across threads when <see cref="Enabled"/> (Stopwatch ticks and
    /// counts): for tuning the solver, off by default.
    /// </summary>
    public static class JointDemuxProfile
    {
        public static bool Enabled { get; set; }
        public static long SetupTicks;
        public static long WeightTicks;
        public static long GradientTicks;
        public static long PassTicks;
        public static long ObjectiveTicks;
        public static long Chunks;
        public static long Rounds;
        public static long Passes;
        public static long ActiveCoefficients;
        public static long BlockSolves;
        public static long BlockColumns;
        public static long DataPoints;
        public static long GridPoints;
        public static long CandidatePoints;
        public static long RefitPoints;
        public static long PassGradientTicks;
        public static long PassGramTicks;
        public static long PassNnlsTicks;
        public static long PassUpdateTicks;
        public static long SingleSolves;
        public static long MovedSolves;
        public static long EmptyVisits;
        /// <summary>The pass's parts, one line.</summary>
        public static string PassSummary()
        {
            double f = 1.0 / Stopwatch.Frequency;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"pass parts: block gradients {0:F1} s, Gram {1:F1} s, NNLS {2:F1} s, update {3:F1} s; " +
                @"{4} block solves, {5:P0} single, {6:P0} moved past the tolerance, {7} dirty points with no active position",
                PassGradientTicks * f, PassGramTicks * f, PassNnlsTicks * f, PassUpdateTicks * f, BlockSolves,
                SingleSolves / (double)Math.Max(BlockSolves, 1), MovedSolves / (double)Math.Max(BlockSolves, 1), EmptyVisits);
        }

        /// <summary>The totals, one line.</summary>
        public static string Summary()
        {
            double f = 1.0 / Stopwatch.Frequency;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                @"joint profile: setup {0:F1} s, weights {1:F1} s, gradient {2:F1} s, passes {3:F1} s, objective {4:F1} s; " +
                @"{5} chunks, {6:F1} rounds and {7:F1} passes per chunk, {8:F0} active coefficients per chunk, " +
                @"{9:F1} positions per block solve, {10:F0} block solves per pass; " +
                @"per chunk {11:F0} data points, {12:F0} grid points, {13:F0} with data in reach, {14:F0} refit after reweighting",
                SetupTicks * f, WeightTicks * f, GradientTicks * f, PassTicks * f, ObjectiveTicks * f, Chunks,
                Rounds / (double)Math.Max(Chunks, 1), Passes / (double)Math.Max(Chunks, 1),
                ActiveCoefficients / (double)Math.Max(Chunks, 1), BlockColumns / (double)Math.Max(BlockSolves, 1),
                BlockSolves / (double)Math.Max(Passes, 1), DataPoints / (double)Math.Max(Chunks, 1),
                GridPoints / (double)Math.Max(Chunks, 1), CandidatePoints / (double)Math.Max(Chunks, 1),
                RefitPoints / (double)Math.Max(Chunks, 1));
        }
    }

    /// <summary>
    /// Demultiplexes and centroids scanning-quadrupole (SCIEX ZT Scan) profile data in one solve (spec
    /// §5.4d). In each sweep the profile counts are
    /// y_i[k] = sum_j A_ij sum_q B[k - q] beta_j[q], beta &gt;= 0,
    /// with i an encoded bin, k a sample of the TOF grid (<see cref="TofGrid"/>, the same in every
    /// spectrum), A the measured quadrupole transmission of source position j into bin i, B the TOF
    /// peak shape, and beta_j[q] the fragment signal of position j at grid point q. Each position's
    /// coefficients then become its centroids.
    /// </summary>
    /// <remarks>
    /// <para>Against demultiplexing centroids channel by channel: the peak model ties a peak's samples to
    /// one set of positions, a lone tail sample included, and two fragments of different precursors
    /// within one peak width keep separate m/z. Against demultiplexing the profile sample by sample
    /// (§5.4c) and centroiding afterwards: each sample is not solved on its own.</para>
    /// <para>The solve: coordinate descent on the active set, the active set grown from the full gradient
    /// until no coefficient violates the optimality conditions. The gradient is factored as the spec
    /// describes: each row's weighted residual convolved with B once, then combined across rows by A.
    /// Each grid point's active positions are solved together exactly (their transmission columns are
    /// nearly collinear). The weights are Poisson, 1 / max(mu, floor): mu first from the data smoothed by B,
    /// then, with <see cref="JointDemuxParams.Reweight"/>, from the first solution; the z-scaled lasso keeps
    /// the active set sparse from the start. With <see cref="JointDemuxParams.Relaxed"/>, the kept
    /// coefficients are refitted without the penalty. A sweep's grid is solved in overlapping chunks;
    /// each chunk keeps only the coefficients it owns.</para>
    /// <para>Determinism: a fixed coordinate order, chunks in grid order, no state shared across
    /// units.</para>
    /// </remarks>
    public static class JointDemultiplexer
    {
        /// <summary>The most neighbouring grid points one block step solves together.</summary>
        public const int MAX_BLOCK_POINTS = 5;

        /// <summary>
        /// Demultiplexes one block of profile data. The unit's points are profile samples (zeros dropped),
        /// their intensities in ions; each must lie on <paramref name="grid"/>.
        /// </summary>
        public static ScanningUnitResult DemuxUnit(ScanningUnit unit, JointDemuxParams parameters, TofGrid grid)
        {
            var result = new ScanningUnitResult();
            int rows = unit.RowBins.Length, cycles = unit.Cycles.Length, columns = unit.ColumnBins.Length;
            var coreRow = new bool[rows];
            for (int r = 0; r < rows; r++)
                coreRow[r] = unit.RowBins[r] >= unit.FirstCoreBin && unit.RowBins[r] <= unit.LastCoreBin;
            var coreColumn = new bool[columns];
            for (int j = 0; j < columns; j++)
                coreColumn[j] = unit.ColumnBins[j] >= unit.FirstCoreBin && unit.ColumnBins[j] <= unit.LastCoreBin;

            var bySweep = new List<(long Sample, int Row, double Ions)>[cycles];
            for (int c = 0; c < cycles; c++)
                bySweep[c] = new List<(long, int, double)>();
            for (int p = 0; p < unit.Mz.Length; p++)
            {
                int c = unit.Cycle[p];
                int cycle = unit.Cycles[c];
                if (cycle < unit.FirstCoreCycle || cycle > unit.LastCoreCycle)
                    continue;
                bySweep[c].Add((grid.Index(unit.Mz[p]), unit.Row[p], unit.Ions[p]));
                if (coreRow[unit.Row[p]])
                    result.IonsIn += unit.Ions[p];
            }

            var solver = new ChunkSolver(unit.Transmission, parameters);
            int margin = 2 * parameters.PeakHalfWidth + 2;
            var kept = new List<(int Column, long Sample, double Beta)>();
            for (int c = 0; c < cycles; c++)
            {
                var points = bySweep[c];
                if (points.Count == 0)
                    continue;
                // Grid order, then row: the order every later step visits them in.
                points.Sort((a, b) => a.Sample != b.Sample ? a.Sample.CompareTo(b.Sample) : a.Row.CompareTo(b.Row)); // Array.Sort OK: (sample, row) is unique
                kept.Clear();
                long first = points[0].Sample, last = points[points.Count - 1].Sample;
                int start = 0;
                for (long core0 = first; core0 <= last; core0 += parameters.ChunkSamples)
                {
                    long core1 = Math.Min(core0 + parameters.ChunkSamples - 1, last);
                    long lo = core0 - margin, hi = core1 + margin;
                    // The samples the chunk's coefficients explain: [lo - half width, hi + half width].
                    long from = lo - parameters.PeakHalfWidth, to = hi + parameters.PeakHalfWidth;
                    while (start < points.Count && points[start].Sample < from)
                        start++;
                    int end = start;
                    while (end < points.Count && points[end].Sample <= to)
                        end++;
                    if (end == start)
                        continue;
                    double centerMz = grid.Mz(0.5 * (core0 + core1));
                    solver.Solve(points, start, end, lo, hi, parameters.PeakAt(centerMz));
                    solver.Keep(core0, core1, coreColumn, kept);
                    result.ChannelsSolved++;
                }
                Centroid(kept, unit, c, grid, parameters.MinOutputIons, result.Demultiplexed);
            }
            return result;
        }

        /// <summary>
        /// Each position's coefficients in one sweep as centroids: every run of adjacent grid points with a
        /// positive coefficient is one peak, at its coefficient-weighted m/z, carrying their sum.
        /// </summary>
        private static void Centroid(List<(int Column, long Sample, double Beta)> kept, ScanningUnit unit, int c,
            TofGrid grid, double minOutputIons, List<ScanningPeak> output)
        {
            kept.Sort((a, b) => a.Column != b.Column ? a.Column.CompareTo(b.Column) : a.Sample.CompareTo(b.Sample)); // Array.Sort OK: (column, sample) is unique
            int k = 0;
            while (k < kept.Count)
            {
                int column = kept[k].Column;
                double sum = 0, weighted = 0;
                long previous = kept[k].Sample - 1;
                while (k < kept.Count && kept[k].Column == column && kept[k].Sample == previous + 1)
                {
                    sum += kept[k].Beta;
                    weighted += kept[k].Beta * kept[k].Sample;
                    previous = kept[k].Sample;
                    k++;
                }
                if (sum >= minOutputIons)
                    output.Add(new ScanningPeak(unit.ColumnBins[column], unit.Cycles[c], grid.Mz(weighted / sum), sum));
            }
        }

        /// <summary>
        /// The solve of one chunk of one sweep's grid, reusing its buffers from chunk to chunk within a
        /// block (one per block, so not shared across threads).
        /// </summary>
        /// <remarks>
        /// Coefficients are stored grid point by grid point, a point's positions together, the order the
        /// block solves visit them in. Only the grid points with data within a peak's reach are visited: a
        /// coefficient whose peak covers no data can only add to the misfit, so it is zero.
        /// A coefficient's gradient depends only on the residual under its peak, which the coefficients within
        /// two peak half-widths share. So a grid point is solved again only when a coefficient within that
        /// reach has moved by more than the tolerance since it was last solved, and after a round's first full
        /// gradient check only the grid points near a change are checked again.
        /// </remarks>
        private sealed class ChunkSolver
        {
            private readonly int _rows;
            private readonly int _columns;
            private readonly JointDemuxParams _parameters;
            private readonly int[] _columnStart;             // per column, its span of _columnRow and _columnA
            private readonly int[] _columnRow;               // the rows each column transmits into, ascending
            private readonly double[] _columnA;              // the transmission into each of them
            private readonly double[] _columnA2;             // its square
            // Per pair of columns j <= j' (index j * columns + j'), the rows both transmit into, ascending, and the
            // product of their transmissions there: a block Hessian entry is these times the grid point's row weights.
            // A block's positions are mostly far apart, and many pairs share no row at all.
            private readonly int[] _pairStart;
            private readonly int[] _pairRow;
            private readonly double[] _pairA;
            private readonly double[] _b;                    // the peak, offsets -h..h, unit area
            private readonly double[] _b2;
            // The peak padded with zeros to whole groups of WIDTH terms: a sample's 2h + 1 terms as
            // a few four-lane operations, in 256-bit vectors where the hardware has them and otherwise in four
            // scalar partial sums in the same lane order, reduced in one fixed order, so every machine gets the
            // same bits. The sample buffers are WIDTH longer than they hold, so the zero terms past the last
            // sample read finite values (and subtract exactly 0 from them).
            private const int WIDTH = 4;
            private static readonly bool VECTORS = Vector256.IsHardwareAccelerated;
            private readonly double[] _bp;
            private readonly Vector256<double>[] _bv;
            private readonly int _half;
            private readonly double[] _laneGradient = new double[WIDTH];
            private int _samples;                            // samples in the chunk: [lo - h, hi + h]
            private int _points;                             // grid points with coefficients: [lo, hi]
            private long _lo;
            private double[] _y = Array.Empty<double>();     // rows x samples
            private double[] _residual = Array.Empty<double>();
            private double[] _weight = Array.Empty<double>();
            private double[] _beta = Array.Empty<double>();  // points x columns
            private double[] _curvature = Array.Empty<double>();
            private double[] _lambda = Array.Empty<double>();
            private bool[] _active = Array.Empty<bool>();
            // Screening: a coefficient whose gradient from the data alone cannot beat its lambda. Within one descent
            // the weights are fixed and the model only adds non-negative peaks, so the residual stays at or below
            // the data and no coefficient's gradient exceeds its value at zero: a screened coefficient never
            // violates, and the later gradient checks of the descent skip it. Per grid point, the columns left.
            private bool[] _screened = Array.Empty<bool>();
            private int[] _unscreened = Array.Empty<int>();
            private bool _screening;                         // the descent's first check computes the screening
            private bool _screenReady;                         // the descent has a screening
            // The gradient check, four neighbouring grid points at a time: the weighted residual (rows x samples),
            // B convolved with it (rows x _groupPoints, grid point fastest), and which groups of four to check.
            private double[] _wr = Array.Empty<double>();
            private double[] _zRow = Array.Empty<double>();
            private double[] _vRow = Array.Empty<double>();  // rows x _groupPoints: v, grid point fastest
            private bool[] _groupCheck = Array.Empty<bool>();
            private readonly int[] _rowSlid;                 // per row: the group check that slid its peak last
            private int _slideStamp;
            private bool[] _inReach = Array.Empty<bool>();
            private int _groupPoints;                        // grid points rounded up to whole groups
            private double[] _v = Array.Empty<double>();     // points x rows: B^2 convolved with the weights
            private bool[] _sampleData = Array.Empty<bool>(); // per sample: data in some row
            private int[] _reach = Array.Empty<int>();       // the grid points with data in reach, ascending
            private int _reachCount;
            private bool[] _dirty = Array.Empty<bool>();      // per grid point: to solve in the next pass
            private int[] _activeCount = Array.Empty<int>();  // per grid point: its active positions
            private int[] _solved = Array.Empty<int>();       // the grid points the current pass has solved
            private int _solvedCount;
            private bool[] _changed = Array.Empty<bool>();    // per grid point: gradient changed since the last check
            private readonly double[] _rowGradient;          // per row: the peak times its weighted residual at a block's point
            private readonly int[] _rowStamp;                // per row: the block solve _rowGradient was computed for
            private readonly double[] _rowDelta;             // per row: a block step's change to its peak's height
            private readonly int[] _rowsMoved;               // the rows a block step changes
            private readonly bool[] _rowIsMoved;
            private int _stamp;
            private readonly int[] _blockColumns;            // a block's active columns
            private readonly int[] _blockPoint;              // with BlockPoints > 1, each one's grid point
            // With BlockPoints > 1, per offset k of a block's grid point from its first: _rowGradient and _rowStamp
            // at that point; B[d] B[d - k], a peak times the one k grid points on; and that times the weights
            // (points x rows), the cross term of v between grid points k apart.
            private readonly double[][] _rowGradientAt = new double[MAX_BLOCK_POINTS][];
            private readonly int[][] _rowStampAt = new int[MAX_BLOCK_POINTS][];
            private readonly double[][] _bShift = new double[MAX_BLOCK_POINTS][];
            private readonly double[][] _vShift = new double[MAX_BLOCK_POINTS][];
            private double[] _vShiftRow = Array.Empty<double>();
            private readonly double[] _blockGradient;
            private readonly double[] _blockOld;
            private readonly double[] _blockNew;
            private readonly double[] _blockRhs;
            private readonly double[] _blockGram;
            private readonly NnlsSolver.Workspace _workspace;
            private readonly bool[] _rowSignal;
            private readonly bool[] _rowUsed;
            private readonly bool[] _columnUsed;
            private long _weightTicks, _gradientTicks, _passTicks, _objectiveTicks, _rounds, _passes, _blockSolves, _blockColumnCount;
            private long _dataPoints;
            private long _refitPoints;
            private long _tGrad, _tGram, _tNnls, _tUpdate, _singles, _moves, _empty;
            private double _decrease;                        // the objective's decrease over the current pass

            public ChunkSolver(double[,] a, JointDemuxParams parameters)
            {
                _rows = a.GetLength(0);
                _columns = a.GetLength(1);
                _parameters = parameters;
                _half = parameters.PeakHalfWidth;
                _b = new double[2 * _half + 1];
                for (int o = 1; o < MAX_BLOCK_POINTS; o++)
                    _bShift[o] = new double[2 * _half + 1];
                _b2 = new double[2 * _half + 1];
                int groups = (_b.Length + WIDTH - 1) / WIDTH;
                _bp = new double[groups * WIDTH];
                _bv = new Vector256<double>[groups];
                _columnStart = new int[_columns + 1];
                int entries = 0;
                for (int j = 0; j < _columns; j++)
                {
                    for (int r = 0; r < _rows; r++)
                    {
                        if (a[r, j] > 0)
                            entries++;
                    }
                }
                _columnRow = new int[entries];
                _columnA = new double[entries];
                int k = 0;
                for (int j = 0; j < _columns; j++)
                {
                    _columnStart[j] = k;
                    for (int r = 0; r < _rows; r++)
                    {
                        if (a[r, j] > 0)
                        {
                            _columnRow[k] = r;
                            _columnA[k] = a[r, j];
                            k++;
                        }
                    }
                }
                _columnStart[_columns] = k;
                _columnA2 = new double[entries];
                for (int e = 0; e < entries; e++)
                    _columnA2[e] = _columnA[e] * _columnA[e];
                _pairStart = new int[_columns * _columns + 1];
                var pairRow = new List<int>();
                var pairA = new List<double>();
                for (int j = 0; j < _columns; j++)
                {
                    for (int j2 = 0; j2 < _columns; j2++)
                    {
                        _pairStart[j * _columns + j2] = pairRow.Count;
                        if (j2 < j)
                            continue;
                        for (int m = _columnStart[j]; m < _columnStart[j + 1]; m++)
                        {
                            double a2 = a[_columnRow[m], j2];
                            if (a2 > 0)
                            {
                                pairRow.Add(_columnRow[m]);
                                pairA.Add(_columnA[m] * a2);
                            }
                        }
                    }
                }
                _pairStart[_columns * _columns] = pairRow.Count;
                _pairRow = pairRow.ToArray();
                _pairA = pairA.ToArray();
                _rowSignal = new bool[_rows];
                _rowUsed = new bool[_rows];
                _rowGradient = new double[_rows];
                _rowSlid = new int[_rows];
                _rowDelta = new double[_rows];
                _rowsMoved = new int[_rows];
                _rowIsMoved = new bool[_rows];
                _rowStamp = new int[_rows];
                _rowGradientAt[0] = _rowGradient;
                _rowStampAt[0] = _rowStamp;
                for (int o = 1; o < MAX_BLOCK_POINTS; o++)
                {
                    _rowGradientAt[o] = new double[_rows];
                    _rowStampAt[o] = new int[_rows];
                    _vShift[o] = Array.Empty<double>();
                }
                _columnUsed = new bool[_columns];
                int most = MAX_BLOCK_POINTS * _columns;
                _blockColumns = new int[most];
                _blockPoint = new int[most];
                _blockGradient = new double[most];
                _blockOld = new double[most];
                _blockNew = new double[most];
                _blockRhs = new double[most];
                _blockGram = new double[most * most];
                _workspace = new NnlsSolver.Workspace(most);
            }

            /// <summary>Solves the coefficients of grid points [lo, hi] from points[start, end).</summary>
            public void Solve(List<(long Sample, int Row, double Ions)> points, int start, int end, long lo, long hi,
                double[] kernel)
            {
                long t0 = Now();
                _lo = lo;
                _points = (int)(hi - lo + 1);
                _samples = _points + 2 * _half;
                Allocate();
                SetPeak(kernel);

                Array.Clear(_y, 0, _rows * _samples);
                Array.Clear(_rowSignal, 0, _rows);
                Array.Clear(_rowStamp, 0, _rows);
                Array.Clear(_rowSlid, 0, _rows);
                _slideStamp = 0;
                for (int o = 1; o < MAX_BLOCK_POINTS; o++)
                    Array.Clear(_rowStampAt[o], 0, _rows);
                _stamp = 0;
                Array.Clear(_sampleData, 0, _samples);
                long baseSample = lo - _half;
                _dataPoints = end - start;
                for (int p = start; p < end; p++)
                {
                    int s = (int)(points[p].Sample - baseSample);
                    _y[points[p].Row * _samples + s] += points[p].Ions;
                    _rowSignal[points[p].Row] = true;
                    _sampleData[s] = true;
                }
                // The grid points whose peak, samples [q, q + 2h], covers some data.
                _reachCount = 0;
                int width = 2 * _half, inWindow = 0;
                for (int s = 0; s < width; s++)
                {
                    if (_sampleData[s])
                        inWindow++;
                }
                for (int q = 0; q < _points; q++)
                {
                    if (_sampleData[q + width])
                        inWindow++;
                    _inReach[q] = inWindow > 0;
                    if (inWindow > 0)
                        _reach[_reachCount++] = q;
                    if (_sampleData[q])
                        inWindow--;
                }
                // The positions any signal-bearing row could come from, and the rows that see them.
                Array.Clear(_rowUsed, 0, _rows);
                for (int j = 0; j < _columns; j++)
                {
                    _columnUsed[j] = false;
                    for (int k = _columnStart[j]; k < _columnStart[j + 1] && !_columnUsed[j]; k++)
                        _columnUsed[j] = _rowSignal[_columnRow[k]];
                    if (!_columnUsed[j])
                        continue;
                    for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        _rowUsed[_columnRow[k]] = true;
                }

                int cp = _points * _columns;
                Array.Clear(_beta, 0, cp);
                Array.Clear(_curvature, 0, cp);
                Array.Clear(_lambda, 0, cp);
                Array.Clear(_active, 0, cp);
                Array.Clear(_activeCount, 0, _points);
                Array.Copy(_y, _residual, _rows * _samples);
                long t1 = Now();
                // 1. Poisson weights from the data smoothed by the peak, with the z-scaled lasso.
                FillWeights(false);
                Descend(_parameters.L1Z, true, false, true);
                // 2. Poisson weights from that solution's expected counts.
                if (_parameters.Reweight)
                {
                    FillWeights(true);
                    Descend(_parameters.L1Z, true, _parameters.RefitLambdaChange > 0);
                }
                // 3. Relaxed: the kept coefficients without the penalty.
                if (_parameters.Relaxed && _parameters.L1Z > 0)
                    Descend(0, false);
                if (JointDemuxProfile.Enabled)
                    Flush(t1 - t0);
            }

            /// <summary>Adds the positive coefficients of the core grid points [core0, core1], core columns only.</summary>
            public void Keep(long core0, long core1, bool[] coreColumn, List<(int Column, long Sample, double Beta)> kept)
            {
                int q0 = (int)(core0 - _lo), q1 = (int)(core1 - _lo);
                for (int j = 0; j < _columns; j++)
                {
                    if (!_columnUsed[j] || !coreColumn[j])
                        continue;
                    for (int q = q0; q <= q1; q++)
                    {
                        double beta = _beta[q * _columns + j];
                        if (beta > 0)
                            kept.Add((j, _lo + q, beta));
                    }
                }
            }

            private static long Now()
            {
                return JointDemuxProfile.Enabled ? Stopwatch.GetTimestamp() : 0;
            }

            private void Flush(long setupTicks)
            {
                long active = 0;
                for (int i = 0; i < _columns * _points; i++)
                {
                    if (_beta[i] > 0)
                        active++;
                }
                Interlocked.Add(ref JointDemuxProfile.SetupTicks, setupTicks);
                Interlocked.Add(ref JointDemuxProfile.WeightTicks, _weightTicks);
                Interlocked.Add(ref JointDemuxProfile.GradientTicks, _gradientTicks);
                Interlocked.Add(ref JointDemuxProfile.PassTicks, _passTicks);
                Interlocked.Add(ref JointDemuxProfile.ObjectiveTicks, _objectiveTicks);
                Interlocked.Increment(ref JointDemuxProfile.Chunks);
                Interlocked.Add(ref JointDemuxProfile.Rounds, _rounds);
                Interlocked.Add(ref JointDemuxProfile.Passes, _passes);
                Interlocked.Add(ref JointDemuxProfile.ActiveCoefficients, active);
                Interlocked.Add(ref JointDemuxProfile.BlockSolves, _blockSolves);
                Interlocked.Add(ref JointDemuxProfile.BlockColumns, _blockColumnCount);
                Interlocked.Add(ref JointDemuxProfile.DataPoints, _dataPoints);
                Interlocked.Add(ref JointDemuxProfile.GridPoints, _points);
                Interlocked.Add(ref JointDemuxProfile.CandidatePoints, _reachCount);
                Interlocked.Add(ref JointDemuxProfile.RefitPoints, _refitPoints);
                Interlocked.Add(ref JointDemuxProfile.PassGradientTicks, _tGrad);
                Interlocked.Add(ref JointDemuxProfile.PassGramTicks, _tGram);
                Interlocked.Add(ref JointDemuxProfile.PassNnlsTicks, _tNnls);
                Interlocked.Add(ref JointDemuxProfile.PassUpdateTicks, _tUpdate);
                Interlocked.Add(ref JointDemuxProfile.SingleSolves, _singles);
                Interlocked.Add(ref JointDemuxProfile.MovedSolves, _moves);
                Interlocked.Add(ref JointDemuxProfile.EmptyVisits, _empty);
                _tGrad = _tGram = _tNnls = _tUpdate = _singles = _moves = _empty = 0;
                _refitPoints = 0;
                _weightTicks = _gradientTicks = _passTicks = _objectiveTicks = _rounds = _passes = _blockSolves = _blockColumnCount = 0;
            }

            private void Allocate()
            {
                int rs = _rows * _samples, cp = _columns * _points, rp = _rows * _points;
                if (_y.Length < rs + WIDTH)
                {
                    _y = new double[rs + WIDTH];
                    _residual = new double[rs + WIDTH];
                    _weight = new double[rs + WIDTH];
                }
                if (_beta.Length < cp)
                {
                    _beta = new double[cp];
                    _curvature = new double[cp];
                    _lambda = new double[cp];
                    _active = new bool[cp];
                    _screened = new bool[cp];
                }
                if (_v.Length < rp)
                    _v = new double[rp];
                for (int o = 1; o < _parameters.BlockPoints; o++)
                {
                    if (_vShift[o].Length < rp)
                        _vShift[o] = new double[rp];
                }
                _groupPoints = (_points + WIDTH - 1) / WIDTH * WIDTH;
                if (_zRow.Length < _rows * _groupPoints)
                {
                    _zRow = new double[_rows * _groupPoints];
                    _vRow = new double[_rows * _groupPoints];
                    _vShiftRow = new double[_rows * _groupPoints];
                }
                if (_wr.Length < rs + 2 * WIDTH)
                    _wr = new double[rs + 2 * WIDTH];
                if (_groupCheck.Length < _groupPoints / WIDTH)
                    _groupCheck = new bool[_groupPoints / WIDTH];
                if (_sampleData.Length < _samples)
                {
                    _sampleData = new bool[_samples];
                    _reach = new int[_samples];
                    _inReach = new bool[_samples];
                    _dirty = new bool[_samples];
                    _activeCount = new int[_samples];
                    _unscreened = new int[_samples];
                    _solved = new int[_samples];
                    _changed = new bool[_samples];
                }
            }

            private void SetPeak(double[] kernel)
            {
                for (int d = 0; d < _b.Length; d++)
                {
                    _b[d] = kernel[d];
                    _b2[d] = _b[d] * _b[d];
                }
                for (int o = 1; o < MAX_BLOCK_POINTS; o++)
                {
                    for (int d = 0; d < _b.Length; d++)
                        _bShift[o][d] = d >= o ? _b[d] * _b[d - o] : 0;
                }
                Array.Copy(_b, _bp, _b.Length);
                for (int m = 0; m < _bv.Length; m++)
                    _bv[m] = Vector256.Create(_bp, m * WIDTH);
            }

            /// <summary>
            /// Poisson weights 1 / max(mu, floor), with mu the model's expected counts, y - residual, or before
            /// there is a model each row's counts smoothed by the peak; then each coefficient's curvature for
            /// the weights.
            /// </summary>
            private void FillWeights(bool fromModel)
            {
                long t0 = Now();
                double floor = _parameters.WeightFloorIons;
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples;
                    if (fromModel)
                    {
                        for (int s = 0; s < _samples; s++)
                            _weight[o + s] = 1 / Math.Max(_y[o + s] - _residual[o + s], floor);
                        continue;
                    }
                    // mu[s] = sum_d B[d] y[s + d], each sample's terms in ascending d: spread from each sample with
                    // data in ascending order, which leaves out only the zero terms (most samples hold no data).
                    Array.Clear(_weight, o, _samples);
                    for (int t = 0; t < _samples; t++)
                    {
                        double yt = _y[o + t];
                        if (yt == 0)
                            continue;
                        int d0 = Math.Max(-_half, t - (_samples - 1)), d1 = Math.Min(_half, t);
                        for (int d = d1; d >= d0; d--)
                            _weight[o + t - d] += _b[d + _half] * yt;
                    }
                    for (int s = 0; s < _samples; s++)
                        _weight[o + s] = 1 / Math.Max(_weight[o + s], floor);
                }
                // v[q, r] = sum_d B[d]^2 w[r, q + d]; curvature[q, j] = sum_r A_rj^2 v[q, r]: four neighbouring grid
                // points at a time, as in the gradient check, each lane's sums in a single point's order.
                int groups = _groupPoints / WIDTH;
                Array.Clear(_groupCheck, 0, groups);
                for (int i = 0; i < _reachCount; i++)
                    _groupCheck[_reach[i] / WIDTH] = true;
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples, oz = r * _groupPoints;
                    for (int m = 0; m < groups; m++)
                    {
                        if (!_groupCheck[m])
                            continue;
                        int q0 = m * WIDTH;
                        Slide(_b2, _weight, o + q0, _vRow, oz + q0);
                        for (int l = 0; l < WIDTH && q0 + l < _points; l++)
                            _v[(q0 + l) * _rows + r] = _vRow[oz + q0 + l];
                        for (int k = 1; k < _parameters.BlockPoints; k++)
                        {
                            Slide(_bShift[k], _weight, o + q0, _vShiftRow, oz + q0);
                            for (int l = 0; l < WIDTH && q0 + l < _points; l++)
                                _vShift[k][(q0 + l) * _rows + r] = _vShiftRow[oz + q0 + l];
                        }
                    }
                }
                for (int m = 0; m < groups; m++)
                {
                    if (!_groupCheck[m])
                        continue;
                    int q0 = m * WIDTH;
                    for (int j = 0; j < _columns; j++)
                    {
                        if (!_columnUsed[j])
                            continue;
                        ColumnSum(_columnA2, _vRow, j, q0);
                        for (int l = 0; l < WIDTH && q0 + l < _points; l++)
                        {
                            if (_inReach[q0 + l])
                                _curvature[(q0 + l) * _columns + j] = _laneGradient[l];
                        }
                    }
                }
                _weightTicks += Now() - t0;
            }

            /// <summary>
            /// Coordinate descent with a lasso of z standard deviations per coefficient (lambda = z sqrt of its
            /// curvature), on an active set grown from the full gradient until nothing violates the
            /// optimality conditions; or, when <paramref name="grow"/> is false, on the current support only.
            /// With <paramref name="selective"/>, only the grid points where some lambda moved by more than
            /// <see cref="JointDemuxParams.RefitLambdaChange"/> since the last descent start out to be solved.
            /// </summary>
            private void Descend(double z, bool grow = true, bool selective = false, bool screen = false)
            {
                double change = _parameters.RefitLambdaChange;
                _screening = screen && grow && _parameters.Screen;
                _screenReady = false;
                Array.Clear(_screened, 0, _points * _columns);
                for (int i = 0; i < _reachCount; i++)
                {
                    int q = _reach[i], oc = q * _columns, count = 0;
                    bool refit = !selective;
                    for (int j = 0; j < _columns; j++)
                    {
                        double lambda = z > 0 ? z * Math.Sqrt(_curvature[oc + j]) : 0;
                        double before = _lambda[oc + j];
                        if (!refit && before > 0 && Math.Abs(lambda / before - 1) > change)
                            refit = true;
                        _lambda[oc + j] = lambda;
                        _active[oc + j] = _beta[oc + j] > 0;
                        if (_active[oc + j])
                            count++;
                    }
                    _activeCount[q] = count;
                    _dirty[q] = _changed[q] = refit;
                    if (selective && refit)
                        _refitPoints++;
                }
                long o0 = Now();
                double objective = Objective();
                _objectiveTicks += Now() - o0;
                for (int round = 0; round < _parameters.MaxRounds; round++)
                {
                    long t0 = Now();
                    int added = grow ? AddViolators() : 0;
                    if (_screening)
                    {
                        _screening = false;
                        _screenReady = true;
                    }
                    long t1 = Now();
                    _gradientTicks += t1 - t0;
                    if (grow && added == 0 && round > 0)
                        break;
                    _rounds++;
                    for (int pass = 0; pass < _parameters.MaxPasses; pass++)
                    {
                        long t2 = Now();
                        double largest = _parameters.BlockPoints > 1 ? MultiPass(pass) : Pass();
                        if (grow && _parameters.PruneActive)
                            Prune();
                        _passTicks += Now() - t2;
                        _passes++;
                        // Each block step's decrease is exact, so the objective is tracked, not recomputed.
                        objective -= _decrease;
                        if (largest < _parameters.ToleranceIons || _decrease <= _parameters.RelativeTolerance * objective)
                            break;
                    }
                    if (!grow)
                        break;
                }
            }

            /// <summary>
            /// Lengthens the block step from _blockOld to its optimum _blockNew by the relaxation factor, or to the
            /// point where the first coefficient reaches zero, and returns the factor used. The objective along
            /// the step is a parabola with its minimum at the optimum, so any length up to twice that does not
            /// raise it.
            /// </summary>
            private double Relax(int n)
            {
                double omega = _parameters.Relaxation;
                if (omega == 1)
                    return 1;
                double t = omega;
                for (int u = 0; u < n; u++)
                {
                    double du = _blockNew[u] - _blockOld[u];
                    if (du < 0)
                        t = Math.Min(t, _blockOld[u] / -du);
                }
                for (int u = 0; u < n; u++)
                    _blockNew[u] = Math.Max(0, _blockOld[u] + t * (_blockNew[u] - _blockOld[u]));
                return t;
            }

            /// <summary>The peak times the weighted residual over the samples from <paramref name="o"/>.</summary>
            private double PeakGradient(int o)
            {
                if (VECTORS)
                {
                    var sum = Vector256<double>.Zero;
                    for (int m = 0, x = o; m < _bv.Length; m++, x += WIDTH)
                        sum += _bv[m] * Vector256.Create(_weight, x) * Vector256.Create(_residual, x);
                    return Reduce(sum.GetElement(0), sum.GetElement(1), sum.GetElement(2), sum.GetElement(3));
                }
                double s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                for (int d = 0, x = o; d < _bp.Length; d += WIDTH, x += WIDTH)
                {
                    s0 += _bp[d] * _weight[x] * _residual[x];
                    s1 += _bp[d + 1] * _weight[x + 1] * _residual[x + 1];
                    s2 += _bp[d + 2] * _weight[x + 2] * _residual[x + 2];
                    s3 += _bp[d + 3] * _weight[x + 3] * _residual[x + 3];
                }
                return Reduce(s0, s1, s2, s3);
            }

            /// <summary>The four lanes' partial sums, added in one fixed order.</summary>
            private static double Reduce(double s0, double s1, double s2, double s3)
            {
                return (s0 + s1) + (s2 + s3);
            }

            /// <summary>Takes <paramref name="scale"/> times the peak from the residual over the samples from <paramref name="o"/>.</summary>
            private void SubtractPeak(int o, double scale)
            {
                if (VECTORS)
                {
                    var factor = Vector256.Create(scale);
                    for (int m = 0, x = o; m < _bv.Length; m++, x += WIDTH)
                        (Vector256.Create(_residual, x) - factor * _bv[m]).CopyTo(_residual, x);
                    return;
                }
                for (int d = 0, x = o; d < _bp.Length; d++, x++)
                    _residual[x] -= scale * _bp[d];
            }

            /// <summary>
            /// One pass of block coordinate descent over runs of up to <see cref="JointDemuxParams.BlockPoints"/>
            /// neighbouring grid points: each run's active positions at all its points solved together, the terms
            /// between points from the shifted v, so that a peak's mass can move between the points within one step.
            /// The runs' boundaries fall on the multiples of the run length offset by the pass; a run stops early where
            /// the next point is not in reach. Otherwise as <see cref="Pass"/>.
            /// </summary>
            private double MultiPass(int pass)
            {
                int length = _parameters.BlockPoints, offset = pass % length;
                double largest = 0;
                _decrease = 0;
                _solvedCount = 0;
                for (int i = 0; i < _reachCount; i++)
                {
                    int q = _reach[i];
                    int most = length - (q + offset) % length, span = 1;
                    while (span < most && i + span < _reachCount && _reach[i + span] == q + span)
                        span++;
                    i += span - 1;
                    bool dirty = false;
                    for (int p = q; p < q + span; p++)
                    {
                        dirty |= _dirty[p];
                        _dirty[p] = false;
                    }
                    if (!dirty)
                        continue;
                    int n = 0;
                    for (int p = q; p < q + span; p++)
                    {
                        if (_activeCount[p] == 0)
                            continue;
                        int op = p * _columns;
                        for (int j = 0; j < _columns; j++)
                        {
                            if (_active[op + j] && _curvature[op + j] > 0)
                            {
                                _blockColumns[n] = j;
                                _blockPoint[n] = p;
                                n++;
                            }
                        }
                    }
                    if (n == 0)
                    {
                        _empty++;
                        continue;
                    }
                    for (int p = q; p < q + span; p++)
                        _solved[_solvedCount++] = p;
                    _blockSolves++;
                    _blockColumnCount += n;
                    double stretch;
                    _stamp++;
                    for (int u = 0; u < n; u++)
                    {
                        int j = _blockColumns[u], p = _blockPoint[u];
                        var rowGradient = _rowGradientAt[p - q];
                        var rowStamp = _rowStampAt[p - q];
                        double g = 0;
                        for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        {
                            int r = _columnRow[k];
                            if (rowStamp[r] != _stamp)
                            {
                                rowGradient[r] = PeakGradient(r * _samples + p);
                                rowStamp[r] = _stamp;
                            }
                            g += _columnA[k] * rowGradient[r];
                        }
                        _blockGradient[u] = g;
                        _blockOld[u] = _beta[p * _columns + j];
                    }
                    if (n == 1)
                    {
                        _singles++;
                        int index = _blockPoint[0] * _columns + _blockColumns[0];
                        double c = _curvature[index];
                        _blockNew[0] = Math.Max(0, _blockOld[0] + (_blockGradient[0] - _lambda[index]) / c);
                        stretch = Relax(1);
                        double step = _blockNew[0] - _blockOld[0];
                        _decrease += (_blockGradient[0] - _lambda[index]) * step - 0.5 * c * step * step;
                    }
                    else
                    {
                        // Within a point, H = sum_r A A v[p, r]; between points k apart, sum_r A A vShift_k[first, r].
                        for (int u = 0; u < n; u++)
                        {
                            int ju = _blockColumns[u], pu = _blockPoint[u];
                            for (int w = u; w < n; w++)
                            {
                                int jw = _blockColumns[w], pw = _blockPoint[w];
                                int pair = Math.Min(ju, jw) * _columns + Math.Max(ju, jw);
                                var weights = pu == pw ? _v : _vShift[Math.Abs(pw - pu)];
                                int ow = Math.Min(pu, pw) * _rows;
                                int m1 = _pairStart[pair + 1];
                                double h = 0;
                                for (int m = _pairStart[pair]; m < m1; m++)
                                    h += _pairA[m] * weights[ow + _pairRow[m]];
                                _blockGram[u * n + w] = h;
                                _blockGram[w * n + u] = h;
                            }
                        }
                        for (int u = 0; u < n; u++)
                        {
                            double rhs = _blockGradient[u] - _lambda[_blockPoint[u] * _columns + _blockColumns[u]];
                            for (int w = 0; w < n; w++)
                                rhs += _blockGram[u * n + w] * _blockOld[w];
                            _blockRhs[u] = rhs;
                        }
                        NnlsSolver.SolveNormal(_blockGram, _blockRhs, n, _blockNew, _workspace, 0, _blockOld);
                        stretch = Relax(n);
                        for (int u = 0; u < n; u++)
                        {
                            double du = _blockNew[u] - _blockOld[u];
                            if (du == 0)
                                continue;
                            double hd = 0;
                            for (int w = 0; w < n; w++)
                                hd += _blockGram[u * n + w] * (_blockNew[w] - _blockOld[w]);
                            _decrease += (_blockGradient[u] - _lambda[_blockPoint[u] * _columns + _blockColumns[u]]) * du - 0.5 * du * hd;
                        }
                    }
                    double moved = 0;
                    for (int p = q; p < q + span; p++)
                    {
                        int rowsMoved = 0;
                        for (int u = 0; u < n; u++)
                        {
                            double delta = _blockNew[u] - _blockOld[u];
                            if (_blockPoint[u] != p || delta == 0)
                                continue;
                            int j = _blockColumns[u];
                            _beta[p * _columns + j] = _blockNew[u];
                            for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                            {
                                int r = _columnRow[k];
                                if (!_rowIsMoved[r])
                                {
                                    _rowIsMoved[r] = true;
                                    _rowsMoved[rowsMoved++] = r;
                                }
                                _rowDelta[r] += _columnA[k] * delta;
                            }
                            moved = Math.Max(moved, Math.Abs(delta));
                        }
                        for (int t = 0; t < rowsMoved; t++)
                        {
                            int r = _rowsMoved[t];
                            SubtractPeak(r * _samples + p, _rowDelta[r]);
                            _rowDelta[r] = 0;
                            _rowIsMoved[r] = false;
                        }
                    }
                    largest = Math.Max(largest, moved);
                    if (moved > _parameters.ToleranceIons)
                        _moves++;
                    if (moved <= _parameters.ToleranceIons)
                        continue;
                    bool overshot = (stretch - 1) / stretch * moved > _parameters.ToleranceIons;
                    int reach0 = Math.Max(0, q - 2 * _half), reach1 = Math.Min(_points - 1, q + span - 1 + 2 * _half);
                    for (int t = reach0; t <= reach1; t++)
                    {
                        _changed[t] = true;
                        if (t < q || t >= q + span || overshot)
                            _dirty[t] = true;
                    }
                }
                return largest;
            }

            /// <summary>
            /// Makes the active set the positive coefficients. Only the grid points the last pass solved can have
            /// changed since the last prune: elsewhere the active set already is the positive coefficients.
            /// </summary>
            private void Prune()
            {
                for (int i = 0; i < _solvedCount; i++)
                {
                    int q = _solved[i], oc = q * _columns, count = 0;
                    for (int j = 0; j < _columns; j++)
                    {
                        _active[oc + j] = _beta[oc + j] > 0;
                        if (_active[oc + j])
                            count++;
                    }
                    _activeCount[q] = count;
                }
            }

            /// <summary>The objective: half the weighted squared residual plus the lasso term.</summary>
            private double Objective()
            {
                double sum = 0;
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples;
                    for (int s = 0; s < _samples; s++)
                        sum += 0.5 * _weight[o + s] * _residual[o + s] * _residual[o + s];
                }
                for (int j = 0; j < _columns; j++)
                {
                    if (!_columnUsed[j])
                        continue;
                    for (int i = 0; i < _reachCount; i++)
                    {
                        int index = _reach[i] * _columns + j;
                        sum += _lambda[index] * _beta[index];
                    }
                }
                return sum;
            }

            /// <summary>
            /// The full gradient, factored: per row, B convolved with the weighted residual; per coefficient,
            /// those combined across rows by A. Adds every inactive coefficient whose gradient exceeds its
            /// lambda to the active set; returns how many. Only the grid points whose gradient has changed since the
            /// last check are checked.
            /// </summary>
            /// <remarks>
            /// Four neighbouring grid points at a time, one per vector lane, each lane's sums in the order a single
            /// point's would be: the weighted residual is formed once, then slid under the peak.
            /// </remarks>
            private int AddViolators()
            {
                int groups = _groupPoints / WIDTH;
                Array.Clear(_groupCheck, 0, groups);
                bool any = false;
                for (int i = 0; i < _reachCount; i++)
                {
                    int q = _reach[i];
                    if (_changed[q] && (!_screenReady || _unscreened[q] > 0))
                    {
                        _groupCheck[q / WIDTH] = true;
                        any = true;
                    }
                }
                if (!any)
                    return 0;
                if (_screening)
                {
                    for (int i = 0; i < _reachCount; i++)
                        _unscreened[_reach[i]] = 0;
                }
                // The weighted residual of the used rows; B convolved with it, z[q] = sum_d B[d] w r[q + d], only
                // for the rows a column tested at a group needs, when it first does.
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples;
                    for (int x = o; x < o + _samples; x++)
                        _wr[x] = _weight[x] * _residual[x];
                }
                int added = 0;
                for (int m = 0; m < groups; m++)
                {
                    if (!_groupCheck[m])
                        continue;
                    int q0 = m * WIDTH;
                    _slideStamp++;
                    for (int j = 0; j < _columns; j++)
                    {
                        if (!_columnUsed[j])
                            continue;
                        // The lanes to test: points in reach, changed since their last check, inactive, curved.
                        int lanes = 0;
                        for (int l = 0; l < WIDTH && q0 + l < _points; l++)
                        {
                            int q = q0 + l, index = q * _columns + j;
                            if (_inReach[q] && _changed[q] && !_active[index] && _curvature[index] > 0 && !_screened[index])
                                lanes |= 1 << l;
                        }
                        if (lanes == 0)
                            continue;
                        for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        {
                            int r = _columnRow[k];
                            if (_rowSlid[r] == _slideStamp)
                                continue;
                            _rowSlid[r] = _slideStamp;
                            Slide(_b, _wr, r * _samples + q0, _zRow, r * _groupPoints + q0);
                        }
                        ColumnSum(_columnA, _zRow, j, q0);
                        for (int l = 0; l < WIDTH; l++)
                        {
                            if ((lanes & (1 << l)) == 0)
                                continue;
                            int q = q0 + l, index = q * _columns + j;
                            double g = _laneGradient[l];
                            // Worth a round only if the coefficient would move by more than a pass's tolerance.
                            double threshold = Math.Max(_lambda[index] * 1e-9 + 1e-12, _parameters.ToleranceIons * _curvature[index]);
                            if (_screening)
                            {
                                // Below the threshold by more than rounding: it can never pass it in this descent.
                                if (g - _lambda[index] <= threshold - 1e-9 * (Math.Abs(g) + _lambda[index]) - 1e-12)
                                    _screened[index] = true;
                                else
                                    _unscreened[q]++;
                            }
                            if (g - _lambda[index] > threshold)
                            {
                                _active[index] = true;
                                _activeCount[q]++;
                                _dirty[q] = true;
                                added++;
                            }
                        }
                    }
                    for (int l = 0; l < WIDTH && q0 + l < _points; l++)
                    {
                        if (_inReach[q0 + l])
                            _changed[q0 + l] = false;
                    }
                }
                return added;
            }

            /// <summary>
            /// A kernel slid over a row's values at four neighbouring grid points: into[oz + l] =
            /// sum_d kernel[d] values[o + l + d], each in ascending d.
            /// </summary>
            private void Slide(double[] kernel, double[] values, int o, double[] into, int oz)
            {
                int span = 2 * _half + 1;
                if (VECTORS)
                {
                    var sum = Vector256<double>.Zero;
                    for (int d = 0; d < span; d++)
                        sum += Vector256.Create(kernel[d]) * Vector256.Create(values, o + d);
                    sum.CopyTo(into, oz);
                    return;
                }
                for (int l = 0; l < WIDTH; l++)
                {
                    double sum = 0;
                    for (int d = 0; d < span; d++)
                        sum += kernel[d] * values[o + l + d];
                    into[oz + l] = sum;
                }
            }

            /// <summary>
            /// Column j's sum over its rows at four neighbouring grid points from q0 into _laneGradient:
            /// sum_k weight[k] rows[row_k, q0 + l], each in the column's row order.
            /// </summary>
            private void ColumnSum(double[] weight, double[] rows, int j, int q0)
            {
                if (VECTORS)
                {
                    var sum = Vector256<double>.Zero;
                    for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        sum += Vector256.Create(weight[k]) * Vector256.Create(rows, _columnRow[k] * _groupPoints + q0);
                    sum.CopyTo(_laneGradient);
                    return;
                }
                for (int l = 0; l < WIDTH; l++)
                {
                    double sum = 0;
                    for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        sum += weight[k] * rows[_columnRow[k] * _groupPoints + q0 + l];
                    _laneGradient[l] = sum;
                }
            }

            /// <summary>
            /// One pass of block coordinate descent, grid point by grid point: each point's active positions
            /// solved together exactly (non-negative, with their lasso) with every other coefficient held,
            /// since neighbouring positions' transmission columns are nearly collinear and single-coordinate
            /// steps separate them only slowly. Returns the largest change of a coefficient, in ions.
            /// </summary>
            private double Pass()
            {
                double largest = 0;
                _decrease = 0;
                _solvedCount = 0;
                for (int i = 0; i < _reachCount; i++)
                {
                    int q = _reach[i], oc = q * _columns, oq = q * _rows;
                    if (!_dirty[q])
                        continue;
                    _dirty[q] = false;
                    if (_activeCount[q] == 0)
                    {
                        _empty++;
                        continue;
                    }
                    int n = 0;
                    for (int j = 0; j < _columns; j++)
                    {
                        if (_active[oc + j] && _curvature[oc + j] > 0)
                            _blockColumns[n++] = j;
                    }
                    if (n == 0)
                    {
                        _empty++;
                        continue;
                    }
                    _solved[_solvedCount++] = q;
                    _blockSolves++;
                    _blockColumnCount += n;
                    double stretch;
                    long p0 = Now();
                    // A block's positions share about half their rows: each row's term once per block.
                    _stamp++;
                    for (int u = 0; u < n; u++)
                    {
                        int j = _blockColumns[u];
                        double g = 0;
                        for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        {
                            int r = _columnRow[k];
                            if (_rowStamp[r] != _stamp)
                            {
                                _rowGradient[r] = PeakGradient(r * _samples + q);
                                _rowStamp[r] = _stamp;
                            }
                            g += _columnA[k] * _rowGradient[r];
                        }
                        _blockGradient[u] = g;
                        _blockOld[u] = _beta[oc + j];
                    }
                    long p1 = Now();
                    _tGrad += p1 - p0;
                    long p2 = p1;
                    if (n == 1)
                    {
                        _singles++;
                        int index = oc + _blockColumns[0];
                        double c = _curvature[index];
                        _blockNew[0] = Math.Max(0, _blockOld[0] + (_blockGradient[0] - _lambda[index]) / c);
                        stretch = Relax(1);
                        double step = _blockNew[0] - _blockOld[0];
                        _decrease += (_blockGradient[0] - _lambda[index]) * step - 0.5 * c * step * step;
                    }
                    else
                    {
                        // Block Hessian H[u, v] = sum_r A_r,ju A_r,jv v[q, r]; minimize over the block
                        // 1/2 x^T H x - (g + H x_old - lambda)^T x, x >= 0.
                        for (int u = 0; u < n; u++)
                        {
                            int pair = _blockColumns[u] * _columns;
                            for (int w = u; w < n; w++)
                            {
                                int m1 = _pairStart[pair + _blockColumns[w] + 1];
                                double h = 0;
                                for (int m = _pairStart[pair + _blockColumns[w]]; m < m1; m++)
                                    h += _pairA[m] * _v[oq + _pairRow[m]];
                                _blockGram[u * n + w] = h;
                                _blockGram[w * n + u] = h;
                            }
                        }
                        for (int u = 0; u < n; u++)
                        {
                            double rhs = _blockGradient[u] - _lambda[oc + _blockColumns[u]];
                            for (int w = 0; w < n; w++)
                                rhs += _blockGram[u * n + w] * _blockOld[w];
                            _blockRhs[u] = rhs;
                        }
                        p2 = Now();
                        _tGram += p2 - p1;
                        NnlsSolver.SolveNormal(_blockGram, _blockRhs, n, _blockNew, _workspace, 0, _blockOld);
                        stretch = Relax(n);
                        // The decrease (g - lambda)^T d - d^T H d / 2 of the step d.
                        for (int u = 0; u < n; u++)
                        {
                            double du = _blockNew[u] - _blockOld[u];
                            if (du == 0)
                                continue;
                            double hd = 0;
                            for (int w = 0; w < n; w++)
                                hd += _blockGram[u * n + w] * (_blockNew[w] - _blockOld[w]);
                            _decrease += (_blockGradient[u] - _lambda[oc + _blockColumns[u]]) * du - 0.5 * du * hd;
                        }
                    }
                    long p3 = Now();
                    _tNnls += p3 - p2;
                    // The step's change to each row, summed over the block's positions, then one peak taken per row.
                    double moved = 0;
                    int rowsMoved = 0;
                    for (int u = 0; u < n; u++)
                    {
                        double delta = _blockNew[u] - _blockOld[u];
                        if (delta == 0)
                            continue;
                        int j = _blockColumns[u];
                        _beta[oc + j] = _blockNew[u];
                        for (int k = _columnStart[j]; k < _columnStart[j + 1]; k++)
                        {
                            int r = _columnRow[k];
                            if (!_rowIsMoved[r])
                            {
                                _rowIsMoved[r] = true;
                                _rowsMoved[rowsMoved++] = r;
                            }
                            _rowDelta[r] += _columnA[k] * delta;
                        }
                        moved = Math.Max(moved, Math.Abs(delta));
                    }
                    for (int t = 0; t < rowsMoved; t++)
                    {
                        int r = _rowsMoved[t];
                        SubtractPeak(r * _samples + q, _rowDelta[r]);
                        _rowDelta[r] = 0;
                        _rowIsMoved[r] = false;
                    }
                    largest = Math.Max(largest, moved);
                    _tUpdate += Now() - p3;
                    if (moved > _parameters.ToleranceIons)
                        _moves++;
                    // The grid points sharing this one's residual: their gradients changed by about as much
                    // as this step, so below the tolerance none can have come to violate by more. This point
                    // itself is at its optimum unless the step was lengthened past it.
                    if (moved <= _parameters.ToleranceIons)
                        continue;
                    bool overshot = (stretch - 1) / stretch * moved > _parameters.ToleranceIons;
                    int reach0 = Math.Max(0, q - 2 * _half), reach1 = Math.Min(_points - 1, q + 2 * _half);
                    for (int t = reach0; t <= reach1; t++)
                    {
                        _changed[t] = true;
                        if (t != q || overshot)
                            _dirty[t] = true;
                    }
                }
                return largest;
            }
        }
    }
}
