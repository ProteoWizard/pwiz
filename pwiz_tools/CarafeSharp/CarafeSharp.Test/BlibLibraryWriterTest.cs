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
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Writes a small library as a .blib and a TSV and reads them back: the BiblioSpec tables,
    /// the blob encoding (decoded independently with <see cref="ZLibStream"/>), and the
    /// invariants Skyline and Osprey rely on, namely non-NULL annotation text, mzObserved equal
    /// to the stored peak m/z, and one annotation per peak.
    /// </summary>
    [TestClass]
    public class BlibLibraryWriterTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestBlibRoundTrip()
        {
            // A ';' in the path must not end the connection string early.
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Blib;" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, BlibLibraryWriter.FILE_NAME);
            try
            {
                var spectra = CreateSpectra();
                using (var writer = new BlibLibraryWriter(path, @"carafe_spectral_library"))
                {
                    Assert.AreEqual(1, writer.WriteBatch(spectra.Take(2).ToList()));
                    Assert.AreEqual(3, writer.WriteBatch(spectra.Skip(2).ToList()));
                    writer.WriteDecoyPairs(new[]
                    {
                        new DecoyPairRow(1, false, false, 1, null),
                        new DecoyPairRow(2, true, false, 1, @"reverse"),
                    });
                    writer.Complete();
                }
                VerifyLibrary(path, spectra);
                CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(folder));

                // A run that stops before Complete leaves the previous library as it was, and
                // the library is readable while the next one is being written.
                using (var writer = new BlibLibraryWriter(path, @"carafe_spectral_library"))
                {
                    writer.WriteBatch(spectra.Take(1).ToList());
                    VerifyLibrary(path, spectra);
                }
                VerifyLibrary(path, spectra);
                CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(folder));
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void TestTsvRows()
        {
            var spectrum = CreateSpectra()[0];
            string expectedPrefix = "_PEPTC[UniMod:4]K_\tPEPTCK\t360.6578\t2\t-35.21\tsp|P1_pep00001|A;sp|P2_pep00002|B\t0\t";
            string[] rows = CarafeLibraryTsvWriter.FormatRows(spectrum).Split('\n');
            Assert.AreEqual(spectrum.Fragments.Count + 1, rows.Length);
            Assert.AreEqual(string.Empty, rows[rows.Length - 1]);
            Assert.AreEqual(expectedPrefix + "400.25\t1.0000\ty\t3\t1\tnoloss", rows[0]);
            Assert.AreEqual(expectedPrefix + "250.125\t0.5000\tb\t2\t1\tnoloss", rows[1]);
            Assert.AreEqual(13, CarafeLibraryTsvWriter.HEADER.Split('\t').Length);

            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Tsv_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, CarafeLibraryTsvWriter.FILE_NAME);
            try
            {
                using (var writer = new CarafeLibraryTsvWriter(path))
                {
                    writer.Write(spectrum);
                    writer.Complete();
                }
                string expected = CarafeLibraryTsvWriter.HEADER + "\n" + CarafeLibraryTsvWriter.FormatRows(spectrum);
                Assert.AreEqual(expected, File.ReadAllText(path));
                CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(folder));

                // A run that stops before Complete leaves the previous TSV as it was.
                using (var writer = new CarafeLibraryTsvWriter(path))
                {
                    writer.Write(CreateSpectra()[1]);
                    Assert.AreEqual(expected, File.ReadAllText(path));
                }
                Assert.AreEqual(expected, File.ReadAllText(path));
                CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(folder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// A spectrum's annotations go in one multi-row INSERT up to the top-N peaks and in
        /// several above it, and the rows, ids included, are those of one INSERT per peak.
        /// </summary>
        [TestMethod]
        public void TestAnnotationInserts()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Annotations_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                const int topN = BlibLibraryWriter.DEFAULT_PEAKS_PER_INSERT;
                // One peak, the top-N, one more than the top-N, and more than two INSERTs' worth.
                var peakCounts = new[] { 1, topN, topN + 1, 2 * topN + 5, 3 };
                var spectra = peakCounts.Select(CreateSpectrum).ToList();
                string multiRow = WriteLibrary(folder, @"multi_row.blib", spectra, topN);
                VerifyAnnotations(multiRow, spectra);

                // One row per INSERT, as before the multi-row INSERT, and INSERTs of 7 rows, give the same tables.
                string singleRow = WriteLibrary(folder, @"single_row.blib", spectra, 1);
                string sevenRows = WriteLibrary(folder, @"seven_rows.blib", spectra, 7);
                foreach (string table in new[] { @"RefSpectra", @"RefSpectraPeaks", @"RefSpectraPeakAnnotations", @"Modifications",
                             @"Proteins", @"RefSpectraProteins", @"RetentionTimes" })
                {
                    var expected = TableRows(singleRow, table);
                    Assert.IsTrue(expected.Count > 0, table);
                    CollectionAssert.AreEqual(expected, TableRows(multiRow, table), table);
                    CollectionAssert.AreEqual(expected, TableRows(sevenRows, table), table);
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(folder, true);
            }
        }

        private static List<LibrarySpectrum> CreateSpectra()
        {
            var target = new LibrarySpectrum(Precursor(@"PEPTCK", @"Carbamidomethyl@C", @"5", 2), 360.6578, -35.214714,
                @"sp|P1_pep00001|A;sp|P2_pep00002|B", 0, new[]
                {
                    Fragment('y', 3, 1, 400.25, 1.0f),
                    Fragment('b', 2, 1, 250.125, 0.5f),
                    Fragment('y', 2, 1, 300.0, 0.25f),
                })
            {
                ModifiedPeptide = @"_PEPTC[UniMod:4]K_",
                SkylineModifiedSequence = @"PEPTC[+57.02146372057]K",
                SkylineModifications = new[] { new SkylineModification(5, 57.02146372057) },
            };
            // Twenty peaks, so the blobs compress.
            var many = Enumerable.Range(1, 20).Select(i => Fragment(i % 2 == 0 ? 'b' : 'y', i, 1 + i % 2, 200 + i, 1.0f / i)).ToArray();
            var decoy = new LibrarySpectrum(Precursor(@"TPEPCK", @"Carbamidomethyl@C", @"5", 2), 360.6578, -30.5,
                @"decoy_sp|P1_pep00001|A", 0, many)
            {
                SkylineModifiedSequence = @"TPEPC[+57.02146372057]K",
                SkylineModifications = new[] { new SkylineModification(5, 57.02146372057) },
            };
            var entrapment = new LibrarySpectrum(Precursor(@"SAMPLER", string.Empty, string.Empty, 3), 267.4675, 12.5,
                @"sp|Q9_p_target_pep00052|X_p_target", 0, new[]
                {
                    new LibraryFragment('y', 5, 2, @"H3PO4", 301.1, (float)301.1, 1.0f),
                })
            {
                SkylineModifiedSequence = @"SAMPLER",
            };
            return new List<LibrarySpectrum> { target, decoy, entrapment };
        }

        private static PrecursorForm Precursor(string sequence, string mods, string sites, int charge)
        {
            return new PrecursorForm(PeptideForm.FromAlphabase(sequence, mods, sites), charge);
        }

        private static LibraryFragment Fragment(char type, int ordinal, int charge, double mz, float intensity)
        {
            return new LibraryFragment(type, ordinal, charge, LibraryFragment.NO_LOSS, mz + 1e-4, (float)mz, intensity);
        }

        private static void VerifyLibrary(string path, List<LibrarySpectrum> spectra)
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path, ReadOnly = true }.ToString()))
            {
                connection.Open();
                var tables = Column<string>(connection, @"SELECT name FROM sqlite_master WHERE type='table'");
                foreach (string table in new[] { @"LibInfo", @"ScoreTypes", @"IonMobilityTypes", @"SpectrumSourceFiles", @"RefSpectra",
                             @"RefSpectraPeaks", @"RefSpectraPeakAnnotations", @"Modifications", @"Proteins", @"RefSpectraProteins",
                             @"RetentionTimes", @"DecoyPairs" })
                {
                    CollectionAssert.Contains(tables, table);
                }
                CollectionAssert.AreEqual(new object[] { 3L, 1L, 11L },
                    Row(connection, @"SELECT numSpecs, majorVersion, minorVersion FROM LibInfo"));
                CollectionAssert.AreEqual(new object[] { @"UNKNOWN", @"NOT_A_PROBABILITY_VALUE" },
                    Row(connection, @"SELECT scoreType, probabilityType FROM ScoreTypes WHERE id = 0"));
                // TINYINT columns read back as bytes; CAST gives Int64 like the others.
                CollectionAssert.AreEqual(new object[] { 1L, @"carafe_spectral_library", 1L },
                    Row(connection, @"SELECT id, fileName, CAST(workflowType AS INTEGER) FROM SpectrumSourceFiles"));

                for (int i = 0; i < spectra.Count; i++)
                    VerifySpectrum(connection, i + 1, spectra[i]);

                Assert.AreEqual((long)spectra.Sum(s => s.Fragments.Count), Scalar(connection, @"SELECT COUNT(*) FROM RefSpectraPeakAnnotations"));
                Assert.AreEqual(0L, Scalar(connection, @"SELECT COUNT(*) FROM RefSpectraPeakAnnotations WHERE formula IS NULL OR " +
                                                     @"inchiKey IS NULL OR otherKeys IS NULL OR adduct IS NULL OR comment IS NULL"));
                CollectionAssert.AreEqual(new[] { @"decoy_sp|P1_pep00001|A", @"sp|P1_pep00001|A", @"sp|P2_pep00002|B", @"sp|Q9_p_target_pep00052|X_p_target" },
                    Column<string>(connection, @"SELECT accession FROM Proteins ORDER BY accession").ToArray());
                CollectionAssert.AreEqual(new[] { 1L, 1L, 2L, 3L },
                    Column<long>(connection, @"SELECT RefSpectraId FROM RefSpectraProteins ORDER BY RefSpectraId").ToArray());
                CollectionAssert.AreEqual(new object[] { 5L, 57.02146372057 },
                    Row(connection, @"SELECT position, mass FROM Modifications WHERE RefSpectraID = 1"));
                Assert.AreEqual(0L, Scalar(connection, @"SELECT COUNT(*) FROM Modifications WHERE RefSpectraID = 3"));
                CollectionAssert.AreEqual(new object[] { 2L, 1L, 0L, 1L, @"reverse" },
                    Row(connection, @"SELECT RefSpectraID, IsDecoy, IsEntrapment, PairID, Method FROM DecoyPairs WHERE IsDecoy = 1"));
                Assert.IsTrue(Row(connection, @"SELECT Method FROM DecoyPairs WHERE IsDecoy = 0")[0] is DBNull);
            }
        }

        private static void VerifySpectrum(SQLiteConnection connection, int id, LibrarySpectrum spectrum)
        {
            var row = Row(connection, @"SELECT peptideSeq, peptideModSeq, precursorMZ, precursorCharge, numPeaks, retentionTime, copies, " +
                                      @"fileID, score, CAST(scoreType AS INTEGER), moleculeName, startTime FROM RefSpectra WHERE id = " + id);
            Assert.AreEqual(spectrum.Sequence, row[0]);
            Assert.AreEqual(spectrum.SkylineModifiedSequence, row[1]);
            Assert.AreEqual(spectrum.PrecursorMz, row[2]);
            Assert.AreEqual((long)spectrum.Charge, row[3]);
            Assert.AreEqual((long)spectrum.Fragments.Count, row[4]);
            Assert.AreEqual(spectrum.RetentionTime, row[5]);
            CollectionAssert.AreEqual(new object[] { 1L, 1L, 0.0, 0L, string.Empty }, row.Skip(6).Take(5).ToArray());
            Assert.IsTrue(row[11] is DBNull);

            var retentionTime = Row(connection, @"SELECT retentionTime, SpectrumSourceID, bestSpectrum, startTime, endTime FROM RetentionTimes " +
                                                @"WHERE RefSpectraID = " + id);
            CollectionAssert.AreEqual(new object[] { spectrum.RetentionTime, 1L, 1L }, retentionTime.Take(3).ToArray());
            Assert.IsTrue(retentionTime[3] is DBNull && retentionTime[4] is DBNull);

            var blobs = Row(connection, @"SELECT peakMZ, peakIntensity FROM RefSpectraPeaks WHERE RefSpectraID = " + id);
            int count = spectrum.Fragments.Count;
            var mzBytes = Decode((byte[])blobs[0], count * sizeof(double));
            var intensityBytes = Decode((byte[])blobs[1], count * sizeof(float));
            Assert.AreEqual(count > 10, ((byte[])blobs[0]).Length < count * sizeof(double), @"m/z compression");
            var annotations = Rows(connection, @"SELECT peakIndex, name, charge, mzTheoretical, mzObserved FROM RefSpectraPeakAnnotations " +
                                               @"WHERE RefSpectraID = " + id + @" ORDER BY peakIndex");
            Assert.AreEqual(count, annotations.Count);
            for (int i = 0; i < count; i++)
            {
                var fragment = spectrum.Fragments[i];
                double peakMz = BitConverter.ToDouble(mzBytes, i * sizeof(double));
                Assert.AreEqual(fragment.Mz, peakMz);
                Assert.AreEqual(fragment.RelativeIntensity, BitConverter.ToSingle(intensityBytes, i * sizeof(float)));
                Assert.AreEqual((long)i, annotations[i][0]);
                string expectedName = fragment.IonType + fragment.Ordinal.ToString() + (fragment.HasLoss ? @"-" + fragment.LossType : string.Empty);
                Assert.AreEqual(expectedName, annotations[i][1]);
                Assert.AreEqual((long)fragment.Charge, annotations[i][2]);
                Assert.AreEqual(fragment.TheoreticalMz, annotations[i][3]);
                Assert.AreEqual(peakMz, annotations[i][4]);
            }
        }

        /// <summary>A spectrum with this many peaks, each distinct, some with a neutral loss.</summary>
        private static LibrarySpectrum CreateSpectrum(int peakCount, int index)
        {
            var fragments = Enumerable.Range(0, peakCount).Select(i => new LibraryFragment(i % 2 == 0 ? 'y' : 'b', i + 1, 1 + i % 3,
                i % 5 == 4 ? @"H2O" : LibraryFragment.NO_LOSS, 200 + 100 * index + i + 1e-4, 200.5f + 100 * index + i, 1.0f / (i + 1))).ToArray();
            return new LibrarySpectrum(Precursor(@"PEPTMCK", @"Carbamidomethyl@C", @"6", 2), 400.25 + index, 10.5 + index,
                @"sp|P" + index + @"|A;sp|P" + (index + 1) + @"|B", 0, fragments)
            {
                SkylineModifiedSequence = @"PEPTMC[+57.02146372057]K",
                SkylineModifications = new[] { new SkylineModification(6, 57.02146372057) },
            };
        }

        private static string WriteLibrary(string folder, string fileName, List<LibrarySpectrum> spectra, int peaksPerInsert)
        {
            string path = Path.Combine(folder, fileName);
            using (var writer = new BlibLibraryWriter(path, @"carafe_spectral_library", peaksPerInsert))
            {
                writer.WriteBatch(spectra);
                writer.Complete();
            }
            return path;
        }

        /// <summary>Every annotation row, in id order: spectrum by spectrum, then peak by peak.</summary>
        private static void VerifyAnnotations(string path, List<LibrarySpectrum> spectra)
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path, ReadOnly = true }.ToString()))
            {
                connection.Open();
                var rows = Rows(connection, @"SELECT id, RefSpectraID, peakIndex, name, formula, inchiKey, otherKeys, charge, adduct, comment, " +
                                            @"mzTheoretical, mzObserved FROM RefSpectraPeakAnnotations ORDER BY id");
                Assert.AreEqual(spectra.Sum(s => s.Fragments.Count), rows.Count);
                int row = 0;
                for (int i = 0; i < spectra.Count; i++)
                {
                    for (int peak = 0; peak < spectra[i].Fragments.Count; peak++)
                    {
                        var fragment = spectra[i].Fragments[peak];
                        string name = fragment.IonType + fragment.Ordinal.ToString(CultureInfo.InvariantCulture) +
                                      (fragment.HasLoss ? @"-" + fragment.LossType : string.Empty);
                        CollectionAssert.AreEqual(new object[] { row + 1L, i + 1L, (long)peak, name, string.Empty, string.Empty, string.Empty,
                            (long)fragment.Charge, string.Empty, string.Empty, fragment.TheoreticalMz, (double)fragment.Mz }, rows[row]);
                        row++;
                    }
                }
            }
        }

        /// <summary>A table's rows in rowid order, each value with its SQLite storage class, doubles round-trip, blobs in hex.</summary>
        private static List<string> TableRows(string path, string table)
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path, ReadOnly = true }.ToString()))
            {
                connection.Open();
                var columns = Column<string>(connection, @"SELECT name FROM pragma_table_info('" + table + @"')");
                string values = string.Join(@", ", columns.Select(column => @"typeof(" + column + @"), " + column));
                return Rows(connection, @"SELECT " + values + @" FROM " + table + @" ORDER BY rowid")
                    .Select(row => string.Join(@"|", row.Select(FormatValue))).ToList();
            }
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case byte[] bytes:
                    return Convert.ToHexString(bytes);
                case double number:
                    return number.ToString(@"R", CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The BiblioSpec reader's rule, independently of the writer's code: raw when the size matches.</summary>
        private static byte[] Decode(byte[] blob, int expectedLength)
        {
            if (blob.Length == expectedLength)
                return blob;
            using (var input = new MemoryStream(blob))
            using (var zlib = new ZLibStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zlib.CopyTo(output);
                var bytes = output.ToArray();
                Assert.AreEqual(expectedLength, bytes.Length);
                CollectionAssert.AreEqual(bytes, BlibLibraryWriter.DecodeBlob(blob, expectedLength));
                return bytes;
            }
        }

        private static object Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection))
                return command.ExecuteScalar();
        }

        private static object[] Row(SQLiteConnection connection, string sql)
        {
            return Rows(connection, sql).Single();
        }

        private static List<object[]> Rows(SQLiteConnection connection, string sql)
        {
            var rows = new List<object[]>();
            using (var command = new SQLiteCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);
                    rows.Add(values);
                }
            }
            return rows;
        }

        private static List<T> Column<T>(SQLiteConnection connection, string sql)
        {
            return Rows(connection, sql).Select(r => (T)r[0]).ToList();
        }
    }
}
