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
