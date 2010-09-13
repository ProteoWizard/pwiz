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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

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
                                                       new TestLibInfo("HumanCRPLib", "human_crp-5-06-2009-it.sptxt", "TDMSR++"), 
                                                       new TestLibInfo("ANL Combined", "ANL_combined.blib", ""),
                                                       new TestLibInfo("PhosphoLib", "phospho_30882_v2.blib", ""),
                                                       new TestLibInfo("Yeast", "Yeast_atlas.blib", "")
                                                   };

        private PeptideSettingsUI PeptideSettingsUI { get; set; }
        private ViewLibraryDlg _viewLibUI;

        [TestMethod]
        public void TestLibraryExplorer()
        {
            TestFilesZip = @"TestFunctional\LibraryExplorerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            PeptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            Assert.IsNotNull(PeptideSettingsUI);

            var editListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(PeptideSettingsUI.EditLibraryList);
            int numLibs = _testLibs.Length;
            for (int i = 0; i < numLibs; i++)
            {
                AddLibrary(editListUI, _testLibs[i]);
            }
            RunUI(editListUI.OkDialog);
            WaitForClosedForm(editListUI);

            // Make sure the libraries actually show up in the peptide settings dialog before continuing.
            WaitForConditionUI(() => _testLibs.Length == PeptideSettingsUI.AvailableLibraries.Count());

            RunUI(() => Assert.IsFalse(PeptideSettingsUI.IsSettingsChanged));

            // Launch the Library Explorer dialog
            _viewLibUI = ShowDialog<ViewLibraryDlg>(PeptideSettingsUI.ShowViewLibraryDlg);

            // Ensure the appropriate default library is selected
            ComboBox libComboBox = null;
            ListBox pepList = null;
            string libSelected = null;
            RunUI(() =>
            {
                libComboBox = (ComboBox)_viewLibUI.Controls.Find("comboLibrary", true)[0];
                Assert.IsNotNull(libComboBox);
                libSelected = libComboBox.SelectedItem.ToString();

                // Find the peptides list control
                pepList = (ListBox)_viewLibUI.Controls.Find("listPeptide", true)[0];
                Assert.IsNotNull(pepList);
            });
            Assert.AreEqual(_testLibs[0].Name, libSelected);

            // Initially, peptide with index 0 should be selected
            WaitForConditionUI(() => pepList.SelectedIndex != -1);

            ViewLibraryPepInfo previousPeptide = new ViewLibraryPepInfo();
            int peptideIndex = -1;
            RunUI(() =>
            {
                previousPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
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

            ViewLibraryPepInfo selPeptide = new ViewLibraryPepInfo();
            RunUI(() =>
            {
                selPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
            });
            Assert.IsNotNull(selPeptide);
            Assert.AreNotEqual(previousPeptide, selPeptide);

            // Click the "Next" link
            RunUI(() =>
                      {
                          var nextLink = (IButtonControl)_viewLibUI.Controls.Find("NextLink", true)[0];
                          Assert.IsNotNull(nextLink);

                          nextLink.PerformClick();
                      });
            RunUI(() =>
            {
                previousPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
            });

            // Click "Previous" link and ensure the peptide selected changes
            RunUI(() =>
                      {
                          var previousLink = (IButtonControl) _viewLibUI.Controls.Find("PreviousLink", true)[0];
                          Assert.IsNotNull(previousLink);

                          previousLink.PerformClick();
                      });
            RunUI(() =>
            {
                selPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
            });
            Assert.AreNotEqual(previousPeptide, selPeptide);


            // Test valid peptide search
            TextBox pepTextBox = null;
            RunUI(() =>
            {
                pepTextBox = (TextBox)_viewLibUI.Controls.Find("textPeptide", true)[0];
                Assert.IsNotNull(pepTextBox);

                pepTextBox.Focus();
                pepTextBox.Text = _testLibs[0].UniquePeptide;
            });
            int pepsCount = 0;
            RunUI(() =>
            {
                selPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
                pepsCount = pepList.Items.Count;
            });
            Assert.AreEqual(_testLibs[0].UniquePeptide, selPeptide.DisplayString);
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
            selPeptide = new ViewLibraryPepInfo();
            RunUI(() =>
            {
                selPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
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
                selPeptide = (ViewLibraryPepInfo)pepList.SelectedItem;
            });
            Assert.IsNotNull(selPeptide);
            Assert.AreNotEqual(previousPeptide, selPeptide);

            // If the library is not in the document settings, offer to add the library to the settings.
            // If the user declines, add the peptides anyways, but strip them so they do not appear to be
            // connected to any library.
            RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddPeptide, msgDlg => msgDlg.Btn1Click());
            Assert.AreEqual(1, SkylineWindow.Document.PeptideCount);
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep => nodePep.HasLibInfo));
            RunUI(() =>
                      {
                          SkylineWindow.SelectAll();
                          SkylineWindow.EditDelete();
                      });

            // Test adding peptides offers to add library if not already in settings.
            // Test add single peptide.
            RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddPeptide, msgDlg => msgDlg.Btn0Click());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 1, 1, 3);
            RunUI(SkylineWindow.EditDelete);

            // Test unmatched peptides are correct.
            AddAllPeptides(8);

            // Test library peptides are merged without duplicates.
            TestForDuplicatePeptides();

            // Test library peptides only get added to the document once.
            var docOriginal = SkylineWindow.Document;
            RunDlg<MessageDlg>(_viewLibUI.AddPeptide, msgDlg => msgDlg.OkDialog());
            var filterMatchedPeptidesDlg5 = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunDlg<MessageDlg>(filterMatchedPeptidesDlg5.OkDialog, msgDlg => msgDlg.OkDialog());
            Assert.AreSame(docOriginal, SkylineWindow.Document);

            // Test missing peptides added. 
            RunUI(() =>
                      {
                          var sequenceTree = SkylineWindow.SequenceTree;
                          var nodePeps = sequenceTree.Nodes[0].Nodes;
                          sequenceTree.SelectedNode = nodePeps[0];
                          sequenceTree.KeysOverride = Keys.Control;
                          for (int i = 2; i < 10; i += 2)
                          {
                              sequenceTree.SelectedNode = nodePeps[i];
                          }
                          sequenceTree.KeysOverride = Keys.None;
                          SkylineWindow.EditDelete();
                      });
            AddAllPeptides(null);
            var docAddBack = SkylineWindow.Document;
            // Peptides will be added back in a different order
            AssertEx.IsDocumentState(docAddBack, null,
                docOriginal.PeptideGroupCount, docOriginal.PeptideCount,
                docOriginal.TransitionGroupCount, docOriginal.TransitionCount);
            TestSamePeptides(docOriginal.Peptides);

            // Test missing transition groups added correctly.
            RunUI(() =>
            {
                var sequenceTree = SkylineWindow.SequenceTree;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0].Nodes[0].Nodes[0];
                sequenceTree.KeysOverride = Keys.Control;
                for (int x = 0; x < 5; x++)
                {
                    var nodePep = sequenceTree.Nodes[0].Nodes[x];
                    var nodeGroups = nodePep.Nodes;
                    for (int i = 0; i < nodeGroups.Count; i += 2)
                    {
                        sequenceTree.SelectedNode = nodeGroups[i];
                    }
                }
                SkylineWindow.EditDelete();
            });
            AddAllPeptides(null);
            var docAddBackGroups = SkylineWindow.Document;
            // Check all precursor charges present.
            foreach (PeptideDocNode nodePep in docOriginal.Peptides)
            {
                var key = nodePep.Key;
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    var charge = nodeGroup.TransitionGroup.PrecursorCharge;
                    Assert.IsTrue(docAddBackGroups.Peptides.Contains(nodePepDoc => Equals(key, nodePepDoc.Key)
                        && nodePepDoc.HasChildCharge(charge)));
                }
            }
            // Check no duplicate TransitionGroups added.
            TestForDuplicateTransitionGroups();

            // Test peptides get heavy label modifications.
            List<StaticMod> heavyMods15N = 
                new List<StaticMod> { new StaticMod("15N", null, null, null, LabelAtoms.N15, null, null) };
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.EditDelete();
                var settings = SkylineWindow.Document.Settings.ChangePeptideModifications(mods => new PeptideModifications(mods.StaticModifications,
                   new[] { new TypedModifications(IsotopeLabelType.heavy, heavyMods15N) }));
                SkylineWindow.ModifyDocument("Change heavy modifications", doc => doc.ChangeSettings(settings));
            });
            AddAllPeptides(null);
            // All peptides should have a heavy label transition group. 
            // There should be no peptide whose children do not contain a transition group with heavy label type.
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep =>
                !nodePep.Children.Contains(nodeGroup =>
                    ((TransitionGroupDocNode)nodeGroup).TransitionGroup.LabelType.Equals(IsotopeLabelType.heavy))));

            // Test peptide setting changes update the library explorer.
            RunUI(() => SkylineWindow.ModifyDocument("Change static modifications", 
                doc => doc.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new List<StaticMod>())))));
            AddAllPeptides(19);
           
            // Switch to ANL_Combined library
            RunUI(() => { libComboBox.SelectedIndex = 2; });
            // Along with Heavy 15N added earlier in the test, adding this modification means that all library peptides
            // will match to the document settings.
            StaticMod varMetOxidized = new StaticMod("Methionine Oxidized", "M", null, true, "O",
                LabelAtoms.None, RelativeRT.Matching, null, null, null);
            var metOxidizedSettings = SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new[] { varMetOxidized }));
            RunUI(() =>
                      {
                          SkylineWindow.SelectAll();
                          SkylineWindow.EditDelete();
                          SkylineWindow.ModifyDocument("Change static mods",
                                                       doc => doc.ChangeSettings(metOxidizedSettings));
                      });
            // User prompted to add library since not in current settings.
            RunDlg<MultiButtonMsgDlg>(() => _viewLibUI.CheckLibraryInSettings(), msgDlg => msgDlg.Btn0Click());
            // Add single peptide to the document.
            RunUI(_viewLibUI.AddPeptide);
            var nodePepAdded = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
            // Because document settings match the library, no duplicates should be found.
            AddAllPeptides(0);

            // Even though there are two matches in the library for the the nodePep we just added 
            // to the document (one with light modifications and one with heavy), the two spectrum 
            // both have the same charge. In this case, both spectrum should be ignored when Add All
            // is called.
            Assert.AreEqual(nodePepAdded, SkylineWindow.SequenceTree.Nodes[0].Nodes[0]);
            Assert.AreEqual(3, SkylineWindow.Document.PeptideCount);

            // Switch to the Phospho Loss Library
            RunUI(() =>
            {
                libComboBox.SelectedIndex = 3;
            });
            // Add modifications to the document matching the settings of the library. 
            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T", null, true, "HPO3",
                                  LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("H3PO4"), });
            var phosphoLossSettings =
                SkylineWindow.Document.Settings.ChangePeptideModifications(mods => mods.ChangeStaticModifications(new[] { phosphoLossMod }));
            RunUI(() =>
                SkylineWindow.ModifyDocument("Change static mods", doc => doc.ChangeSettings(phosphoLossSettings)));
            RunDlg<MultiButtonMsgDlg>(() => _viewLibUI.CheckLibraryInSettings(), msgDlg => msgDlg.Btn0Click());
            // Again, we should be able to match all peptides since the document settings match use the peptides found 
            // in the library.
            RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddAllPeptides, msgDlg =>
             {
                 Assert.AreEqual(0, (int)msgDlg.Tag);
                 msgDlg.Btn1Click();
             });
            // Test losses are being displayed in the graph, indicating that the spectrum have been matched correctly.
            WaitForConditionUI(() => _viewLibUI.GraphItem.IonLabels.Contains(str => str.Contains("98")));
            Assert.IsTrue(_viewLibUI.GraphItem.IonLabels.Contains(str => str.Contains("98")));

            // Associate yeast background proteome
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg, 
                buildBackgroundProteomeDlg =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomePath = TestFilesDir.GetTestPath("yeast_mini.protdb");
                buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast";
                buildBackgroundProteomeDlg.OkDialog();
            });
            RunUI(peptideSettingsUI.OkDialog);
            WaitForCondition(() =>
            {
                var peptideSettings = Program.ActiveDocument.Settings.PeptideSettings;
                var backgroundProteome = peptideSettings.BackgroundProteome;
                return backgroundProteome.GetDigestion(peptideSettings) != null;
            });

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.PrecursorCharges = "1,2,3";
                transitionSettingsUI.OkDialog();
            });
            
            // Test add all with the yeast background proteome connected.
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.EditDelete();
                // Switch to yeast library.
                libComboBox.SelectedIndex = 4;
                _viewLibUI.AssociateMatchingProteins = true;
            });
            var addLibraryDlg = ShowDialog<MultiButtonMsgDlg>(_viewLibUI.AddAllPeptides);
            // Add the library to the document.
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(addLibraryDlg.Btn0Click);
            RunUI(() => filterMatchedPeptidesDlg.AddUnmatched = false);
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, messageDlg => messageDlg.Btn1Click());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 4, 10, 13, 39);
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep => !nodePep.HasLibInfo));

            // Test adding a single peptide that matches two different proteins using keep all.
            RunUI(() =>
                      {
                          SkylineWindow.Undo();
                          _viewLibUI.ChangeSelectedPeptide("ADTGIAVEGATDAAR+");
                      });
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                                             filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 2, 6);

            // Test adding a second charge state for that peptide.
            RunUI(() => _viewLibUI.ChangeSelectedPeptide("ADTGIAVEGATDAAR++"));
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                                  filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 4, 12);
                        RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                                  filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());
            
            // Test adding a second charge state - No Duplicates.
            RunUI(() => SkylineWindow.Undo());
            var docPrev = SkylineWindow.Document;
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                      filterMatchedPeptideDlg =>
            {
                filterMatchedPeptideDlg.SetFilter(ViewLibraryPepMatching.DuplicateProteinsFilter.NoDuplicates);
                filterMatchedPeptideDlg.OkDialog();
            });
            Assert.AreEqual(docPrev, SkylineWindow.Document);
            
            // Test adding a second charge state - First Only.
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                      filterMatchedPeptideDlg =>
            {
                filterMatchedPeptideDlg.SetFilter(ViewLibraryPepMatching.DuplicateProteinsFilter.FirstOccurence);
                filterMatchedPeptideDlg.OkDialog();
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 3, 9);

            RunUI(() =>
            {
                SkylineWindow.Undo();
                SkylineWindow.Undo();
                // Add doubly charged state, no protein association.
                _viewLibUI.AssociateMatchingProteins = false;
                _viewLibUI.AddPeptide();
                _viewLibUI.AssociateMatchingProteins = true;
            });
            // Add doubly charged state with protein assocation.
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());  // First only
            // Select singly charged state
            RunUI(() => _viewLibUI.ChangeSelectedPeptide("ADTGIAVEGATDAAR+"));
            // Test adding peptide associated with the background proteome does not affect any matching peptides that are 
            // in peptide lists.
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                      filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog()); // First only
            var peptideGroups = SkylineWindow.Document.PeptideGroups.ToArray();
            int index = peptideGroups.IndexOf(nodeGroup => nodeGroup.IsPeptideList);
            Assert.IsTrue(peptideGroups[index].Children.Count == 1);
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 2, 3, 9);
           
            // Test selecting no duplicates prevents any peptide from appearing twice in the document.
            RunUI(() => SkylineWindow.Undo());
            RunUI(() => SkylineWindow.Undo());
            var filterMatchedPeptidesDlg1 = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunUI(() => filterMatchedPeptidesDlg1.SetFilter(ViewLibraryPepMatching.DuplicateProteinsFilter.NoDuplicates));
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg1.OkDialog, messageDlg => messageDlg.Btn1Click());
            TestForDuplicatePeptides();

            // Test selecting first occurence prevents any peptide from appearing twice in the document.
            RunUI(() => SkylineWindow.Undo());
            var filterMatchedPeptidesDlg2 = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunUI(() => filterMatchedPeptidesDlg2.SetFilter(ViewLibraryPepMatching.DuplicateProteinsFilter.FirstOccurence));
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg2.OkDialog, messageDlg => messageDlg.Btn1Click());
            TestForDuplicatePeptides();
            
            // Test peptides are added to "Library Peptides" peptide list if this peptide group already exists.
            RunUI(() =>
                      {
                          SkylineWindow.Undo();
                          _viewLibUI.AssociateMatchingProteins = false;
                          _viewLibUI.AddPeptide();
                          _viewLibUI.ChangeSelectedPeptide("AAAP");
                          _viewLibUI.AddPeptide();
                      });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, 2, 3, 9);

            // Close the Library Explorer dialog
            OkDialog(_viewLibUI, _viewLibUI.CancelDialog);
        }

        private void AddAllPeptides(int? expectedUnmatched)
        {
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, addLibraryPepsDlg =>
            {
                if(expectedUnmatched != null)
                    Assert.AreEqual(expectedUnmatched, (int)addLibraryPepsDlg.Tag);
                addLibraryPepsDlg.Btn1Click();
            });
        }

        private void AddLibrary(EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec> editListUI, TestLibInfo info)
        {
            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                var nameTextBox = (TextBox)addLibUI.Controls.Find("textName", true)[0];
                Assert.IsNotNull(nameTextBox);
                var pathTextBox = (TextBox)addLibUI.Controls.Find("textPath", true)[0];
                Assert.IsNotNull(pathTextBox);
                nameTextBox.Text = info.Name;
                pathTextBox.Text = TestFilesDir.GetTestPath(info.Filename);
                addLibUI.OkDialog();
            });
        }

        private static void TestForDuplicatePeptides()
        {
            Assert.AreEqual(SkylineWindow.Document.PeptideCount,
                            (from nodePep in SkylineWindow.Document.Peptides
                             group nodePep by nodePep.Key into g
                             select g).Count());
        }

        private static void TestForDuplicateTransitionGroups()
        {
            Assert.AreEqual(SkylineWindow.Document.TransitionGroupCount,
                (from nodePep in SkylineWindow.Document.TransitionGroups
                 group nodePep by nodePep.Id into g
                 select g).Count());
        }

        private static void TestSamePeptides(IEnumerable<PeptideDocNode> peptides)
        {
            foreach (PeptideDocNode nodePep in peptides)
            {
                Assert.IsTrue(SkylineWindow.Document.Peptides.Contains(nodePep));
            }
        }
    }
}
