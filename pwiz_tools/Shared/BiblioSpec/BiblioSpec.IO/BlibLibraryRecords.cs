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

namespace pwiz.BiblioSpec.IO
{
    /// <summary>
    /// The <c>LibInfo</c> row: what a library says about itself as a whole, as
    /// <see cref="BlibReader.Info"/> reads it. <see cref="SpectrumCount"/> is -1 in a library
    /// still being written and has been 0 in a damaged one, so a reader that needs the count
    /// falls back to <see cref="BlibReader.CountSpectra"/>.
    /// </summary>
    public sealed class BlibLibraryInfo
    {
        public BlibLibraryInfo(string lsid, string createTime, int spectrumCount, int revision, int schemaVersion)
        {
            Lsid = lsid;
            CreateTime = createTime;
            SpectrumCount = spectrumCount;
            Revision = revision;
            SchemaVersion = schemaVersion;
        }

        public string Lsid { get; }
        /// <summary>The creation time in ctime() format, as <see cref="BlibSchema.FormatCreateTime"/> writes it.</summary>
        public string CreateTime { get; }
        /// <summary><c>numSpecs</c>.</summary>
        public int SpectrumCount { get; }
        /// <summary><c>majorVersion</c>: the build of this library, from 1.</summary>
        public int Revision { get; }
        /// <summary><c>minorVersion</c>: the schema version (<see cref="BlibSchema.CurrentMinorVersion"/>).</summary>
        public int SchemaVersion { get; }
        /// <summary>True for a redundant library, by its LSID; a redundant library has no <c>RetentionTimes</c> table.</summary>
        public bool IsRedundant
        {
            get { return Lsid != null && Lsid.Contains(@":redundant:"); }
        }
    }

    /// <summary>
    /// One <c>Proteins</c> row: a protein, or a protein group as its accessions joined by
    /// semicolons, with the global q-value and PEP of the optional columns
    /// (<see cref="BlibSchema.ProteinQValue"/>, <see cref="BlibSchema.ProteinPep"/>) where the
    /// library has them.
    /// </summary>
    public sealed class BlibProtein
    {
        public BlibProtein(long id, string accession, double? qValue, double? pep)
        {
            Id = id;
            Accession = accession;
            QValue = qValue;
            Pep = pep;
        }

        public long Id { get; }
        public string Accession { get; }
        public double? QValue { get; }
        public double? Pep { get; }
    }

    /// <summary>One <c>SpectrumSourceFiles</c> row, as <see cref="BlibWriter.AddSourceFile"/> writes it.</summary>
    public sealed class BlibSourceFile
    {
        public BlibSourceFile(long id, string fileName, string idFileName, double? cutoffScore, int workflowType)
        {
            Id = id;
            FileName = fileName;
            IdFileName = idFileName;
            CutoffScore = cutoffScore;
            WorkflowType = workflowType;
        }

        /// <summary>The id <c>RefSpectra.fileID</c> and <c>RetentionTimes.SpectrumSourceID</c> refer to.</summary>
        public long Id { get; }
        public string FileName { get; }
        /// <summary>The identification file, or null.</summary>
        public string IdFileName { get; }
        public double? CutoffScore { get; }
        /// <summary><see cref="BlibWriter.WORKFLOW_TYPE_DDA"/> or <see cref="BlibWriter.WORKFLOW_TYPE_DIA"/>; DDA in a library before the column.</summary>
        public int WorkflowType { get; }
    }

    /// <summary>
    /// One <c>RetentionTimes</c> row: an observation of a spectrum in a source file, as
    /// <see cref="BlibWriter.AddRetentionTime"/> writes it. The ion mobility columns are those
    /// of schema 6 and later; a library before that (drift time columns under other names)
    /// reads them as null.
    /// </summary>
    public sealed class BlibRetentionTimeRecord
    {
        public long RefSpectraId { get; set; }
        /// <summary>The observation's own spectrum in the redundant library, or 0 when the library was not filtered from one.</summary>
        public long RedundantRefSpectraId { get; set; }
        /// <summary>The <c>SpectrumSourceFiles</c> id, or null.</summary>
        public long? SpectrumSourceId { get; set; }
        public double? IonMobility { get; set; }
        public double? CollisionalCrossSectionSqA { get; set; }
        public double? IonMobilityHighEnergyOffset { get; set; }
        public int IonMobilityType { get; set; } = BlibWriter.ION_MOBILITY_TYPE_NONE;
        public double? RetentionTime { get; set; }
        public double? StartTime { get; set; }
        public double? EndTime { get; set; }
        public double? Score { get; set; }
        public bool BestSpectrum { get; set; }
    }

    /// <summary>
    /// One <c>DecoyPairs</c> row (<see cref="BlibSchema.CreateDecoyPairs"/>): a spectrum's place
    /// in a library's decoy and entrapment design, as <see cref="BlibWriter.AddDecoyPair"/>
    /// writes it.
    /// </summary>
    public readonly struct BlibDecoyPair
    {
        public const string TABLE = @"DecoyPairs";

        public BlibDecoyPair(bool isDecoy, bool isEntrapment, int pairingId, long? targetRefSpectraId = null, string method = null)
        {
            IsDecoy = isDecoy;
            IsEntrapment = isEntrapment;
            PairingId = pairingId;
            TargetRefSpectraId = targetRefSpectraId;
            Method = method;
        }

        public bool IsDecoy { get; }
        /// <summary>True for an entrapment target or entrapment decoy (FDRBench's p_target / p_decoy).</summary>
        public bool IsEntrapment { get; }
        /// <summary>
        /// The pairing manifest's <c>peptide_pair_index</c>: shared by a target, its entrapment
        /// target and both their decoys, the group an entrapment peptide is paired with its
        /// original target by.
        /// </summary>
        public int PairingId { get; }
        /// <summary>A decoy's target spectrum, at the same charge; null on a target or a decoy with no target.</summary>
        public long? TargetRefSpectraId { get; }
        /// <summary>How a decoy was made (<c>reverse</c>, <c>cycle</c>); null on a target or when unknown.</summary>
        public string Method { get; }
    }

    /// <summary>The decoded peaks of one spectrum: m/z values and intensities, index for index.</summary>
    public sealed class BlibPeaks
    {
        public BlibPeaks(double[] mzs, float[] intensities)
        {
            Mzs = mzs;
            Intensities = intensities;
        }

        public double[] Mzs { get; }
        public float[] Intensities { get; }
        public int Count
        {
            get { return Mzs.Length; }
        }
    }

    /// <summary>
    /// A column a library has only when its writer had values for it, so a library without
    /// them pays no size or read cost: the experiment-wide (global) q-values and PEPs of
    /// <see cref="BlibSchema.OptionalRefSpectraColumns"/> and
    /// <see cref="BlibSchema.OptionalProteinsColumns"/>. Added with <c>ALTER TABLE</c> by
    /// <see cref="BlibWriter.AddOptionalColumn"/>; a reader tests for the column, never for a
    /// schema version, and BlibFilter carries it from the redundant library to the non-redundant
    /// one while a BlibBuild merge drops it, since a global value from one experiment means
    /// nothing next to one from another.
    /// </summary>
    public sealed class BlibOptionalColumn
    {
        public BlibOptionalColumn(string table, string name, string type)
        {
            Table = table;
            Name = name;
            Type = type;
        }

        public string Table { get; }
        public string Name { get; }
        /// <summary>The SQLite type the column is added with.</summary>
        public string Type { get; }

        public override string ToString()
        {
            return Table + @"." + Name;
        }
    }
}
