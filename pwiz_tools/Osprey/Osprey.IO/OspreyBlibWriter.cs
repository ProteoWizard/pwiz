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
    /// ours writes the format through, with <see cref="OspreyBlibTables"/> beside the standard
    /// tables, and the column values Osprey's search output and <c>--export-library</c> have
    /// always written (Rust osprey's <c>blib.rs</c>): a <c>GENERIC Q-VALUE</c> score, the
    /// spectrum's ordinal as its id in file, zero rather than NULL for the ion mobility columns
    /// and the total ion current, the apex and bounds in the spectrum row, and the peptide text
    /// through <see cref="BlibPeptideText"/>. The library is written beside its path and moved
    /// over it by <see cref="Complete"/>; disposed without that, nothing at the path changes.
    /// </summary>
    public sealed class OspreyBlibWriter : IDisposable
    {
        private readonly OspreyBlibTables _tables;
        private readonly BlibWriter _writer;

        /// <summary>Starts the library that <see cref="Complete"/> writes to <paramref name="path"/>.</summary>
        public OspreyBlibWriter(string path)
        {
            _tables = new OspreyBlibTables();
            // The writer owns the tables from here: it disposes their statements with its connection.
            _writer = new BlibWriter(path, Path.GetFileName(path), _tables);
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
        /// Writes a library precursor prepared by <see cref="BlibSpectrum.FromLibraryEntry"/>: its
        /// <c>RefSpectra</c> and peak rows, then its modification and protein rows. The retention
        /// times, score, source file and copy count are what the caller knows about this row - a
        /// search result's apex, bounds, q-value and run count, or a library's own retention time.
        /// Returns the <c>RefSpectra</c> id.
        /// </summary>
        public long AddSpectrum(BlibSpectrum spectrum, double retentionTime, double startTime, double endTime,
            double score, long fileId, int copies)
        {
            return AddSpectrum(spectrum.PeptideSeq, spectrum.ModifiedSequence, spectrum.PrecursorMz, spectrum.Charge,
                retentionTime, startTime, endTime, spectrum.MzBlob, spectrum.IntensityBlob, spectrum.NumPeaks,
                score, fileId, copies, 0.0, spectrum.Modifications, spectrum.ProteinIds);
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
            IReadOnlyList<Modification> modifications = null, IReadOnlyList<string> proteins = null)
        {
            return AddSpectrum(peptideSeq, peptideModSeq, precursorMz, precursorCharge,
                retentionTime, startTime, endTime,
                BlibWriter.CompressMzs(mzs), BlibWriter.CompressIntensities(intensities), mzs.Length,
                score, fileId, copies, totalIonCurrent, modifications, proteins);
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

        /// <summary>One <c>OspreyPeakBoundaries</c> row: a passing precursor's best run.</summary>
        public void AddPeakBoundaries(long refId, string fileName,
            double startRt, double endRt, double apexRt,
            double apexIntensity, double integratedArea)
        {
            _tables.AddPeakBoundaries(refId, fileName, startRt, endRt, apexRt, apexIntensity, integratedArea);
        }

        /// <summary>One <c>OspreyRunScores</c> row: a passing precursor's best run.</summary>
        public void AddRunScores(long refId, string fileName,
            double runQValue, double discriminantScore, double posteriorErrorProb)
        {
            _tables.AddRunScores(refId, fileName, runQValue, discriminantScore, posteriorErrorProb);
        }

        /// <summary>One <c>OspreyExperimentScores</c> row per passing precursor.</summary>
        public void AddExperimentScores(long refId,
            double experimentQValue, int nRunsDetected, int nRunsSearched)
        {
            _tables.AddExperimentScores(refId, experimentQValue, nRunsDetected, nRunsSearched);
        }

        /// <summary>One <c>OspreyCoefficients</c> row: one scan of a peak's coefficient series.</summary>
        public void AddCoefficient(long refId, string fileName,
            uint scanNumber, double rt, double coefficient)
        {
            _tables.AddCoefficient(refId, fileName, scanNumber, rt, coefficient);
        }

        /// <summary>Adds or replaces an <c>OspreyMetadata</c> key-value row.</summary>
        public void AddMetadata(string key, string value)
        {
            _tables.AddMetadata(key, value);
        }

        /// <summary>
        /// Records <paramref name="stamp"/> in <c>OspreyMetadata</c> under
        /// <see cref="ArtifactStamp.PARQUET_KEY"/>, so the blib carries its own validity record.
        /// </summary>
        public void AddStamp(ArtifactStamp stamp)
        {
            _tables.AddStamp(stamp);
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
            IReadOnlyList<Modification> modifications, IReadOnlyList<string> proteins)
        {
            var record = new BlibSpectrumRecord
            {
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
}
