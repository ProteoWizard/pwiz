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
using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for OspreySharp.IO types: BlibWriter, SpectraCache, MzmlReader, and ParquetScoreCache.
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
                        long numSpecs = (long)cmd.ExecuteScalar();
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
                        string storedModSeq = (string)cmd.ExecuteScalar();
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
                LibraryCache.SaveCache(tempPath, entries);
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
                    CollectionAssert.AreEqual(orig.ProteinIds, copy.ProteinIds);
                    CollectionAssert.AreEqual(orig.GeneNames, copy.GeneNames);

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

                        var origNl = orig.Fragments[f].Annotation.NeutralLoss;
                        var copyNl = copy.Fragments[f].Annotation.NeutralLoss;
                        if (origNl == null)
                            Assert.IsNull(copyNl);
                        else
                        {
                            Assert.IsNotNull(copyNl);
                            Assert.AreEqual(origNl.Mass, copyNl.Mass, 1e-10);
                        }
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
        public void TestLibraryCacheEmpty()
        {
            var entries = new List<LibraryEntry>();

            string tempPath = Path.Combine(Path.GetTempPath(),
                "osprey_test_empty_" + Guid.NewGuid().ToString("N") + ".libcache");

            try
            {
                LibraryCache.SaveCache(tempPath, entries);
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
            string path = Path.GetTempFileName() + ".parquet";
            try
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
                ParquetScoreCache.WriteScoresParquet(path, entries, metadata);
                Assert.IsTrue(File.Exists(path), "Parquet file should exist");

                // Read FDR stubs
                var stubs = ParquetScoreCache.LoadFdrStubsFromParquet(path);
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

                // Read PIN features
                var features = ParquetScoreCache.LoadPinFeaturesFromParquet(path);
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
                Assert.IsTrue(ParquetScoreCache.ValidateMetadata(path, metadata));
                var wrongMeta = new Dictionary<string, string>
                {
                    { "osprey.version", "2.0.0" },
                };
                Assert.IsFalse(ParquetScoreCache.ValidateMetadata(path, wrongMeta));
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
                    System.Globalization.CultureInfo.InvariantCulture,
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
                System.Globalization.CultureInfo.InvariantCulture,
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
                        NeutralLoss = NeutralLoss.H2O
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
                        NeutralLoss = NeutralLoss.Custom(98.0)
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
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", conn))
            {
                cmd.Parameters.AddWithValue("@name", tableName);
                long count = (long)cmd.ExecuteScalar();
                Assert.AreEqual(1L, count, "Table " + tableName + " should exist");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                // Also clean up SQLite WAL/SHM files
                string walPath = path + "-wal";
                string shmPath = path + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        #endregion
    }
}
