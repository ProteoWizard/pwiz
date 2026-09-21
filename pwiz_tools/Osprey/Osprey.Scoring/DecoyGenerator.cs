/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading.Tasks;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Digestion enzyme (affects terminal preservation during reversal).
    /// </summary>
    public enum Enzyme
    {
        /// <summary>Trypsin: C-terminal cleavage at K/R (preserves C-terminus).</summary>
        Trypsin,
        /// <summary>Lys-C: C-terminal cleavage at K (preserves C-terminus).</summary>
        LysC,
        /// <summary>Lys-N: N-terminal cleavage at K (preserves N-terminus).</summary>
        LysN,
        /// <summary>Asp-N: N-terminal cleavage at D (preserves N-terminus).</summary>
        AspN,
        /// <summary>No enzyme specificity.</summary>
        Unspecific
    }

    /// <summary>
    /// Extension methods for <see cref="Enzyme"/>.
    /// </summary>
    public static class EnzymeExtensions
    {
        /// <summary>
        /// Returns true if this enzyme cleaves at C-terminus (so C-term should be preserved).
        /// </summary>
        public static bool PreservesCTerminus(this Enzyme enzyme)
        {
            return enzyme == Enzyme.Trypsin || enzyme == Enzyme.LysC || enzyme == Enzyme.Unspecific;
        }
    }

    /// <summary>
    /// Generates decoy peptide sequences for target-decoy scoring.
    /// Maps to osprey-scoring/src/lib.rs DecoyGenerator in the Rust implementation.
    /// </summary>
    public class DecoyGenerator
    {
        private static readonly Dictionary<char, double> STANDARD_AA_MASSES = new Dictionary<char, double>
        {
            { 'A', 71.037114 }, { 'R', 156.101111 }, { 'N', 114.042927 },
            { 'D', 115.026943 }, { 'C', 103.009185 }, { 'E', 129.042593 },
            { 'Q', 128.058578 }, { 'G', 57.021464 }, { 'H', 137.058912 },
            { 'I', 113.084064 }, { 'L', 113.084064 }, { 'K', 128.094963 },
            { 'M', 131.040485 }, { 'F', 147.068414 }, { 'P', 97.052764 },
            { 'S', 87.032028 }, { 'T', 101.047679 }, { 'W', 186.079313 },
            { 'Y', 163.063329 }, { 'V', 99.068414 }
        };

        private const double PROTON_MASS = 1.007276;
        private const double H2O_MASS = 18.010565;

        private readonly Enzyme _enzyme;

        /// <summary>
        /// Create a new decoy generator with the specified enzyme.
        /// </summary>
        public DecoyGenerator(Enzyme enzyme)
        {
            _enzyme = enzyme;
        }

        /// <summary>
        /// Create a new decoy generator with default trypsin enzyme.
        /// </summary>
        public DecoyGenerator() : this(Enzyme.Trypsin) { }

        /// <summary>
        /// Detect the digestion enzyme from the C-terminal residue of a peptide sequence.
        /// </summary>
        public static Enzyme DetectEnzyme(string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                return Enzyme.Trypsin;

            char cTerm = sequence[sequence.Length - 1];
            if (cTerm == 'K' || cTerm == 'R')
                return Enzyme.Trypsin;

            char nTerm = sequence[0];
            if (nTerm == 'K')
                return Enzyme.LysN;
            if (nTerm == 'D')
                return Enzyme.AspN;

            return Enzyme.Unspecific;
        }

        /// <summary>
        /// Generate a decoy from a target library entry using sequence reversal.
        /// Preserves terminal residue per enzyme, reverses internal residues.
        /// Falls back to cycling if reversed == original.
        /// </summary>
        public LibraryEntry Generate(LibraryEntry target)
        {
            int[] positionMapping;
            string decoySequence = ReverseSequence(target.Sequence, out positionMapping);

            // Collision fallback: if reversed == original, try cycling
            if (decoySequence == target.Sequence)
            {
                decoySequence = CycleSequence(target.Sequence, out positionMapping);
            }

            var decoy = new LibraryEntry(
                target.Id | 0x80000000,
                decoySequence,
                "DECOY_" + target.ModifiedSequence,
                target.Charge,
                target.PrecursorMz,
                target.RetentionTime);
            decoy.RtCalibrated = target.RtCalibrated;
            decoy.IsDecoy = true;

            // Remap modifications to new positions
            decoy.Modifications = RemapModifications(target.Modifications, positionMapping);

            // Recalculate fragment m/z values for the reversed sequence
            decoy.Fragments = RecalculateFragments(target, positionMapping, decoySequence);

            // Update protein IDs to indicate decoy
            decoy.ProteinIds = BuildDecoyProteinIds(target.ProteinIds, null);
            decoy.GeneNames = CopyGeneNames(target.GeneNames, null);

            return decoy;
        }

        /// <summary>
        /// Build the decoy's protein IDs ("DECOY_" + each target accession),
        /// interning through <paramref name="interner"/> when supplied. Empty /
        /// null input yields the shared empty array.
        /// </summary>
        private static string[] BuildDecoyProteinIds(
            IReadOnlyList<string> targetProteinIds, LibraryStringInterner interner)
        {
            if (targetProteinIds == null || targetProteinIds.Count == 0)
                return Array.Empty<string>();
            var pids = new string[targetProteinIds.Count];
            for (int i = 0; i < targetProteinIds.Count; i++)
            {
                string decoyAcc = "DECOY_" + targetProteinIds[i];
                pids[i] = interner != null ? interner.Intern(decoyAcc) : decoyAcc;
            }
            return pids;
        }

        /// <summary>
        /// Copy the target's gene names into the decoy, interning through
        /// <paramref name="interner"/> when supplied. Empty / null input yields
        /// the shared empty array.
        /// </summary>
        private static string[] CopyGeneNames(
            IReadOnlyList<string> targetGeneNames, LibraryStringInterner interner)
        {
            if (targetGeneNames == null || targetGeneNames.Count == 0)
                return Array.Empty<string>();
            var genes = new string[targetGeneNames.Count];
            for (int i = 0; i < targetGeneNames.Count; i++)
            {
                string g = targetGeneNames[i];
                genes[i] = interner != null ? interner.Intern(g) : g;
            }
            return genes;
        }

        /// <summary>
        /// Generate decoy entries from the target library with collision detection.
        /// Matches Rust DecoyGenerator.generate_all_with_collision_detection:
        ///   1. Build set of target sequences (stripped) for collision detection
        ///   2. For each target, try reversing
        ///   3. If reversed collides or is palindromic, try cycling with lengths 1..10
        ///   4. If all methods fail, exclude the target-decoy pair
        /// Modifies <paramref name="validTargets"/> to contain only targets that
        /// produced valid decoys (Rust: library = valid_targets; library.extend(decoys)).
        ///
        /// Relocated verbatim out of AbstractScoringTask; the ambient pipeline-context
        /// logger is now an injected <paramref name="logInfo"/> callback so Scoring stays
        /// free of a Tasks/PipelineContext dependency. Arithmetic is unchanged, so
        /// cross-impl parity is unaffected.
        ///
        /// <paramref name="omitFragments"/> supports the lean library a
        /// FirstPassFDR / <c>StopAfterStage5</c> worker loads (fragment peaks
        /// dropped, six identity scalars kept). When set: the empty-fragment
        /// exclusion gate is skipped (every real library entry has >= 1 fragment,
        /// so the gate never excluded any, and the loaded entries now carry no
        /// fragments), and each decoy is built with an empty fragment list
        /// (RecalculateFragments is skipped -- decoy fragments are the same dead
        /// weight the targets dropped). The decoys' six identity scalars are
        /// derived from the target scalars and the collision-checked sequence,
        /// never from fragment content, so the target/decoy set the FDR stages
        /// see is byte-identical to a full load.
        /// </summary>
        public static List<LibraryEntry> GenerateAllWithCollisionDetection(
            List<LibraryEntry> targets, OspreyConfig config,
            Action<string> logInfo, bool omitFragments,
            out List<LibraryEntry> validTargets)
        {
            // Public API: tolerate a missing logger as a no-op rather than throwing.
            logInfo = logInfo ?? (_ => { });
            logInfo(string.Format("Generating decoys using {0} method...", config.DecoyMethod));

            // Build set of all target (stripped) sequences for collision detection, I->L
            // normalized so isobaric collisions are visible (see NormalizeIsoleucine).
            var targetSequences = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in targets)
            {
                if (!t.IsDecoy)
                    targetSequences.Add(NormalizeIsoleucine(t.Sequence));
            }

            // Generate decoys in parallel (matches Rust's par_iter approach).
            // Each target produces a (target, decoy) pair or is excluded.
            int nReversed = 0, nCycled = 0, nExcluded = 0, nSkipped = 0;
            var results = new (LibraryEntry target, LibraryEntry decoy, int kind)[targets.Count];
            // kind: 0=skip, 1=reversed, 2=cycled, 3=excluded

            Parallel.For(0, targets.Count, i =>
            {
                var target = targets[i];
                // The fragment-count gate is skipped under omitFragments: the
                // library was loaded lean (every entry keeps only its scalars),
                // and every real entry had >= 1 fragment before the drop, so the
                // gate could not have excluded any. The IsDecoy skip is kept.
                if (target.IsDecoy ||
                    (!omitFragments && (target.Fragments == null || target.Fragments.Count == 0)))
                {
                    results[i] = (null, null, 0);
                    return;
                }

                // Each thread gets its own generator (DecoyGenerator is lightweight)
                var gen = new DecoyGenerator();
                int[] mapping;
                string reversedSeq = gen.ReverseSequence(target.Sequence, out mapping);

                if (reversedSeq != target.Sequence &&
                    !targetSequences.Contains(NormalizeIsoleucine(reversedSeq)) &&
                    IsCandidateAcceptable(target.Sequence, reversedSeq))
                {
                    var decoy = BuildDecoyFromSequence(target, reversedSeq, mapping, omitFragments);
                    if (decoy != null)
                    {
                        results[i] = (target, decoy, 1);
                        return;
                    }
                }

                // Fallback: cycling with lengths 1..min(len, 10)
                int maxRetries = Math.Min(target.Sequence.Length, 10);
                for (int cycleLength = 1; cycleLength <= maxRetries; cycleLength++)
                {
                    string cycledSeq = gen.CycleSequence(target.Sequence, cycleLength, out mapping);
                    if (cycledSeq != target.Sequence &&
                        !targetSequences.Contains(NormalizeIsoleucine(cycledSeq)) &&
                        IsCandidateAcceptable(target.Sequence, cycledSeq))
                    {
                        var decoy = BuildDecoyFromSequence(target, cycledSeq, mapping, omitFragments);
                        if (decoy != null)
                        {
                            results[i] = (target, decoy, 2);
                            return;
                        }
                    }
                }

                results[i] = (null, null, 3);
            });

            // Collect results (sequential, preserves order). Interning runs
            // here rather than in the parallel body because the pool is a plain
            // single-threaded dictionary. Decoys mint fresh strings ("DECOY_" +
            // accession, "DECOY_" + modified sequence) that the target load
            // never interned, so every decoy peptide of a protein would
            // otherwise hold a duplicate copy (huge proteins -- titin / obscurin
            // / nebulin -- dominate the string-duplicate retention view). One
            // pool per decoy build; only object identity changes, so the
            // regression golden stays byte-identical.
            var interner = new LibraryStringInterner();
            validTargets = new List<LibraryEntry>(targets.Count);
            var decoys = new List<LibraryEntry>(targets.Count);
            foreach (var r in results)
            {
                switch (r.kind)
                {
                    case 0: nSkipped++; break;
                    case 1: nReversed++; validTargets.Add(r.target); decoys.Add(InternDecoy(r.decoy, interner)); break;
                    case 2: nCycled++; validTargets.Add(r.target); decoys.Add(InternDecoy(r.decoy, interner)); break;
                    case 3: nExcluded++; break;
                }
            }

            interner.LogSummary(logInfo);
            logInfo(string.Format(
                "Generated {0} decoys from {1} targets ({2} excluded due to collisions)",
                decoys.Count, targets.Count, nExcluded));
            return decoys;
        }

        /// <summary>
        /// Maximum fraction of a candidate decoy's theoretical b/y ions that may fall within
        /// <see cref="LADDER_MATCH_TOLERANCE"/> of its target's. EncyclopeDIA's threshold
        /// (<c>PeptideUtils.getSmartDecoy</c> rejects above 0.4 and reshuffles).
        /// </summary>
        private const double MAX_FRAGMENT_OVERLAP = 0.4;

        /// <summary>
        /// Fixed m/z window for counting ladder coincidences, in daltons.
        /// Deliberately NOT the run's fragment tolerance: the decoy set must be a pure
        /// function of the library, and keying it to the search tolerance would make the
        /// same library produce different decoys under <c>unit</c> vs <c>hram</c>. A fixed
        /// window also lets Rust apply the identical rule without plumbing config into
        /// DecoyGenerator, which is required for cross-impl parity.
        /// </summary>
        private const double LADDER_MATCH_TOLERANCE = 0.02;

        /// <summary>
        /// Reject a candidate decoy sequence whose theoretical b/y ladder is too close to its
        /// target's, so the cycling fallback supplies another candidate instead.
        ///
        /// Osprey previously accepted the first sequence that merely differed from its target
        /// and collided with no target sequence. Every other implementation surveyed
        /// (EncyclopeDIA, SpectraST, OpenSWATH) also measures how SIMILAR the candidate is and
        /// regenerates when it is too close: a decoy whose ladder nearly coincides with its
        /// target's cannot lose the target/decoy competition on fragment evidence, so it is
        /// not an honest null.
        ///
        /// Measured effect at library scale is nil -- it excludes on the order of 1e-4 of
        /// peptides and moves entrapment FDP by less than noise. It is kept for robustness at
        /// SMALL library scale, where palindromes and low-complexity runs are a far larger
        /// fraction and a near-identical decoy would be both real and baffling.
        ///
        /// Computed from the stripped sequences only -- no modifications, no loaded fragment
        /// lists. Modifications shift both ladders alike, so they cannot change whether the
        /// two coincide; excluding them keeps the lean (<c>omitFragments</c>) library path
        /// bit-identical to a full load and keeps the C#/Rust rule trivially the same.
        /// </summary>
        internal static bool IsCandidateAcceptable(string targetSeq, string candidateSeq)
        {
            // Deliberately the FULL ladder, matching EncyclopeDIA's rule exactly: the 0.4
            // threshold is their published number, and it is only their number if measured
            // over their statistic. Two rungs are invariant under any C-terminus-preserving
            // permutation (y1, and b_{n-1} whose prefix multiset never changes), so they
            // always match and impose a 1/(n-1) floor on the ratio. That floor is tolerable
            // BECAUSE LibraryValidation.ValidatePeptideLength enforces a 6-residue minimum
            // at load: the worst case is 1/5 = 0.2 against a 0.4 budget. Without that
            // guarantee a length-3 peptide would floor at 0.5 and every candidate would be
            // rejected, silently dropping the peptide from the search.
            double[] targetLadder = TheoreticalLadder(targetSeq);
            double[] decoyLadder = TheoreticalLadder(candidateSeq);
            if (decoyLadder.Length == 0)
                return true;

            Array.Sort(targetLadder); // Array.Sort OK: sorting a private double[] purely to binary-search it; equal doubles are indistinguishable so tie order cannot reach any output
            int matches = 0;
            foreach (double mz in decoyLadder)
            {
                if (MatchesWithinTolerance(targetLadder, mz))
                    matches++;
            }
            return (double)matches / decoyLadder.Length <= MAX_FRAGMENT_OVERLAP;
        }

        /// <summary>
        /// Normalize isoleucine to leucine for collision detection.
        ///
        /// I and L have identical residue masses (113.08406), so a decoy differing from a real
        /// target only by I&lt;-&gt;L substitutions is precursor-mass-identical AND produces an
        /// identical b/y ladder - indistinguishable from that target by mass spectrometry. It
        /// is therefore not a valid null: it is detected wherever its target twin is, and every
        /// such detection is counted as a decoy hit when it is really a target hit, which
        /// deflates the estimated FDR.
        ///
        /// This SUBSUMES the exact-string collision check rather than adding to it, because a
        /// sequence containing no isoleucine normalizes to itself.
        ///
        /// It is independent of <see cref="IsCandidateAcceptable"/>, which cannot catch this:
        /// that gate compares a candidate to its OWN source target, while a collision here is
        /// an isobaric match to a DIFFERENT target. That is why an exact-string audit of a
        /// delivered library reports 0 and misses the population entirely. Measured over the
        /// 1,390,979-target Astral set with the overlap gate on, simulating this path: 0 exact
        /// collisions and 742 I/L-isobaric ones (0.0534%), e.g. AAEESLR -&gt; LSEEAAR.
        /// </summary>
        internal static string NormalizeIsoleucine(string sequence)
        {
            return sequence.Replace('I', 'L');
        }

        /// <summary>
        /// Singly-charged b and y ion m/z for every cleavage site of a stripped sequence,
        /// using the same residue masses and terminal adjustments as
        /// <see cref="CalculateFragmentMz"/>. Ions spanning an unknown residue are skipped
        /// rather than aborting the ladder.
        /// </summary>
        internal static double[] TheoreticalLadder(string sequence)
        {
            if (sequence == null || sequence.Length < 2)
                return Array.Empty<double>();
            int len = sequence.Length;

            // Prefix sums for b ions and SUFFIX sums for y ions; NaN marks an unknown
            // residue so only ions actually spanning it are dropped. Deriving y from
            // (total - prefix) instead would poison EVERY y ion the moment any residue is
            // unknown, because total itself is then NaN -- and a leading unknown residue
            // would empty the ladder outright, which the caller reads as "accept".
            // Selenocysteine (U) and the ambiguity codes B/Z/X/J/O are all absent from
            // STANDARD_AA_MASSES and do occur in UniProt-derived libraries.
            var prefix = new double[len + 1];
            for (int i = 0; i < len; i++)
            {
                double aa;
                prefix[i + 1] = STANDARD_AA_MASSES.TryGetValue(sequence[i], out aa)
                    ? prefix[i] + aa
                    : double.NaN;
                if (double.IsNaN(prefix[i]))
                    prefix[i + 1] = double.NaN;
            }
            var suffix = new double[len + 1];
            for (int i = len - 1; i >= 0; i--)
            {
                double aa;
                suffix[len - i] = STANDARD_AA_MASSES.TryGetValue(sequence[i], out aa)
                    ? suffix[len - i - 1] + aa
                    : double.NaN;
                if (double.IsNaN(suffix[len - i - 1]))
                    suffix[len - i] = double.NaN;
            }

            var ladder = new List<double>((len - 1) * 2);
            for (int ordinal = 1; ordinal < len; ordinal++)
            {
                double bMass = prefix[ordinal];
                if (!double.IsNaN(bMass))
                    ladder.Add(bMass + PROTON_MASS);
                // y{ordinal} spans the last `ordinal` residues.
                double yMass = suffix[ordinal];
                if (!double.IsNaN(yMass))
                    ladder.Add(yMass + H2O_MASS + PROTON_MASS);
            }
            return ladder.ToArray();
        }

        /// <summary>
        /// True when <paramref name="mz"/> is within <see cref="LADDER_MATCH_TOLERANCE"/> of
        /// any entry of the sorted <paramref name="sortedLadder"/>. Binary search keeps the
        /// gate O(n log n) over a library rather than O(n^2).
        /// </summary>
        private static bool MatchesWithinTolerance(double[] sortedLadder, double mz)
        {
            int idx = Array.BinarySearch(sortedLadder, mz);
            if (idx >= 0)
                return true;
            idx = ~idx;
            if (idx < sortedLadder.Length && sortedLadder[idx] - mz <= LADDER_MATCH_TOLERANCE)
                return true;
            if (idx > 0 && mz - sortedLadder[idx - 1] <= LADDER_MATCH_TOLERANCE)
                return true;
            return false;
        }

        /// <summary>
        /// Intern the freshly-minted string fields of a decoy entry (Sequence,
        /// ModifiedSequence, each Modification.Name, ProteinIds, GeneNames)
        /// through the shared pool. The decoy was just built by this class, so
        /// reassigning its interned arrays touches nothing shared. Only string
        /// identity changes.
        /// </summary>
        private static LibraryEntry InternDecoy(LibraryEntry decoy, LibraryStringInterner interner)
        {
            decoy.Sequence = interner.Intern(decoy.Sequence);
            decoy.ModifiedSequence = interner.Intern(decoy.ModifiedSequence);

            var mods = decoy.Modifications;
            if (mods.Count > 0)
            {
                var internedMods = new Modification[mods.Count];
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = mods[i];
                    m.Name = interner.Intern(m.Name);
                    internedMods[i] = m;
                }
                decoy.Modifications = internedMods;
            }

            var pids = decoy.ProteinIds;
            if (pids.Count > 0)
            {
                var internedPids = new string[pids.Count];
                for (int i = 0; i < pids.Count; i++)
                    internedPids[i] = interner.Intern(pids[i]);
                decoy.ProteinIds = internedPids;
            }

            var genes = decoy.GeneNames;
            if (genes.Count > 0)
            {
                var internedGenes = new string[genes.Count];
                for (int i = 0; i < genes.Count; i++)
                    internedGenes[i] = interner.Intern(genes[i]);
                decoy.GeneNames = internedGenes;
            }

            return decoy;
        }

        /// <summary>
        /// Build a decoy LibraryEntry from a decoy sequence and position mapping.
        /// Mirrors <see cref="Generate"/>'s construction but takes an already-chosen sequence.
        /// With <paramref name="omitFragments"/> the decoy gets an empty fragment
        /// list (RecalculateFragments is skipped) -- the identity scalars are
        /// unchanged, matching the lean library a StopAfterStage5 worker loads.
        /// </summary>
        private static LibraryEntry BuildDecoyFromSequence(
            LibraryEntry target, string decoySequence, int[] positionMapping, bool omitFragments)
        {
            var decoy = new LibraryEntry(
                target.Id | 0x80000000u,
                decoySequence,
                "DECOY_" + target.ModifiedSequence,
                target.Charge,
                target.PrecursorMz,
                target.RetentionTime);
            decoy.RtCalibrated = target.RtCalibrated;
            decoy.IsDecoy = true;
            decoy.Modifications = RemapModificationsStatic(
                target.Modifications, positionMapping);
            decoy.Fragments = omitFragments
                ? Array.Empty<LibraryFragment>()
                : RecalculateFragmentsStatic(target, positionMapping, decoySequence);
            // Strings stay un-interned here (this runs in a Parallel.For body);
            // the sequential collection loop interns every decoy afterwards.
            decoy.ProteinIds = BuildDecoyProteinIds(target.ProteinIds, null);
            decoy.GeneNames = CopyGeneNames(target.GeneNames, null);
            return decoy;
        }

        /// <summary>
        /// Reverse sequence preserving terminal residue based on enzyme.
        /// Returns (reversed_sequence, position_mapping) where position_mapping[new_pos] = old_pos.
        /// </summary>
        public string ReverseSequence(string sequence, out int[] positionMapping)
        {
            int len = sequence.Length;
            if (len <= 2)
            {
                positionMapping = new int[len];
                for (int i = 0; i < len; i++)
                    positionMapping[i] = i;
                return sequence;
            }

            char[] reversed = new char[len];
            positionMapping = new int[len];

            if (_enzyme.PreservesCTerminus())
            {
                // Preserve C-terminus only: reverse positions 0..len-2, keep position len-1
                for (int i = len - 2; i >= 0; i--)
                {
                    int newPos = len - 2 - i;
                    reversed[newPos] = sequence[i];
                    positionMapping[newPos] = i;
                }
                reversed[len - 1] = sequence[len - 1];
                positionMapping[len - 1] = len - 1;
            }
            else
            {
                // Preserve N-terminus only: keep position 0, reverse positions 1..len-1
                reversed[0] = sequence[0];
                positionMapping[0] = 0;
                for (int i = len - 1; i >= 1; i--)
                {
                    int newPos = len - i;
                    reversed[newPos] = sequence[i];
                    positionMapping[newPos] = i;
                }
            }

            return new string(reversed);
        }

        /// <summary>
        /// Cycle sequence by 1 position, preserving terminal residue based on enzyme.
        /// For trypsin: ABCDEK -> BCDEAK (shift internal by 1, keep K).
        /// </summary>
        public string CycleSequence(string sequence, out int[] positionMapping)
        {
            return CycleSequence(sequence, 1, out positionMapping);
        }

        /// <summary>
        /// Cycle sequence by N positions, preserving terminal residue based on enzyme.
        /// </summary>
        public string CycleSequence(string sequence, int cycleLength, out int[] positionMapping)
        {
            int len = sequence.Length;
            if (len <= 2 || cycleLength == 0)
            {
                positionMapping = new int[len];
                for (int i = 0; i < len; i++)
                    positionMapping[i] = i;
                return sequence;
            }

            char[] cycled = new char[len];
            positionMapping = new int[len];

            if (_enzyme.PreservesCTerminus())
            {
                int middleLen = len - 1;
                int effectiveCycle = cycleLength % middleLen;

                for (int i = 0; i < middleLen; i++)
                {
                    int srcIdx = (i + effectiveCycle) % middleLen;
                    cycled[i] = sequence[srcIdx];
                    positionMapping[i] = srcIdx;
                }
                cycled[len - 1] = sequence[len - 1];
                positionMapping[len - 1] = len - 1;
            }
            else
            {
                cycled[0] = sequence[0];
                positionMapping[0] = 0;

                int middleLen = len - 1;
                int effectiveCycle = cycleLength % middleLen;

                for (int i = 0; i < middleLen; i++)
                {
                    int srcIdx = 1 + ((i + effectiveCycle) % middleLen);
                    cycled[i + 1] = sequence[srcIdx];
                    positionMapping[i + 1] = srcIdx;
                }
            }

            return new string(cycled);
        }

        /// <summary>
        /// Public static wrapper for <see cref="RemapModifications"/> so that
        /// AnalysisPipeline can build decoys using a collision-checked sequence
        /// while reusing the remapping logic.
        /// </summary>
        public static Modification[] RemapModificationsStatic(
            IReadOnlyList<Modification> modifications, int[] positionMapping)
        {
            var instance = new DecoyGenerator();
            return instance.RemapModifications(modifications, positionMapping);
        }

        private Modification[] RemapModifications(IReadOnlyList<Modification> modifications, int[] positionMapping)
        {
            // Create reverse mapping: old_pos -> new_pos
            var reverseMap = new Dictionary<int, int>();
            for (int newPos = 0; newPos < positionMapping.Length; newPos++)
            {
                reverseMap[positionMapping[newPos]] = newPos;
            }

            // A modification whose old position has no entry in the reverse map
            // is dropped, so the count is not known up front; accumulate in a
            // list and return an array (empty -> shared empty array).
            var remapped = new List<Modification>();
            foreach (var m in modifications)
            {
                int newPosition;
                if (reverseMap.TryGetValue(m.Position, out newPosition))
                {
                    remapped.Add(new Modification
                    {
                        Position = newPosition,
                        UnimodId = m.UnimodId,
                        MassDelta = m.MassDelta,
                        Name = m.Name
                    });
                }
            }
            return remapped.Count == 0 ? Array.Empty<Modification>() : remapped.ToArray();
        }

        /// <summary>
        /// Public static wrapper for <see cref="RecalculateFragments"/> so that
        /// AnalysisPipeline can rebuild fragments for a collision-checked decoy.
        /// </summary>
        public static LibraryFragment[] RecalculateFragmentsStatic(
            LibraryEntry target, int[] positionMapping, string decoySequence)
        {
            var instance = new DecoyGenerator();
            return instance.RecalculateFragments(target, positionMapping, decoySequence);
        }

        private LibraryFragment[] RecalculateFragments(
            LibraryEntry target, int[] positionMapping, string decoySequence)
        {
            int seqLen = target.Sequence.Length;

            // Build modification mass map for decoy (by new position)
            var modMasses = new Dictionary<int, double>();
            foreach (var m in target.Modifications)
            {
                for (int newPos = 0; newPos < positionMapping.Length; newPos++)
                {
                    if (positionMapping[newPos] == m.Position)
                    {
                        modMasses[newPos] = m.MassDelta;
                        break;
                    }
                }
            }

            var result = new List<LibraryFragment>();
            foreach (var frag in target.Fragments)
            {
                var annotation = frag.Annotation;

                IonType newIonType;
                int newOrdinal;

                if (annotation.IonType == IonType.B || annotation.IonType == IonType.Y)
                {
                    // The decoy fragment keeps the target's ion type and ordinal (a target y7
                    // yields a decoy y7); only the m/z is recomputed below for the permuted
                    // sequence, so the copied relative intensity stays on the same-numbered ion.
                    //
                    // This replaced a b<->y swap (target b_k -> decoy y_{n-k}) that carried the
                    // intensity along with the relabel. The residue-coverage reasoning behind
                    // that mapping was sound, but intensity is dominated by ion TYPE, not by
                    // which residues an ion spans: y ions are systematically more intense than
                    // b ions, so the swap inverted the decoy spectrum's intensity structure
                    // relative to any real peptide. Entrapment-measured FDP at a claimed 1% q
                    // was 10.9% on Stellar and 7.6% on Astral, against 1.5% / 2.0% after.
                    //
                    // Skyline, OpenSWATH, DIA-NN, EncyclopeDIA and SpectraST all map the
                    // intensity to the same ion; none of them swaps.
                    newIonType = annotation.IonType;
                    newOrdinal = annotation.Ordinal;
                }
                else
                {
                    // For other ion types, keep as-is (annotation copied by value).
                    result.Add(new LibraryFragment
                    {
                        Mz = frag.Mz,
                        RelativeIntensity = frag.RelativeIntensity,
                        Annotation = annotation
                    });
                    continue;
                }

                if (newOrdinal <= 0 || newOrdinal > seqLen)
                    continue;

                double? mz = CalculateFragmentMz(
                    newIonType, newOrdinal, annotation.Charge,
                    decoySequence, modMasses,
                    annotation.HasNeutralLoss ? annotation.NeutralLossMass : null);

                if (mz.HasValue)
                {
                    // Ion type and ordinal are carried through unchanged; charge and
                    // neutral loss (code + custom mass) carry over from the target
                    // fragment. Only the m/z is recomputed for the permuted sequence.
                    var newAnnotation = annotation;
                    newAnnotation.IonType = newIonType;
                    newAnnotation.Ordinal = (byte)newOrdinal;
                    result.Add(new LibraryFragment
                    {
                        Mz = mz.Value,
                        RelativeIntensity = frag.RelativeIntensity,
                        Annotation = newAnnotation
                    });
                }
            }

            // Fragments are filtered (b/y swap can drop out-of-range or
            // uncomputable ordinals), so the count is not known up front;
            // accumulate in a list and return an array.
            return result.Count == 0 ? Array.Empty<LibraryFragment>() : result.ToArray();
        }

        private double? CalculateFragmentMz(
            IonType ionType, int ordinal, byte charge,
            string sequence, Dictionary<int, double> modMasses,
            double? neutralLoss)
        {
            int seqLen = sequence.Length;
            int start, end;

            switch (ionType)
            {
                case IonType.B:
                    start = 0;
                    end = ordinal;
                    break;
                case IonType.Y:
                    start = seqLen - ordinal;
                    end = seqLen;
                    break;
                default:
                    return null;
            }

            if (end > seqLen)
                return null;

            double mass = 0.0;
            for (int i = start; i < end; i++)
            {
                double aaMass;
                if (!STANDARD_AA_MASSES.TryGetValue(sequence[i], out aaMass))
                    return null;
                mass += aaMass;

                double modMass;
                if (modMasses.TryGetValue(i, out modMass))
                    mass += modMass;
            }

            switch (ionType)
            {
                case IonType.B:
                    mass += PROTON_MASS;
                    break;
                case IonType.Y:
                    mass += H2O_MASS + PROTON_MASS;
                    break;
            }

            if (neutralLoss.HasValue)
                mass -= neutralLoss.Value;

            double mz = (mass + (charge - 1.0) * PROTON_MASS) / charge;
            return mz;
        }
    }
}
