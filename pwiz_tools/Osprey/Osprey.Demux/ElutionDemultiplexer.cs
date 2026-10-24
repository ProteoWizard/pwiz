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
using pwiz.Osprey.Centroid;

namespace pwiz.Osprey.Demux
{
    /// <summary>Settings of <see cref="ElutionDemultiplexer"/>.</summary>
    public sealed class ElutionDemuxParams
    {
        /// <summary>Full width at half maximum of a chromatographic peak, in seconds.</summary>
        public double FwhmSeconds { get; set; } = 5;

        /// <summary>Spacing of the candidate apex times, in peak sigmas.</summary>
        public double ApexStepSigmas { get; set; } = 0.5;

        /// <summary>How far a peak reaches each side of its apex, in peak sigmas.</summary>
        public double ReachSigmas { get; set; } = 3;

        /// <summary>
        /// The drop in weighted squared misfit (chi-square under Poisson weights) a peak must bring to be added. A
        /// peak fitted to noise alone brings about 1; 6 keeps one only on clear evidence.
        /// </summary>
        public double MinGain { get; set; } = 6;

        /// <summary>The most peaks one channel's fit holds in one block.</summary>
        public int MaxPeaks { get; set; } = 64;

        /// <summary>The weight floor, in ions, before any peak is fitted (the observations' own counts weight them).</summary>
        public double InitialFloorIons { get; set; } = 1;

        /// <summary>
        /// An observed peak whose window has no fitted signal at its time (a fragment too weak for any peak to clear
        /// <see cref="MinGain"/>) is shared equally among the spectrum's bins, as a pass-through peak is; false drops it.
        /// </summary>
        public bool ShareUnexplained { get; set; }

        /// <summary>Peak sigma, in minutes (the unit of spectrum times).</summary>
        public double SigmaMinutes
        {
            get { return FwhmSeconds / 60.0 / CentrixCalibration.FWHM_PER_SIGMA; }
        }

        /// <summary>The settings that change the output, for a log or a cache descriptor.</summary>
        public string Descriptor
        {
            get
            {
                var ic = CultureInfo.InvariantCulture;
                return string.Format(ic, @"elution_fwhm_s={0};apex_step_sigmas={1};reach_sigmas={2};min_gain={3};max_peaks={4};initial_floor_ions={5};share_unexplained={6}",
                    FwhmSeconds.ToString(@"R", ic), ApexStepSigmas.ToString(@"R", ic), ReachSigmas.ToString(@"R", ic),
                    MinGain.ToString(@"R", ic), MaxPeaks, InitialFloorIons.ToString(@"R", ic), ShareUnexplained);
            }
        }
    }

