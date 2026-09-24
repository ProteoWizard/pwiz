/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/ForeignEntrapmentSource.java
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
using System.IO;
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A pool of foreign-species peptides to use as entrapment instead of shuffling each
    /// target. A shuffle is an anagram of its target and shares many fragment masses, so it is
    /// over-identified; a real peptide from a distant proteome has no such relationship but is
    /// still genuinely absent from the sample.
    /// <para>
    /// Each target is paired with a unique foreign peptide of near-identical neutral mass, so
    /// the pair is scored in the same DIA isolation window. The pool is binned at
    /// 0.25 Da and a target is served from the nearest non-empty bin within a 6 Da window,
    /// examining at most 16 candidates against <see cref="DecoySimilarityGate"/> (always, even
    /// in an ungated build). When the window yields nothing and the budget is not spent, the
    /// search continues outward without bound. Rejected candidates go back to the end of their
    /// bin for later targets.
    /// </para>
    /// </summary>
    public sealed class ForeignEntrapmentSource
    {
        /// <summary>
        /// Neutral-mass window (Da) inside which an entrapment peptide co-locates with its
        /// target: a 3 m/z isolation window at charge 2.
        /// </summary>
        public const double CO_LOCATION_WINDOW_DA = 6.0;

        /// <summary>Bin width (Da).</summary>
        private const double BIN_WIDTH_DA = 0.25;

        /// <summary>Bins either side of the target's own bin that still co-locate.</summary>
        private const int CO_LOCATION_BINS = (int)(CO_LOCATION_WINDOW_DA / 2.0 / BIN_WIDTH_DA);

        /// <summary>Candidates examined per target before giving up when the gate keeps rejecting.</summary>
        private const int MAX_GATE_ATTEMPTS = 16;

        private readonly Queue<string>[] _bins;
        private readonly double _minMass;

        private ForeignEntrapmentSource(int binCount, double minMass)
        {
            _bins = new Queue<string>[binCount];
            _minMass = minMass;
        }

        /// <summary>Distinct foreign peptides still available.</summary>
        public int Available { get; private set; }

        /// <summary>Pool size before any assignment.</summary>
        public int Size { get; private set; }

        /// <summary>Foreign peptides excluded for being I/L-isobaric to a real target.</summary>
        public int IlCollisionsDropped { get; private set; }

        /// <summary>
        /// Digests a foreign proteome into a pool. A peptide is dropped when it equals a real
        /// target, is I/L-isobaric to one, has an unknown residue or, when
        /// <paramref name="applyMzFilter"/>, has no unmodified m/z in range at any charge.
        /// </summary>
        public static ForeignEntrapmentSource Build(string fastaPath, Digester digester,
            ISet<string> excluded, ISet<string> excludedIl, bool applyMzFilter,
            int[] charges, double minMz, double maxMz, TextWriter log = null)
        {
            int proteins = 0, droppedHomologous = 0, droppedIlCollision = 0, droppedUnknownAa = 0, droppedOutOfMz = 0;
            var seen = new HashSet<string>();
            var keptSequences = new List<string>();
            var keptMasses = new List<double>();
            double lo = double.MaxValue;
            double hi = -double.MaxValue;
            foreach (var entry in FastaReader.ReadFile(fastaPath, log == null ? null : new Action<string>(log.WriteLine)))
            {
                string sequence = JavaText.ToUpper(RemoveAsciiWhitespace(entry.Sequence));
                if (sequence.Length == 0)
                    continue;
                proteins++;
                foreach (string peptide in digester.Digest(sequence))
                {
                    if (!seen.Add(peptide))
                        continue;
                    if (excluded.Contains(peptide))
                    {
                        droppedHomologous++;
                        continue;
                    }
                    if (excludedIl.Contains(EntrapmentSequences.IlNormalize(peptide)))
                    {
                        // The case an exact-string audit reports as clean: not equal to any
                        // target, but isobaric to one with the same fragment ladder.
                        droppedIlCollision++;
                        continue;
                    }
                    double? mass = ResidueMasses.PeptideNeutralMass(peptide);
                    if (!mass.HasValue)
                    {
                        droppedUnknownAa++;
                        continue;
                    }
                    if (applyMzFilter && !ResidueMasses.FitsMzRange(mass.Value, charges, minMz, maxMz))
                    {
                        droppedOutOfMz++;
                        continue;
                    }
                    keptSequences.Add(peptide);
                    keptMasses.Add(mass.Value);
                    lo = Math.Min(lo, mass.Value);
                    hi = Math.Max(hi, mass.Value);
                }
            }
            if (keptSequences.Count == 0)
                throw new IOException(@"Foreign entrapment FASTA yielded no usable peptides: " + fastaPath);

            var pool = new ForeignEntrapmentSource((int)((hi - lo) / BIN_WIDTH_DA) + 2, lo);
            // Filled in mass order (sequence breaking ties) so each bin's contents, and the order
            // they are served in, are deterministic.
            var order = new int[keptSequences.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = keptMasses[a].CompareTo(keptMasses[b]);
                return c != 0 ? c : string.CompareOrdinal(keptSequences[a], keptSequences[b]);
            });
            foreach (int index in order)
                pool.Add(keptSequences[index], keptMasses[index]);
            pool.IlCollisionsDropped = droppedIlCollision;
            log?.WriteLine(string.Format(CultureInfo.InvariantCulture,
                @"Foreign entrapment source: {0} proteins -> {1} unique candidate peptides ({2} dropped equal to a real target, {3} dropped I/L-isobaric to a real target, {4} unknown AA, {5} out of m/z range)",
                proteins, pool.Size, droppedHomologous, droppedIlCollision, droppedUnknownAa, droppedOutOfMz));
            return pool;
        }

        /// <summary>
        /// A unique foreign peptide for <paramref name="targetSequence"/>, preferring one that
        /// co-locates with it, or null when the pool is exhausted or every candidate examined
        /// failed the similarity gate.
        /// </summary>
        public string Assign(string targetSequence, double targetMass)
        {
            // An exhausted pool is an anticipated state, so do not scan every empty bin.
            if (Available == 0)
                return null;
            // One gate budget across both scans, with rejected candidates held aside until the
            // end so the wider scan cannot re-poll them.
            var budget = new GateBudget(MAX_GATE_ATTEMPTS);
            var putBack = new List<string>();
            try
            {
                string hit = Search(targetSequence, targetMass, 0, CO_LOCATION_BINS, budget, putBack);
                if (hit != null)
                    return hit;
                // The window had candidates and they all failed the gate: widening cannot help.
                if (budget.Remaining <= 0)
                    return null;
                return Search(targetSequence, targetMass, CO_LOCATION_BINS + 1, _bins.Length, budget, putBack);
            }
            finally
            {
                // Rejected candidates go back: a target with a different ladder can use them.
                foreach (string candidate in putBack)
                {
                    double? mass = ResidueMasses.PeptideNeutralMass(candidate);
                    if (mass.HasValue)
                    {
                        EnqueueInBin(candidate, mass.Value);
                        Available++;
                    }
                }
            }
        }

        private void Add(string peptide, double mass)
        {
            EnqueueInBin(peptide, mass);
            Available++;
            Size++;
        }

        private void EnqueueInBin(string peptide, double mass)
        {
            int b = BinOf(mass);
            if (_bins[b] == null)
                _bins[b] = new Queue<string>();
            _bins[b].Enqueue(peptide);
        }

        private int BinOf(double mass)
        {
            // Java's (int) cast truncates toward zero, as C#'s does.
            int b = (int)((mass - _minMass) / BIN_WIDTH_DA);
            if (b < 0)
                return 0;
            return Math.Min(b, _bins.Length - 1);
        }

        /// <summary>
        /// The first candidate passing the gate from the nearest non-empty bin at a radius in
        /// [minRadius, maxRadius], the higher-mass side first at each radius.
        /// </summary>
        private string Search(string targetSequence, double targetMass, int minRadius, int maxRadius,
            GateBudget budget, List<string> putBack)
        {
            int home = BinOf(targetMass);
            for (int radius = minRadius; radius <= maxRadius; radius++)
            {
                for (int sign = 0; sign < (radius == 0 ? 1 : 2); sign++)
                {
                    int b = home + (sign == 0 ? radius : -radius);
                    if (b < 0 || b >= _bins.Length || _bins[b] == null || _bins[b].Count == 0)
                        continue;
                    var bin = _bins[b];
                    while (bin.Count > 0 && budget.Remaining > 0)
                    {
                        string candidate = bin.Dequeue();
                        Available--;
                        if (DecoySimilarityGate.IsCandidateAcceptable(targetSequence, candidate))
                            return candidate;
                        putBack.Add(candidate);
                        budget.Remaining--;
                    }
                    if (budget.Remaining <= 0)
                        return null;
                }
            }
            return null;
        }

        /// <summary>Java's <c>replaceAll("\\s", "")</c>: removes the six ASCII whitespace characters.</summary>
        private static string RemoveAsciiWhitespace(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c != ' ' && c != '\t' && c != '\n' && c != '\u000B' && c != '\f' && c != '\r')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The gate-rejection allowance one assignment shares across its scans.</summary>
        private sealed class GateBudget
        {
            public GateBudget(int remaining)
            {
                Remaining = remaining;
            }

            public int Remaining { get; set; }
        }
    }
}
