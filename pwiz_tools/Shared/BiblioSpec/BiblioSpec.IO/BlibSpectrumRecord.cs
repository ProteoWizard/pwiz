/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// One <c>RefSpectra</c> row and the rows that hang off it - its peaks, their annotations,
    /// its modifications and its proteins - as <see cref="BlibWriter.AddSpectrum"/> writes
    /// them. Every column of <c>RefSpectra</c> is here, named as the table names it, so a
    /// peptide writer (Osprey's search output, CarafeSharp's predicted library) and a
    /// small-molecule writer fill the same record. The defaults are what BiblioSpec writes
    /// for a column a writer has nothing for: <c>-</c> flanking residues, one copy, empty
    /// small-molecule text (Skyline reads those columns with GetString, so never NULL), and
    /// NULL for the optional numbers.
    /// </summary>
    public sealed class BlibSpectrumRecord
    {
        public const string NO_FLANKING_RESIDUE = @"-";

        public string PeptideSeq { get; set; }
        public double PrecursorMz { get; set; }
        public int PrecursorCharge { get; set; }
        /// <summary>The modified sequence in BiblioSpec's mass-shift notation, <c>PEPTC[+57.0215]K</c>.</summary>
        public string PeptideModSeq { get; set; }
        public string PrevAA { get; set; } = NO_FLANKING_RESIDUE;
        public string NextAA { get; set; } = NO_FLANKING_RESIDUE;
        public int Copies { get; set; } = 1;
        /// <summary>The ion mobility value, in the units <see cref="IonMobilityType"/> names, or null.</summary>
        public double? IonMobility { get; set; }
        public double? CollisionalCrossSectionSqA { get; set; }
        public double? IonMobilityHighEnergyOffset { get; set; }
        /// <summary>An <c>IonMobilityTypes</c> id, <see cref="BlibWriter.ION_MOBILITY_TYPE_NONE"/> by default.</summary>
        public int IonMobilityType { get; set; } = BlibWriter.ION_MOBILITY_TYPE_NONE;
        public double? RetentionTime { get; set; }
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
        public double? TotalIonCurrent { get; set; }
        public string MoleculeName { get; set; } = string.Empty;
        public string ChemicalFormula { get; set; } = string.Empty;
        public string PrecursorAdduct { get; set; } = string.Empty;
        public string InchiKey { get; set; } = string.Empty;
        public string OtherKeys { get; set; } = string.Empty;
        /// <summary>The <c>SpectrumSourceFiles</c> id <see cref="BlibWriter.AddSourceFile"/> returned.</summary>
        public long FileId { get; set; }
        /// <summary>The spectrum's label in its source file, or null.</summary>
        public string SpecIdInFile { get; set; }
        public double Score { get; set; }
        /// <summary>A <c>ScoreTypes</c> id, <see cref="BlibWriter.SCORE_TYPE_UNKNOWN"/> by default.</summary>
        public int ScoreType { get; set; } = BlibWriter.SCORE_TYPE_UNKNOWN;

        /// <summary>The peak m/z values as <see cref="BlibWriter.CompressMzs"/> encodes them.</summary>
        public byte[] MzBlob { get; set; }
        /// <summary>The peak intensities as <see cref="BlibWriter.CompressIntensities"/> encodes them.</summary>
        public byte[] IntensityBlob { get; set; }
        public int NumPeaks { get; set; }

        /// <summary>The <c>Modifications</c> rows, 1-based positions; none by default.</summary>
        public IReadOnlyList<BlibModification> Modifications { get; set; } = Array.Empty<BlibModification>();

        /// <summary>The protein accessions, each mapped through <c>RefSpectraProteins</c>; none by default.</summary>
        public IReadOnlyList<string> Proteins { get; set; } = Array.Empty<string>();

        /// <summary>The <c>RefSpectraPeakAnnotations</c> rows, in peak order; none by default.</summary>
        public IReadOnlyList<BlibPeakAnnotation> Annotations { get; set; } = Array.Empty<BlibPeakAnnotation>();
    }

    /// <summary>One <c>Modifications</c> row: the modified residue, 1-based, and its mass shift.</summary>
    public sealed class BlibModification
    {
        public BlibModification(int position, double mass)
        {
            Position = position;
            Mass = mass;
        }

        public int Position { get; }
        public double Mass { get; }
    }

    /// <summary>
    /// One <c>RefSpectraPeakAnnotations</c> row of a peptide fragment: the peak's index, its
    /// ion name (<c>y7</c>, <c>b3-H2O</c>), charge, calculated m/z and the m/z stored for the
    /// peak, which Skyline checks against the peak list to 1e-7. The small-molecule text columns
    /// (formula, InChI key, other keys, adduct, comment) are written empty.
    /// </summary>
    public sealed class BlibPeakAnnotation
    {
        public BlibPeakAnnotation(int peakIndex, string name, int charge, double mzTheoretical, double mzObserved)
        {
            PeakIndex = peakIndex;
            Name = name;
            Charge = charge;
            MzTheoretical = mzTheoretical;
            MzObserved = mzObserved;
        }

        public int PeakIndex { get; }
        public string Name { get; }
        public int Charge { get; }
        public double MzTheoretical { get; }
        public double MzObserved { get; }
    }

    /// <summary>
    /// Tables a tool keeps in its libraries beside BiblioSpec's: Osprey's score and boundary
    /// tables, CarafeSharp's decoy pairs. Created in the same database, before any row is
    /// written, and indexed when the library is completed; their rows go through
    /// <see cref="BlibWriter.Connection"/> inside the writer's batches.
    /// </summary>
    public interface IBlibExtensionTables
    {
        /// <summary>Creates the tool's tables; the standard tables already exist.</summary>
        void CreateTables(System.Data.SQLite.SQLiteConnection connection);

        /// <summary>Creates the tool's indexes, after the standard ones.</summary>
        void CreateIndexes(System.Data.SQLite.SQLiteConnection connection);
    }
}