    /// <summary>
    /// Staggered demultiplexing at the acquisition's own sampling rate. Every narrow bin is observed once per pass
    /// through some window, but each window comes back only every k passes, and on a short gradient a chromatographic
    /// peak spans little more than one pass. So instead of interpolating each window's own series to a spectrum's time,
    /// each fragment channel's signal in each bin is fitted as a few chromatographic peaks of known width, from every
    /// observation of the block at once: a source in bin b at apex t shows in the windows covering b at the passes
    /// around t and is absent from the windows that do not cover b. Each observed peak is then apportioned among its
    /// spectrum's bins by the fitted values at its own time, so each bin keeps one measured value per pass.
    /// </summary>
    /// <remarks>
    /// <para>The fit is sparse and greedy: candidate peaks are every bin a window with signal covers, at apex times
    /// every <see cref="ElutionDemuxParams.ApexStepSigmas"/> sigmas near that signal. The candidate that best explains
    /// the residual under Poisson weights is added, the amplitudes of all added peaks are refitted by weighted NNLS, and
    /// the weights are taken again from the fit (1 / max(fit, floor)), until a peak no longer lowers the weighted misfit
    /// by <see cref="ElutionDemuxParams.MinGain"/>. Zeros count: a window that covers a candidate bin and saw nothing
    /// argues against it.</para>
    /// <para>Deterministic: every loop runs in index order, and ties go to the lowest candidate.</para>
    /// </remarks>
    public static class ElutionDemultiplexer
    {
        /// <summary>
        /// Demultiplexes one block of a staggered acquisition: each output spectrum's observed peaks apportioned among its
        /// window's bins by each channel's fitted elution at the spectrum's time.
        /// </summary>
        public static ScanningUnitResult DemuxUnit(InterpolatedUnit unit, ScanningDemuxParams parameters,
            ElutionDemuxParams elution)
        {
            var result = new ScanningUnitResult();
            int peaks = unit.Mz.Length;
            int rows = unit.RowTimes.Length, outputs = unit.OutputRow.Length, columns = unit.ColumnBins.Length;
            ScanningDemultiplexer.OutputOwnership(unit, out var keep, out var ownOutput);

            var channel = new int[peaks];
            result.Channels = ScanningDemultiplexer.FindChannels(unit.Mz, unit.Ions, parameters, channel);
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
            var observed = new List<int>[outputs];  // the channel's peaks in each output spectrum
            for (int s = 0; s < outputs; s++)
                observed[s] = new List<int>();
            var observedOutputs = new List<int>();
            var fit = new ChannelFit(unit, elution, parameters.WeightFloorIons);
            var x = new double[columns];
            var cols = new int[columns];
            int k = 0;
            while (k < peaks)
            {
                int ch = (int)(order[k] / peaks) - 1;
                int end = k;
                while (end < peaks && (int)(order[end] / peaks) - 1 == ch)
                    end++;

                double ions = 0;
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
                    if (ScanningDemultiplexer.OwnsPassThrough(unit, ownOutput, i))
                        result.IonsIn += unit.Ions[i];
                }

                bool strong = ch >= 0 && ions >= parameters.MinChannelIons && cells >= parameters.MinChannelCells;
                if (!strong)
                {
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        if (!ScanningDemultiplexer.OwnsPassThrough(unit, ownOutput, i))
                            continue;
                        result.PassedThrough.Add(new ScanningPeak(-1, unit.RowSpectra[unit.Row[i]][unit.Acquisition[i]],
                            unit.Mz[i], unit.Ions[i]));
                        result.IonsPassedThrough += unit.Ions[i];
                    }
                }
                else
                {
                    for (int m = k; m < end; m++)
                    {
                        int i = (int)(order[m] % peaks);
                        int s = outputOf[unit.Row[i]][unit.Acquisition[i]];
                        if (s < 0)
                            continue;
                        if (observed[s].Count == 0)
                            observedOutputs.Add(s);
                        observed[s].Add(i);
                    }
                    fit.Solve(series, rowHasSignal);
                    observedOutputs.Sort(); // Array.Sort OK: distinct output indices, no ties
                    foreach (int s in observedOutputs)
                    {
                        int nc = fit.Evaluate(unit.OutputRow[s], unit.OutputTime[s], x, cols);
                        double total = 0;
                        for (int jj = 0; jj < nc; jj++)
                            total += unit.Transmission[unit.OutputRow[s], cols[jj]] * x[jj];
                        if (total > 0)
                            ScanningDemultiplexer.Apportion(unit, keep, observed[s], s, nc, x, cols, result.Demultiplexed);
                        else
                            Unexplained(unit, keep, ownOutput, observed[s], s, elution.ShareUnexplained, result);
                        observed[s].Clear();
                    }
                    observedOutputs.Clear();
                    result.ChannelsSolved++;
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
        /// Observed peaks of output spectrum <paramref name="s"/> that no fitted signal explains: counted once (by the
        /// unit that owns the spectrum's pass-through) and, when sharing, split equally among its window's bins.
        /// </summary>
        private static void Unexplained(InterpolatedUnit unit, bool[] keep, Dictionary<int, bool> ownOutput,
            List<int> observed, int s, bool share, ScanningUnitResult result)
        {
            int r = unit.OutputRow[s], columns = unit.ColumnBins.Length;
            if (ownOutput.TryGetValue(unit.OutputSpectrum[s], out bool own) && own)
            {
                foreach (int i in observed)
                    result.IonsUnexplained += unit.Ions[i];
            }
            if (!share)
                return;
            int bins = 0;
            for (int j = 0; j < columns; j++)
            {
                if (unit.Transmission[r, j] >= 0.5)
                    bins++;
            }
            foreach (int i in observed)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (keep[s * columns + j])
                        result.Demultiplexed.Add(new ScanningPeak(unit.ColumnBins[j], unit.OutputSpectrum[s], unit.Mz[i], unit.Ions[i] / bins));
                }
            }
        }

