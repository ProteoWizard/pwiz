/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Pipeline;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Osprey's rows of a <c>.blib</c>: a <see cref="BlibWriter"/>, the one writer every tool of
    /// ours writes the format through, and the column values Osprey's search output and
    /// <c>--export-library</c> have always written (Rust osprey's <c>blib.rs</c>): a
    /// <c>GENERIC Q-VALUE</c> score, the spectrum's ordinal as its id in file, zero rather than
    /// NULL for the ion mobility columns and the total ion current, the apex and bounds in the
    /// spectrum row, and the peptide text through <see cref="BlibPeptideText"/>. Osprey keeps no
    /// table of its own beside the standard ones: what it records about the library as a whole
    /// goes in <c>LibMetadata</c> (<see cref="BlibMetadata"/>) under the <c>osprey.</c> keys
    /// below, and every per-spectrum value is in <c>RefSpectra</c> and <c>RetentionTimes</c>.
    /// The library is written beside its path and moved over it by <see cref="Complete"/>;
    /// disposed without that, nothing at the path changes.
    /// </summary>
    public sealed class OspreyBlibWriter : IDisposable
    {
        /// <summary>The Osprey build that wrote the library (<c>OspreyVersion.Current</c>).</summary>
        public const string KEY_VERSION = @"osprey.version";
        /// <summary>The full command line of the run that wrote the library.</summary>
        public const string KEY_COMMAND = @"osprey.command";
        /// <summary>A search output's search mode (<c>coelution</c>).</summary>
        public const string KEY_SEARCH_MODE = @"osprey.search_mode";
        /// <summary>A search output's run-level FDR threshold.</summary>
        public const string KEY_RUN_FDR = @"osprey.run_fdr";
        /// <summary>A search output's experiment-level FDR threshold.</summary>
        public const string KEY_EXPERIMENT_FDR = @"osprey.experiment_fdr";
        /// <summary>An exported library's source: the library file it was loaded from.</summary>
        public const string KEY_LIBRARY_SOURCE = @"osprey.library_source";
        /// <summary>
        /// A derived library's validity: the hash of the source library's composition and the
        /// reader that reads the derived file back (<c>LibraryLoader.DerivedLibraryHash</c>).
        /// The loader reuses the file only while the hash it computes equals this.
        /// </summary>
        public const string KEY_LIBRARY_HASH = @"osprey.library_hash";

        /// <summary>
        /// The validity stamp recorded in a blib's <c>LibMetadata</c> (or, for one release, its
        /// <c>OspreyMetadata</c>) table, or null when the file is missing, unreadable or
        /// unstamped. Never throws.
        /// </summary>
        public static ArtifactStamp ReadStamp(string path)
        {
            return ArtifactStamp.Parse(BlibMetadata.Read(path, ArtifactStamp.PARQUET_KEY));
        }

        private readonly BlibWriter _writer;

        /// <summary>Starts the library that <see cref="Complete"/> writes to <paramref name="path"/>.</summary>
        public OspreyBlibWriter(string path)
        {
            _writer = new BlibWriter(path, Path.GetFileName(path));
        }

        /// <summary>Begins a transaction over the rows that follow, for fast inserts.</summary>
        public void BeginBatch()
        {
            _writer.BeginBatch();
        }

        /// <summary>Commits the batch <see cref="BeginBatch"/> began.</summary>
        public void CommitBatch()
        {
            _writer.CommitBatch();
        }

        /// <summary>
        /// Adds a DIA spectrum source file and returns its id: <paramref name="fileName"/> the
        /// acquisition, <paramref name="idFileName"/> the library it was searched against, and
        /// <paramref name="cutoffScore"/> the q-value the spectra were filtered at.
        /// </summary>
        public long AddSourceFile(string fileName, string idFileName, double cutoffScore)
        {
            return _writer.AddSourceFile(fileName, idFileName, cutoffScore, BlibWriter.WORKFLOW_TYPE_DIA);
        }

        /// <summary>
        /// Adds the experiment-level columns a search result fills, the optional BiblioSpec
        /// columns Skyline filters on: the precursor's q-value and PEP
        /// (<see cref="BlibSchema.PrecursorQValue"/>, <see cref="BlibSchema.PrecursorPep"/>), the
        /// peptide's q-value (<see cref="BlibSchema.PeptideQValue"/>) and the protein group's
        /// q-value (<see cref="BlibSchema.ProteinQValue"/>). A blib gets these columns only when
        /// it has values for them, so a library export never calls this. Once it is called, every
        /// <c>Proteins</c> row is a protein group written by <see cref="AddProteinGroup"/> and
        /// every spectrum carries its <see cref="SearchGlobalValues"/>. Call before
        /// <see cref="BeginBatch"/>.
        /// </summary>
        public void AddSearchGlobalColumns()
        {
            _writer.AddOptionalColumn(BlibSchema.PrecursorQValue);
            _writer.AddOptionalColumn(BlibSchema.PrecursorPep);
            _writer.AddOptionalColumn(BlibSchema.PeptideQValue);
            _writer.AddOptionalColumn(BlibSchema.ProteinQValue);
        }

        /// <summary>
        /// Writes a library precursor prepared by <see cref="BlibSpectrum.FromLibraryEntry"/>: its
        /// <c>RefSpectra</c> and peak rows, then its modification and protein rows. The retention
        /// times, score, source file and copy count are what the caller knows about this row - a
        /// search result's apex, bounds, q-value and run count, or a library's own retention time.
        /// A search result also gives its <paramref name="globalValues"/>, for the columns
        /// <see cref="AddSearchGlobalColumns"/> added. Returns the <c>RefSpectra</c> id.
        /// </summary>
        public long AddSpectrum(BlibSpectrum spectrum, double retentionTime, double startTime, double endTime,
            double score, long fileId, int copies, SearchGlobalValues globalValues = null)
        {
            return AddSpectrum(spectrum.PeptideSeq, spectrum.ModifiedSequence, spectrum.PrecursorMz, spectrum.Charge,
                retentionTime, startTime, endTime, spectrum.MzBlob, spectrum.IntensityBlob, spectrum.NumPeaks,
                score, fileId, copies, 0.0, spectrum.Modifications, spectrum.ProteinIds, globalValues);
        }

        /// <summary>
        /// Writes a spectrum given as its parts rather than as a library entry, with
        /// <paramref name="modifications"/> at 0-based positions and <paramref name="proteins"/>
        /// as accessions. Returns the <c>RefSpectra</c> id.
        /// </summary>
        public long AddSpectrum(string peptideSeq, string peptideModSeq,
            double precursorMz, int precursorCharge,
            double retentionTime, double startTime, double endTime,
            double[] mzs, float[] intensities,
            double score, long fileId, int copies, double totalIonCurrent,
            IReadOnlyList<Modification> modifications = null, IReadOnlyList<string> proteins = null,
            SearchGlobalValues globalValues = null)
        {
            return AddSpectrum(peptideSeq, peptideModSeq, precursorMz, precursorCharge,
                retentionTime, startTime, endTime,
                BlibWriter.CompressMzs(mzs), BlibWriter.CompressIntensities(intensities), mzs.Length,
                score, fileId, copies, totalIonCurrent, modifications, proteins, globalValues);
        }

        /// <summary>
        /// Adds a protein group as one <c>Proteins</c> row: <paramref name="accessions"/> is the
        /// group's members joined by semicolons, <paramref name="qValue"/> its experiment-level
        /// protein group q-value. Returns the row id, for <see cref="AddSpectrumProtein"/>; a
        /// group already added is returned as it is. Requires <see cref="AddSearchGlobalColumns"/>.
        /// </summary>
        public long AddProteinGroup(string accessions, double qValue)
        {
            return _writer.AddProtein(accessions, qValue);
        }

        /// <summary>Links a spectrum to a <c>Proteins</c> row; a spectrum may link to several rows and a row to several spectra.</summary>
        public void AddSpectrumProtein(long refId, long proteinId)
        {
            _writer.AddSpectrumProtein(refId, proteinId);
        }

        /// <summary>
        /// Writes a library entry as its own source file's one spectrum, at <paramref name="bestRt"/>
        /// with bounds a minute either side. Returns the <c>RefSpectra</c> id.
        /// </summary>
        public long AddSpectrum(LibraryEntry entry, string fileName, double bestRt)
        {
            long fileId = AddSourceFile(fileName, fileName, 0.01);
            return AddSpectrum(BlibSpectrum.FromLibraryEntry(entry), bestRt, bestRt - 1.0, bestRt + 1.0, 0.01, fileId, 1);
        }

        /// <summary>
        /// Adds a <c>RetentionTimes</c> row for one run's observation of a spectrum. Pass null for
        /// <paramref name="retentionTime"/> when the precursor did not pass run-level FDR there,
        /// and null <paramref name="startTime"/> and <paramref name="endTime"/> for a library
        /// retention time, which has no peak boundaries.
        /// </summary>
        public void AddRetentionTime(long refId, long sourceFileId,
            double? retentionTime, double? startTime, double? endTime,
            double score, bool bestSpectrum)
        {
            _writer.AddRetentionTime(refId, sourceFileId, retentionTime, startTime, endTime, score, bestSpectrum,
                0.0, 0.0, 0.0);
        }

        /// <summary>
        /// Adds a <c>DecoyPairs</c> row (see <see cref="BlibWriter.AddDecoyPair"/>): the library
        /// supplies this spectrum as a decoy or a target of the pairing
        /// <paramref name="pairingId"/>, a decoy naming its target spectrum.
        /// </summary>
        public void AddDecoyPair(long refId, bool isDecoy, bool isEntrapment, int pairingId, long? targetRefSpectraId, string method = null)
        {
            _writer.AddDecoyPair(refId, isDecoy, isEntrapment, pairingId, targetRefSpectraId, method);
        }

        /// <summary>Adds or replaces a <c>LibMetadata</c> row; <paramref name="key"/> is one of the <c>KEY_</c> constants or a shared key.</summary>
        public void AddMetadata(string key, string value)
        {
            _writer.AddMetadata(key, value);
        }

        /// <summary>
        /// Records <paramref name="stamp"/> in <c>LibMetadata</c> under
        /// <see cref="ArtifactStamp.PARQUET_KEY"/>, so the blib carries its own validity record.
        /// </summary>
        public void AddStamp(ArtifactStamp stamp)
        {
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));
            _writer.AddMetadata(ArtifactStamp.PARQUET_KEY, stamp.ToString());
        }

        /// <summary>
        /// Records the spectrum count, adds the indexes, closes the file and moves it over the
        /// final name, replacing any library there.
        /// </summary>
        public void Complete()
        {
            _writer.Complete();
        }

        public void Dispose()
        {
            _writer.Dispose();
        }

        private long AddSpectrum(string peptideSeq, string peptideModSeq,
            double precursorMz, int precursorCharge,
            double retentionTime, double startTime, double endTime,
            byte[] mzBlob, byte[] intensityBlob, int numPeaks,
            double score, long fileId, int copies, double totalIonCurrent,
            IReadOnlyList<Modification> modifications, IReadOnlyList<string> proteins,
            SearchGlobalValues globalValues)
        {
            var record = new BlibSpectrumRecord
            {
                PrecursorQValue = globalValues?.PrecursorQValue,
                PrecursorPep = globalValues?.PrecursorPep,
                PeptideQValue = globalValues?.PeptideQValue,
                PeptideSeq = BlibPeptideText.StripFlankingChars(peptideSeq),
                PeptideModSeq = BlibPeptideText.ConvertUnimodToMass(BlibPeptideText.StripFlankingChars(peptideModSeq)),
                PrecursorMz = precursorMz,
                PrecursorCharge = precursorCharge,
                Copies = copies,
                NumPeaks = numPeaks,
                IonMobility = 0.0,
                CollisionalCrossSectionSqA = 0.0,
                IonMobilityHighEnergyOffset = 0.0,
                IonMobilityType = BlibWriter.ION_MOBILITY_TYPE_NONE,
                RetentionTime = retentionTime,
                StartTime = startTime,
                EndTime = endTime,
                TotalIonCurrent = totalIonCurrent,
                FileId = fileId,
                // The spectrum's ordinal, from 0, as Rust osprey labels it.
                SpecIdInFile = _writer.SpectrumCount.ToString(CultureInfo.InvariantCulture),
                Score = score,
                ScoreType = BlibWriter.SCORE_TYPE_GENERIC_QVALUE,
                MzBlob = mzBlob,
                IntensityBlob = intensityBlob,
                Modifications = ToBlibModifications(modifications),
                Proteins = proteins ?? Array.Empty<string>(),
            };
            return _writer.AddSpectrum(record);
        }

        /// <summary>The <c>Modifications</c> rows: positions 0-based in Osprey, 1-based in a blib.</summary>
        private static IReadOnlyList<BlibModification> ToBlibModifications(IReadOnlyList<Modification> modifications)
        {
            if (modifications == null || modifications.Count == 0)
                return Array.Empty<BlibModification>();
            var result = new BlibModification[modifications.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new BlibModification(modifications[i].Position + 1, modifications[i].MassDelta);
            return result;
        }
    }

    /// <summary>
    /// A search result's experiment-level values for one spectrum, written to the columns
    /// <see cref="OspreyBlibWriter.AddSearchGlobalColumns"/> adds: the precursor q-value (the
    /// same value as <c>score</c>), the precursor PEP (one value per entry for the whole
    /// analysis) and the peptide q-value (one value per modified sequence, the same on every
    /// charge state's row).
    /// </summary>
    public sealed class SearchGlobalValues
    {
        public SearchGlobalValues(double precursorQValue, double precursorPep, double peptideQValue)
        {
            PrecursorQValue = precursorQValue;
            PrecursorPep = precursorPep;
            PeptideQValue = peptideQValue;
        }

        public double PrecursorQValue { get; }
        public double PrecursorPep { get; }
        public double PeptideQValue { get; }
    }
}
