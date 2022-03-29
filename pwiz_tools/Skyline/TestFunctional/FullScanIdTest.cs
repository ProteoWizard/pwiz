/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.BiblioSpec;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for full-scan MS1 filtering with peptide IDs.
    /// </summary>
    [TestClass]
    public class FullScanIdTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFullScanId()
        {
            TestFilesZip = @"TestFunctional\FullScanIdTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var docInitial = SkylineWindow.Document;

            // Set up necessary modifications
            var peptideSettingsUI = ShowPeptideSettings();
            const string oxidationMName = "Oxidation (M)";
            AddStaticMod(oxidationMName, true, peptideSettingsUI);
            RunUI(() =>
                      {
                          peptideSettingsUI.PickedStaticMods = new[] {oxidationMName};
                          peptideSettingsUI.MissedCleavages = 1;
                      });

            // Build the library from the pepXML
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);

            const string libraryName = "FullScanIdTest";
            string libraryPath = TestFilesDir.GetTestPath(libraryName + BiblioSpecLiteSpec.EXT);
            string pepXmlPath = TestFilesDir.GetTestPath("CAexample.pep.xml");
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.LibraryKeepRedundant = true;
                buildLibraryDlg.LibraryBuildAction = LibraryBuildAction.Create;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new [] { pepXmlPath });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            Assert.IsTrue(WaitForCondition(() =>
                peptideSettingsUI.AvailableLibraries.Contains(libraryName)));

            // Add the library to the document
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { libraryName });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChange(docInitial);
            try
            {
                WaitForCondition(() =>
                {
                    var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                    return librarySettings.IsLoaded &&
                           librarySettings.Libraries.Count > 0 &&
                           librarySettings.Libraries[0].Keys.Count() == 43;
                });
            }
            catch (Exception e)
            {
                var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                var libraryKeysCounts = string.Join(",", librarySettings.Libraries.Select(
                    library => null == library ? "null" : string.Empty + library.Keys.Count()));
                string message =
                    string.Format(
                        "Timeout waiting for libraries.  IsLoaded:{0} Libraries.Count:{1} LibrariesKeysCounts:{2}",
                        librarySettings.IsLoaded,
                        librarySettings.Libraries.Count,
                        libraryKeysCounts);
                    
                throw new Exception(message, e);
            }

            var docSetup = SkylineWindow.Document;

            // Add all but 2 of the peptides in the library to the document
            var libraryExplorer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var matchedPepsDlg = WaitForOpenForm<AddModificationsDlg>();
            RunUI(matchedPepsDlg.CancelDialog);
            WaitForClosedForm<AddModificationsDlg>(); // Wait for cancellation to take effect
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(libraryExplorer.AddAllPeptides);
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, addLibraryPepsDlg =>
            {
                Assert.AreEqual(2, (int)addLibraryPepsDlg.Tag);
                addLibraryPepsDlg.Btn1Click();
            });
            RunUI(libraryExplorer.Close);

            var docPeptides = WaitForDocumentChange(docSetup);
            AssertEx.IsDocumentState(docPeptides, null, 1, 33, 41, 123);

            // Switch to full-scan filtering of precursors in MS1
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.FragmentTypes = ""; // Set this empty to verify that we automatically set it to "p" due to MS1 fullscan settings enabled 
                transitionSettingsUI.PrecursorCharges = "2, 3";
                transitionSettingsUI.UseLibraryPick = false;
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.qit;
                transitionSettingsUI.OkDialog();
            });

            var docFullScan = WaitForDocumentChange(docPeptides);
            AssertEx.IsDocumentState(docFullScan, null, 1, 33, 41, 41);  // precursors only
            foreach (var chromInfo in docFullScan.PeptideTransitionGroups.SelectMany(nodeGroup => nodeGroup.ChromInfos))
                Assert.IsTrue(chromInfo.IsIdentified);

            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("CAexample.sky")));

            // Import results
            const string importFileName = "CAexample.mzXML";
            ImportResultsFile(importFileName);

            var docResults = WaitForDocumentChange(docFullScan);

            AssertResult.IsDocumentResultsState(docResults, Path.GetFileNameWithoutExtension(importFileName),
                                                33, 41, 0, 41, 0);

            // Make sure spectrum and chromatogram graphs behave as expected in
            // relation to MS/MS spectrum selection.
            const int level = (int) SrmDocument.Level.TransitionGroups;
            RunUI(() => SkylineWindow.SelectedPath = docResults.GetPathTo(level, 0));

            // CONSIDER: This could be a lot more interesting with a multi-replicate data set,
            //           but it is hard to get one small enough.
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.GraphSpectrum.SelectedSpectrum.IsBest);
                var availableSpectra = SkylineWindow.GraphSpectrum.AvailableSpectra.ToArray();
                Assert.AreEqual(2, availableSpectra.Length);

                var graphChrom = SkylineWindow.GraphChromatograms.First();
                double idTime = availableSpectra[0].RetentionTime ?? 0;
                Assert.AreEqual(idTime, graphChrom.SelectedRetentionTimeMsMs);

                graphChrom.FirePickedSpectrum(new ScaledRetentionTime(idTime, idTime));
            });

            WaitForGraphs();
            RunUI(() => Assert.IsFalse(SkylineWindow.GraphSpectrum.SelectedSpectrum.IsBest));
        }
    }
}
