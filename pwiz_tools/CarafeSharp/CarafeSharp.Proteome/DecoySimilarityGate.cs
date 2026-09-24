/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/DecoySimilarityGate.java
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Rejects a candidate decoy or entrapment sequence whose theoretical singly charged b/y
    /// ladder coincides with its target's for more than <see cref="MAX_FRAGMENT_OVERLAP"/> of
    /// its ions, so the caller tries another candidate. The same rule as Osprey's
    /// <c>DecoyGenerator.IsCandidateAcceptable</c> and EncyclopeDIA's 0.4 threshold, on
    /// stripped sequences with a fixed 0.02 Da window.
    /// </summary>
    public static class DecoySimilarityGate
    {
        /// <summary>Largest acceptable fraction of the candidate's ladder matching the target's.</summary>
        public const double MAX_FRAGMENT_OVERLAP = 0.4;

        /// <summary>Fixed m/z window (Da) for counting ladder coincidences.</summary>
        public const double LADDER_MATCH_TOLERANCE = 0.02;

        /// <summary>True when <paramref name="candidateSequence"/> is far enough from its target to use.</summary>
        public static bool IsCandidateAcceptable(string targetSequence, string candidateSequence)
        {
            return FragmentOverlap(targetSequence, candidateSequence) <= MAX_FRAGMENT_OVERLAP;
        }

        /// <summary>
        /// Fraction of the candidate's ladder within <see cref="LADDER_MATCH_TOLERANCE"/> of any
        /// target ion, in [0, 1]; 0 for a candidate with no ladder.
        /// </summary>
        public static double FragmentOverlap(string targetSequence, string candidateSequence)
        {
            double[] targetLadder = TheoreticalLadder(targetSequence);
            double[] candidateLadder = TheoreticalLadder(candidateSequence);
            if (candidateLadder.Length == 0)
                return 0.0;
            Array.Sort(targetLadder);
            int matches = 0;
            foreach (double mz in candidateLadder)
            {
                if (MatchesWithinTolerance(targetLadder, mz))
                    matches++;
            }
            return (double)matches / candidateLadder.Length;
        }

        /// <summary>
        /// Singly charged b and y m/z for every cleavage site, b1 y1 b2 y2 and so on. Prefix sums
        /// give b and suffix sums give y, so an unknown residue skips only the ions that span it.
        /// </summary>
        public static double[] TheoreticalLadder(string sequence)
        {
            if (sequence == null || sequence.Length < 2)
                return Array.Empty<double>();
            int len = sequence.Length;
            var prefix = new double[len + 1];
            for (int i = 0; i < len; i++)
            {
                // NaN propagates on its own, as Carafe's explicit NaN check makes it.
                prefix[i + 1] = prefix[i] + ResidueMasses.Get(sequence[i]);
            }
            var suffix = new double[len + 1];
            for (int i = len - 1; i >= 0; i--)
                suffix[len - i] = suffix[len - i - 1] + ResidueMasses.Get(sequence[i]);

            var ladder = new double[(len - 1) * 2];
            int n = 0;
            for (int ordinal = 1; ordinal < len; ordinal++)
            {
                double bMass = prefix[ordinal];
                if (!double.IsNaN(bMass))
                    ladder[n++] = bMass + ResidueMasses.PROTON_MONO;
                double yMass = suffix[ordinal];
                if (!double.IsNaN(yMass))
                    ladder[n++] = yMass + ResidueMasses.H2O_MONO + ResidueMasses.PROTON_MONO;
            }
            if (n < ladder.Length)
                Array.Resize(ref ladder, n);
            return ladder;
        }

        private static bool MatchesWithinTolerance(double[] sortedLadder, double mz)
        {
            int idx = Array.BinarySearch(sortedLadder, mz);
            if (idx >= 0)
                return true;
            idx = ~idx;
            if (idx < sortedLadder.Length && sortedLadder[idx] - mz <= LADDER_MATCH_TOLERANCE)
                return true;
            return idx > 0 && mz - sortedLadder[idx - 1] <= LADDER_MATCH_TOLERANCE;
        }
    }
}
