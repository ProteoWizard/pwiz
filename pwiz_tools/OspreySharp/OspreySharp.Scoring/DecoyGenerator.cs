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

using System.Collections.Generic;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
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
            decoy.ProteinIds = new List<string>();
            foreach (string p in target.ProteinIds)
                decoy.ProteinIds.Add("DECOY_" + p);

            decoy.GeneNames = new List<string>(target.GeneNames);

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
        public static List<Modification> RemapModificationsStatic(
            List<Modification> modifications, int[] positionMapping)
        {
            var instance = new DecoyGenerator();
            return instance.RemapModifications(modifications, positionMapping);
        }

        private List<Modification> RemapModifications(List<Modification> modifications, int[] positionMapping)
        {
            // Create reverse mapping: old_pos -> new_pos
            var reverseMap = new Dictionary<int, int>();
            for (int newPos = 0; newPos < positionMapping.Length; newPos++)
            {
                reverseMap[positionMapping[newPos]] = newPos;
            }

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
            return remapped;
        }

        /// <summary>
        /// Public static wrapper for <see cref="RecalculateFragments"/> so that
        /// AnalysisPipeline can rebuild fragments for a collision-checked decoy.
        /// </summary>
        public static List<LibraryFragment> RecalculateFragmentsStatic(
            LibraryEntry target, int[] positionMapping, string decoySequence)
        {
            var instance = new DecoyGenerator();
            return instance.RecalculateFragments(target, positionMapping, decoySequence);
        }

        private List<LibraryFragment> RecalculateFragments(
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

                if (annotation.IonType == IonType.B)
                {
                    newIonType = IonType.Y;
                    newOrdinal = seqLen - annotation.Ordinal;
                }
                else if (annotation.IonType == IonType.Y)
                {
                    newIonType = IonType.B;
                    newOrdinal = seqLen - annotation.Ordinal;
                }
                else
                {
                    // For other ion types, keep as-is
                    result.Add(new LibraryFragment
                    {
                        Mz = frag.Mz,
                        RelativeIntensity = frag.RelativeIntensity,
                        Annotation = new FragmentAnnotation
                        {
                            IonType = annotation.IonType,
                            Ordinal = annotation.Ordinal,
                            Charge = annotation.Charge,
                            NeutralLoss = annotation.NeutralLoss
                        }
                    });
                    continue;
                }

                if (newOrdinal <= 0 || newOrdinal > seqLen)
                    continue;

                double? mz = CalculateFragmentMz(
                    newIonType, newOrdinal, annotation.Charge,
                    decoySequence, modMasses,
                    annotation.NeutralLoss != null ? (double?)annotation.NeutralLoss.Mass : null);

                if (mz.HasValue)
                {
                    result.Add(new LibraryFragment
                    {
                        Mz = mz.Value,
                        RelativeIntensity = frag.RelativeIntensity,
                        Annotation = new FragmentAnnotation
                        {
                            IonType = newIonType,
                            Ordinal = (byte)newOrdinal,
                            Charge = annotation.Charge,
                            NeutralLoss = annotation.NeutralLoss
                        }
                    });
                }
            }

            return result;
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
