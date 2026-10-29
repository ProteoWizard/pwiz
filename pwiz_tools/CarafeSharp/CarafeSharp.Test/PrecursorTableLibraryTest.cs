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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The prediction of a precursor table (<see cref="LibraryGenerator.PrecursorTablePath"/>): the
    /// table's precursors, with their modification sites and charges, come out as a .blib alone,
    /// each with its retention time, its CCS and its 1/K0, under Carafe's fragment rules.
    /// </summary>
    [TestClass]
    public class PrecursorTableLibraryTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestPredictPrecursorTable()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"PrecursorTable_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(folder);
            try
            {
                string tablePath = Path.Combine(folder, @"input.tsv");
                // The columns in another order than the table's, with a blank line, as a reader must allow;
                // a modification on a residue, and one on the N terminus, which Skyline places on residue 1
                File.WriteAllLines(tablePath, new[]
                {
                    string.Join("\t", PrecursorTable.CHARGE, PrecursorTable.SEQUENCE, PrecursorTable.MODS, PrecursorTable.MOD_SITES),
                    string.Join("\t", @"2", @"LGGNEQVTR", string.Empty, string.Empty),
                    string.Join("\t", @"2", @"ACDEFGHIK", @"Carbamidomethyl@C", @"2"),
                    string.Empty,
                    string.Join("\t", @"3", @"QPEPTIDEK", @"Gln->pyro-Glu@Q^Any_N-term", @"0"),
                });
                string outputPath = Path.Combine(folder, @"output_libs", @"predict.blib");
                var log = new StringWriter();
                var settings = new LibrarySettings
                {
                    Device = TorchDevice.CPU,
                    OutputFile = outputPath,
                    OutputDirectory = Path.GetDirectoryName(outputPath),
                };
                var generator = new LibraryGenerator(settings, log) { PrecursorTablePath = tablePath };
                generator.Run();

                Assert.AreEqual(3, generator.SpectrumCount, log.ToString());
                Assert.AreEqual(outputPath, generator.BlibPath);
                Assert.IsNull(generator.TsvPath, @"a precursor table's library is a .blib alone");
                Assert.IsTrue(File.Exists(outputPath), outputPath);
                StringAssert.Contains(log.ToString(), @"Predicting 3 precursors (3 peptide forms)");
                StringAssert.Contains(log.ToString(), @"Library RT: iRT = ");

                using (var connection = new SQLiteConnection(@"Data Source=" + outputPath + @";Version=3;Read Only=True"))
                {
                    connection.Open();
                    Assert.AreEqual(3L, Scalar(connection, @"SELECT numSpecs FROM LibInfo"));
                    var rows = Rows(connection, @"SELECT peptideSeq, peptideModSeq, precursorCharge, precursorMz, retentionTime, " +
                                                @"collisionalCrossSectionSqA, ionMobility, numPeaks FROM RefSpectra ORDER BY id");
                    // The table's order, its charges, and Skyline's mass-shift notation from alphabase's masses
                    CollectionAssert.AreEqual(new[] { @"LGGNEQVTR", @"ACDEFGHIK", @"QPEPTIDEK" }, rows.Select(r => (string)r[0]).ToArray());
                    CollectionAssert.AreEqual(new[] { 2L, 2L, 3L }, rows.Select(r => (long)r[2]).ToArray());
                    Assert.AreEqual(@"LGGNEQVTR", (string)rows[0][1]);
                    StringAssert.StartsWith((string)rows[1][1], @"AC[+57.02");
                    StringAssert.StartsWith((string)rows[2][1], @"Q[-17.02");
                    foreach (var row in rows)
                    {
                        Assert.IsTrue((double)row[3] > 200, @"precursor m/z");
                        Assert.IsTrue((double)row[4] != 0, @"retention time");
                        Assert.IsTrue((double)row[5] > 0, @"CCS");
                        Assert.IsTrue((double)row[6] > 0, @"1/K0");
                        long peaks = (long)row[7];
                        Assert.IsTrue(peaks >= settings.MinFragments && peaks <= settings.TopFragments, @"peaks: " + peaks);
                    }
                    CollectionAssert.AreEqual(new[] { @"2:2", @"3:1" },
                        Rows(connection, @"SELECT RefSpectraID || ':' || position FROM Modifications ORDER BY RefSpectraID").Select(r => (string)r[0]).ToArray());
                    Assert.AreEqual(57.0215, (double)Scalar(connection, @"SELECT mass FROM Modifications WHERE RefSpectraID = 2"), 1e-3);
                    Assert.AreEqual(-17.0265, (double)Scalar(connection, @"SELECT mass FROM Modifications WHERE RefSpectraID = 3"), 1e-3);
                    // No proteins, which the table does not name; the source file is the library
                    Assert.AreEqual(0L, Scalar(connection, @"SELECT COUNT(*) FROM Proteins"));
                    Assert.AreEqual(@"predict", Scalar(connection, @"SELECT fileName FROM SpectrumSourceFiles"));
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static object Scalar(SQLiteConnection connection, string sql)
        {
            using var command = new SQLiteCommand(sql, connection);
            return command.ExecuteScalar();
        }

        private static List<object[]> Rows(SQLiteConnection connection, string sql)
        {
            var rows = new List<object[]>();
            using var command = new SQLiteCommand(sql, connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                rows.Add(row);
            }
            return rows;
        }
    }
}
