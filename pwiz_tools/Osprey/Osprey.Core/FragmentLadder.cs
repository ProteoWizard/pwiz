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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The full b/y fragment ladder of a peptide at fragment charges 1 and 2, laid out in
    /// AlphaPeptDeep's slot order so a consumer that trains an intensity predictor can read
    /// the arrays without a key: slot <c>p * 4 + t</c> for cleavage position
    /// <c>p = 0 .. L-2</c> (after residue <c>p</c>) and ion type
    /// <c>t = 0 b(z=1), 1 b(z=2), 2 y(z=1), 3 y(z=2)</c>. Position <c>p</c> holds
    /// <c>b(p+1)</c> and <c>y(L-1-p)</c>, so the two ions of a slot pair are complementary.
    ///
    /// <para>A slot is NOT applicable - its m/z is NaN - when its fragment charge exceeds
    /// <c>min(precursor charge, 2)</c>, or when the ion spans a residue with no standard mass.
    /// m/z comes from <see cref="PeptideFragmentMass.CalculateFragmentMz"/>, the same residue
    /// masses and order of additions decoy generation and the blib annotation check use.</para>
    /// </summary>
    public static class FragmentLadder
    {
        /// <summary>Ion-type slots per cleavage position.</summary>
        public const int TYPES_PER_POSITION = 4;

        /// <summary>The highest fragment charge the ladder carries.</summary>
        public const int MAX_FRAGMENT_CHARGE = 2;

        /// <summary>Number of slots for a peptide of <paramref name="length"/> residues.</summary>
        public static int SlotCount(int length)
        {
            return length < 2 ? 0 : TYPES_PER_POSITION * (length - 1);
        }

        /// <summary>The slot index of an ion (see the class summary for the layout).</summary>
        public static int SlotOf(int position, IonType ionType, int fragmentCharge)
        {
            return position * TYPES_PER_POSITION + TypeIndex(ionType, fragmentCharge);
        }

        /// <summary>
        /// The ladder slot a library annotation names, or -1 when it names none: an ion
        /// other than b or y, a neutral loss, a fragment charge outside 1..2, or an ordinal
        /// outside 1..L-1.
        /// </summary>
        public static int SlotOf(FragmentAnnotation annotation, int length)
        {
            if (annotation.HasNeutralLoss)
                return -1;
            if (annotation.Charge < 1 || annotation.Charge > MAX_FRAGMENT_CHARGE)
                return -1;
            int ordinal = annotation.Ordinal;
            if (ordinal < 1 || ordinal > length - 1)
                return -1;
            switch (annotation.IonType)
            {
                case IonType.B:
                    return SlotOf(ordinal - 1, IonType.B, annotation.Charge);
                case IonType.Y:
                    return SlotOf(length - 1 - ordinal, IonType.Y, annotation.Charge);
                default:
                    return -1;
            }
        }

        /// <summary>Ion type of a slot.</summary>
        public static IonType IonTypeOf(int slot)
        {
            return slot % TYPES_PER_POSITION < 2 ? IonType.B : IonType.Y;
        }

        /// <summary>Fragment charge of a slot.</summary>
        public static int ChargeOf(int slot)
        {
            return slot % 2 == 0 ? 1 : 2;
        }

        /// <summary>Ordinal (residues in the ion) of a slot in a peptide of <paramref name="length"/>.</summary>
        public static int OrdinalOf(int slot, int length)
        {
            int position = slot / TYPES_PER_POSITION;
            return IonTypeOf(slot) == IonType.B ? position + 1 : length - 1 - position;
        }

        /// <summary>
        /// m/z of every slot for <paramref name="sequence"/> carrying
        /// <paramref name="modifications"/> at <paramref name="precursorCharge"/>, NaN where a
        /// slot is not applicable. Empty for a sequence shorter than two residues.
        /// </summary>
        public static double[] Build(string sequence, IEnumerable<Modification> modifications,
            int precursorCharge)
        {
            int length = sequence?.Length ?? 0;
            var mzs = new double[SlotCount(length)];
            if (mzs.Length == 0)
                return mzs;
            var modMasses = PeptideFragmentMass.ModMassesByPosition(modifications);
            int maxCharge = Math.Min(Math.Max(precursorCharge, 1), MAX_FRAGMENT_CHARGE);
            for (int slot = 0; slot < mzs.Length; slot++)
            {
                int charge = ChargeOf(slot);
                double? mz = charge > maxCharge
                    ? null
                    : PeptideFragmentMass.CalculateFragmentMz(IonTypeOf(slot), OrdinalOf(slot, length),
                        (byte)charge, sequence, modMasses, null);
                mzs[slot] = mz ?? double.NaN;
            }
            return mzs;
        }

        private static int TypeIndex(IonType ionType, int fragmentCharge)
        {
            int offset = ionType == IonType.B ? 0 : 2;
            return offset + (fragmentCharge == 1 ? 0 : 1);
        }
    }
}
