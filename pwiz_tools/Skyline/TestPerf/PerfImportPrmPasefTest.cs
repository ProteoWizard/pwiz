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
    /// Verify consistent import of Bruker PRM PASEF, in particular the handling of heavy modifications in library lookup of IM values.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerPrmPasefTest : AbstractFunctionalTestEx
    {
        private bool IsRecordMode { get { return false; } }

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void BrukerPrmPasefImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportBrukerPrmPasef.zip";
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
            
            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            var doc = SkylineWindow.Document;
            OpenDocument("PRM_LIH_01102019.sky");
            var doc1 = WaitForDocumentChange(doc);

            ImportResults("PRM_LIH1250_Slot1-20_1_6588.d");
            doc1 = WaitForDocumentChangeLoaded(doc1, 15 * 60 * 1000); // 15 minutes
            AssertEx.IsDocumentState(doc1, null, 254, 253, 305, 3123);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);
            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                if (results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out var chromGroupInfo))
                {
                    foreach (var chromGroup in chromGroupInfo)
                    {
                        foreach (var tranInfo in chromGroup.TransitionPointSets)
                        {
                            maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                        }
                    }
                }
            }
            Assert.AreEqual(472345.4375, maxHeight, 1); 

            // Does CCS show up in reports?
            TestReports(doc1);

        }

        private string RemoveReplicateReference(string text, string replicateName)
        {
            // Remove reference to replicate with file type that we don't need to handle at this time
            var open = text.IndexOf(string.Format("<replicate name=\"{0}\">", replicateName), StringComparison.Ordinal);
            var close = text.IndexOf("</replicate>", open, StringComparison.Ordinal) + 12;
            var snip = text.Substring(0, open) + text.Substring(close);
            while ((open = snip.IndexOf(replicateName, StringComparison.Ordinal)) != -1)
            {
                open = snip.LastIndexOf('<', open);
                close = snip.IndexOf(">", open, StringComparison.Ordinal);
                snip = snip.Substring(0, open) + snip.Substring(close + 1);
            }
            return snip;
        }


        private void TestReports(SrmDocument doc1, string msg = null)
        {
            // Verify reports working for CCS
            var row = 10;
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
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, .97, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityFragment", row, .97, msg); 
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityValue.GetUnitsString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.03, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 392.02, msg);
            EnableDocumentGridColumns(documentGrid,
                Resources.ReportSpecList_GetDefaults_Peptide_RT_Results,
                210, null);
            var rts = new double?[] {
                12.45, 21.48, 16.93, 22.93, 13.63, 19.12, 28.97, 14.88, 14.22, 27.25, 14.97, 14.26, 25.7, 15.06, 11.93, 26.37, 12.89, 15.88,
                18.34, 11.16, 10.46, 10.98, 28.02, 24.01, 11.15, 18.97, 23.33, 26.56, 11.94, 19, 24.2, 23.42, 26.1, 27.86, 27.76, 20.99, 26.15,
                21.16, 25.99, 14.29, 26.04, 15.04, 24.07, 28.15, 20.1, 26.03, 15.07, 19.78, 27.2, 21.26, 25.39, 25.04, 11.71, 21.1, 20.51,
                14.71, 25.24, 24.9, 28.57, 21.13, 17.19, 15.39, 18.08, 16.06, 26.11, 10.26, 13.11, 9.46, 10.78, 14.42, 25.16, 26.36, 17.21,
                24.79, 13.9, 20.63, 19.7, 22.5, 13.97, 19.34, 19.05, 18.45, 13.96, 6.59, 26.12, 17.76, 28.31, 24.27, 29.25, 27.56, 23.72,
                12.09, 8.92, 18.39, 20.23, 26.42, 28.45, 22.17, 15.17, 24.73, 28.22, 17.16, 9.59, 13.84, 22.91, 17.14, 19.92, 24.24, 27.82,
                null, 10.41, 12.27, 21.21, 18.69, 12.68, 21.09, 22.11, 21.43, 14.51, 17.31, 21.92, 22.43, 19.53, 25.24, 25.31, 11.61, 25.79,
                10.07, 24.29, 23.28, 15.83, 14.87, 28.53, 14.85, 28.5, 22.49, 19.35, 24.81, 24.75, 24.63, 24.36, 15.46, 22.7, 19.85, 22.56,
                21.91, 15.97, 20.82, 13.62, 19.52, 15.55, 23.71, 30.35, 19.57, 13.57, 29.08, 15.56, 21.73, 16.05, 13.95, 25.39, 14.3, 25.3,
                15.72, 10.48, 9.88, 21.31, 9.92, 26.56, 27.89, 29.85, 22.12, 25.06, 21.53, 15.25, 21.13, 22.81, 20.44, 18.23, 11.49, 25.59,
                20.13, 16.32, 13.19, 9.63, 12.11, 21.84, 10.83, 12.49, 16.99, 18.83, 11.71, 12.84, 17.31, 11.13, 12.88, 24.35, 16.83, 15.26,
                22.72, 8.45, 11.17, 18.41, 18.85, 22.18, 16.56, 10.02, 10.13, 25.91, 29.07
            };
            for (row = 0; row < rts.Length; row++)
            {
                var rt = rts[row];

                // CONSIDER: No idea why these differ in uncombined mode, but they do
                if (MsDataFileImpl.ForceUncombinedIonMobility && (rt == 28.02 || rt == 22.81))
                    continue;

                CheckFieldByName(documentGrid, "PeptideRetentionTime", row, rt, msg, true);
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

        private static int ValuesRecordedCount;

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, double? expected, string msg = null, bool recordValues = false)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as double?;
                Assert.AreEqual(expected.HasValue, val.HasValue, name + (msg ?? string.Empty));
                if (!IsRecordMode || !recordValues)
                    Assert.AreEqual(expected ?? 0, val ?? 0, 0.005, name + (msg ?? string.Empty));
                else
                {
                    Console.Write(@"{0:0.##}, ", val);
                    if (++ValuesRecordedCount % 18 == 0)
                        Console.WriteLine();
                }
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
