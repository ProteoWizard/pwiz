/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (ilNormalize, shufflePreservingCterm, derivePepSeed, generateShuffledEntrapment,
 *   reversePreservingCterm, cyclePreservingCterm, generateReverseDecoy)
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
using System.Security.Cryptography;
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The sequence generators behind Carafe's entrapment FASTA: a seeded shuffle for the
    /// entrapment (p_target) peptide and Osprey's reverse-then-cycle rule for decoys, both
    /// keeping the C-terminal residue, and both optionally gated on I/L-normalized collisions
    /// and <see cref="DecoySimilarityGate"/>.
    /// </summary>
    public static class EntrapmentSequences
    {
        /// <summary>
        /// Shuffles tried before a peptide is declared to have no acceptable entrapment. A
        /// peptide with no permutational freedom (17 alanines) fails fast and is dropped.
        /// </summary>
        public const int MAX_ENTRAPMENT_SHUFFLE_ATTEMPTS = 20;

        /// <summary>Most internal-residue rotations tried for a decoy after the reversal.</summary>
        public const int MAX_DECOY_CYCLES = 10;

        /// <summary>
        /// The sequence with every I replaced by L. I and L are isobaric, so two sequences equal
        /// after this are indistinguishable by mass and fragment ladder.
        /// </summary>
        public static string IlNormalize(string sequence)
        {
            return sequence.IndexOf('I') < 0 ? sequence : sequence.Replace('I', 'L');
        }

        public static HashSet<string> IlNormalizedSet(IEnumerable<string> sequences)
        {
            var normalized = new HashSet<string>();
            foreach (string sequence in sequences)
                normalized.Add(IlNormalize(sequence));
            return normalized;
        }

        /// <summary>
        /// Fisher-Yates shuffle of every residue but the last, driven by a
        /// <see cref="JavaRandom"/> seeded from <see cref="DerivePeptideSeed"/>. Sequences of
        /// length 1 and 2 come back unchanged.
        /// </summary>
        public static string ShufflePreservingCterm(string sequence, long masterSeed, int attempt = 0)
        {
            if (sequence.Length <= 2)
                return sequence;
            char[] residues = sequence.ToCharArray();
            var rng = new JavaRandom(DerivePeptideSeed(masterSeed, sequence, attempt));
            for (int i = residues.Length - 2; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (residues[i], residues[j]) = (residues[j], residues[i]);
            }
            return new string(residues);
        }

        /// <summary>
        /// The first 8 bytes of SHA-1 of the UTF-8 text "masterSeed:sequence", big-endian, as a
        /// signed long. Retries append ":attempt", but attempt 0 deliberately keeps the two-part
        /// form so a peptide whose first shuffle passes gets the entrapment it had before the
        /// similarity gate existed, which is what lets an ungated build reproduce an old library
        /// byte for byte.
        /// </summary>
        public static long DerivePeptideSeed(long masterSeed, string sequence, int attempt)
        {
            string seedText = masterSeed.ToString(CultureInfo.InvariantCulture);
            string material = attempt == 0
                ? seedText + @":" + sequence
                : seedText + @":" + sequence + @":" + attempt.ToString(CultureInfo.InvariantCulture);
            byte[] digest = SHA1.HashData(Encoding.UTF8.GetBytes(material));
            long seed = 0;
            for (int i = 0; i < 8; i++)
                seed = (seed << 8) | digest[i];
            return seed;
        }

        /// <summary>
        /// An entrapment peptide made by shuffling the target: the first shuffle that differs
        /// from the target, collides with no real target and, when gated, passes
        /// <see cref="DecoySimilarityGate"/>. Ungated, only one shuffle is tried and a collision
        /// is an exact match. Null when there is no acceptable shuffle.
        /// </summary>
        /// <param name="sequence">The target peptide.</param>
        /// <param name="masterSeed">Carafe's <c>-entrapment_seed</c>.</param>
        /// <param name="targetSet">Every real target sequence, for the ungated exact check.</param>
        /// <param name="targetSetIl">The same set after <see cref="IlNormalize"/>, for the gated check.</param>
        /// <param name="gate">False reproduces the pre-gate behavior, for audit builds only.</param>
        public static string GenerateShuffledEntrapment(string sequence, long masterSeed,
            ISet<string> targetSet, ISet<string> targetSetIl, bool gate)
        {
            int attempts = gate ? MAX_ENTRAPMENT_SHUFFLE_ATTEMPTS : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                string candidate = ShufflePreservingCterm(sequence, masterSeed, attempt);
                if (IsAcceptable(sequence, candidate, targetSet, targetSetIl, gate))
                    return candidate;
            }
            return null;
        }

        /// <summary>Every residue but the last in reverse order (Osprey's <c>ReverseSequence</c>).</summary>
        public static string ReversePreservingCterm(string sequence)
        {
            int len = sequence.Length;
            if (len <= 2)
                return sequence;
            var sb = new StringBuilder(len);
            for (int i = len - 2; i >= 0; i--)
                sb.Append(sequence[i]);
            sb.Append(sequence[len - 1]);
            return sb.ToString();
        }

        /// <summary>
        /// Every residue but the last rotated left by <paramref name="cycleLength"/>, so ABCDEK
        /// becomes BCDEAK for 1 (Osprey's <c>CycleSequence</c>).
        /// </summary>
        public static string CyclePreservingCterm(string sequence, int cycleLength)
        {
            int len = sequence.Length;
            if (len <= 2 || cycleLength == 0)
                return sequence;
            // The len - 1 residues before the preserved C terminus.
            int middleLen = len - 1;
            int effectiveCycle = cycleLength % middleLen;
            var sb = new StringBuilder(len);
            for (int i = 0; i < middleLen; i++)
                sb.Append(sequence[(i + effectiveCycle) % middleLen]);
            sb.Append(sequence[len - 1]);
            return sb.ToString();
        }

        /// <summary>
        /// A decoy made Osprey's way: the reversal if it is acceptable, else the first acceptable
        /// rotation by 1..min(length, 10). Acceptable means different from the target, colliding
        /// with nothing in the target-side set and, when gated, passing the similarity gate.
        /// Null when nothing qualifies, and the caller then drops the pair.
        /// </summary>
        public static string GenerateReverseDecoy(string sequence, ISet<string> targetSet,
            ISet<string> targetSetIl, bool gate)
        {
            string reversed = ReversePreservingCterm(sequence);
            if (IsAcceptable(sequence, reversed, targetSet, targetSetIl, gate))
                return reversed;
            int maxRetries = Math.Min(sequence.Length, MAX_DECOY_CYCLES);
            for (int c = 1; c <= maxRetries; c++)
            {
                string cycled = CyclePreservingCterm(sequence, c);
                if (IsAcceptable(sequence, cycled, targetSet, targetSetIl, gate))
                    return cycled;
            }
            return null;
        }

        private static bool IsAcceptable(string sequence, string candidate, ISet<string> targetSet,
            ISet<string> targetSetIl, bool gate)
        {
            if (string.Equals(candidate, sequence, StringComparison.Ordinal))
                return false;
            if (gate)
            {
                // I/L-normalized comparison subsumes the exact one.
                return !targetSetIl.Contains(IlNormalize(candidate)) &&
                       DecoySimilarityGate.IsCandidateAcceptable(sequence, candidate);
            }
            return !targetSet.Contains(candidate);
        }
    }
}
