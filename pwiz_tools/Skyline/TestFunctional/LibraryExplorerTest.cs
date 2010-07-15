/*
 * Original author: Tahmina Baker <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class LibraryExplorerTest : AbstractFunctionalTest
    {
        private const string INVALID_SEARCH = "C[";

        private struct TestLibInfo
        {
            public string Name { get; private set; }
            public string Filename { get; private set; }
            public string UniquePeptide { get; private set; }

            public TestLibInfo(string name, string filename, string uniquePeptide) : this()
            {
                Name = name;
                Filename = filename;
                UniquePeptide = uniquePeptide;
            }
        }

        private readonly TestLibInfo[] _testLibs = {
                                                       new TestLibInfo("HumanB2MGLib", "human_b2mg-5-06-2009-it.sptxt", "EVDLLK+"),
                                                       new TestLibInfo("HumanCRPLib", "human_crp-5-06-2009-it.sptxt", "TDMSR++")
                                                   };

        private PeptideSettingsUI PeptideSettingsUI { get; set; }

        [TestMethod]
        public void TestLibraryExplorer()
        {
            TestFilesZip = @"TestFunctional\LibraryExplorerTest.zip";
            RunFunctionalTest();
        }

        private void LaunchPeptideSettings()
        {
            PeptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
        }

        private void AddLibraries()
        {
            var editListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(PeptideSettingsUI.EditLibraryList);
            int numLibs = _testLibs.Length;
            for (int i = 0; i < numLibs; i++)
            {
                AddLibrary(editListUI, _testLibs[i]);
            }

            RunUI(editListUI.OkDialog);
            WaitForClosedForm(editListUI);
        }

        private void AddLibrary(EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec> editListUI, TestLibInfo info)
        {
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            var nameTextBox = addLibUI.Controls.Find("textName", true)[0] as TextBox;
            Assert.IsNotNull(nameTextBox);
            var pathTextBox = addLibUI.Controls.Find("textPath", true)[0] as TextBox;
            Assert.IsNotNull(pathTextBox);
            RunUI(() =>
            {
                nameTextBox.Text = info.Name;
                pathTextBox.Text = TestFilesDir.GetTestPath(info.Filename);
                addLibUI.OkDialog();
            });
            WaitForClosedForm(addLibUI);
        }

        protected override void DoTest()
        {
            LaunchPeptideSettings();
            AddLibraries();

            // Launch the Library Explorer dialog
            var viewLibUI = ShowDialog<ViewLibraryDlg>(PeptideSettingsUI.ShowViewLibraryDlg);
            
            // Ensure the appropriate default library is selected
            string libSelected = null;
            var libComboBox = viewLibUI.Controls.Find("LibraryComboBox", true)[0] as ComboBox;
            Assert.IsNotNull(libComboBox);
            RunUI(() =>
            {
                libSelected = libComboBox.SelectedItem.ToString();
            });
            Assert.AreEqual(_testLibs[0].Name, libSelected);

            // Find the peptides list control
            var pepList = viewLibUI.Controls.Find("PeptideListBox", true)[0] as ListBox;
            Assert.IsNotNull(pepList);

            // Initially, peptide with index 0 should be selected
            string previousPeptide = null;
            int peptideIndex = -1;
            WaitForConditionUI(() => pepList.SelectedIndex != -1);
            RunUI(() =>
            {
                previousPeptide = pepList.SelectedItem.ToString();
                peptideIndex = pepList.SelectedIndex;
            });
            Assert.IsNotNull(previousPeptide);
            Assert.AreEqual(0, peptideIndex);

            // Now try to select a different peptide and check to see if the
            // selection changes
            const int selectPeptideIndex = 1;
            RunUI(() =>
            {
                pepList.SelectedIndex = selectPeptideIndex;
            });

            string selPeptide = null;
            RunUI(() =>
            {
                selPeptide = pepList.SelectedItem.ToString();
            });
            Assert.IsNotNull(selPeptide);
            Assert.AreNotEqual(previousPeptide, selPeptide);
            
            // Click the "Next" link
            var nextLink = viewLibUI.Controls.Find("NextLink", true)[0] as IButtonControl;
            Assert.IsNotNull(nextLink);
            RunUI(nextLink.PerformClick);
            RunUI(() =>
            {
                previousPeptide = pepList.SelectedItem.ToString();
            });

            // Click "Previous" link and ensure the peptide selected changes
            var previousLink = viewLibUI.Controls.Find("PreviousLink", true)[0] as IButtonControl;
            Assert.IsNotNull(previousLink);
            RunUI(previousLink.PerformClick);
            RunUI(() =>
            {
                selPeptide = pepList.SelectedItem.ToString();
            });
            Assert.AreNotEqual(previousPeptide, selPeptide);


            // Test valid peptide search
            var pepTextBox = viewLibUI.Controls.Find("PeptideTextBox", true)[0] as TextBox;
            Assert.IsNotNull(pepTextBox);
            RunUI(() =>
            {
                pepTextBox.Focus();
                pepTextBox.Text = _testLibs[0].UniquePeptide;
            });
            int pepsCount = 0;
            RunUI(() =>
            {
                selPeptide = pepList.SelectedItem.ToString();
                pepsCount = pepList.Items.Count;
            });
            Assert.AreEqual(_testLibs[0].UniquePeptide, selPeptide);
            Assert.AreEqual(1, pepsCount);

            // Test invalid peptide search
            RunUI(() =>
            {
                pepTextBox.Focus();
                pepTextBox.Text = INVALID_SEARCH;
            });
            RunUI(() =>
            {
                pepsCount = pepList.Items.Count;
            });
            Assert.AreEqual(0, pepsCount);

            // Test clearing invalid peptide search
            RunUI(() =>
            {
                pepTextBox.Focus();
                pepTextBox.Text = "";
            });
            selPeptide = null;
            RunUI(() =>
            {
                selPeptide = pepList.SelectedItem.ToString();
                pepsCount = pepList.Items.Count;
            });
            Assert.IsNotNull(selPeptide);
            Assert.AreNotEqual(0, pepsCount);

            // Test selecting a different library
            previousPeptide = selPeptide;
            RunUI(() =>
            {
                libComboBox.SelectedIndex = 1;
            });
            RunUI(() =>
            {
                libSelected = libComboBox.SelectedItem.ToString();
            });
            Assert.AreEqual(libSelected, _testLibs[1].Name);
            RunUI(() =>
            {
                selPeptide = pepList.SelectedItem.ToString();
            });
            Assert.IsNotNull(selPeptide);
            Assert.AreNotEqual(previousPeptide, selPeptide);

            // Close the Library Explorer dialog
            OkDialog(viewLibUI, viewLibUI.CancelDialog);

            // Close the peptide settings dialog
            OkDialog(PeptideSettingsUI, PeptideSettingsUI.OkDialog);
        }
    }
}
