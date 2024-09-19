/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests renaming a molecule and makes sure that the <see cref="PeptideDocNode.OriginalMoleculeTarget"/>
    /// gets set correctly so that chromatograms do not get orphaned
    /// </summary>
    [TestClass]
    public class RenameMoleculeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRenameMolecule()
        {
            TestFilesZip = @"TestFunctional\RenameMoleculeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var transitionList = TextUtil.LineSeparate(
                "Molecule Name\tMolecular Formula\tPrecursor Adduct\tProduct Formula\tProduct Adduct",
                "Molecule1\tC77H129N23O27S\t[M+3H]\tC77H129N23O27S\t[M+3H]");
            RunUI(() =>
            {
                SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules);
            });
            PasteSmallMoleculeList(transitionList);
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoTransitions = true;
                refineDlg.OkDialog();
            });
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUi.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUi.OkDialog();
            });
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("RenameMoleculeTest.sky"));
            });
            
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide, editCustomMoleculeDlg =>
            {
                editCustomMoleculeDlg.NameText = "Molecule2";
                editCustomMoleculeDlg.OkDialog();
            });
            // The original molecule name should not be remembered because the document does not have results
            Assert.IsNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);

            ImportResultsFile(TestFilesDir.GetTestPath("S_1.mzML"));
            VerifyAllTransitionsHaveChromatograms();
            
            // Rename the molecule and verify the previous name is remembered
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide, editCustomMoleculeDlg =>
            {
                editCustomMoleculeDlg.NameText = "Molecule3";
                editCustomMoleculeDlg.OkDialog();
            });
            Assert.IsNotNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);
            Assert.AreEqual("Molecule2", SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget.Molecule?.Name);
            VerifyAllTransitionsHaveChromatograms();

            // Rename the molecule a second time and verify that the original name is remembered
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide, editCustomMoleculeDlg =>
            {
                editCustomMoleculeDlg.NameText = "Molecule4";
                editCustomMoleculeDlg.OkDialog();
            });
            Assert.AreEqual("Molecule2", SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget.Molecule?.Name);
            VerifyAllTransitionsHaveChromatograms();

            ImportResultsFile(TestFilesDir.GetTestPath("S_2.mzML"));
            VerifyAllTransitionsHaveChromatograms();
            
            // Reimport one of the two replicates and verify that the original name is remembered
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg=>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms.Take(1);
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            Assert.IsNotNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);
            Assert.AreEqual("Molecule2", SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget.Molecule?.Name);
            WaitForDocumentLoaded();
            VerifyAllTransitionsHaveChromatograms();

            // Reimport all the replicates and verify that the original name is forgotten
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.ReimportResults();
                manageResultsDlg.OkDialog();
            });
            Assert.IsNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);
            WaitForDocumentLoaded();
            VerifyAllTransitionsHaveChromatograms();

            // Rename the molecule again
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide, editCustomMoleculeDlg =>
            {
                editCustomMoleculeDlg.NameText = "Molecule5";
                editCustomMoleculeDlg.OkDialog();
            });
            VerifyAllTransitionsHaveChromatograms();
            Assert.IsNotNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);
            Assert.AreEqual("Molecule4", SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget.Molecule?.Name);

            // Remove all the replicates and verify the original name is forgotten
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms;
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });
            Assert.IsNull(SkylineWindow.Document.Molecules.First().OriginalMoleculeTarget);
        }
    }
}
