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

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that when if you change the Transition Full Scan settings and do a reimport,
    /// peaks are integrated using the new settings.
    /// </summary>
    [TestClass]
    public class SettingsChangeReimportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSettingsChangeReimport()
        {
            TestFilesZip = @"TestFunctional\SettingsChangeReimportTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SettingsChangeReimportTest.sky")));
            var idPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
            var transitionGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(idPath);
            Assert.IsNull(transitionGroup.Results);

            // Import the file with "UseSelectiveExtraction" set to "false"
            Assert.IsFalse(SkylineWindow.Document.Settings.TransitionSettings.FullScan.UseSelectiveExtraction);
            ImportResultsFile(TestFilesDir.GetTestPath("S_1.mzML"));
            transitionGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(idPath);
            Assert.IsNotNull(transitionGroup.Results);
            Assert.AreEqual(1, transitionGroup.Results.Count);
            var transitionGroupChromInfo = transitionGroup.Results[0].First();
            // Verify the peak area is what we expect
            Assert.AreEqual(1.460189E+08f, transitionGroupChromInfo.Area.Value);

            // Reimport the file with "UseSelectiveExtraction" set to "true"
            RunUI(() => SkylineWindow.ModifyDocument("Change selective extraction",
                doc => doc.ChangeSettings(doc.Settings.ChangeTransitionSettings(
                    doc.Settings.TransitionSettings.ChangeFullScan(doc.Settings.TransitionSettings.FullScan
                        .ChangeUseSelectiveExtraction(true)))),
                AuditLogEntry.SettingsLogFunction));
            Assert.IsTrue(SkylineWindow.Document.Settings.TransitionSettings.FullScan.UseSelectiveExtraction);

            // TODO (nicksh): Update the timestamp on the .mzML file so that ChromatogramSet.CalcCacheFlags notices
            // that the file is different.
            // This should be removed once we have a robust way of noticing that a file has been reimported
            File.SetLastWriteTimeUtc(TestFilesDir.GetTestPath("S_1.mzML"), DateTime.UtcNow);

            // Do a reimport, and make sure that the manually chosen peak boundary remains
            var document = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms.ToArray();
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            WaitForDocumentChange(document);
            WaitForDocumentLoaded();
            transitionGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(idPath);
            Assert.IsNotNull(transitionGroup.Results);
            Assert.AreEqual(1, transitionGroup.Results.Count);
            transitionGroupChromInfo = transitionGroup.Results[0].First();
            // Verify that the peak area is a smaller number because the chromatogram extraction was more selective
            Assert.AreEqual(119880880f, transitionGroupChromInfo.Area.Value);
        }
    }
}
