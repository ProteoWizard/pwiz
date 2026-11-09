/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Linq;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Hand-built Osprey training records: every ladder ion applicable and in the scan window
    /// (charge 2 ions of a 1+ precursor are not applicable), nothing matched until
    /// <see cref="Match"/>, and the slot of any b or y ion by its ordinal; and the records
    /// written as a training export through the shared writer, or rewritten column by column
    /// to make a file the writer would not produce.
    /// </summary>
    internal static class OspreyTestRecords
    {
        public static TrainingRecord NewRecord(string sequence, int charge)
        {
            int slots = AlphabaseFragmentMz.COLUMN_COUNT * Math.Max(0, sequence.Length - 1);
            var record = new TrainingRecord
            {
                FileName = @"run",
                Sequence = sequence,
                ModifiedSequence = sequence,
                Charge = (byte)charge,
                ModPositions = Array.Empty<int>(),
                ModMasses = Array.Empty<double>(),
                ModUnimodIds = Array.Empty<int>(),
                NSlots = slots,
                IonMz = Enumerable.Range(0, slots).Select(s => 300.0 + s).ToArray(),
                IonFlags = new byte[slots],
                ApexIntensity = new float[slots],
                ApexMzError = new float[slots],
                LibraryRelIntensity = new float[slots],
                NFiniteScans = new ushort[slots],
                XicStart = new float[slots],
                XicEnd = new float[slots],
                XicMax = new float[slots],
                CorrPolish = Enumerable.Repeat(float.NaN, slots).ToArray(),
                CorrReference = Enumerable.Repeat(float.NaN, slots).ToArray(),
                PolishRowEffect = new float[slots],
                PolishR2 = new float[slots],
                PolishPosResidMax = new float[slots],
                PolishApexResidual = new float[slots],
                PolishOutlierZ = new float[slots],
                PolishApexRatio = new float[slots],
                PolishRelIntensity = new float[slots],
                SharedApexN = new byte[slots],
                SharedCoeluteN = new byte[slots],
                MinClaimantQ = Enumerable.Repeat(float.NaN, slots).ToArray(),
                MpResidualMad = double.NaN,
            };
            for (int slot = 0; slot < slots; slot++)
            {
                if (IsChargeTwo(slot) && charge < 2)
                    record.IonMz[slot] = double.NaN;
                else
                    record.IonFlags[slot] = TrainingIonFlags.APPLICABLE | TrainingIonFlags.IN_SCAN_RANGE;
            }
            return record;
        }

        /// <summary>Marks an ion matched at the apex, with the same correlation to both profiles.</summary>
        public static void Match(TrainingRecord record, int slot, float intensity, float correlation)
        {
            record.IonFlags[slot] |= TrainingIonFlags.MATCHED_AT_APEX;
            record.ApexIntensity[slot] = intensity;
            record.XicMax[slot] = intensity;
            record.CorrPolish[slot] = correlation;
            record.CorrReference[slot] = correlation;
        }

        /// <summary>Matches every charge 1 ion of the ladder with a clean profile, intensity rising with the slot.</summary>
        public static TrainingRecord CleanRecord(string sequence, int charge)
        {
            var record = NewRecord(sequence, charge);
            for (int slot = 0; slot < record.NSlots; slot++)
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

        /// <summary>Writes the records as a training export at <paramref name="path"/>, <paramref name="rowsPerGroup"/> to a row group.</summary>
        public static void WriteExport(string path, IReadOnlyList<TrainingRecord> records,
            IReadOnlyDictionary<string, string> footer, int rowsPerGroup)
        {
            TrainingExportParquet.Write(path, records, footer, false, rowsPerGroup);
        }

        /// <summary>
        /// Rewrites the export at <paramref name="source"/> to <paramref name="path"/> column by
        /// column, with <paramref name="footer"/> as written (no format version is added) and
        /// <paramref name="edit"/> applied to the columns: a file the shared writer would never
        /// produce, for the reader's refusals and its tolerance of an older format.
        /// </summary>
        public static void Rewrite(string source, string path, IReadOnlyDictionary<string, string> footer,
            Func<List<KeyValuePair<string, Array>>, List<KeyValuePair<string, Array>>> edit)
        {
            var names = TrainingExportParquet.ColumnNames(false).ToArray();
            var read = ParquetColumns.Read(source, names);
            var columns = names.Select(name => Column(name, read.GetRaw(name))).ToList();
            ParquetColumns.Write(path, edit(columns), footer, 2);
        }

        public static KeyValuePair<string, Array> Column(string name, Array values)
        {
            return new KeyValuePair<string, Array>(name, values);
        }

        private static bool IsChargeTwo(int slot)
        {
            int column = slot % AlphabaseFragmentMz.COLUMN_COUNT;
            return column == AlphabaseFragmentMz.B_Z2 || column == AlphabaseFragmentMz.Y_Z2;
        }
    }
}
