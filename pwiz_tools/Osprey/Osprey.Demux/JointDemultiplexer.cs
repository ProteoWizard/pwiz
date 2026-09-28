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

        /// <summary>The TOF peak's support: this many samples either side of its center.</summary>
        public int PeakHalfWidth { get; set; } = 5;

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

        /// <summary>The TOF peak's sigma at an m/z.</summary>
        public double SigmaAt(double mz)
        {
            var at = PeakSigmaMz;
            var sigma = PeakSigmaSamples;
            if (mz <= at[0])
                return sigma[0];
            for (int i = 1; i < at.Length; i++)
            {
                if (mz <= at[i])
                    return sigma[i - 1] + (sigma[i] - sigma[i - 1]) * (mz - at[i - 1]) / (at[i] - at[i - 1]);
            }
            return sigma[sigma.Length - 1];
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
                    solver.Solve(points, start, end, lo, hi, parameters.SigmaAt(centerMz));
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
        private sealed class ChunkSolver
        {
            private readonly double[,] _a;
            private readonly int _rows;
            private readonly int _columns;
            private readonly JointDemuxParams _parameters;
            private readonly double[] _b;          // the peak, offsets -h..h, unit area
            private readonly double[] _b2;
            private readonly int _half;
            private int _samples;                  // samples in the chunk: [lo - h, hi + h]
            private int _points;                   // grid points with coefficients: [lo, hi]
            private long _lo;
            private double[] _y = Array.Empty<double>();     // rows x samples
            private double[] _residual = Array.Empty<double>();
            private double[] _weight = Array.Empty<double>();
            private double[] _beta = Array.Empty<double>();  // columns x points
            private double[] _curvature = Array.Empty<double>();
            private double[] _lambda = Array.Empty<double>();
            private double[] _z = Array.Empty<double>();     // rows x points: B convolved with the weighted residual
            private double[] _v = Array.Empty<double>();     // rows x points: B^2 convolved with the weights
            private bool[] _active = Array.Empty<bool>();
            private readonly int[] _blockColumns;            // one grid point's active columns
            private readonly double[] _blockGradient;
            private readonly double[] _blockOld;
            private readonly double[] _blockNew;
            private readonly double[] _blockRhs;
            private readonly double[] _blockGram;
            private readonly NnlsSolver.Workspace _workspace;
            private readonly bool[] _rowSignal;
            private readonly bool[] _rowUsed;
            private readonly bool[] _columnUsed;
            private readonly List<int>[] _columnRows;        // per column: the used rows it transmits into

            public ChunkSolver(double[,] a, JointDemuxParams parameters)
            {
                _a = a;
                _rows = a.GetLength(0);
                _columns = a.GetLength(1);
                _parameters = parameters;
                _half = parameters.PeakHalfWidth;
                _b = new double[2 * _half + 1];
                _b2 = new double[2 * _half + 1];
                _rowSignal = new bool[_rows];
                _rowUsed = new bool[_rows];
                _columnUsed = new bool[_columns];
                _columnRows = new List<int>[_columns];
                for (int j = 0; j < _columns; j++)
                    _columnRows[j] = new List<int>();
                _blockColumns = new int[_columns];
                _blockGradient = new double[_columns];
                _blockOld = new double[_columns];
                _blockNew = new double[_columns];
                _blockRhs = new double[_columns];
                _blockGram = new double[_columns * _columns];
                _workspace = new NnlsSolver.Workspace(_columns);
            }

            /// <summary>Solves the coefficients of grid points [lo, hi] from points[start, end).</summary>
            public void Solve(List<(long Sample, int Row, double Ions)> points, int start, int end, long lo, long hi,
                double sigma)
            {
                _lo = lo;
                _points = (int)(hi - lo + 1);
                _samples = _points + 2 * _half;
                Allocate();
                SetPeak(sigma);

                Array.Clear(_y, 0, _rows * _samples);
                Array.Clear(_rowSignal, 0, _rows);
                long baseSample = lo - _half;
                for (int p = start; p < end; p++)
                {
                    int s = (int)(points[p].Sample - baseSample);
                    _y[points[p].Row * _samples + s] += points[p].Ions;
                    _rowSignal[points[p].Row] = true;
                }
                // The positions any signal-bearing row could come from, and the rows that see them.
                Array.Clear(_rowUsed, 0, _rows);
                for (int j = 0; j < _columns; j++)
                {
                    _columnUsed[j] = false;
                    for (int r = 0; r < _rows && !_columnUsed[j]; r++)
                        _columnUsed[j] = _rowSignal[r] && _a[r, j] > 0;
                    _columnRows[j].Clear();
                    if (!_columnUsed[j])
                        continue;
                    for (int r = 0; r < _rows; r++)
                    {
                        if (_a[r, j] > 0)
                        {
                            _columnRows[j].Add(r);
                            _rowUsed[r] = true;
                        }
                    }
                }

                Array.Clear(_beta, 0, _columns * _points);
                Array.Copy(_y, _residual, _rows * _samples);
                // 1. Poisson weights from the data smoothed by the peak, with the z-scaled lasso.
                FillWeights(false);
                Descend(_parameters.L1Z);
                // 2. Poisson weights from that solution's expected counts.
                if (_parameters.Reweight)
                {
                    FillWeights(true);
                    Descend(_parameters.L1Z);
                }
                // 3. Relaxed: the kept coefficients without the penalty.
                if (_parameters.Relaxed && _parameters.L1Z > 0)
                    Descend(0, false);
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
                        double beta = _beta[j * _points + q];
                        if (beta > 0)
                            kept.Add((j, _lo + q, beta));
                    }
                }
            }

            private void Allocate()
            {
                int rs = _rows * _samples, cp = _columns * _points, rp = _rows * _points;
                if (_y.Length < rs)
                {
                    _y = new double[rs];
                    _residual = new double[rs];
                    _weight = new double[rs];
                }
                if (_beta.Length < cp)
                {
                    _beta = new double[cp];
                    _curvature = new double[cp];
                    _lambda = new double[cp];
                    _active = new bool[cp];
                }
                if (_z.Length < rp)
                {
                    _z = new double[rp];
                    _v = new double[rp];
                }
            }

            private void SetPeak(double sigma)
            {
                double sum = 0;
                for (int d = -_half; d <= _half; d++)
                {
                    double v = Math.Exp(-0.5 * d * d / (sigma * sigma));
                    _b[d + _half] = v;
                    sum += v;
                }
                for (int d = 0; d < _b.Length; d++)
                {
                    _b[d] /= sum;
                    _b2[d] = _b[d] * _b[d];
                }
            }

            /// <summary>
            /// Poisson weights 1 / max(mu, floor), with mu the model's expected counts, y - residual, or before
            /// there is a model each row's counts smoothed by the peak; then each coefficient's curvature for
            /// the weights.
            /// </summary>
            private void FillWeights(bool fromModel)
            {
                double floor = _parameters.WeightFloorIons;
                for (int r = 0; r < _rows; r++)
                {
                    int o = r * _samples;
                    for (int s = 0; s < _samples; s++)
                    {
                        double mu;
                        if (fromModel)
                        {
                            mu = _y[o + s] - _residual[o + s];
                        }
                        else
                        {
                            mu = 0;
                            for (int d = -_half; d <= _half; d++)
                            {
                                int t = s + d;
                                if (t >= 0 && t < _samples)
                                    mu += _b[d + _half] * _y[o + t];
                            }
                        }
                        _weight[o + s] = 1 / Math.Max(mu, floor);
                    }
                }
                // v[r, q] = sum_d B[d]^2 w[r, q + d]; curvature[j, q] = sum_r A_rj^2 v[r, q].
                Array.Clear(_v, 0, _rows * _points);
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples, ov = r * _points;
                    for (int q = 0; q < _points; q++)
                    {
                        double v = 0;
                        for (int d = 0; d < _b2.Length; d++)
                            v += _b2[d] * _weight[o + q + d];
                        _v[ov + q] = v;
                    }
                }
                for (int j = 0; j < _columns; j++)
                {
                    if (!_columnUsed[j])
                        continue;
                    int oj = j * _points;
                    for (int q = 0; q < _points; q++)
                        _curvature[oj + q] = 0;
                    foreach (int r in _columnRows[j])
                    {
                        double a2 = _a[r, j] * _a[r, j];
                        int ov = r * _points;
                        for (int q = 0; q < _points; q++)
                            _curvature[oj + q] += a2 * _v[ov + q];
                    }
                }
            }

            /// <summary>
            /// Coordinate descent with a lasso of z standard deviations per coefficient (lambda = z sqrt of its
            /// curvature), on an active set grown from the full gradient until nothing violates the
            /// optimality conditions; or, when <paramref name="grow"/> is false, on the current support only.
            /// </summary>
            private void Descend(double z, bool grow = true)
            {
                int cp = _columns * _points;
                for (int i = 0; i < cp; i++)
                    _lambda[i] = z > 0 ? z * Math.Sqrt(_curvature[i]) : 0;
                for (int j = 0; j < _columns; j++)
                {
                    if (!_columnUsed[j])
                        continue;
                    int oj = j * _points;
                    for (int q = 0; q < _points; q++)
                    {
                        _active[oj + q] = _beta[oj + q] > 0;
                    }
                }
                for (int round = 0; round < _parameters.MaxRounds; round++)
                {
                    if (grow && AddViolators() == 0 && round > 0)
                        break;
                    double previous = Objective();
                    for (int pass = 0; pass < _parameters.MaxPasses; pass++)
                    {
                        double largest = Pass();
                        double current = Objective();
                        if (largest < _parameters.ToleranceIons || previous - current <= _parameters.RelativeTolerance * current)
                            break;
                        previous = current;
                    }
                    if (!grow)
                        break;
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
                    int oj = j * _points;
                    for (int q = 0; q < _points; q++)
                        sum += _lambda[oj + q] * _beta[oj + q];
                }
                return sum;
            }

            /// <summary>
            /// The full gradient, factored: per row, B convolved with the weighted residual; per coefficient,
            /// those combined across rows by A. Adds every inactive coefficient whose gradient exceeds its
            /// lambda to the active set, in (column, grid point) order; returns how many.
            /// </summary>
            private int AddViolators()
            {
                for (int r = 0; r < _rows; r++)
                {
                    if (!_rowUsed[r])
                        continue;
                    int o = r * _samples, oz = r * _points;
                    for (int q = 0; q < _points; q++)
                    {
                        double g = 0;
                        for (int d = 0; d < _b.Length; d++)
                            g += _b[d] * _weight[o + q + d] * _residual[o + q + d];
                        _z[oz + q] = g;
                    }
                }
                int added = 0;
                for (int j = 0; j < _columns; j++)
                {
                    if (!_columnUsed[j])
                        continue;
                    int oj = j * _points;
                    for (int q = 0; q < _points; q++)
                    {
                        if (_active[oj + q] || _curvature[oj + q] <= 0)
                            continue;
                        double g = 0;
                        foreach (int r in _columnRows[j])
                            g += _a[r, j] * _z[r * _points + q];
                        if (g > _lambda[oj + q] * (1 + 1e-9) + 1e-12)
                        {
                            _active[oj + q] = true;
                            added++;
                        }
                    }
                }
                return added;
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
                for (int q = 0; q < _points; q++)
                {
                    int n = 0;
                    for (int j = 0; j < _columns; j++)
                    {
                        if (_columnUsed[j] && _active[j * _points + q] && _curvature[j * _points + q] > 0)
                            _blockColumns[n++] = j;
                    }
                    if (n == 0)
                        continue;
                    for (int u = 0; u < n; u++)
                    {
                        int j = _blockColumns[u];
                        double g = 0;
                        foreach (int r in _columnRows[j])
                        {
                            int o = r * _samples + q;
                            double s = 0;
                            for (int d = 0; d < _b.Length; d++)
                                s += _b[d] * _weight[o + d] * _residual[o + d];
                            g += _a[r, j] * s;
                        }
                        _blockGradient[u] = g;
                        _blockOld[u] = _beta[j * _points + q];
                    }
                    if (n == 1)
                    {
                        int index = _blockColumns[0] * _points + q;
                        _blockNew[0] = Math.Max(0, _blockOld[0] + (_blockGradient[0] - _lambda[index]) / _curvature[index]);
                    }
                    else
                    {
                        // Block Hessian H[u, v] = sum_r A_r,ju A_r,jv v[r, q]; minimize over the block
                        // 1/2 x^T H x - (g + H x_old - lambda)^T x, x >= 0.
                        for (int u = 0; u < n; u++)
                        {
                            int ju = _blockColumns[u];
                            for (int w = u; w < n; w++)
                            {
                                int jw = _blockColumns[w];
                                double h = 0;
                                foreach (int r in _columnRows[ju])
                                {
                                    double aw = _a[r, jw];
                                    if (aw > 0)
                                        h += _a[r, ju] * aw * _v[r * _points + q];
                                }
                                _blockGram[u * n + w] = h;
                                _blockGram[w * n + u] = h;
                            }
                        }
                        for (int u = 0; u < n; u++)
                        {
                            double rhs = _blockGradient[u] - _lambda[_blockColumns[u] * _points + q];
                            for (int w = 0; w < n; w++)
                                rhs += _blockGram[u * n + w] * _blockOld[w];
                            _blockRhs[u] = rhs;
                        }
                        NnlsSolver.SolveNormal(_blockGram, _blockRhs, n, _blockNew, _workspace, 0, _blockOld);
                    }
                    for (int u = 0; u < n; u++)
                    {
                        double delta = _blockNew[u] - _blockOld[u];
                        if (delta == 0)
                            continue;
                        int j = _blockColumns[u];
                        _beta[j * _points + q] = _blockNew[u];
                        foreach (int r in _columnRows[j])
                        {
                            double ad = _a[r, j] * delta;
                            int o = r * _samples + q;
                            for (int d = 0; d < _b.Length; d++)
                                _residual[o + d] -= ad * _b[d];
                        }
                        largest = Math.Max(largest, Math.Abs(delta));
                    }
                }
                return largest;
            }
        }
    }
}
