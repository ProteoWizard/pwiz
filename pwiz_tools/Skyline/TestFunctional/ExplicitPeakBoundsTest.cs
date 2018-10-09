/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ExplicitPeakBoundsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestExplicitPeakBounds()
        {
            TestFilesZip = @"TestFunctional\ExplicitPeakBoundsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExplicitPeakBoundsTest.sky")));
            ImportResults(TestFilesDir.GetTestPath("CAExample" + ExtensionTestContext.ExtMz5));
            WaitForDocumentLoaded();
            var doc = SkylineWindow.Document;
            ILookup<String, PeakBounds> expectedPeakBounds = ReadPeakBounds(TestFilesDir.GetTestPath("WithExplicitBounds.blib"));
            int countWithExplicitPeak = 0;
            int countWithoutExplicitPeak = 0;
            foreach (var peptideDocNode in doc.Peptides)
            {
                var peakBounds = expectedPeakBounds[peptideDocNode.Peptide.Sequence].FirstOrDefault();
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    ChromatogramGroupInfo[] chromatograms;
                    doc.Settings.MeasuredResults.TryLoadChromatogram(
                        doc.Settings.MeasuredResults.Chromatograms.First(), peptideDocNode, transitionGroupDocNode, 0,
                        true, out chromatograms);
                    Assert.AreEqual(1, chromatograms.Length);
                    if (peakBounds == null)
                    {
                        countWithoutExplicitPeak++;
                        Assert.AreNotEqual(1, chromatograms[0].NumPeaks);
                    }
                    else
                    {
                        countWithExplicitPeak++;
                        Assert.AreEqual(1, chromatograms[0].NumPeaks);
                        var peak = chromatograms[0].GetPeaks(0).First();
                        Assert.AreEqual(peakBounds.StartTime, peak.StartTime, .1);
                        Assert.AreEqual(peakBounds.EndTime, peak.EndTime, .1);
                    }
                }
            }
            Assert.AreNotEqual(0, countWithExplicitPeak);
            Assert.AreNotEqual(0, countWithoutExplicitPeak);
        }

        private ILookup<String, PeakBounds> ReadPeakBounds(string blibFile)
        {
            var entries = new List<Tuple<string, PeakBounds>>();
            using (var connection = new SQLiteConnection(
                SessionFactoryFactory.SQLiteConnectionStringBuilderFromFilePath(blibFile).ToString())
                .OpenAndReturn())
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT s.peptideSeq, r.startTime, r.endTime\n" +
                                      "FROM RefSpectra s LEFT JOIN RetentionTimes r ON s.id = r.RefSpectraID\n" +
                                      "WHERE r.startTime IS NOT NULL AND r.endTime IS NOT NULL";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entries.Add(Tuple.Create(reader.GetString(0), 
                                new PeakBounds(reader.GetDouble(1), reader.GetDouble(2))));
                        }
                    }
                }
            }
            return entries.ToLookup(tuple => tuple.Item1, tuple => tuple.Item2);
        }
    }
}
