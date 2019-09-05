/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker PASEF in concert with Mascot.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerDiaPasefTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void BrukerDiaPasefImportTest()
        {
             RunPerfTests = true; // Uncomment this to force test to run in IDE TODO re-comment this
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportBrukerDiaPasef.zip";
            TestFilesPersistent = new[] { ".d" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();

            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        protected override void DoTest()
        {
            
            // Load a .sky with mising .skyd, forcing re-import with existing parameters
            // This simplifies the test code since we have six different PASEF modes to deal with here
            string skyfile = TestFilesDir.GetTestPath("snipped.sky");
            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                SkylineWindow.OpenFile(skyfile);
            });

            var doc1 = WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            AssertEx.IsDocumentState(doc1, null, 1, 34, 34, 204);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out var chromGroupInfo));

                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
            }
            Assert.AreEqual(205249.594, maxHeight, 1); 

            // Does CCS show up in reports?
            TestReports(doc1);

        }

        private void TestReports(SrmDocument doc1, string msg = null)
        {
            // Verify reports working for CCS
            var row = 0;
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            EnableDocumentGridColumns(documentGrid,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                doc1.PeptideTransitionCount,
                new[]
                {
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityMS1",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityFragment",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityUnits",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityWindow"
                });
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, 1.1732, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityFragment", row, 1.1732, msg); 
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityValue.GetUnitsString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.12, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 666.9175, msg);

            EnableDocumentGridColumns(documentGrid,
                Resources.ReportSpecList_GetDefaults_Peptide_RT_Results,
                doc1.PeptideCount * doc1.MeasuredResults.Chromatograms.Count, null);
            foreach (var rt in new[] {
                14.36,14.33,14.35,14.33,14.33,14.33,14.15,14.1,14.15,14.12,14.1,14.12,14.64,14.6,14.63,14.6,14.61,
                14.61,14.76,14.69,14.75,14.72,14.73,14.72,14.06,14.51,14.06,14.06,14.04,14.03,14.44,14.4,14.43,14.42,
                14.42,14.43,14.37,14.36,14.36,14.36,14.35,14.35,14.31,14.27,14.31,14.3,14.28,14.28,14.5,14.47,14.48,
                14.48,14.47,14.48,14.7,14.65,14.69,14.66,14.66,14.67,14.38,14.36,14.36,14.36,14.35,14.35,14.25,14.24,
                14.25,14.24,14.22,14.23,14.38,14.32,14.37,14.36,14.35,14.35,14.54,14.49,14.51,14.51,14.5,14.51,14.24,
                14.22,14.24,14.24,14.22,14.23,14.81,14.77,14.81,14.78,14.78,14.79,14.64,14.6,14.63,14.63,14.61,14.62,
                14.5,14.49,14.48,14.48,14.47,14.46,14.53,14.47,14.52,14.51,14.5,14.49,14.68,14.65,14.67,14.66,14.65,
                14.66,14.47,14.41,14.46,14.45,14.43,14.44,14.45,14.42,14.44,14.42,14.42,14.43,14.24,14.22,14.24,14.24,
                14.25,14.25,14.48,14.43,14.48,14.46,14.45,14.44,14.2,14.14,14.19,14.18,14.17,14.17,14.39,14.35,14.38,
                14.36,14.35,14.36,14.89,14.87,14.88,14.87,14.86,14.85,14.23,14.2,14.22,14.21,14.2,14.21,14.21,14.18,
                14.19,14.18,14.19,14.18,14.11,14.09,14.11,14.09,14.08,14.08,14.73,14.68,14.72,14.69,14.69,14.71,14.65,
                14.6,14.64,14.6,14.61,14.61,14.13,14.09,14.12,14.12,14.1,14.1,14.23,14.19,14.23,14.21,14.2,14.2
            })
            {
                CheckFieldByName(documentGrid, "PeptideRetentionTime", row++, rt, msg);
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, double? expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as double?;
                Assert.AreEqual(expected.HasValue, val.HasValue, name + (msg ?? string.Empty));
                Assert.AreEqual(expected ?? 0, val ?? 0, 0.005, name + (msg ?? string.Empty));
            });
        }

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, string expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as string;
                Assert.AreEqual(expected, val, name + (msg ?? string.Empty));
            });
        }
    }
}
