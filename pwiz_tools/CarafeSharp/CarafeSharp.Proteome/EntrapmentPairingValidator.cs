/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentPairingValidator.java
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
using System.Linq;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Integrity checks on an entrapment pairing, so a broken pairing is refused where it is
    /// made rather than worked around by a paired FDP estimator (FDRBench, Osprey), which
    /// either crashes or silently mis-scales on one. Two surfaces: what the generator emitted
    /// (<see cref="ValidateQuartets"/>), and what a predictor produced from it
    /// (<see cref="ValidateLibraryAgainstManifest"/>).
    /// </summary>
    public static class EntrapmentPairingValidator
    {
        public const string KIND_MISSING_TARGET = @"quartet with no target";
        public const string KIND_SELECTED_WITHOUT_ENTRAPMENT = @"entrapment-selected quartet with no entrapment";
        public const string KIND_ENTRAPMENT_EQUALS_TARGET = @"entrapment sequence equal to its target";
        public const string KIND_GENERATED_EQUALS_TARGET = @"generated sequence equal to a real target";
        public const string KIND_ORPHAN_ENTRAPMENT = @"entrapment peptide with no target twin";
        public const string KIND_UNCOVERED_TARGET = @"target with no entrapment coverage";

        private const int MAX_EXAMPLES = 5;

        /// <summary>
        /// Checks the quartets about to be written: each has a target, each selected for
        /// entrapment has one, no entrapment equals its own target, and no generated sequence
        /// equals any real target. Generated sequences may collide with each other; with a
        /// million short peptides that is unavoidable and harmless. Examples of the last check
        /// come in first-seen order, where Carafe reports them in HashMap order.
        /// </summary>
        public static List<PairingViolation> ValidateQuartets(IReadOnlyList<EntrapmentQuartet> quartets)
        {
            var missingTarget = new Tally();
            var selectedWithoutEntrapment = new Tally();
            var entrapmentEqualsTarget = new Tally();
            var targetSequences = new HashSet<string>();
            var generated = new List<string>();
            var generatedSeen = new HashSet<string>();
            for (int pairIndex = 0; pairIndex < quartets.Count; pairIndex++)
            {
                var q = quartets[pairIndex];
                if (string.IsNullOrEmpty(q.Target))
                    missingTarget.Hit(@"pair_index " + pairIndex.ToString(CultureInfo.InvariantCulture));
                if (q.EntrapmentSelected && q.PTarget == null)
                    selectedWithoutEntrapment.Hit(q.Target);
                if (q.PTarget != null && string.Equals(q.PTarget, q.Target, StringComparison.Ordinal))
                    entrapmentEqualsTarget.Hit(q.Target);
                if (q.Target != null)
                    targetSequences.Add(q.Target);
                foreach (string sequence in new[] { q.PTarget, q.Decoy, q.PDecoy })
                {
                    if (sequence != null && generatedSeen.Add(sequence))
                        generated.Add(sequence);
                }
            }
            // Equal to a real target is exactly what the collision-drop pass prevents, so one
            // surviving here is a generator bug, not a statistical inevitability.
            var collidesWithRealTarget = new Tally();
            foreach (string sequence in generated)
            {
                if (targetSequences.Contains(sequence))
                    collidesWithRealTarget.Hit(sequence);
            }

            var violations = new List<PairingViolation>();
            missingTarget.AddTo(violations, KIND_MISSING_TARGET);
            selectedWithoutEntrapment.AddTo(violations, KIND_SELECTED_WITHOUT_ENTRAPMENT);
            entrapmentEqualsTarget.AddTo(violations, KIND_ENTRAPMENT_EQUALS_TARGET);
            collidesWithRealTarget.AddTo(violations, KIND_GENERATED_EQUALS_TARGET);
            return violations;
        }

        /// <summary>
        /// Checks a predicted library against the manifest it came from. Every library
        /// entrapment peptide must pair with a library target: its manifest twin, or, for a
        /// methionine-clipped entrapment, the clip of its twin. A library target whose manifest
        /// pair lists an entrapment the library lacks is reported too, as it silently biases an
        /// entrapment FDP downward; a pair never given entrapment is by design.
        /// </summary>
        /// <param name="libraryEntrapment">Entrapment (p_target) sequences in the library.</param>
        /// <param name="libraryTargets">Target sequences in the library.</param>
        /// <param name="manifestPairs">Manifest sequence to pair index, for every type.</param>
        /// <param name="manifestTypes">Manifest sequence to peptide_type.</param>
        public static List<PairingViolation> ValidateLibraryAgainstManifest(
            IEnumerable<string> libraryEntrapment, IEnumerable<string> libraryTargets,
            IReadOnlyDictionary<string, int> manifestPairs, IReadOnlyDictionary<string, string> manifestTypes)
        {
            var targetOfPair = new Dictionary<int, string>();
            var entrapmentOfPair = new Dictionary<int, string>();
            foreach (var pair in manifestPairs)
            {
                manifestTypes.TryGetValue(pair.Key, out string type);
                if (type == EntrapmentFastaBuilder.PEPTIDE_TYPE_TARGET)
                    targetOfPair[pair.Value] = pair.Key;
                else if (type == EntrapmentFastaBuilder.PEPTIDE_TYPE_P_TARGET)
                    entrapmentOfPair[pair.Value] = pair.Key;
            }
            var entrapmentList = libraryEntrapment.ToList();
            var targetList = libraryTargets.ToList();
            var libraryTargetSet = new HashSet<string>(targetList);
            var libraryEntrapmentSet = new HashSet<string>(entrapmentList);

            var orphanEntrapment = new Tally();
            foreach (string entrapment in entrapmentList)
            {
                string requiredTarget;
                if (manifestPairs.TryGetValue(entrapment, out int index))
                {
                    targetOfPair.TryGetValue(index, out requiredTarget);
                }
                else if (manifestPairs.TryGetValue(@"M" + entrapment, out int clippedIndex))
                {
                    // A clipped entrapment pairs with the clip of its target, which exists only
                    // when the target itself starts with M.
                    targetOfPair.TryGetValue(clippedIndex, out string target);
                    requiredTarget = StripLeadingMethionine(target);
                }
                else
                {
                    // Neither a manifest peptide nor the clip of one: equally unusable.
                    orphanEntrapment.Hit(entrapment);
                    continue;
                }
                if (requiredTarget == null || !libraryTargetSet.Contains(requiredTarget))
                    orphanEntrapment.Hit(entrapment);
            }

            var uncoveredTargets = new Tally();
            foreach (string target in targetList)
            {
                string manifestEntrapment;
                bool clipped = false;
                if (manifestPairs.TryGetValue(target, out int index))
                {
                    entrapmentOfPair.TryGetValue(index, out manifestEntrapment);
                }
                else if (manifestPairs.TryGetValue(@"M" + target, out int clippedIndex))
                {
                    entrapmentOfPair.TryGetValue(clippedIndex, out manifestEntrapment);
                    clipped = true;
                }
                else
                {
                    continue;
                }
                // No manifest entrapment is by design: this pair was never selected to carry one.
                if (manifestEntrapment == null)
                    continue;
                string required = clipped ? StripLeadingMethionine(manifestEntrapment) : manifestEntrapment;
                if (required == null || !libraryEntrapmentSet.Contains(required))
                    uncoveredTargets.Hit(target);
            }

            var violations = new List<PairingViolation>();
            orphanEntrapment.AddTo(violations, KIND_ORPHAN_ENTRAPMENT);
            uncoveredTargets.AddTo(violations, KIND_UNCOVERED_TARGET);
            return violations;
        }

        /// <summary>
        /// Refuses (throws) when there are violations and <paramref name="failOnViolation"/>, else
        /// writes a warning and returns. Nothing happens for no violations.
        /// </summary>
        public static void Enforce(IReadOnlyList<PairingViolation> violations, bool failOnViolation, TextWriter log = null)
        {
            if (violations.Count == 0)
                return;
            string detail = Describe(violations);
            if (failOnViolation)
            {
                throw new InvalidOperationException(@"Entrapment manifest failed integrity checks: " + detail +
                    @". These pairings would break a paired FDP estimator (FDRBench), so the manifest was not written. " +
                    @"If you have reviewed the violations above and want the manifest anyway, re-run with -ignore_pairing_errors. " +
                    @"Do NOT use -no_similarity_gate for this: it changes which sequences are generated and produces a library that should not be searched.");
            }
            log?.WriteLine(@"WARNING: Entrapment manifest failed integrity checks: " + detail +
                @". Writing it anyway because -ignore_pairing_errors was given. A paired FDP estimator may crash or report a mis-scaled FDP on this manifest.");
        }

        /// <summary>The violations as one message, separated by "; ".</summary>
        public static string Describe(IEnumerable<PairingViolation> violations)
        {
            return string.Join(@"; ", violations);
        }

        private static string StripLeadingMethionine(string sequence)
        {
            return sequence != null && sequence.StartsWith(@"M", StringComparison.Ordinal) ? sequence.Substring(1) : null;
        }

        /// <summary>A violation tally: the full count, with capped examples.</summary>
        private sealed class Tally
        {
            private readonly List<string> _examples = new List<string>();
            private int _count;

            public void Hit(string example)
            {
                _count++;
                if (_examples.Count < MAX_EXAMPLES)
                    _examples.Add(example);
            }

            public void AddTo(List<PairingViolation> violations, string kind)
            {
                if (_count > 0)
                    violations.Add(new PairingViolation(kind, _count, _examples));
            }
        }
    }
}
