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
using pwiz.Skyline.Model.Proteome;
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

        private const string ANL_COMBINED = "ANL Combined";
        private const string PHOSPHO_LIB = "PhosphoLib";
        private const string YEAST = "Yeast";

        private readonly TestLibInfo[] _testLibs = {
                                                       new TestLibInfo("HumanB2MGLib", "human_b2mg-5-06-2009-it.sptxt", "EVDLLK+"),
                                                       new TestLibInfo("HumanCRPLib", "human_crp-5-06-2009-it.sptxt", "TDMSR++"), 
                                                       new TestLibInfo(ANL_COMBINED, "ANL_combined.blib", ""),
                                                       new TestLibInfo(PHOSPHO_LIB, "phospho_30882_v2.blib", ""),
                                                       new TestLibInfo(YEAST, "Yeast_atlas.blib", "")
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
            SetUpTestLibraries();
            TestBasicFunctionality();
            RunDlg<MultiButtonMsgDlg>(SkylineWindow.NewDocument, msgDlg => msgDlg.Btn1Click());
            TestModificationMatching();
        }

        private void SetUpTestLibraries()
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

            RunUI(() => PeptideSettingsUI.OkDialog());
            WaitForClosedForm(PeptideSettingsUI);
        }

        private void TestBasicFunctionality()
        {
            // Launch the Library Explorer dialog
            _viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);

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
            var modDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(modDlg.Btn1Click);

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
            if (Equals(previousPeptide, selPeptide))
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
            RunDlg<MultiButtonMsgDlg>(() => libComboBox.SelectedIndex = 1, dlg => dlg.Btn1Click());
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
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(1, 1, 1, 3))
            {
                RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddPeptide, msgDlg => msgDlg.Btn0Click());
            }
            RunUI(SkylineWindow.EditDelete);

            // Test unmatched peptides are correct.
            // One unmatched because its precursor m/z is outside the instrument measurement range
            AddAllPeptides(1, 4, 3);

            // Verify that everything matches, given a wide enough mass range
            RunUI(() => SkylineWindow.ModifyDocument("Change m/z range",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionInstrument(inst =>
                        inst.ChangeMaxMz(TransitionInstrument.MAX_MEASURABLE_MZ)))));
            WaitForConditionUI(() => !_viewLibUI.HasUnmatchedPeptides);
            RunUI(() => SkylineWindow.ModifyDocument("Change m/z range",
                    doc => doc.ChangeSettings(doc.Settings.ChangeTransitionInstrument(inst =>
                        inst.ChangeMaxMz(1500)))));
            WaitForConditionUI(() => _viewLibUI.HasUnmatchedPeptides);

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
            AddAllPeptides();
            var docAddBack = SkylineWindow.Document;
            // Peptides will be added back in a different order
            AssertEx.IsDocumentState(docAddBack, null,
                docOriginal.PeptideGroupCount, docOriginal.PeptideCount,
                docOriginal.PeptideTransitionGroupCount, docOriginal.PeptideTransitionCount);
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
            AddAllPeptides();
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
            AddAllPeptides();

            // All peptides should have a heavy label transition group. 
            // There should be no peptide whose children do not contain a transition group with heavy label type.
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep =>
                !nodePep.Children.Contains(nodeGroup =>
                    ((TransitionGroupDocNode)nodeGroup).TransitionGroup.LabelType.Equals(IsotopeLabelType.heavy))));

            // Test peptide setting changes update the library explorer.
            RunUI(() => SkylineWindow.ModifyDocument("Change static modifications", 
                doc => doc.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new List<StaticMod>())))));
            AddAllPeptides(1, 8, 3);

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
            
            // Switch to ANL_Combined library
            RunDlg<MultiButtonMsgDlg>(() => { libComboBox.SelectedIndex = 2; }, msgDlg => msgDlg.BtnCancelClick());

            // User prompted to add library since not in current settings.
            RunDlg<MultiButtonMsgDlg>(() => _viewLibUI.CheckLibraryInSettings(), msgDlg => msgDlg.Btn0Click());
            // Add single peptide to the document.
            RunUI(_viewLibUI.AddPeptide);
            WaitForProteinMetadataBackgroundLoaderCompletedUI();
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
            RunDlg<MultiButtonMsgDlg>(() => libComboBox.SelectedIndex = 3, msgDlg => msgDlg.Btn1Click());

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
                return backgroundProteome.HasDigestion(peptideSettings);
            });
            WaitForDocumentLoaded(); // Give background loader a chance to get the protein metadata too

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
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(4, 10, 13, 39))
            {
                RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, messageDlg => messageDlg.Btn1Click());
            }
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep => !nodePep.HasLibInfo));

            // Test adding a single peptide that matches two different proteins using keep all.
            RunUI(() =>
                      {
                          SkylineWindow.Undo();
                          _viewLibUI.ChangeSelectedPeptide("ADTGIAVEGATDAAR+");
                      });
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(2, 2, 2, 6))
            {
                RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                    filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());
            }

            // Test adding a second charge state for that peptide.
            RunUI(() => _viewLibUI.ChangeSelectedPeptide("ADTGIAVEGATDAAR++"));
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(2, 2, 4, 12))
            {
                RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                    filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());
            }
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                      filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog());

            // Test adding a second charge state - No Duplicates.
            RunUI(() => SkylineWindow.Undo());
            var docPrev = WaitForDocumentLoaded();
            RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                      filterMatchedPeptideDlg =>
            {
                filterMatchedPeptideDlg.DuplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.NoDuplicates;
                filterMatchedPeptideDlg.OkDialog();
            });
            Assert.AreEqual(docPrev, WaitForDocumentLoaded());

            // Test adding a second charge state - First Only.
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(2, 2, 3, 9))
            {
                RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide, filterMatchedPeptideDlg =>
                {
                    filterMatchedPeptideDlg.DuplicateProteinsFilter =
                        BackgroundProteome.DuplicateProteinsFilter.FirstOccurence;
                    filterMatchedPeptideDlg.OkDialog();
                });
            }

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
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(2, 2, 3, 9))
            {
                RunDlg<FilterMatchedPeptidesDlg>(_viewLibUI.AddPeptide,
                    filterMatchedPeptideDlg => filterMatchedPeptideDlg.OkDialog()); // First only
            }
            var peptideGroups = SkylineWindow.Document.PeptideGroups.ToArray();
            int index = peptideGroups.IndexOf(nodeGroup => nodeGroup.IsPeptideList);
            Assert.IsTrue(peptideGroups[index].Children.Count == 1);

            // Test selecting no duplicates prevents any peptide from appearing twice in the document.
            RunUI(() => SkylineWindow.Undo());
            RunUI(() => SkylineWindow.Undo());
            var filterMatchedPeptidesDlg1 = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunUI(() => filterMatchedPeptidesDlg1.DuplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.NoDuplicates);
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg1.OkDialog, messageDlg => messageDlg.Btn1Click());
            TestForDuplicatePeptides();

            // Test selecting first occurence prevents any peptide from appearing twice in the document.
            RunUI(() => SkylineWindow.Undo());
            var filterMatchedPeptidesDlg2 = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            RunUI(() => filterMatchedPeptidesDlg2.DuplicateProteinsFilter = BackgroundProteome.DuplicateProteinsFilter.FirstOccurence);
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg2.OkDialog, messageDlg => messageDlg.Btn1Click());
            TestForDuplicatePeptides();

            // Test peptides are added to "Library Peptides" peptide list if this peptide group already exists.
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(1, 2, 3, 9))
            {
                RunUI(() =>
                    {
                        SkylineWindow.Undo();
                        _viewLibUI.AssociateMatchingProteins = false;
                        _viewLibUI.AddPeptide();
                        _viewLibUI.ChangeSelectedPeptide("AAAP");
                        _viewLibUI.AddPeptide();
                    });
            }

            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.EditDelete();
            });

            // Close the Library Explorer dialog
            OkDialog(_viewLibUI, _viewLibUI.CancelDialog);
        }

        private ComboBox _libComboBox;
        private ListBox _pepList;

        private void TestModificationMatching()
        {
            var phosphoLossMod = new StaticMod("Phospho Loss", "S, T", null, true, "HPO3",
                LabelAtoms.None, RelativeRT.Matching, null, null, new[] { new FragmentLoss("H3PO4"), });

            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.Add(phosphoLossMod.ChangeExplicit(false));

            var phosphoNotVariable = SkylineWindow.Document.Settings.ChangePeptideModifications(
                mods => mods.ChangeStaticModifications(new[] { phosphoLossMod.ChangeExplicit(false) }));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods",
                                                     doc => doc.ChangeSettings(phosphoNotVariable)));

            RelaunchLibExplorer(false, PHOSPHO_LIB);

            // Test doesn't find variable match if implicit match exists in doc
            WaitForConditionUI(() => !_viewLibUI.HasUnmatchedPeptides);
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(1, 4, 4, 12))
            {
                RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddAllPeptides, msgDlg => msgDlg.Btn1Click());
            }
            Assert.IsTrue(SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications
                .Contains(mod => mod.Equivalent(phosphoLossMod)));
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications
                .Contains(mod => mod.Equivalent(phosphoLossMod) && mod.IsVariable));
            RunUI(SkylineWindow.Undo);

            // Test creates variable mod if implicit match exists in globals
            var pepModsNoMods1 =
                SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                    mods.ChangeStaticModifications(new StaticMod[0]));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc => 
                doc.ChangeSettings(pepModsNoMods1)));

            RelaunchLibExplorer(false, PHOSPHO_LIB);

            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(1, 4, 4, 12))
            {
                RunDlg<MultiButtonMsgDlg>(_viewLibUI.AddAllPeptides, msgDlg => msgDlg.Btn1Click());
            }
            Assert.IsTrue(SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications
                .Contains(mod => mod.Equivalent(phosphoLossMod) && mod.IsVariable));
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications
                .Contains(mod => mod.Equivalent(phosphoLossMod) && !mod.IsVariable));

            // Test implicit mods only created if safe to do so
            RunUI(() => SkylineWindow.Undo());
            RunDlg<MultiButtonMsgDlg>(() => _libComboBox.SelectedIndex = 4, msgDlg => msgDlg.Btn1Click());
            WaitForConditionUI(() => _pepList.SelectedIndex != -1);
            WaitForConditionUI(() => _viewLibUI.HasMatches);
            RunDlg<MultiButtonMsgDlg>(() => _viewLibUI.CheckLibraryInSettings(),
                                      msgDlg => msgDlg.Btn0Click());
       
            var fmpDlg0 = ShowDialog<FilterMatchedPeptidesDlg>(() => _viewLibUI.AddAllPeptides());
            RunUI(() => fmpDlg0.AddFiltered = true);
            using (new CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(1, 82, 96,
                TransitionGroup.IsAvoidMismatchedIsotopeTransitions ? 279 : 282))
            {
                RunDlg<MultiButtonMsgDlg>(fmpDlg0.OkDialog, msgDlg => msgDlg.Btn1Click());
            }
            // Implicit mod is created because there are no conflicting peptides in the current document.
            Assert.IsTrue(
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Contains(mod =>
                !mod.IsExplicit && !mod.IsVariable));

            var nodeRemoved = "";
            RunUI(() =>
            {
                Settings.Default.EditFindText = "C";
                SkylineWindow.FindNext(false);
                nodeRemoved = ((PeptideTreeNode) SkylineWindow.SelectedNode).DocNode.Peptide.Sequence;
                SkylineWindow.EditDelete();
            });
            Assert.IsFalse(
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Contains(mod =>
                mod.IsExplicit));
            var pepModsNoMods2 =
                SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                    mods.ChangeStaticModifications(new StaticMod[0]));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc =>
                doc.ChangeSettings(pepModsNoMods2)));
            RelaunchLibExplorer(false, YEAST);
            RunUI(() =>
            {
                _viewLibUI.ChangeSelectedPeptide(nodeRemoved.Replace("C", "C[+57.0]"));
                _viewLibUI.AddPeptide();
            });

            // Explicit mod is created because an implicit mod conflicts with current document settings.
            Assert.IsFalse(
                SkylineWindow.Document.Settings.PeptideSettings.Modifications.StaticModifications.Contains(mod =>
                !mod.IsExplicit));
            Assert.IsTrue(SkylineWindow.Document.Peptides.Contains(nodePep => 
                nodePep.Peptide.Sequence == nodeRemoved && nodePep.HasExplicitMods));

            // Implicit modifications only added to document if they apply to the added peptide
            RunUI(() =>
                      {
                          SkylineWindow.SelectAll();
                          SkylineWindow.EditDelete();
                      });
            var pepModsNoMods3 =
                SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new StaticMod[0]));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc =>
                doc.ChangeSettings(pepModsNoMods3)));
            Settings.Default.StaticModList.Clear();
            RelaunchLibExplorer(true, ANL_COMBINED);
            RunUI(() => _viewLibUI.AddPeptide());
            Assert.AreEqual(0, Settings.Default.StaticModList.Count);

            // Variable mods not added if conflict with existing peptides.
            RunUI(() =>
            {
                _pepList.SelectedIndex = 1;
                _viewLibUI.AddPeptide();
            });
            Assert.IsFalse(SkylineWindow.Document.Peptides.Contains(nodePep => 
                nodePep.HasExplicitMods && nodePep.ExplicitMods.IsVariableStaticMods));

            // Test removing mod from globals affects matches
            var pepModsNoMods4 = SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(Settings.Default.StaticModList.GetDefaults().ToArray()));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc =>
                doc.ChangeSettings(pepModsNoMods4)));
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(Settings.Default.StaticModList.GetDefaults());
            RelaunchLibExplorer(false, YEAST);
            Assert.IsFalse(_viewLibUI.HasUnmatchedPeptides);

            // Test removing mod from doc does not remove matches
            Settings.Default.StaticModList.Clear();
            var pepModsNone = SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new StaticMod[0]));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc =>
                doc.ChangeSettings(pepModsNone)));
            RunUI(SkylineWindow.Activate);
            RunUI(_viewLibUI.Activate);
            WaitForConditionUI(() => _pepList.Items.Count == 96);
            WaitForConditionUI(() => _pepList.SelectedIndex != -1);
            Assert.IsFalse(_viewLibUI.HasUnmatchedPeptides);

            // Relaunch explorer without modification matching
            RunUI(() => _viewLibUI.CancelDialog());
            WaitForClosedForm(_viewLibUI);
            _viewLibUI = ShowDialog<ViewLibraryDlg>(() => SkylineWindow.OpenLibraryExplorer(YEAST));
            var matchedPepModsDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(matchedPepModsDlg.BtnCancelClick);
            WaitForConditionUI(() => _pepList.Items.Count == 96);
            WaitForConditionUI(() => _pepList.SelectedIndex != -1);
            Assert.IsTrue(_viewLibUI.HasUnmatchedPeptides);

            // Test adding mod to doc affects matches
            Settings.Default.StaticModList.AddRange(Settings.Default.StaticModList.GetDefaults());
            var pepModsDefault = SkylineWindow.Document.Settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(Settings.Default.StaticModList.Select(mod =>
                    mod.ChangeExplicit(false)).ToArray()));
            RunUI(() => SkylineWindow.ModifyDocument("Change static mods", doc =>
                doc.ChangeSettings(pepModsDefault)));
            RunUI(SkylineWindow.Activate);
            RunUI(_viewLibUI.Activate);
            WaitForConditionUI(() => _pepList.Items.Count == 96);
            WaitForConditionUI(() => _pepList.SelectedIndex != -1);
            Assert.IsFalse(_viewLibUI.HasUnmatchedPeptides);

            RunUI(() => _viewLibUI.CancelDialog());
            WaitForClosedForm(_viewLibUI);
        }

        private void RelaunchLibExplorer(bool showModsDlg, string libName)
        {
            if (_viewLibUI != null)
            {
                RunUI(() => _viewLibUI.CancelDialog());
                WaitForClosedForm(_viewLibUI);
            }
            _viewLibUI = ShowDialog<ViewLibraryDlg>(() => SkylineWindow.OpenLibraryExplorer(libName));
            if (showModsDlg)
            {
                var modDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                if (modDlg != null)
                    RunUI(modDlg.Btn1Click);
                WaitForClosedForm(modDlg);
            }
            RunUI(() =>
            {
                // Find the libary combobox control
                _libComboBox = (ComboBox)_viewLibUI.Controls.Find("comboLibrary", true)[0];
                Assert.IsNotNull(_libComboBox);
                // Find the peptides list control
                _pepList = (ListBox)_viewLibUI.Controls.Find("listPeptide", true)[0];
                Assert.IsNotNull(_pepList);
            });
            WaitForConditionUI(() => _pepList.SelectedIndex != -1);
            WaitForConditionUI(() => _viewLibUI.HasMatches || FindOpenForm<MultiButtonMsgDlg>() != null);
            Assert.IsNull(FindOpenForm<MultiButtonMsgDlg>());
            var docLibraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            if (docLibraries.GetLibrary(libName) == null)
                RunDlg<MultiButtonMsgDlg>(() => _viewLibUI.CheckLibraryInSettings(), msgDlg => msgDlg.Btn0Click());            
        }

        private void AddAllPeptides(int? expectedUnmatched = null, int? explicitMods = null, int? variableMods = null)
        {
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(_viewLibUI.AddAllPeptides);
            var docBefore = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            RunDlg<MultiButtonMsgDlg>(filterMatchedPeptidesDlg.OkDialog, addLibraryPepsDlg =>
            {
                if(expectedUnmatched != null)
                    Assert.AreEqual(expectedUnmatched, (int)addLibraryPepsDlg.Tag);
                addLibraryPepsDlg.Btn1Click();
            });
            var docAfter = WaitForDocumentChange(docBefore);
            if (explicitMods.HasValue)
                Assert.AreEqual(explicitMods.Value, CountExplicitMods(docAfter) - CountExplicitMods(docBefore));
            if (variableMods.HasValue)
                Assert.AreEqual(variableMods.Value, CountVariableMods(docAfter) - CountVariableMods(docBefore));
        }

        /// <summary>
        /// Returns a count of peptides in a document with explicit modifications which contain more
        /// than just variable static mods.
        /// </summary>
        private static int CountExplicitMods(SrmDocument doc)
        {
            return doc.Peptides.Count(
                nodePep => nodePep.HasExplicitMods &&
                           (!nodePep.HasVariableMods || nodePep.ExplicitMods.GetHeavyModifications().Any()));
        }

        /// <summary>
        /// Returns a count of peptides in a document with variable modifications.
        /// </summary>
        private int CountVariableMods(SrmDocument doc)
        {
            return doc.Peptides.Count(nodePep => nodePep.HasVariableMods);
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
            foreach (var tranGroup in SkylineWindow.Document.PeptideTransitionGroups)
            {
                TransitionGroupDocNode @group = tranGroup;
                Assert.AreEqual(1, SkylineWindow.Document.PeptideTransitionGroups.Count(docTranGroup =>
                                                                                 ReferenceEquals(docTranGroup, group)));
            }
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
