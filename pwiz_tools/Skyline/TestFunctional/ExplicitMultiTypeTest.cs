/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class ExplicitMultiTypeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExplicitMultiType()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));

            Settings.Default.HeavyModList.Clear();
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(StaticModList.GetDefaultsOn());

            // Show dialog asking user if text is FASTA or peptide list
            RunDlg<PasteTypeDlg>(() => SkylineWindow.Paste(TextUtil.LineSeparate("QFVLSCVILQFVLSCVILQFVLSCVILQFVLSCVILR",
                                                                                 "DIEVYCDGAITTKDIEVYCDGAITTKDIEVYCDGAITTK")),
                dlg => dlg.CancelDialog());

            // Add two peptides
            const string pepSequence1 = "QFVLSCVILR";
            const string pepSequence2 = "DIEVYCDGAITTK";
            RunUI(() => SkylineWindow.Paste(string.Join("\n", new[] {pepSequence1, pepSequence2})));

            // Check and save original document information
            var docOrig = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var pathPeptide = docOrig.GetPathTo((int) SrmDocument.Level.Molecules, 1);
            var peptideImplicit = (PeptideDocNode) docOrig.FindNode(pathPeptide);
            Assert.AreEqual(1, peptideImplicit.Children.Count);
            Assert.IsNull(peptideImplicit.ExplicitMods);

            // Add a new static modification on cysteine
            var peptideSettingsUI = ShowPeptideSettings();
            var modStatic = new StaticMod("Another Cysteine", "C", null, "CO8N2");
            AddStaticMod(modStatic, peptideSettingsUI);
            RunUI(peptideSettingsUI.OkDialog);
            WaitForClosedForm(peptideSettingsUI);

            // Select the peptide of interest
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = pathPeptide);

            Assert.AreSame(docOrig, SkylineWindow.Document);

            // Explicitly add the new modification
            var modifyPeptideDlg = ShowModifyPeptide();
            RunUI(() =>
                      {
                          modifyPeptideDlg.SelectModification(IsotopeLabelType.light,
                              pepSequence2.IndexOf('C'), modStatic.Name);
                          modifyPeptideDlg.OkDialog();
                      });
            
            // Check for correct response to modification
            var docExplicit = WaitForDocumentChange(docOrig);
            var peptideExplicit = (PeptideDocNode) docExplicit.FindNode(pathPeptide);
            Assert.AreEqual(1, peptideExplicit.Children.Count);
            // Precursor m/z value should have increased
            Assert.IsTrue(((TransitionGroupDocNode)peptideExplicit.Children[0]).PrecursorMz >
                ((TransitionGroupDocNode)peptideImplicit.Children[0]).PrecursorMz);

            var mods = peptideExplicit.ExplicitMods;
            Assert.IsNotNull(mods);
            Assert.IsTrue(mods.IsModified(IsotopeLabelType.light));
            // Heavy should not be explicitly modified
            Assert.IsFalse(mods.IsModified(IsotopeLabelType.heavy));

            // Add new label types
            peptideSettingsUI = ShowPeptideSettings();
            string[] heavyLabelNames = { "heavy AA", "heavy All" };
            SetHeavyLabelNames(peptideSettingsUI, heavyLabelNames);

            // Make sure label type list edit worked
            IsotopeLabelType[] heavyLabelTypes = peptideSettingsUI.LabelTypes.ToArray();
            Assert.AreEqual(2, heavyLabelTypes.Length);
            var labelTypeAa = heavyLabelTypes[0];
            var labelTypeAll = heavyLabelTypes[1];

            Assert.AreEqual(heavyLabelNames.Length, heavyLabelTypes.Length);
            for (int i = 0; i < heavyLabelTypes.Length; i++)
                Assert.AreEqual(heavyLabelNames[i], heavyLabelTypes[i].Name);

            // Add new heavy modifications
            var mod15N = new StaticMod("Label:15N", null, null, null, LabelAtoms.N15, null, null);
            AddHeavyMod(mod15N, peptideSettingsUI);