        /// <summary>
        /// One channel's elution fit within a unit, reusing its buffers from channel to channel (one per unit, so not
        /// shared across threads).
        /// </summary>
        private sealed class ChannelFit
        {
            private readonly InterpolatedUnit _unit;
            private readonly ElutionDemuxParams _params;
            private readonly double _floor;
            private readonly double _sigma;
            private readonly double _reach;
            private readonly double _step;

            // Observations: every acquisition of the rows the candidate bins touch.
            private readonly List<int> _obsRow = new List<int>();
            private readonly List<double> _obsTime = new List<double>();
            private readonly List<double> _obsY = new List<double>();
            private readonly int[][] _obsOf;   // observation index of each row's acquisitions, or -1

            // Candidate peaks: a column, an apex time, and the observations it reaches with their values.
            private readonly List<int> _atomColumn = new List<int>();
            private readonly List<double> _atomTau = new List<double>();
            private readonly List<int> _atomStart = new List<int>();
            private readonly List<int> _footObs = new List<int>();
            private readonly List<double> _footValue = new List<double>();

            // The fit.
            private readonly List<int> _chosen = new List<int>();
            private double[] _beta = new double[0];

            public ChannelFit(InterpolatedUnit unit, ElutionDemuxParams parameters, double floorIons)
            {
                _unit = unit;
                _params = parameters;
                _floor = floorIons;
                _sigma = parameters.SigmaMinutes;
                _reach = parameters.ReachSigmas * _sigma;
                _step = parameters.ApexStepSigmas * _sigma;
                _obsOf = new int[unit.RowTimes.Length][];
                for (int r = 0; r < _obsOf.Length; r++)
                    _obsOf[r] = new int[unit.RowTimes[r].Length];
            }

            /// <summary>Fits the channel whose per-acquisition ions are <paramref name="series"/>.</summary>
            public void Solve(double[][] series, bool[] rowHasSignal)
            {
                var a = _unit.Transmission;
                int rows = a.GetLength(0), columns = a.GetLength(1);
                Clear();

                // Candidate columns: those a row with signal covers. Observations: every acquisition of the rows that
                // cover a candidate column, zeros included.
                var candidate = new bool[columns];
                for (int r = 0; r < rows; r++)
                {
                    if (!rowHasSignal[r])
                        continue;
                    for (int j = 0; j < columns; j++)
                    {
                        if (a[r, j] > 0)
                            candidate[j] = true;
                    }
                }
                for (int r = 0; r < rows; r++)
                {
                    bool used = false;
                    for (int j = 0; j < columns && !used; j++)
                        used = candidate[j] && a[r, j] > 0;
                    var times = _unit.RowTimes[r];
                    for (int q = 0; q < times.Length; q++)
                    {
                        _obsOf[r][q] = used ? _obsRow.Count : -1;
                        if (!used)
                            continue;
                        _obsRow.Add(r);
                        _obsTime.Add(times[q]);
                        _obsY.Add(series[r][q]);
                    }
                }
                int observations = _obsY.Count;
                if (observations == 0)
                    return;

                // Candidate peaks: each candidate column, at apex times on a fixed grid within reach of an observation
                // with signal in a row covering it.
                double first = double.MaxValue, last = double.MinValue;
                for (int o = 0; o < observations; o++)
                {
                    first = Math.Min(first, _obsTime[o]);
                    last = Math.Max(last, _obsTime[o]);
                }
                double origin = Math.Floor((first - _reach) / _step) * _step;
                int steps = (int)Math.Ceiling((last + _reach - origin) / _step) + 1;
                var near = new bool[steps];
                for (int j = 0; j < columns; j++)
                {
                    if (!candidate[j])
                        continue;
                    Array.Clear(near, 0, steps);
                    for (int r = 0; r < rows; r++)
                    {
                        if (a[r, j] <= 0 || !rowHasSignal[r])
                            continue;
                        var times = _unit.RowTimes[r];
                        for (int q = 0; q < times.Length; q++)
                        {
                            if (series[r][q] <= 0)
                                continue;
                            int lo = Math.Max(0, (int)Math.Ceiling((times[q] - _reach - origin) / _step));
                            int hi = Math.Min(steps - 1, (int)Math.Floor((times[q] + _reach - origin) / _step));
                            for (int m = lo; m <= hi; m++)
                                near[m] = true;
                        }
                    }
                    for (int m = 0; m < steps; m++)
                    {
                        if (near[m])
                            AddAtom(j, origin + m * _step);
                    }
                }
                Fit();
            }

