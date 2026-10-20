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
    /// its modifications and its proteins - as <see cref="BlibWriter.AddSpectrum(BlibSpectrumRecord)"/> writes
    /// them. Every column of <c>RefSpectra</c> is here, named as the table names it, so a
    /// peptide writer (Osprey's search output, CarafeSharp's predicted library) and a
    /// small-molecule writer fill the same record. The defaults are what BiblioSpec writes
    /// for a column a writer has nothing for: <c>-</c> flanking residues, one copy, empty
    /// small-molecule text (Skyline reads those columns with GetString, so never NULL), and
    /// NULL for the optional numbers. <see cref="BlibReader"/> reads the same record back, with
    /// <see cref="Id"/> set; a column a library of an older schema lacks keeps its default.
    /// </summary>
    public sealed class BlibSpectrumRecord
    {
        public const string NO_FLANKING_RESIDUE = @"-";

        /// <summary>The <c>RefSpectra</c> id a reader found the row under; the writer assigns its own and ignores this.</summary>
        public long Id { get; set; }
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

        /// <summary>
        /// The experiment-wide (global) precursor q-value, written to the optional
        /// <see cref="BlibSchema.PrecursorQValue"/> column when the writer added it
        /// (<see cref="BlibWriter.AddOptionalColumn"/>) and read from it when the library has it;
        /// null where unknown. The three below are its PEP and the peptide's q-value and PEP.
        /// </summary>
        public double? PrecursorQValue { get; set; }
        public double? PrecursorPep { get; set; }
        public double? PeptideQValue { get; set; }
        public double? PeptidePep { get; set; }

        /// <summary>
        /// The peak m/z values as <see cref="BlibWriter.CompressMzs"/> encodes them; null for a
        /// spectrum with no <c>RefSpectraPeaks</c> row at all, as BiblioSpec writes a
        /// precursor-only entry (an empty array is a row of empty blobs).
        /// </summary>
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
    /// One <c>RefSpectraPeakAnnotations</c> row: the peak's index, its ion name (<c>y7</c>,
    /// <c>b3-H2O</c>, or a fragment molecule's name), charge, calculated m/z and the m/z stored
    /// for the peak, which Skyline checks against the peak list to 1e-7. A small-molecule
    /// fragment also carries its neutral formula, InChI key, other keys, adduct and a comment;
    /// a peptide fragment leaves them empty, as the tools writing peptide libraries do.
    /// </summary>
    public sealed class BlibPeakAnnotation
    {
        public BlibPeakAnnotation(int peakIndex, string name, int charge, double mzTheoretical, double mzObserved,
            string formula = null, string inchiKey = null, string otherKeys = null, string adduct = null, string comment = null)
        {
            PeakIndex = peakIndex;
            Name = name;
            Charge = charge;
            MzTheoretical = mzTheoretical;
            MzObserved = mzObserved;
            Formula = formula ?? string.Empty;
            InchiKey = inchiKey ?? string.Empty;
            OtherKeys = otherKeys ?? string.Empty;
            Adduct = adduct ?? string.Empty;
            Comment = comment ?? string.Empty;
        }

        public int PeakIndex { get; }
        public string Name { get; }
        public int Charge { get; }
        public double MzTheoretical { get; }
        public double MzObserved { get; }
        public string Formula { get; }
        public string InchiKey { get; }
        public string OtherKeys { get; }
        public string Adduct { get; }
        public string Comment { get; }
    }

    /// <summary>
    /// Tables a tool keeps in its libraries beside BiblioSpec's: Osprey's score and boundary
    /// tables, CarafeSharp's decoy pairs. Created in the same database, before any row is
    /// written, and indexed when the library is completed; their rows go through
    /// <see cref="BlibWriter.Connection"/> inside the writer's batches. The writer disposes it when
    /// it closes the connection, since prepared statements on that connection must go before it.
    /// </summary>
    public interface IBlibExtensionTables : IDisposable
    {
        /// <summary>Creates the tool's tables; the standard tables already exist.</summary>
        void CreateTables(System.Data.SQLite.SQLiteConnection connection);

        /// <summary>Creates the tool's indexes, after the standard ones.</summary>
        void CreateIndexes(System.Data.SQLite.SQLiteConnection connection);
    }
}