//            var mod13C = new StaticMod("All 13C", null, null, null, LabelAtoms.C13, null, null);
//            AddHeavyMod(mod13C, peptideSettingsUI);
            var modK13C = new StaticMod("Label:13C(6) (C-term K)", "K", ModTerminus.C, null, LabelAtoms.C13, null, null);
            AddHeavyMod(modK13C, peptideSettingsUI);
            var modR13C = new StaticMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, null, LabelAtoms.C13, null, null);
            AddHeavyMod(modR13C, peptideSettingsUI);
            var modV13C = new StaticMod("Label:13C(5)15N(1) (V)", "V", null, null, LabelAtoms.C13 | LabelAtoms.N15, null, null);
            AddHeavyMod(modV13C, peptideSettingsUI);

            // Set heavy modification for the peptides
            RunUI(() =>
                      {
                          peptideSettingsUI.SelectedLabelTypeName = heavyLabelNames[1];
                          peptideSettingsUI.PickedHeavyMods = new[] { mod15N.Name };
                          peptideSettingsUI.SelectedLabelTypeName = heavyLabelNames[0];
                          peptideSettingsUI.PickedHeavyMods = new[] { modK13C.Name, modR13C.Name };
                          peptideSettingsUI.OkDialog();
                      });

            // Make sure the document was updated as expected.  Explicit modification
            // should not keep new types from adding precursors to both peptides.
            var docMultiType = WaitForDocumentChange(docExplicit);
            foreach (var nodePep in docMultiType.Peptides)
            {
                Assert.AreEqual(3, nodePep.Children.Count);
                TransitionGroupDocNode nodeGroupLast = null;
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    if (nodeGroupLast != null)
                        Assert.IsTrue(nodeGroup.PrecursorMz > nodeGroupLast.PrecursorMz);
                    nodeGroupLast = nodeGroup;

                    var labelType = nodeGroup.TransitionGroup.LabelType;
                    if (!labelType.IsLight)
                        Assert.IsTrue(heavyLabelTypes.Contains(labelType));
                }
            }

            // Get the modified peptide in the new document
            peptideExplicit = (PeptideDocNode)docMultiType.FindNode(pathPeptide);

            // Explicitly set the heavy All modification to match the heavy AA modification
            var modifyPeptideMultiDlg = ShowModifyPeptide();
            RunUI(() =>
            {
                for (int i = 0; i < pepSequence2.Length - 1; i++)
                    modifyPeptideMultiDlg.SelectModification(labelTypeAll, i, "");
                modifyPeptideMultiDlg.SelectModification(labelTypeAll, pepSequence2.Length - 1, modK13C.Name);
                modifyPeptideMultiDlg.OkDialog();
            });

            // Make sure both heavy types have precursor m/z value that match the original
            var docModMatch = WaitForDocumentChange(docMultiType);
            var peptideMatch = (PeptideDocNode)docModMatch.FindNode(pathPeptide);
            AssertPrecursorMzAreEqaul(peptideExplicit, 1, peptideMatch, 1);
            AssertPrecursorMzAreEqaul(peptideExplicit, 1, peptideMatch, 2);

            // Remove the modification on K for heavyAA, and make sure this gets rid of the precursor
            var modifyPeptideEmptyDlg = ShowModifyPeptide();
            RunUI(() =>
            {
                modifyPeptideEmptyDlg.SelectModification(labelTypeAa, pepSequence2.Length - 1, "");
                modifyPeptideEmptyDlg.OkDialog();
            });
            var docModRemoved = WaitForDocumentChange(docModMatch);
            var peptideRemoved = (PeptideDocNode)docModRemoved.FindNode(pathPeptide);
            Assert.AreEqual(2, peptideRemoved.Children.Count);

            // Reset the modifications
            var modifyPeptideResetDlg = ShowModifyPeptide();
            RunUI(() =>
            {
                modifyPeptideResetDlg.ResetMods();
                modifyPeptideResetDlg.OkDialog();
            });            

            // Make sure this removes the explicit modification and adds back the removed precursor
            var docModReset = WaitForDocumentChange(docModRemoved);
            peptideImplicit = (PeptideDocNode)docModReset.FindNode(pathPeptide);
            Assert.IsNull(peptideImplicit.ExplicitMods);
            Assert.AreEqual(3, peptideImplicit.Children.Count);

            // Explicitly set the heavy All modification to match the heavy AA modification
            var modifyPeptideMatchDlg = ShowModifyPeptide();
            RunUI(() =>
            {
                for (int i = 0; i < pepSequence2.Length - 1; i++)
                    modifyPeptideMatchDlg.SelectModification(labelTypeAll, i, "");
                modifyPeptideMatchDlg.SelectModification(labelTypeAll, pepSequence2.Length - 1, modK13C.Name);
                modifyPeptideMatchDlg.OkDialog();
            });
            var docModAA = WaitForDocumentChange(docModReset);

            // Change implicit static modification and heavy AA
            var peptideSettingsUIChange = ShowPeptideSettings();
            RunUI(() =>
            {
                peptideSettingsUIChange.PickedStaticMods = new[] {modStatic.Name};
                peptideSettingsUIChange.PickedHeavyMods = new[] {modV13C.Name};
                peptideSettingsUIChange.OkDialog();
            });
            var docModImplicitStatic = WaitForDocumentChange(docModAA);
            var peptideMatch2 = (PeptideDocNode)docModImplicitStatic.FindNode(pathPeptide);
            Assert.IsNotNull(peptideMatch2.ExplicitMods);
            Assert.IsFalse(peptideMatch2.ExplicitMods.IsModified(heavyLabelTypes[0]));
            Assert.IsTrue(peptideMatch2.ExplicitMods.IsModified(heavyLabelTypes[1]));
            AssertPrecursorMzAreEqaul(peptideMatch, 0, peptideMatch2, 0);
            AssertPrecursorMzAreNotEqaul(peptideMatch, 1, peptideMatch2, 1);
            AssertPrecursorMzAreEqaul(peptideMatch, 2, peptideMatch2, 2);

            // Remove the heavy All type, which should remove the only explicit modification
            var peptideSettingsUIRemove = ShowPeptideSettings();
            SetHeavyLabelNames(peptideSettingsUIRemove, new[] {heavyLabelNames[0]});
            RunUI(peptideSettingsUIRemove.OkDialog);

            var docModRemoveType = WaitForDocumentChange(docModImplicitStatic);
            foreach (var nodePep in docModRemoveType.Peptides)
            {
                Assert.IsNull(nodePep.ExplicitMods);
                Assert.AreEqual(2, nodePep.Children.Count);
            }

            // Undo, and add an explicit modification to the heavy AA type
            RunUI(SkylineWindow.Undo);
            Assert.AreSame(docModImplicitStatic, SkylineWindow.Document);

            var modifyPeptideAADlg = ShowModifyPeptide();
            RunUI(() =>
            {
                modifyPeptideAADlg.SelectModification(labelTypeAa, pepSequence2.IndexOf('V'), "");
                modifyPeptideAADlg.SelectModification(labelTypeAa, pepSequence2.Length - 1, modK13C.Name);
                modifyPeptideAADlg.OkDialog();
            });
            var docModExplicitStatic = WaitForDocumentChange(docModImplicitStatic);

            // Remove the heavy All label type again
            peptideSettingsUIRemove = ShowPeptideSettings();
            SetHeavyLabelNames(peptideSettingsUIRemove, new[] { heavyLabelNames[0] });
            OkDialog(peptideSettingsUIRemove, peptideSettingsUIRemove.OkDialog);

            var docMultiExplicit = WaitForDocumentChange(docModExplicitStatic);
            var peptideStillExplicit = (PeptideDocNode)docMultiExplicit.FindNode(pathPeptide);
            Assert.IsNotNull(peptideStillExplicit.ExplicitMods);
            Assert.IsTrue(peptideStillExplicit.ExplicitMods.IsModified(labelTypeAa));
            Assert.IsFalse(peptideStillExplicit.ExplicitMods.IsModified(labelTypeAll));
            AssertPrecursorMzAreEqaul(peptideMatch, 1, peptideStillExplicit, 1);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static void AssertPrecursorMzAreEqaul(PeptideDocNode nodePep1, int index1, PeptideDocNode nodePep2, int index2)