            /// <summary>
            /// The fitted signal at <paramref name="time"/> of each column <paramref name="row"/> covers that has any:
            /// values in <paramref name="x"/>, their columns in <paramref name="cols"/>. Returns how many.
            /// </summary>
            public int Evaluate(int row, double time, double[] x, int[] cols)
            {
                var a = _unit.Transmission;
                int nc = 0;
                for (int c = 0; c < _chosen.Count; c++)
                {
                    if (_beta[c] <= 0)
                        continue;
                    int atom = _chosen[c];
                    int j = _atomColumn[atom];
                    if (a[row, j] <= 0)
                        continue;
                    double d = (time - _atomTau[atom]) / _sigma;
                    double value = _beta[c] * Math.Exp(-0.5 * d * d);
                    int slot = 0;
                    while (slot < nc && cols[slot] != j)
                        slot++;
                    if (slot == nc)
                    {
                        cols[nc] = j;
                        x[nc] = 0;
                        nc++;
                    }
                    x[slot] += value;
                }
                return nc;
            }

            private void Clear()
            {
                _obsRow.Clear();
                _obsTime.Clear();
                _obsY.Clear();
                _atomColumn.Clear();
                _atomTau.Clear();
                _atomStart.Clear();
                _footObs.Clear();
                _footValue.Clear();
                _chosen.Clear();
                _beta = new double[0];
            }

            private void AddAtom(int column, double tau)
            {
                var a = _unit.Transmission;
                int start = _footObs.Count;
                for (int r = 0; r < a.GetLength(0); r++)
                {
                    double share = a[r, column];
                    if (share <= 0)
                        continue;
                    var times = _unit.RowTimes[r];
                    int q = LowerBound(times, tau - _reach);
                    for (; q < times.Length && times[q] <= tau + _reach; q++)
                    {
                        double d = (times[q] - tau) / _sigma;
                        _footObs.Add(_obsOf[r][q]);
                        _footValue.Add(share * Math.Exp(-0.5 * d * d));
                    }
                }
                if (_footObs.Count == start)
                    return;
                _atomColumn.Add(column);
                _atomTau.Add(tau);
                _atomStart.Add(start);
            }

