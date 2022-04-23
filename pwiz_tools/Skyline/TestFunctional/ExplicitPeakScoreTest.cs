/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using System.Linq;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestFunctional
{
    // ReSharper disable AccessToModifiedClosure
    [TestClass]
    public class ExplicitPeakScoreTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExplicitPeakScore()
        {
            TestFilesZip = @"TestFunctional\ExplicitPeakScoreTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var fileToImport = TestFilesDir.GetTestPath("ExplicitPeakScoreTest.mzML");
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ExplicitPeakScoreTest.sky")));
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryname = "OpenSwathExplicitPeakScoreTest";
            RunUI(()=>
            {
                buildLibraryDlg.LibraryName = libraryname;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath(libraryname + ".blib");
                buildLibraryDlg.LibraryBuildAction = LibraryBuildAction.Create;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDir.GetTestPath("explicitpeakscoretest.tsv") });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            Assert.IsTrue(WaitForCondition(()=>peptideSettings.AvailableLibraries.Contains(libraryname)));
            OkDialog(peptideSettings, peptideSettings.OkDialog);
            WaitForDocumentLoaded();
            ImportResultsFile(fileToImport);
            var precursorsWithLibInfo = SkylineWindow.Document.PeptidePrecursorPairs.Where(entry => entry.NodeGroup.HasLibInfo)
                .Select(entry=>entry.NodePep.Peptide.Sequence).ToArray();
            Assert.AreNotEqual(0, precursorsWithLibInfo.Length);
            var precursorsWithQValue =
                SkylineWindow.Document.PeptidePrecursorPairs.Where(entry => entry.NodeGroup.Results.First().First().QValue.HasValue)
                .Select(entry=>entry.NodeGroup.Peptide.Sequence).ToArray();
            CollectionAssert.AreEquivalent(precursorsWithLibInfo, precursorsWithQValue);

            // Save the document so we can turn off "Use explicit peak bounds" 
            // (it calls "SrmSettings.UpdateLists" which copies the "AnyExplicitBounds" over to the LibrarySpec).
            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(()=>SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
            
            Assert.IsTrue(SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0].UseExplicitPeakBounds);

            peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editLibraryList = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettings.EditLibraryList);
            RunUI(()=>editLibraryList.SelectItem(libraryname));
            var editLibraryDlg = ShowDialog<EditLibraryDlg>(editLibraryList.EditItem);
            RunUI(() =>
            {
                Assert.IsTrue(editLibraryDlg.CbxUseExplicitPeakBounds.Enabled);
                Assert.IsTrue(editLibraryDlg.CbxUseExplicitPeakBounds.Checked);
                editLibraryDlg.CbxUseExplicitPeakBounds.Checked = false;
            });
            OkDialog(editLibraryDlg, editLibraryDlg.OkDialog);
            OkDialog(editLibraryList, editLibraryList.OkDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            WaitForDocumentLoaded();
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0].UseExplicitPeakBounds);

            var manageResultsDialog = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(()=>manageResultsDialog.RemoveAllReplicates());
            OkDialog(manageResultsDialog, manageResultsDialog.OkDialog);
            RunUI(()=>SkylineWindow.SaveDocument());
            ImportResultsFile(fileToImport);
            WaitForDocumentLoaded();

            var newPrecursorsWithLibInfo = SkylineWindow.Document.PeptidePrecursorPairs.Where(entry => entry.NodeGroup.HasLibInfo)
                .Select(entry => entry.NodePep.Peptide.Sequence).ToArray();
            var newPrecursorsWithQValue =
                SkylineWindow.Document.PeptidePrecursorPairs.Where(entry => entry.NodeGroup.Results.First().First().QValue.HasValue)
                    .Select(entry => entry.NodeGroup.Peptide.Sequence).ToArray();
            CollectionAssert.AreEqual(precursorsWithLibInfo, newPrecursorsWithLibInfo);
            Assert.AreEqual(0, newPrecursorsWithQValue.Length);
        }
    }
}