// ReSharper restore SuggestBaseTypeForParameter
        {
            Assert.AreEqual(((TransitionGroupDocNode)nodePep1.Children[index1]).PrecursorMz,
                ((TransitionGroupDocNode)nodePep2.Children[index2]).PrecursorMz);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static void AssertPrecursorMzAreNotEqaul(PeptideDocNode nodePep1, int index1, PeptideDocNode nodePep2, int index2)
// ReSharper restore SuggestBaseTypeForParameter
        {
            Assert.AreNotEqual(((TransitionGroupDocNode)nodePep1.Children[index1]).PrecursorMz,
                ((TransitionGroupDocNode)nodePep2.Children[index2]).PrecursorMz);
        }

        private static EditPepModsDlg ShowModifyPeptide()
        {
            return ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
        }

        private static EditLabelTypeListDlg ShowEditLabelTypeListDlg(PeptideSettingsUI peptideSettingsUI)
        {
            return ShowDialog<EditLabelTypeListDlg>(peptideSettingsUI.EditLabelTypeList);
        }

        private static void SetHeavyLabelNames(PeptideSettingsUI peptideSettingsUI, string[] heavyLabelNames)
        {
            var editLabelTypeListDlg = ShowEditLabelTypeListDlg(peptideSettingsUI);
            RunUI(() =>
            {
                editLabelTypeListDlg.LabelTypeText = string.Join("\n", heavyLabelNames);
                editLabelTypeListDlg.OkDialog();
            });
            WaitForClosedForm(editLabelTypeListDlg);
        }
    }
}