/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests using the Import Peptide Search wizard to use an existing .elib file,
    /// add missing modifications, use a small FASTA file and extract chromatograms.
    /// </summary>
    [TestClass]
    public class ImportEncyclopeDiaLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportEncyclopeDiaLibrary()
        {
            TestFilesZip = @"TestFunctional\ImportEncyclopeDiaLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("ImportLibraryTest.sky")));
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(()=>
            {
                var buildPepSearchCtrl = importPeptideSearchDlg.BuildPepSearchLibControl;
                buildPepSearchCtrl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                buildPepSearchCtrl.UseExistingLibrary = true;
                buildPepSearchCtrl.ExistingLibraryPath = TestFilesDir.GetTestPath("importlibsearchtest.elib");
                importPeptideSearchDlg.ClickNextButton();
            });
            RunUI(()=>
            {
                string resultFilePath = TestFilesDir.GetTestPath("22jun2016_mcf7_phospho_1a.mzML");
                importPeptideSearchDlg.ImportResultsControl.FoundResultsFiles =
                    new List<ImportPeptideSearch.FoundResultsFile>()
                    {
                        new ImportPeptideSearch.FoundResultsFile(Path.GetFileNameWithoutExtension(resultFilePath), resultFilePath)
                    };
                importPeptideSearchDlg.ClickNextButton();
            });
            RunUI(() =>
                {
                    importPeptideSearchDlg.MatchModificationsControl.CheckedModifications =
                        importPeptideSearchDlg.MatchModificationsControl.MatchedModifications;
                    importPeptideSearchDlg.ClickNextButton();
                });
            
            RunUI(()=>
            {
                // Configure transition settings
                importPeptideSearchDlg.ClickNextButton();
            });
            RunUI(()=>
            {
                // Configure full-scan settings
                importPeptideSearchDlg.ClickNextButton();
            });
            RunUI(() =>
                {
                    importPeptideSearchDlg.ImportFastaControl.SetFastaContent(
                        TestFilesDir.GetTestPath("ns_peptides.fasta"));
                });
            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(()=>importPeptideSearchDlg.ClickNextButton());
            RunUI(()=>peptidesPerProteinDlg.KeepAll = true);
            WaitForConditionUI(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.SaveDocument());
        }
    }
}
