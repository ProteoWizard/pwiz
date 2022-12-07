/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing peak boundaries when there are multi-sample WIFF files, and the
    /// file path and sample name are needed to specify the ResultFile.
    /// </summary>
    [TestClass]
    public class MultiSampleImportPeakBoundariesTest : AbstractFunctionalTestEx
    {
        private const double PEAK_WIDTH = 0.75;

        [TestMethod]
        public void TestMultiSampleImportPeakBoundaries()
        {
            TestFilesZip = @"TestFunctional\MultiSampleImportPeakBoundariesTest.zip";
            RunFunctionalTest();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SimpleWiffTest.sky")));

            // Import results from 2 wiff files ("firstfile.wiff" and "secondfile.wiff")
            // the wiff files are identical, and have 4 samples in them:
            // "blank" "rfp9_after_h_1" "test" "rfp9_before_h_1"

            // Import all of the samples from "firstfile.wiff" into a single replicate named "ReplicateOne"
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.ReplicateName = "ReplicateOne";
            });
            ConfirmAction<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDlg =>
            {
                openDataSourceDlg.SelectFile(TestFilesDir.GetTestPath("firstfile.wiff"));
                openDataSourceDlg.Open();
            });
            var importResultsSamplesDlg = WaitForOpenForm<ImportResultsSamplesDlg>();
            var docBefore = SkylineWindow.Document;
            OkDialog(importResultsSamplesDlg, importResultsSamplesDlg.OkDialog);
            WaitForDocumentChangeLoaded(docBefore);
            Assert.AreEqual(1, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);

            // Import the first two samples from "secondfile.wiff" into a replicate named "ReplicateTwo"
            importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.ReplicateName = "ReplicateTwo";
            });
            ConfirmAction<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDlg =>
            {
                openDataSourceDlg.SelectFile(TestFilesDir.GetTestPath("secondfile.wiff"));
                openDataSourceDlg.Open();
            });
            importResultsSamplesDlg = WaitForOpenForm<ImportResultsSamplesDlg>();
            RunUI(() =>
            {
                importResultsSamplesDlg.ExcludeSample(2);
                importResultsSamplesDlg.ExcludeSample(3);
            });
            OkDialog(importResultsSamplesDlg, importResultsSamplesDlg.OkDialog);
            WaitForCondition(() => 2 == SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
            WaitForDocumentLoaded();
            Assert.AreEqual(2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);

            // Import the second and third samples from "secondfile.wiff" into a replicate named "ReplicateThree"
            importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                importResultsDlg.ReplicateName = "ReplicateThree";
            });
            ConfirmAction<OpenDataSourceDialog>(importResultsDlg.OkDialog, openDataSourceDlg =>
            {
                openDataSourceDlg.SelectFile(TestFilesDir.GetTestPath("secondfile.wiff"));
                openDataSourceDlg.Open();
            });
            importResultsSamplesDlg = WaitForOpenForm<ImportResultsSamplesDlg>();
            RunUI(() =>
            {
                importResultsSamplesDlg.ExcludeSample(0);
                importResultsSamplesDlg.ExcludeSample(1);
            });
            OkDialog(importResultsSamplesDlg, importResultsSamplesDlg.OkDialog);
            WaitForCondition(() => 3 == SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
            WaitForDocumentLoaded();
            Assert.AreEqual(3, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);

            var peakBoundariesFile = TestFilesDir.GetTestPath("PeakBoundaries.tsv");
            using (var fileWriter = new StreamWriter(peakBoundariesFile))
            {
                WritePeakBoundariesFile(SkylineWindow.Document, fileWriter);
            }
            RunUI(()=>SkylineWindow.ImportPeakBoundariesFile(peakBoundariesFile));

            VerifyPeakBoundaries(SkylineWindow.Document);
        }

        /// <summary>
        /// Return a number to use as the peak boundary based on the peptide sequence, replicate name, and 
        /// sample name. This number is then used to verify that the peak boundaries were applied correctly.
        /// </summary>
        public double GetPeakStartTime(PeptideDocNode peptide, ChromatogramSet chromatogramSet, MsDataFileUri msDataFileUri)
        {
            double time = peptide.Peptide.Sequence.Length;

            var replicateTimes = new Dictionary<string, double>()
                {{"ReplicateOne", 1}, {"ReplicateTwo", 0}, {"ReplicateThree", 2}};
            time += replicateTimes[chromatogramSet.Name];

            var sampleTimes = new Dictionary<string, double>()
                {{"blank", 0}, {"rfp9_after_h_1", -.25}, {"test", .3}, {"rfp9_before_h_1", .4}};
            time += sampleTimes[msDataFileUri.GetSampleName()];

            return time;
        }

        /// <summary>
        ///  Writes out a peak boundaries file using the numbers decided on by <see cref="GetPeakStartTime" />.
        /// </summary>
        public void WritePeakBoundariesFile(SrmDocument document, TextWriter writer)
        {
            writer.WriteLine(TabSeparate("ModifiedSequence", "FileName", "MinStartTime", "MaxEndTime", "SampleName"));
            foreach (var peptideDocNode in document.Peptides)
            {
                foreach (var replicate in document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (var msDataFileInfo in replicate.MSDataFileInfos)
                    {
                        var startTime = GetPeakStartTime(peptideDocNode, replicate, msDataFileInfo.FilePath);
                        var endTime = startTime + PEAK_WIDTH;
                        writer.WriteLine(TabSeparate(peptideDocNode.ModifiedSequence, msDataFileInfo.FilePath.GetFilePath(), startTime, endTime, msDataFileInfo.FilePath.GetSampleName()));
                    }
                }
            }
        }

        /// <summary>
        /// Verify that the start and end times on all of the peak boundaries are what <see cref="GetPeakStartTime"/>
        /// says they should be.
        /// </summary>
        public void VerifyPeakBoundaries(SrmDocument document)
        {
            foreach (var peptideDocNode in document.Peptides)
            {
                foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
                {
                    var results = transitionGroupDocNode.Results;
                    for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
                    {
                        var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[replicateIndex];
                        foreach (var transitionGroupChromInfo in results[replicateIndex])
                        {
                            var chromFileInfo = chromatogramSet.GetFileInfo(transitionGroupChromInfo.FileId);
                            var expectedStartTime =
                                GetPeakStartTime(peptideDocNode, chromatogramSet, chromFileInfo.FilePath);
                            var expectedEndTime = expectedStartTime + PEAK_WIDTH;
                            Assert.AreEqual(expectedStartTime, transitionGroupChromInfo.StartRetentionTime.Value, .01);
                            Assert.AreEqual(expectedEndTime, transitionGroupChromInfo.EndRetentionTime.Value, .01);
                        }
                    }

                }
            }

        }

        private string TabSeparate(params object[] values)
        {
            return string.Join("\t",
                values.Select(value => DsvWriter.ToDsvField('\t', (value ?? string.Empty).ToString())));
        }
    }
}
