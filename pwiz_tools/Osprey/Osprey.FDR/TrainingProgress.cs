/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.FDR
{
    /// <summary>The steps of one run's first-pass model training, in the order they run.</summary>
    internal enum TrainingStep
    {
        round_1,
        round_1_scoring,
        round_2,
        scoring,
        safeguard,
        calibration
    }

    /// <summary>
    /// The percent of one run's first-pass model training (<see cref="PerFileFirstPassModel"/>),
    /// fed from inside it into a <see cref="ProgressReporter"/>: the trainer opens its steps and its
    /// folds report each iteration, so the run's progress moves while it trains instead of sitting
    /// still for minutes (with <c>--parallel-files</c> every run of a wave trains at once, and the
    /// log looked hung).
    ///
    /// <para>Each step owns a slice of the 0..100, sized by its share of the wall time on SEA-AD
    /// (round 1 ~50 s, scoring every candidate by round 1 plus the protein-best column ~17 s,
    /// round 2 ~60 s, out-of-fold scoring ~11 s). A step that does not run (round 2 of a
    /// one-round training) leaves its slice behind when the next step opens.</para>
    ///
    /// <para>The fold loops stop on convergence, not at a known count, so a fold's fraction is its
    /// iterations done over those done plus the fewest still to come: the iterations it may run
    /// before its patience runs out, or its last iteration, whichever is sooner. Each iteration
    /// that does not improve brings the fold closer to 1 and an improving one holds it, so the
    /// percent never runs ahead of the work. A fold is done when it returns, whichever way its loop
    /// ended. Reporting only: it never changes what is trained.</para>
    /// </summary>
    internal sealed class TrainingProgress
    {
        // The percent at which each step ends, by TrainingStep.
        private static readonly int[] STEP_ENDS = { 35, 45, 85, 93, 98, 100 };

        // Resolution of the reporter, so a fraction of a step still moves a percent of a small slice.
        private const long UNITS = 10000;

        private readonly ProgressReporter _progress;
        private readonly object _lock = new object();
        private int _stepStart;
        private int _stepEnd;
        private double[] _foldFractions;

        /// <param name="progress">The reporter the training's percent goes to.</param>
        public TrainingProgress(ProgressReporter progress)
        {
            _progress = progress;
        }

        /// <summary>The reporter's total, in units of <see cref="Report"/>.</summary>
        public static long Total => UNITS;

        /// <summary>Opens <paramref name="step"/>: the percent moves to the end of the step before it.</summary>
        public void BeginStep(TrainingStep step)
        {
            lock (_lock)
            {
                int index = (int)step;
                _stepStart = index > 0 ? STEP_ENDS[index - 1] : 0;
                _stepEnd = STEP_ENDS[index];
                _foldFractions = null;
                Report(0);
            }
        }

        /// <summary>
        /// One fold iteration of the open step's cross-validation: <paramref name="iterationsDone"/>
        /// iterations of the fold have run, and it can still run at most
        /// <paramref name="maxIterations"/> of them, stopping once <paramref name="noImprove"/>
        /// reaches <paramref name="patience"/>.
        /// </summary>
        public void ReportFoldIteration(int fold, int nFolds, int iterationsDone, int noImprove, int patience,
            int maxIterations)
        {
            int remaining = Math.Max(0, Math.Min(patience - noImprove, maxIterations - iterationsDone));
            double fraction = iterationsDone + remaining > 0 ? (double)iterationsDone / (iterationsDone + remaining) : 1.0;
            lock (_lock)
            {
                SetFoldFraction(fold, nFolds, fraction);
            }
        }

        /// <summary>
        /// Fold <paramref name="fold"/> of the open step's cross-validation has returned: its share of
        /// the step is done, also when its loop stopped early (no positive training set).
        /// </summary>
        public void ReportFoldDone(int fold, int nFolds)
        {
            lock (_lock)
            {
                SetFoldFraction(fold, nFolds, 1.0);
            }
        }

        // Raise the fold's fraction and report the mean over folds. Caller holds _lock.
        private void SetFoldFraction(int fold, int nFolds, double fraction)
        {
            if (_foldFractions == null || _foldFractions.Length != nFolds)
                _foldFractions = new double[nFolds];
            _foldFractions[fold] = Math.Max(_foldFractions[fold], fraction);
            double sum = 0;
            foreach (double f in _foldFractions)
                sum += f;
            Report(sum / nFolds);
        }

        // The fraction of the open step done, as the reporter's units. Caller holds _lock.
        private void Report(double stepFraction)
        {
            double percent = _stepStart + (_stepEnd - _stepStart) * Math.Max(0.0, Math.Min(1.0, stepFraction));
            _progress.Report((long)(percent * UNITS / 100));
        }
    }
}