            /// <summary>Greedy sparse fit: add the best candidate, refit the amplitudes it interacts with, reweight.</summary>
            /// <remarks>
            /// <para>Peaks that reach no common observation do not interact: the weighted Gram matrix is block diagonal
            /// over groups of peaks linked by shared observations, and the NNLS solution of each group is its own. So
            /// adding a peak refits only the group it joins (the groups of every chosen peak it shares an observation
            /// with, merged), warm-started from their amplitudes, and the misfit, the fit and the weights change only
            /// over that group's observations.</para>
            /// <para>A candidate's score depends only on the observations it reaches. After the first peak is added every
            /// weight changes (from the data's counts to the fit's), so every candidate is scored again; after that
            /// only the candidates reaching the refitted group's observations are.</para>
            /// </remarks>
            private void Fit()
            {
                int observations = _obsY.Count, atoms = _atomColumn.Count;
                if (atoms == 0)
                    return;
                var footObs = _footObs.ToArray();
                var footValue = _footValue.ToArray();
                var atomStart = new int[atoms + 1];
                for (int atom = 0; atom < atoms; atom++)
                    atomStart[atom] = _atomStart[atom];
                atomStart[atoms] = footObs.Length;
                var y = _obsY.ToArray();

                // The candidates reaching each observation.
                var obsStart = new int[observations + 1];
                foreach (int o in footObs)
                    obsStart[o + 1]++;
                for (int o = 0; o < observations; o++)
                    obsStart[o + 1] += obsStart[o];
                var obsAtoms = new int[footObs.Length];
                var fill = new int[observations];
                for (int atom = 0; atom < atoms; atom++)
                {
                    for (int f = atomStart[atom]; f < atomStart[atom + 1]; f++)
                    {
                        int o = footObs[f];
                        obsAtoms[obsStart[o] + fill[o]++] = atom;
                    }
                }

                // Zero observations count, so the floor keeps their weights finite even when set to zero.
                var w = new double[observations];
                var mu = new double[observations];
                double initialFloor = Math.Max(_params.InitialFloorIons, _floor);
                for (int o = 0; o < observations; o++)
                    w[o] = 1 / Math.Max(y[o], initialFloor);
                var score = new double[atoms];
                for (int atom = 0; atom < atoms; atom++)
                    score[atom] = Score(atom, atomStart, footObs, footValue, y, w, mu);

                // The chosen peaks, their amplitudes and groups.
                var isChosen = new bool[atoms];
                var beta = new double[atoms];
                var group = new int[atoms];
                var chosen = new List<int>();
                var inTrial = new bool[atoms];
                var trial = new List<int>();
                var trialObs = new List<int>();
                var obsMark = new bool[observations];
                var dense = new double[observations];
                var stale = new bool[atoms];
                int maxPeaks = Math.Min(_params.MaxPeaks, atoms);
                var gram = new double[maxPeaks * maxPeaks];
                var atb = new double[maxPeaks];
                var start = new double[maxPeaks];
                var solution = new double[maxPeaks];
                var workspace = new NnlsSolver.Workspace(maxPeaks);
                var newMu = new double[observations];

                // The weighted NNLS of a group of candidates, warm-started from their amplitudes, into solution.
                void SolveGroup(List<int> members)
                {
                    int count = members.Count;
                    for (int c1 = 0; c1 < count; c1++)
                    {
                        int a1 = members[c1];
                        start[c1] = isChosen[a1] ? beta[a1] : 0;
                        for (int f = atomStart[a1]; f < atomStart[a1 + 1]; f++)
                            dense[footObs[f]] = footValue[f];
                        double b = 0;
                        for (int f = atomStart[a1]; f < atomStart[a1 + 1]; f++)
                            b += w[footObs[f]] * footValue[f] * y[footObs[f]];
                        atb[c1] = b;
                        for (int c2 = 0; c2 < count; c2++)
                        {
                            int a2 = members[c2];
                            double sum = 0;
                            for (int f = atomStart[a2]; f < atomStart[a2 + 1]; f++)
                                sum += w[footObs[f]] * dense[footObs[f]] * footValue[f];
                            gram[c1 * count + c2] = sum;
                        }
                        for (int f = atomStart[a1]; f < atomStart[a1 + 1]; f++)
                            dense[footObs[f]] = 0;
                    }
                    NnlsSolver.SolveNormal(gram, atb, count, solution, workspace, 0, start);
                }

                while (chosen.Count < maxPeaks)
                {
                    int best = -1;
                    double bestScore = 0;
                    for (int atom = 0; atom < atoms; atom++)
                    {
                        if (!isChosen[atom] && score[atom] > bestScore)
                        {
                            bestScore = score[atom];
                            best = atom;
                        }
                    }
                    if (best < 0)
                        break;

                    // The trial group: the best candidate and every chosen peak in a group it shares an observation
                    // with, in index order.
                    trial.Clear();
                    for (int f = atomStart[best]; f < atomStart[best + 1]; f++)
                    {
                        int o = footObs[f];
                        for (int k = obsStart[o]; k < obsStart[o + 1]; k++)
                        {
                            int other = obsAtoms[k];
                            if (!isChosen[other] || inTrial[other])
                                continue;
                            foreach (int member in chosen)
                            {
                                if (group[member] == group[other] && !inTrial[member])
                                {
                                    inTrial[member] = true;
                                    trial.Add(member);
                                }
                            }
                        }
                    }
                    trial.Add(best);
                    inTrial[best] = true;
                    trial.Sort(); // Array.Sort OK: distinct candidate indices, no ties
                    int n = trial.Count;
                    if (n > maxPeaks)
                    {
                        foreach (int atom in trial)
                            inTrial[atom] = false;
                        break;
                    }

                    // The group's observations, and the fit there without the group.
                    trialObs.Clear();
                    foreach (int atom in trial)
                    {
                        for (int f = atomStart[atom]; f < atomStart[atom + 1]; f++)
                        {
                            int o = footObs[f];
                            if (obsMark[o])
                                continue;
                            obsMark[o] = true;
                            trialObs.Add(footObs[f]);
                        }
                    }
                    trialObs.Sort(); // Array.Sort OK: distinct observation indices, no ties

                    // Weighted NNLS of the group, warm-started from its amplitudes.
                    SolveGroup(trial);

                    // The group's new fit over its observations: the rest of the fit there is the same.
                    foreach (int o in trialObs)
                        newMu[o] = mu[o];
                    for (int c = 0; c < n; c++)
                    {
                        int atom = trial[c];
                        double delta = solution[c] - (isChosen[atom] ? beta[atom] : 0);
                        for (int f = atomStart[atom]; f < atomStart[atom + 1]; f++)
                            newMu[footObs[f]] += delta * footValue[f];
                    }
                    double gain = 0;
                    foreach (int o in trialObs)
                    {
                        double before = y[o] - mu[o], after = y[o] - newMu[o];
                        gain += w[o] * (before * before - after * after);
                    }
                    foreach (int o in trialObs)
                        obsMark[o] = false;
                    foreach (int atom in trial)
                        inTrial[atom] = false;
                    if (gain < _params.MinGain)
                        break;

                    // Accept: the group's amplitudes and one group id, its observations' fit and weights.
                    isChosen[best] = true;
                    chosen.Add(best);
                    for (int c = 0; c < n; c++)
                    {
                        int atom = trial[c];
                        beta[atom] = solution[c];
                        group[atom] = best;
                    }
                    if (chosen.Count == 1)
                    {
                        foreach (int o in trialObs)
                            mu[o] = newMu[o];
                        for (int o = 0; o < observations; o++)
                            w[o] = 1 / Math.Max(mu[o], _floor);
                        for (int atom = 0; atom < atoms; atom++)
                            score[atom] = Score(atom, atomStart, footObs, footValue, y, w, mu);
                        continue;
                    }
                    foreach (int o in trialObs)
                    {
                        mu[o] = newMu[o];
                        w[o] = 1 / Math.Max(mu[o], _floor);
                        for (int k = obsStart[o]; k < obsStart[o + 1]; k++)
                            stale[obsAtoms[k]] = true;
                    }
                    for (int atom = 0; atom < atoms; atom++)
                    {
                        if (!stale[atom])
                            continue;
                        stale[atom] = false;
                        score[atom] = Score(atom, atomStart, footObs, footValue, y, w, mu);
                    }
                }
                // One more refit of each group under the final weights: a group the later peaks did not join was last
                // fitted under the weights before its own reweighting.
                chosen.Sort(); // Array.Sort OK: distinct candidate indices, no ties
                var done = new HashSet<int>();
                foreach (int atom in chosen)
                {
                    if (!done.Add(group[atom]))
                        continue;
                    trial.Clear();
                    foreach (int member in chosen)
                    {
                        if (group[member] == group[atom])
                            trial.Add(member);
                    }
                    SolveGroup(trial);
                    for (int c = 0; c < trial.Count; c++)
                        beta[trial[c]] = solution[c];
                }
                _chosen.AddRange(chosen);
                _beta = new double[chosen.Count];
                for (int c = 0; c < chosen.Count; c++)
                    _beta[c] = beta[chosen[c]];
            }

            /// <summary>A candidate's correlation with the weighted residual over its weighted norm; 0 without reach.</summary>
            private static double Score(int atom, int[] atomStart, int[] footObs, double[] footValue, double[] y, double[] w,
                double[] mu)
            {
                double numerator = 0, norm = 0;
                for (int f = atomStart[atom]; f < atomStart[atom + 1]; f++)
                {
                    int o = footObs[f];
                    double g = footValue[f];
                    numerator += w[o] * (y[o] - mu[o]) * g;
                    norm += w[o] * g * g;
                }
                return norm > 0 ? numerator / Math.Sqrt(norm) : 0;
            }

            private static int LowerBound(double[] values, double target)
            {
                int lo = 0, hi = values.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (values[mid] < target)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                return lo;
            }
        }
    }
}
