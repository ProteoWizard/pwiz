using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using System.Collections.Generic;
using System.Windows.Forms;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    class LibraryImportFilteringTest : AbstractFunctionalTest
    {
        private const string HMDB_CONFLICTS = "MoNA-export-HMDB.msp";
        private const string AURUM_FINAL = "aurum_consensus_final_true_lib.msp";
        private const string SILICO_LARGE = "MoNA-export-In-Silico_Spectra.msp";

        private struct CommentInfo
        {
            public string commentText;
            public int categoryNum;
            public string category;
            public string value;
            public CommentInfo(string comment, int numCategories, string knownCategory, string knownValue)
            {
                commentText = comment;
                categoryNum = numCategories;
                category = knownCategory;
                value = knownValue;
            }
        }

        private void TestCommentFormatting(CommentInfo commentInfo)
        {
            var dict = MspParser.FormatComment(commentInfo.commentText);
            Assert.AreEqual(commentInfo.categoryNum, dict.Keys.Count);
            Assert.AreEqual(dict[commentInfo.category], commentInfo.value);
        }

        public void TestFiltering()
        {
            //var filterForm = new FilterLibraryDlg();
            //filterForm.Show();
            //WaitForClosedForm(filterForm);

            // First test how we pick apart the comments field for information we then use to filter
            // The format of comments field can vary between libraries
            var commentList = new List<CommentInfo>
            {
                // Comment with double quotes around some values, found in MoNA libraries
                new CommentInfo(
                    "Contributor=\"NIST Mass Spectrometry Data Center\" Spec=Consensus Nreps=63/64 Mz_diff=0.323", 4,
                    "Contributor", "NIST Mass Spectrometry Data Center"),

                // Another MoNA comment, this time with double quotes around every field-value pair, and an equals sign in a value.
                // Because use the equals sign to split the field from the value it is important to handle this case
                new CommentInfo("\"SMILES=CCCCC(=O)OC(CC(O)=O)C[N+](C)(C)C\" \"compound class=ACar\" \"computed SMILES=O=C(O)CC(OC(=O)CCCC)C[N+](C)(C)C\" \"computed InChI=InChI=1S/C12H23NO4/c1-5-6-7-12(16)17-10(8-11(14)15)9-13(2,3)4/h10H,5-9H2,1-4H3/p+1\"", 5,
                    "computed SMILES", "O=C(O)CC(OC(=O)CCCC)C[N+](C)(C)C"),

                // Comment without double quotes around field-value pairs, found in NIST libraries
                new CommentInfo("Pep=Tryptic Mods=3(0,K,iTRAQ)(0,K,iTRAQ)(7,K,iTRAQ) Fullname=R.KVESALIKPINPR.L Charge=3 Parent=633.0701", 5,
                    "Pep", "Tryptic")
            };

            foreach (var commentInfo in commentList)
            {
                TestCommentFormatting(commentInfo);
            }

            // Now load a .msp library with multiple spectra per molecule adduct pair
            var filepath = TestFilesDir.GetTestPath(HMDB_CONFLICTS);

            // See how we pick apart libraries as a whole
            var parser = new MspParser(filepath);

            // Check if we found all the distinct categories
            var libDict = parser.CreateCategories();
            var duplicates = parser.FindDuplicates();


            // Set criteria that should resolve all conflicts in the file
            var criteriaList = new List<ImportFilteringParser.FilterCriteria>
            {
                new ImportFilteringParser.FilterCriteria(ImportFilteringParser.LibFilterType.contains,
                    "Instrument_type", "GC-MS"),
                new ImportFilteringParser.FilterCriteria(ImportFilteringParser.LibFilterType.greater_than,
                    "MoNA Rating", "2"),
                new ImportFilteringParser.FilterCriteria(ImportFilteringParser.LibFilterType.matches_exactly,
                    "derivatization formula", "C37H64O2Si2"),
                new ImportFilteringParser.FilterCriteria(ImportFilteringParser.LibFilterType.greater_than,
                    "Retention_index", "3500")
            };

            // The filepath of the new .msp we will create
            var resultsFilepath = TestFilesDir.GetTestPath("testWriteFile.msp");

            // Write our filtered list 
            parser.WriteMatchingEntries(resultsFilepath, criteriaList);


            // Find the edit library control and add the library
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);

            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                var nameTextBox = (TextBox)addLibUI.Controls.Find("textName", true)[0];
                var pathTextBox = (TextBox)addLibUI.Controls.Find("textPath", true)[0];
                nameTextBox.Text = @"MoNa Export";
                pathTextBox.Text = resultsFilepath;
                addLibUI.OkDialog();

            });
            // Close the edit libraries dialog
            RunUI(editListUI.OkDialog);
            WaitForClosedForm(editListUI);

            // Close the settings window
            RunUI(peptideSettingsUI.OkDialog);
            WaitForClosedForm(peptideSettingsUI);

            // Now test the UI of the filtering dialog
            var pepSettings1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            Assert.IsNotNull(pepSettings1);

            var editListUI1 = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(pepSettings1.EditLibraryList);

            AddLibrary(editListUI1, "name", HMDB_CONFLICTS);
            PauseTest();
            


        }
        private void AddLibrary(EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec> editListUI, string name, string fileName)
        {
            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                var nameTextBox = (TextBox)addLibUI.Controls.Find("textName", true)[0];
                Assert.IsNotNull(nameTextBox);
                var pathTextBox = (TextBox)addLibUI.Controls.Find("textPath", true)[0];
                Assert.IsNotNull(pathTextBox);
                nameTextBox.Text = name;
                pathTextBox.Text = TestFilesDir.GetTestPath(fileName);
                addLibUI.OkDialog();
            });
            editListUI.OkDialog();
        }

        [TestMethod]
        public void TestLibraryImportFiltering()
        {
            TestFilesZip = @"TestFunctional\LibraryImportFilteringTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFiltering();
        }
    }
}
