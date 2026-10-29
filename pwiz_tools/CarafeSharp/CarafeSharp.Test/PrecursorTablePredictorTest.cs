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
using System.Globalization;
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
    /// The prediction of a precursor table (<see cref="PrecursorTablePredictor"/>): the table's
    /// precursors, with their modification sites and charges, come out as peptdeep's library TSV
    /// rows, with its fragment rules.
    /// </summary>
    [TestClass]
    public class PrecursorTablePredictorTest
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
                // The columns in another order than the predictor's, with a blank line, as a reader must allow
                File.WriteAllLines(tablePath, new[]
                {
                    string.Join("\t", PrecursorTable.CHARGE, PrecursorTable.SEQUENCE, PrecursorTable.MODS, PrecursorTable.MOD_SITES),
                    string.Join("\t", @"2", @"LGGNEQVTR", string.Empty, string.Empty),
                    string.Join("\t", @"2", @"ACDEFGHIK", @"Carbamidomethyl@C", @"2"),
                    string.Empty,
                    string.Join("\t", @"3", @"QPEPTIDEK", @"Gln->pyro-Glu@Q^Any_N-term", @"0"),
                });
                string outputPath = Path.Combine(folder, @"output_libs", @"predict.speclib.tsv");
                var log = new StringWriter();
                var settings = new LibrarySettings { Device = TorchDevice.CPU };
                var predictor = new PrecursorTablePredictor(settings, tablePath, outputPath, log);
                predictor.Run();

                Assert.AreEqual(3, predictor.PrecursorCount, log.ToString());
                // peptdeep's library defaults applied, since the command line gave none
                Assert.AreEqual(PrecursorTablePredictor.DEFAULT_NCE, settings.Nce);
                Assert.AreEqual(PrecursorTablePredictor.DEFAULT_INSTRUMENT, settings.Instrument);
                StringAssert.Contains(log.ToString(), @"Library RT: iRT = ");

                var lines = File.ReadAllLines(outputPath);
                Assert.AreEqual(string.Join("\t", PrecursorTablePredictor.OUTPUT_COLUMNS), lines[0]);
                var rows = lines.Skip(1).Select(l => l.Split('\t')).ToList();
                Assert.IsTrue(rows.All(r => r.Length == PrecursorTablePredictor.OUTPUT_COLUMNS.Count));
                var byPrecursor = rows.GroupBy(r => r[0]).ToDictionary(g => g.Key, g => g.ToList());
                CollectionAssert.AreEquivalent(new[] { @"_LGGNEQVTR_", @"_AC[UniMod:4]DEFGHIK_", @"_Q[UniMod:28]PEPTIDEK_" }, byPrecursor.Keys.ToList());
                foreach (var precursor in byPrecursor)
                {
                    var fragments = precursor.Value;
                    int charge = int.Parse(fragments[0][1], CultureInfo.InvariantCulture);
                    Assert.AreEqual(precursor.Key.StartsWith(@"_Q") ? 3 : 2, charge);
                    Assert.IsTrue(fragments.Count <= 12 && fragments.Count > 0, precursor.Key);
                    // One RT, one CCS and one precursor m/z per precursor; decoy 0
                    Assert.AreEqual(1, fragments.Select(f => f[2]).Distinct().Count());
                    Assert.IsTrue(double.Parse(fragments[0][3], CultureInfo.InvariantCulture) > 0, @"CCS");
                    Assert.IsTrue(double.Parse(fragments[0][5], CultureInfo.InvariantCulture) > 200, @"precursor m/z");
                    Assert.IsTrue(fragments.All(f => f[6] == @"0"));
                    // Intensities renormalized to the most intense, in decreasing order, b and y of charge 1 and 2
                    var intensities = fragments.Select(f => double.Parse(f[9], CultureInfo.InvariantCulture)).ToList();
                    Assert.AreEqual(1.0, intensities[0], 1e-6);
                    for (int i = 1; i < intensities.Count; i++)
                        Assert.IsTrue(intensities[i] <= intensities[i - 1] && intensities[i] > 0, precursor.Key + @" row " + i);
                    Assert.IsTrue(fragments.All(f => f[7] == @"b" || f[7] == @"y"));
                    Assert.IsTrue(fragments.All(f => int.Parse(f[10], CultureInfo.InvariantCulture) <= Math.Min(2, charge)));
                    Assert.IsTrue(fragments.All(f => f[12] == @"noloss"));
                    double maxMz = fragments.Max(f => double.Parse(f[8], CultureInfo.InvariantCulture));
                    Assert.IsTrue(maxMz <= 2000 && fragments.Min(f => double.Parse(f[8], CultureInfo.InvariantCulture)) >= 200);
                }
                // A tryptic peptide fragments mostly to y ions, which the most intense fragment is
                Assert.AreEqual(@"y", byPrecursor[@"_LGGNEQVTR_"][0][7]);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
