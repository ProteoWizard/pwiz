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
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Hand-built Osprey training records: every ladder ion applicable and in the scan window
    /// (charge 2 ions of a 1+ precursor are not applicable), nothing matched until
    /// <see cref="Match"/>, and the slot of any b or y ion by its ordinal.
    /// </summary>
    internal static class OspreyTestRecords
    {
        public static OspreyTrainingRecord NewRecord(string sequence, int charge)
        {
            int slots = AlphabaseFragmentMz.COLUMN_COUNT * Math.Max(0, sequence.Length - 1);
            var record = new OspreyTrainingRecord
            {
                FileName = @"run",
                Sequence = sequence,
                ModifiedSequence = sequence,
                Charge = charge,
                ModPositions = Array.Empty<int>(),
                ModMasses = Array.Empty<double>(),
                ModUnimodIds = Array.Empty<int>(),
                IonMz = Enumerable.Range(0, slots).Select(s => 300.0 + s).ToArray(),
                IonFlags = new byte[slots],
                ApexIntensity = new float[slots],
                ApexMzError = new float[slots],
                LibraryRelIntensity = new float[slots],
                FiniteScanCount = new ushort[slots],
                XicStart = new float[slots],
                XicEnd = new float[slots],
                XicMax = new float[slots],
                CorrPolish = Enumerable.Repeat(float.NaN, slots).ToArray(),
                CorrReference = Enumerable.Repeat(float.NaN, slots).ToArray(),
                PolishRowEffect = new float[slots],
                PolishR2 = new float[slots],
                PolishPositiveResidualMax = new float[slots],
                PolishApexResidual = new float[slots],
                PolishOutlierZ = new float[slots],
                PolishApexRatio = new float[slots],
                PolishRelIntensity = new float[slots],
                SharedApexCount = new byte[slots],
                SharedCoeluteCount = new byte[slots],
                MinClaimantQ = Enumerable.Repeat(float.NaN, slots).ToArray(),
                MedianPolishResidualMad = double.NaN,
            };
            for (int slot = 0; slot < slots; slot++)
            {
                if (IsChargeTwo(slot) && charge < 2)
                    record.IonMz[slot] = double.NaN;
                else
                    record.IonFlags[slot] = OspreyIonFlags.APPLICABLE | OspreyIonFlags.IN_SCAN_RANGE;
            }
            return record;
        }

        /// <summary>Marks an ion matched at the apex, with the same correlation to both profiles.</summary>
        public static void Match(OspreyTrainingRecord record, int slot, float intensity, float correlation)
        {
            record.IonFlags[slot] |= OspreyIonFlags.MATCHED_AT_APEX;
            record.ApexIntensity[slot] = intensity;
            record.XicMax[slot] = intensity;
            record.CorrPolish[slot] = correlation;
            record.CorrReference[slot] = correlation;
        }

        /// <summary>Matches every charge 1 ion of the ladder with a clean profile, intensity rising with the slot.</summary>
        public static OspreyTrainingRecord CleanRecord(string sequence, int charge)
        {
            var record = NewRecord(sequence, charge);
            for (int slot = 0; slot < record.SlotCount; slot++)
            {
                if (!IsChargeTwo(slot))
                    Match(record, slot, 100 + slot, 0.95f);
            }
            return record;
        }

        /// <summary>The slot of b<paramref name="ordinal"/> at <paramref name="fragmentCharge"/>.</summary>
        public static int BSlot(int ordinal, int fragmentCharge = 1)
        {
            return (ordinal - 1) * AlphabaseFragmentMz.COLUMN_COUNT + AlphabaseFragmentMz.B_Z1 + fragmentCharge - 1;
        }

        /// <summary>The slot of y<paramref name="ordinal"/> of <paramref name="sequence"/> at <paramref name="fragmentCharge"/>.</summary>
        public static int YSlot(string sequence, int ordinal, int fragmentCharge = 1)
        {
            return (sequence.Length - 1 - ordinal) * AlphabaseFragmentMz.COLUMN_COUNT + AlphabaseFragmentMz.Y_Z1 + fragmentCharge - 1;
        }

        private static bool IsChargeTwo(int slot)
        {
            int column = slot % AlphabaseFragmentMz.COLUMN_COUNT;
            return column == AlphabaseFragmentMz.B_Z2 || column == AlphabaseFragmentMz.Y_Z2;
        }
    }
}
