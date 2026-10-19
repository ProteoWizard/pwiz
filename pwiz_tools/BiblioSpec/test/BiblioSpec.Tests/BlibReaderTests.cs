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

using System.Data.SQLite;
using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// The shared <see cref="BlibReader"/> reads back what the shared <see cref="BlibWriter"/>
/// wrote - every table, every column, the same records - and reads a library of the earliest
/// schema, with most tables and columns missing, through the same calls.
/// </summary>
[TestClass]
public class BlibReaderTests
{
    private const string LIBRARY_NAME = "reader-test.blib";

    /// <summary>
    /// Written with the writer, read with the reader: the header, the small tables whole, the
    /// spectra with and without their peaks (and in partitions that together are the table),
    /// the peaks decoded from compressed and raw blobs, and the per-spectrum rows by id.
    /// </summary>
    [TestMethod]
    public void Reader_ReadsBackWhatTheWriterWrote()
    {
        string path = TempPath();
        try
        {
            // Enough peaks that zlib wins over the raw bytes, so the blobs are compressed.
            var mzs = Enumerable.Range(0, 64).Select(i => 200.0 + i * 1.5).ToArray();
            var intensities = Enumerable.Range(0, 64).Select(i => (float)(1000 - i * 10)).ToArray();
            byte[] mzBlob = BlibWriter.CompressMzs(mzs);
            byte[] intensityBlob = BlibWriter.CompressIntensities(intensities);
            Assert.AreNotEqual(mzs.Length * sizeof(double), mzBlob.Length, "The m/z blob should be compressed.");
            long fileA, fileB, peptideId, moleculeId, bareId;
            using (var writer = new BlibWriter(path, LIBRARY_NAME))
            {
                fileA = writer.AddSourceFile("a.mzML", "a.ssl", 0.95, BlibWriter.WORKFLOW_TYPE_DIA);
                fileB = writer.AddSourceFile("b.mzML", null, null, BlibWriter.WORKFLOW_TYPE_DDA);
                // Three of the optional columns, added before the rows that carry values, and a
                // protein with its value written ahead of the spectra that map to it by accession.
                writer.AddOptionalColumn(BlibSchema.PrecursorQValue);
                writer.AddOptionalColumn(BlibSchema.PeptidePep);
                writer.AddOptionalColumn(BlibSchema.ProteinQValue);
                Assert.IsTrue(writer.HasOptionalColumn(BlibSchema.PeptidePep));
                Assert.IsFalse(writer.HasOptionalColumn(BlibSchema.PrecursorPep));
                writer.AddProtein("P1", 0.02);
                writer.BeginBatch();
                peptideId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "PEPTIDEK",
                    PeptideModSeq = "PEPT[+80.0]IDEK",
                    PrecursorMz = 500.25,
                    PrecursorCharge = 2,
                    PrevAA = "K",
                    NextAA = "R",
                    Copies = 3,
                    IonMobility = 0.95,
                    CollisionalCrossSectionSqA = 310.5,
                    IonMobilityHighEnergyOffset = -0.1,
                    IonMobilityType = BlibWriter.ION_MOBILITY_TYPE_INVERSE_K0,
                    RetentionTime = 12.5,
                    StartTime = 12.0,
                    EndTime = 13.0,
                    TotalIonCurrent = 1e6,
                    FileId = fileA,
                    SpecIdInFile = "scan=17",
                    Score = 0.001,
                    ScoreType = BlibWriter.SCORE_TYPE_GENERIC_QVALUE,
                    PrecursorQValue = 0.001,
                    PrecursorPep = 0.05,
                    PeptidePep = 0.3,
                    MzBlob = mzBlob,
                    IntensityBlob = intensityBlob,
                    NumPeaks = mzs.Length,
                    Modifications = new[] { new BlibModification(4, 79.966331) },
                    Proteins = new[] { "P1", "P2" },
                    Annotations = new[] { new BlibPeakAnnotation(0, "y1", 1, 200.0, 200.0), new BlibPeakAnnotation(3, "b2", 1, 204.5, 204.5) },
                });
                // A small molecule with raw blobs and one protein; a precursor-only entry with no peaks row.
                moleculeId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = string.Empty,
                    PeptideModSeq = string.Empty,
                    PrecursorMz = 180.06,
                    PrecursorCharge = 1,
                    MoleculeName = "Glucose",
                    ChemicalFormula = "C6H12O6",
                    PrecursorAdduct = "[M+H]",
                    InchiKey = "GZCGUPFRVQAUEE-SLPGGIOYSA-N",
                    OtherKeys = "cas:50-99-7",
                    FileId = fileB,
                    MzBlob = BlibWriter.EncodeMzs(new[] { 100.0, 120.0 }, false),
                    IntensityBlob = BlibWriter.EncodeIntensities(new[] { 1f, 0.5f }, false),
                    NumPeaks = 2,
                    Proteins = new[] { "P1" },
                });
                bareId = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "SAMPLER",
                    PeptideModSeq = "SAMPLER",
                    PrecursorMz = 400.2,
                    PrecursorCharge = 2,
                    FileId = fileA,
                });
                writer.AddRetentionTime(peptideId, fileA, 12.5, 12.0, 13.0, 0.001, true, 0.95, 310.5, -0.1, BlibWriter.ION_MOBILITY_TYPE_INVERSE_K0);
                writer.AddRetentionTime(peptideId, fileB, 12.7, null, null, 0.002, false, redundantRefId: 42);
                writer.AddRetentionTime(bareId, fileA, null, null, null, 0, true);
                writer.AddMetadata("test.key", "value");
                writer.AddMetadata("test.null", null);
                writer.AddDecoyPair(peptideId, false, false, 7, null, null);
                writer.AddDecoyPair(bareId, true, true, 7, peptideId, "reverse");
                // P2 was written by the spectrum's accession loop without a value, so the first
                // value supplied for it fills the row; P1's value stands against a later one.
                Assert.AreEqual(writer.AddProtein("P2"), writer.AddProtein("P2", 0.04));
                writer.AddProtein("P1", 0.5);
                writer.CommitBatch();
                writer.Complete();
            }

            using var reader = new BlibReader(path);
            var info = reader.Info;
            Assert.AreEqual(BlibSchema.NonRedundantLsid(LIBRARY_NAME), info.Lsid);
            Assert.IsFalse(info.IsRedundant);
            Assert.AreEqual(3, info.SpectrumCount);
            Assert.AreEqual(BlibWriter.MAJOR_VERSION, info.Revision);
            Assert.AreEqual(BlibSchema.CurrentMinorVersion, info.SchemaVersion);
            Assert.AreEqual(3, reader.CountSpectra());

            var files = reader.ReadSourceFiles();
            Assert.AreEqual(2, files.Count);
            Assert.AreEqual(fileA, files[0].Id);
            Assert.AreEqual("a.mzML", files[0].FileName);
            Assert.AreEqual("a.ssl", files[0].IdFileName);
            Assert.AreEqual(0.95, files[0].CutoffScore);
            Assert.AreEqual(BlibWriter.WORKFLOW_TYPE_DIA, files[0].WorkflowType);
            Assert.IsNull(files[1].IdFileName);
            Assert.IsNull(files[1].CutoffScore);
            Assert.AreEqual(BlibWriter.WORKFLOW_TYPE_DDA, files[1].WorkflowType);

            var scoreTypes = reader.ReadScoreTypes();
            Assert.AreEqual(BlibSchema.ScoreTypes.Length, scoreTypes.Count);
            Assert.AreEqual("GENERIC Q-VALUE", scoreTypes[BlibWriter.SCORE_TYPE_GENERIC_QVALUE]);

            var metadata = reader.ReadMetadata();
            Assert.IsNotNull(metadata);
            Assert.AreEqual("value", metadata["test.key"]);
            Assert.IsNull(metadata["test.null"]);

            var pairs = reader.ReadDecoyPairs();
            Assert.IsNotNull(pairs);
            Assert.AreEqual(2, pairs.Count);
            Assert.IsFalse(pairs[peptideId].IsDecoy);
            Assert.IsFalse(pairs[peptideId].IsEntrapment);
            Assert.AreEqual(7, pairs[peptideId].PairingId);
            Assert.IsNull(pairs[peptideId].TargetRefSpectraId);
            Assert.IsNull(pairs[peptideId].Method);
            Assert.IsTrue(pairs[bareId].IsDecoy);
            Assert.IsTrue(pairs[bareId].IsEntrapment);
            Assert.AreEqual(7, pairs[bareId].PairingId);
            Assert.AreEqual(peptideId, pairs[bareId].TargetRefSpectraId);
            Assert.AreEqual("reverse", pairs[bareId].Method);

            var proteins = reader.ReadProteins();
            CollectionAssert.AreEqual(new[] { "P1", "P2" }, proteins[peptideId].ToArray());
            CollectionAssert.AreEqual(new[] { "P1" }, proteins[moleculeId].ToArray());
            Assert.IsFalse(proteins.Contains(bareId));

            // The index scan: every column, no blobs.
            var index = reader.ReadSpectra(false).ToList();
            CollectionAssert.AreEqual(new[] { peptideId, moleculeId, bareId }, index.Select(s => s.Id).ToArray());
            var peptide = index[0];
            Assert.AreEqual("PEPTIDEK", peptide.PeptideSeq);
            Assert.AreEqual("PEPT[+80.0]IDEK", peptide.PeptideModSeq);
            Assert.AreEqual(500.25, peptide.PrecursorMz);
            Assert.AreEqual(2, peptide.PrecursorCharge);
            Assert.AreEqual("K", peptide.PrevAA);
            Assert.AreEqual("R", peptide.NextAA);
            Assert.AreEqual(3, peptide.Copies);
            Assert.AreEqual(0.95, peptide.IonMobility);
            Assert.AreEqual(310.5, peptide.CollisionalCrossSectionSqA);
            Assert.AreEqual(-0.1, peptide.IonMobilityHighEnergyOffset);
            Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_INVERSE_K0, peptide.IonMobilityType);
            Assert.AreEqual(12.5, peptide.RetentionTime);
            Assert.AreEqual(12.0, peptide.StartTime);
            Assert.AreEqual(13.0, peptide.EndTime);
            Assert.AreEqual(1e6, peptide.TotalIonCurrent);
            Assert.AreEqual(string.Empty, peptide.MoleculeName);
            Assert.AreEqual(fileA, peptide.FileId);
            Assert.AreEqual("scan=17", peptide.SpecIdInFile);
            Assert.AreEqual(0.001, peptide.Score);
            Assert.AreEqual(BlibWriter.SCORE_TYPE_GENERIC_QVALUE, peptide.ScoreType);
            // The optional columns the library has read back; the one it lacks reads as null,
            // as does the record's PrecursorPep, which had no column to go to.
            Assert.AreEqual(0.001, peptide.PrecursorQValue);
            Assert.AreEqual(0.3, peptide.PeptidePep);
            Assert.IsNull(peptide.PrecursorPep);
            Assert.IsNull(peptide.PeptideQValue);
            using (var command = new SQLiteCommand("SELECT QValue FROM Proteins WHERE accession = 'P1'", reader.Connection))
                Assert.AreEqual(0.02, (double)command.ExecuteScalar());
            using (var command = new SQLiteCommand("SELECT QValue FROM Proteins WHERE accession = 'P2'", reader.Connection))
                Assert.AreEqual(0.04, (double)command.ExecuteScalar());
            using (var command = new SQLiteCommand("SELECT COUNT(*) FROM Proteins", reader.Connection))
                Assert.AreEqual(2L, command.ExecuteScalar());
            Assert.AreEqual(mzs.Length, peptide.NumPeaks);
            Assert.IsNull(peptide.MzBlob);
            Assert.IsNull(peptide.IntensityBlob);
            var molecule = index[1];
            Assert.AreEqual("Glucose", molecule.MoleculeName);
            Assert.AreEqual("C6H12O6", molecule.ChemicalFormula);
            Assert.AreEqual("[M+H]", molecule.PrecursorAdduct);
            Assert.AreEqual("GZCGUPFRVQAUEE-SLPGGIOYSA-N", molecule.InchiKey);
            Assert.AreEqual("cas:50-99-7", molecule.OtherKeys);
            Assert.AreEqual(BlibSpectrumRecord.NO_FLANKING_RESIDUE, molecule.PrevAA);
            Assert.IsNull(molecule.IonMobility);
            Assert.IsNull(molecule.RetentionTime);
            Assert.IsNull(molecule.SpecIdInFile);
            Assert.AreEqual(BlibWriter.SCORE_TYPE_UNKNOWN, molecule.ScoreType);
            Assert.IsNull(molecule.PrecursorQValue);
            var bare = index[2];
            Assert.AreEqual(0, bare.NumPeaks);
            Assert.AreEqual(1, bare.Copies);

            // The partitions together are the table, each in id order; a bad partition is refused at the call.
            var partitioned = Enumerable.Range(0, 3).SelectMany(i => reader.ReadSpectra(false, 3, i)).Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(index.Select(s => s.Id).ToList(), partitioned);
            Assert.AreEqual(3, partitioned.Distinct().Count());
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.ReadSpectra(false, 3, 3));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reader.ReadRetentionTimes(0));

            // The filtered scan: each bound inclusive, a bound left null not applied, composed with a partition.
            var inWindow = reader.ReadSpectra(false, new BlibSpectrumFilter { MinPrecursorMz = 400.2, MaxPrecursorMz = 500.25 }).Select(s => s.Id).ToList();
            CollectionAssert.AreEqual(new[] { peptideId, bareId }, inWindow);
            var withPeakRows = reader.ReadSpectra(false, new BlibSpectrumFilter { MinNumPeaks = 1 }).Select(s => s.Id).ToList();
            CollectionAssert.AreEqual(new[] { peptideId, moleculeId }, withPeakRows);
            var singlyCharged = reader.ReadSpectra(false, new BlibSpectrumFilter { MinPrecursorCharge = 1, MaxPrecursorCharge = 1 }).Select(s => s.Id).ToList();
            CollectionAssert.AreEqual(new[] { moleculeId }, singlyCharged);
            var withPeaksRows = reader.ReadSpectra(false, new BlibSpectrumFilter { RequirePeaksRow = true }).Select(s => s.Id).ToList();
            CollectionAssert.AreEqual(new[] { peptideId, moleculeId }, withPeaksRows);
            Assert.AreEqual(0, reader.ReadSpectra(false, new BlibSpectrumFilter { MinPrecursorMz = 600 }).Count());
            var partitionedInWindow = Enumerable.Range(0, 2).SelectMany(i => reader.ReadSpectra(false, new BlibSpectrumFilter { MinPrecursorMz = 400.0 }, 2, i)).Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(inWindow, partitionedInWindow);

            // One row by id, with and without its blobs; null for an id the library lacks.
            var one = reader.ReadSpectrum(moleculeId, true);
            Assert.IsNotNull(one);
            Assert.AreEqual("Glucose", one.MoleculeName);
            CollectionAssert.AreEqual(new[] { 100.0, 120.0 }, BlibReader.DecodePeaks(one)!.Mzs);
            Assert.IsNull(reader.ReadSpectrum(peptideId, false)!.MzBlob);
            Assert.IsNull(reader.ReadSpectrum(bareId + 1, true));

            // The scan with peaks: blobs as written, null for the spectrum without a peaks row.
            var withPeaks = reader.ReadSpectra(true).ToList();
            CollectionAssert.AreEqual(mzBlob, withPeaks[0].MzBlob);
            CollectionAssert.AreEqual(intensityBlob, withPeaks[0].IntensityBlob);
            Assert.IsNull(withPeaks[2].MzBlob);
            Assert.IsNull(BlibReader.DecodePeaks(withPeaks[2]));
            var peaks = BlibReader.DecodePeaks(withPeaks[0]);
            Assert.IsNotNull(peaks);
            Assert.AreEqual(mzs.Length, peaks.Count);
            CollectionAssert.AreEqual(mzs, peaks.Mzs);
            CollectionAssert.AreEqual(intensities, peaks.Intensities);
            var rawPeaks = BlibReader.DecodePeaks(withPeaks[1]);
            Assert.IsNotNull(rawPeaks);
            CollectionAssert.AreEqual(new[] { 100.0, 120.0 }, rawPeaks.Mzs);
            CollectionAssert.AreEqual(new[] { 1f, 0.5f }, rawPeaks.Intensities);

            // By id: peaks, modifications, annotations.
            var byId = reader.ReadPeaks(peptideId, mzs.Length);
            Assert.IsNotNull(byId);
            CollectionAssert.AreEqual(mzs, byId.Mzs);
            Assert.IsNull(reader.ReadPeaks(bareId, 0));
            var modifications = reader.ReadModifications(peptideId);
            Assert.AreEqual(1, modifications.Count);
            Assert.AreEqual(4, modifications[0].Position);
            Assert.AreEqual(79.966331, modifications[0].Mass);
            Assert.AreEqual(0, reader.ReadModifications(bareId).Count);
            var annotations = reader.ReadAnnotations(peptideId);
            Assert.AreEqual(2, annotations.Count);
            Assert.AreEqual(3, annotations[1].PeakIndex);
            Assert.AreEqual("b2", annotations[1].Name);
            Assert.AreEqual(1, annotations[1].Charge);
            Assert.AreEqual(204.5, annotations[1].MzTheoretical);
            Assert.AreEqual(204.5, annotations[1].MzObserved);

            // The retention times, in spectrum order, with the partitions together the table.
            var times = reader.ReadRetentionTimes().ToList();
            Assert.AreEqual(3, times.Count);
            Assert.AreEqual(peptideId, times[0].RefSpectraId);
            Assert.AreEqual(fileA, times[0].SpectrumSourceId);
            Assert.AreEqual(12.5, times[0].RetentionTime);
            Assert.AreEqual(12.0, times[0].StartTime);
            Assert.AreEqual(13.0, times[0].EndTime);
            Assert.AreEqual(0.001, times[0].Score);
            Assert.IsTrue(times[0].BestSpectrum);
            Assert.AreEqual(0.95, times[0].IonMobility);
            Assert.AreEqual(310.5, times[0].CollisionalCrossSectionSqA);
            Assert.AreEqual(-0.1, times[0].IonMobilityHighEnergyOffset);
            Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_INVERSE_K0, times[0].IonMobilityType);
            Assert.AreEqual(0L, times[0].RedundantRefSpectraId);
            Assert.AreEqual(42L, times[1].RedundantRefSpectraId);
            Assert.IsNull(times[1].StartTime);
            Assert.IsFalse(times[1].BestSpectrum);
            Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_NONE, times[1].IonMobilityType);
            Assert.AreEqual(bareId, times[2].RefSpectraId);
            Assert.IsNull(times[2].RetentionTime);
            var partitionedTimes = Enumerable.Range(0, 2).SelectMany(i => reader.ReadRetentionTimes(2, i)).Select(t => t.RefSpectraId).ToList();
            CollectionAssert.AreEquivalent(times.Select(t => t.RefSpectraId).ToList(), partitionedTimes);
        }
        finally
        {
            DeleteTemp(path);
        }
    }

    /// <summary>
    /// A library of the first schema - LibInfo and RefSpectra with the columns of the day,
    /// raw peak blobs, and none of the later tables - reads through the same calls: the
    /// columns it lacks read as the record's defaults, the tables it lacks as empty or null,
    /// and float64 intensities as the floats they hold.
    /// </summary>
    [TestMethod]
    public void Reader_ReadsLegacyLibraryWithMissingTablesAndColumns()
    {
        string path = TempPath();
        try
        {
            var mzs = new[] { 300.0, 400.0 };
            byte[] mzBlob = BlibWriter.EncodeMzs(mzs, false);
            byte[] doubleIntensities = new byte[2 * sizeof(double)];
            Buffer.BlockCopy(BitConverter.GetBytes(1.0), 0, doubleIntensities, 0, sizeof(double));
            Buffer.BlockCopy(BitConverter.GetBytes(0.25), 0, doubleIntensities, sizeof(double), sizeof(double));
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3 };
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                Execute(connection, "CREATE TABLE LibInfo (libLSID TEXT, createTime TEXT, numSpecs INTEGER, majorVersion INTEGER, minorVersion INTEGER)");
                Execute(connection, "INSERT INTO LibInfo VALUES ('urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:redundant:old.blib', 'Thu Nov  3 17:02:18 2011', 1, 2, 0)");
                Execute(connection, "CREATE TABLE RefSpectra (id INTEGER PRIMARY KEY AUTOINCREMENT, peptideSeq VARCHAR(150), precursorMZ REAL, " +
                                    "precursorCharge INTEGER, peptideModSeq VARCHAR(200), prevAA CHAR(1), nextAA CHAR(1), copies INTEGER, numPeaks INTEGER)");
                Execute(connection, "INSERT INTO RefSpectra VALUES (1, 'PEPTIDEK', 500.25, 2, 'PEPTIDEK', '-', '-', 1, 2)");
                Execute(connection, "CREATE TABLE RefSpectraPeaks (RefSpectraID INTEGER, peakMZ BLOB, peakIntensity BLOB)");
                using var insert = new SQLiteCommand("INSERT INTO RefSpectraPeaks VALUES (1, @mz, @intensity)", connection);
                insert.Parameters.AddWithValue("@mz", mzBlob);
                insert.Parameters.AddWithValue("@intensity", doubleIntensities);
                insert.ExecuteNonQuery();
            }

            using var reader = new BlibReader(path);
            Assert.IsTrue(reader.Info.IsRedundant);
            Assert.AreEqual(0, reader.Info.SchemaVersion);
            Assert.AreEqual(2, reader.Info.Revision);
            Assert.IsFalse(reader.HasTable(BlibReader.TABLE_RETENTION_TIMES));
            Assert.AreEqual(0, reader.ReadSourceFiles().Count);
            Assert.AreEqual(0, reader.ReadScoreTypes().Count);
            Assert.IsNull(reader.ReadMetadata());
            Assert.IsNull(reader.ReadDecoyPairs());
            Assert.AreEqual(0, reader.ReadProteins().Count);
            Assert.AreEqual(0, reader.ReadRetentionTimes().Count());
            Assert.AreEqual(0, reader.ReadModifications(1).Count);
            Assert.AreEqual(0, reader.ReadAnnotations(1).Count);

            var spectrum = reader.ReadSpectra(true).Single();
            Assert.AreEqual(1L, spectrum.Id);
            Assert.AreEqual("PEPTIDEK", spectrum.PeptideSeq);
            Assert.AreEqual(2, spectrum.NumPeaks);
            Assert.IsNull(spectrum.RetentionTime);
            Assert.IsNull(spectrum.IonMobility);
            Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_NONE, spectrum.IonMobilityType);
            Assert.AreEqual(string.Empty, spectrum.MoleculeName);
            Assert.AreEqual(0L, spectrum.FileId);
            Assert.AreEqual(BlibWriter.SCORE_TYPE_UNKNOWN, spectrum.ScoreType);
            var peaks = BlibReader.DecodePeaks(spectrum);
            Assert.IsNotNull(peaks);
            CollectionAssert.AreEqual(mzs, peaks.Mzs);
            CollectionAssert.AreEqual(new[] { 1f, 0.25f }, peaks.Intensities);
            reader.Dispose();

            // Without the peaks table at all, the scan with peaks still runs, with null blobs.
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                Execute(connection, "DROP TABLE RefSpectraPeaks");
            }
            using var indexOnly = new BlibReader(path);
            var withoutPeaks = indexOnly.ReadSpectra(true).Single();
            Assert.IsNull(withoutPeaks.MzBlob);
            Assert.IsNull(BlibReader.DecodePeaks(withoutPeaks));
            Assert.IsNull(indexOnly.ReadPeaks(1, 2));
            Assert.AreEqual(0, indexOnly.ReadSpectra(true, new BlibSpectrumFilter { RequirePeaksRow = true }).Count());
        }
        finally
        {
            DeleteTemp(path);
        }
    }

    /// <summary>
    /// A spectrum copied through the copier keeps everything that hangs off it - the peak blobs,
    /// its modifications, its annotations' small-molecule columns, its proteins with their
    /// values and its source file with its cutoff - its copy count is the caller's, and it
    /// keeps its id when asked. The optional columns come across with the source files. The
    /// grouped scan and the score types in use read the copy back.
    /// </summary>
    [TestMethod]
    public void Copier_CopiesSpectraWithTheirRowsAndIds()
    {
        string source = TempPath();
        string target = TempPath();
        try
        {
            long fileA, id1, id2;
            using (var writer = new BlibWriter(source, "source.blib"))
            {
                fileA = writer.AddSourceFile("a.mzML", "a.pep.xml", 0.95, BlibWriter.WORKFLOW_TYPE_DDA);
                writer.AddOptionalColumn(BlibSchema.PrecursorQValue);
                writer.AddOptionalColumn(BlibSchema.ProteinQValue);
                writer.AddProtein("P1", 0.02);
                id1 = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "PEPTIDEK",
                    PeptideModSeq = "PEPT[+80.0]IDEK",
                    PrecursorMz = 500.25,
                    PrecursorCharge = 2,
                    FileId = fileA,
                    SpecIdInFile = "scan=17",
                    NumPeaks = 2,
                    MzBlob = BlibWriter.EncodeMzs(new[] { 100.0, 200.0 }, false),
                    IntensityBlob = BlibWriter.EncodeIntensities(new[] { 1f, 2f }, false),
                    PrecursorQValue = 0.001,
                    Modifications = new[] { new BlibModification(4, 79.966331) },
                    Proteins = new[] { "P1", "P2" },
                    Annotations = new[] { new BlibPeakAnnotation(0, "frag", 1, 100.0, 100.0, "C6H12O6", "KEY", "cas:1", "[M+H]", "note") },
                });
                id2 = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "SAMPLER",
                    PeptideModSeq = "SAMPLER",
                    PrecursorMz = 400.2,
                    PrecursorCharge = 2,
                    FileId = fileA,
                    SpecIdInFile = "scan=20",
                });
                writer.Complete();
            }

            using (var reader = new BlibReader(source))
            using (var writer = new BlibWriter(target, "target.blib"))
            {
                var copier = new BlibCopier(reader, writer);
                copier.CopySourceFiles();
                Assert.IsTrue(writer.HasOptionalColumn(BlibSchema.PrecursorQValue));
                Assert.IsTrue(writer.HasOptionalColumn(BlibSchema.ProteinQValue));
                // Out of id order, under their own ids, with the caller's copy counts.
                Assert.AreEqual(id2, copier.CopySpectrum(id2, 3, keepId: true));
                Assert.AreEqual(id1, copier.CopySpectrum(id1, 1, keepId: true));
                Assert.IsNull(copier.CopySpectrum(99, 1, keepId: true));
                writer.Complete();
            }

            using var copy = new BlibReader(target);
            var spectra = copy.ReadSpectra(true).ToList();
            CollectionAssert.AreEqual(new[] { id1, id2 }, spectra.Select(s => s.Id).ToArray());
            var first = spectra[0];
            Assert.AreEqual(1, first.Copies);
            Assert.AreEqual(3, spectra[1].Copies);
            Assert.AreEqual(0.001, first.PrecursorQValue);
            CollectionAssert.AreEqual(new[] { 100.0, 200.0 }, BlibReader.DecodePeaks(first).Mzs);
            var modifications = copy.ReadModifications(id1);
            Assert.AreEqual(1, modifications.Count);
            Assert.AreEqual(4, modifications[0].Position);
            Assert.AreEqual(79.966331, modifications[0].Mass);
            var annotations = copy.ReadAnnotations(id1);
            Assert.AreEqual(1, annotations.Count);
            Assert.AreEqual("C6H12O6", annotations[0].Formula);
            Assert.AreEqual("KEY", annotations[0].InchiKey);
            Assert.AreEqual("cas:1", annotations[0].OtherKeys);
            Assert.AreEqual("[M+H]", annotations[0].Adduct);
            Assert.AreEqual("note", annotations[0].Comment);
            var proteins = copy.ReadProteinTable();
            Assert.AreEqual(2, proteins.Count);
            Assert.AreEqual(0.02, proteins.Single(p => p.Accession == "P1").QValue);
            Assert.IsNull(proteins.Single(p => p.Accession == "P2").QValue);
            CollectionAssert.AreEqual(new[] { "P1", "P2" }, copy.ReadProteins()[id1].ToArray());
            Assert.AreEqual(2, copy.ReadProteinIds()[id1].Count());
            var files = copy.ReadSourceFiles();
            Assert.AreEqual(1, files.Count);
            Assert.AreEqual("a.mzML", files[0].FileName);
            Assert.AreEqual("a.pep.xml", files[0].IdFileName);
            Assert.AreEqual(0.95, files[0].CutoffScore);
            Assert.AreEqual(files[0].Id, first.FileId);
            // The scan grouped by precursor (PEPT[+80.0]IDEK sorts before SAMPLER) and the score types in use.
            CollectionAssert.AreEqual(new[] { id1, id2 }, copy.ReadSpectraByPrecursor(false, 0).Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { BlibWriter.SCORE_TYPE_UNKNOWN }, copy.ReadScoreTypesInUse().ToArray());
        }
        finally
        {
            DeleteTemp(source);
            DeleteTemp(target);
        }
    }

    /// <summary>
    /// The calls Skyline's library reader and writer make beyond BiblioSpec's own tools: a
    /// library under an LSID of the caller's, with and without a RetentionTimes table; a score
    /// type registered by name, BiblioSpec's own found and a new one numbered after them; a
    /// spectrum without a source file, written as NULL and read back as 0; one spectrum's
    /// observations by id and their count; the score types with their probability types; the
    /// optional columns probed for a value; and a source file deleted from an appended library
    /// with everything of its spectra's.
    /// </summary>
    [TestMethod]
    public void Reader_AndWriter_ServeSkylinesLibraryReadsAndWrites()
    {
        string path = TempPath();
        string redundantPath = TempPath();
        try
        {
            const string lsid = "urn:lsid:unknown.org:spectral_library:bibliospec:nr:minimal:lib:guid:1.11";
            long fileA, fileB, withFile, withoutFile;
            int generic, custom;
            using (var writer = BlibWriter.Create(path, lsid))
            {
                fileA = writer.AddSourceFile("a.raw", "a.pep.xml", 0.9, BlibWriter.WORKFLOW_TYPE_DDA);
                fileB = writer.AddSourceFile("b.raw", null, null, BlibWriter.WORKFLOW_TYPE_DDA);
                generic = writer.AddScoreType("GENERIC Q-VALUE", "PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT");
                custom = writer.AddScoreType("SKYLINE DOT PRODUCT", null);
                Assert.AreEqual(custom, writer.AddScoreType("SKYLINE DOT PRODUCT", "NOT_A_PROBABILITY_VALUE"));
                writer.AddOptionalColumn(BlibSchema.PrecursorQValue);
                writer.AddOptionalColumn(BlibSchema.PeptideQValue);
                withFile = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "PEPTIDEK", PeptideModSeq = "PEPTIDEK", PrecursorMz = 500.25, PrecursorCharge = 2,
                    FileId = fileA, ScoreType = custom, Score = 0.8, PrecursorQValue = 0.01,
                    MzBlob = BlibWriter.EncodeMzs(new[] { 100.0 }, false), IntensityBlob = BlibWriter.EncodeIntensities(new[] { 1f }, false),
                    NumPeaks = 1, Modifications = new[] { new BlibModification(1, 42.0) }, Proteins = new[] { "P1" },
                    Annotations = new[] { new BlibPeakAnnotation(0, "y1", 1, 100.0, 100.0) },
                });
                withoutFile = writer.AddSpectrum(new BlibSpectrumRecord
                {
                    PeptideSeq = "SAMPLER", PeptideModSeq = "SAMPLER", PrecursorMz = 400.2, PrecursorCharge = 2, FileId = fileB,
                });
                writer.AddRetentionTime(withFile, fileA, 10.5, null, null, 0, true, redundantRefId: -1);
                writer.AddRetentionTime(withFile, fileB, 10.7, null, null, 0, false, redundantRefId: -1);
                writer.AddRetentionTime(withoutFile, fileB, 20.0, null, null, 0, true);
                writer.SetRevision(3);
                writer.Complete();
            }
            using (var redundant = BlibWriter.Create(redundantPath, lsid.Replace(":nr:", ":redundant:"), true))
            {
                redundant.AddSpectrum(new BlibSpectrumRecord { Id = 7, PeptideSeq = "SAMPLER", PeptideModSeq = "SAMPLER", PrecursorMz = 400.2, PrecursorCharge = 2 }, true);
                redundant.Complete();
            }

            using (var reader = new BlibReader(path))
            {
                Assert.AreEqual(lsid, reader.Info.Lsid);
                Assert.AreEqual(3, reader.Info.Revision);
                Assert.IsTrue(reader.HasTable(BlibReader.TABLE_RETENTION_TIMES));
                Assert.AreEqual(BlibWriter.SCORE_TYPE_GENERIC_QVALUE, generic);
                Assert.AreEqual(BlibSchema.ScoreTypes.Length, custom);
                var scoreTypes = reader.ReadScoreTypeTable();
                Assert.AreEqual(BlibSchema.ScoreTypes.Length + 1, scoreTypes.Count);
                Assert.AreEqual("SKYLINE DOT PRODUCT", scoreTypes[custom].Name);
                Assert.IsNull(scoreTypes[custom].ProbabilityType);
                Assert.AreEqual("NOT_A_PROBABILITY_VALUE", scoreTypes[BlibWriter.SCORE_TYPE_UNKNOWN].ProbabilityType);
                Assert.IsTrue(reader.HasValues(BlibReader.TABLE_REF_SPECTRA, BlibSchema.PrecursorQValue.Name));
                Assert.IsFalse(reader.HasValues(BlibReader.TABLE_REF_SPECTRA, BlibSchema.PeptideQValue.Name), "A column NULL throughout has no values.");
                Assert.IsFalse(reader.HasValues(BlibReader.TABLE_REF_SPECTRA, BlibSchema.PeptidePep.Name), "A column the library lacks has no values.");
                Assert.AreEqual(3, reader.CountRetentionTimes());
                var times = reader.ReadSpectrumRetentionTimes(withFile);
                Assert.AreEqual(2, times.Count);
                Assert.AreEqual(fileA, times[0].SpectrumSourceId);
                Assert.AreEqual(10.5, times[0].RetentionTime);
                Assert.IsTrue(times[0].BestSpectrum);
                Assert.AreEqual(-1L, times[0].RedundantRefSpectraId);
                Assert.AreEqual(fileB, times[1].SpectrumSourceId);
                Assert.IsFalse(times[1].BestSpectrum);
                Assert.AreEqual(0, reader.ReadSpectrumRetentionTimes(99).Count);
            }
            using (var reader = new BlibReader(redundantPath))
            {
                Assert.IsTrue(reader.Info.IsRedundant);
                Assert.IsFalse(reader.HasTable(BlibReader.TABLE_RETENTION_TIMES));
                Assert.AreEqual(0, reader.CountRetentionTimes());
                Assert.AreEqual(0, reader.ReadSpectrumRetentionTimes(7).Count);
                Assert.AreEqual(7L, reader.ReadSpectrum(7, false)!.Id);
            }

            // A spectrum whose record names no source file (0) is written as NULL, not as 0.
            using (var writer = BlibWriter.Open(path, true))
            {
                writer.DropIndexes();
                writer.AddSpectrum(new BlibSpectrumRecord { PeptideSeq = "NOFILEK", PeptideModSeq = "NOFILEK", PrecursorMz = 300.1, PrecursorCharge = 1 });
                writer.Complete();
            }
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3, ReadOnly = true };
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                using var command = new SQLiteCommand("SELECT COUNT(*) FROM RefSpectra WHERE fileID IS NULL", connection);
                Assert.AreEqual(1L, command.ExecuteScalar());
            }
            using (var reader = new BlibReader(path))
            {
                var spectra = reader.ReadSpectra(false).ToList();
                Assert.AreEqual(3, spectra.Count);
                Assert.AreEqual(0L, spectra[2].FileId);
            }

            // Deleting a source file takes its spectra and everything of theirs; the other file's
            // stay, and the header's count follows without Complete, as Skyline edits a library.
            using (var writer = BlibWriter.Open(path, true))
            {
                writer.DeleteSourceFiles(new[] { "a.raw", "missing.raw" });
                Assert.AreEqual(2, writer.SpectrumCount);
            }
            using (var reader = new BlibReader(path))
            {
                Assert.AreEqual(2, reader.Info.SpectrumCount);
                CollectionAssert.AreEqual(new[] { "b.raw" }, reader.ReadSourceFiles().Select(f => f.FileName).ToArray());
                CollectionAssert.AreEqual(new[] { withoutFile, withoutFile + 1 }, reader.ReadSpectra(false).Select(s => s.Id).ToArray());
                Assert.AreEqual(0, reader.ReadModifications(withFile).Count);
                Assert.AreEqual(0, reader.ReadAnnotations(withFile).Count);
                Assert.IsNull(reader.ReadPeaks(withFile, 1));
                Assert.AreEqual(0, reader.ReadProteins().Count);
                Assert.AreEqual(1, reader.CountRetentionTimes());
                Assert.AreEqual(withoutFile, reader.ReadRetentionTimes().Single().RefSpectraId);
            }
        }
        finally
        {
            DeleteTemp(path);
            DeleteTemp(redundantPath);
        }
    }

    /// <summary>
    /// A library written in place into a file another tool keeps a table in: that table stays,
    /// the library's tables are made anew each time, and the result reads as any library does.
    /// </summary>
    [TestMethod]
    public void Writer_CreatesLibraryInPlaceBesideAnotherToolsTable()
    {
        string path = TempPath();
        try
        {
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3 };
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                Execute(connection, "CREATE TABLE IrtLibrary (id INTEGER PRIMARY KEY, PeptideModSeq TEXT, Irt REAL)");
                Execute(connection, "INSERT INTO IrtLibrary VALUES (1, 'PEPTIDEK', 42.0)");
            }
            const string lsid = "urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:inplace.blib";
            for (int round = 1; round <= 2; round++)
            {
                using (var writer = BlibWriter.Create(path, lsid, false, true))
                {
                    for (int i = 0; i < round; i++)
                        writer.AddSpectrum(new BlibSpectrumRecord { PeptideSeq = "PEPTIDEK", PeptideModSeq = "PEPTIDEK", PrecursorMz = 500.25, PrecursorCharge = 1 + i });
                    writer.Complete();
                }
                using var reader = new BlibReader(path);
                Assert.AreEqual(lsid, reader.Info.Lsid);
                Assert.AreEqual(round, reader.Info.SpectrumCount, "Each in-place write starts the library's tables anew.");
                Assert.IsTrue(reader.HasTable(BlibReader.TABLE_RETENTION_TIMES));
                Assert.IsTrue(reader.HasTable("IrtLibrary"), "The other tool's table stays.");
                using var count = new SQLiteCommand("SELECT COUNT(*) FROM IrtLibrary", reader.Connection);
                Assert.AreEqual(1L, count.ExecuteScalar());
            }
        }
        finally
        {
            DeleteTemp(path);
        }
    }

    /// <summary>
    /// The RetentionTimes table of the schemas before the ion mobility units: the drift time
    /// columns of schemas 4 and 5 read as a drift time, and the value column of schemas 2 and 3
    /// as a drift time or a CCS by that schema's own type code, as Skyline has always read them.
    /// </summary>
    [TestMethod]
    public void Reader_ReadsLegacyRetentionTimeIonMobilityColumns()
    {
        string path = TempPath();
        try
        {
            var connectionString = new SQLiteConnectionStringBuilder { DataSource = path, Version = 3 };
            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                Execute(connection, "CREATE TABLE LibInfo (libLSID TEXT, createTime TEXT, numSpecs INTEGER, majorVersion INTEGER, minorVersion INTEGER)");
                Execute(connection, "INSERT INTO LibInfo VALUES ('urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:old.blib', 'Thu Nov  3 17:02:18 2011', 1, 1, 4)");
                Execute(connection, "CREATE TABLE RefSpectra (id INTEGER PRIMARY KEY AUTOINCREMENT, peptideSeq VARCHAR(150), precursorMZ REAL, " +
                                    "precursorCharge INTEGER, peptideModSeq VARCHAR(200), prevAA CHAR(1), nextAA CHAR(1), copies INTEGER, numPeaks INTEGER)");
                Execute(connection, "INSERT INTO RefSpectra VALUES (1, 'PEPTIDEK', 500.25, 2, 'PEPTIDEK', '-', '-', 1, 0)");
                Execute(connection, "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, RedundantRefSpectraID INTEGER, SpectrumSourceID INTEGER, " +
                                    "driftTimeMsec REAL, collisionalCrossSectionSqA REAL, driftTimeHighEnergyOffsetMsec REAL, retentionTime REAL, bestSpectrum INTEGER)");
                Execute(connection, "INSERT INTO RetentionTimes VALUES (1, 0, 1, 3.5, 250.0, -0.2, 12.0, 1)");
                Execute(connection, "INSERT INTO RetentionTimes VALUES (1, 0, 2, NULL, NULL, NULL, 12.5, 0)");
            }
            using (var reader = new BlibReader(path))
            {
                var times = reader.ReadSpectrumRetentionTimes(1);
                Assert.AreEqual(2, times.Count);
                Assert.AreEqual(3.5, times[0].IonMobility);
                Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_DRIFT_TIME, times[0].IonMobilityType);
                Assert.AreEqual(250.0, times[0].CollisionalCrossSectionSqA);
                Assert.AreEqual(-0.2, times[0].IonMobilityHighEnergyOffset);
                Assert.IsNull(times[1].IonMobility);
                Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_NONE, times[1].IonMobilityType);
            }

            using (var connection = new SQLiteConnection(connectionString.ToString()))
            {
                connection.Open();
                Execute(connection, "DROP TABLE RetentionTimes");
                Execute(connection, "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, RedundantRefSpectraID INTEGER, SpectrumSourceID INTEGER, " +
                                    "ionMobilityValue REAL, ionMobilityType INTEGER, ionMobilityHighEnergyDriftTimeOffsetMsec REAL, retentionTime REAL, bestSpectrum INTEGER)");
                Execute(connection, "INSERT INTO RetentionTimes VALUES (1, 0, 1, 3.5, 1, -0.2, 12.0, 1)");
                Execute(connection, "INSERT INTO RetentionTimes VALUES (1, 0, 2, 250.0, 2, 0, 12.5, 0)");
                Execute(connection, "INSERT INTO RetentionTimes VALUES (1, 0, 3, 0, 0, 0, 13.0, 0)");
            }
            using (var reader = new BlibReader(path))
            {
                var times = reader.ReadSpectrumRetentionTimes(1);
                Assert.AreEqual(3, times.Count);
                Assert.AreEqual(3.5, times[0].IonMobility);
                Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_DRIFT_TIME, times[0].IonMobilityType);
                Assert.IsNull(times[0].CollisionalCrossSectionSqA);
                Assert.AreEqual(-0.2, times[0].IonMobilityHighEnergyOffset);
                Assert.IsNull(times[1].IonMobility);
                Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_NONE, times[1].IonMobilityType);
                Assert.AreEqual(250.0, times[1].CollisionalCrossSectionSqA);
                Assert.IsNull(times[2].IonMobility, "A row of that schema's type none has no ion mobility.");
                Assert.AreEqual(BlibWriter.ION_MOBILITY_TYPE_NONE, times[2].IonMobilityType);
                Assert.IsNull(times[2].CollisionalCrossSectionSqA);
            }
        }
        finally
        {
            DeleteTemp(path);
        }
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"pwiz-sharp-blibreader-{Guid.NewGuid():N}.blib");
    }

    private static void Execute(SQLiteConnection connection, string sql)
    {
        using var command = new SQLiteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void DeleteTemp(string path)
    {
        SQLiteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort temp-file cleanup.
        }
    }
}
