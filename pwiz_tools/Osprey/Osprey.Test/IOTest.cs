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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for Osprey.IO types: BlibWriter, SpectraCache, MzmlReader, and ParquetScoreCache.
    /// Ported from osprey-io Rust tests.
    /// </summary>
    [TestClass]
    public class IOTest
    {
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
                using (var writer = new BlibWriter(path))
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
                    writer.AddMetadata("osprey_version", "0.1.0");
                    writer.FinalizeDatabase();
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
                using (var writer = new BlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTCIDE", "PEPTC[+57.021]IDE",
                        500.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0);

                    var mods = new List<Modification>
                    {
                        new Modification { Position = 4, UnimodId = 4, MassDelta = 57.021464, Name = "Carbamidomethyl" }
                    };
                    writer.AddModifications(refId, mods);
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
                using (var writer = new BlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTCIDE", "PEPTC[UniMod:4]IDE",
                        500.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0);
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

                using (var writer = new BlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        400.0, 2,
                        10.0, 9.0, 11.0,
                        mzs, intensities,
                        0.005, fileId, 1, 0.0);
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
                            byte[] mzBytes = BlibWriter.DecompressBlob(mzBlob, mzs.Length * 8);
                            double[] readMzs = BlibWriter.BytesToDoubles(mzBytes);
                            Assert.AreEqual(mzs.Length, readMzs.Length);
                            for (int i = 0; i < mzs.Length; i++)
                            {
                                Assert.AreEqual(mzs[i], readMzs[i], 1e-10,
                                    string.Format("m/z mismatch at index {0}", i));
                            }

                            // Decompress and verify intensities
                            byte[] intBytes = BlibWriter.DecompressBlob(intBlob, intensities.Length * 4);
                            float[] readInts = BlibWriter.BytesToFloats(intBytes);
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
                using (var writer = new BlibWriter(path))
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
                using (var writer = new BlibWriter(path))
                {
                    long fileId = writer.AddSourceFile("test.mzML", "library.tsv", 0.01);
                    refId = writer.AddSpectrum(
                        "PEPTIDE", "PEPTIDE",
                        400.0, 2,
                        10.0, 9.0, 11.0,
                        new[] { 300.0 },
                        new[] { 100.0f },
                        0.01, fileId, 1, 0.0);

                    var proteins = new List<string>
                    {
                        "sp|P12345|PROT_HUMAN",
                        "sp|P67890|PROT2_HUMAN"
                    };
                    writer.AddProteinMapping(refId, proteins);
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
        /// Verifies that StripFlankingChars correctly removes flanking amino acid notation.
        /// </summary>
        [TestMethod]
        public void TestStripFlankingChars()
        {
            // No flanking chars
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars("PEPTIDE"));

            // Underscores (DIA-NN format)
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars("_PEPTIDE_"));

            // Periods/underscores (Prosit format)
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars("_.PEPTIDE._"));

            // Flanking amino acids with periods (standard notation)
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars("K.PEPTIDE.R"));

            // Just periods
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars(".PEPTIDE."));

            // Modified sequence with flanking chars
            Assert.AreEqual("PEPTC[+57.021]IDE",
                BlibWriter.StripFlankingChars("_PEPTC[+57.021]IDE_"));

            // Dashes at ends
            Assert.AreEqual("PEPTIDE", BlibWriter.StripFlankingChars("-PEPTIDE-"));

            // Dots inside modification brackets must NOT be treated as flanking separators
            Assert.AreEqual("C[+57.02146]GPSPC[+57.02146]GLLK",
                BlibWriter.StripFlankingChars("C[+57.02146]GPSPC[+57.02146]GLLK"));

            // Multiple modifications with decimal dots
            Assert.AreEqual("AC[+57.02146]AC[+57.02146]AHGMLAEDGASC[+57.02146]R",
                BlibWriter.StripFlankingChars("AC[+57.02146]AC[+57.02146]AHGMLAEDGASC[+57.02146]R"));

            // Flanking chars with modification dots
            Assert.AreEqual("C[+57.02146]PEPTIDE",
                BlibWriter.StripFlankingChars("K.C[+57.02146]PEPTIDE.R"));

            // N-terminal modification with dot
            Assert.AreEqual("[+42.011]PEPTC[+57.02146]IDE",
                BlibWriter.StripFlankingChars("[+42.011]PEPTC[+57.02146]IDE"));
        }

        /// <summary>
        /// Verifies that UniMod IDs are correctly converted to mass deltas.
        /// </summary>
        [TestMethod]
        public void TestConvertUnimodToMass()
        {
            // No modifications
            Assert.AreEqual("PEPTIDE", BlibWriter.ConvertUnimodToMass("PEPTIDE"));

            // Already mass notation - unchanged
            Assert.AreEqual("PEPTC[+57.021]IDE",
                BlibWriter.ConvertUnimodToMass("PEPTC[+57.021]IDE"));

            // UniMod:4 (Carbamidomethyl)
            string result = BlibWriter.ConvertUnimodToMass("PEPTC[UniMod:4]IDE");
            Assert.IsTrue(result.StartsWith("PEPTC[+57."),
                "Expected mass notation for UniMod:4, got: " + result);
            Assert.IsTrue(result.EndsWith("]IDE"),
                "Expected to end with ]IDE, got: " + result);

            // UniMod:35 (Oxidation)
            result = BlibWriter.ConvertUnimodToMass("PEPTM[UniMod:35]IDE");
            Assert.IsTrue(result.StartsWith("PEPTM[+15."),
                "Expected mass notation for UniMod:35, got: " + result);

            // Multiple modifications
            result = BlibWriter.ConvertUnimodToMass("PEPTC[UniMod:4]M[UniMod:35]IDE");
            Assert.IsTrue(result.Contains("[+57."), "Should contain Carbamidomethyl mass");
            Assert.IsTrue(result.Contains("[+15."), "Should contain Oxidation mass");

            // Unknown UniMod - kept as-is
            Assert.AreEqual("PEPTX[UniMod:99999]IDE",
                BlibWriter.ConvertUnimodToMass("PEPTX[UniMod:99999]IDE"));

            // Case insensitive UNIMOD
            result = BlibWriter.ConvertUnimodToMass("PEPTC[UNIMOD:4]IDE");
            Assert.IsTrue(result.StartsWith("PEPTC[+57."),
                "Case-insensitive UNIMOD should work, got: " + result);

            // Negative mass (Glu->pyro-Glu)
            result = BlibWriter.ConvertUnimodToMass("E[UniMod:28]PEPTIDE");
            Assert.IsTrue(result.Contains("[-18."),
                "Expected negative mass for UniMod:28, got: " + result);

            // Verify TryGetUnimodMass for known IDs
            double mass;
            Assert.IsTrue(BlibWriter.TryGetUnimodMass(4, out mass));
            Assert.AreEqual(57.021464, mass, 1e-6);
            Assert.IsTrue(BlibWriter.TryGetUnimodMass(35, out mass));
            Assert.AreEqual(15.994915, mass, 1e-6);
            Assert.IsFalse(BlibWriter.TryGetUnimodMass(99999, out mass));
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
                }
                // No record lost or double-counted across the window partition.
                Assert.AreEqual(full.Ms2Spectra.Count, streamedTotal);

                // Absent key -> empty list (matches the dictionary miss).
                int absentKey = 1;
                while (expected.ContainsKey(absentKey))
                    absentKey++;
                Assert.AreEqual(0, index.LoadWindow(absentKey).Count);

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

                // First-cycle isolation windows: the distinct windows of DIA cycle 1 (records
                // 1-3; record 4's 500.03 repeats key 5000 and ends the cycle), each carrying
                // that key's first record's window, sorted by center. Mirrors
                // ScoringTaskShared.ExtractIsolationWindows so scoring's window fan-out is
                // unchanged without materializing the MS2 list.
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

            // A cache with bad magic -> null (same rule as LoadSpectraCache), so a
            // caller can fall back to a resident load rather than stream garbage.
            string badPath = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(badPath, System.Text.Encoding.ASCII.GetBytes("NOTVALID"));
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
                    w.Write(System.Text.Encoding.ASCII.GetBytes("OSPRSPC\0"));
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
            double massDelta;
            int? unimodId;
            string name;

            BlibLoader.IdentifyModification(57.021, false, out massDelta, out unimodId, out name);
            Assert.AreEqual(57.021464, massDelta, 0.01);
            Assert.AreEqual(4, unimodId);
            Assert.AreEqual("Carbamidomethyl", name);
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

        #region LibraryCache Tests

        [TestMethod]
        public void TestLibraryCacheRoundTrip()
        {
            var entries = new List<LibraryEntry>
            {
                MakeTestEntry(0),
                MakeTestEntry(1)
            };

            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_" + Guid.NewGuid().ToString("N") + ".libcache");

            try
            {
                LibraryCache.SaveCache(tempPath, entries, "round-trip-hash");
                var loaded = LibraryCache.LoadCache(tempPath);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(2, loaded.Count);

                for (int i = 0; i < entries.Count; i++)
                {
                    var orig = entries[i];
                    var copy = loaded[i];

                    Assert.AreEqual(orig.Id, copy.Id);
                    Assert.AreEqual(orig.Sequence, copy.Sequence);
                    Assert.AreEqual(orig.ModifiedSequence, copy.ModifiedSequence);
                    Assert.AreEqual(orig.Charge, copy.Charge);
                    Assert.AreEqual(orig.PrecursorMz, copy.PrecursorMz, 1e-10);
                    Assert.AreEqual(orig.RetentionTime, copy.RetentionTime, 1e-10);
                    Assert.AreEqual(orig.RtCalibrated, copy.RtCalibrated);
                    Assert.AreEqual(orig.IsDecoy, copy.IsDecoy);
                    Assert.AreEqual(orig.Fragments.Count, copy.Fragments.Count);
                    Assert.AreEqual(orig.Modifications.Count, copy.Modifications.Count);
                    CollectionAssert.AreEqual(orig.ProteinIds.ToArray(), copy.ProteinIds.ToArray());
                    CollectionAssert.AreEqual(orig.GeneNames.ToArray(), copy.GeneNames.ToArray());

                    // Check fragment details
                    for (int f = 0; f < orig.Fragments.Count; f++)
                    {
                        Assert.AreEqual(orig.Fragments[f].Mz, copy.Fragments[f].Mz, 1e-10);
                        Assert.AreEqual(orig.Fragments[f].RelativeIntensity,
                            copy.Fragments[f].RelativeIntensity, 1e-6);
                        Assert.AreEqual(orig.Fragments[f].Annotation.IonType,
                            copy.Fragments[f].Annotation.IonType);
                        Assert.AreEqual(orig.Fragments[f].Annotation.Ordinal,
                            copy.Fragments[f].Annotation.Ordinal);
                        Assert.AreEqual(orig.Fragments[f].Annotation.Charge,
                            copy.Fragments[f].Annotation.Charge);

                        Assert.AreEqual(orig.Fragments[f].Annotation.HasNeutralLoss,
                            copy.Fragments[f].Annotation.HasNeutralLoss);
                        Assert.AreEqual(orig.Fragments[f].Annotation.NeutralLossMass,
                            copy.Fragments[f].Annotation.NeutralLossMass, 1e-10);
                    }

                    // Check modification details
                    for (int m = 0; m < orig.Modifications.Count; m++)
                    {
                        Assert.AreEqual(orig.Modifications[m].Position,
                            copy.Modifications[m].Position);
                        Assert.AreEqual(orig.Modifications[m].UnimodId,
                            copy.Modifications[m].UnimodId);
                        Assert.AreEqual(orig.Modifications[m].MassDelta,
                            copy.Modifications[m].MassDelta, 1e-10);
                        Assert.AreEqual(orig.Modifications[m].Name,
                            copy.Modifications[m].Name);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestLibraryCacheIdentityHash()
        {
            // The .libcache stamps the source library's identity hash into its
            // header (v2). A cache is reused only when the caller-supplied
            // expected hash matches the stored one; a mismatch reports
            // IdentityMismatch and does NOT read the entries (so a stale cache
            // from a rebuilt-in-place library, whose decoys/pairing no longer
            // match the current run, is rebuilt from source rather than loaded).
            var entries = new List<LibraryEntry>
            {
                MakeTestEntry(0),
                MakeTestEntry(1)
            };

            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_identity_" + Guid.NewGuid().ToString("N") + ".libcache");

            try
            {
                LibraryCache.SaveCache(tempPath, entries, "hash-A");

                // Matching hash -> loaded with entries.
                LibraryCache.LibraryCacheStatus status;
                var loadedMatch = LibraryCache.LoadCache(tempPath, "hash-A", out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.Loaded, status);
                Assert.IsNotNull(loadedMatch);
                Assert.AreEqual(2, loadedMatch.Count);

                // Different hash -> identity mismatch, no entries read.
                var loadedMismatch = LibraryCache.LoadCache(tempPath, "hash-B", out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.IdentityMismatch, status);
                Assert.IsNull(loadedMismatch);

                // Null expected hash -> identity check skipped, entries loaded.
                var loadedNoCheck = LibraryCache.LoadCache(tempPath, null, out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.Loaded, status);
                Assert.IsNotNull(loadedNoCheck);
                Assert.AreEqual(2, loadedNoCheck.Count);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestLibraryCacheRealIdentityHash()
        {
            // End-to-end: stamp a cache with the REAL LibraryIdentityHash of a
            // temp library file, then change the file's size and mtime and
            // confirm the recomputed hash no longer matches the stored one, so
            // the cache reports IdentityMismatch. This locks the size+mtime
            // sensitivity of SearchIdentity.LibraryIdentityHash to the actual
            // file, catching even a timestamp-preserving swap that the old
            // mtime-ordering check would have missed.
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_cache_identity_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string source = Path.Combine(dir, "lib.tsv");
            string cachePath = Path.Combine(dir, "lib.tsv.libcache");
            try
            {
                File.WriteAllText(source, "original library contents");
                var config = new OspreyConfig { LibrarySource = LibrarySource.FromPath(source) };
                string hashBefore = config.Identity.LibraryIdentityHash();

                var entries = new List<LibraryEntry> { MakeTestEntry(0) };
                LibraryCache.SaveCache(cachePath, entries, hashBefore);

                // The cache is reused while the file identity is unchanged.
                LibraryCache.LibraryCacheStatus status;
                var loaded = LibraryCache.LoadCache(cachePath, hashBefore, out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.Loaded, status);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.Count);

                // Rebuild the library in place with a different size and mtime.
                File.WriteAllText(source, "a rebuilt library with different contents and length");
                File.SetLastWriteTimeUtc(source,
                    File.GetLastWriteTimeUtc(source).AddHours(1));
                string hashAfter = config.Identity.LibraryIdentityHash();
                Assert.AreNotEqual(hashBefore, hashAfter,
                    "changing the library's size and mtime must change its identity hash");

                // The stale cache is now detected and rebuilt from source.
                var stale = LibraryCache.LoadCache(cachePath, hashAfter, out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.IdentityMismatch, status);
                Assert.IsNull(stale);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        [TestMethod]
        public void TestLibraryCacheStaleVersionInvalid()
        {
            // A cache whose header carries an unsupported version -- e.g. a pre-v2
            // cache left on disk from an older build -- reads as Invalid and is
            // rebuilt; its body is never reinterpreted under the current layout.
            // Poke the version field (the uint32 immediately after the 8-byte
            // magic) down to v1 to simulate that, and confirm both the
            // identity-checked and the no-check load report Invalid rather than
            // misparsing the body.
            var entries = new List<LibraryEntry> { MakeTestEntry(0) };
            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_version_" + Guid.NewGuid().ToString("N") + ".libcache");
            try
            {
                LibraryCache.SaveCache(tempPath, entries, "hash-A");

                byte[] bytes = File.ReadAllBytes(tempPath);
                BitConverter.GetBytes((uint)1).CopyTo(bytes, 8); // version at offset 8
                File.WriteAllBytes(tempPath, bytes);

                LibraryCache.LibraryCacheStatus status;
                var loaded = LibraryCache.LoadCache(tempPath, "hash-A", out status);
                Assert.AreEqual(LibraryCache.LibraryCacheStatus.Invalid, status);
                Assert.IsNull(loaded);

                // The version gate precedes the identity check, so the identity-
                // agnostic overload is Invalid too, not silently accepted.
                Assert.IsNull(LibraryCache.LoadCache(tempPath));
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestLibraryCacheEmpty()
        {
            var entries = new List<LibraryEntry>();

            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_empty_" + Guid.NewGuid().ToString("N") + ".libcache");

            try
            {
                LibraryCache.SaveCache(tempPath, entries, "empty-hash");
                var loaded = LibraryCache.LoadCache(tempPath);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(0, loaded.Count);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestLibraryCacheInvalidMagic()
        {
            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_bad_" + Guid.NewGuid().ToString("N") + ".libcache");

            try
            {
                File.WriteAllBytes(tempPath, System.Text.Encoding.ASCII.GetBytes("NOTVALID"));

                var result = LibraryCache.LoadCache(tempPath);
                Assert.IsNull(result);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestLibraryCacheNeutralLossCollapse()
        {
            // A Custom neutral-loss mass within 1e-6 of a named loss must serialize
            // to the named tag (byte-identity with the legacy reference-type writer)
            // and read back as the named code -- the most byte-sensitive
            // WriteNeutralLoss branch, which the round-trip test does not exercise.
            var entry = new LibraryEntry(1, "PEPTIDER", "PEPTIDER", 2, 500.0, 10.0);
            entry.Fragments = new List<LibraryFragment>
            {
                new LibraryFragment
                {
                    Mz = 200.0,
                    RelativeIntensity = 1.0f,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.B,
                        Ordinal = 1,
                        Charge = 1,
                        NeutralLoss = NeutralLossCode.Custom,
                        CustomLossMass = NeutralLoss.H2OMass
                    }
                }
            };

            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_nl_" + Guid.NewGuid().ToString("N") + ".libcache");
            try
            {
                LibraryCache.SaveCache(tempPath, new List<LibraryEntry> { entry }, "hash");
                var loaded = LibraryCache.LoadCache(tempPath);
                Assert.AreEqual(1, loaded.Count);
                var ann = loaded[0].Fragments[0].Annotation;
                Assert.AreEqual(NeutralLossCode.H2O, ann.NeutralLoss);
                Assert.AreEqual(NeutralLoss.H2OMass, ann.NeutralLossMass, 1e-10);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

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

            // Decompress and verify
            byte[] decompressedMz = BlibLoader.TryZlibDecompress(compressedMz, mzBytes.Length);
            Assert.IsNotNull(decompressedMz);
            Assert.AreEqual(mzBytes.Length, decompressedMz.Length);

            // Verify round-trip of m/z values
            for (int i = 0; i < mzValues.Length; i++)
            {
                double mz = BitConverter.ToDouble(decompressedMz, i * 8);
                Assert.AreEqual(mzValues[i], mz, 1e-10);
            }

            byte[] decompressedInt = BlibLoader.TryZlibDecompress(compressedInt, intBytes.Length);
            Assert.IsNotNull(decompressedInt);
            Assert.AreEqual(intBytes.Length, decompressedInt.Length);
        }

        #endregion

        #region BlibLoader Tests

        [TestMethod]
        public void TestBlibModifications()
        {
            // Simple unmodified
            var mods = BlibLoader.ParseBlibModifications("PEPTIDE");
            Assert.AreEqual(0, mods.Count);

            // Carbamidomethyl
            mods = BlibLoader.ParseBlibModifications("PEPTC[+57.021]IDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(4, mods[0].Position);
            Assert.AreEqual(57.021464, mods[0].MassDelta, 0.01);
            Assert.AreEqual(4, mods[0].UnimodId);

            // Oxidation
            mods = BlibLoader.ParseBlibModifications("PEPTM[+15.995]IDE");
            Assert.AreEqual(1, mods.Count);
            Assert.AreEqual(4, mods[0].Position);
            Assert.AreEqual(35, mods[0].UnimodId);
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

        #endregion

        #region MzmlReader Tests

        /// <summary>
        /// Verifies that isolation window CVs are parsed correctly from a minimal mzML snippet.
        /// </summary>
        [TestMethod]
        public void TestMzmlIsolationWindowParsing()
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
                var result = MzmlReader.LoadAllSpectra(path);

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
        public void TestMzmlMs1Ms2Separation()
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
                var result = MzmlReader.LoadAllSpectra(path);

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
        /// Verifies that retention time in seconds (MS:1000894) is converted to minutes.
        /// </summary>
        [TestMethod]
        public void TestMzmlRetentionTimeSeconds()
        {
            // Build mzML with RT in seconds using MS:1000894
            string spectrumBlock = @"
      <spectrum index=""0"" defaultArrayLength=""1"" id=""scan=1"">
        <cvParam cvRef=""MS"" accession=""MS:1000511"" value=""2"" />
        <scanList count=""1"">
          <scan>
            <cvParam cvRef=""MS"" accession=""MS:1000894"" value=""330.0"" unitName=""second"" />
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
                var result = MzmlReader.LoadAllSpectra(path);

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

        #region ParquetScoreCache Tests

        /// <summary>
        /// Write scored entries to Parquet, then read back FDR stubs and PIN features,
        /// verifying round-trip data integrity.
        /// </summary>
        [TestMethod]
        public void TestParquetScoreCacheRoundTrip()
        {
            // FileSaver owns the scratch file's lifecycle: write + read back through
            // its sibling temp and never Commit() -- Dispose discards it (no leaked
            // GetTempFileName file, cleaned up even if an assertion throws).
            string dest = Path.Combine(Path.GetTempPath(),
                @"osprey_roundtrip_" + Path.GetRandomFileName() + @".parquet");
            using (var saver = new FileSaver(dest))
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
            // FileSaver owns the scratch file's lifecycle: write the fixture to its
            // sibling temp, probe that, and never Commit() -- Dispose discards the temp
            // (no leaked temp file, cleaned up even if an assertion throws).
            string dest = Path.Combine(Path.GetTempPath(),
                @"osprey_haspin_" + Path.GetRandomFileName() + @".parquet");
            using (var saver = new FileSaver(dest))
            {
                var entryIdField = new DataField<uint>("entry_id");
                var schema = new ParquetSchema(entryIdField);
                using (var stream = new FileStream(saver.SafeName, FileMode.Create, FileAccess.Write))
                using (var writer = ParquetWriter.CreateAsync(schema, stream).GetAwaiter().GetResult())
                using (var group = writer.CreateRowGroup())
                {
                    group.WriteColumnAsync(new DataColumn(entryIdField, new[] { 1u, 2u, 3u }))
                        .GetAwaiter().GetResult();
                }

                Assert.IsFalse(ParquetScoreCache.HasPinFeatureColumns(saver.SafeName),
                    "A parquet without the PIN feature columns must be rejected by the lean-path guard.");
            }
        }

        /// <summary>
        /// Issue #4374 risk #2 (the highest-value new test): the 2nd-pass projection
        /// bakes each survivor's <c>ParquetIndex</c> from
        /// <see cref="Pass2FdrSidecar.BuildReconciledIdentityToRow"/>, then the streaming
        /// score pass reads the feature row at that index. This must resolve the EXACT
        /// vector the resident 2nd pass binds via
        /// <see cref="Pass2FdrSidecar.LoadReconciledFeaturesByIdentity"/> +
        /// <c>MapFeaturesByIdentity</c>. Writes a reconciled-parquet fixture whose rows
        /// arrive in NON-sorted identity order plus a gap-fill-style row that interleaves
        /// into the <c>(entry_id, charge, scan_number)</c> sort, each carrying a DISTINCT
        /// 21-feature vector, then asserts:
        /// <list type="bullet">
        /// <item><c>featRows[rowMap[identity]] == featByIdentity[identity]</c> for every
        /// identity (risk #2) -- so the streamed feature lookup is byte-identical to the
        /// resident identity binding; distinct vectors make a mis-mapping observable;</item>
        /// <item>within each <c>(entry_id, charge)</c> group the baked row increases with
        /// scan (risk #3) -- what keeps the scan-omitted projection sort
        /// <c>(EntryId, Charge, ParquetIndex)</c> equal to the legacy
        /// <c>(EntryId, Charge, ScanNumber, ParquetIndex)</c> order.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestBuildReconciledIdentityToRowMatchesFeatureBinding()
        {
            string path = Path.GetTempFileName() + ".parquet";
            try
            {
                // Deliberately unsorted, with two charges of entry 100 and a second
                // (later-scan) row for entry 101 that interleaves into the reconciled
                // (entry_id, charge, scan_number) sort -- a gap-fill-style append.
                var entryIds = new uint[] { 101, 100, 100, 101, 100 };
                var charges = new byte[] { 2, 3, 2, 2, 2 };
                var scans = new uint[] { 2200, 1500, 1100, 1200, 1300 };

                var entries = new List<CoelutionScoredEntry>();
                for (int i = 0; i < entryIds.Length; i++)
                {
                    entries.Add(new CoelutionScoredEntry
                    {
                        EntryId = entryIds[i],
                        IsDecoy = false,
                        Sequence = "PEPTIDE",
                        ModifiedSequence = "PEPTIDE",
                        Charge = charges[i],
                        ScanNumber = scans[i],
                        FileName = "recon.mzML",
                        PeakBounds = new XICPeakBounds { StartRt = 4.0, EndRt = 5.0 },
                        // Distinct feature vector per row so a wrong identity->row map
                        // surfaces as a value mismatch, not a silent pass.
                        Features = new CoelutionFeatureSet
                        {
                            CoelutionSum = 10.0 + i,
                            CoelutionMax = 20.0 + i,
                            NCoelutingFragments = (byte)(3 + i),
                            PeakApex = 100.0 + i,
                            PeakArea = 200.0 + i,
                            PeakSharpness = 0.3 + i,
                            Xcorr = 50.0 + i,
                            ConsecutiveIons = (byte)(1 + i),
                            ExplainedIntensity = 0.50 + i * 0.01,
                            MassAccuracyMean = -0.5 - i,
                            AbsMassAccuracyMean = 0.5 + i,
                            RtDeviation = 0.1 + i,
                            AbsRtDeviation = 0.1 + i,
                            Ms1PrecursorCoelution = 0.80 + i * 0.01,
                            Ms1IsotopeCosine = 0.90 + i * 0.01,
                            MedianPolishCosine = 0.88 + i * 0.001,
                            MedianPolishResidualRatio = 0.15 + i * 0.001,
                            SgWeightedXcorr = 2.3 + i,
                            SgWeightedCosine = 0.87 + i * 0.001,
                            MedianPolishMinFragmentR2 = 0.70 + i * 0.001,
                            MedianPolishResidualCorrelation = 0.30 + i * 0.001,
                        },
                    });
                }

                // WriteScoresParquet re-sorts (entry_id, charge, scan_number) and assigns
                // ParquetIndex = row -- exactly the reconciled write path.
                ParquetScoreCache.WriteScoresParquet(path, entries, null);

                var rowMap = Pass2FdrSidecar.BuildReconciledIdentityToRow(path);
                var featByIdentity = Pass2FdrSidecar.LoadReconciledFeaturesByIdentity(path);
                var featRows = ParquetScoreCache.LoadPinFeaturesFromParquet(path);

                Assert.AreEqual(entryIds.Length, rowMap.Count);
                Assert.AreEqual(entryIds.Length, featByIdentity.Count);
                Assert.AreEqual(entryIds.Length, featRows.Count);

                // Risk #2: the baked row addresses the identity's own feature vector, so
                // the streamed lookup equals the resident identity binding byte-for-byte.
                foreach (var kvp in featByIdentity)
                {
                    Assert.IsTrue(rowMap.TryGetValue(kvp.Key, out uint row),
                        "every bound identity must resolve to a reconciled row");
                    Assert.IsTrue((int)row < featRows.Count, "baked row in range");
                    CollectionAssert.AreEqual(kvp.Value, featRows[(int)row],
                        "the baked row must address the identity's own feature vector");
                }

                // Risk #3: within each (entry_id, charge) group the reconciled row is
                // scan-monotonic -- what validates the scan-omitted projection sort.
                var groups = new Dictionary<(uint, byte), List<(uint scan, uint row)>>();
                for (int i = 0; i < entryIds.Length; i++)
                {
                    uint row = rowMap[(entryIds[i], charges[i], scans[i])];
                    var key = (entryIds[i], charges[i]);
                    if (!groups.TryGetValue(key, out var list))
                    {
                        list = new List<(uint, uint)>();
                        groups[key] = list;
                    }
                    list.Add((scans[i], row));
                }
                foreach (var kv in groups)
                {
                    var list = kv.Value;
                    list.Sort((a, b) => a.scan.CompareTo(b.scan));
                    for (int k = 1; k < list.Count; k++)
                    {
                        Assert.IsTrue(list[k].row > list[k - 1].row,
                            "reconciled row must increase with scan within a (entry_id, charge) group");
                    }
                }
            }
            finally
            {
                TryDeleteFile(path);
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
        /// Verifies EffectiveScoresPathFromScoresPath returns the reconciled
        /// sibling when it exists on disk, else the original -- the per-file
        /// read contract that makes the separate-reconciled-file design
        /// byte-equivalent to the former in-place overwrite.
        /// </summary>
        [TestMethod]
        public void TestEffectiveScoresPathFromScoresPath()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_eff_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string original = Path.Combine(dir, "sample1.scores.parquet");
                string reconciled = Path.Combine(dir, "sample1.scores-reconciled.parquet");
                File.WriteAllText(original, "x");

                // No reconciled sibling -> original (no-work file).
                Assert.AreEqual(original,
                    ParquetScoreCache.EffectiveScoresPathFromScoresPath(original));

                // Reconciled sibling present -> reconciled (rescored file).
                File.WriteAllText(reconciled, "y");
                Assert.AreEqual(reconciled,
                    ParquetScoreCache.EffectiveScoresPathFromScoresPath(original));

                // An already-reconciled input that exists is returned as-is.
                Assert.AreEqual(reconciled,
                    ParquetScoreCache.EffectiveScoresPathFromScoresPath(reconciled));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
            }
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
            <cvParam cvRef=""MS"" accession=""MS:1000016"" value=""{3}"" unitName=""minute"" />
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

        private static LibraryEntry MakeTestEntry(uint id)
        {
            var entry = new LibraryEntry(id, "PEPTIDEK", "PEPTIDEK", 2, 471.2567, 25.3);
            entry.Modifications = new List<Modification>
            {
                new Modification
                {
                    Position = 0,
                    UnimodId = 1,
                    MassDelta = 42.010565,
                    Name = "Acetyl"
                }
            };
            entry.Fragments = new List<LibraryFragment>
            {
                new LibraryFragment
                {
                    Mz = 147.1128,
                    RelativeIntensity = 1.0f,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.Y,
                        Ordinal = 1,
                        Charge = 1
                    }
                },
                new LibraryFragment
                {
                    Mz = 262.1398,
                    RelativeIntensity = 0.5f,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.B,
                        Ordinal = 3,
                        Charge = 1,
                        NeutralLoss = NeutralLossCode.H2O
                    }
                },
                new LibraryFragment
                {
                    Mz = 500.0,
                    RelativeIntensity = 0.3f,
                    Annotation = new FragmentAnnotation
                    {
                        IonType = IonType.Y,
                        Ordinal = 5,
                        Charge = 2,
                        NeutralLoss = NeutralLossCode.Custom,
                        CustomLossMass = 98.0
                    }
                }
            };
            entry.ProteinIds = new List<string> { "P12345", "Q67890" };
            entry.GeneNames = new List<string> { "GENE1" };

            return entry;
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

        private static FdrEntry MakeFdrEntry(uint id, double score, double q, double pep,
            double runProteinQvalue = 1.0)
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
                RunProteinQvalue = runProteinQvalue,
                ExperimentPrecursorQvalue = q + 2.0e-9,
                ExperimentPeptideQvalue = q + 3.0e-9,
                ExperimentProteinQvalue = 1.0,
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
                // Distinct run_protein_qvalue per entry catches a writer that
                // drops the v3 field. Values match Rust's
                // fdr_scores_sidecar_v3_round_trip exactly so the
                // OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT byte-parity gate compares
                // identical inputs on both sides.
                var entries = new List<FdrEntry>
                {
                    MakeFdrEntry(0, -3.5, 0.001, 0.02, runProteinQvalue: 0.0042),
                    MakeFdrEntry(1, -3.4, 0.002, 0.05, runProteinQvalue: 0.0123),
                    MakeFdrEntry(2, -3.3, 0.003, 0.08, runProteinQvalue: 0.95),
                };

                FdrScoresSidecar.Write(path, entries, FdrScoresSidecar.Pass.FirstPass);

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
                    FdrScoresSidecar.HeaderLength + entries.Count * FdrScoresSidecar.RecordLength,
                    size);

                // Stubs with cleared FDR fields — TryRead must repopulate them.
                var loaded = new List<FdrEntry>
                {
                    MakeFdrEntry(0, 0.0, 0.0, 0.0),
                    MakeFdrEntry(1, 0.0, 0.0, 0.0),
                    MakeFdrEntry(2, 0.0, 0.0, 0.0),
                };
                Assert.IsTrue(FdrScoresSidecar.TryRead(path, loaded, FdrScoresSidecar.Pass.FirstPass));

                for (int i = 0; i < entries.Count; i++)
                {
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].Score),
                                    BitConverter.DoubleToInt64Bits(loaded[i].Score));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].RunPrecursorQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].RunPrecursorQvalue));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].RunPeptideQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].RunPeptideQvalue));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].ExperimentPrecursorQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].ExperimentPrecursorQvalue));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].ExperimentPeptideQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].ExperimentPeptideQvalue));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].Pep),
                                    BitConverter.DoubleToInt64Bits(loaded[i].Pep));
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(entries[i].RunProteinQvalue),
                                    BitConverter.DoubleToInt64Bits(loaded[i].RunProteinQvalue));
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Two-phase 1st-pass sidecar write (issue #4355 struct-shrink S1, risk R2):
        /// a phase-1 partial write (every record's run_protein_qvalue held at the 1.0
        /// placeholder) followed by <see cref="FdrScoresSidecar.PatchRunProteinQvalues"/>
        /// must produce a file that is BYTE-IDENTICAL to a single-phase reference write
        /// whose records already carried the finalized run_protein_qvalue. The map is
        /// entry_id-keyed and deliberately built out of record order (and the records
        /// carry non-sequential entry_ids) to prove the patch locates each record's
        /// [52..60] by entry_id, not by position.
        /// </summary>
        [TestMethod]
        public void TestFdrScoresSidecarTwoPhasePatchMatchesSinglePhase()
        {
            string dir = Path.Combine(Path.GetTempPath(), "fdr_sidecar_2ph_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Finalized records with a distinct real run_protein_qvalue each (the
                // last arg). entry_ids are non-sequential so a positional patch would
                // land the wrong value.
                var real = new List<FdrScoreRecord>
                {
                    new FdrScoreRecord(10, -3.5, 0.001, 0.0011, 0.0012, 0.0013, 0.02, 0.0042),
                    new FdrScoreRecord(7,  -3.4, 0.002, 0.0021, 0.0022, 0.0023, 0.05, 0.0123),
                    new FdrScoreRecord(42, -3.3, 0.003, 0.0031, 0.0032, 0.0033, 0.08, 0.95),
                    new FdrScoreRecord(3,  -3.2, 0.004, 0.0041, 0.0042, 0.0043, 0.11, 1.0),
                };

                // Phase-1 partial records: identical EXCEPT run_protein_qvalue = 1.0.
                var partial = new List<FdrScoreRecord>(real.Count);
                foreach (var r in real)
                {
                    partial.Add(new FdrScoreRecord(
                        r.EntryId, r.Score, r.RunPrecursorQvalue, r.RunPeptideQvalue,
                        r.ExperimentPrecursorQvalue, r.ExperimentPeptideQvalue, r.Pep, 1.0));
                }

                // Map entry_id -> finalized run_protein_qvalue, inserted out of record
                // order to prove entry_id-keyed (order-independent) patching.
                var runProteinByEntryId = new Dictionary<uint, double>
                {
                    { 42, 0.95 },
                    { 3, 1.0 },
                    { 10, 0.0042 },
                    { 7, 0.0123 },
                };

                string refPath = Path.Combine(dir, "ref.1st-pass.fdr_scores.bin");
                string twoPhasePath = Path.Combine(dir, "twophase.1st-pass.fdr_scores.bin");

                // Single-phase reference (the pre-S1 write): records carry the real value.
                FdrScoresSidecar.Write(refPath, real, FdrScoresSidecar.Pass.FirstPass);

                // Two-phase: phase-1 partial + phase-2 [52..60] patch.
                FdrScoresSidecar.Write(twoPhasePath, partial, FdrScoresSidecar.Pass.FirstPass);
                Assert.IsTrue(FdrScoresSidecar.PatchRunProteinQvalues(
                    twoPhasePath, runProteinByEntryId, FdrScoresSidecar.Pass.FirstPass));

                // The finalized two-phase file must be byte-for-byte identical to the
                // single-phase reference (risk R2 -- what mode3 compares cross-process).
                byte[] refBytes = File.ReadAllBytes(refPath);
                byte[] twoPhaseBytes = File.ReadAllBytes(twoPhasePath);
                CollectionAssert.AreEqual(refBytes, twoPhaseBytes,
                    "Two-phase (partial + [52..60] patch) sidecar diverged from the single-phase write");

                // Sanity: the patch actually finalized run_protein_qvalue (a pre-patch
                // read would have seen the 1.0 placeholder for entries 10/7/42).
                var loaded = new List<FdrEntry>
                {
                    MakeFdrEntry(10, 0.0, 0.0, 0.0),
                    MakeFdrEntry(7, 0.0, 0.0, 0.0),
                    MakeFdrEntry(42, 0.0, 0.0, 0.0),
                    MakeFdrEntry(3, 0.0, 0.0, 0.0),
                };
                Assert.IsTrue(FdrScoresSidecar.TryRead(twoPhasePath, loaded, FdrScoresSidecar.Pass.FirstPass));
                var byId = new Dictionary<uint, FdrEntry>();
                foreach (var e in loaded)
                    byId[e.EntryId] = e;
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(0.0042),
                                BitConverter.DoubleToInt64Bits(byId[10].RunProteinQvalue));
                Assert.AreEqual(BitConverter.DoubleToInt64Bits(0.95),
                                BitConverter.DoubleToInt64Bits(byId[42].RunProteinQvalue));

                // A patch with the wrong pass byte must be rejected and leave bytes intact.
                Assert.IsFalse(FdrScoresSidecar.PatchRunProteinQvalues(
                    twoPhasePath, runProteinByEntryId, FdrScoresSidecar.Pass.SecondPass));
                CollectionAssert.AreEqual(refBytes, File.ReadAllBytes(twoPhasePath),
                    "A rejected patch must not modify the sidecar");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
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
                    FdrScoresSidecar.Pass.FirstPass);

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
                    FdrScoresSidecar.Pass.FirstPass);

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
        /// the record loop. Both <see cref="FdrScoresSidecar.TryRead"/>
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
                    FdrScoresSidecar.Pass.FirstPass);
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
                    FdrScoresSidecar.Pass.FirstPass);

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
                FileStems = new List<string> { "round_trip" },
                FirstPassBaseIds = new[] { 3u, 100u, 101u, 200u, 201u },
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
                LibraryHash = "lib-hash-abc",
                RefinedRtCalibration = new RefinedRtCalibrationJson
                {
                    AbsResiduals = new[] { 0.01, 0.02, 0.015 },
                    FittedRts    = new[] { 10.5, 20.5, 30.5 },
                    LibraryRts   = new[] { 10.0, 20.0, 30.0 },
                    ResidualSd   = 0.123,
                },
                SearchHash = "search-hash-xyz",
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

                ReconciliationFile.Save(path, file);

                if (!string.IsNullOrEmpty(OspreyEnvironment.CrossImplReconciliationOut))
                    File.Copy(path, OspreyEnvironment.CrossImplReconciliationOut, overwrite: true);

                var parsed = ReconciliationFile.Load(path);

                Assert.AreEqual(file.FormatVersion, parsed.FormatVersion);
                Assert.AreEqual(file.SearchHash, parsed.SearchHash);
                Assert.AreEqual(file.LibraryHash, parsed.LibraryHash);
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
                    StringAssert.Contains(ex.Message, "unsupported format_version");
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

        #region RescoreHydration Tests

        /// <summary>
        /// End-to-end round-trip: write a synthetic Stage 5 → Stage 6
        /// boundary file pair (.scores.parquet + .1st-pass.fdr_scores.bin
        /// + .reconciliation.json) for a single file, hydrate it via
        /// <see cref="RescoreHydration.HydrateForRescore"/>, and assert
        /// every piece of the in-memory state matches what was written.
        /// Mirrors what the worker does at startup before driving the
        /// rescore engine.
        /// </summary>
        [TestMethod]
        public void TestRescoreHydrationRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "rescore_hydrate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                const string stem = "sample1";
                string parquetPath = Path.Combine(dir, stem + ".scores.parquet");
                string mzmlSynthetic = Path.Combine(dir, stem + ".mzML");
                string sidecarPath = FdrScoresSidecar.Pass1Path(mzmlSynthetic);
                string reconPath = ReconciliationFile.PathForInput(mzmlSynthetic);

                // 1. Build 3 synthetic scored entries and write the parquet.
                var scoredEntries = new List<CoelutionScoredEntry>();
                for (int i = 0; i < 3; i++)
                {
                    scoredEntries.Add(new CoelutionScoredEntry
                    {
                        EntryId = (uint)(100 + i),
                        IsDecoy = i == 1,
                        Sequence = "PEPTIDE",
                        ModifiedSequence = "PEPTIDE",
                        Charge = 2,
                        PrecursorMz = 500.0 + i,
                        ScanNumber = (uint)(1000 + i),
                        ApexRt = 5.0 + i * 0.5,
                        FileName = stem + ".mzML",
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
                            PeakApex = 1000.0,
                            PeakArea = 5000.0,
                            PeakSharpness = 0.8,
                            Xcorr = 2.5,
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
                    });
                }
                ParquetScoreCache.WriteScoresParquet(parquetPath, scoredEntries,
                    new Dictionary<string, string>
                    {
                        { "osprey.version", "1.0.0" },
                        { "osprey.search_hash", "abc123" },
                    });

                // 2. Build matching FdrEntry list (same EntryId order +
                //    distinct field values per record) and write the v3
                //    sidecar.
                var sidecarEntries = new List<FdrEntry>
                {
                    MakeFdrEntry(100, -3.5, 0.001, 0.02, runProteinQvalue: 0.42),
                    MakeFdrEntry(101, -3.4, 0.002, 0.05, runProteinQvalue: 0.43),
                    MakeFdrEntry(102, -3.3, 0.003, 0.08, runProteinQvalue: 0.44),
                };
                FdrScoresSidecar.Write(sidecarPath, sidecarEntries,
                    FdrScoresSidecar.Pass.FirstPass);

                // 3. Build a reconciliation.json envelope: one
                //    UseCwtPeak action on entry 100, one ForcedIntegration
                //    on entry 102, plus a refined cal and a single
                //    gap-fill target.
                var reconFile = new ReconciliationFile
                {
                    FormatVersion = ReconciliationFile.CurrentFormatVersion,
                    FileStems = new List<string> { stem },
                    FirstPassBaseIds = new[] { 100u, 101u, 102u },
                    SearchHash = "abc123",
                    LibraryHash = "lib-h",
                    UseCwtPeakActions = new List<UseCwtPeakEntry>
                    {
                        new UseCwtPeakEntry
                        {
                            ApexRt = 5.07, CandidateIdx = 1,
                            EndRt = 5.40, EntryId = 100, StartRt = 4.70,
                        },
                    },
                    ForcedIntegrationActions = new List<ForcedIntegrationEntry>
                    {
                        new ForcedIntegrationEntry
                            { EntryId = 102, ExpectedRt = 6.10, HalfWidth = 0.075 },
                    },
                    RefinedRtCalibration = new RefinedRtCalibrationJson
                    {
                        AbsResiduals = new[] { 0.01, 0.02, 0.015 },
                        FittedRts = new[] { 10.5, 20.5, 30.5 },
                        LibraryRts = new[] { 10.0, 20.0, 30.0 },
                        ResidualSd = 0.123,
                    },
                    GapFillTargets = new List<GapFillEntry>
                    {
                        new GapFillEntry
                        {
                            Charge = 2,
                            DecoyEntryId = 0x80000005u,
                            ExpectedRt = 33.5,
                            HalfWidth = 0.08,
                            ModifiedSequence = "PEPTIDE",
                            TargetEntryId = 5,
                        },
                    },
                };
                ReconciliationFile.Save(reconPath, reconFile);

                // 4. Hydrate.
                var inputs = RescoreHydration.HydrateForRescore(new[] { parquetPath });

                // 5. Assert per-file entries: same fileName, same count,
                //    Score / RunProteinQvalue overlaid bit-exactly.
                Assert.AreEqual(1, inputs.PerFileEntries.Count);
                Assert.AreEqual(stem, inputs.PerFileEntries[0].Key);
                var got = inputs.PerFileEntries[0].Value;
                Assert.AreEqual(3, got.Count);
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreEqual(sidecarEntries[i].EntryId, got[i].EntryId);
                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(sidecarEntries[i].Score),
                                    BitConverter.DoubleToInt64Bits(got[i].Score));
                    Assert.AreEqual(
                        BitConverter.DoubleToInt64Bits(sidecarEntries[i].RunProteinQvalue),
                        BitConverter.DoubleToInt64Bits(got[i].RunProteinQvalue));
                }

                // 6. Assert reconciliation actions: keyed by
                //    (fileName, vec_idx) where vec_idx is the row index
                //    in PerFileEntries[0].Value.
                Assert.AreEqual(2, inputs.ReconciliationActions.Count);
                var useCwt = inputs.ReconciliationActions[(stem, 0)] as ReconcileAction.UseCwtPeak;
                Assert.IsNotNull(useCwt, "expected UseCwtPeak at (stem, 0)");
                Assert.AreEqual(1, useCwt.CandidateIndex);
                Assert.AreEqual(5.07, useCwt.ApexRt);
                Assert.AreEqual(4.70, useCwt.StartRt);
                Assert.AreEqual(5.40, useCwt.EndRt);

                var forced = inputs.ReconciliationActions[(stem, 2)]
                    as ReconcileAction.ForcedIntegration;
                Assert.IsNotNull(forced, "expected ForcedIntegration at (stem, 2)");
                Assert.AreEqual(6.10, forced.ExpectedRt);
                Assert.AreEqual(0.075, forced.HalfWidth);

                // 7. Assert refined RT calibration is reconstructed.
                Assert.IsTrue(inputs.RefinedCalibrations.ContainsKey(stem));

                // 8. Assert gap-fill target round-trip.
                Assert.IsTrue(inputs.PerFileGapFill.ContainsKey(stem));
                var gap = inputs.PerFileGapFill[stem];
                Assert.AreEqual(1, gap.Count);
                Assert.AreEqual(5u, gap[0].TargetEntryId);
                Assert.AreEqual(0x80000005u, gap[0].DecoyEntryId);
                Assert.AreEqual(33.5, gap[0].ExpectedRt);
                Assert.AreEqual(0.08, gap[0].HalfWidth);
                Assert.AreEqual("PEPTIDE", gap[0].ModifiedSequence);
                Assert.AreEqual((byte)2, gap[0].Charge);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// If the planner emits an entry_id that doesn't appear in the
        /// stub list (e.g., parquet/boundary mismatch from a botched
        /// rebuild), the hydrator must throw rather than silently
        /// skipping the action — a Stage 6 worker proceeding with
        /// missing actions would scramble gap-fill results.
        /// </summary>
        [TestMethod]
        public void TestRescoreHydrationRejectsActionEntryIdNotInStubs()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "rescore_hyd_drift_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                const string stem = "sample1";
                string parquetPath = Path.Combine(dir, stem + ".scores.parquet");
                string mzmlSynthetic = Path.Combine(dir, stem + ".mzML");
                string sidecarPath = FdrScoresSidecar.Pass1Path(mzmlSynthetic);
                string reconPath = ReconciliationFile.PathForInput(mzmlSynthetic);

                var scored = new List<CoelutionScoredEntry>
                {
                    new CoelutionScoredEntry
                    {
                        EntryId = 100, ModifiedSequence = "PEPTIDE", Sequence = "PEPTIDE",
                        Charge = 2, PrecursorMz = 500.0, ScanNumber = 1000, ApexRt = 5.0,
                        FileName = stem + ".mzML",
                        PeakBounds = new XICPeakBounds { StartRt = 4.5, EndRt = 5.5 },
                        Features = new CoelutionFeatureSet { CoelutionSum = 0.9 },
                    },
                };
                ParquetScoreCache.WriteScoresParquet(parquetPath, scored,
                    new Dictionary<string, string> { { "osprey.version", "1.0.0" } });
                FdrScoresSidecar.Write(sidecarPath,
                    new List<FdrEntry> { MakeFdrEntry(100, -3.5, 0.001, 0.02) },
                    FdrScoresSidecar.Pass.FirstPass);

                // entry_id 999 is NOT in the parquet/sidecar — drift!
                var reconFile = new ReconciliationFile
                {
                    FormatVersion = ReconciliationFile.CurrentFormatVersion,
                    FileStems = new List<string> { stem },
                    FirstPassBaseIds = new[] { 100u, 101u, 102u },
                    SearchHash = "x",
                    LibraryHash = "y",
                    UseCwtPeakActions = new List<UseCwtPeakEntry>
                    {
                        new UseCwtPeakEntry
                        {
                            ApexRt = 5.0, CandidateIdx = 0, EndRt = 5.5,
                            EntryId = 999, StartRt = 4.5,
                        },
                    },
                    ForcedIntegrationActions = new List<ForcedIntegrationEntry>(),
                    GapFillTargets = new List<GapFillEntry>(),
                };
                ReconciliationFile.Save(reconPath, reconFile);

                try
                {
                    RescoreHydration.HydrateForRescore(new[] { parquetPath });
                    Assert.Fail("expected InvalidDataException for entry_id drift");
                }
                catch (InvalidDataException ex)
                {
                    StringAssert.Contains(ex.Message, "999");
                    StringAssert.Contains(ex.Message, "not found in stubs");
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        #endregion

        #region RescoreCompaction Tests

        /// <summary>
        /// Worker compaction: drop non-passing entries by base_id and
        /// re-key reconciliation actions from pre-compaction vec_idx to
        /// post-compaction vec_idx. Mirrors what the in-process flow
        /// does between first-pass FDR and Stage 6 — see
        /// AnalysisPipeline's "First-pass compaction" block.
        ///
        /// Compaction predicate is the UNION of (a) the local-FDR
        /// predicate (peptide_q OR protein_q pass) AND (b) every
        /// base_id that has a reconciliation action emitted by the
        /// planner. The planner runs cross-file consensus rescue
        /// (ConsensusRts.Compute), so an entry whose own file fails
        /// local first-pass FDR can still be a reconciliation target
        /// when its peptide passes FDR in a sibling file. Without the
        /// union, the local-FDR-only predicate drops those entries and
        /// their planner actions are silently dropped too -- the
        /// reconciled .scores.parquet ends up with stale Stage 4 apex_rt
        /// / bounds for the rescued entries and the blib output diverges
        /// from the in-memory straight-through pipeline.
        ///
        /// Test layout (single file, five entries):
        ///   idx 0: target id=1, peptide_q=0.005 (PASS peptide)
        ///   idx 1: decoy  id=0x80000001, base=1 (retained via target's base_id)
        ///   idx 2: target id=2, peptide_q=0.5  (FAIL local; PLANNER ACTION at (f,2))
        ///   idx 3: decoy  id=0x80000002, base=2 (retained via target's base_id from action)
        ///   idx 4: target id=3, peptide_q=0.5, protein_q=0.005 (PASS via protein-rescue)
        ///
        /// With ProteinFdr=0.01 AND the planner-action union:
        ///   local-FDR pass: base_ids {1, 3}
        ///   planner action targets: base_ids {1, 2, 3}
        ///   UNION: base_ids {1, 2, 3} — all 5 entries survive compaction.
        ///
        /// Reconciliation actions are seeded at:
        ///   (f, 0) on id=1  → remains at (f, 0) (no compaction shift)
        ///   (f, 4) on id=3  → remains at (f, 4)
        ///   (f, 2) on id=2  → remains at (f, 2) (entry survives via union)
        /// </summary>
        [TestMethod]
        public void TestRescoreCompactionUnionsActionsWithLocalFdrPredicate()
        {
            const string fileName = "f1";
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>
                {
                    MakeFdrEntryWithProteinQ(1u,           runPeptideQ: 0.005, runProteinQ: 1.0),
                    MakeFdrEntryWithProteinQ(0x80000001u,  runPeptideQ: 0.5,   runProteinQ: 1.0),
                    MakeFdrEntryWithProteinQ(2u,           runPeptideQ: 0.5,   runProteinQ: 1.0),
                    MakeFdrEntryWithProteinQ(0x80000002u,  runPeptideQ: 0.5,   runProteinQ: 1.0),
                    MakeFdrEntryWithProteinQ(3u,           runPeptideQ: 0.5,   runProteinQ: 0.005),
                }),
            };

            var actions = new Dictionary<(string, int), ReconcileAction>
            {
                { (fileName, 0), new ReconcileAction.UseCwtPeak(0, 1.0, 2.0, 3.0) },
                { (fileName, 4), new ReconcileAction.ForcedIntegration(7.0, 0.05) },
                { (fileName, 2), new ReconcileAction.UseCwtPeak(1, 5.0, 6.0, 7.0) },
            };

            var inputs = new RescoreInputs
            {
                PerFileEntries = perFile,
                ReconciliationActions = actions,
                RefinedCalibrations = new Dictionary<string, RTCalibration>(),
                PerFileGapFill = new Dictionary<string, List<GapFillTarget>>(),
                // FirstJoin's authoritative join-wide set: only base_id 1 passed
                // first-pass FDR. This test deliberately exercises the documented
                // union semantics: RescoreCompaction retains that set PLUS the
                // base_ids of reconciliation-action targets (below), pulling in 2
                // and 3, so the retained set is {1, 2, 3} -- not {1} alone.
                GlobalFirstPassBaseIds = new HashSet<uint> { 1u },
            };

            var stats = RescoreCompaction.Apply(inputs);

            // Compaction stats: planner-action union keeps base 2 alive.
            Assert.AreEqual(5, stats.EntriesBefore);
            Assert.AreEqual(5, stats.EntriesAfter);
            Assert.AreEqual(3, stats.FirstPassBaseIds);   // base_ids {1, 2, 3}
            Assert.AreEqual(0, stats.DroppedActions);     // no actions dropped

            // Per-file list unchanged (all base_ids survived).
            Assert.AreEqual(1, inputs.PerFileEntries.Count);
            var got = inputs.PerFileEntries[0].Value;
            Assert.AreEqual(5, got.Count);
            Assert.AreEqual(1u, got[0].EntryId);
            Assert.AreEqual(0x80000001u, got[1].EntryId);
            Assert.AreEqual(2u, got[2].EntryId);
            Assert.AreEqual(0x80000002u, got[3].EntryId);
            Assert.AreEqual(3u, got[4].EntryId);

            // All 3 reconciliation actions preserved.
            Assert.AreEqual(3, inputs.ReconciliationActions.Count);
            Assert.IsInstanceOfType(
                inputs.ReconciliationActions[(fileName, 0)], typeof(ReconcileAction.UseCwtPeak));
            Assert.IsInstanceOfType(
                inputs.ReconciliationActions[(fileName, 2)], typeof(ReconcileAction.UseCwtPeak));
            Assert.IsInstanceOfType(
                inputs.ReconciliationActions[(fileName, 4)], typeof(ReconcileAction.ForcedIntegration));
        }

        /// <summary>
        /// With ProteinFdr=null (--protein-fdr not passed), the
        /// protein-rescue branch is skipped entirely. An entry that
        /// would have been retained via protein rescue (peptide_q
        /// fails, protein_q passes) gets compacted away, taking any
        /// action keyed to it with it.
        /// </summary>
        [TestMethod]
        public void TestRescoreCompactionWithoutProteinFdrSkipsRescue()
        {
            const string fileName = "f1";
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>
                {
                    MakeFdrEntryWithProteinQ(1u, runPeptideQ: 0.005, runProteinQ: 1.0),
                    MakeFdrEntryWithProteinQ(2u, runPeptideQ: 0.5,   runProteinQ: 0.005),
                }),
            };

            var inputs = new RescoreInputs
            {
                PerFileEntries = perFile,
                ReconciliationActions = new Dictionary<(string, int), ReconcileAction>(),
                RefinedCalibrations = new Dictionary<string, RTCalibration>(),
                PerFileGapFill = new Dictionary<string, List<GapFillTarget>>(),
                // FirstJoin built the passing set WITHOUT the protein rescue, so
                // entry 2 (failing peptide, passing protein) is not in it.
                GlobalFirstPassBaseIds = new HashSet<uint> { 1u },
            };

            var stats = RescoreCompaction.Apply(inputs);

            // Only entry 1 passes; entry 2 dropped (no protein rescue).
            Assert.AreEqual(1, stats.EntriesAfter);
            Assert.AreEqual(1, stats.FirstPassBaseIds);
            Assert.AreEqual(1u, inputs.PerFileEntries[0].Value[0].EntryId);
        }

        /// <summary>
        /// Decoys are excluded from the predicate by design: a passing
        /// target retains its paired decoy automatically via the
        /// base_id retain step. If decoys WERE allowed in the
        /// predicate, a decoy peptide whose paired-target's protein
        /// passes protein-FDR would self-rescue via the protein-rescue
        /// clause, inflating the base_id set beyond what the
        /// in-process pipeline produces (AnalysisPipeline.cs:517 has
        /// the matching `if (entry.IsDecoy) continue;` filter; Rust
        /// rescore.rs:457 has the matching `!e.is_decoy &&`).
        ///
        /// Test layout: a single decoy with a passing protein_q AND a
        /// failing peptide_q. Without the decoy filter, the decoy's
        /// base_id would land in firstPassBaseIds via protein-rescue.
        /// With the filter, the decoy is skipped — and since no target
        /// shares its base_id, no entries survive at all.
        /// </summary>
        [TestMethod]
        public void TestRescoreCompactionSkipsDecoysInPredicate()
        {
            const string fileName = "f1";
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>
                {
                    // Lone decoy with a passing protein_q. No paired target.
                    MakeFdrEntryWithProteinQ(0x80000007u, runPeptideQ: 0.5, runProteinQ: 0.005),
                }),
            };

            var inputs = new RescoreInputs
            {
                PerFileEntries = perFile,
                ReconciliationActions = new Dictionary<(string, int), ReconcileAction>(),
                RefinedCalibrations = new Dictionary<string, RTCalibration>(),
                PerFileGapFill = new Dictionary<string, List<GapFillTarget>>(),
                // FirstJoin excludes decoys when building the set, so a lone decoy's
                // base_id is never in it -> empty set here -> decoy dropped.
                GlobalFirstPassBaseIds = new HashSet<uint>(),
            };

            var stats = RescoreCompaction.Apply(inputs);

            // Decoy's base_id is not in the global set; the base_id retain step
            // drops it.
            Assert.AreEqual(0, stats.FirstPassBaseIds);
            Assert.AreEqual(0, stats.EntriesAfter);
            Assert.AreEqual(0, inputs.PerFileEntries[0].Value.Count);
        }

        /// <summary>
        /// Helper: build a minimal FdrEntry usable in compaction tests.
        /// Sets the FDR fields the predicate inspects and leaves the
        /// rest at their defaults.
        /// </summary>
        private static FdrEntry MakeFdrEntryWithProteinQ(uint id, double runPeptideQ,
            double runProteinQ)
        {
            return new FdrEntry
            {
                EntryId = id,
                ParquetIndex = id,
                IsDecoy = (id & 0x80000000u) != 0,
                Charge = 2,
                RunPeptideQvalue = runPeptideQ,
                RunProteinQvalue = runProteinQ,
                ModifiedSequence = "PEPTIDE",
            };
        }

        #endregion

        #region TaskValiditySidecar Tests

        private const string TASK_NAME = "PerFileScoring";
        private const string TASK_VERSION = "26.6.0";

        /// <summary>
        /// Write a sidecar with a known validity_key, then confirm
        /// <see cref="TaskValiditySidecar.IsValid"/> reports true when
        /// queried with the same key. Baseline round-trip.
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarRoundTrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_rt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "out.scores.parquet");
                File.WriteAllText(output, "stub");
                const string key = "search=abc123;library=def456";

                TaskValiditySidecar.Write(output, TASK_NAME, TASK_VERSION, key,
                    new[] { Path.Combine(dir, "in.mzML"), Path.Combine(dir, "lib.tsv") });

                Assert.IsTrue(File.Exists(TaskValiditySidecar.PathFor(output, TASK_NAME)));
                Assert.IsTrue(TaskValiditySidecar.IsValid(output, TASK_NAME, key));
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key + "_modified"));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Validity keys containing quotes, backslashes, newlines, and
        /// other control characters must round-trip exactly through the
        /// JSON escape/unescape path. A naive writer would emit invalid
        /// JSON; a naive reader would scramble the key. Either failure
        /// would silently invalidate every sidecar with a path-derived
        /// key on Windows (backslashes in paths).
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarJsonEscapes()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_esc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "out.scores.parquet");
                // Mix of every escape branch in TaskValiditySidecar.JsonString:
                // quote, backslash, \b \f \n \r \t, and a sub-0x20 control
                // character ("\u0001") that exercises the \u escape branch.
                const string key = "k=\"v\";path=C:\\proj\\ai;ctrl=\b\f\n\r\t\u0001";

                TaskValiditySidecar.Write(output, TASK_NAME, TASK_VERSION, key,
                    new[] { "path with \"quotes\".mzML", "C:\\path\\with\\slashes.tsv" });

                Assert.IsTrue(TaskValiditySidecar.IsValid(output, TASK_NAME, key));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A missing sidecar must yield <c>IsValid == false</c> without
        /// throwing. "I can't tell" is the conservative answer; throwing
        /// would crash the pipeline driver on its first invocation.
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarMissingFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_miss_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "never_written.scores.parquet");
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, "any-key"));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Malformed sidecar contents (truncated mid-field, missing
        /// validity_key, raw garbage) must yield
        /// <c>IsValid == false</c> without throwing. Each shape exercises
        /// a different reader path: truncated → unterminated string;
        /// missing field → ExtractStringField returns null; garbage →
        /// the field-name needle is never found.
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarMalformedRejected()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_bad_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "out.scores.parquet");
                string sidecar = TaskValiditySidecar.PathFor(output, TASK_NAME);
                const string key = "the-key";

                // Truncated mid-key: writer wrote the validity_key opening
                // quote and a few chars, then died. Unterminated string
                // returns null (which IsValid maps to false).
                File.WriteAllText(sidecar, "{\n  \"task\": \"PerFileScoring\",\n  \"validity_key\": \"the-");
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key));

                // Missing validity_key field entirely.
                File.WriteAllText(sidecar, "{\n  \"task\": \"PerFileScoring\",\n  \"version\": \"26.5.0\"\n}\n");
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key));

                // Not-JSON garbage.
                File.WriteAllText(sidecar, "not json at all");
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key));

                // Empty file.
                File.WriteAllText(sidecar, string.Empty);
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Two tasks writing sidecars for the same output path must not
        /// trample each other. Naming includes the task name, so
        /// PerFileScoring's sidecar and PerFileRescoring's sidecar are
        /// distinct files on disk. This is the load-bearing property
        /// that lets PerFileRescore overwrite a parquet in place while
        /// PerFileScoring's "I produced this" record survives untouched.
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarPerTaskNamingCollision()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_coll_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "out.scores.parquet");
                const string scoringKey = "scoring-key";
                const string rescoreKey = "rescore-key";

                TaskValiditySidecar.Write(output, "PerFileScoring", TASK_VERSION,
                    scoringKey, new string[0]);
                TaskValiditySidecar.Write(output, "PerFileRescoring", TASK_VERSION,
                    rescoreKey, new string[0]);

                string scoringPath = TaskValiditySidecar.PathFor(output, "PerFileScoring");
                string rescorePath = TaskValiditySidecar.PathFor(output, "PerFileRescoring");
                Assert.AreNotEqual(scoringPath, rescorePath);
                Assert.IsTrue(File.Exists(scoringPath));
                Assert.IsTrue(File.Exists(rescorePath));

                // Each task's IsValid sees its own key, not the other's.
                Assert.IsTrue(TaskValiditySidecar.IsValid(output, "PerFileScoring", scoringKey));
                Assert.IsTrue(TaskValiditySidecar.IsValid(output, "PerFileRescoring", rescoreKey));
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, "PerFileScoring", rescoreKey));
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, "PerFileRescoring", scoringKey));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// <see cref="TaskValiditySidecar.Delete"/> removes an existing
        /// sidecar (subsequent IsValid → false) and is a silent no-op
        /// when the sidecar is absent. The no-op contract matters because
        /// task Run methods call Delete unconditionally before producing
        /// outputs.
        /// </summary>
        [TestMethod]
        public void TestTaskValiditySidecarDelete()
        {
            string dir = Path.Combine(Path.GetTempPath(), "task_sidecar_del_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string output = Path.Combine(dir, "out.scores.parquet");
                const string key = "k";

                TaskValiditySidecar.Write(output, TASK_NAME, TASK_VERSION, key, new string[0]);
                Assert.IsTrue(TaskValiditySidecar.IsValid(output, TASK_NAME, key));

                TaskValiditySidecar.Delete(output, TASK_NAME);
                Assert.IsFalse(File.Exists(TaskValiditySidecar.PathFor(output, TASK_NAME)));
                Assert.IsFalse(TaskValiditySidecar.IsValid(output, TASK_NAME, key));

                // Second Delete on the now-absent sidecar must not throw.
                TaskValiditySidecar.Delete(output, TASK_NAME);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        #endregion
    }
}
