/*
 * Original author: Michael J. MacCoss <maccoss .at. uw.edu>,
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// The candidate peaks the pick chose among, with each one's mean pairwise
    /// fragment coelution and pick score, PUBLISHED BY THE HARNESS from
    /// <c>ScoreCandidate</c> (only the extractor holds them). The runner-up features
    /// read it: how far the chosen peak stands above the alternatives in its own
    /// window, which a single apex score cannot show. On the boundary-override path,
    /// which scores a peak someone else chose, the candidates are the ones the first pass
    /// would have chosen among, with the imposed peak in place of any whose apex it covers
    /// (<c>PeakDataExtractor.OverrideRunnerUpCandidates</c>); there the chosen peak need not
    /// be the best, and its margins can be negative.
    /// </summary>
    public sealed class PeakCandidatesByproduct
    {
        /// <summary>Every candidate that passed apex acceptance, the chosen one included.</summary>
        public IReadOnlyList<(XICPeakBounds peak, double coelutionScore, double rankScore)> Candidates { get; }

        /// <summary>The chosen peak, one of <see cref="Candidates"/>.</summary>
        public XICPeakBounds ChosenPeak { get; }

        public PeakCandidatesByproduct(IReadOnlyList<(XICPeakBounds peak, double coelutionScore, double rankScore)> candidates,
            XICPeakBounds chosenPeak)
        {
            Candidates = candidates;
            ChosenPeak = chosenPeak;
        }

        /// <summary>
        /// The chosen candidate's value and the best value among the others, through
        /// <paramref name="value"/>; false when the chosen peak is not in the list. <paramref name="runnerUp"/> is null when the chosen peak was
        /// the only candidate.
        /// </summary>
        public bool TryGetChosenAndRunnerUp(Func<(XICPeakBounds peak, double coelutionScore, double rankScore), double> value,
            out double chosen, out double? runnerUp)
        {
            chosen = 0.0;
            runnerUp = null;
            bool found = false;
            foreach (var candidate in Candidates)
            {
                double v = value(candidate);
                // By bounds: the extractor recomputes the chosen peak's apex after the pick.
                if (!found && candidate.peak.StartIndex == ChosenPeak.StartIndex &&
                    candidate.peak.EndIndex == ChosenPeak.EndIndex)
                {
                    chosen = v;
                    found = true;
                }
                else if (!runnerUp.HasValue || v > runnerUp.Value)
                {
                    runnerUp = v;
                }
            }
            return found;
        }
    }

    /// <summary>
    /// peak_coelution_margin: the chosen peak's mean pairwise fragment coelution minus
    /// the best of the other candidates in its window, the others floored at 0 (a lone
    /// candidate competes with uncorrelated noise). A weak true peak that clearly beats
    /// every alternative scores high; a decoy among several similar noise peaks scores
    /// near 0. Can be negative when the pick preferred a less coeluting peak on RT or
    /// intensity.
    /// </summary>
    internal sealed class PeakCoelutionMarginCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_coelution_margin"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakCoelutionMarginCalc_DisplayName_Coelution_margin_over_runner_up_peak; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            if (!context.TryGetInfo(out PeakCandidatesByproduct peaks) ||
                !peaks.TryGetChosenAndRunnerUp(c => c.coelutionScore, out double chosen, out double? runnerUp))
            {
                return 0.0;
            }
            double other = runnerUp.HasValue && runnerUp.Value > 0.0 ? runnerUp.Value : 0.0;
            return chosen - other;
        }
    }

    /// <summary>
    /// peak_rank_margin: the chosen peak's pick score minus the runner-up's, 0 when the
    /// chosen peak was the only candidate (peak_candidate_count tells the two apart).
    /// The pick score is the learned linear pick model's by default, the product form
    /// under OSPREY_PICK_LDA=0.
    /// </summary>
    internal sealed class PeakRankMarginCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_rank_margin"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakRankMarginCalc_DisplayName_Pick_score_margin_over_runner_up_peak; } }

        public override bool IsReversedScore { get { return false; } }   // higher is better

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            if (!context.TryGetInfo(out PeakCandidatesByproduct peaks) ||
                !peaks.TryGetChosenAndRunnerUp(c => c.rankScore, out double chosen, out double? runnerUp) ||
                !runnerUp.HasValue)
            {
                return 0.0;
            }
            return chosen - runnerUp.Value;
        }
    }

    /// <summary>
    /// peak_candidate_count: how many candidate peaks passed apex acceptance in the
    /// window (on the boundary-override path, the imposed peak plus the first-pass
    /// candidates it does not cover). Many similar candidates mean the
    /// window holds no single convincing peak.
    /// </summary>
    internal sealed class PeakCandidateCountCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"peak_candidate_count"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeakCandidateCountCalc_DisplayName_Candidate_peaks_in_window; } }

        public override bool IsReversedScore { get { return true; } }   // fewer is better

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            return context.TryGetInfo(out PeakCandidatesByproduct peaks) ? peaks.Candidates.Count : 1.0;
        }
    }

    /// <summary>
    /// context_peptide_length: residues in the candidate's sequence. Context for the
    /// classifier, as Percolator and DIA-NN use it: how reliable a match is depends on
    /// what is being matched. A decoy has its target's length, so it carries no
    /// target/decoy information of its own.
    /// </summary>
    internal sealed class PeptideLengthCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"context_peptide_length"; } }

        public override string DisplayName { get { return OspreyScoringResources.PeptideLengthCalc_DisplayName_Peptide_length; } }

        public override bool IsReversedScore { get { return false; } }   // no expected direction

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            return peakData.Candidate.Sequence?.Length ?? 0;
        }
    }

    /// <summary>context_precursor_charge: the candidate's precursor charge, context shared with its decoy.</summary>
    internal sealed class PrecursorChargeCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"context_precursor_charge"; } }

        public override string DisplayName { get { return OspreyScoringResources.PrecursorChargeCalc_DisplayName_Precursor_charge; } }

        public override bool IsReversedScore { get { return false; } }   // no expected direction

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            return peakData.Candidate.Charge;
        }
    }

    /// <summary>context_precursor_mz: the candidate's precursor m/z, context shared with its decoy.</summary>
    internal sealed class PrecursorMzCalc : SummaryOspreyFeatureCalculator
    {
        public override string Name { get { return @"context_precursor_mz"; } }

        public override string DisplayName { get { return OspreyScoringResources.PrecursorMzCalc_DisplayName_Precursor_m_z; } }

        public override bool IsReversedScore { get { return false; } }   // no expected direction

        public override double Calculate(OspreyScoringContext context, IOspreySummaryPeakData peakData)
        {
            return peakData.Candidate.PrecursorMz;
        }
    }
}
