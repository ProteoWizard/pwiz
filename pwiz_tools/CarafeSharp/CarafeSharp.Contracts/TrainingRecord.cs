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

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// One row of <c>&lt;stem&gt;.training.parquet</c>: an identified target precursor in one
    /// run, its final peak, and the per-ion evidence for every slot of its b/y ladder. The
    /// schema - names, types, units and the meaning of every NaN - is Osprey's
    /// docs/22-training-export.md, the contract a consumer reads; this type is its in-memory
    /// form, shared by the evidence code that fills it (Osprey.Scoring), the writer
    /// (<see cref="TrainingExportParquet"/>) and the consumer that trains on it
    /// (CarafeSharp.Training). Property names follow the column names.
    ///
    /// <para>Per-ion arrays all have <see cref="NSlots"/> = <c>4 * (L - 1)</c> elements for a
    /// peptide of <c>L</c> residues, in AlphaPeptDeep's order: <c>slot = p * 4 + t</c>, t = b1+,
    /// b2+, y1+, y2+; position p holds b(p+1) and y(L-1-p). A slot that is not applicable
    /// (fragment charge above <c>min(precursor charge, 2)</c>) carries NaN in every float
    /// array and 0 in every integer array, and no <see cref="TrainingIonFlags.APPLICABLE"/>
    /// bit.</para>
    ///
    /// <para>Modifications: <see cref="ModPositions"/> are 0-based residue positions. An
    /// N-terminal modification and one of the first residue both sit at position 0
    /// (<c>(UniMod:1)M(UniMod:35)</c>), listed separately and in that order, so only the
    /// first modification at position 0 can be the terminal one.</para>
    /// </summary>
    public sealed class TrainingRecord
    {
        // ---- Identity -------------------------------------------------------------------
        public uint EntryId { get; set; }
        public bool IsDecoy { get; set; }
        public bool IsEntrapment { get; set; }
        public string PeptideKind { get; set; }
        public string Sequence { get; set; }
        public string ModifiedSequence { get; set; }
        public int[] ModPositions { get; set; }
        public double[] ModMasses { get; set; }
        /// <summary>UniMod id of each modification, -1 when the library did not give one.</summary>
        public int[] ModUnimodIds { get; set; }
        public byte Charge { get; set; }
        public double PrecursorMz { get; set; }
        public double LibraryRt { get; set; }
        public string ProteinIds { get; set; }
        public string FileName { get; set; }

        // ---- The final peak --------------------------------------------------------------
        public uint ScanNumber { get; set; }
        public double ApexRt { get; set; }
        public double StartRt { get; set; }
        public double EndRt { get; set; }
        public int NPeakScans { get; set; }
        public double IsolationLower { get; set; }
        public double IsolationUpper { get; set; }
        public double BoundsArea { get; set; }
        public double CoelutionSum { get; set; }

        // ---- Scores ----------------------------------------------------------------------
        public double Score { get; set; }
        public double RunPrecursorQ { get; set; }
        public double RunPeptideQ { get; set; }

        // ---- Apex-spectrum summary -------------------------------------------------------
        public double ApexTic { get; set; }
        public double ExplainedIntensity { get; set; }
        public int NSlots { get; set; }
        public int NIonsApplicable { get; set; }
        public int NIonsObserved { get; set; }

        // ---- Osprey's median polish over the final boundaries ----------------------------
        public bool MpFitted { get; set; }
        public bool MpConverged { get; set; }
        public int MpIterations { get; set; }
        public double MpOverall { get; set; }
        public double MpCosine { get; set; }
        public bool MpCosineParity { get; set; }
        /// <summary>Median absolute residual of the core fit, ln units; NaN without a fit.</summary>
        public double MpResidualMad { get; set; }
        public int MpNCore { get; set; }
        public int MpNFragmentsUsed { get; set; }
        public double BoundaryStartRatioMedian { get; set; }
        public double BoundaryEndRatioMedian { get; set; }

        // ---- Competing precursors --------------------------------------------------------
        public int NCoelutingClaimants { get; set; }
        public int NSameApexClaimants { get; set; }
        public int DdcNeighborCount { get; set; }

        // ---- Per ion (NSlots each) -------------------------------------------------------
        /// <summary>Theoretical m/z of each slot, NaN when not applicable.</summary>
        public double[] IonMz { get; set; }
        /// <summary>Bits of <see cref="TrainingIonFlags"/> per slot.</summary>
        public byte[] IonFlags { get; set; }
        public float[] ApexIntensity { get; set; }
        public float[] ApexMzError { get; set; }
        public float[] LibraryRelIntensity { get; set; }
        public ushort[] NFiniteScans { get; set; }
        public float[] XicStart { get; set; }
        public float[] XicEnd { get; set; }
        public float[] XicMax { get; set; }
        public float[] CorrPolish { get; set; }
        public float[] CorrReference { get; set; }
        public float[] PolishRowEffect { get; set; }
        public float[] PolishR2 { get; set; }
        public float[] PolishPosResidMax { get; set; }
        public float[] PolishApexResidual { get; set; }
        public float[] PolishOutlierZ { get; set; }
        public float[] PolishApexRatio { get; set; }
        public float[] PolishRelIntensity { get; set; }
        public byte[] SharedApexN { get; set; }
        public byte[] SharedCoeluteN { get; set; }
        /// <summary>Lowest run q among the claimants sharing each ion, NaN when none.</summary>
        public float[] MinClaimantQ { get; set; }

        // ---- Optional XIC matrix (--training-export-xics) --------------------------------
        /// <summary>Retention time of each peak scan (NPeakScans), or null when not written.</summary>
        public double[] XicRts { get; set; }

        /// <summary>
        /// Observed intensity of every slot at every peak scan, slot-major
        /// (<c>[slot * NPeakScans + scan]</c>), or null when not written.
        /// </summary>
        public float[] XicIntensities { get; set; }

        /// <summary>True when <paramref name="slot"/> carries the <paramref name="flag"/> bit.</summary>
        public bool Has(int slot, byte flag)
        {
            return (IonFlags[slot] & flag) != 0;
        }
    }

    /// <summary>
    /// Bits of <see cref="TrainingRecord.IonFlags"/>.
    /// </summary>
    public static class TrainingIonFlags
    {
        /// <summary>The slot's fragment charge is at most <c>min(precursor charge, 2)</c> and its m/z is defined.</summary>
        public const byte APPLICABLE = 1;
        /// <summary>The ion m/z lies inside the run's MS2 scan window (set for every applicable ion when the window is unknown).</summary>
        public const byte IN_SCAN_RANGE = 2;
        /// <summary>A peak was found within the calibrated tolerance in the apex spectrum.</summary>
        public const byte MATCHED_AT_APEX = 4;
        /// <summary>One of the library fragments Osprey's median polish was fit to.</summary>
        public const byte CORE = 8;
        /// <summary>The library holds this ion by annotation (ion type, ordinal, charge).</summary>
        public const byte LIBRARY_ANNOTATED = 16;
        /// <summary>The library holds an unannotated fragment matched to this ion by m/z.</summary>
        public const byte LIBRARY_MZ_MATCHED = 32;
        /// <summary>A claimant sharing this ion's apex peak has a better run q (or equal q and a higher score).</summary>
        public const byte BETTER_CLAIMANT_APEX = 64;
        /// <summary>A co-eluting claimant with this ion in its ladder has a better run q (or equal q and a higher score).</summary>
        public const byte BETTER_CLAIMANT_COELUTE = 128;
    }
}
