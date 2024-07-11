/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test import of Chromatogram Library exported from Panorama, and including
    /// a mix of peptide and small molecule values
    /// </summary>
    [TestClass]
    public class AddMixedLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAddMixedLibrary()
        {
            TestFilesZip = @"TestFunctional\AddMixedLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestAddMixedProteomicAndSMallMoleculeChromatogramLibrary();
        }

        private void FindNodes(out PeptideDocNode peptideDocNode, out TransitionGroupDocNode precursorDocNode, out TransitionDocNode transitionDocNode)
        {
            peptideDocNode = SkylineWindow.Document.Molecules.ElementAt(142);
            precursorDocNode = (TransitionGroupDocNode)peptideDocNode.Children.First();
            transitionDocNode = SkylineWindow.Document.MoleculeTransitions.ElementAt(438);
        }

        protected void TestAddMixedProteomicAndSMallMoleculeChromatogramLibrary()
        {
            const string libName = "Mixed";

            RunUI(() => SkylineWindow.NewDocument(true));
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("MixedLib_rev9.clib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(() => { peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library; });
            var exploreDlg = ShowDialog<ViewLibraryDlg>(() => peptideSettingsUi.ShowViewLibraryDlg());
            var modDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(modDlg, modDlg.OkDialogAll);
            // Wait for the list update caused by adding all modifications to complete
            WaitForConditionUI(() => exploreDlg.IsUpdateComplete);
            // Populate the document from library contents
            ShowAndDismissDlg<MultiButtonMsgDlg>(exploreDlg.AddAllPeptides, messageDlg =>
            {
                messageDlg.DialogResult = DialogResult.Yes; // Agree to add lib to document
            });

            // Verify that library imported properly
            using (new CheckDocumentState(4, 186, 230, 482))
            {
                var confirmCountDlg = WaitForOpenForm<AlertDlg>();
                OkDialog(confirmCountDlg, confirmCountDlg.OkDialog);
            }

            RunUI(() => exploreDlg.Close());

            // Molecule "PC aa C24:0" has full accession details
            var doc = SkylineWindow.Document;
            FindNodes(out var peptideDocNode, out var precursorDocNode, out var transitionDocNode);
            var molecule = peptideDocNode.Target;
            AssertEx.AreEqual("PC aa C24:0", molecule.DisplayName);
            AssertEx.AreEqual("C32H64NO8P", molecule.Molecule.Formula);
            AssertEx.AreEqual(
                "1S/C32H64NO8P/c1-6-8-10-12-14-16-18-20-22-24-31(34)38-28-30(29-40-42(36,37)39-27-26-33(3,4)5)41-32(35)25-23-21-19-17-15-13-11-9-7-2/h30H,6-29H2,1-5H3/p+1/t30-/m0/s1",
                molecule.Molecule.AccessionNumbers.GetInChI());
            AssertEx.AreEqual("IJFVSSZAOYLHEE-PMERELPUSA-O", molecule.Molecule.AccessionNumbers.GetInChiKey());
            AssertEx.AreEqual("CCCCCCCCCCCC(=O)OCC(COP(=O)(O)OCC[N+](C)(C)C)OC(=O)CCCCCCCCCCC",
                molecule.Molecule.AccessionNumbers.GetSMILES());
            AssertEx.AreEqual("PC", doc.MoleculeGroups.ElementAt(3).Name);


            // Check ion mobility
            // Enable use of ion mobility values from spectral libraries, and set a nonzero resolving power
            var ionMobilityFilteringSettings =
                doc.Settings.TransitionSettings.IonMobilityFiltering
                    .ChangeUseSpectralLibraryIonMobilityValues(true)
                    .ChangeFilterWindowWidthCalculator(new IonMobilityWindowWidthCalculator(30.0));
            RunUI(() => SkylineWindow.ModifyDocument("adjust ion mobility filter settings", skyDoc => 
                skyDoc.ChangeSettings(
                    doc.Settings.ChangeTransitionSettings(
                        doc.Settings.TransitionSettings.ChangeIonMobilityFiltering(ionMobilityFilteringSettings)))));
            doc = WaitForDocumentLoaded();

            for (var pass = 0; pass < 2; pass++)
            {
                var libKey = precursorDocNode.GetLibKey(doc.Settings, peptideDocNode);
                var libraryIonMobilityInfo = doc.Settings.GetIonMobilities(new [] {libKey}, new MsDataFilePath("silac_1_to_4"));
                var imsFilter = doc.Settings.GetIonMobilityFilter(peptideDocNode, precursorDocNode, transitionDocNode,
                    libraryIonMobilityInfo, null, 0);
                AssertEx.AreEqual(eIonMobilityUnits.drift_time_msec, imsFilter.IonMobilityUnits);
                AssertEx.AreEqual(26.4669, imsFilter.IonMobility.Mobility, 0.001);
                var libraryIonMobilityInfoNoReplicate = doc.Settings.GetIonMobilities(new[] { libKey }, null); // Formerly this would return nothing if no file was given
                var imsFilterNoReplicate = doc.Settings.GetIonMobilityFilter(peptideDocNode, precursorDocNode, transitionDocNode,
                    libraryIonMobilityInfoNoReplicate, null, 0);
                AssertEx.AreEqual(eIonMobilityUnits.drift_time_msec, imsFilterNoReplicate.IonMobilityUnits);
                AssertEx.AreEqual(26.4669, imsFilterNoReplicate.IonMobility.Mobility, 0.001);

                if (pass == 1)
                {
                    break;
                }
                // Make sure clib cache format works - save the document, close it, reopen it, recheck values
                RunUI(() =>
                {
                    var testPath = TestFilesDirs[0].GetTestPath("test.sky");
                    SkylineWindow.SaveDocument(testPath);
                    SkylineWindow.NewDocument(true);
                    SkylineWindow.OpenFile(testPath);
                    doc = WaitForDocumentLoaded();
                    FindNodes(out peptideDocNode, out precursorDocNode, out transitionDocNode);
                });
            }
        }
    }
}
