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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests adding peptides from the Spectral Library Viewer with "Associate Proteins" checked
    /// when there is a protein in the document whose name matches something in the background proteome
    /// but whose protein sequence is different.
    /// </summary>
    [TestClass]
    public class AssociateProteinVariantTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAssociateProteinVariant()
        {
            TestFilesZip = @"TestFunctional\AssociateProteinVariantTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string firstPeptideSequence = "PEPTIDEA";
            const string secondPeptideSequence = "PEPTIDEC";
            const string thirdPeptideSequence = "PEPTIDED";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUi.TextExcludeAAs = 0;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = "peptides";
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("peptides.blib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);

            addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = "peptided";
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("peptided.blib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);

            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(()=>
            {
                peptideSettingsUi.SetLibraryChecked(0, true);
                peptideSettingsUi.SetLibraryChecked(1, true);
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsUi.PeptidePick = PeptidePick.either;
            });

            // Create a background proteome with protein "MyProtein" whose sequence contains both "PEPTIDEA" and "PEPTIDEC".
            var buildBackgroundProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(
                peptideSettingsUi.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomeName = "MyBackgroundProteome";
                buildBackgroundProteomeDlg.BackgroundProteomePath = TestFilesDir.GetTestPath("MyBackgroundProteome.protdb");
                buildBackgroundProteomeDlg.AddFastaFile(TestFilesDir.GetTestPath("PEPTIDEC.fasta"));
            });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();

            Assert.AreEqual(0, SkylineWindow.Document.MoleculeGroupCount);
            // Add a protein to the document whose name is "MyProtein" and whose sequence contains "PEPTIDEA" but does not
            // contain "PEPTIDEC".
            SetClipboardTextUI(">MyProtein\r\nKAAAPEPTIDEAAAKKK");
            RunUI(()=>SkylineWindow.Paste());
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeGroupCount);
            var firstProtein = SkylineWindow.Document.PeptideGroups.FirstOrDefault();
            Assert.IsNotNull(firstProtein);
            StringAssert.Contains(firstProtein.PeptideGroup.Sequence, firstPeptideSequence);
            Assert.IsFalse(firstProtein.PeptideGroup.Sequence.Contains(secondPeptideSequence));

            // Add the peptides "PEPTIDEA" and "PEPTIDEC" to the document, and associate proteins.
            var libraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                libraryViewer.ChangeSelectedLibrary("peptides");
                libraryViewer.AssociateMatchingProteins = true;
            });
            RunDlg<AlertDlg>(libraryViewer.AddAllPeptides, alertDlg => alertDlg.DialogResult = DialogResult.Yes);

            // Verify that "PEPTIDEA" got added to the first protein, and that a new protein had to be created for "PEPTIDEC" since
            // the first protein's sequence did not contain it.
            var doc = SkylineWindow.Document;
            Assert.AreEqual(2, doc.Children.Count);
            firstProtein = (PeptideGroupDocNode) doc.Children[0];
            PeptideGroupDocNode secondProtein = (PeptideGroupDocNode) doc.Children[1];
            Assert.AreEqual(1, secondProtein.Children.Count);

            StringAssert.Contains(firstProtein.PeptideGroup.Sequence, firstPeptideSequence);
            Assert.IsFalse(firstProtein.PeptideGroup.Sequence.Contains(secondPeptideSequence));
            StringAssert.Contains(secondProtein.PeptideGroup.Sequence, secondPeptideSequence);
            
            var firstPeptide = firstProtein.Peptides.FirstOrDefault(pep => firstPeptideSequence.Equals(pep.Peptide.Sequence));
            Assert.IsNotNull(firstPeptide);
            StringAssert.Contains(secondProtein.PeptideGroup.Sequence, secondPeptideSequence);
            var secondPeptide = secondProtein.Peptides.FirstOrDefault(pep => secondPeptideSequence.Equals(pep.Peptide.Sequence));
            Assert.IsNotNull(secondPeptide);
            Assert.IsNull(firstProtein.Peptides.FirstOrDefault(pep=>pep.Peptide.Sequence == secondPeptide.Peptide.Sequence));

            // Now add just the peptide "PEPTIDED" and make sure it gets added to the second protein in the document.
            RunUI(()=>libraryViewer.ChangeSelectedLibrary("peptided"));
            RunDlg<AlertDlg>(libraryViewer.AddAllPeptides, alertDlg=>alertDlg.DialogResult = DialogResult.Yes);
            secondProtein = (PeptideGroupDocNode) SkylineWindow.Document.Children[1];
            Assert.AreEqual(2, secondProtein.Children.Count);
            var thirdPeptide = (PeptideDocNode) secondProtein.Children[1];
            Assert.AreEqual(thirdPeptideSequence, thirdPeptide.Peptide.Sequence);

            OkDialog(libraryViewer, libraryViewer.Close);
        }
    }
}
