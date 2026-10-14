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
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Hand-built Osprey training records: every ladder ion applicable and in the scan window
    /// (charge 2 ions of a 1+ precursor are not applicable), nothing matched until
    /// <see cref="Match"/>, and the slot of any b or y ion by its ordinal; and the records
    /// written as an Osprey training export, column by column with Osprey's types.
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

        /// <summary>Writes the records as a training export at <paramref name="path"/>, <paramref name="rowsPerGroup"/> to a row group.</summary>
        public static void WriteExport(string path, IReadOnlyList<OspreyTrainingRecord> records,
            IReadOnlyDictionary<string, string> footer, int rowsPerGroup)
        {
            ParquetColumns.Write(path, Columns(records), footer, rowsPerGroup);
        }

        /// <summary>
        /// The export's columns with the types Osprey writes: scalars as their own types, per-ion
        /// and per-modification arrays as little-endian blobs, an empty array as NULL.
        /// </summary>
        public static List<KeyValuePair<string, Array>> Columns(IReadOnlyList<OspreyTrainingRecord> r)
        {
            return new List<KeyValuePair<string, Array>>
            {
                Column(@"entry_id", r.Select(x => x.EntryId).ToArray()),
                Column(@"is_entrapment", r.Select(x => x.IsEntrapment).ToArray()),
                Column(@"sequence", r.Select(x => x.Sequence).ToArray()),
                Column(@"modified_sequence", r.Select(x => x.ModifiedSequence).ToArray()),
                Column(@"mod_positions", r.Select(x => Blob(x.ModPositions)).ToArray()),
                Column(@"mod_masses", r.Select(x => Blob(x.ModMasses)).ToArray()),
                Column(@"mod_unimod_ids", r.Select(x => Blob(x.ModUnimodIds)).ToArray()),
                Column(@"charge", r.Select(x => (byte)x.Charge).ToArray()),
                Column(@"precursor_mz", r.Select(x => x.PrecursorMz).ToArray()),
                Column(@"protein_ids", r.Select(x => x.ProteinIds).ToArray()),
                Column(@"file_name", r.Select(x => x.FileName).ToArray()),
                Column(@"apex_rt", r.Select(x => x.ApexRt).ToArray()),
                Column(@"start_rt", r.Select(x => x.StartRt).ToArray()),
                Column(@"end_rt", r.Select(x => x.EndRt).ToArray()),
                Column(@"n_peak_scans", r.Select(x => x.PeakScanCount).ToArray()),
                Column(@"score", r.Select(x => x.Score).ToArray()),
                Column(@"run_precursor_q", r.Select(x => x.RunPrecursorQ).ToArray()),
                Column(@"mp_fitted", r.Select(x => x.MedianPolishFitted).ToArray()),
                Column(@"mp_residual_mad", r.Select(x => x.MedianPolishResidualMad).ToArray()),
                Column(@"n_same_apex_claimants", r.Select(x => x.SameApexClaimantCount).ToArray()),
                Column(@"n_coeluting_claimants", r.Select(x => x.CoelutingClaimantCount).ToArray()),
                Column(@"ion_mz", r.Select(x => Blob(x.IonMz)).ToArray()),
                Column(@"ion_flags", r.Select(x => Blob(x.IonFlags)).ToArray()),
                Column(@"apex_intensity", r.Select(x => Blob(x.ApexIntensity)).ToArray()),
                Column(@"apex_mz_error", r.Select(x => Blob(x.ApexMzError)).ToArray()),
                Column(@"library_rel_intensity", r.Select(x => Blob(x.LibraryRelIntensity)).ToArray()),
                Column(@"n_finite_scans", r.Select(x => Blob(x.FiniteScanCount)).ToArray()),
                Column(@"xic_start", r.Select(x => Blob(x.XicStart)).ToArray()),
                Column(@"xic_end", r.Select(x => Blob(x.XicEnd)).ToArray()),
                Column(@"xic_max", r.Select(x => Blob(x.XicMax)).ToArray()),
                Column(@"corr_polish", r.Select(x => Blob(x.CorrPolish)).ToArray()),
                Column(@"corr_reference", r.Select(x => Blob(x.CorrReference)).ToArray()),
                Column(@"polish_row_effect", r.Select(x => Blob(x.PolishRowEffect)).ToArray()),
                Column(@"polish_r2", r.Select(x => Blob(x.PolishR2)).ToArray()),
                Column(@"polish_pos_resid_max", r.Select(x => Blob(x.PolishPositiveResidualMax)).ToArray()),
                Column(@"polish_apex_residual", r.Select(x => Blob(x.PolishApexResidual)).ToArray()),
                Column(@"polish_outlier_z", r.Select(x => Blob(x.PolishOutlierZ)).ToArray()),
                Column(@"polish_apex_ratio", r.Select(x => Blob(x.PolishApexRatio)).ToArray()),
                Column(@"polish_rel_intensity", r.Select(x => Blob(x.PolishRelIntensity)).ToArray()),
                Column(@"shared_apex_n", r.Select(x => Blob(x.SharedApexCount)).ToArray()),
                Column(@"shared_coelute_n", r.Select(x => Blob(x.SharedCoeluteCount)).ToArray()),
                Column(@"min_claimant_q", r.Select(x => Blob(x.MinClaimantQ)).ToArray()),
            };
        }

        public static KeyValuePair<string, Array> Column(string name, Array values)
        {
            return new KeyValuePair<string, Array>(name, values);
        }

        private static byte[] Blob<T>(T[] values) where T : struct
        {
            return values.Length == 0 ? null : MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        }

        private static bool IsChargeTwo(int slot)
        {
            int column = slot % AlphabaseFragmentMz.COLUMN_COUNT;
            return column == AlphabaseFragmentMz.B_Z2 || column == AlphabaseFragmentMz.Y_Z2;
        }
    }
}
