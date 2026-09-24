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

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Bits of the <c>ion_flags</c> blob in Osprey's training export
    /// (pwiz_tools/Osprey/docs/22-training-export.md).
    /// </summary>
    public static class OspreyIonFlags
    {
        public const byte APPLICABLE = 1;
        public const byte IN_SCAN_RANGE = 2;
        public const byte MATCHED_AT_APEX = 4;
        public const byte CORE = 8;
        public const byte LIBRARY_ANNOTATED = 16;
        public const byte LIBRARY_MZ_MATCHED = 32;
        public const byte BETTER_CLAIMANT_APEX = 64;
        public const byte BETTER_CLAIMANT_COELUTE = 128;
    }

    /// <summary>
    /// One precursor row of an Osprey <c>&lt;stem&gt;.training.parquet</c>: a confidently
    /// identified target with the evidence for every b and y ion of its ladder. Per-ion arrays
    /// have <c>4 * (L - 1)</c> slots in AlphaPeptDeep order (<c>slot = p * 4 + t</c>, t = b1+, b2+,
    /// y1+, y2+; position p holds b(p+1) and y(L-1-p)), exactly the layout of the MS2 model output.
    /// </summary>
    public sealed class OspreyTrainingRecord
    {
        public string FileName { get; set; }
        public uint EntryId { get; set; }
        public bool IsEntrapment { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }

        /// <summary>0-based residue position of each modification.</summary>
        public int[] ModPositions { get; set; }

        public double[] ModMasses { get; set; }

        /// <summary>UniMod id of each modification, -1 when unknown.</summary>
        public int[] ModUnimodIds { get; set; }

        public int Charge { get; set; }
        public double PrecursorMz { get; set; }
        public string ProteinIds { get; set; }
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public int PeakScanCount { get; set; }
        public double Score { get; set; }
        public double RunPrecursorQ { get; set; }
        public double ExperimentPrecursorQ { get; set; }
        public double Pep { get; set; }

        public bool MedianPolishFitted { get; set; }

        /// <summary>Median absolute residual of the core fit, ln units; NaN without a fit.</summary>
        public double MedianPolishResidualMad { get; set; }

        public int SameApexClaimantCount { get; set; }
        public int CoelutingClaimantCount { get; set; }

        /// <summary>Theoretical m/z of each slot, NaN when not applicable.</summary>
        public double[] IonMz { get; set; }

        public byte[] IonFlags { get; set; }
        public float[] ApexIntensity { get; set; }
        public float[] ApexMzError { get; set; }
        public float[] LibraryRelIntensity { get; set; }
        public ushort[] FiniteScanCount { get; set; }
        public float[] XicStart { get; set; }
        public float[] XicEnd { get; set; }
        public float[] XicMax { get; set; }
        public float[] CorrPolish { get; set; }
        public float[] CorrReference { get; set; }
        public float[] PolishRowEffect { get; set; }
        public float[] PolishR2 { get; set; }
        public float[] PolishPositiveResidualMax { get; set; }
        public float[] PolishApexResidual { get; set; }
        public float[] PolishOutlierZ { get; set; }
        public float[] PolishApexRatio { get; set; }
        public float[] PolishRelIntensity { get; set; }
        public byte[] SharedApexCount { get; set; }
        public byte[] SharedCoeluteCount { get; set; }

        /// <summary>Lowest run q among the claimants sharing each ion, NaN when none.</summary>
        public float[] MinClaimantQ { get; set; }

        public int SlotCount
        {
            get { return IonFlags.Length; }
        }

        public bool Has(int slot, byte flag)
        {
            return (IonFlags[slot] & flag) != 0;
        }
    }
}
