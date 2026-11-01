/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Parquet;
using Parquet.Schema;
using Pwiz.Data.MsData.Readers;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for Osprey.IO types: OspreyBlibWriter, SpectraCache, SpectrumFileReader, and ParquetScoreCache.
    /// Ported from osprey-io Rust tests.
    /// </summary>
    [TestClass]
    public class IOTest
    {
        #region CarafeProteinIdNormalizer Tests

        /// <summary>
        /// Carafe writes one synthetic accession PER PEPTIDE
        /// (<c>sp|O95139_pep00019|NDUB6_HUMAN</c>), so every peptide becomes its own protein.
        /// Left in place that breaks protein parsimony and, worse, silently empties the
        /// <c>protein-compact</c> stratum - "proteins with &gt;=2 detected peptides" can never
        /// fire when each protein owns one peptide. Measured on the Stellar entrapment library:
        /// 7 proteins / 23 base_ids before stripping, 4,022 / 167,660 after.
        /// </summary>
        [TestMethod]
        public void TestCarafeProteinIdNormalizer()
        {
            // Two peptides of ONE real protein, each carrying its own pseudo-accession, plus the
            // entrapment and decoy forms whose markers must survive intact.
            var library = new List<LibraryEntry>
            {
                ProtEntry(@"PEPTIDEK", 2, @"sp|O95139_pep00019|NDUB6_HUMAN"),
                ProtEntry(@"PEPTIDER", 2, @"sp|O95139_pep00042|NDUB6_HUMAN"),
                ProtEntry(@"ENTRAPK", 2, @"sp|Q9ULK4_p_target_pep00052|MED23_HUMAN_p_target"),
                ProtEntry(@"DECOYK", 2, @"decoy_sp|Q04726_p_target_pep00087|TLE3_HUMAN_p_target"),
            };
            string warning = null;
            int n = CarafeProteinIdNormalizer.Normalize(library, w => warning = w);

            Assert.AreEqual(4, n);
            Assert.IsNotNull(warning, @"stripping the suffix must warn - it changes protein-level results");
            // The two peptides now share ONE protein, which is the whole point: only then can a
            // protein reach the 2-detected-peptide bar protein-compact stratifies on.
            Assert.AreEqual(@"sp|O95139|NDUB6_HUMAN", library[0].ProteinIds[0]);
            Assert.AreEqual(@"sp|O95139|NDUB6_HUMAN", library[1].ProteinIds[0]);
            // Entrapment and decoy markers are NOT part of the suffix and must be preserved -
            // the entrapment form must match what the pairing manifest's `proteins` column holds,
            // or classification and pairing would stop agreeing with it.
            Assert.AreEqual(@"sp|Q9ULK4_p_target|MED23_HUMAN_p_target", library[2].ProteinIds[0]);
            Assert.AreEqual(@"decoy_sp|Q04726_p_target|TLE3_HUMAN_p_target", library[3].ProteinIds[0]);
        }

        /// <summary>
        /// The no-op paths: a clean library must be left byte-identical and must NOT warn, and an
        /// accession containing a bare "_pep" with no digits is a real accession, not a Carafe
        /// suffix. Also covers the collapse case - once the suffix is gone, two accessions on one
        /// entry can become the same protein and must not be stored twice.
        /// </summary>
        [TestMethod]
        public void TestCarafeProteinIdNormalizerLeavesCleanLibraries()
        {
            var clean = new List<LibraryEntry>
            {
                ProtEntry(@"PEPTIDEK", 2, @"sp|Q9NP61|ARFG3_HUMAN"),
                ProtEntry(@"PEPTIDER", 2, @"sp|P00761_peptidase|TRYP_PIG"),
            };
            string warning = null;
            Assert.AreEqual(0, CarafeProteinIdNormalizer.Normalize(clean, w => warning = w));
            Assert.IsNull(warning, @"a library with no Carafe suffix must not warn");
            Assert.AreEqual(@"sp|Q9NP61|ARFG3_HUMAN", clean[0].ProteinIds[0]);
            Assert.AreEqual(@"sp|P00761_peptidase|TRYP_PIG", clean[1].ProteinIds[0],
                @"'_pep' without digits is part of a real accession and must survive");

            var collapsing = new List<LibraryEntry> { ProtEntry(@"SHAREDK", 2, null) };
            collapsing[0].ProteinIds = new[] { @"sp|P1_pep00001|A", @"sp|P1_pep00002|A", @"sp|P2_pep00003|B" };
            Assert.AreEqual(1, CarafeProteinIdNormalizer.Normalize(collapsing, w => { }));
            CollectionAssert.AreEqual(new[] { @"sp|P1|A", @"sp|P2|B" },
                collapsing[0].ProteinIds.ToArray(),
                @"accessions that become identical once the suffix is stripped must collapse to one");
        }

        private static uint _protEntryId;

        private static LibraryEntry ProtEntry(string sequence, byte charge, string proteinId)
        {
            var entry = new LibraryEntry(++_protEntryId, sequence, sequence, charge, 500.0, 10.0);
            if (proteinId != null)
                entry.ProteinIds = new[] { proteinId };
            return entry;
        }

        #endregion

        #region BlibWriter Tests

        /// <summary>
        /// Verifies that a blib database can be created, a spectrum added, and the schema exists.
        /// </summary>
        [TestMethod]
        public void TestCreateBlib()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    Assert.IsTrue(fileId > 0);

                    long refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        500.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0, 400.0, 500.0 },
                        new[] { 100.0f, 200.0f, 300.0f },
                        0.01, fileId, 1, 0.0);
                    Assert.IsTrue(refId > 0);

                    writer.AddRetentionTime(refId, fileId, 10.0, 9.0, 11.0, 0.01, true);
                    writer.AddMetadata(OspreyBlibWriter.KEY_VERSION, "0.1.0");
                    writer.Complete();
                }

                // Verify schema and data by opening a separate read connection
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();

                    VerifyTableExists(conn, "LibInfo");
                    VerifyTableExists(conn, "RefSpectra");
                    VerifyTableExists(conn, "RefSpectraPeaks");
                    VerifyTableExists(conn, "Modifications");
                    VerifyTableExists(conn, "RetentionTimes");
                    VerifyTableExists(conn, "Proteins");
                    VerifyTableExists(conn, "RefSpectraProteins");

                    // Verify numSpecs updated
                    using (var cmd = new SQLiteCommand("SELECT numSpecs FROM LibInfo", conn))
                    {
                        long numSpecs = (long)(cmd.ExecuteScalar() ?? 0);
                        Assert.AreEqual(1L, numSpecs);
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that modifications are stored with 1-based positions (converted from 0-based).
        /// </summary>
        [TestMethod]
        public void TestBlibModifications1Based()
        {
            string path = Path.GetTempFileName();
            try
            {
                long refId;
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    var mods = new List<Modification>
                    {
                        new Modification { Position = 4, UnimodId = 4, MassDelta = 57.021464, Name = "Carbamidomethyl" }
                    };
                    refId = writer.AddSpectrum(
                        "PEPTCIDE", "PEPTC[+57.021]IDE",
                        500.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0, mods);
                    writer.Complete();
                }

                // Verify stored as 1-based: position 4 (0-based) -> 5 (1-based)
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "SELECT position FROM Modifications WHERE RefSpectraID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", refId);
                        int storedPos = Convert.ToInt32(cmd.ExecuteScalar());
                        Assert.AreEqual(5, storedPos, "Position should be 1-based (0-based 4 -> 1-based 5)");
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that modified sequences with UniMod notation are converted to mass notation
        /// when written to the database.
        /// </summary>
        [TestMethod]
        public void TestModSeqRoundtrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                long refId;
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTCIDE", "PEPTC[UniMod:4]IDE",
                        500.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0);
                    writer.Complete();
                }

                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "SELECT peptideModSeq FROM RefSpectra WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", refId);
                        string storedModSeq = (string)cmd.ExecuteScalar() ?? string.Empty;
                        Assert.IsTrue(storedModSeq.Contains("[+57."),
                            "Expected mass notation, got: " + storedModSeq);
                        Assert.IsFalse(storedModSeq.Contains("UniMod"),
                            "UniMod notation should be converted: " + storedModSeq);
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that fragment m/z and intensity values survive the write-read round trip
        /// through zlib compression.
        /// </summary>
        [TestMethod]
        public void TestFragmentRoundtrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                double[] mzs = { 175.1190, 288.2030, 375.2351, 488.3191, 601.4032 };
                float[] intensities = { 100.0f, 80.5f, 60.0f, 45.2f, 30.0f };
                long refId;

                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        400.0, 2,
                        10.0, 9.0, 11.0,
                        mzs, intensities,
                        0.005, fileId, 1, 0.0);
                    writer.Complete();
                }

                // Read back from SQLite using a separate connection
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "SELECT p.peakMZ, p.peakIntensity, r.numPeaks " +
                        "FROM RefSpectraPeaks p JOIN RefSpectra r ON r.id = p.RefSpectraID " +
                        "WHERE p.RefSpectraID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", refId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            Assert.IsTrue(reader.Read());

                            byte[] mzBlob = (byte[])reader["peakMZ"];
                            byte[] intBlob = (byte[])reader["peakIntensity"];
                            int numPeaks = Convert.ToInt32(reader["numPeaks"]);

                            Assert.AreEqual(5, numPeaks);

                            // Decompress and verify m/z values
                            double[] readMzs = BlibWriter.DecodeMzs(mzBlob, mzs.Length);
                            Assert.AreEqual(mzs.Length, readMzs.Length);
                            for (int i = 0; i < mzs.Length; i++)
                            {
                                Assert.AreEqual(mzs[i], readMzs[i], 1e-10,
                                    string.Format("m/z mismatch at index {0}", i));
                            }

                            // Decompress and verify intensities
                            float[] readInts = BlibWriter.DecodeIntensities(intBlob, intensities.Length);
                            Assert.AreEqual(intensities.Length, readInts.Length);
                            for (int i = 0; i < intensities.Length; i++)
                            {
                                Assert.AreEqual(intensities[i], readInts[i], 1e-5f,
                                    string.Format("intensity mismatch at index {0}", i));
                            }
                        }
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that multi-run retention times are stored with correct best_spectrum flag
        /// and that nullable retentionTime works correctly.
        /// </summary>
        [TestMethod]
        public void TestRetentionTimesMultirun()
        {
            string path = Path.GetTempFileName();
            try
            {
                long refId;
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId1 = writer.AddSourceFile("run1.mzML", "library.tsv", 0.01);
                    long fileId2 = writer.AddSourceFile("run2.mzML", "library.tsv", 0.01);
                    long fileId3 = writer.AddSourceFile("run3.mzML", "library.tsv", 0.01);

                    refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        400.0, 2,
                        10.5, 9.5, 11.5,
                        new[] { 300.0, 400.0 },
                        new[] { 100.0f, 80.0f },
                        0.005, fileId1, 3, 0.0);

                    // Run 1: has ID, not best
                    writer.AddRetentionTime(refId, fileId1, 10.5, 9.5, 11.5, 0.008, false);
                    // Run 2: has ID, best
                    writer.AddRetentionTime(refId, fileId2, 10.3, 9.3, 11.3, 0.005, true);
                    // Run 3: no ID (null RT), has boundaries
                    writer.AddRetentionTime(refId, fileId3, null, 9.7, 11.7, 0.012, false);
                    writer.Complete();
                }

                // Read back RetentionTimes using a separate connection
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "SELECT SpectrumSourceID, retentionTime, startTime, endTime, bestSpectrum " +
                        "FROM RetentionTimes WHERE RefSpectraID = @id ORDER BY SpectrumSourceID",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("@id", refId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            // Run 1: has ID, not best
                            Assert.IsTrue(reader.Read());
                            Assert.AreEqual(0, Convert.ToInt32(reader["bestSpectrum"]));
                            Assert.AreEqual(10.5, Convert.ToDouble(reader["retentionTime"]), 0.01);
                            Assert.AreEqual(9.5, Convert.ToDouble(reader["startTime"]), 0.01);

                            // Run 2: has ID, best
                            Assert.IsTrue(reader.Read());
                            Assert.AreEqual(1, Convert.ToInt32(reader["bestSpectrum"]));
                            Assert.AreEqual(10.3, Convert.ToDouble(reader["retentionTime"]), 0.01);

                            // Run 3: no ID (null RT), has boundaries
                            Assert.IsTrue(reader.Read());
                            Assert.AreEqual(0, Convert.ToInt32(reader["bestSpectrum"]));
                            Assert.IsTrue(reader.IsDBNull(reader.GetOrdinal("retentionTime")),
                                "Run 3 should have NULL retentionTime");
                            Assert.AreEqual(9.7, Convert.ToDouble(reader["startTime"]), 0.01);
                            Assert.AreEqual(11.7, Convert.ToDouble(reader["endTime"]), 0.01);
                        }
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that protein accession mappings are correctly stored and retrievable.
        /// </summary>
        [TestMethod]
        public void TestProteinMappingRoundtrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                long refId;
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    var proteins = new List<string>
                    {
                        "sp|P12345|PROT_HUMAN",
                        "sp|P67890|PROT2_HUMAN"
                    };
                    refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        400.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0, proteins: proteins);
                    writer.Complete();
                }

                // Read back protein accessions via join using a separate connection
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "SELECT p.accession FROM Proteins p " +
                        "JOIN RefSpectraProteins rp ON p.id = rp.ProteinID " +
                        "WHERE rp.RefSpectraID = @id ORDER BY p.accession",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("@id", refId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            Assert.IsTrue(reader.Read());
                            Assert.AreEqual("sp|P12345|PROT_HUMAN", reader.GetString(0));
                            Assert.IsTrue(reader.Read());
                            Assert.AreEqual("sp|P67890|PROT2_HUMAN", reader.GetString(0));
                            Assert.IsFalse(reader.Read());
                        }
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies the experiment-level columns of a search blib: absent from a library export,
        /// present once <see cref="OspreyBlibWriter.AddSearchGlobalColumns"/> asks for them, then
        /// a precursor q-value, precursor PEP and peptide q-value on each spectrum and a q-value
        /// on each protein group, with a group shared by two spectra written once and a spectrum
        /// linked to two groups.
        /// </summary>
        [TestMethod]
        public void TestSearchGlobalColumns()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var writer = new OspreyBlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    AddTestSpectrum(writer, fileId, 2);
                    writer.Complete();
                }
                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    Assert.IsFalse(HasColumn(conn, "RefSpectra", "PrecursorQValue"));
                    Assert.IsFalse(HasColumn(conn, "RefSpectra", "PrecursorPep"));
                    Assert.IsFalse(HasColumn(conn, "RefSpectra", "PeptideQValue"));
                    Assert.IsFalse(HasColumn(conn, "Proteins", "QValue"));
                }

                long refId2, refId3;
                using (var writer = new OspreyBlibWriter(path))
                {
                    writer.AddSearchGlobalColumns();
                    writer.BeginBatch();
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId2 = AddTestSpectrum(writer, fileId, 2, new SearchGlobalValues(0.003, 0.02, 0.004));
                    refId3 = AddTestSpectrum(writer, fileId, 3, new SearchGlobalValues(0.006, 0.05, 0.004));
                    long groupA = writer.AddProteinGroup("sp|P11111|A_HUMAN;sp|P22222|B_HUMAN", 0.002);
                    long groupC = writer.AddProteinGroup("sp|P33333|C_HUMAN", 1.0);
                    Assert.AreEqual(groupA, writer.AddProteinGroup("sp|P11111|A_HUMAN;sp|P22222|B_HUMAN", 0.002));
                    writer.AddSpectrumProtein(refId2, groupA);
                    writer.AddSpectrumProtein(refId2, groupC);
                    writer.AddSpectrumProtein(refId3, groupA);
                    writer.CommitBatch();
                    writer.Complete();
                }

                using (var conn = new SQLiteConnection("Data Source=" + path + ";Version=3;Read Only=True;"))
                {
                    conn.Open();
                    Assert.AreEqual(0.003, QueryDouble(conn, "SELECT PrecursorQValue FROM RefSpectra WHERE id = " + refId2), 1e-12);
                    Assert.AreEqual(0.006, QueryDouble(conn, "SELECT PrecursorQValue FROM RefSpectra WHERE id = " + refId3), 1e-12);
                    Assert.AreEqual(0.02, QueryDouble(conn, "SELECT PrecursorPep FROM RefSpectra WHERE id = " + refId2), 1e-12);
                    Assert.AreEqual(0.05, QueryDouble(conn, "SELECT PrecursorPep FROM RefSpectra WHERE id = " + refId3), 1e-12);
                    Assert.AreEqual(0.004, QueryDouble(conn, "SELECT PeptideQValue FROM RefSpectra WHERE id = " + refId2), 1e-12);
                    Assert.AreEqual(0.004, QueryDouble(conn, "SELECT PeptideQValue FROM RefSpectra WHERE id = " + refId3), 1e-12);
                    Assert.AreEqual(2L, QueryLong(conn, "SELECT COUNT(*) FROM Proteins"));
                    Assert.AreEqual(0.002, QueryDouble(conn,
                        "SELECT QValue FROM Proteins WHERE accession = 'sp|P11111|A_HUMAN;sp|P22222|B_HUMAN'"), 1e-12);
                    Assert.AreEqual(2L, QueryLong(conn, "SELECT COUNT(*) FROM RefSpectraProteins WHERE RefSpectraID = " + refId2));
                    Assert.AreEqual(2L, QueryLong(conn,
                        "SELECT COUNT(*) FROM RefSpectraProteins rp JOIN Proteins p ON p.id = rp.ProteinID " +
                        "WHERE p.accession = 'sp|P11111|A_HUMAN;sp|P22222|B_HUMAN'"));
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        private static long AddTestSpectrum(OspreyBlibWriter writer, long fileId, int charge, SearchGlobalValues globalValues = null)
        {
            return writer.AddSpectrum("PEPTIDE", "PEPTIDE", 800.0 / charge, charge,
                10.0, 9.0, 11.0, new[] { 300.0 }, new[] { 100.0f }, 0.01, fileId, 1, 0.0, null, null, globalValues);
        }

        private static bool HasColumn(SQLiteConnection conn, string table, string column)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ")", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.GetString(1) == column)
                        return true;
                }
            }
            return false;
        }

        private static double QueryDouble(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                return Convert.ToDouble(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static long QueryLong(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Verifies that StripFlankingChars correctly removes flanking amino acid notation.
        /// </summary>
        [TestMethod]
        public void TestStripFlankingChars()
        {
            // No flanking chars
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars("PEPTIDE"));

            // Underscores (DIA-NN format)
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars("_PEPTIDE_"));

            // Periods/underscores (Prosit format)
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars("_.PEPTIDE._"));

            // Flanking amino acids with periods (standard notation)
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars("K.PEPTIDE.R"));

            // Just periods
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars(".PEPTIDE."));

            // Modified sequence with flanking chars
            Assert.AreEqual("PEPTC[+57.021]IDE",
                BlibPeptideText.StripFlankingChars("_PEPTC[+57.021]IDE_"));

            // Dashes at ends
            Assert.AreEqual("PEPTIDE", BlibPeptideText.StripFlankingChars("-PEPTIDE-"));

            // Dots inside modification brackets must NOT be treated as flanking separators
            Assert.AreEqual("C[+57.02146]GPSPC[+57.02146]GLLK",
                BlibPeptideText.StripFlankingChars("C[+57.02146]GPSPC[+57.02146]GLLK"));

            // Multiple modifications with decimal dots
            Assert.AreEqual("AC[+57.02146]AC[+57.02146]AHGMLAEDGASC[+57.02146]R",
                BlibPeptideText.StripFlankingChars("AC[+57.02146]AC[+57.02146]AHGMLAEDGASC[+57.02146]R"));

            // Flanking chars with modification dots
            Assert.AreEqual("C[+57.02146]PEPTIDE",
                BlibPeptideText.StripFlankingChars("K.C[+57.02146]PEPTIDE.R"));

            // N-terminal modification with dot
            Assert.AreEqual("[+42.011]PEPTC[+57.02146]IDE",
                BlibPeptideText.StripFlankingChars("[+42.011]PEPTC[+57.02146]IDE"));
        }

        /// <summary>
        /// Verifies that UniMod IDs are correctly converted to mass deltas.
        /// </summary>
        [TestMethod]
        public void TestConvertUnimodToMass()
        {
            // No modifications
            Assert.AreEqual("PEPTIDE", BlibPeptideText.ConvertUnimodToMass("PEPTIDE"));

            // Already mass notation - unchanged
            Assert.AreEqual("PEPTC[+57.021]IDE",
                BlibPeptideText.ConvertUnimodToMass("PEPTC[+57.021]IDE"));

            // UniMod:4 (Carbamidomethyl)
            string result = BlibPeptideText.ConvertUnimodToMass("PEPTC[UniMod:4]IDE");
            Assert.IsTrue(result.StartsWith("PEPTC[+57."),
                "Expected mass notation for UniMod:4, got: " + result);
            Assert.IsTrue(result.EndsWith("]IDE"),
                "Expected to end with ]IDE, got: " + result);

            // UniMod:35 (Oxidation)
            result = BlibPeptideText.ConvertUnimodToMass("PEPTM[UniMod:35]IDE");
            Assert.IsTrue(result.StartsWith("PEPTM[+15."),
                "Expected mass notation for UniMod:35, got: " + result);

            // Multiple modifications
            result = BlibPeptideText.ConvertUnimodToMass("PEPTC[UniMod:4]M[UniMod:35]IDE");
            Assert.IsTrue(result.Contains("[+57."), "Should contain Carbamidomethyl mass");
            Assert.IsTrue(result.Contains("[+15."), "Should contain Oxidation mass");

            // Unknown UniMod - kept as-is
            Assert.AreEqual("PEPTX[UniMod:99999]IDE",
                BlibPeptideText.ConvertUnimodToMass("PEPTX[UniMod:99999]IDE"));

            // Case insensitive UNIMOD
            result = BlibPeptideText.ConvertUnimodToMass("PEPTC[UNIMOD:4]IDE");
            Assert.IsTrue(result.StartsWith("PEPTC[+57."),
                "Case-insensitive UNIMOD should work, got: " + result);

            // Negative masses: Glu->pyro-Glu is UniMod:27, Gln->pyro-Glu UniMod:28
            Assert.AreEqual("E[-18.0106]PEPTIDE", BlibPeptideText.ConvertUnimodToMass("E[UniMod:27]PEPTIDE"));
            Assert.AreEqual("Q[-17.0265]PEPTIDE", BlibPeptideText.ConvertUnimodToMass("Q[UniMod:28]PEPTIDE"));

            // Verify TryGetUnimodMass for known IDs
            double mass;
            Assert.IsTrue(BlibPeptideText.TryGetUnimodMass(4, out mass));
            Assert.AreEqual(57.021464, mass, 1e-6);
            Assert.IsTrue(BlibPeptideText.TryGetUnimodMass(35, out mass));
            Assert.AreEqual(15.994915, mass, 1e-6);
            Assert.IsFalse(BlibPeptideText.TryGetUnimodMass(99999, out mass));

            // The ids Osprey once mapped to another modification's mass, now Skyline's UniModData
            // masses: every reader and writer shares the one table.
            var skylineMasses = new Dictionary<int, double>
            {
                { 27, -18.010565 },   // Glu->pyro-Glu
                { 28, -17.026549 },   // Gln->pyro-Glu, once Glu->pyro-Glu
                { 122, 27.994915 },   // Formyl, once SUMO
                { 214, 144.102062 },  // iTRAQ4plex, once Nitro
                { 312, 119.004099 },  // Cysteinyl, once Ammonia-loss
                { 354, 44.985078 },   // Nitro
                { 385, -17.026549 },  // Ammonia-loss, once TMT6plex
                { 737, 229.162932 },  // TMT6plex
                { 747, 86.000394 },   // Malonyl, once TMTpro
                { 2016, 304.207146 }, // TMTpro
                { 259, 8.014199 },    // Label:13C(6)15N(2)
                { 267, 10.008269 },   // Label:13C(6)15N(4)
            };
            foreach (var pair in skylineMasses)
            {
                Assert.IsTrue(BlibPeptideText.TryGetUnimodMass(pair.Key, out mass), pair.Key.ToString());
                Assert.AreEqual(pair.Value, mass, pair.Key.ToString());
                Assert.AreEqual(pair.Value, DiannTsvLoader.UnimodIdToMass(pair.Key), pair.Key.ToString());
            }
        }

        #endregion

        #region SpectraCache Tests

        /// <summary>
        /// Verifies that MS2 and MS1 spectra survive save/load round trip through the binary cache.
        /// </summary>
        [TestMethod]
        public void TestSpectraCacheRoundTrip()
        {
            string path = Path.GetTempFileName();
            try
            {
                var ms2 = new List<Spectrum>
                {
                    new Spectrum
                    {
                        ScanNumber = 1,
                        RetentionTime = 10.5,
                        PrecursorMz = 500.0,
                        IsolationWindow = new IsolationWindow(500.0, 1.5, 1.5),
                        Mzs = new [] { 100.0, 200.0, 300.0 },
                        Intensities = new[] { 1000.0f, 2000.0f, 500.0f }
                    },
                    new Spectrum
                    {
                        ScanNumber = 2,
                        RetentionTime = 11.0,
                        PrecursorMz = 600.0,
                        IsolationWindow = new IsolationWindow(600.0, 2.0, 3.0),
                        Mzs = new [] { 150.0, 250.0 },
                        Intensities = new [] { 800.0f, 1200.0f }
                    }
                };

                var ms1 = new List<MS1Spectrum>
                {
                    new MS1Spectrum
                    {
                        ScanNumber = 0,
                        RetentionTime = 10.0,
                        Mzs = new[] { 400.0, 500.0, 600.0 },
                        Intensities = new[] { 5000.0f, 3000.0f, 1000.0f }
                    }
                };

                SpectraCache.SaveSpectraCache(path, ms2, ms1);
                SpectraCacheResult loaded = SpectraCache.LoadSpectraCache(path);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(2, loaded.Ms2Spectra.Count);
                Assert.AreEqual(1, loaded.Ms1Spectra.Count);

                // Verify MS2 spectrum 0
                Assert.AreEqual((uint)1, loaded.Ms2Spectra[0].ScanNumber);
                Assert.AreEqual(10.5, loaded.Ms2Spectra[0].RetentionTime, 1e-10);
                Assert.AreEqual(500.0, loaded.Ms2Spectra[0].PrecursorMz, 1e-10);
                CollectionAssert.AreEqual(new[] { 100.0, 200.0, 300.0 }, loaded.Ms2Spectra[0].Mzs);
                CollectionAssert.AreEqual(new[] { 1000.0f, 2000.0f, 500.0f }, loaded.Ms2Spectra[0].Intensities);

                // Verify MS2 spectrum 1 isolation window
                Assert.AreEqual(2.0, loaded.Ms2Spectra[1].IsolationWindow.LowerOffset, 1e-10);
                Assert.AreEqual(3.0, loaded.Ms2Spectra[1].IsolationWindow.UpperOffset, 1e-10);

                // Verify MS1 spectrum
                Assert.AreEqual((uint)0, loaded.Ms1Spectra[0].ScanNumber);
                Assert.AreEqual(10.0, loaded.Ms1Spectra[0].RetentionTime, 1e-10);
                CollectionAssert.AreEqual(new[] { 400.0, 500.0, 600.0 }, loaded.Ms1Spectra[0].Mzs);
                CollectionAssert.AreEqual(new[] { 5000.0f, 3000.0f, 1000.0f }, loaded.Ms1Spectra[0].Intensities);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that empty spectrum lists round-trip correctly.
        /// </summary>
        [TestMethod]
        public void TestEmptySpectraCache()
        {
            string path = Path.GetTempFileName();
            try
            {
                SpectraCache.SaveSpectraCache(path, new List<Spectrum>(), new List<MS1Spectrum>());
                SpectraCacheResult loaded = SpectraCache.LoadSpectraCache(path);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(0, loaded.Ms2Spectra.Count);
                Assert.AreEqual(0, loaded.Ms1Spectra.Count);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that <see cref="SpectraWindowIndex"/> streams each isolation
        /// window's MS2 spectra byte-for-byte identically to the resident grouping
        /// that scoring builds today from <see cref="SpectraCache.LoadSpectraCache"/>.
        /// This is the load-bearing correctness gate for the per-window streaming
        /// refactor: same window key (Round(center*10)), same file-order membership
        /// (including two distinct centers that round to one key), same decoded
        /// fields/peaks, an empty-peak record, MS1 records the index must skip, an
        /// absent key returning empty, AllMs2Rts mirroring the file-order RTs, and
        /// invalid/missing caches rejected the same way LoadSpectraCache rejects them.
        /// </summary>
        [TestMethod]
        public void TestSpectraWindowIndex()
        {
            // Interleaved DIA-style cycling across three windows in the INPUT (file /
            // acquisition order), so the v4 writer's window grouping makes on-disk order
            // differ from file order -- this exercises both the grouping and the readers'
            // reconstruction of file-order membership. 500.03 and 699.98 round to the
            // same keys as 500.0 and 700.0, and one record has zero peaks.
            var ms2 = new List<Spectrum>
            {
                MakeIndexMs2(1, 10.0, 500.00, 3),
                MakeIndexMs2(2, 10.0, 600.00, 2),
                MakeIndexMs2(3, 10.0, 700.00, 5),
                MakeIndexMs2(4, 10.1, 500.03, 1),
                MakeIndexMs2(5, 10.1, 600.00, 0),
                MakeIndexMs2(6, 10.1, 699.98, 4),
                MakeIndexMs2(7, 10.2, 500.00, 2),
                MakeIndexMs2(8, 10.2, 600.00, 3),
                MakeIndexMs2(9, 10.2, 700.00, 1),
            };
            var ms1 = new List<MS1Spectrum>
            {
                new MS1Spectrum
                {
                    ScanNumber = 0, RetentionTime = 9.9,
                    Mzs = new[] { 400.0, 500.0 }, Intensities = new[] { 10.0f, 20.0f }
                },
                new MS1Spectrum
                {
                    ScanNumber = 10, RetentionTime = 10.3,
                    Mzs = new[] { 450.0 }, Intensities = new[] { 30.0f }
                },
            };

            string path = Path.GetTempFileName();
            try
            {
                SpectraCache.SaveSpectraCache(path, ms2, ms1);

                // Reference grouping = exactly what RunCoelutionScoring builds today.
                SpectraCacheResult full = SpectraCache.LoadSpectraCache(path);
                Assert.IsNotNull(full);

                // LoadSpectraCache must return MS2 in ACQUISITION (file) order even
                // though the v4 body is physically window-grouped -- Stage-6 rescore
                // relies on a full resident load in file order, so grouping must not
                // force it to stream. The interleaved input makes grouped order differ
                // from file order, so this catches a loader that returns on-disk order.
                Assert.AreEqual(ms2.Count, full.Ms2Spectra.Count);
                for (int i = 0; i < ms2.Count; i++)
                {
                    Assert.AreEqual(ms2[i].ScanNumber, full.Ms2Spectra[i].ScanNumber);
                    Assert.AreEqual(ms2[i].RetentionTime, full.Ms2Spectra[i].RetentionTime, 0.0);
                }

                var expected = GroupByWindowKey(full.Ms2Spectra);

                SpectraWindowIndex index = SpectraWindowIndex.BuildFromCache(path);
                Assert.IsNotNull(index);
                Assert.AreEqual(full.Ms2Spectra.Count, index.Ms2Count);

                // Each window's streamed spectra are byte-identical, in file order.
                int streamedTotal = 0;
                foreach (var kvp in expected)
                {
                    List<Spectrum> streamed = index.LoadWindow(kvp.Key);
                    AssertSpectraListEqual(kvp.Value, streamed);
                    streamedTotal += streamed.Count;
                    // One block read decodes to the same spectra; the last window's block ends
                    // at the MS1 section, the others at the next window's first record.
                    AssertSpectraListEqual(kvp.Value, index.LoadWindowSerialRead(kvp.Key));
                }
                // No record lost or double-counted across the window partition.
                Assert.AreEqual(full.Ms2Spectra.Count, streamedTotal);

                // One caller-kept buffer across every window, twice: after the first pass it is
                // the largest block's size, so the second pass reads smaller blocks into a buffer
                // whose tail still holds another window's bytes.
                byte[] readBuffer = null;
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var kvp in expected)
                        AssertSpectraListEqual(kvp.Value, index.LoadWindowSerialRead(kvp.Key, ref readBuffer));
                }

                // Absent key -> empty list (matches the dictionary miss).
                int absentKey = 1;
                while (expected.ContainsKey(absentKey))
                    absentKey++;
                Assert.AreEqual(0, index.LoadWindow(absentKey).Count);
                Assert.AreEqual(0, index.LoadWindowSerialRead(absentKey).Count);

                // AllMs2Rts mirrors the file-order RTs (dedup's sole dependency).
                Assert.AreEqual(full.Ms2Spectra.Count, index.AllMs2Rts.Count);
                for (int i = 0; i < full.Ms2Spectra.Count; i++)
                    Assert.AreEqual(full.Ms2Spectra[i].RetentionTime, index.AllMs2Rts[i], 0.0);

                // MS1 loaded via the EOF index (its recorded section offset) is byte-identical
                // to the full load's MS1 -- streaming Stages 1-4 get MS1 without the MS2 list.
                Assert.AreEqual(full.Ms1Spectra.Count, index.Ms1Spectra.Count);
                for (int i = 0; i < full.Ms1Spectra.Count; i++)
                {
                    Assert.AreEqual(full.Ms1Spectra[i].ScanNumber, index.Ms1Spectra[i].ScanNumber);
                    Assert.AreEqual(full.Ms1Spectra[i].RetentionTime, index.Ms1Spectra[i].RetentionTime, 0.0);
                    CollectionAssert.AreEqual(full.Ms1Spectra[i].Mzs, index.Ms1Spectra[i].Mzs);
                    CollectionAssert.AreEqual(full.Ms1Spectra[i].Intensities, index.Ms1Spectra[i].Intensities);
                }

                // Isolation windows: every distinct rounded-center key, each carrying that
                // key's first record's window, sorted by center - here the three windows of
                // cycle 1 (records 1-3), since the later records only repeat their keys.
                Assert.AreEqual(3, index.IsolationWindows.Count);
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreEqual(ms2[i].IsolationWindow.Center, index.IsolationWindows[i].Center, 0.0);
                    Assert.AreEqual(ms2[i].IsolationWindow.LowerOffset, index.IsolationWindows[i].LowerOffset, 0.0);
                    Assert.AreEqual(ms2[i].IsolationWindow.UpperOffset, index.IsolationWindows[i].UpperOffset, 0.0);
                }
            }
            finally
            {
                TryDeleteFile(path);
            }

            // Windows acquired out of center order are stored out of center order, as on real Astral
            // data: a window's block must end at the next block on disk, not at the next window by
            // center.
            AssertSerialReadsMatch(new List<Spectrum>
            {
                MakeIndexMs2(1, 10.0, 600.00, 3),
                MakeIndexMs2(2, 10.0, 500.00, 2),
                MakeIndexMs2(3, 10.0, 700.00, 4),
                MakeIndexMs2(4, 10.1, 600.00, 1),
                MakeIndexMs2(5, 10.1, 500.00, 5),
                MakeIndexMs2(6, 10.1, 700.00, 2),
            });

            // A cache with bad magic -> null (same rule as LoadSpectraCache), so a
            // caller can fall back to a resident load rather than stream garbage.
            string badPath = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(badPath, Encoding.ASCII.GetBytes("NOTVALID"));
                Assert.IsNull(SpectraWindowIndex.BuildFromCache(badPath));
            }
            finally
            {
                TryDeleteFile(badPath);
            }

            // A missing file -> null.
            Assert.IsNull(SpectraWindowIndex.BuildFromCache(
                Path.Combine(Path.GetTempPath(),
                    "osprey_no_such_" + Guid.NewGuid().ToString("N") + ".spectra.bin")));

            // A cache written by the pre-grouping v3 format is rejected (the VERSION
            // bump invalidates old caches so they re-populate window-grouped on first
            // use) -- both readers return null on the version mismatch.
            string v3Path = Path.GetTempFileName();
            try
            {
                using (var fs = new FileStream(v3Path, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(fs))
                {
                    w.Write(Encoding.ASCII.GetBytes("OSPRSPC\0"));
                    w.Write((uint)3);   // pre-grouping version
                    w.Write((ulong)0);  // source size (no fingerprint)
                    w.Write((long)0);   // source mtime
                    w.Write((uint)0);   // n_ms2
                    w.Write((uint)0);   // n_ms1
                }
                Assert.IsNull(SpectraWindowIndex.BuildFromCache(v3Path));
                Assert.IsNull(SpectraCache.LoadSpectraCache(v3Path));
            }
            finally
            {
                TryDeleteFile(v3Path);
            }
        }

        /// <summary>
        /// A build WITHOUT the vendor reader must still run against a <c>.spectra.bin</c>
        /// that an opt-in build produced from a vendor raw file. That is the whole point of
        /// staging caches once on a vendor-capable machine: every other machine consumes
        /// them with no ProteoWizard and no mzML conversion.
        ///
        /// The guarantee is an ORDERING one - <c>EnsureSpectraCache</c> has to consult the
        /// cache before it dispatches on file extension - and nothing else pins it. An
        /// up-front extension check, added for a friendlier error message, would silently
        /// break this workflow while every other test stayed green.
        ///
        /// The assertion is sound in either build. Where the vendor reader is absent
        /// <see cref="SpectrumFileReader"/> throws for a non-mzML path, so a cache MISS
        /// fails loudly rather than passing for the wrong reason; where it is present the
        /// stand-in file is not a real raw, so a miss fails there too.
        /// </summary>
        [TestMethod]
        public void TestVendorCacheUsableWithoutVendorReader()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"OspreyVendorCache" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Stands in for a Thermo .raw. The extension is what dispatch keys off,
                // and the bytes are never read because the cache hit precedes the reader.
                string rawPath = Path.Combine(dir, @"vendor_file.raw");
                File.WriteAllBytes(rawPath, new byte[] { 1, 2, 3, 4 });

                var ms2 = new List<Spectrum>
                {
                    new Spectrum
                    {
                        ScanNumber = 1,
                        RetentionTime = 10.5,
                        PrecursorMz = 500.0,
                        IsolationWindow = new IsolationWindow(500.0, 1.5, 1.5),
                        Mzs = new[] { 100.0, 200.0 },
                        Intensities = new[] { 1000.0f, 2000.0f }
                    }
                };
                var ms1 = new List<MS1Spectrum>
                {
                    new MS1Spectrum
                    {
                        ScanNumber = 0,
                        RetentionTime = 10.0,
                        Mzs = new[] { 400.0 },
                        Intensities = new[] { 5000.0f }
                    }
                };

                // Saved WITH the source path, so the header carries the same fingerprint an
                // opt-in build would have written, and the read back exercises the real
                // staleness check rather than the no-fingerprint shortcut.
                string cachePath = SpectraCache.GetCachePath(rawPath);
                SpectraCache.SaveSpectraCache(cachePath, ms2, ms1, rawPath);

                var ctx = new PipelineContext(new OspreyConfig(), new OspreyTask[0], null, null, null);
                SpectraWindowIndex index = ScoringTaskShared.EnsureSpectraCache(
                    rawPath, false, out int unsortedCount, ctx);

                Assert.IsNotNull(index);
                Assert.AreEqual(ms2.Count, index.Ms2Count);
                // Deliberately NOT asserting unsortedCount here. EnsureSpectraCache
                // zeroes it up front and only assigns on the cache-MISS branch, which
                // this test asserts is not taken - so the assertion could never fail and
                // would read as coverage it does not provide.
                List<Spectrum> streamed = index.LoadWindow(SpectraCache.WindowKey(500.0));
                Assert.AreEqual(1, streamed.Count);
                Assert.AreEqual(10.5, streamed[0].RetentionTime, 0.0);
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        // Distinct, non-round peak values per record so any field/peak mis-decode
        // surfaces; isolation offsets vary per scan so those decode paths are checked.
        private static Spectrum MakeIndexMs2(uint scan, double rt, double center, int nPeaks)
        {
            var mzs = new double[nPeaks];
            var intensities = new float[nPeaks];
            for (int p = 0; p < nPeaks; p++)
            {
                mzs[p] = 100.0 + scan * 7.0 + p * 1.5;
                intensities[p] = scan * 100.0f + p * 3.0f;
            }
            return new Spectrum
            {
                ScanNumber = scan,
                RetentionTime = rt,
                PrecursorMz = center,
                IsolationWindow = new IsolationWindow(center, 0.5 + scan * 0.01, 1.5 + scan * 0.02),
                Mzs = mzs,
                Intensities = intensities
            };
        }

        // Group MS2 by the same key scoring uses: (int)Math.Round(center * 10.0),
        // preserving file order within each window.
        private static void AssertSerialReadsMatch(List<Spectrum> ms2)
        {
            string path = Path.GetTempFileName();
            try
            {
                SpectraCache.SaveSpectraCache(path, ms2, new List<MS1Spectrum>());
                SpectraWindowIndex index = SpectraWindowIndex.BuildFromCache(path);
                Assert.IsNotNull(index);
                foreach (var kvp in GroupByWindowKey(ms2))
                {
                    AssertSpectraListEqual(kvp.Value, index.LoadWindow(kvp.Key));
                    AssertSpectraListEqual(kvp.Value, index.LoadWindowSerialRead(kvp.Key));
                }
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        private static Dictionary<int, List<Spectrum>> GroupByWindowKey(List<Spectrum> ms2)
        {
            var byKey = new Dictionary<int, List<Spectrum>>();
            foreach (var s in ms2)
            {
                int key = (int)Math.Round(s.IsolationWindow.Center * 10.0);
                if (!byKey.TryGetValue(key, out var list))
                {
                    list = new List<Spectrum>();
                    byKey[key] = list;
                }
                list.Add(s);
            }
            return byKey;
        }

        private static void AssertSpectraListEqual(List<Spectrum> expected, List<Spectrum> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Spectrum e = expected[i];
                Spectrum a = actual[i];
                Assert.AreEqual(e.ScanNumber, a.ScanNumber);
                Assert.AreEqual(e.RetentionTime, a.RetentionTime, 0.0);
                Assert.AreEqual(e.PrecursorMz, a.PrecursorMz, 0.0);
                Assert.AreEqual(e.IsolationWindow.Center, a.IsolationWindow.Center, 0.0);
                Assert.AreEqual(e.IsolationWindow.LowerOffset, a.IsolationWindow.LowerOffset, 0.0);
                Assert.AreEqual(e.IsolationWindow.UpperOffset, a.IsolationWindow.UpperOffset, 0.0);
                CollectionAssert.AreEqual(e.Mzs, a.Mzs);
                CollectionAssert.AreEqual(e.Intensities, a.Intensities);
            }
        }

        #endregion

        #region DiannTsvLoader Tests

        [TestMethod]
        public void TestParseModMass()
        {
            double? mass = DiannTsvLoader.ParseModMass("+57.0215");
            Assert.IsNotNull(mass);
            Assert.AreEqual(57.0215, mass.Value, 1e-4);

            mass = DiannTsvLoader.ParseModMass("-17.026");
            Assert.IsNotNull(mass);
            Assert.AreEqual(-17.026, mass.Value, 1e-3);

            mass = DiannTsvLoader.ParseModMass("Oxidation");
            Assert.IsNotNull(mass);
            Assert.AreEqual(15.9949, mass.Value, 1e-4);
        }

        [TestMethod]
        public void TestParseModifications()
        {
            var mods = DiannTsvLoader.ParseModifications("PEPTM[+15.9949]IDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(4, mods[0].Position);
            Assert.AreEqual(15.9949, mods[0].MassDelta, 1e-4);
        }

        [TestMethod]
        public void TestParseModificationsParenthetical()
        {
            var mods = DiannTsvLoader.ParseModifications("M(UniMod:35)PEPTIDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(0, mods[0].Position);
            Assert.IsNotNull(mods[0].UnimodId);
            Assert.AreEqual(35, mods[0].UnimodId.Value);
            Assert.AreEqual(15.994915, mods[0].MassDelta, 1e-4);
        }

        [TestMethod]
        public void TestStripModifications()
        {
            Assert.AreEqual("PEPTIDE", DiannTsvLoader.StripModifications("PEPTIDE"));
            Assert.AreEqual("PEPTIDE", DiannTsvLoader.StripModifications("PEP[+15.99]TIDE"));
            Assert.AreEqual("PEPTIDE", DiannTsvLoader.StripModifications("(UniMod:1)PEPTIDE"));
            Assert.AreEqual("PEPTMIDE", DiannTsvLoader.StripModifications("PEPTM[Oxidation]IDE"));
        }

        [TestMethod]
        public void TestSplitList()
        {
            CollectionAssert.AreEqual(
                new List<string> { "A", "B", "C" },
                DiannTsvLoader.SplitList("A;B;C"));

            CollectionAssert.AreEqual(
                new List<string> { "A", "B", "C" },
                DiannTsvLoader.SplitList("A,B,C"));

            CollectionAssert.AreEqual(
                new List<string> { "A", "B", "C" },
                DiannTsvLoader.SplitList("A; B; C"));
        }

        [TestMethod]
        public void TestIdentifyModification()
        {
            var modification = BlibLoader.IdentifyModification("+57.021", 'C', false, false);
            Assert.AreEqual(57.021464, modification.MassDelta);
            Assert.AreEqual(4, modification.UnimodId);
            Assert.AreEqual("Carbamidomethyl", modification.Name);
        }

        #endregion

        #region LibraryDeduplicator Tests

        [TestMethod]
        public void TestDeduplicateNoDuplicates()
        {
            var entries = new List<LibraryEntry>
            {
                new LibraryEntry(0, "AAA", "AAA", 2, 300.0, 10.0),
                new LibraryEntry(1, "BBB", "BBB", 2, 400.0, 20.0)
            };

            var result = LibraryDeduplicator.DeduplicateLibrary(entries);
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public void TestDeduplicateRemovesDuplicates()
        {
            var e1 = new LibraryEntry(0, "AAA", "AAA", 2, 300.0, 10.0);
            e1.Fragments = new List<LibraryFragment>
            {
                MakeFragment(200.0, 100.0f),
                MakeFragment(300.0, 80.0f)
            };
            e1.ProteinIds = new List<string> { "P1" };

            var e2 = new LibraryEntry(1, "AAA", "AAA", 2, 300.0, 12.0);
            e2.Fragments = new List<LibraryFragment>
            {
                MakeFragment(200.0, 90.0f),
                MakeFragment(300.0, 70.0f),
                MakeFragment(400.0, 50.0f)
            };
            e2.ProteinIds = new List<string> { "P2" };

            var e3 = new LibraryEntry(2, "BBB", "BBB", 3, 500.0, 15.0);
            e3.Fragments = new List<LibraryFragment> { MakeFragment(250.0, 100.0f) };

            var result = LibraryDeduplicator.DeduplicateLibrary(
                new List<LibraryEntry> { e1, e2, e3 });

            Assert.AreEqual(2, result.Count);

            // Find the AAA entry - should have 3 fragments (e2 was best)
            LibraryEntry aaa = result.Find(e => e.Sequence == "AAA");
            Assert.IsNotNull(aaa);
            Assert.AreEqual(3, aaa.Fragments.Count);

            // Average RT of 10.0 and 12.0
            Assert.AreEqual(11.0, aaa.RetentionTime, 0.01);

            // Merged proteins
            Assert.AreEqual(2, aaa.ProteinIds.Count);
        }

        [TestMethod]
        public void TestDeduplicateSequentialIds()
        {
            var entries = new List<LibraryEntry>
            {
                new LibraryEntry(100, "AAA", "AAA", 2, 300.0, 10.0),
                new LibraryEntry(200, "AAA", "AAA", 2, 300.0, 12.0),
                new LibraryEntry(300, "BBB", "BBB", 2, 400.0, 15.0)
            };

            var result = LibraryDeduplicator.DeduplicateLibrary(entries);
            Assert.AreEqual(2, result.Count);

            // IDs should be 0 and 1 (sequential)
            var ids = new List<uint>();
            foreach (var e in result)
                ids.Add(e.Id);
            ids.Sort();
            CollectionAssert.AreEqual(new List<uint> { 0, 1 }, ids);
        }

        [TestMethod]
        public void TestDeduplicateDifferentCharges()
        {
            var entries = new List<LibraryEntry>
            {
                new LibraryEntry(0, "AAA", "AAA", 2, 300.0, 10.0),
                new LibraryEntry(1, "AAA", "AAA", 3, 200.0, 10.0)
            };

            var result = LibraryDeduplicator.DeduplicateLibrary(entries);
            Assert.AreEqual(2, result.Count);
        }

        #endregion

        #region Library string interning

        [TestMethod]
        public void TestLibraryStringInterning()
        {
            // The interner is an instance pool the loaders route every string
            // through as they build each entry's arrays. Build two entries that
            // repeat the same values across every interned field, sharing one
            // pool, and a third with a null value / empty arrays.
            var interner = new LibraryStringInterner();
            var e0 = MakeInternEntry(interner, 1, "PEPTIDER", "M[16]PEPTIDER", "P12345", "GENEA", "Oxidation");
            var e1 = MakeInternEntry(interner, 2, "PEPTIDER", "M[16]PEPTIDER", "P12345", "GENEA", "Oxidation");

            // A third entry with a null protein element, empty gene array, and a
            // null modification name -- interning must leave these unchanged, not throw.
            var e2 = new LibraryEntry(3,
                interner.Intern(FreshCopy("PEPTIDEK")), interner.Intern(FreshCopy("PEPTIDEK")),
                2, 600.0, 8.0);
            e2.ProteinIds = new[] { interner.Intern(null) };
            e2.GeneNames = Array.Empty<string>();
            e2.Modifications = new[] { new Modification { Position = 0, Name = interner.Intern(null) } };

            // Values are unchanged on every interned field...
            Assert.AreEqual("PEPTIDER", e0.Sequence);
            Assert.AreEqual("M[16]PEPTIDER", e0.ModifiedSequence);
            Assert.AreEqual("P12345", e0.ProteinIds[0]);
            Assert.AreEqual("GENEA", e0.GeneNames[0]);
            Assert.AreEqual("Oxidation", e0.Modifications[0].Name);

            // ...but duplicates share one instance, for each interned field.
            // (The char-array copies in MakeInternEntry defeat the compiler's
            // literal interning, so AreSame proves the pool shared them -- not
            // the CLR string pool.)
            Assert.AreSame(e0.Sequence, e1.Sequence);
            Assert.AreSame(e0.ModifiedSequence, e1.ModifiedSequence);
            Assert.AreSame(e0.ProteinIds[0], e1.ProteinIds[0]);
            Assert.AreSame(e0.GeneNames[0], e1.GeneNames[0]);
            Assert.AreSame(e0.Modifications[0].Name, e1.Modifications[0].Name);

            // The null/empty entry is untouched (no NRE).
            Assert.IsNull(e2.ProteinIds[0]);
            Assert.AreEqual(0, e2.GeneNames.Count);
            Assert.IsNull(e2.Modifications[0].Name);
            Assert.AreEqual("PEPTIDEK", e2.Sequence);
        }

        // Fresh instance with the same characters, so the compiler's literal
        // interning does not pre-share it before the interner runs.
        private static string FreshCopy(string s)
        {
            return new string(s.ToCharArray());
        }

        private static LibraryEntry MakeInternEntry(LibraryStringInterner interner, uint id,
            string seq, string modSeq, string protein, string gene, string modName)
        {
            var e = new LibraryEntry(id,
                interner.Intern(FreshCopy(seq)), interner.Intern(FreshCopy(modSeq)),
                2, 500.0, 10.0);
            e.ProteinIds = new[] { interner.Intern(FreshCopy(protein)) };
            e.GeneNames = new[] { interner.Intern(FreshCopy(gene)) };
            e.Modifications = new[]
            {
                new Modification { Position = 1, Name = interner.Intern(FreshCopy(modName)) }
            };
            return e;
        }

        #endregion

        #region Blob Compression Tests

        [TestMethod]
        public void TestBlobCompression()
        {
            // Create test data: 3 doubles for m/z, 3 floats for intensity
            double[] mzValues = { 100.0, 200.0, 300.0 };
            float[] intValues = { 1000.0f, 500.0f, 250.0f };

            byte[] mzBytes = new byte[mzValues.Length * 8];
            for (int i = 0; i < mzValues.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(mzValues[i]), 0, mzBytes, i * 8, 8);

            byte[] intBytes = new byte[intValues.Length * 4];
            for (int i = 0; i < intValues.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(intValues[i]), 0, intBytes, i * 4, 4);

            // Compress using zlib format (2 header bytes + deflate data)
            byte[] compressedMz = ZlibCompress(mzBytes);
            byte[] compressedInt = ZlibCompress(intBytes);

            // Decompress and verify through the shared reader's decoders
            byte[] decompressedMz = BlibWriter.DecodeBlob(compressedMz, mzBytes.Length);
            Assert.IsNotNull(decompressedMz);
            Assert.AreEqual(mzBytes.Length, decompressedMz.Length);

            // Verify round-trip of m/z values
            for (int i = 0; i < mzValues.Length; i++)
            {
                double mz = BitConverter.ToDouble(decompressedMz, i * 8);
                Assert.AreEqual(mzValues[i], mz, 1e-10);
            }

            byte[] decompressedInt = BlibWriter.DecodeBlob(compressedInt, intBytes.Length);
            Assert.IsNotNull(decompressedInt);
            Assert.AreEqual(intBytes.Length, decompressedInt.Length);

            // Decoded together, scaled to the base peak, as the loader makes fragments of them.
            var peaks = BlibReader.DecodePeaks(compressedMz, compressedInt, mzValues.Length);
            Assert.IsNotNull(peaks);
            CollectionAssert.AreEqual(mzValues, peaks.Mzs);
            CollectionAssert.AreEqual(intValues, peaks.Intensities);
            var fragments = BlibLoader.ToFragments(peaks);
            CollectionAssert.AreEqual(new[] { 1f, 0.5f, 0.25f }, fragments.Select(f => f.RelativeIntensity).ToArray());

            // Eight bytes per peak is a float64 intensity blob, which an early writer stored.
            byte[] doubleIntBytes = new byte[intValues.Length * 8];
            for (int i = 0; i < intValues.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes((double)intValues[i]), 0, doubleIntBytes, i * 8, 8);
            CollectionAssert.AreEqual(intValues, BlibWriter.DecodeIntensities(doubleIntBytes, intValues.Length));
            // Compressed too, on enough values that the zlib bytes cannot be a raw length.
            float[] manyValues = Enumerable.Range(0, 64).Select(i => 1000f - i * 7.5f).ToArray();
            byte[] manyDoubleBytes = new byte[manyValues.Length * 8];
            for (int i = 0; i < manyValues.Length; i++)
                Buffer.BlockCopy(BitConverter.GetBytes((double)manyValues[i]), 0, manyDoubleBytes, i * 8, 8);
            byte[] compressedDoubleInt = ZlibCompress(manyDoubleBytes);
            Assert.AreNotEqual(manyValues.Length * 4, compressedDoubleInt.Length, "The fixture must not look like a raw blob.");
            Assert.AreNotEqual(manyValues.Length * 8, compressedDoubleInt.Length, "The fixture must not look like a raw blob.");
            CollectionAssert.AreEqual(manyValues, BlibWriter.DecodeIntensities(compressedDoubleInt, manyValues.Length));

            // A blob that inflates to another length than numPeaks says, or that is neither raw
            // nor zlib, is refused as library data, not misread.
            Assert.ThrowsException<InvalidDataException>(() => BlibWriter.DecodeBlob(compressedMz, mzBytes.Length + 8));
            Assert.ThrowsException<InvalidDataException>(() => BlibWriter.DecodeBlob(mzBytes, mzBytes.Length - 8));
            Assert.ThrowsException<InvalidDataException>(() => BlibWriter.DecodeIntensities(compressedMz, intValues.Length + 1));
        }

        #endregion

        #region BlibLoader Tests

        [TestMethod]
        public void TestBlibModifications()
        {
            // Simple unmodified
            Assert.AreEqual(0, ParseBlibModifications("PEPTIDE").Count);

            // Carbamidomethyl
            var mods = ParseBlibModifications("PEPTC[+57.021]IDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(4, mods[0].Position);
            Assert.AreEqual(57.021464, mods[0].MassDelta);
            Assert.AreEqual(4, mods[0].UnimodId);

            // Oxidation
            mods = ParseBlibModifications("PEPTM[+15.995]IDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(4, mods[0].Position);
            Assert.AreEqual(35, mods[0].UnimodId);

            // A mass matches a known modification as Skyline matches it: rounded at the precision
            // its text prints, so BiblioSpec's one decimal gets the exact mass.
            AssertBlibModification("PEPTC[+57.0]IDE", 57.021464, 4);
            AssertBlibModification("[+42.0]PEPTIDE", 42.010565, 1);
            AssertBlibModification("PEPS[+80.0]TIDE", 79.966331, 21);
            AssertBlibModification("PEPTIDEK[+8.0]", 8.014199, 259);
            AssertBlibModification("PEPTIDER[+10.0]", 10.008269, 267);
            AssertBlibModification("Q[-17.0]PEPTIDE", -17.026549, 28);
            AssertBlibModification("PEPTK[+114.0]IDE", 114.042927, 121);
            // Only on its residue or terminus: acetyl on lysine, trimethyl where two decimals
            // tell it from acetyl, and sulfo where three tell it from phospho.
            AssertBlibModification("PEPTK[+42.0]IDE", 42.010565, 1);
            AssertBlibModification("PEPTK[+42.05]IDE", 42.046950, 37);
            AssertBlibModification("PEPTY[+79.957]IDE", 79.956815, 40);
            // A mass printed with four or more decimals that matches nothing is used as printed.
            AssertBlibModification("[+42.03000]PEPTIDE", 42.03, null);
            AssertBlibModification("PEPTC[+125.047679]IDE", 125.047679, null);
            // A delta between 100 and 200 Da is not an absolute cysteine mass on any other
            // residue (GlyGly on lysine), nor on cysteine when it is signed (N-ethylmaleimide).
            AssertBlibModification("PEPTK[+114.042927]IDE", 114.042927, 121);
            // An unsigned value on cysteine is still its absolute mass.
            AssertBlibModification("PEPTC[160.030649]IDE", 57.021464, 4);
            AssertBlibModification("PEPTC[160.0]IDE", 57.021464, 4);
            // A UniMod id, in brackets or in parentheses as DIA-NN writes it.
            AssertBlibModification("PEPTC[UniMod:4]IDE", 57.021464, 4);
            AssertBlibModification("PEPTM(UniMod:35)IDE", 15.994915, 35);
            mods = ParseBlibModifications("C(UniMod:4)PEPTIDEM(UniMod:35)K");
            Assert.AreEqual(2, mods.Count);
            Assert.AreEqual(0, mods[0].Position);
            Assert.AreEqual(8, mods[1].Position);
            // A bracket summing a modification of the first residue and the N-terminus is used
            // as printed at four decimals, as Osprey writes it.
            AssertBlibModification("M[+58.0055]PEPTIDEK", 58.0055, null);
            // A mass that rounds to zero is written as a readable zero.
            Assert.AreEqual("[+0.0]", BlibSpectrum.FormatMassDelta(-4e-6));
            Assert.AreEqual("[-17.0265]", BlibSpectrum.FormatMassDelta(-17.026549));

            // Not identified: a mass printed with fewer than four decimals that matches nothing,
            // a known mass off its residue, an unknown UniMod id, and other text.
            AssertUnidentified("PEPTC[+99.9]IDE", "[+99.9]");
            AssertUnidentified("PEPTIDEK[+80.0]", "[+80.0]");
            AssertUnidentified("M[+58.0]PEPTIDEK", "[+58.0]");
            AssertUnidentified("PEPTIDEK(UniMod:99999)", "(UniMod:99999)");
            AssertUnidentified("PEPTIDEK[Oxidation]", "[Oxidation]");
            AssertUnidentified("PEPTIDEK[+1e2]", "[+1e2]");
        }

        private static List<Modification> ParseBlibModifications(string modSeq)
        {
            var mods = BlibLoader.ParseBlibModifications(modSeq, out string unidentified);
            Assert.IsNull(unidentified, modSeq);
            return mods;
        }

        private static void AssertBlibModification(string modSeq, double massDelta, int? unimodId)
        {
            var mods = ParseBlibModifications(modSeq);
            Assert.AreEqual(1, mods.Count, modSeq);
            Assert.AreEqual(massDelta, mods[0].MassDelta, 1e-12, modSeq);
            Assert.AreEqual(unimodId, mods[0].UnimodId, modSeq);
        }

        private static void AssertUnidentified(string modSeq, string bracket)
        {
            Assert.IsNull(BlibLoader.ParseBlibModifications(modSeq, out string unidentified), modSeq);
            Assert.AreEqual(bracket, unidentified, modSeq);
        }

        #endregion

        #region DiannTsvLoader Integration Tests

        [TestMethod]
        public void TestDiannTsvLoaderParsesMinimalFile()
        {
            string tsv = "PrecursorMz\tPrecursorCharge\tModifiedPeptide\tProductMz\tLibraryIntensity\tFragmentType\tFragmentSeriesNumber\tFragmentCharge\tRT\n" +
                          "500.0\t2\tPEPTIDEK\t200.0\t1000\ty\t1\t1\t10.5\n" +
                          "500.0\t2\tPEPTIDEK\t300.0\t800\ty\t2\t1\t10.5\n" +
                          "500.0\t2\tPEPTIDEK\t400.0\t600\ty\t3\t1\t10.5\n";

            var loader = new DiannTsvLoader();
            List<LibraryEntry> entries;
            using (var reader = new StringReader(tsv))
            {
                entries = loader.ParseReader(reader);
            }

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("PEPTIDEK", entries[0].Sequence);
            Assert.AreEqual(3, entries[0].Fragments.Count);
            Assert.AreEqual(2, entries[0].Charge);
            Assert.AreEqual(500.0, entries[0].PrecursorMz, 1e-4);
            Assert.AreEqual(10.5, entries[0].RetentionTime, 1e-4);
        }

        [TestMethod]
        public void TestDiannTsvLoaderSkipsBelowMinFragments()
        {
            string tsv = "PrecursorMz\tPrecursorCharge\tModifiedPeptide\tProductMz\tLibraryIntensity\n" +
                          "500.0\t2\tPEPTIDEK\t200.0\t1000\n" +
                          "500.0\t2\tPEPTIDEK\t300.0\t800\n";

            var loader = new DiannTsvLoader(3); // min 3 fragments
            List<LibraryEntry> entries;
            using (var reader = new StringReader(tsv))
            {
                entries = loader.ParseReader(reader);
            }

            Assert.AreEqual(0, entries.Count); // should be skipped (only 2 fragments)
        }

        /// <summary>
        /// A library value the loader cannot read is never replaced with a guess: every invalid
        /// value on every line is reported, after the whole file is read, and the library is
        /// refused. Each error is located as Skyline's transition list import locates one - the
        /// 1-based line of the file (the header is line 1, a blank line still counts) and the
        /// 1-based column. Covers each column the reader validates, two bad values on one line,
        /// a modification with no known mass, and the cap on how many errors the message lists.
        /// </summary>
        [TestMethod]
        public void TestDiannTsvLoaderRefusesEveryInvalidLine()
        {
            string tsv = DIANN_HEADER +
                DiannRow() +                                             // line 2: valid
                "\n" +                                                   // line 3: blank
                DiannRow(fragmentCharge: @"1.0") +                        // line 4
                DiannRow(fragmentCharge: @"0") +                          // line 5
                DiannRow(fragmentType: @"q") +                            // line 6
                DiannRow(fragmentNumber: string.Empty) +                  // line 7
                DiannRow(loss: @"garbage") +                              // line 8
                DiannRow(decoy: @"maybe") +                               // line 9
                DiannRow(rt: @"abc") +                                    // line 10
                DiannRow(precursorMz: @"x", intensity: @"?") +            // line 11: two errors
                DiannRow(modifiedPeptide: @"_(UniMod:9999)PEPTIDER_") +   // line 12
                DiannRow(charge: @"0");                                   // line 13

            string message = LoadDiannExpectingRefusal(tsv);
            StringAssert.Contains(message, string.Format(
                OspreyIOResources.DiannTsvLoader_ToException__0__library_lines_have_errors__Fix_the_library_and_load_it_again_, 10));
            // (line, 1-based column in DIANN_HEADER, column name, value)
            foreach (var (line, column, name, value) in new[]
            {
                (4, 12, @"FragmentCharge", @"1.0"), (5, 12, @"FragmentCharge", @"0"), (6, 10, @"FragmentType", @"q"),
                (8, 13, @"FragmentLossType", @"garbage"), (9, 7, @"Decoy", @"maybe"),
                (10, 5, @"NormalizedRetentionTime", @"abc"), (11, 3, @"PrecursorMz", @"x"),
                (11, 9, @"RelativeIntensity", @"?"), (12, 1, @"ModifiedPeptide", @"UniMod:9999"),
                (13, 4, @"PrecursorCharge", @"0")
            })
            {
                StringAssert.Contains(message, string.Format(
                    OspreyIOResources.DiannTsvLoader_LineReader___line__0___column__1___Invalid__2____3__, line, column, name, value));
            }
            StringAssert.Contains(message, string.Format(
                OspreyIOResources.DiannTsvLoader_LineReader___line__0___column__1___Missing__2_, 7, 11, @"FragmentSeriesNumber"));
            // The header line and exactly the eleven errors: the valid line reports nothing.
            Assert.AreEqual(12, message.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length);

            // A column wrong throughout lists the first 100 errors and counts the rest.
            var many = new StringBuilder(DIANN_HEADER);
            for (int i = 0; i < 150; i++)
                many.Append(DiannRow(fragmentCharge: @"x"));
            string manyMessage = LoadDiannExpectingRefusal(many.ToString());
            StringAssert.Contains(manyMessage, string.Format(
                OspreyIOResources.DiannTsvLoader_ToException__0__library_lines_have_errors__Fix_the_library_and_load_it_again_, 150));
            StringAssert.Contains(manyMessage, string.Format(OspreyIOResources.DiannTsvLoader_ToException____and__0__more_errors, 50));
        }

        private const string DIANN_HEADER =
            "ModifiedPeptide\tStrippedPeptide\tPrecursorMz\tPrecursorCharge\tTr_recalibrated\tProteinID\tDecoy\t" +
            "FragmentMz\tRelativeIntensity\tFragmentType\tFragmentNumber\tFragmentCharge\tFragmentLossType\n";

        /// <summary>One DIA-NN library line, valid unless a value is overridden.</summary>
        private static string DiannRow(string modifiedPeptide = @"_PEPTIDEK_", string precursorMz = @"500.0",
            string charge = @"2", string rt = @"10.5", string decoy = @"0", string intensity = @"1.0",
            string fragmentType = @"y", string fragmentNumber = @"1", string fragmentCharge = @"1", string loss = @"noloss")
        {
            string stripped = DiannTsvLoader.StripModifications(DiannTsvLoader.StripFlankingChars(modifiedPeptide));
            return string.Join("\t", modifiedPeptide, stripped, precursorMz, charge, rt, @"sp|P00001|TEST_HUMAN", decoy,
                @"200.0", intensity, fragmentType, fragmentNumber, fragmentCharge, loss) + "\n";
        }

        /// <summary>The message of the <see cref="InvalidDataException"/> loading <paramref name="tsv"/> must throw.</summary>
        private static string LoadDiannExpectingRefusal(string tsv)
        {
            using (var reader = new StringReader(tsv))
            {
                var ex = Assert.ThrowsException<InvalidDataException>(() => new DiannTsvLoader(1).ParseReader(reader));
                return ex.Message;
            }
        }

        #endregion

        #region Retention time precision

        /// <summary>
        /// A scan start time must survive the mzML round trip bit-exactly.
        /// 0.86653405 is a real Thermo value (spectrum index 5679 of a TDP-43
        /// PlasmaEV acquisition) that came out of Osprey's hand-written mzML
        /// reader as 0.8665340500000001 on the net472 build while the net8.0
        /// build of the same code returned it exactly - a 1-ULP difference that
        /// made a raw-sourced .spectra.bin disagree with an mzML-sourced one
        /// (issue #4496). That reader is gone (issue #4497); the case survives
        /// because ProteoWizard now has to clear the same bar, and because
        /// <c>SpectrumFileReader.GetStartTime</c> deliberately does NOT route a
        /// minute-valued scan start time through TimeInSeconds()/60, which is a
        /// multiply-then-divide that perturbs most values by an ULP (PR #4501).
        ///
        /// The reference values below are COMPILED LITERALS on purpose: Roslyn
        /// rounds them correctly at compile time, so they are a parser-independent
        /// oracle. An earlier version of this test compared the reader against
        /// XmlConvert.ToDouble of the same text and passed while the defect was
        /// live, because both sides were wrong in the same way - a self-referential
        /// assertion proves nothing.
        /// </summary>
        [TestMethod]
        public void TestSpectrumFileReaderRetentionTimePrecision()
        {
            // Text as it appears in the mzML, paired with the correctly rounded
            // double. The text must be exactly what a literal formats to, so the
            // file really does contain the decimal under test.
            var cases = new[]
            {
                new { Text = @"0.86653405", Expected = 0.86653405 },
                new { Text = @"0.97263695", Expected = 0.97263695 },
                new { Text = @"0.5903117", Expected = 0.5903117 },
            };
            foreach (var testCase in cases)
            {
                string text = testCase.Text;
                double expected = testCase.Expected;

                string mzml = BuildMinimalMzml(
                    msLevel: 2,
                    retentionTimeMinutes: expected,
                    precursorMz: 711.07312,
                    isoTarget: 711.07312,
                    isoLower: 1.5006999969482422,
                    isoUpper: 1.5006999969482422,
                    mzValues: new[] { 200.0, 300.0, 400.0 },
                    intensityValues: new[] { 100.0f, 200.0f, 300.0f });

                // The file must actually carry the decimal under test; otherwise a
                // formatting change could quietly swap in a different value and the
                // assertion below would still pass.
                StringAssert.Contains(mzml, @"value=""" + text + @"""");

                string path = Path.GetTempFileName() + ".mzML";
                try
                {
                    File.WriteAllText(path, mzml);
                    var result = SpectrumFileReader.LoadAllSpectra(path);
                    Assert.AreEqual(1, result.Ms2Spectra.Count);
                    Assert.AreEqual(expected, result.Ms2Spectra[0].RetentionTime, 0.0,
                        @"retention time not preserved bit-exactly for " + text);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        #endregion

        #region SpectrumFileReader Tests

        /// <summary>
        /// The by-extension format predicates. ProteoWizard reads every format
        /// now, so these no longer pick a parser - but <see cref="SpectrumFileReader.IsVendorFormat"/>
        /// still decides whether vendor centroiding is REQUESTED, and asking for
        /// it on a non-vendor input lands in a VendorOnlyPeakDetector that throws.
        /// Consolidated: the gzip and case variants are exactly where a wrong
        /// answer would happen quietly.
        /// </summary>
        [TestMethod]
        public void TestSpectrumFileReaderFormatRouting()
        {
            foreach (var mzml in new[]
                     {
                         @"a.mzML", @"a.mzml", @"a.MZML",
                         @"C:\data\some.file.name.mzML",
                         @"a.mzML.gz", @"a.mzml.gz",
                     })
            {
                Assert.IsTrue(SpectrumFileReader.IsMzml(mzml), mzml);
            }

            foreach (var vendor in new[]
                     {
                         @"a.raw", @"a.RAW", @"a.d", @"a.wiff", @"a.wiff2",
                         @"C:\data\some.file.name.raw",
                         // Not mzML despite the substring: routing must key on the
                         // extension, not a name that happens to contain "mzml".
                         @"mzml_backup.raw", @"a.mzXML",
                     })
            {
                Assert.IsFalse(SpectrumFileReader.IsMzml(vendor), vendor);
            }
        }

        #endregion

        #region mzML reading

        /// <summary>
        /// Verifies that isolation window CVs are parsed correctly from a minimal mzML
        /// snippet. Reads through <see cref="SpectrumFileReader"/>, i.e. ProteoWizard:
        /// the assertions are unchanged from when Osprey parsed this file itself, so
        /// they pin that the reader swap of issue #4497 preserved the offsets-not-widths
        /// reading of MS:1000828 / MS:1000829 and the precursor-m/z choice.
        /// </summary>
        [TestMethod]
        public void TestSpectrumFileReaderIsolationWindowParsing()
        {
            string mzml = BuildMinimalMzml(
                msLevel: 2,
                retentionTimeMinutes: 5.5,
                precursorMz: 500.25,
                isoTarget: 500.25,
                isoLower: 12.5,
                isoUpper: 12.5,
                mzValues: new[] { 200.0, 300.0, 400.0 },
                intensityValues: new[] { 100.0f, 200.0f, 300.0f });

            string path = Path.GetTempFileName() + ".mzML";
            try
            {
                File.WriteAllText(path, mzml);
                var result = SpectrumFileReader.LoadAllSpectra(path);

                Assert.AreEqual(1, result.Ms2Spectra.Count);
                Assert.AreEqual(0, result.Ms1Spectra.Count);

                var spectrum = result.Ms2Spectra[0];
                Assert.AreEqual(500.25, spectrum.PrecursorMz, 0.001);
                Assert.AreEqual(500.25, spectrum.IsolationWindow.Center, 0.001);
                Assert.AreEqual(12.5, spectrum.IsolationWindow.LowerOffset, 0.001);
                Assert.AreEqual(12.5, spectrum.IsolationWindow.UpperOffset, 0.001);
                Assert.AreEqual(5.5, spectrum.RetentionTime, 0.001);
                Assert.AreEqual(3, spectrum.Mzs.Length);
                Assert.AreEqual(3, spectrum.Intensities.Length);
                Assert.AreEqual(200.0, spectrum.Mzs[0], 0.001);
                Assert.AreEqual(300.0, spectrum.Mzs[1], 0.001);
                Assert.AreEqual(100.0f, spectrum.Intensities[0], 0.01f);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that MS1 and MS2 spectra are separated correctly.
        /// </summary>
        [TestMethod]
        public void TestSpectrumFileReaderMs1Ms2Separation()
        {
            // Build mzML with one MS1 and one MS2
            string ms1Block = BuildSpectrumElement(
                index: 0,
                msLevel: 1,
                retentionTimeMinutes: 1.0,
                precursorMz: 0,
                isoTarget: 0,
                isoLower: 0,
                isoUpper: 0,
                mzValues: new[] { 100.0, 200.0 },
                intensityValues: new[] { 50.0f, 60.0f },
                hasPrecursor: false);

            string ms2Block = BuildSpectrumElement(
                index: 1,
                msLevel: 2,
                retentionTimeMinutes: 1.1,
                precursorMz: 500.0,
                isoTarget: 500.0,
                isoLower: 10.0,
                isoUpper: 10.0,
                mzValues: new[] { 300.0 },
                intensityValues: new[] { 150.0f },
                hasPrecursor: true);

            string mzml = WrapInMzml(ms1Block + "\n" + ms2Block);

            string path = Path.GetTempFileName() + ".mzML";
            try
            {
                File.WriteAllText(path, mzml);
                var result = SpectrumFileReader.LoadAllSpectra(path);

                Assert.AreEqual(1, result.Ms1Spectra.Count);
                Assert.AreEqual(1, result.Ms2Spectra.Count);

                Assert.AreEqual(1.0, result.Ms1Spectra[0].RetentionTime, 0.001);
                Assert.AreEqual(2, result.Ms1Spectra[0].Mzs.Length);
                Assert.AreEqual(500.0, result.Ms2Spectra[0].PrecursorMz, 0.001);
                Assert.AreEqual(10.0, result.Ms2Spectra[0].IsolationWindow.LowerOffset, 0.001);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        /// <summary>
        /// Verifies that a scan start time recorded in seconds is converted to minutes.
        ///
        /// The term is MS:1000016 (scan start time) with UO:0000010 seconds, which is what
        /// a seconds-valued acquisition actually writes. The fixture used to say MS:1000894
        /// (retention time) instead - a different term that Osprey's hand-written parser
        /// also accepted. ProteoWizard does not read it for a scan start time and neither
        /// does Skyline's MsDataFileImpl, so keeping it would have pinned a leniency of the
        /// parser deleted in issue #4497 rather than the conversion under test.
        /// </summary>
        [TestMethod]
        public void TestSpectrumFileReaderRetentionTimeSeconds()
        {
            // Build mzML with the scan start time in seconds (330 s = 5.5 min).
            string spectrumBlock = @"
      <spectrum index=""0"" defaultArrayLength=""1"" id=""scan=1"">
        <cvParam cvRef=""MS"" accession=""MS:1000511"" value=""2"" />
        <scanList count=""1"">
          <scan>
            <cvParam cvRef=""MS"" accession=""MS:1000016"" value=""330.0"" unitCvRef=""UO"" unitAccession=""UO:0000010"" unitName=""second"" />
          </scan>
        </scanList>
        <precursorList count=""1"">
          <precursor>
            <isolationWindow>
              <cvParam cvRef=""MS"" accession=""MS:1000827"" value=""500.0"" />
              <cvParam cvRef=""MS"" accession=""MS:1000828"" value=""12.5"" />
              <cvParam cvRef=""MS"" accession=""MS:1000829"" value=""12.5"" />
            </isolationWindow>
            <selectedIonList count=""1"">
              <selectedIon>
                <cvParam cvRef=""MS"" accession=""MS:1000744"" value=""500.0"" />
              </selectedIon>
            </selectedIonList>
          </precursor>
        </precursorList>
        <binaryDataArrayList count=""2"">
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000514"" />
            <cvParam cvRef=""MS"" accession=""MS:1000523"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>" + EncodeDoublesBase64(new[] { 300.0 }) + @"</binary>
          </binaryDataArray>
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000515"" />
            <cvParam cvRef=""MS"" accession=""MS:1000521"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>" + EncodeFloatsBase64(new[] { 100.0f }) + @"</binary>
          </binaryDataArray>
        </binaryDataArrayList>
      </spectrum>";

            string mzml = WrapInMzml(spectrumBlock);
            string path = Path.GetTempFileName() + ".mzML";
            try
            {
                File.WriteAllText(path, mzml);
                var result = SpectrumFileReader.LoadAllSpectra(path);

                Assert.AreEqual(1, result.Ms2Spectra.Count);
                // 330 seconds = 5.5 minutes
                Assert.AreEqual(5.5, result.Ms2Spectra[0].RetentionTime, 0.001);
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        #endregion

        #region Raw vs mzML parity

        /// <summary>
        /// The same acquisition read from a vendor .raw and from the mzML msconvert wrote
        /// from it must produce the same Osprey spectra. This is the axis the reader swap
        /// of issue #4497 is most exposed on, and the only one a committed test can cover:
        /// the reader-vs-reader and raw-vs-raw comparisons both need multi-GB off-repo
        /// acquisitions, but the ProteoWizard Thermo test fixture is already tracked here.
        ///
        /// Designed during issue #4496 (see ai/todos/completed/TODO-20260729_osprey_vendor_raw_reader.md,
        /// "Tier 2") and never landed - the large-file parity was run by hand instead, so
        /// nothing has guarded the field mapping since. It is cheap: three spectra, no
        /// msconvert run, no staged data.
        ///
        /// Known limit, inherited from the fixture: its binary arrays are 32-bit for BOTH
        /// m/z and intensity, so this pins the field mapping, the isolation-window
        /// offset semantics and the MS-level split, but cannot catch an f64 m/z precision
        /// defect. That is what the large-file comparisons cover.
        ///
        /// Without the vendor SDK the .raw cannot be read at all, so the test asserts the
        /// OTHER half of the contract there: that the failure is a clear, actionable error
        /// rather than an empty result. Both branches are real assertions - this never
        /// silently passes for lack of a vendor build.
        /// </summary>
        [TestMethod]
        public void TestRawVsMzmlSpectraParity()
        {
            string dataDir = Path.Combine(FindPwizRoot(),
                @"pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data");
            string rawPath = Path.Combine(dataDir, @"source_cid_test_3scans.raw");
            // The CENTROID conversion: Osprey asks ProteoWizard for vendor centroiding on a
            // .raw, so the mzML that must match is the one msconvert wrote with the same
            // request, not the profile conversion beside it.
            string mzmlPath = Path.Combine(dataDir, @"source_cid_test_3scans-centroid.mzML");
            Assert.IsTrue(File.Exists(rawPath), rawPath);
            Assert.IsTrue(File.Exists(mzmlPath), mzmlPath);

            var fromMzml = SpectrumFileReader.LoadAllSpectra(mzmlPath);
            Assert.IsTrue(fromMzml.Ms1Spectra.Count + fromMzml.Ms2Spectra.Count > 0,
                @"the mzML fixture produced no spectra");

            SpectrumFileResult fromRaw;
            try
            {
                fromRaw = SpectrumFileReader.LoadAllSpectra(rawPath);
            }
            catch (UserMessageException ex) when (ex.InnerException is VendorSupportNotEnabledException)
            {
                // No vendor SDK in this build. Pin that the message at least NAMES the
                // file, so a user is not left with a bare format complaint. Asserting on the
                // file name rather than on wording keeps this translation-proof. The type
                // itself is pinned too: it is what makes the top level print this message
                // alone instead of a stack trace.
                //
                // Only the refusal for missing vendor support, NOT any Exception or any
                // UserMessageException (the peak-picking refusal is one too): SpectrumFileReader
                // interpolates the path into its message, so a blanket catch would let an
                // IOException from a file lock - or a genuine mapping defect that happened to
                // quote the path - satisfy this assertion and skip every parity check below.
                // The two branches have to be disjoint for the pass to mean anything.
                StringAssert.Contains(ex.Message, Path.GetFileName(rawPath));
                return;
            }

            Assert.AreEqual(fromMzml.Ms2Spectra.Count, fromRaw.Ms2Spectra.Count);
            Assert.AreEqual(fromMzml.Ms1Spectra.Count, fromRaw.Ms1Spectra.Count);
            for (int i = 0; i < fromMzml.Ms1Spectra.Count; i++)
            {
                AssertMs1SpectrumEqual(fromMzml.Ms1Spectra[i], fromRaw.Ms1Spectra[i]);
            }
            for (int i = 0; i < fromMzml.Ms2Spectra.Count; i++)
            {
                AssertMs2SpectrumEqual(fromMzml.Ms2Spectra[i], fromRaw.Ms2Spectra[i]);
            }
        }

        private static void AssertMs1SpectrumEqual(MS1Spectrum expected, MS1Spectrum actual)
        {
            Assert.AreEqual(expected.ScanNumber, actual.ScanNumber);
            Assert.AreEqual(expected.RetentionTime, actual.RetentionTime, 0.0);
            AssertPeaksEqual(expected.Mzs, expected.Intensities, actual.Mzs, actual.Intensities);
        }

        private static void AssertMs2SpectrumEqual(Spectrum expected, Spectrum actual)
        {
            Assert.AreEqual(expected.ScanNumber, actual.ScanNumber);
            Assert.AreEqual(expected.RetentionTime, actual.RetentionTime, 0.0);
            Assert.AreEqual(expected.PrecursorMz, actual.PrecursorMz, 0.0);
            // The isolation window is the field most likely to be silently wrong in a
            // binding: lower/upper are OFFSETS from the centre, not a width or bounds.
            Assert.AreEqual(expected.IsolationWindow.Center, actual.IsolationWindow.Center, 0.0);
            Assert.AreEqual(expected.IsolationWindow.LowerOffset, actual.IsolationWindow.LowerOffset, 0.0);
            Assert.AreEqual(expected.IsolationWindow.UpperOffset, actual.IsolationWindow.UpperOffset, 0.0);
            AssertPeaksEqual(expected.Mzs, expected.Intensities, actual.Mzs, actual.Intensities);
        }

        /// <summary>
        /// Exact comparison, no tolerance: both sides come from ProteoWizard reading the
        /// same acquisition, so any difference is a defect rather than accumulated error.
        /// </summary>
        private static void AssertPeaksEqual(double[] expectedMzs, float[] expectedIntensities,
            double[] actualMzs, float[] actualIntensities)
        {
            Assert.AreEqual(expectedMzs.Length, actualMzs.Length);
            Assert.AreEqual(expectedIntensities.Length, actualIntensities.Length);
            for (int i = 0; i < expectedMzs.Length; i++)
            {
                Assert.AreEqual(expectedMzs[i], actualMzs[i], 0.0);
                Assert.AreEqual(expectedIntensities[i], actualIntensities[i], 0.0f);
            }
        }

        /// <summary>
        /// Walk up from the test assembly to the pwiz checkout root (the directory holding
        /// both pwiz/ and pwiz_tools/), so the tracked vendor-reader fixtures can be found
        /// without hardcoding a machine path.
        /// </summary>
        internal static string FindPwizRoot()
        {
            string dir = Path.GetDirectoryName(typeof(IOTest).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, @"pwiz")) &&
                    Directory.Exists(Path.Combine(dir, @"pwiz_tools")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                @"Could not locate the pwiz root from the test assembly location.");
        }

        #endregion

        #region ParquetScoreCache Tests

        /// <summary>
        /// Write scored entries to Parquet, then read back FDR stubs and PIN features,
        /// verifying round-trip data integrity.
        /// </summary>
        [TestMethod]
        public void TestParquetScoreCacheRoundTrip()
        {
            // SimpleFileSaver owns the scratch file's lifecycle: write + read back through
            // its sibling temp and never Commit() -- Dispose discards it (no leaked
            // GetTempFileName file, cleaned up even if an assertion throws).
            string dest = Path.Combine(Path.GetTempPath(),
                @"osprey_roundtrip_" + Path.GetRandomFileName() + @".parquet");
            using (var saver = new SimpleFileSaver(dest))
            {
                // Create test entries
                var entries = new List<CoelutionScoredEntry>();
                for (int i = 0; i < 3; i++)
                {
                    var entry = new CoelutionScoredEntry
                    {
                        EntryId = (uint)(100 + i),
                        IsDecoy = (i == 1),
                        Sequence = "PEPTIDE",
                        ModifiedSequence = "PEPTIDE",
                        Charge = (byte)(2 + i % 2),
                        PrecursorMz = 500.0 + i,
                        ScanNumber = (uint)(1000 + i),
                        ApexRt = 5.0 + i * 0.5,
                        FileName = "test.mzML",
                        PeakBounds = new XICPeakBounds
                        {
                            StartRt = 4.5 + i * 0.5,
                            EndRt = 5.5 + i * 0.5,
                        },
                        Features = new CoelutionFeatureSet
                        {
                            CoelutionSum = 0.9 + i * 0.01,
                            CoelutionMax = 0.95,
                            NCoelutingFragments = 5,
                            PeakApex = 1000.0 + i,
                            PeakArea = 5000.0,
                            PeakSharpness = 0.8,
                            Xcorr = 2.5 + i * 0.1,
                            ConsecutiveIons = 3,
                            ExplainedIntensity = 0.75,
                            MassAccuracyMean = -0.5,
                            AbsMassAccuracyMean = 0.5,
                            RtDeviation = 0.1,
                            AbsRtDeviation = 0.1,
                            Ms1PrecursorCoelution = 0.85,
                            Ms1IsotopeCosine = 0.92,
                            MedianPolishCosine = 0.88,
                            MedianPolishResidualRatio = 0.15,
                            SgWeightedXcorr = 2.3,
                            SgWeightedCosine = 0.87,
                            MedianPolishMinFragmentR2 = 0.7,
                            MedianPolishResidualCorrelation = 0.3,
                        },
                    };
                    entries.Add(entry);
                }

                var metadata = new Dictionary<string, string>
                {
                    { "osprey.version", "1.0.0" },
                    { "osprey.search_hash", "abc123" },
                };

                // Write
                ParquetScoreCache.WriteScoresParquet(saver.SafeName, entries, metadata);
                Assert.IsTrue(File.Exists(saver.SafeName), "Parquet file should exist");

                // Read FDR stubs
                var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(saver.SafeName);
                Assert.AreEqual(3, stubs.Count);
                Assert.AreEqual(100u, stubs[0].EntryId);
                Assert.AreEqual(101u, stubs[1].EntryId);
                Assert.AreEqual(102u, stubs[2].EntryId);
                Assert.IsFalse(stubs[0].IsDecoy);
                Assert.IsTrue(stubs[1].IsDecoy);
                Assert.IsFalse(stubs[2].IsDecoy);
                Assert.AreEqual(2, stubs[0].Charge);
                Assert.AreEqual(5.0, stubs[0].ApexRt, 0.001);
                Assert.AreEqual(4.5, stubs[0].StartRt, 0.001);
                Assert.AreEqual(5.5, stubs[0].EndRt, 0.001);
                Assert.AreEqual(0.9, stubs[0].CoelutionSum, 0.001);
                Assert.AreEqual("PEPTIDE", stubs[0].ModifiedSequence);
                Assert.AreEqual(0u, stubs[0].ParquetIndex);
                Assert.AreEqual(1u, stubs[1].ParquetIndex);

                // A valid Osprey parquet must pass the lean-path feature-presence
                // guard (else the resume/HPC-merge fail-fast would reject every real
                // run). This also pins the writer's first feature column name to the
                // PIN_FEATURE_NAMES[0] the probe checks -- a rename desync between them
                // would silently break both.
                Assert.IsTrue(ParquetScoreCache.HasPinFeatureColumns(saver.SafeName));

                // Read PIN features
                var features = ParquetScoreCache.LoadPinFeaturesFromParquet(saver.SafeName);
                Assert.AreEqual(3, features.Count);
                Assert.AreEqual(ParquetScoreCache.NUM_PIN_FEATURES, features[0].Length);
                // Check first entry features
                Assert.AreEqual(0.9, features[0][0], 0.001); // coelution_sum
                Assert.AreEqual(0.95, features[0][1], 0.001); // coelution_max
                Assert.AreEqual(5.0, features[0][2], 0.001); // n_coeluting_fragments
                Assert.AreEqual(2.5, features[0][6], 0.001); // xcorr
                // Check second entry xcorr
                Assert.AreEqual(2.6, features[1][6], 0.001);

                // Validate metadata
                Assert.IsTrue(ParquetScoreCache.ValidateMetadata(saver.SafeName, metadata));
                var wrongMeta = new Dictionary<string, string>
                {
                    { "osprey.version", "2.0.0" },
                };
                Assert.IsFalse(ParquetScoreCache.ValidateMetadata(saver.SafeName, wrongMeta));
            }
        }

        /// <summary>
        /// A parquet that carries the scalar stub columns but NOT the PIN feature
        /// columns (a foreign or truncated scores file) must fail the lean-path
        /// feature-presence guard, so the resume / HPC-merge paths abort up front
        /// instead of streaming scalars from an untrustworthy file. Writes a minimal
        /// single-column parquet lacking the feature schema and asserts the footer
        /// probe reports it. Paired with the positive assertion in
        /// <see cref="TestParquetScoreCacheRoundTrip"/>.
        /// </summary>
        [TestMethod]
        public void TestHasPinFeatureColumnsRejectsFeaturelessParquet()
        {
            // SimpleFileSaver owns the scratch file's lifecycle: write the fixture to its
            // sibling temp, probe that, and never Commit() -- Dispose discards the temp
            // (no leaked temp file, cleaned up even if an assertion throws).
            string dest = Path.Combine(Path.GetTempPath(),
                @"osprey_haspin_" + Path.GetRandomFileName() + @".parquet");
            using (var saver = new SimpleFileSaver(dest))
            {
                var entryIdField = new DataField<uint>("entry_id");
                var schema = new ParquetSchema(entryIdField);
                using (var stream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                {
                    var writer = ParquetWriter.CreateAsync(schema, stream).GetAwaiter().GetResult();
                    using (var group = writer.CreateRowGroup())
                    {
                        group.WriteAsync(entryIdField, new ReadOnlyMemory<uint>(new[] { 1u, 2u, 3u }))
                            .GetAwaiter().GetResult();
                    }
                    // Writes the footer
                    writer.DisposeAsync().GetAwaiter().GetResult();
                }

                Assert.IsFalse(ParquetScoreCache.HasPinFeatureColumns(saver.SafeName),
                    "A parquet without the PIN feature columns must be rejected by the lean-path guard.");
            }
        }

        /// <summary>
        /// Verifies GetScoresPath returns the expected path.
        /// </summary>
        [TestMethod]
        public void TestGetScoresPath()
        {
            string result = ParquetScoreCache.GetScoresPath(@"C:\data\sample1.mzML");
            Assert.AreEqual(@"C:\data\sample1.scores.parquet", result);

            result = ParquetScoreCache.GetScoresPath(@"D:\runs\experiment.raw.mzML");
            Assert.AreEqual(@"D:\runs\experiment.raw.scores.parquet", result);
        }

        /// <summary>
        /// Verifies GetReconciledScoresPath returns the reconciled sibling path
        /// (Stage 6's separate output, not an overwrite of Stage 4's).
        /// </summary>
        [TestMethod]
        public void TestGetReconciledScoresPath()
        {
            Assert.AreEqual(@"C:\data\sample1.scores-reconciled.parquet",
                ParquetScoreCache.GetReconciledScoresPath(@"C:\data\sample1.mzML"));
            Assert.AreEqual(@"D:\runs\experiment.raw.scores-reconciled.parquet",
                ParquetScoreCache.GetReconciledScoresPath(@"D:\runs\experiment.raw.mzML"));
        }

        /// <summary>
        /// Verifies ReconciledPathFromScoresPath swaps the ".scores.parquet"
        /// suffix for ".scores-reconciled.parquet" and is idempotent on an
        /// already-reconciled path (the --task SecondPassFDR case where the input IS
        /// the reconciled file).
        /// </summary>
        [TestMethod]
        public void TestReconciledPathFromScoresPath()
        {
            Assert.AreEqual(@"C:\data\sample1.scores-reconciled.parquet",
                ParquetScoreCache.ReconciledPathFromScoresPath(@"C:\data\sample1.scores.parquet"));
            // Idempotent: an already-reconciled path is returned unchanged.
            Assert.AreEqual(@"C:\data\sample1.scores-reconciled.parquet",
                ParquetScoreCache.ReconciledPathFromScoresPath(@"C:\data\sample1.scores-reconciled.parquet"));
            // Composes with GetScoresPath to equal GetReconciledScoresPath.
            const string mzml = @"D:\runs\experiment.raw.mzML";
            Assert.AreEqual(ParquetScoreCache.GetReconciledScoresPath(mzml),
                ParquetScoreCache.ReconciledPathFromScoresPath(ParquetScoreCache.GetScoresPath(mzml)));
        }

        /// <summary>
        /// Regression for the suffix-ambiguity Copilot flagged on PR #4261: an
        /// input stem that itself ends in ".reconciled" must NOT be mistaken for
        /// a Stage 6 reconciled output. Because the marker sits AFTER ".scores"
        /// (".scores-reconciled.parquet"), the Stage 4 file is unambiguously an
        /// original and maps to a distinct reconciled sibling (no overwrite).
        /// </summary>
        [TestMethod]
        public void TestReconciledNamingUnambiguousForReconciledStem()
        {
            // Input "sample.reconciled.mzML" -> Stage 4 "sample.reconciled.scores.parquet".
            string stage4 = ParquetScoreCache.GetScoresPath(@"C:\data\sample.reconciled.mzML");
            Assert.AreEqual(@"C:\data\sample.reconciled.scores.parquet", stage4);
            // It must be classified as an original, not a reconciled output...
            Assert.IsFalse(ParquetScoreCache.IsReconciledScoresPath(stage4));
            // ...and map to a DISTINCT reconciled sibling (not back onto itself).
            string reconciled = ParquetScoreCache.ReconciledPathFromScoresPath(stage4);
            Assert.AreEqual(@"C:\data\sample.reconciled.scores-reconciled.parquet", reconciled);
            Assert.AreNotEqual(stage4, reconciled);
            Assert.IsTrue(ParquetScoreCache.IsReconciledScoresPath(reconciled));
        }

        /// <summary>
        /// Which parquet a task reads is decided by the TASK, and NOT by what is on disk.
        ///
        /// <para>The predecessor of this test pinned the opposite - a probe that took the
        /// reconciled sibling wherever it existed. That gives the right answer only because
        /// the pipeline runs the stages in order, and it silently gives the wrong one on a
        /// re-run: <c>--task FirstPassFDR</c> over a directory a previous run completed found
        /// the reconciled parquets and would have trained the FIRST pass on the survivor
        /// SUBSET, with every version, search and library hash matching. So the assertion
        /// that matters is the negative one - BOTH files present, and the task still decides.
        /// A test that laid down only one file would pass against the probe as well.</para>
        /// </summary>
        [TestMethod]
        public void TestScoresPathsDependOnTaskNotDisk()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_eff_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string input = Path.Combine(dir, "sample1.mzML");
                string original = Path.Combine(dir, "sample1.scores.parquet");
                string reconciled = Path.Combine(dir, "sample1.scores-reconciled.parquet");
                // BOTH on disk, which is the state a completed run leaves behind.
                File.WriteAllText(input, "x");
                File.WriteAllText(original, "x");
                File.WriteAllText(reconciled, "y");

                // The two passes that run BEFORE Stage 6 read the Stage 4 file even though
                // the reconciled sibling is sitting beside it.
                AssertScoresPathForTask(input, FirstPassFdrTask.TASK_NAME, original);
                AssertScoresPathForTask(input, PerFileRescoreTask.TASK_NAME, original);

                // The join reads the reconciled one - the only artifact its node is shipped.
                AssertScoresPathForTask(input, SecondPassFdrTask.TASK_NAME, reconciled);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// One input, one task, one expected parquet - through the same helper the pipeline
        /// calls, so the test cannot agree with a rule the tasks do not use.
        /// </summary>
        private static void AssertScoresPathForTask(string input, string taskName, string expected)
        {
            var config = TaskConfigs.ForTask(taskName);
            config.InputFiles = new List<string> { input };
            var paths = ScoringTaskShared.ScoresPathsForInputs(config);
            Assert.AreEqual(1, paths.Count);
            Assert.AreEqual(expected, paths[0]);
        }

        /// <summary>
        /// Verifies that writing an empty list does not create a file.
        /// </summary>
        [TestMethod]
        public void TestParquetScoreCacheEmptyWrite()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                ParquetScoreCache.WriteScoresParquet(path, new List<CoelutionScoredEntry>(), null);
                Assert.IsFalse(File.Exists(path), "Empty entry list should not create a file");

                // The 3-arg overload (CoelutionScoredEntry) takes List<>, the
                // FdrEntry overload takes 5 args, so a bare `null` is an
                // unambiguous match for the CoelutionScoredEntry overload.
                ParquetScoreCache.WriteScoresParquet(path, null, null);
                Assert.IsFalse(File.Exists(path), "Null entry list should not create a file");
            }
            finally
            {
                TryDeleteFile(path);
            }
        }

        #endregion

        #region MzML Test Helpers

        private static string BuildMinimalMzml(int msLevel, double retentionTimeMinutes,
            double precursorMz, double isoTarget, double isoLower, double isoUpper,
            double[] mzValues, float[] intensityValues)
        {
            string specBlock = BuildSpectrumElement(0, msLevel, retentionTimeMinutes,
                precursorMz, isoTarget, isoLower, isoUpper,
                mzValues, intensityValues, msLevel == 2);
            return WrapInMzml(specBlock);
        }

        /// <summary>
        /// Build one <c>spectrum</c> element. Units are spelled out in full -
        /// <c>unitCvRef</c> + <c>unitAccession</c> + <c>unitName</c> - because that is
        /// what a real mzML carries (ProteoWizard's own writer emits all three) and what
        /// a reader keys off. Osprey's hand-written parser read <c>unitName</c>, so a
        /// fixture carrying only that name survived until issue #4497; ProteoWizard reads
        /// <c>unitAccession</c>, and a scan start time with no unit accession has UNKNOWN
        /// units, which converts to 0 rather than failing. A fixture that is not valid
        /// mzML tests the reader against input the reader will never see.
        /// </summary>
        private static string BuildSpectrumElement(int index, int msLevel,
            double retentionTimeMinutes, double precursorMz,
            double isoTarget, double isoLower, double isoUpper,
            double[] mzValues, float[] intensityValues, bool hasPrecursor)
        {
            string mzBase64 = EncodeDoublesBase64(mzValues);
            string intBase64 = EncodeFloatsBase64(intensityValues);

            string precursorBlock = string.Empty;
            if (hasPrecursor)
            {
                precursorBlock = string.Format(
                    CultureInfo.InvariantCulture,
                    @"
        <precursorList count=""1"">
          <precursor>
            <isolationWindow>
              <cvParam cvRef=""MS"" accession=""MS:1000827"" value=""{0}"" />
              <cvParam cvRef=""MS"" accession=""MS:1000828"" value=""{1}"" />
              <cvParam cvRef=""MS"" accession=""MS:1000829"" value=""{2}"" />
            </isolationWindow>
            <selectedIonList count=""1"">
              <selectedIon>
                <cvParam cvRef=""MS"" accession=""MS:1000744"" value=""{3}"" />
              </selectedIon>
            </selectedIonList>
          </precursor>
        </precursorList>",
                    isoTarget, isoLower, isoUpper, precursorMz);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                @"
      <spectrum index=""{0}"" defaultArrayLength=""{1}"" id=""scan={0}"">
        <cvParam cvRef=""MS"" accession=""MS:1000511"" value=""{2}"" />
        <scanList count=""1"">
          <scan>
            <cvParam cvRef=""MS"" accession=""MS:1000016"" value=""{3}"" unitCvRef=""UO"" unitAccession=""UO:0000031"" unitName=""minute"" />
          </scan>
        </scanList>{4}
        <binaryDataArrayList count=""2"">
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000514"" />
            <cvParam cvRef=""MS"" accession=""MS:1000523"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>{5}</binary>
          </binaryDataArray>
          <binaryDataArray>
            <cvParam cvRef=""MS"" accession=""MS:1000515"" />
            <cvParam cvRef=""MS"" accession=""MS:1000521"" />
            <cvParam cvRef=""MS"" accession=""MS:1000576"" />
            <binary>{6}</binary>
          </binaryDataArray>
        </binaryDataArrayList>
      </spectrum>",
                index, mzValues.Length, msLevel, retentionTimeMinutes,
                precursorBlock, mzBase64, intBase64);
        }

        private static string WrapInMzml(string spectrumElements)
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<mzML xmlns=""http://psi.hupo.org/ms/mzml"">
  <run>
    <spectrumList count=""1"" defaultDataProcessingRef=""dp"">"
                + spectrumElements + @"
    </spectrumList>
  </run>
</mzML>";
        }

        private static string EncodeDoublesBase64(double[] values)
        {
            byte[] bytes = new byte[values.Length * 8];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        private static string EncodeFloatsBase64(float[] values)
        {
            byte[] bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        #endregion

        #region Test Helpers

        private static LibraryFragment MakeFragment(double mz, float intensity)
        {
            return new LibraryFragment
            {
                Mz = mz,
                RelativeIntensity = intensity,
                Annotation = new FragmentAnnotation
                {
                    IonType = IonType.Y,
                    Ordinal = 1,
                    Charge = 1
                }
            };
        }

        /// <summary>
        /// Compress data using zlib format (2-byte header + deflate + checksum).
        /// </summary>
        private static byte[] ZlibCompress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                // Write zlib header bytes (CM=8 Deflate, CINFO=7 32K window)
                output.WriteByte(0x78);
                output.WriteByte(0x9C);

                using (var deflate = new DeflateStream(output, CompressionMode.Compress, true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                // Adler-32 checksum (simplified)
                uint adler = ComputeAdler32(data);
                byte[] checksumBytes = BitConverter.GetBytes(adler);
                // Adler-32 is big-endian in zlib
                output.WriteByte(checksumBytes[3]);
                output.WriteByte(checksumBytes[2]);
                output.WriteByte(checksumBytes[1]);
                output.WriteByte(checksumBytes[0]);

                return output.ToArray();
            }
        }

        private static uint ComputeAdler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        private static void VerifyTableExists(SQLiteConnection conn, string tableName)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", conn))
            {
                cmd.Parameters.AddWithValue("@name", tableName);
                long count = (long)(cmd.ExecuteScalar() ?? 0);
                Assert.AreEqual(1L, count, "Table " + tableName + " should exist");
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception)
            {
                // Best effort: a temp directory left behind must never fail a test.
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                // Also clean up SQLite WAL/SHM files
                string walPath = path + "-wal";
                string shmPath = path + "-shm";
                if (File.Exists(walPath))
                    File.Delete(walPath);
                if (File.Exists(shmPath))
                    File.Delete(shmPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        #endregion

        #region FdrScoresSidecar Tests

        /// <summary>
        /// The feature mask is applied where the FDR stage loads features, not where scoring writes
        /// them: the scores parquet holds every feature, and only
        /// <see cref="ParquetScoreCache.LoadPinFeaturesFromParquet"/> reads a masked one as 0. That
        /// split is what lets a mask arm adopt another arm's per-file scores and run the FDR stage
        /// alone. The test process leaves OSPREY_FEATURE_MASK unset, so the default set is masked.
        /// </summary>
        [TestMethod]
        public void TestFeatureMaskAppliesAtFdrLoad()
        {
            var features = new double[ParquetScoreCache.NUM_PIN_FEATURES];
            for (int f = 0; f < features.Length; f++)
                features[f] = f + 1.0;
            var entries = new List<FdrEntry>
            {
                new FdrEntry { EntryId = 1, Charge = 2, ScanNumber = 10, ModifiedSequence = "PEPTIDE", Features = features },
            };
            string dest = Path.Combine(Path.GetTempPath(),
                @"osprey_featmask_" + Path.GetRandomFileName() + @".parquet");
            using (var saver = new SimpleFileSaver(dest))
            {
                ParquetScoreCache.WriteScoresParquet(saver.SafeName, entries, null, null, "f.mzML");

                double[] stored = ParquetScoreCache.LoadFullFdrEntries(saver.SafeName).Single().Features;
                double[] loaded = ParquetScoreCache.LoadPinFeaturesFromParquet(saver.SafeName).Single();
                int maskedCount = 0;
                for (int f = 0; f < features.Length; f++)
                {
                    string name = ParquetScoreCache.PIN_FEATURE_NAMES[f];
                    Assert.AreEqual(f + 1.0, stored[f], name + " must be written unmasked");
                    bool masked = PinFeatureMask.DEFAULT_MASKED_FEATURES.Contains(name);
                    Assert.AreEqual(masked, PinFeatureMask.IsMasked(f), name);
                    Assert.AreEqual(masked ? 0.0 : f + 1.0, loaded[f], name + " as the FDR stage loads it");
                    if (masked)
                        maskedCount++;
                }
                Assert.AreEqual(PinFeatureMask.DEFAULT_MASKED_FEATURES.Length, maskedCount,
                    "every default-masked name must be a PIN feature");
            }
            Assert.IsNull(PinFeatureMask.UnknownFeatureName());
            Assert.AreEqual(@";featmask=default", PinFeatureMask.ValidityKeySuffix());
        }

        /// <summary>
        /// Bounded row-group write: a parquet written as several row groups (forced via the
        /// test-only <see cref="ParquetScoreCache.RowGroupRowCapForTest"/> cap) reads back
        /// logically identical -- same rows in the same global (entry_id, charge, scan_number)
        /// order, same running ParquetIndex spanning the group boundaries, same per-row
        /// features + fragment blobs -- to the same entries written as one row group. Only the
        /// physical row-group framing differs. Guards the memory-capping chunked write path.
        /// </summary>
        [TestMethod]
        public void TestParquetBoundedRowGroupRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_rowgroup_rt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // EntryIds deliberately out of order so the write's canonical sort is
                // exercised; distinct features + fragment blobs per row so a chunk-boundary
                // row mismap surfaces as a value mismatch, not a silent pass.
                var entries = new List<FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1, 7, 3, 8 })
                {
                    var features = new double[ParquetScoreCache.NUM_PIN_FEATURES];
                    for (int f = 0; f < features.Length; f++)
                        features[f] = id * 100.0 + f;
                    entries.Add(new FdrEntry
                    {
                        EntryId = id,
                        IsDecoy = (id & 1u) == 0,
                        Charge = 2,
                        ScanNumber = id * 10,
                        ApexRt = id + 0.5,
                        StartRt = id + 0.1,
                        EndRt = id + 0.9,
                        ModifiedSequence = "PEPTIDE" + id,
                        Features = features,
                        FragmentMzs = new[] { id + 0.25, id + 0.75 },
                        FragmentIntensities = new float[] { id, id + 1 },
                        ReferenceXicRts = new[] { id + 1.5, id + 2.5, id + 3.5 },
                        ReferenceXicIntensities = new[] { id * 2.0, id * 3.0 },
                    });
                }

                string singlePath = Path.Combine(dir, "single.scores.parquet");
                string multiPath = Path.Combine(dir, "multi.scores.parquet");

                // Single-group reference (cap unset), then multi-group (cap 3 -> ceil(7/3)=3
                // groups). Same entries, same writer, only the row-group cap differs.
                ParquetScoreCache.RowGroupRowCapForTest = null;
                ParquetScoreCache.WriteScoresParquet(singlePath, entries, null, null, "f.mzML");
                ParquetScoreCache.RowGroupRowCapForTest = 3;
                ParquetScoreCache.WriteScoresParquet(multiPath, entries, null, null, "f.mzML");
                ParquetScoreCache.RowGroupRowCapForTest = null;

                Assert.AreEqual(1, CountRowGroups(singlePath));
                Assert.AreEqual(3, CountRowGroups(multiPath));

                var single = ParquetScoreCache.LoadFullFdrEntries(singlePath);
                var multi = ParquetScoreCache.LoadFullFdrEntries(multiPath);

                Assert.AreEqual(entries.Count, single.Count);
                Assert.AreEqual(single.Count, multi.Count);

                // Distinct entry_ids -> canonical sort is ascending; ParquetIndex is the
                // running global row position, unbroken across the row-group boundaries.
                var expectedIds = new uint[] { 1, 2, 3, 5, 7, 8, 9 };
                for (int i = 0; i < multi.Count; i++)
                {
                    Assert.AreEqual(expectedIds[i], multi[i].EntryId);
                    Assert.AreEqual((uint)i, multi[i].ParquetIndex,
                        "ParquetIndex must be the global row position across row groups");

                    // Multi-group row equals single-group row field-for-field.
                    Assert.AreEqual(single[i].EntryId, multi[i].EntryId);
                    Assert.AreEqual(single[i].ParquetIndex, multi[i].ParquetIndex);
                    Assert.AreEqual(single[i].IsDecoy, multi[i].IsDecoy);
                    Assert.AreEqual(single[i].Charge, multi[i].Charge);
                    Assert.AreEqual(single[i].ScanNumber, multi[i].ScanNumber);
                    Assert.AreEqual(single[i].ApexRt, multi[i].ApexRt);
                    Assert.AreEqual(single[i].StartRt, multi[i].StartRt);
                    Assert.AreEqual(single[i].EndRt, multi[i].EndRt);
                    Assert.AreEqual(single[i].ModifiedSequence, multi[i].ModifiedSequence);
                    CollectionAssert.AreEqual(single[i].Features, multi[i].Features);
                    CollectionAssert.AreEqual(single[i].FragmentMzs, multi[i].FragmentMzs);
                    CollectionAssert.AreEqual(single[i].FragmentIntensities, multi[i].FragmentIntensities);
                }

                // An all-empty blob column across bounded row groups. Decoys form a
                // contiguous block at the end of the entry_id sort, so a group can fall
                // entirely in a region where a blob (e.g. reference XIC) is absent on
                // every row. A zero-length blob would make the whole group's column
                // zero-length, which the Parquet reader cannot decode; empty blobs are
                // written as NULL cells to avoid that. Here reference_xic is empty on
                // every row and cap=3 forces 3 groups -> every group's reference_xic
                // column is all-null; it must still read back as empty per row.
                var emptyXic = new List<FdrEntry>();
                foreach (uint id in new uint[] { 1, 2, 3, 4, 5, 6, 7 })
                {
                    emptyXic.Add(new FdrEntry
                    {
                        EntryId = id,
                        Charge = 2,
                        ScanNumber = id,
                        ModifiedSequence = "PEP" + id,
                        Features = new double[ParquetScoreCache.NUM_PIN_FEATURES],
                        FragmentMzs = new[] { id + 0.5 },
                        FragmentIntensities = new float[] { id },
                        // ReferenceXicRts / ReferenceXicIntensities left null (empty).
                    });
                }
                string emptyXicPath = Path.Combine(dir, "empty_xic.scores.parquet");
                ParquetScoreCache.RowGroupRowCapForTest = 3;
                ParquetScoreCache.WriteScoresParquet(emptyXicPath, emptyXic, null, null, "f.mzML");
                ParquetScoreCache.RowGroupRowCapForTest = null;

                Assert.AreEqual(3, CountRowGroups(emptyXicPath));
                var reloaded = ParquetScoreCache.LoadFullFdrEntries(emptyXicPath);
                Assert.AreEqual(emptyXic.Count, reloaded.Count);
                foreach (var e in reloaded)
                {
                    Assert.AreEqual(0, e.ReferenceXicRts.Length,
                        "an all-empty reference-XIC column must read back as empty, not crash");
                    Assert.AreEqual(0, e.ReferenceXicIntensities.Length);
                    Assert.AreEqual(1, e.FragmentMzs.Length);
                }
            }
            finally
            {
                ParquetScoreCache.RowGroupRowCapForTest = null;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// CWT-candidate column round-trip through the real writer and reader: the
        /// per-row candidate lists come back bit-identical, keep their own row via the
        /// running <see cref="FdrEntry.ParquetIndex"/>, and survive a multi-row-group
        /// write. Covers <see cref="CwtCandidateCodec"/>'s wiring into the
        /// cwt_candidates column, which the codec's own unit tests cannot reach
        /// because they never touch parquet.
        ///
        /// A row with a NULL candidate list is included deliberately: the writer
        /// normalizes null and empty alike to a 4-byte zero-count blob (never a null
        /// cell) to match Rust, and nothing else exercises the null branch.
        /// </summary>
        [TestMethod]
        public void TestCwtCandidatesRoundTripThroughParquet()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_cwt_parquet_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Ids out of order so the write's canonical sort runs; candidate-list
                // lengths deliberately mixed (2 / none / 1 / null) so a per-row boundary
                // mismap surfaces as a count mismatch instead of a silent pass.
                var byId = new Dictionary<uint, List<CwtCandidate>>
                {
                    { 5, new List<CwtCandidate> {
                        NewCwtCandidate(51.5, 51.0, 52.0, 1.5e6, 42.5, 8.25),
                        // Boundary value that pins the byte-exact BitConverter path
                        // (the ryu/R formatter ambiguity from the Stage 5 dump).
                        NewCwtCandidate(0.097751617431640625, 0.5, 1.5, 1.0, 1.0, -1.5) } },
                    { 2, new List<CwtCandidate>() },
                    { 9, new List<CwtCandidate> {
                        NewCwtCandidate(90.25, 90.0, 90.5, 7.5e5, 13.0, 3.125) } },
                    { 1, null },
                };

                var entries = new List<FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1 })
                {
                    var e = MakeStreamEntry(id, id * 100.0);
                    e.CwtCandidates = byId[id];
                    entries.Add(e);
                }

                string path = Path.Combine(dir, "cwt.scores.parquet");
                // Cap 2 -> ceil(4/2) = 2 row groups, so the reader's per-group append
                // loop is exercised rather than a single-group degenerate case.
                ParquetScoreCache.RowGroupRowCapForTest = 2;
                ParquetScoreCache.WriteScoresParquet(path, entries, null, null, "f.mzML");
                ParquetScoreCache.RowGroupRowCapForTest = null;

                // Full entries carry both ParquetIndex and CwtCandidates, so one load
                // checks the candidate values AND that each list landed on the row its
                // running index claims (the alignment defect that once made every
                // lookup return row 0's list).
                var reloaded = ParquetScoreCache.LoadFullFdrEntries(path);
                Assert.AreEqual(entries.Count, reloaded.Count, "row count");
                for (int i = 0; i < reloaded.Count; i++)
                {
                    Assert.AreEqual((uint)i, reloaded[i].ParquetIndex,
                        "ParquetIndex must be the running row position");
                }

                // The column-only reader must agree row-for-row with the full load.
                var columnOnly = ParquetScoreCache.LoadCwtCandidatesFromParquet(path);
                Assert.AreEqual(reloaded.Count, columnOnly.Count, "cwt column row count");

                foreach (var row in reloaded)
                {
                    var expected = byId[row.EntryId] ?? new List<CwtCandidate>();
                    var actual = row.CwtCandidates ?? new List<CwtCandidate>();
                    string label = "entry " + row.EntryId;
                    Assert.AreEqual(expected.Count, actual.Count, label + " candidate count");
                    // Null and empty must both decode to an empty list, never null:
                    // the writer emits the 4-byte zero-count blob for each.
                    Assert.IsNotNull(row.CwtCandidates, label + " list must not be null");
                    Assert.AreEqual(expected.Count, columnOnly[(int)row.ParquetIndex].Count,
                        label + " column-only candidate count");
                    for (int k = 0; k < expected.Count; k++)
                    {
                        AssertCwtCandidateBitEqual(expected[k], actual[k],
                            label + " candidate " + k);
                    }
                }

                AssertTrainingExportProjection(path, reloaded);
            }
            finally
            {
                ParquetScoreCache.RowGroupRowCapForTest = null;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Concurrent scores-parquet writes must not corrupt each other's nullable columns.
        /// When the writer's DataFields were shared statics, building one write's schema reset
        /// the shared fields' definition level to 0 for a moment. A write in progress on another
        /// thread then recorded present values as null, and its file read back with null
        /// cwt_candidates cells and every later list shifted onto the wrong row. Found with 4
        /// files scored at once in the 2026-10-13 SEA-AD acceptance run. Each row here carries
        /// one candidate whose apex is its own entry id, so any null or shift is a mismatch.
        /// </summary>
        [TestMethod]
        public void TestConcurrentScoresParquetWritesKeepCwtCandidatesAligned()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_cwt_concurrent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                const int rows = 20000;
                var entries = new List<FdrEntry>(rows);
                for (uint id = 1; id <= rows; id++)
                {
                    var e = MakeStreamEntry(id, id);
                    e.CwtCandidates = new List<CwtCandidate> { NewCwtCandidate(id, id - 0.5, id + 0.5, 1.0, 1.0, 1.0) };
                    entries.Add(e);
                }

                int writers = Math.Max(4, Environment.ProcessorCount / 2);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                int files = 0;
                var failures = new List<string>();
                Parallel.For(0, writers, new ParallelOptions { MaxDegreeOfParallelism = writers }, w =>
                {
                    // Each writer needs its own entries: the write assigns ParquetIndex.
                    var mine = entries.Select(e => MakeStreamEntry(e.EntryId, e.EntryId)).ToList();
                    for (int i = 0; i < mine.Count; i++)
                        mine[i].CwtCandidates = entries[i].CwtCandidates;
                    // A minimum number of writes each, so a writer started late still overlaps the others.
                    for (int n = 0; n < 3 || DateTime.UtcNow < deadline; n++)
                    {
                        string path = Path.Combine(dir, string.Format("w{0}_{1}.scores.parquet", w, n));
                        ParquetScoreCache.WriteScoresParquet(path, mine, null, null, "f.mzML");
                        var lists = ParquetScoreCache.LoadCwtCandidatesFromParquet(path);
                        Interlocked.Increment(ref files);
                        if (lists.Count != rows)
                        {
                            lock (failures)
                                failures.Add(string.Format("{0}: {1} rows", Path.GetFileName(path), lists.Count));
                        }
                        for (int row = 0; row < lists.Count; row++)
                        {
                            var list = lists[row];
                            if (list == null || list.Count != 1 || list[0].ApexRt != row + 1)
                            {
                                lock (failures)
                                {
                                    failures.Add(string.Format("{0} row {1}: {2}", Path.GetFileName(path), row,
                                        list == null ? "null" : list.Count == 1 ? "apex " + list[0].ApexRt : list.Count + " candidates"));
                                }
                                break;
                            }
                        }
                        File.Delete(path);
                    }
                });
                Assert.AreEqual(0, failures.Count, string.Format("{0} of {1} concurrently written files misaligned: {2}",
                    failures.Count, files, string.Join("; ", failures.Take(5))));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The training export's projection of the same two-group file: the target rows only,
        /// each field it reads exactly as the full load reads it - the running ParquetIndex
        /// across groups with the decoys skipped included - and no CWT candidates or fragment
        /// arrays.
        /// </summary>
        private static void AssertTrainingExportProjection(string path, List<FdrEntry> full)
        {
            var targets = full.Where(e => !e.IsDecoy).ToList();
            var projected = ParquetScoreCache.LoadTrainingExportRows(path);
            Assert.AreEqual(targets.Count, projected.Count, "targets only");
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                var a = projected[i];
                Assert.AreEqual(e.EntryId, a.EntryId);
                Assert.AreEqual(e.ParquetIndex, a.ParquetIndex);
                Assert.AreEqual(e.Charge, a.Charge);
                Assert.AreEqual(e.ModifiedSequence, a.ModifiedSequence);
                Assert.AreEqual(e.ScanNumber, a.ScanNumber);
                Assert.AreEqual(e.ApexRt, a.ApexRt);
                Assert.AreEqual(e.StartRt, a.StartRt);
                Assert.AreEqual(e.EndRt, a.EndRt);
                Assert.AreEqual(e.BoundsArea, a.BoundsArea);
                CollectionAssert.AreEqual(e.Features, a.Features);
                CollectionAssert.AreEqual(e.ReferenceXicRts, a.ReferenceXicRts);
                CollectionAssert.AreEqual(e.ReferenceXicIntensities, a.ReferenceXicIntensities);
                Assert.IsNull(a.CwtCandidates, "the CWT candidates are not decoded");
                Assert.AreEqual(0, a.FragmentMzs.Length, "the fragment m/z are not decoded");
                Assert.AreEqual(0, a.FragmentIntensities.Length, "the fragment intensities are not decoded");
            }
        }

        private static CwtCandidate NewCwtCandidate(double apexRt, double startRt, double endRt,
            double area, double snr, double coelutionScore)
        {
            return new CwtCandidate
            {
                ApexRt = apexRt,
                StartRt = startRt,
                EndRt = endRt,
                Area = area,
                Snr = snr,
                CoelutionScore = coelutionScore,
            };
        }

        private static void AssertCwtCandidateBitEqual(CwtCandidate expected, CwtCandidate actual,
            string label)
        {
            AssertBitEqual(expected.ApexRt, actual.ApexRt, label + " ApexRt");
            AssertBitEqual(expected.StartRt, actual.StartRt, label + " StartRt");
            AssertBitEqual(expected.EndRt, actual.EndRt, label + " EndRt");
            AssertBitEqual(expected.Area, actual.Area, label + " Area");
            AssertBitEqual(expected.Snr, actual.Snr, label + " Snr");
            AssertBitEqual(expected.CoelutionScore, actual.CoelutionScore, label + " CoelutionScore");
        }

        private static void AssertBitEqual(double expected, double actual, string label)
        {
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected),
                BitConverter.DoubleToInt64Bits(actual), label + " bit mismatch");
        }

        /// <summary>
        /// The survivor-subset currency predicate
        /// (<see cref="ParquetScoreCache.IsCurrentReconciledSurvivorSubset"/>), against real
        /// artifacts rather than a hand-built footer.
        ///
        /// <para>It answers whether a reconciled parquet can be READ in the shape a per-run
        /// survivor rebuild needs, and two callers ask it: the Stage 7 refusal that names the
        /// stale files, and the admission that decides whether the second-pass join may fold a
        /// run at a time. Both of them turn a wrong answer into a whole-cohort outcome - a run
        /// admitted to a fold it then aborts, or an O(files) pool nobody asked for - so the
        /// three states are pinned here: absent, present-without-the-marker (the Stage 4
        /// original, which is exactly the file a path-derivation slip would hand it), and
        /// present-and-current.</para>
        /// </summary>
        [TestMethod]
        public void TestIsCurrentReconciledSurvivorSubset()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_recon_current_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string originalPath = Path.Combine(dir, "sample1.scores.parquet");
                string reconciledPath = Path.Combine(dir, "sample1.scores-reconciled.parquet");

                // Absent: false, and no throw. This is the no-work state on a cold cohort, so it
                // has to be an answer rather than an error.
                Assert.IsFalse(ParquetScoreCache.IsCurrentReconciledSurvivorSubset(reconciledPath));

                var original = new List<FdrEntry>();
                foreach (uint id in new uint[] { 3, 1, 2 })
                    original.Add(MakeStreamEntry(id, id * 10.0));
                ParquetScoreCache.WriteScoresParquet(originalPath, original, null, null, "f.mzML");

                // The Stage 4 original is a well-formed parquet with neither the marker nor the
                // score_index column, and it must not pass: it is the file a stem-derivation
                // slip substitutes, and its rows are the PRE-reconciliation ones.
                Assert.IsFalse(ParquetScoreCache.IsCurrentReconciledSurvivorSubset(originalPath));

                // Written the way Stage 6 writes it - the marker in the footer, score_index in
                // the schema - which is the only combination that passes.
                //
                // Metadata from the REAL producer, not hand-fed. StreamReconciledScoresParquet
                // writes the caller's map verbatim, so a test that supplies the marker itself
                // asserts only that the reader can read what the test wrote: let
                // ReconciledParquetWriter stop stamping it and this would still pass while
                // AllReconciledParquetsCurrent returned false for every real run and the whole
                // cohort fell back to the resident join. Going through the producer is what
                // makes the two sides able to disagree.
                var metadata = ReconciledParquetWriter.BuildReconciliationMetadata(
                    new OspreyConfig(), null);
                Assert.AreEqual(ParquetScoreCache.RECONCILED_SURVIVORS,
                    metadata["osprey.reconciled"]);
                ParquetScoreCache.StreamReconciledScoresParquet(
                    originalPath, reconciledPath, null, null, metadata, null, "f.mzML", null,
                    PerFileRescoreTask.TASK_NAME, null, null);
                Assert.IsTrue(ParquetScoreCache.IsCurrentReconciledSurvivorSubset(reconciledPath));

                // And it is the POSITIVE form of the interim-shape refusal, not a second
                // opinion: a file this accepts is one that one rejects.
                Assert.IsFalse(ParquetScoreCache.IsSubsetWithoutScoreIndex(reconciledPath));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// Stage-6 streaming reconciled transfer: streaming the original parquet
        /// group-by-group with an overlay map + gap-fill list
        /// (<see cref="ParquetScoreCache.StreamReconciledScoresParquet"/>) is logically
        /// identical -- same rows, same per-row features + blobs -- to the former
        /// load-all + in-place overlay + re-sort write, including the gap-fill (which the
        /// streaming path merges into canonical (entry_id, charge, scan) position). Also asserts the
        /// replaced / appended / original counts, the out-of-range warning, and that the
        /// output is genuinely multi-group.
        /// </summary>
        [TestMethod]
        public void TestStreamReconciledTransferMatchesLoadAllOverlay()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_stream_recon_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Original rows: out-of-order ids so the write's canonical sort runs,
                // distinct features + blobs per row so any chunk-boundary mismap surfaces
                // as a value mismatch rather than a silent pass.
                var original = new List<FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1, 7, 3, 8 })
                    original.Add(MakeStreamEntry(id, id * 100.0));

                string originalPath = Path.Combine(dir, "orig.scores.parquet");
                string refPath = Path.Combine(dir, "ref.scores-reconciled.parquet");
                string streamPath = Path.Combine(dir, "stream.scores-reconciled.parquet");

                // Write the original as several bounded row groups (cap 3 -> 3 groups).
                ParquetScoreCache.RowGroupRowCapForTest = 3;
                ParquetScoreCache.WriteScoresParquet(originalPath, original, null, null, "f.mzML");

                // Map each original entry_id to the global row position the write assigned,
                // so the overlay indices below hit the right rows.
                var posById = new Dictionary<uint, uint>();
                foreach (var e in ParquetScoreCache.LoadFullFdrEntries(originalPath))
                    posById[e.EntryId] = e.ParquetIndex.Value;

                // Overlay two existing rows (id 3, id 8) with distinctly re-scored
                // features/blobs; leave id 2 un-overlaid (its original row must stream
                // through untouched); and one out-of-range index that must be dropped
                // with a warning.
                var overlayByIndex = new Dictionary<uint, FdrEntry>
                {
                    { posById[3], MakeStreamEntry(3, 7777.0) },
                    { posById[8], MakeStreamEntry(8, 8888.0) },
                    { 999u, MakeStreamEntry(200, 1.0) },   // out of range -> dropped + warned
                };
                // Gap-fill ids 4 and 6 fall BETWEEN existing original ids (3 < 4 < 5 < 6 <
                // 7), so the streaming merge must interleave them into canonical position;
                // a plain append-at-end would misplace them and the physical-order check
                // below would fail. Listed out of order so the merge's own sort is exercised.
                var gapFill = new List<FdrEntry>
                {
                    MakeStreamEntry(6, 60060.0),
                    MakeStreamEntry(4, 40040.0),
                };

                // Reference (old behavior): load the whole file, overlay in place by index,
                // append gap-fill, re-sort on write.
                var oldFull = ParquetScoreCache.LoadFullFdrEntries(originalPath);
                foreach (var kv in overlayByIndex)
                    if (kv.Key < oldFull.Count)
                        oldFull[(int)kv.Key] = kv.Value;
                oldFull.AddRange(gapFill);
                ParquetScoreCache.WriteScoresParquet(refPath, oldFull, null, null, "f.mzML");
                var refResult = ParquetScoreCache.LoadFullFdrEntries(refPath);

                // New behavior: stream the transfer.
                var warnings = new List<string>();
                var result = ParquetScoreCache.StreamReconciledScoresParquet(
                    originalPath, streamPath, overlayByIndex, gapFill, null, null, "f.mzML", null,
                    PerFileRescoreTask.TASK_NAME, null, warnings.Add);
                ParquetScoreCache.RowGroupRowCapForTest = null;

                // Counts: two in-range overlays replaced, two gap-fill appended, 7 originals.
                Assert.AreEqual(2, result.NReplaced);
                Assert.AreEqual(2, result.NAppended);
                Assert.AreEqual(7, result.OrigRowCount);

                // The one out-of-range overlay index is dropped with exactly one warning.
                Assert.AreEqual(1, warnings.Count);

                // The stream output is genuinely multi-group (3 original groups + gap-fill).
                Assert.IsTrue(CountRowGroups(streamPath) >= 3,
                    "streaming reconciled transfer must preserve bounded row groups");

                // Physical order: the streaming merge must emit rows already in canonical
                // (entry_id, charge, scan_number) order -- gap-fill interleaved by scan,
                // NOT appended at the end. This is the guarantee Pass 2's projection sort
                // relies on; a plain append would leave ids 4/6 after id 9 and fail here.
                var streamResult = ParquetScoreCache.LoadFullFdrEntries(streamPath);
                Comparison<FdrEntry> byKey = (a, b) =>
                {
                    int c = a.EntryId.CompareTo(b.EntryId);
                    if (c != 0)
                        return c;
                    c = a.Charge.CompareTo(b.Charge);
                    return c != 0 ? c : a.ScanNumber.CompareTo(b.ScanNumber);
                };
                for (int i = 1; i < streamResult.Count; i++)
                    Assert.IsTrue(byKey(streamResult[i - 1], streamResult[i]) <= 0,
                        "streaming reconciled transfer must emit rows in canonical sorted order");

                // Logical equivalence: same rows, same values (both canonical after sort).
                refResult.Sort(byKey);
                streamResult.Sort(byKey);
                AssertStreamRowsEqual(refResult, streamResult);

                // The overlay actually took effect: id 3 / id 8 carry the re-scored
                // features, while the un-overlaid id 2 keeps its ORIGINAL features.
                Assert.AreEqual(7777.0, RowById(streamResult, 3).Features[0]);
                Assert.AreEqual(8888.0, RowById(streamResult, 8).Features[0]);
                Assert.AreEqual(200.0, RowById(streamResult, 2).Features[0]);
            }
            finally
            {
                ParquetScoreCache.RowGroupRowCapForTest = null;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The Stage-6 reconciled parquet carries only the Stage 5 SURVIVORS, not a
        /// row-for-row twin of the Stage 4 parquet: an original row whose entry_id is
        /// outside the keep set is not emitted, gap-fill rows are emitted regardless
        /// (survivors by construction), canonical order still holds across the drops, and
        /// the reported original-row count still describes the INPUT rather than the
        /// output. Emitting the compacted-away rows is what made the Stage 4 parquet a
        /// required Stage 7 input (issue #4486).
        /// </summary>
        [TestMethod]
        public void TestReconciledTransferKeepsOnlySurvivors()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_recon_subset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var original = new List<FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1, 7, 3, 8 })
                    original.Add(MakeStreamEntry(id, id * 100.0));

                string originalPath = Path.Combine(dir, "orig.scores.parquet");
                string subsetPath = Path.Combine(dir, "subset.scores-reconciled.parquet");
                ParquetScoreCache.WriteScoresParquet(originalPath, original, null, null, "f.mzML");

                // Survivors: 2, 3, 8. Dropped: 1, 5, 7, 9. Gap-fill id 4 sorts between a
                // kept row (3) and a DROPPED one (5), so an interleave that only advanced
                // on emitted rows would misplace it.
                // Keyed on the full identity MakeStreamEntry assigns (charge 2, scan id*10),
                // not on entry_id: compaction drops an entry_id's extra scans, so an
                // entry_id-keyed set would match every row and drop nothing.
                var keep = new HashSet<(uint, byte, uint)>
                {
                    (2u, 2, 20u), (3u, 2, 30u), (8u, 2, 80u),
                };
                var gapFill = new List<FdrEntry> { MakeStreamEntry(4, 40040.0) };

                var result = ParquetScoreCache.StreamReconciledScoresParquet(
                    originalPath, subsetPath, new Dictionary<uint, FdrEntry>(), gapFill,
                    null, null, "f.mzML", keep, PerFileRescoreTask.TASK_NAME, null, s => { });

                Assert.AreEqual(1, result.NAppended);
                // Rows READ, not emitted - the count still describes the input.
                Assert.AreEqual(7, result.OrigRowCount);
                // Four emitted: three survivors plus the gap-fill row.
                Assert.AreEqual(4, result.NWritten);

                var rows = ParquetScoreCache.LoadFullFdrEntries(subsetPath);
                var ids = new List<uint>();
                foreach (var row in rows)
                    ids.Add(row.EntryId);
                CollectionAssert.AreEqual(new uint[] { 2, 3, 4, 8 }, ids,
                    "reconciled parquet must hold the survivors plus gap-fill, in canonical order");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The parquet footer's declared row count is the SAME number a full scan reaches.
        ///
        /// <para>This is the invariant the counts-only projection rests on. It used to read
        /// every row of every file - 1,342,686,095 at 446 runs, ~10 minutes - through a builder
        /// whose <c>AddRow</c> discards all five fields and increments a counter, to learn what
        /// <c>ProbeResumeSchemaAndRows</c> returns from a footer the same loop had already
        /// opened. The scan is gone; if these two ever disagreed, first-pass FDR would be sized
        /// from a number no longer describing the file, so pin them together.</para>
        ///
        /// <para>Multiple row groups on purpose (cap 2 over 5 rows -> 3 groups): a footer count
        /// and a per-group append loop are the two things that could drift apart, and a
        /// single-group file cannot tell them apart.</para>
        /// </summary>
        [TestMethod]
        public void TestFooterRowCountMatchesScan()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_rowcount_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var entries = new List<FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1, 7 })
                    entries.Add(MakeStreamEntry(id, id * 100.0));

                string path = Path.Combine(dir, "counts.scores.parquet");
                ParquetScoreCache.RowGroupRowCapForTest = 2;
                try
                {
                    ParquetScoreCache.WriteScoresParquet(path, entries, null, null, "f.mzML");
                }
                finally
                {
                    ParquetScoreCache.RowGroupRowCapForTest = null;
                }

                // Assert the multi-group shape rather than assuming the cap took. If the writer
                // is ever re-routed off WriteChunkedParquet the 5 rows land in ONE group and
                // this test still passes 5 == 5 - now exercising exactly the degenerate case
                // its own premise says cannot detect footer-vs-per-group drift.
                Assert.AreEqual(3, CountRowGroups(path));

                int scanned = 0;
                ParquetScoreCache.ReadFdrStubScalars(path,
                    (entryId, charge, isDecoy, coelutionSum, modseq, apexRt) => scanned++,
                    StubColumns.Core);

                var probe = ParquetScoreCache.ProbeResumeSchemaAndRows(path);
                Assert.IsTrue(probe.HasPinFeatures);
                Assert.AreEqual(entries.Count, scanned);
                Assert.AreEqual(scanned, probe.RowCount);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
        }

        // Build an FdrEntry with a per-row-distinct feature vector (feature[f] = baseValue + f)
        // and distinct fragment / XIC blobs derived from baseValue, so a chunk-boundary row
        // mismap -- or an overlay row silently keeping the ORIGINAL blobs -- surfaces as a
        /// <summary>
        /// Write and read the same fixture many times over and assert every scalar survives.
        /// The regression guard for the parallel-write data race: `ParquetPlainEncoder` returned
        /// its widening buffer to the ArrayPool and then encoded from it, so once columns
        /// compressed concurrently another column's dictionary-index buffer could land in the
        /// byte-typed <c>charge</c> column. Measured at 27 corrupted round-trips in 20,000
        /// before the fix and 0 in 100,000 after.
        ///
        /// <para>A single-shot round-trip cannot see a defect this rare - it surfaced as a ~2%
        /// failure across four unrelated tests. Iterating in-process is what turns hours of
        /// full-suite soaking into seconds. Cheap by default (25 iterations) so it costs the
        /// gate nothing; set <c>OSPREYDEV_PARQUET_STRESS_ITERS</c> to sweep harder. The 6.1.0 fork
        /// had the same bug, fixed in 6.1.0-osprey3, and Osprey prepares a row group's columns
        /// concurrently on it too. On .NET 10 this test did not catch the unfixed fork in 5,000
        /// iterations, so it is a round-trip check rather than a guard for that fix.</para>
        /// </summary>
        [TestMethod]
        public void TestParquetRoundTripScalarStress()
        {
            int iters = 25;
            string raw = Environment.GetEnvironmentVariable(@"OSPREYDEV_PARQUET_STRESS_ITERS");
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out int parsed) && parsed > 0)
                iters = parsed;

            string dir = Path.Combine(Path.GetTempPath(), @"osprey_stress_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                var entries = new List<FdrEntry>();
                var expected = new Dictionary<uint, FdrEntry>();
                foreach (uint id in new uint[] { 5, 2, 9, 1, 7, 3, 8 })
                {
                    var e = MakeStreamEntry(id, id * 100.0);
                    entries.Add(e);
                    expected[id] = e;
                }

                int nBad = 0;
                string firstBad = null;
                for (int i = 0; i < iters; i++)
                {
                    string path = Path.Combine(dir, @"stress" + i + ParquetScoreCache.EXT_SCORES);
                    ParquetScoreCache.WriteScoresParquet(path, entries, null, null, @"f.mzML");
                    var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(path);
                    string bad = null;
                    if (stubs.Count != entries.Count)
                    {
                        bad = string.Format(@"row count {0}, expected {1}", stubs.Count, entries.Count);
                    }
                    else
                    {
                        foreach (var s in stubs)
                        {
                            if (!expected.TryGetValue(s.EntryId, out FdrEntry want))
                            {
                                bad = string.Format(@"unknown entry_id {0}", s.EntryId);
                                break;
                            }
                            if (s.Charge != want.Charge)
                                bad = string.Format(@"charge {0}, expected {1}", s.Charge, want.Charge);
                            else if (s.ScanNumber != want.ScanNumber)
                                bad = string.Format(@"scan_number {0}, expected {1}", s.ScanNumber, want.ScanNumber);
                            else if (s.IsDecoy != want.IsDecoy)
                                bad = string.Format(@"is_decoy {0}, expected {1}", s.IsDecoy, want.IsDecoy);
                            if (bad != null)
                            {
                                bad = string.Format(@"entry_id {0}: {1}", s.EntryId, bad);
                                break;
                            }
                        }
                    }
                    if (bad != null)
                    {
                        nBad++;
                        if (firstBad == null)
                            firstBad = string.Format(@"iteration {0} - {1}", i, bad);
                    }
                    File.Delete(path);
                }

                Assert.AreEqual(0, nBad, string.Format(
                    @"{0} of {1} parquet round-trips lost a scalar. First: {2}",
                    nBad, iters, firstBad ?? @"(none)"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // value mismatch. Key fields (entry_id, charge, scan_number) stay id-derived so an
        // overlay preserves the canonical sort key of the row it replaces.
        /// <summary>
        /// A score parquet with a zero charge - the pre-2026-09-17 column-writer race - is
        /// refused by the stub walks even when the charges are taken from the library
        /// (OSPREY_STUB_IDENTITY=1, the default), which never needs the file's charge. Without
        /// the check such a file passed Stages 5 and 6 on library charges and failed hours later.
        /// Asserts the exception type only: the message is a resource string.
        /// </summary>
        [TestMethod]
        public void TestZeroChargeRefusedUnderLibraryIdentity()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_zero_charge_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Odd ids, so MakeStreamEntry marks them targets, matching the library.
                var entries = new List<FdrEntry> { MakeStreamEntry(1, 100.0), MakeStreamEntry(3, 300.0) };
                entries[1].Charge = 0;
                string path = Path.Combine(dir, "zero.scores.parquet");
                ParquetScoreCache.WriteScoresParquet(path, entries, null, null, "f.mzML");
                var identity = new LibraryIdentity(new List<LibraryEntry>
                {
                    new LibraryEntry(1, @"PEPTIDE1", @"PEPTIDE1", 2, 450.0, 10.0),
                    new LibraryEntry(3, @"PEPTIDE3", @"PEPTIDE3", 2, 460.0, 11.0),
                });

                Assert.ThrowsException<InvalidDataException>(() =>
                    ParquetScoreCache.ReadFdrStubScalars(path, (id, charge, decoy, coelution, modseq, apex) => { },
                        StubColumns.Core, identity));
                Assert.ThrowsException<InvalidDataException>(() =>
                    ParquetScoreCache.LoadFdrStubsFromParquet(path, null, null, identity));
                // And without the library, as before.
                Assert.ThrowsException<InvalidDataException>(() =>
                    ParquetScoreCache.ReadFdrStubScalars(path, (id, charge, decoy, coelution, modseq, apex) => { },
                        StubColumns.Core));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static FdrEntry MakeStreamEntry(uint id, double baseValue)
        {
            var features = new double[ParquetScoreCache.NUM_PIN_FEATURES];
            for (int f = 0; f < features.Length; f++)
                features[f] = baseValue + f;
            return new FdrEntry
            {
                EntryId = id,
                IsDecoy = (id & 1u) == 0,
                Charge = 2,
                ScanNumber = id * 10,
                ApexRt = id + 0.5,
                StartRt = id + 0.1,
                EndRt = id + 0.9,
                BoundsArea = id + 0.3,
                BoundsSnr = id + 0.7,
                ModifiedSequence = "PEPTIDE" + id,
                Features = features,
                FragmentMzs = new[] { baseValue + 0.25, baseValue + 0.75 },
                FragmentIntensities = new[] { (float)baseValue, (float)(baseValue + 1) },
                ReferenceXicRts = new[] { baseValue + 1.5, baseValue + 2.5 },
                ReferenceXicIntensities = new[] { baseValue * 2.0, baseValue * 3.0 },
            };
        }

        private static FdrEntry RowById(List<FdrEntry> rows, uint id)
        {
            return rows.First(e => e.EntryId == id);
        }

        private static void AssertStreamRowsEqual(List<FdrEntry> expected, List<FdrEntry> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                var e = expected[i];
                var a = actual[i];
                Assert.AreEqual(e.EntryId, a.EntryId);
                Assert.AreEqual(e.IsDecoy, a.IsDecoy);
                Assert.AreEqual(e.Charge, a.Charge);
                Assert.AreEqual(e.ScanNumber, a.ScanNumber);
                Assert.AreEqual(e.ApexRt, a.ApexRt);
                Assert.AreEqual(e.StartRt, a.StartRt);
                Assert.AreEqual(e.EndRt, a.EndRt);
                Assert.AreEqual(e.CoelutionSum, a.CoelutionSum);
                Assert.AreEqual(e.BoundsArea, a.BoundsArea);
                Assert.AreEqual(e.BoundsSnr, a.BoundsSnr);
                Assert.AreEqual(e.ModifiedSequence, a.ModifiedSequence);
                CollectionAssert.AreEqual(e.Features, a.Features);
                CollectionAssert.AreEqual(e.FragmentMzs, a.FragmentMzs);
                CollectionAssert.AreEqual(e.FragmentIntensities, a.FragmentIntensities);
                CollectionAssert.AreEqual(e.ReferenceXicRts, a.ReferenceXicRts);
                CollectionAssert.AreEqual(e.ReferenceXicIntensities, a.ReferenceXicIntensities);
            }
        }

        private static int CountRowGroups(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return ParquetReader.CreateAsync(stream).GetAwaiter().GetResult().RowGroupCount;
        }

        private static FdrEntry MakeFdrEntry(uint id, double score, double q, double pep,
            double proteinQvalue = 1.0)
        {
            return new FdrEntry
            {
                EntryId = id,
                ParquetIndex = id,
                IsDecoy = false,
                Charge = 2,
                ScanNumber = 0,
                Score = score,
                RunPrecursorQvalue = q,
                RunPeptideQvalue = q + 1.0e-9,
                ExperimentPrecursorQvalue = q + 2.0e-9,
                ExperimentPeptideQvalue = q + 3.0e-9,
                ExperimentProteinQvalue = proteinQvalue,
                Pep = pep,
                ModifiedSequence = "PEPTIDE",
            };
        }

        /// <summary>
        /// Round-trip: write entries via Write, then read them back via
        /// TryRead and verify every numeric field survives bit-for-bit.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_rt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.1st-pass.fdr_scores.bin");
                // The RUN-scope columns are all this file carries at v5; the experiment-scope
                // half is covered by TestFdrScopeSplitV5. Values still match Rust's
                // fdr_scores_sidecar round-trip inputs, but the records themselves no longer
                // do - Rust fuses both scopes into one 68-byte record, so the
                // OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT byte-parity hook below cannot compare
                // equal until that side splits too (issue #4486).
                var entries = new List<FdrEntry>
                {
                    MakeFdrEntry(0, -3.5, 0.001, 0.02, proteinQvalue: 0.0042),
                    MakeFdrEntry(1, -3.4, 0.002, 0.05, proteinQvalue: 0.0123),
                    MakeFdrEntry(2, -3.3, 0.003, 0.08, proteinQvalue: 0.95),
                };

                FdrScoresSidecar.Write(path, entries, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                // Cross-impl byte-parity hook: when the harness runs this test
                // with OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT=<path> set, copy our
                // output to that path so a sibling test on the Rust osprey
                // side (using the same input data) can be byte-compared
                // against ours. Same hardcoded entries on both sides; same
                // format spec; the output files must match bit-for-bit.
                if (!string.IsNullOrEmpty(OspreyEnvironment.CrossImplFdrSidecarOut))
                    File.Copy(path, OspreyEnvironment.CrossImplFdrSidecarOut, overwrite: true);

                // File size sanity check.
                long size = new FileInfo(path).Length;
                Assert.AreEqual(
                    FdrScoresSidecar.HeaderLength + entries.Count * FdrScoresSidecar.RecordLength +
                    BinarySidecarStamp.Encode(TestStamps.Any).Length,
                    size);

                // Stubs with cleared FDR fields — TryRead must repopulate them.
                var loaded = new List<FdrEntry>
                {
                    MakeFdrEntry(0, 0.0, 0.0, 0.0),
                    MakeFdrEntry(1, 0.0, 0.0, 0.0),
                    MakeFdrEntry(2, 0.0, 0.0, 0.0),
                };
                // Snapshot the EXPERIMENT-scope columns before the read: at v5 they are not in
                // this file, so TryRead must leave them exactly as it found them. A reader that
                // still wrote them would be decoding whatever field now sits at the offset it
                // remembers, which is the failure mode the split was designed to make loud.
                var expBefore = new List<double[]>();
                foreach (var e in loaded)
                {
                    expBefore.Add(new[]
                    {
                        e.ExperimentPrecursorQvalue, e.ExperimentPeptideQvalue,
                        e.ExperimentProteinQvalue, e.ExperimentAggregateScore,
                    });
                }
                Assert.IsTrue(FdrScoresSidecar.TryRead(path, loaded, FdrScoresSidecar.Pass.FirstPass));

                for (int i = 0; i < entries.Count; i++)
                {
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].Score),
                                    BitConverter.DoubleToInt64Bits(loaded[i].Score));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].RunPrecursorQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].RunPrecursorQvalue));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].RunPeptideQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].RunPeptideQvalue));
                    // NOT Pep: the per-file sidecar no longer carries it (issue #4486). It is an
                    // experiment-scope value now, asserted against the experiment map below.
                    Assert.AreEqual(expBefore[i][0], loaded[i].ExperimentPrecursorQvalue);
                    Assert.AreEqual(expBefore[i][1], loaded[i].ExperimentPeptideQvalue);
                    Assert.AreEqual(expBefore[i][2], loaded[i].ExperimentProteinQvalue);
                    Assert.AreEqual(expBefore[i][3], loaded[i].ExperimentAggregateScore);
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The per-file second-pass competition decoys artifact (issue #4486) - the decoy side of
        /// one file's pass-2 competition, which the pool-image sidecar structurally cannot carry
        /// because the winning decoy of a base_id is routinely a non-survivor.
        ///
        /// <list type="bullet">
        /// <item><b>Canonical.</b> The file is a function of its CONTENTS, not of the order the
        /// producer's dictionary enumerated: the same pairs inserted in opposite orders must
        /// produce byte-identical files. The straight-through and distributed routes accumulate
        /// in different orders and the gate compares their artifacts byte for byte.</item>
        /// <item><b>Key-checked.</b> The base_id key is not stored - it is the entry_id's low 31
        /// bits - so a TARGET entry_id or a mis-filed key would have the reader silently
        /// re-derive a different base_id and put a target observation into the decoy null. Both
        /// fail the write instead.</item>
        /// <item><b>Rejects a foreign file.</b> This shares a header shape with the per-file
        /// scores sidecar and the analysis-wide experiment sidecar; only the magic separates
        /// them, so a mix-up has to be a rejection rather than plausible garbage.</item>
        /// <item><b>Unreadable is null, never empty.</b> An empty competition and an unreadable
        /// file must be distinguishable: the second silently empties the null every experiment
        /// q-value is computed against, and no golden or entrapment gate can see that.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestPass2CompetitionDecoysArtifact()
        {
            // The decoy high bit, as PercolatorEntry sets it. Spelled out here because the
            // artifact under test derives every base_id key from it.
            const uint DECOY_BIT = 0x80000000;
            string dir = Path.Combine(Path.GetTempPath(), "osprey_dcy_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var ascending = new Dictionary<uint, (double score, uint entryId)>();
                var descending = new Dictionary<uint, (double score, uint entryId)>();
                for (uint b = 1; b <= 8; b++)
                    ascending[b] = (0.1 * b, DECOY_BIT | b);
                for (uint b = 8; b >= 1; b--)
                    descending[b] = (0.1 * b, DECOY_BIT | b);

                string a = Path.Combine(dir, "a.bin");
                string d = Path.Combine(dir, "d.bin");
                Pass2CompetitionDecoys.Write(a, ascending, TestStamps.Any);
                Pass2CompetitionDecoys.Write(d, descending, TestStamps.Any);
                CollectionAssert.AreEqual(File.ReadAllBytes(a), File.ReadAllBytes(d),
                    "the artifact must be a function of its contents, not of insertion order");

                var map = Pass2CompetitionDecoys.ReadMap(a);
                Assert.IsNotNull(map);
                Assert.AreEqual(8, map.Count);
                for (uint b = 1; b <= 8; b++)
                {
                    Assert.AreEqual(DECOY_BIT | b, map[b].entryId);
                    Assert.AreEqual(0.1 * b, map[b].score);
                }
                Assert.IsTrue(Pass2CompetitionDecoys.IsCurrentFormat(a));

                // A target entry_id has no decoy bit, so the reader would key it to its own
                // base_id and the file would claim a target observation as a decoy best.
                var target = new Dictionary<uint, (double score, uint entryId)> { { 3u, (0.5, 3u) } };
                Assert.ThrowsException<InvalidOperationException>(
                    () => Pass2CompetitionDecoys.Write(Path.Combine(dir, "t.bin"), target, TestStamps.Any));

                // A key that does not match the entry_id it files means the map was assembled
                // rather than serialized - the one error this format cannot survive.
                var misfiled = new Dictionary<uint, (double score, uint entryId)>
                {
                    { 3u, (0.5, DECOY_BIT | 4u) }
                };
                Assert.ThrowsException<InvalidOperationException>(
                    () => Pass2CompetitionDecoys.Write(Path.Combine(dir, "m.bin"), misfiled, TestStamps.Any));

                // Empty is a real answer and round trips as one - distinct from unreadable.
                string empty = Path.Combine(dir, "empty.bin");
                Pass2CompetitionDecoys.Write(empty,
                    new Dictionary<uint, (double score, uint entryId)>(), TestStamps.Any);
                var emptyMap = Pass2CompetitionDecoys.ReadMap(empty);
                Assert.IsNotNull(emptyMap);
                Assert.AreEqual(0, emptyMap.Count);

                // A corrupted magic byte is a rejection on both entry points, not a decode.
                string foreign = Path.Combine(dir, "foreign.bin");
                var bytes = File.ReadAllBytes(a);
                bytes[5] = (byte)'X';
                File.WriteAllBytes(foreign, bytes);
                Assert.IsFalse(Pass2CompetitionDecoys.IsCurrentFormat(foreign));
                Assert.IsNull(Pass2CompetitionDecoys.ReadMap(foreign));

                // So is a truncated body, which is what a partial write leaves behind.
                string truncated = Path.Combine(dir, "truncated.bin");
                var full = File.ReadAllBytes(a);
                File.WriteAllBytes(truncated, full.Take(full.Length - 1).ToArray());
                Assert.IsFalse(Pass2CompetitionDecoys.IsCurrentFormat(truncated));
                Assert.IsNull(Pass2CompetitionDecoys.ReadMap(truncated));

                // And so is a missing file: null, never an empty map.
                Assert.IsNull(Pass2CompetitionDecoys.ReadMap(Path.Combine(dir, "absent.bin")));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The v5 scope split (issue #4486), covered end to end in one test because the three
        /// behaviours are one design: what the per-file sidecar keeps, what the analysis-wide
        /// experiment sidecar takes over, and the single patch that survived the split.
        ///
        /// <list type="bullet">
        /// <item><b>Experiment round trip.</b> One record per distinct entry_id, written in
        /// ascending entry_id order whatever order the producer accumulated them in - the
        /// straight-through and distributed routes do not agree on that order and the gate
        /// compares their files byte for byte.</item>
        /// <item><b>The collapse is checked, not trusted.</b> Two observations of one entry_id
        /// carrying different experiment values must THROW rather than let one silently win.
        /// The whole split rests on those values being a property of the precursor; a first-wins
        /// collapse over values that had diverged would report q-values no run computed, and
        /// both routes would collapse identically so no self-consistency gate could see it.</item>
        /// <item><b>The two files cannot be read as each other.</b> Different magic, so a
        /// mix-up is a rejection rather than plausible garbage.</item>
        /// <item><b>PatchPep matches a single-phase write byte for byte.</b> The map is
        /// entry_id-keyed and built out of record order, over records with non-sequential
        /// entry_ids, to prove the patch locates each record's pep by entry_id and not by
        /// position.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestFdrScopeSplitV5()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_scope_split_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                AssertExperimentSidecarRoundTrips(dir);
                AssertExperimentCollapseRejectsDisagreement();
                AssertUnreadableExperimentSidecarStopsTheRun(dir);
                AssertSidecarsRejectEachOther(dir);
                AssertPerFileSidecarCarriesNoPep(dir);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Accumulate out of entry_id order, then assert the file comes back in ascending
        /// entry_id order with every column intact.
        /// </summary>
        /// <summary>
        /// A PARTIAL update of an experiment record preserves every field it does not set.
        ///
        /// <para><c>SetProteinQvalue</c> replaces one column by rebuilding the record, so any
        /// field it forgets to carry is silently replaced by whatever the constructor supplies.
        /// PEP was dropped exactly that way on its first day in this record, and no C#-only gate
        /// noticed: the whole Stellar regression stayed green while every experiment winner
        /// reported 1.0. Only the cross-impl comparison against Rust caught it, and that gate is
        /// scheduled for retirement (issue #4486).</para>
        ///
        /// <para>Asserts the WHOLE record rather than the one column, because "the field I
        /// happened to think of" is the bug. The constructor no longer defaults <c>pep</c>, so a
        /// newly added column is a compile error at every construction site - this covers the
        /// case where a site compiles because it passed something wrong instead.</para>
        /// </summary>
        private static void AssertPartialUpdatePreservesEveryField()
        {
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(7, 0.011, 0.012, 1.0, -1.5, 0.25);
            accumulator.SetProteinQvalue(7, 0.0042);

            var updated = accumulator.Records[7];
            Assert.AreEqual(7u, updated.EntryId);
            AssertBitEqual(0.011, updated.ExperimentPrecursorQvalue);
            AssertBitEqual(0.012, updated.ExperimentPeptideQvalue);
            AssertBitEqual(0.0042, updated.ExperimentProteinQvalue);
            AssertBitEqual(-1.5, updated.ExperimentAggregateScore);
            AssertBitEqual(0.25, updated.Pep);
        }

        /// <summary>
        /// The best-of-runs floors are APPLIED to the experiment q-values before the records are
        /// written, and every other column survives the raise.
        ///
        /// <para>This is the whole of issue #4522's fix. The second pass used to write an
        /// experiment q it had not floored - a value more confident than the precursor's own best
        /// run, which downstream then corrected on 1,125,526 rows of the 446-run CHS cohort -
        /// and the correction lived only in the .blib. Applying it here, where both the value and
        /// the floor are in hand, makes the persisted number the final one.</para>
        ///
        /// <para>Asserted as MAX, not as assignment: a floor below the competed q must leave it
        /// alone. The two q-values floor against differently-keyed minima, so they are moved
        /// independently - a single entry can have its precursor q raised and its peptide q
        /// untouched, which is the case a one-value test would miss.</para>
        /// </summary>
        private static void AssertRunQFloorsAreAppliedNotStored()
        {
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(1, 0.001, 0.002, 0.5, -1.0, 0.1);   // both floors bite
            accumulator.Add(2, 0.030, 0.040, 0.6, -2.0, 0.2);   // neither floor bites
            accumulator.Add(3, 0.004, 0.050, 0.7, -3.0, 0.3);   // only the precursor floor bites
            accumulator.Add(4, 0.005, 0.006, 0.8, -4.0, 0.4);   // floors unknown

            var floors = new Dictionary<uint, (double Entry, double Peptide)>
            {
                { 1, (0.010, 0.020) },
                { 2, (0.010, 0.040) },
                { 3, (0.010, 0.001) },
                { 4, (double.NaN, double.NaN) },
            };
            int raised = accumulator.ApplyRunQFloors(id => floors[id]);

            // 1 raises both, 3 raises one. Nothing else moves. The count is of RECORDS, so entry
            // 1 counts once although both of its q-values moved.
            Assert.AreEqual(2, raised);
            AssertBitEqual(0.010, accumulator.Records[1].ExperimentPrecursorQvalue);
            AssertBitEqual(0.020, accumulator.Records[1].ExperimentPeptideQvalue);
            // A floor EQUAL to the value is not a raise, and one below it is not a ceiling.
            AssertBitEqual(0.030, accumulator.Records[2].ExperimentPrecursorQvalue);
            AssertBitEqual(0.040, accumulator.Records[2].ExperimentPeptideQvalue);
            AssertBitEqual(0.010, accumulator.Records[3].ExperimentPrecursorQvalue);
            AssertBitEqual(0.050, accumulator.Records[3].ExperimentPeptideQvalue);
            // NaN means "not known" and must raise nothing - every comparison against it is
            // false, so this is a statement of intent rather than a guard the arithmetic needs.
            AssertBitEqual(0.005, accumulator.Records[4].ExperimentPrecursorQvalue);
            AssertBitEqual(0.006, accumulator.Records[4].ExperimentPeptideQvalue);

            // The columns the raise does not own, on a record it DID rewrite.
            AssertBitEqual(0.5, accumulator.Records[1].ExperimentProteinQvalue);
            AssertBitEqual(-1.0, accumulator.Records[1].ExperimentAggregateScore);
            AssertBitEqual(0.1, accumulator.Records[1].Pep);
            Assert.AreEqual(4, accumulator.Count);
        }

        private static void AssertExperimentSidecarRoundTrips(string dir)
        {
            AssertPartialUpdatePreservesEveryField();
            AssertRunQFloorsAreAppliedNotStored();
            string path = Path.Combine(dir, "analysis.1st-pass.fdr_experiment.bin");
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(7, 0.011, 0.012, 0.013, -1.5, 1.0);
            accumulator.Add(2, 0.021, 0.022, 0.023, -2.5, 1.0);
            accumulator.Add(9, 0.031, 0.032, 0.033, -3.5, 1.0);
            // A repeat sighting of an entry_id already held, with identical values: the ordinary
            // case, since every observation of a precursor carries the same experiment values.
            accumulator.Add(2, 0.021, 0.022, 0.023, -2.5, 1.0);
            Assert.AreEqual(3, accumulator.Count);

            FdrExperimentSidecar.Write(path, accumulator.Records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);
            Assert.AreEqual(
                FdrExperimentSidecar.HeaderLength + 3 * FdrExperimentSidecar.RecordLength +
                BinarySidecarStamp.Encode(TestStamps.Any).Length,
                new FileInfo(path).Length);
            Assert.IsTrue(FdrExperimentSidecar.IsCurrentFormat(path, FdrScoresSidecar.Pass.FirstPass));

            var order = new List<uint>();
            Assert.IsTrue(FdrExperimentSidecar.ReadRecords(
                path, FdrScoresSidecar.Pass.FirstPass, rec => order.Add(rec.EntryId)));
            CollectionAssert.AreEqual(new List<uint> { 2, 7, 9 }, order);

            var map = FdrExperimentSidecar.ReadMap(path, FdrScoresSidecar.Pass.FirstPass);
            Assert.IsNotNull(map);
            AssertBitEqual(0.031, map[9].ExperimentPrecursorQvalue);
            AssertBitEqual(0.032, map[9].ExperimentPeptideQvalue);
            AssertBitEqual(0.033, map[9].ExperimentProteinQvalue);
            AssertBitEqual(-3.5, map[9].ExperimentAggregateScore);

            // SetProteinQvalue replaces only the column protein FDR owns, and ignores an
            // entry_id the accumulator never saw rather than inventing a record for it.
            accumulator.SetProteinQvalue(9, 0.5);
            accumulator.SetProteinQvalue(12345, 0.5);
            Assert.AreEqual(3, accumulator.Count);
            AssertBitEqual(0.5, accumulator.Records[9].ExperimentProteinQvalue);
            AssertBitEqual(0.031, accumulator.Records[9].ExperimentPrecursorQvalue);
        }

        /// <summary>
        /// Absence and unreadability are DIFFERENT answers, and only absence is tolerable.
        ///
        /// <para>A sidecar the analysis never wrote reads as an empty map, because the callers'
        /// defaults cover it. One that EXISTS and cannot be read stops the run: every consumer
        /// applies these values through a <c>TryGetValue</c>, so an empty map is
        /// indistinguishable from one that simply holds no matching entry, and every entry then
        /// keeps its <c>ResetScores</c> defaults - which the run reports as computed values. A
        /// valid file is read back FIRST as the negative control, because a throws-on-corrupt
        /// assertion fed only corrupt input proves nothing about the case it must let through.
        /// </para>
        /// </summary>
        private static void AssertUnreadableExperimentSidecarStopsTheRun(string dir)
        {
            const FdrScoresSidecar.Pass pass = FdrScoresSidecar.Pass.SecondPass;

            // Nothing named and nothing on disk: the analysis has none, which is not an error.
            Assert.AreEqual(0, Pass2FdrSidecar.LoadExperimentRecordsFrom(null, pass).Count);
            Assert.AreEqual(0, Pass2FdrSidecar.LoadExperimentRecordsFrom(string.Empty, pass).Count);
            Assert.AreEqual(0, Pass2FdrSidecar.LoadExperimentRecordsFrom(
                Path.Combine(dir, "never-written.2nd-pass.fdr_experiment.bin"), pass).Count);

            // Control: a valid file of the expected pass reads back with its records.
            string path = Path.Combine(dir, "unreadable-probe.2nd-pass.fdr_experiment.bin");
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(4, 0.041, 0.042, 0.043, -4.5, 1.0);
            accumulator.Add(5, 0.051, 0.052, 0.053, -5.5, 1.0);
            FdrExperimentSidecar.Write(path, accumulator.Records, pass, TestStamps.Any);
            Assert.AreEqual(2, Pass2FdrSidecar.LoadExperimentRecordsFrom(path, pass).Count);

            // The same file asked for as the OTHER pass. It is present and it is not what the
            // caller needs, which is the shape a stale or mis-wired artifact arrives in.
            Assert.ThrowsException<InvalidOperationException>(
                () => Pass2FdrSidecar.LoadExperimentRecordsFrom(path, FdrScoresSidecar.Pass.FirstPass));

            // Truncated mid-record, with a header whose count still claims both records - the
            // shape a killed writer leaves behind.
            string truncated = Path.Combine(dir, "truncated.2nd-pass.fdr_experiment.bin");
            var bytes = File.ReadAllBytes(path);
            var shortened = new byte[bytes.Length - 8];
            Array.Copy(bytes, shortened, shortened.Length);
            File.WriteAllBytes(truncated, shortened);
            Assert.ThrowsException<InvalidOperationException>(
                () => Pass2FdrSidecar.LoadExperimentRecordsFrom(truncated, pass));
        }

        /// <summary>
        /// A second observation of one entry_id carrying a DIFFERENT experiment value must
        /// throw. See the class remarks on <see cref="FdrExperimentAccumulator"/>: this is the
        /// assertion that keeps the per-entry_id collapse honest.
        /// </summary>
        private static void AssertExperimentCollapseRejectsDisagreement()
        {
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(4, 0.01, 0.02, 0.03, -1.0, 1.0);
            try
            {
                accumulator.Add(4, 0.01, 0.02, 0.03, -1.25, 1.0);
                Assert.Fail("Disagreeing experiment values for one entry_id must throw.");
            }
            catch (InvalidOperationException)
            {
                // Expected: the collapse refuses to pick a winner.
            }
        }

        /// <summary>
        /// The per-file and experiment sidecars share a header shape and a pass byte, so each
        /// reader must refuse the other's file on magic alone.
        /// </summary>
        private static void AssertSidecarsRejectEachOther(string dir)
        {
            string perFile = Path.Combine(dir, "run.1st-pass.fdr_scores.bin");
            string experiment = Path.Combine(dir, "mix.1st-pass.fdr_experiment.bin");
            FdrScoresSidecar.Write(perFile,
                new List<FdrEntry> { MakeFdrEntry(1, -1.0, 0.01, 0.02) },
                FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);
            var accumulator = new FdrExperimentAccumulator();
            accumulator.Add(1, 0.01, 0.02, 0.03, -1.0, 1.0);
            FdrExperimentSidecar.Write(experiment, accumulator.Records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

            Assert.IsFalse(FdrExperimentSidecar.IsCurrentFormat(perFile, FdrScoresSidecar.Pass.FirstPass));
            Assert.IsNull(FdrExperimentSidecar.ReadMap(perFile, FdrScoresSidecar.Pass.FirstPass));
            Assert.IsFalse(FdrScoresSidecar.IsCurrentFormat(experiment, FdrScoresSidecar.Pass.FirstPass));
            Assert.IsFalse(FdrScoresSidecar.ReadRecords(
                experiment, FdrScoresSidecar.Pass.FirstPass, rec => { }));
        }

        /// <summary>
        /// A per-file sidecar carries NO PEP column, on either pass, and a second write of the
        /// same records reproduces the file byte for byte (issue #4486).
        ///
        /// <para>This replaces a test that asserted a placeholder write plus <c>PatchPep</c>
        /// equalled a single-phase write. That test passed for as long as the patch existed, and
        /// the patch is exactly what it should have been questioning: PEP is one value per
        /// base_id, so writing it per observation forced the second pass to reopen and rewrite
        /// every per-run sidecar after the experiment fold. Those files are now written once and
        /// never revisited, and PEP lives on <see cref="FdrExperimentRecord.Pep"/>.</para>
        /// </summary>
        private static void AssertPerFileSidecarCarriesNoPep(string dir)
        {
            string first = Path.Combine(dir, "a.2nd-pass.fdr_scores.bin");
            string second = Path.Combine(dir, "b.2nd-pass.fdr_scores.bin");
            // Non-sequential entry_ids, so a positional read cannot pass for a keyed one.
            var records = new List<FdrScoreRecord>
            {
                new FdrScoreRecord(3, -2.0, 0.01, 0.02, 31.5),
                new FdrScoreRecord(77, -1.0, 0.03, 0.04, 42.25),
            };
            FdrScoresSidecar.Write(first, records, FdrScoresSidecar.Pass.SecondPass, TestStamps.Any);
            FdrScoresSidecar.Write(second, records, FdrScoresSidecar.Pass.SecondPass, TestStamps.Any);
            CollectionAssert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(second));

            // The record is exactly entry_id + score + the two RUN q-values + the apex RT.
            // Every one of those is RUN-scope and per-observation; an experiment-scope column
            // reappearing here would widen it, and that is what this pins.
            Assert.AreEqual(sizeof(uint) + 4 * sizeof(double), FdrScoresSidecar.RecordLength);
            Assert.AreEqual(FdrScoresSidecar.HeaderLength + records.Count * FdrScoresSidecar.RecordLength +
                            BinarySidecarStamp.Encode(TestStamps.Any).Length,
                new FileInfo(first).Length);

            var read = new List<FdrScoreRecord>();
            Assert.IsTrue(FdrScoresSidecar.ReadRecords(
                first, FdrScoresSidecar.Pass.SecondPass, rec => read.Add(rec)));
            Assert.AreEqual(2, read.Count);
            AssertBitEqual(-2.0, read[0].Score);
            AssertBitEqual(0.03, read[1].RunPrecursorQvalue);
            // Round-tripped, and per record: the apex RT is the v7 column, and it is the only
            // one whose writer and reader were added at the same time - so nothing else in this
            // file would notice if the two disagreed on its offset.
            AssertBitEqual(31.5, read[0].ApexRt);
            AssertBitEqual(42.25, read[1].ApexRt);

            // WRITE-ONCE. Rewriting a sidecar inside one run is the defect class this whole
            // change exists to remove: the file no longer matches the stamp first written with
            // it, and a separate experiment-wide node has only what the per-file node
            // left behind. Asserted here rather than trusted to the contract, because the
            // contract WAS stated - in a commit title - and drifted for a sprint regardless.
            Assert.ThrowsException<InvalidOperationException>(
                () => FdrScoresSidecar.Write(first, records, FdrScoresSidecar.Pass.SecondPass, TestStamps.Any));
        }

        /// <summary>Bit-exact double comparison, so a rounded value cannot pass.</summary>
        private static void AssertBitEqual(double expected, double actual)
        {
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected),
                            BitConverter.DoubleToInt64Bits(actual));
        }


        /// <summary>
        /// Pre-v2 sidecar files (no magic header, just raw f64 scores)
        /// must be rejected by the v2 reader.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarV1FormatRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_v1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.1st-pass.fdr_scores.bin");
                using (var fs = new FileStream(path, FileMode.Create))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(0.1);
                    bw.Write(0.2);
                    bw.Write(0.3);
                }

                var entries = new List<FdrEntry>
                {
                    MakeFdrEntry(0, 0.0, 0.0, 0.0),
                    MakeFdrEntry(1, 0.0, 0.0, 0.0),
                    MakeFdrEntry(2, 0.0, 0.0, 0.0),
                };
                Assert.IsFalse(FdrScoresSidecar.TryRead(path, entries, FdrScoresSidecar.Pass.FirstPass));
                foreach (var e in entries)
                    Assert.AreEqual(0.0, e.Score);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Caller may pass a SUPERSET of the sidecar's entries — a real
        /// case for --task SecondPassFDR stage 7 entry where the reconciled
        /// parquet has gap-fill stubs the 1st-pass sidecar (written
        /// pre-gap-fill) does not. Sidecar records overlay onto matching
        /// entry_ids; entries with no matching record keep their default
        /// (Score=0, q=1) values. Reader must accept and overlay only
        /// the matched records.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarSupersetEntries()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_super_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.1st-pass.fdr_scores.bin");
                FdrScoresSidecar.Write(path,
                    new List<FdrEntry> { MakeFdrEntry(0, -3.5, 0.001, 0.02) },
                    FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                // entry_id=99 is the gap-fill stub (no sidecar record).
                var entries = new List<FdrEntry>
                {
                    MakeFdrEntry(0, 0.0, 0.0, 0.0),
                    MakeFdrEntry(99, 0.0, 0.0, 0.0),
                };
                Assert.IsTrue(FdrScoresSidecar.TryRead(path, entries, FdrScoresSidecar.Pass.FirstPass));
                Assert.AreEqual(-3.5, entries[0].Score, 0.0);
                Assert.AreEqual(0.001, entries[0].RunPrecursorQvalue, 0.0);
                // Gap-fill stub at index 1: untouched (no sidecar record
                // for entry_id=99). MakeFdrEntry's q=0.0 placeholder
                // survives unchanged.
                Assert.AreEqual(0.0, entries[1].Score, 0.0);
                Assert.AreEqual(0.0, entries[1].RunPrecursorQvalue, 0.0);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Chunk-boundary coverage for the buffered body reads. Both
        /// <see cref="FdrScoresSidecar.TryRead(string, IList{FdrEntry}, FdrScoresSidecar.Pass)"/>
        /// and <see cref="FdrScoresSidecar.TryReadOverlay"/> now walk the body in fixed-size
        /// buffers instead of materialising the file, which gives the record loop seams the
        /// old whole-file indexing did not have: a count that is an exact multiple of the
        /// buffer, one either side of it, and one that leaves a short final chunk.
        ///
        /// <para>Every other sidecar test in this file writes a handful of records, so all of
        /// them would pass against a reader that dropped, duplicated or misaligned every
        /// record past the first buffer. That is the whole reason this one exists.</para>
        ///
        /// <para>The counts bracket a range of plausible buffer sizes rather than naming the
        /// private constant, so the test keeps its meaning if the buffer is ever retuned.</para>
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarChunkBoundaries()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_chunk_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                foreach (int count in new[] { 0, 1, 1023, 1024, 1025, 2047, 2048, 2049, 4096, 4103 })
                {
                    // A distinct path per count: FdrScoresSidecar refuses to write the same
                    // path twice in one process, which is the guard that keeps a sidecar
                    // write-once (P11).
                    string path = Path.Combine(dir, "n" + count + ".1st-pass.fdr_scores.bin");
                    var written = new List<FdrEntry>(count);
                    for (int i = 0; i < count; i++)
                        written.Add(MakeFdrEntry((uint)i, -i * 0.5, i * 1.0e-6, 0.0));
                    FdrScoresSidecar.Write(path, written, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                    var loaded = new List<FdrEntry>(count);
                    for (int i = 0; i < count; i++)
                        loaded.Add(MakeFdrEntry((uint)i, 0.0, 0.0, 0.0));
                    Assert.IsTrue(FdrScoresSidecar.TryRead(path, loaded, FdrScoresSidecar.Pass.FirstPass),
                        "TryRead rejected a " + count + "-record sidecar");
                    // Exact equality (delta 0.0): the assert recomputes the same expressions
                    // MakeFdrEntry used, so anything but a bit-identical round trip is a
                    // misaligned read rather than arithmetic drift.
                    for (int i = 0; i < count; i++)
                    {
                        Assert.AreEqual(-i * 0.5, loaded[i].Score, 0.0,
                            "Score at record " + i + " of " + count);
                        Assert.AreEqual(i * 1.0e-6, loaded[i].RunPrecursorQvalue, 0.0,
                            "RunPrecursorQvalue at record " + i + " of " + count);
                        Assert.AreEqual(i * 1.0e-6 + 1.0e-9, loaded[i].RunPeptideQvalue, 0.0,
                            "RunPeptideQvalue at record " + i + " of " + count);
                    }

                    var byId = new Dictionary<uint, FdrEntry>();
                    for (int i = 0; i < count; i++)
                        byId[(uint)i] = MakeFdrEntry((uint)i, 0.0, 0.0, 0.0);
                    Assert.IsTrue(
                        FdrScoresSidecar.TryReadOverlay(path, byId, FdrScoresSidecar.Pass.FirstPass),
                        "TryReadOverlay rejected a " + count + "-record sidecar");
                    for (int i = 0; i < count; i++)
                    {
                        Assert.AreEqual(-i * 0.5, byId[(uint)i].Score, 0.0,
                            "Overlay Score at record " + i + " of " + count);
                    }
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// If a sidecar record's entry_id has no match in the caller's
        /// stub list, the reader must refuse rather than silently dropping
        /// the record. Detects "sidecar from a different parquet" and
        /// "sidecar from a different binary version" corruption.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarStaleRecordRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_stale_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.1st-pass.fdr_scores.bin");
                // Sidecar record for entry_id=0, but the caller's stubs
                // don't contain entry_id=0 — only entry_id=42.
                FdrScoresSidecar.Write(path,
                    new List<FdrEntry> { MakeFdrEntry(0, -3.5, 0.001, 0.02) },
                    FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                var unrelated = new List<FdrEntry> { MakeFdrEntry(42, 0.0, 0.0, 0.0) };
                Assert.IsFalse(FdrScoresSidecar.TryRead(path, unrelated, FdrScoresSidecar.Pass.FirstPass));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A corrupt or malicious sidecar with a huge headerCount would
        /// otherwise wrap int when computing
        /// <c>HeaderLength + headerCount * RecordLength</c> and let the
        /// size check pass spuriously, leading to out-of-bounds reads in
        /// the record loop. Both <see cref="FdrScoresSidecar.TryRead(string, IList{FdrEntry}, FdrScoresSidecar.Pass)"/>
        /// and <see cref="FdrScoresSidecar.TryReadOverlay"/> must reject
        /// the load via the checked-arithmetic guard.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarOversizedHeaderRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_oversized_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.1st-pass.fdr_scores.bin");
                // Write a valid 1-entry sidecar to get the right magic /
                // version / pass byte layout, then truncate to header-only
                // and overwrite headerCount with ulong.MaxValue.
                FdrScoresSidecar.Write(path,
                    new List<FdrEntry> { MakeFdrEntry(0, 0.0, 0.0, 0.0) },
                    FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);
                byte[] header = File.ReadAllBytes(path);
                Array.Resize(ref header, FdrScoresSidecar.HeaderLength);
                BitConverter.GetBytes(ulong.MaxValue).CopyTo(header, 16);
                File.WriteAllBytes(path, header);

                var entries = new List<FdrEntry> { MakeFdrEntry(0, 0.0, 0.0, 0.0) };
                Assert.IsFalse(FdrScoresSidecar.TryRead(path, entries, FdrScoresSidecar.Pass.FirstPass));

                var dict = new Dictionary<uint, FdrEntry> { { 0, entries[0] } };
                Assert.IsFalse(FdrScoresSidecar.TryReadOverlay(path, dict, FdrScoresSidecar.Pass.FirstPass));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// The v9 first-pass sidecar's feature sums - the run's share of the model diagnostics
        /// Model tab - round-trip exactly beside unchanged records; a sidecar written without
        /// them reads as having none; and a share whose counts are corrupt reads as none rather
        /// than throwing, which would abort a resume or a report fold.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarFeatureSums()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_sums_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var sums = new FeatureSums(2, 3) { TargetCount = 5, DecoyCount = 4 };
                sums.TargetSums[0] = 1.25;
                sums.TargetSums[1] = -0.1;
                sums.DecoySums[0] = double.NaN;
                sums.DecoySums[1] = 3e-300;
                sums.TargetHistograms[1][2] = 7;
                sums.DecoyHistograms[0][0] = 2;
                var records = new List<FdrScoreRecord> { new FdrScoreRecord(3, 0.5, 0.01, 0.02, 12.5) };

                string path = Path.Combine(dir, "with.1st-pass.fdr_scores.bin");
                FdrScoresSidecar.Write(path, records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any, sums);
                var read = FdrScoresSidecar.ReadFeatureSums(path);
                Assert.IsNotNull(read);
                Assert.AreEqual(sums.TargetCount, read.TargetCount);
                Assert.AreEqual(sums.DecoyCount, read.DecoyCount);
                for (int j = 0; j < 2; j++)
                {
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(sums.TargetSums[j]), BitConverter.DoubleToInt64Bits(read.TargetSums[j]));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(sums.DecoySums[j]), BitConverter.DoubleToInt64Bits(read.DecoySums[j]));
                    CollectionAssert.AreEqual(sums.TargetHistograms[j], read.TargetHistograms[j]);
                    CollectionAssert.AreEqual(sums.DecoyHistograms[j], read.DecoyHistograms[j]);
                }
                var readRecords = new List<FdrScoreRecord>();
                Assert.IsTrue(FdrScoresSidecar.ReadRecords(path, FdrScoresSidecar.Pass.FirstPass, readRecords.Add));
                Assert.AreEqual(1, readRecords.Count);
                Assert.AreEqual(3u, readRecords[0].EntryId);
                Assert.AreEqual(TestStamps.Any.ToString(), FdrScoresSidecar.ReadStamp(path, FdrScoresSidecar.Pass.FirstPass)?.ToString());

                string without = Path.Combine(dir, "without.1st-pass.fdr_scores.bin");
                FdrScoresSidecar.Write(without, records, FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);
                Assert.IsNull(FdrScoresSidecar.ReadFeatureSums(without));

                // Feature and bin counts large enough that the implied length overflows an int.
                byte[] data = File.ReadAllBytes(path);
                int sumsOffset = FdrScoresSidecar.HeaderLength + FdrScoresSidecar.RecordLength;
                BitConverter.GetBytes(20000u).CopyTo(data, sumsOffset);
                BitConverter.GetBytes(20000u).CopyTo(data, sumsOffset + 4);
                File.WriteAllBytes(path, data);
                Assert.IsNull(FdrScoresSidecar.ReadFeatureSums(path));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A 1st-pass sidecar must NOT load into a TryRead call that
        /// expects 2nd-pass (and vice versa) — would otherwise scramble
        /// q-values silently because the records are positionally
        /// compatible but semantically different.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarPassMismatchRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_pm_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "test.fdr_scores.bin");
                FdrScoresSidecar.Write(path,
                    new List<FdrEntry>
                    {
                        MakeFdrEntry(0, -3.5, 0.001, 0.02),
                        MakeFdrEntry(1, -2.1, 0.005, 0.04),
                    },
                    FdrScoresSidecar.Pass.FirstPass, TestStamps.Any);

                var stubs = new List<FdrEntry>
                {
                    MakeFdrEntry(0, 0.0, 0.0, 0.0),
                    MakeFdrEntry(1, 0.0, 0.0, 0.0),
                };
                Assert.IsFalse(FdrScoresSidecar.TryRead(path, stubs, FdrScoresSidecar.Pass.SecondPass));
                foreach (var s in stubs)
                    Assert.AreEqual(0.0, s.Score);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        #endregion

        #region ReconciliationFile Tests

        private static ReconciliationFile MakeSampleReconciliationFile()
        {
            return new ReconciliationFile
            {
                ForcedIntegrationActions = new List<ForcedIntegrationEntry>
                {
                    new ForcedIntegrationEntry { EntryId = 200, ExpectedRt = 41.125, HalfWidth = 0.075 },
                    new ForcedIntegrationEntry { EntryId = 201, ExpectedRt = 18.5,   HalfWidth = 0.05  },
                },
                FormatVersion = ReconciliationFile.CurrentFormatVersion,
                GapFillTargets = new List<GapFillEntry>
                {
                    new GapFillEntry
                    {
                        Charge = 2,
                        DecoyEntryId = 0x80000003u,
                        ExpectedRt = 33.5,
                        HalfWidth = 0.08,
                        ModifiedSequence = "PEPTIDE",
                        TargetEntryId = 3,
                    },
                },
                RefinedRtCalibration = new RefinedRtCalibrationJson
                {
                    AbsResiduals = new[] { 0.01, 0.02, 0.015 },
                    FittedRts    = new[] { 10.5, 20.5, 30.5 },
                    LibraryRts   = new[] { 10.0, 20.0, 30.0 },
                    ResidualSd   = 0.123,
                },
                UseCwtPeakActions = new List<UseCwtPeakEntry>
                {
                    new UseCwtPeakEntry { ApexRt = 23.45, CandidateIdx = 1, EndRt = 23.80, EntryId = 100, StartRt = 23.10 },
                    new UseCwtPeakEntry { ApexRt = 8.07,  CandidateIdx = 0, EndRt = 8.20,  EntryId = 101, StartRt = 7.95  },
                },
            };
        }

        /// <summary>
        /// Round-trip: save a sample reconciliation file, reload it, and
        /// verify every field survives bit-for-bit. Also exposes the
        /// cross-impl byte-parity hook so the same hardcoded sample can
        /// be byte-compared against the Rust sibling test's output.
        /// </summary>
        [TestMethod]
        public void TestReconciliationFileRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "recon_rt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "round_trip.reconciliation.json");
                var file = MakeSampleReconciliationFile();

                ReconciliationFile.Save(path, file, TestStamps.Any);

                if (!string.IsNullOrEmpty(OspreyEnvironment.CrossImplReconciliationOut))
                    File.Copy(path, OspreyEnvironment.CrossImplReconciliationOut, overwrite: true);

                var parsed = ReconciliationFile.Load(path);

                Assert.AreEqual(file.FormatVersion, parsed.FormatVersion);
                Assert.AreEqual(file.UseCwtPeakActions.Count, parsed.UseCwtPeakActions.Count);
                Assert.AreEqual(file.ForcedIntegrationActions.Count, parsed.ForcedIntegrationActions.Count);
                Assert.AreEqual(file.GapFillTargets.Count, parsed.GapFillTargets.Count);

                // Spot-check bit-exact f64 round-trip on a non-trivial value.
                var origCwt = file.UseCwtPeakActions[0];
                var gotCwt = parsed.UseCwtPeakActions[0];
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(origCwt.ApexRt),
                                BitConverter.DoubleToInt64Bits(gotCwt.ApexRt));
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(origCwt.StartRt),
                                BitConverter.DoubleToInt64Bits(gotCwt.StartRt));
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(origCwt.EndRt),
                                BitConverter.DoubleToInt64Bits(gotCwt.EndRt));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A reconciliation file with an unsupported format_version must
        /// be rejected with a clear error rather than silently parsed.
        /// </summary>
        [TestMethod]
        public void TestReconciliationFileFormatVersionMismatchRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "recon_ver_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "bad_version.reconciliation.json");
                File.WriteAllText(path,
                    "{\n" +
                    "  \"forced_integration_actions\": [],\n" +
                    "  \"format_version\": 99,\n" +
                    "  \"gap_fill_targets\": [],\n" +
                    "  \"library_hash\": \"x\",\n" +
                    "  \"refined_rt_calibration\": null,\n" +
                    "  \"search_hash\": \"y\",\n" +
                    "  \"use_cwt_peak_actions\": []\n" +
                    "}\n");

                try
                {
                    ReconciliationFile.Load(path);
                    Assert.Fail("expected InvalidDataException");
                }
                catch (InvalidDataException ex)
                {
                    Assert.AreEqual(string.Format(
                        OspreyIOResources.ReconciliationFile_Load_Reconciliation_file__0__has_unsupported_format_version__1___expected__2____Delete_this_,
                        path, 99, ReconciliationFile.CurrentFormatVersion, "format_version",
                        "*" + ReconciliationFile.EXT), ex.Message);
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Mirror of the Rust regression test
        /// <c>reconciliation_io::tests::serde_json_f64_roundtrip_is_bit_exact</c>.
        /// On the Rust side, serde_json's default f64 parser is best-effort
        /// and loses 1 ULP on some shortest-roundtrip strings (e.g.,
        /// "3.1575921556296254" parses to 0x...878 instead of the correct
        /// 0x...877). The fix on the Rust side is the `float_roundtrip`
        /// feature flag on serde_json.
        ///
        /// .NET's <see cref="JsonTextReader"/> for <see cref="JsonToken.Float"/>
        /// uses <c>double.Parse</c> internally, which has been correctly
        /// rounded since Core 3.0. This test asserts that the same six
        /// known-tricky values round-trip bit-exactly through Newtonsoft so
        /// the C# Stage-N JSON path inherits the same byte-parity invariant
        /// the Rust side now enforces. If a future Newtonsoft upgrade
        /// regresses the f64 parser, this test surfaces it before any
        /// cross-impl harness diff is taken.
        /// </summary>
        [TestMethod]
        public void TestNewtonsoftJsonF64RoundtripIsBitExact()
        {
            double[] candidates =
            {
                3.1575921556296254,
                3.1585537473115846,
                3.1741410573166426,
                BitConverter.Int64BitsToDouble(0x0010_0000_0000_0000L), // smallest normal
                1.0,
                0.0123,
            };
            foreach (var v in candidates)
            {
                string s = Diagnostics.FormatF64Roundtrip(v);

                // Path 1: low-level JsonTextReader, the underlying f64
                // parser used by every Newtonsoft deserialization path.
                // FormatF64Roundtrip emits whole numbers as "1" (no
                // decimal point), so the token type can be Integer or
                // Float depending on the value -- both are valid sources
                // of an f64 per JSON spec.
                using (var sr = new StringReader(s))
                using (var reader = new JsonTextReader(sr))
                {
                    Assert.IsTrue(reader.Read(),
                        "JsonTextReader.Read returned false for {0}", s);
                    Assert.IsTrue(reader.TokenType == JsonToken.Float ||
                                  reader.TokenType == JsonToken.Integer,
                        "Expected numeric token for {0}, got {1}", s, reader.TokenType);
                    double parsed = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(v),
                                    BitConverter.DoubleToInt64Bits(parsed),
                        "JsonTextReader lost bits: input={0} ({1:X16}) -> str={2} -> parsed={3} ({4:X16})",
                        v,
                        BitConverter.DoubleToInt64Bits(v),
                        s,
                        parsed,
                        BitConverter.DoubleToInt64Bits(parsed));
                }

                // Path 2: JsonConvert.DeserializeObject, the high-level
                // path ReconciliationFile.Load goes through.
                double parsedHi = JsonConvert.DeserializeObject<double>(s);
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(v),
                                BitConverter.DoubleToInt64Bits(parsedHi),
                    "JsonConvert.DeserializeObject lost bits: input={0} ({1:X16}) -> str={2} -> parsed={3} ({4:X16})",
                    v,
                    BitConverter.DoubleToInt64Bits(v),
                    s,
                    parsedHi,
                    BitConverter.DoubleToInt64Bits(parsedHi));
            }
        }

        #endregion

        #region ArtifactStamp Tests

        /// <summary>
        /// The validity stamp every pipeline artifact embeds in itself (replacing the
        /// <c>.osprey.task</c> file that used to sit beside it). Covers the serialized form, the
        /// "may this run reuse it" rule, and that every artifact family round-trips its stamp
        /// through its own slot - and that a damaged or unstamped artifact reads as "no stamp"
        /// rather than throwing, because "I cannot tell" has to resolve to recomputing.
        /// </summary>
        [TestMethod]
        public void TestArtifactStampEmbedding()
        {
            string dir = Path.Combine(Path.GetTempPath(), "artifact_stamp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // A key carrying every character that could confuse the serialized form or a
                // JSON / HTML slot: the field separators, quotes, backslashes, control characters.
                const string key = "search=abc;library=def;path=C:\\proj\\ai;q=\"x\";ctrl=\b\f\n\r\t\u0001";
                var stamp = OspreyVersion.Stamp(PerFileScoringTask.TASK_NAME, key);
                AssertStampSerialization(stamp, key);
                AssertStampCurrency(stamp, key);
                AssertBinaryStamps(dir, stamp);
                AssertTextStamps(dir, stamp);
                AssertParquetAndBlibStamps(dir, stamp);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        private static void AssertStampSerialization(ArtifactStamp stamp, string key)
        {
            var parsed = ArtifactStamp.Parse(stamp.ToString());
            Assert.IsNotNull(parsed);
            Assert.AreEqual(stamp.Task, parsed.Task);
            Assert.AreEqual(stamp.Version, parsed.Version);
            Assert.AreEqual(key, parsed.Key);
            foreach (string garbage in new[]
                     {
                         null, string.Empty, "not a stamp",
                         ArtifactStamp.PREFIX + ";task=T;version=V",          // no key field
                         ArtifactStamp.PREFIX + ";version=V;task=T;key=K",    // fields out of order
                         ArtifactStamp.PREFIX + ";task=;version=V;key=K",     // empty task
                         "osprey-validity/2;task=T;version=V;key=K",          // unknown layout
                     })
            {
                Assert.IsNull(ArtifactStamp.Parse(garbage), garbage);
            }
        }

        private static void AssertStampCurrency(ArtifactStamp stamp, string key)
        {
            Assert.IsTrue(stamp.IsCurrent(PerFileScoringTask.TASK_NAME, key));
            Assert.IsFalse(stamp.IsCurrent(PerFileRescoreTask.TASK_NAME, key));
            Assert.IsFalse(stamp.IsCurrent(PerFileScoringTask.TASK_NAME, key + "_modified"));
            // Another build's artifact is never current: a version change invalidates.
            var otherBuild = new ArtifactStamp(PerFileScoringTask.TASK_NAME, OspreyVersion.Current + ".1", key);
            Assert.IsFalse(otherBuild.IsCurrent(PerFileScoringTask.TASK_NAME, key));
        }

        private static void AssertBinaryStamps(string dir, ArtifactStamp stamp)
        {
            var records = new List<FdrScoreRecord> { new FdrScoreRecord(7, 1.5, 0.01, 0.02, 12.5) };
            string pass1 = Path.Combine(dir, "run.1st-pass.fdr_scores.bin");
            string pass2 = Path.Combine(dir, "run.2nd-pass.fdr_scores.bin");
            FdrScoresSidecar.Write(pass1, records, FdrScoresSidecar.Pass.FirstPass, stamp);
            FdrScoresSidecar.Write(pass2, records, FdrScoresSidecar.Pass.SecondPass, stamp);
            AssertStampRead(pass1, stamp);
            AssertStampRead(pass2, stamp);
            // The records still read at their fixed offsets with the stamp after them.
            var read = new List<FdrScoreRecord>();
            Assert.IsTrue(FdrScoresSidecar.ReadRecords(pass1, FdrScoresSidecar.Pass.FirstPass, read.Add));
            Assert.AreEqual(1, read.Count);
            Assert.AreEqual(12.5, read[0].ApexRt);

            string decoys = Path.Combine(dir, "run.2nd-pass" + Pass2CompetitionDecoys.EXT);
            Pass2CompetitionDecoys.Write(decoys,
                new Dictionary<uint, (double score, uint entryId)> { { 7u, (2.5, 0x80000007u) } }, stamp);
            AssertStampRead(decoys, stamp);
            Assert.AreEqual(1, Pass2CompetitionDecoys.ReadMap(decoys).Count);

            string experiment = Path.Combine(dir, "out.1st-pass" + FdrExperimentSidecar.EXT);
            FdrExperimentSidecar.Write(experiment,
                new Dictionary<uint, FdrExperimentRecord> { { 7u, new FdrExperimentRecord(7, 0.1, 0.2, 0.3, 4.0, 0.5) } },
                FdrScoresSidecar.Pass.FirstPass, stamp);
            AssertStampRead(experiment, stamp);
            Assert.AreEqual(1, FdrExperimentSidecar.ReadMap(experiment, FdrScoresSidecar.Pass.FirstPass).Count);

            string retained = Path.Combine(dir, "out.1st-pass" + RetainedBaseIdSidecar.EXT);
            RetainedBaseIdSidecar.Write(retained, new[] { 3u, 1u, 2u }, stamp);
            AssertStampRead(retained, stamp);
            Assert.AreEqual(3, RetainedBaseIdSidecar.Read(retained).Count);

            // A file cut short inside its stamp is damaged: neither the stamp nor the records
            // may be trusted, so it reads as not current rather than as a shorter file.
            byte[] bytes = File.ReadAllBytes(pass1);
            File.WriteAllBytes(pass1, bytes.Take(bytes.Length - 3).ToArray());
            Assert.IsNull(ArtifactValidity.ReadStamp(pass1));
            Assert.IsFalse(FdrScoresSidecar.IsCurrentFormat(pass1, FdrScoresSidecar.Pass.FirstPass));
            Assert.IsNull(ArtifactValidity.ReadStamp(Path.Combine(dir, "never_written" + FdrScoresSidecar.EXT)));
        }

        private static void AssertTextStamps(string dir, ArtifactStamp stamp)
        {
            // JSON: the stamp is the FIRST property, read without parsing the rest of a document
            // far larger than the head the reader looks at.
            string reconciliation = Path.Combine(dir, "run" + ReconciliationFile.EXT);
            ReconciliationFile.Save(reconciliation, new ReconciliationFile
            {
                FormatVersion = ReconciliationFile.CurrentFormatVersion,
                GapFillTargets = Enumerable.Range(0, 20000).Select(i => new GapFillEntry
                {
                    Charge = 2,
                    DecoyEntryId = (uint)i | 0x80000000u,
                    ExpectedRt = i * 0.01,
                    HalfWidth = 0.1,
                    ModifiedSequence = "PEPTIDE",
                    TargetEntryId = (uint)i,
                }).ToList(),
            }, stamp);
            Assert.IsTrue(new FileInfo(reconciliation).Length > 1024 * 1024);
            AssertStampRead(reconciliation, stamp);
            Assert.IsNotNull(ReconciliationFile.Load(reconciliation));

            string calibration = Path.Combine(dir, "run" + CalibrationIO.EXT);
            CalibrationIO.SaveCalibration(new CalibrationParams(), calibration, stamp);
            AssertStampRead(calibration, stamp);
            Assert.IsNotNull(CalibrationIO.LoadCalibration(calibration));

            // A JSON document whose first property is anything else carries no stamp.
            string unstamped = Path.Combine(dir, "unstamped.json");
            File.WriteAllText(unstamped, "{\"format_version\": 3, \"" + ArtifactStamp.JSON_PROPERTY + "\": \"" + stamp + "\"}");
            Assert.IsNull(ArtifactValidity.ReadStamp(unstamped));

            // HTML: an opening comment ahead of the DOCTYPE.
            string html = Path.Combine(dir, "out.model-diagnostics.html");
            File.WriteAllText(html, stamp.ToHtmlComment() + "\n<!DOCTYPE html><html></html>");
            AssertStampRead(html, stamp);
            File.WriteAllText(html, "<!DOCTYPE html><html></html>");
            Assert.IsNull(ArtifactValidity.ReadStamp(html));
        }

        private static void AssertParquetAndBlibStamps(string dir, ArtifactStamp stamp)
        {
            // One row: an empty entry list writes no parquet at all.
            var entries = new List<CoelutionScoredEntry>
            {
                new CoelutionScoredEntry
                {
                    EntryId = 1, Sequence = "PEPTIDE", ModifiedSequence = "PEPTIDE", Charge = 2,
                    FileName = "run.mzML", PeakBounds = new XICPeakBounds(), Features = new CoelutionFeatureSet(),
                },
            };
            string parquet = Path.Combine(dir, "run.scores.parquet");
            ParquetScoreCache.WriteScoresParquet(parquet, entries, ParquetScoreCache.WithStamp(null, stamp));
            AssertStampRead(parquet, stamp);
            string unstampedParquet = Path.Combine(dir, "unstamped.scores.parquet");
            ParquetScoreCache.WriteScoresParquet(unstampedParquet, entries, null);
            Assert.IsNull(ArtifactValidity.ReadStamp(unstampedParquet));

            string blib = Path.Combine(dir, "out.blib");
            using (var writer = new OspreyBlibWriter(blib))
            {
                writer.AddStamp(stamp);
                writer.Complete();
            }
            AssertStampRead(blib, stamp);
        }

        private static void AssertStampRead(string path, ArtifactStamp expected)
        {
            var read = ArtifactValidity.ReadStamp(path);
            Assert.IsNotNull(read, path);
            Assert.AreEqual(expected.ToString(), read.ToString(), path);
            Assert.IsTrue(ArtifactValidity.IsCurrent(path, expected.Task, expected.Key), path);
        }

        #endregion
    }
}
