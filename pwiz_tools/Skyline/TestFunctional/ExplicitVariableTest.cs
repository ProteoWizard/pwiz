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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryExplorerTest
    /// </summary>
    [TestClass]
    public class ExplicitVariableTest : AbstractFunctionalTest
    {
        private const string TEXT_FASTA_YEAST_39 =
            ">YAL039C CYC3 SGDID:S000000037, Chr I from 69526-68717, reverse complement, Verified ORF, \"Cytochrome c heme lyase (holocytochrome c synthase), attaches heme to apo-cytochrome c (Cyc1p or Cyc7p) in the mitochondrial intermembrane space; human ortholog may have a role in microphthalmia with linear skin defects (MLS)\"\n" +
            "MGWFWADQKTTGKDIGGAAVSSMSGCPVMHESSSSSPPSSECPVMQGDNDRINPLNNMPE\n" +
            "LAASKQPGQKMDLPVDRTISSIPKSPDSNEFWEYPSPQQMYNAMVRKGKIGGSGEVAEDA\n" +
            "VESMVQVHNFLNEGCWQEVLEWEKPHTDESHVQPKLLKFMGKPGVLSPRARWMHLCGLLF\n" +
            "PSHFSQELPFDRHDWIVLRGERKAEQQPPTFKEVRYVLDFYGGPDDENGMPTFHVDVRPA\n" +
            "LDSLDNAKDRMTRFLDRMISGPSSSSSAP*\n";

        [TestMethod]
        public void TestExplicitVariable()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Create modifications used in this test
            var aquaMods = new[]
            {
                new StaticMod("Heavy K", "K", ModTerminus.C, null, LabelAtoms.C13|LabelAtoms.N15, null, null),
                new StaticMod("Heavy R", "R", ModTerminus.C, null, LabelAtoms.C13|LabelAtoms.N15, null, null)
            };
            var explicitMod = new StaticMod("13C L", "L", null, null, LabelAtoms.C13, null, null);
            var variableMod = new StaticMod("Methionine Oxidized", "M", null, true, "O",
                LabelAtoms.None, RelativeRT.Matching, null, null, null);

            Settings.Default.HeavyModList.Clear();
            Settings.Default.HeavyModList.AddRange(aquaMods);
            Settings.Default.HeavyModList.Add(explicitMod);
            Settings.Default.StaticModList.Clear();
            Settings.Default.StaticModList.AddRange(StaticModList.GetDefaultsOn());
            var carbMod = Settings.Default.StaticModList[0];
            Settings.Default.StaticModList.Add(variableMod);

            // Clean-up before running the test
            var settings = SrmSettingsList.GetDefault().ChangePeptideModifications(mod =>
                mod.ChangeHeavyModifications(aquaMods));
            RunUI(() => SkylineWindow.ModifyDocument("Set test settings",
                                                     doc => doc.ChangeSettings(settings)));

            // Add FASTA sequence
            RunUI(() => SkylineWindow.Paste(TEXT_FASTA_YEAST_39));

            // Check and save original document information
            var docOrig = WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var pathPeptide = docOrig.GetPathTo((int) SrmDocument.Level.Molecules, docOrig.PeptideCount - 3);
            var peptideOrig = (PeptideDocNode) docOrig.FindNode(pathPeptide);
            Assert.AreEqual(2, peptideOrig.Children.Count);
            Assert.IsNull(peptideOrig.ExplicitMods);

            // Add methionine oxidation variable modification
            SetStaticModifications(names => new List<string>(names) { variableMod.Name });
            var docVarMod = WaitForDocumentChange(docOrig);

            // Check that variable modification worked, and that the peptide of
            // interest is variably modified.
            Assert.IsTrue(docOrig.PeptideCount < docVarMod.PeptideCount);
            pathPeptide = docVarMod.GetPathTo((int)SrmDocument.Level.Molecules, docVarMod.PeptideCount - 4);
            var peptideVarMod = (PeptideDocNode)docVarMod.FindNode(pathPeptide);
            Assert.AreEqual(2, peptideVarMod.Children.Count);
            Assert.IsTrue(peptideVarMod.HasVariableMods,
                string.Format("No variable modifications found on the peptide {0}", peptideVarMod.Peptide.Sequence));
            Assert.IsFalse(peptideVarMod.ExplicitMods.IsModified(IsotopeLabelType.heavy));
            AssertPrecursorMzIsModified(peptideVarMod, 0, peptideVarMod, 1, -5, 0.2);

            // Select the peptide of interest
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = pathPeptide);

            // Make sure the explicit modifications dialog does not modify the
            // peptide when nothing is changed.
            string sequence = peptideVarMod.Peptide.Sequence;
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                for (int i = 0; i < sequence.Length; i++)
                {
                    dlg.SelectModification(IsotopeLabelType.light, i, "");
                    dlg.SelectModification(IsotopeLabelType.heavy, i, "");
                }
                dlg.ResetMods();
                dlg.OkDialog();
            });
            Assert.AreSame(docVarMod, SkylineWindow.Document);

            // Explicitly change the heavy modification
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.SelectModification(IsotopeLabelType.heavy, sequence.Length - 1, "");
                dlg.SelectModification(IsotopeLabelType.heavy, sequence.LastIndexOf('L'),
                                       explicitMod.Name);
                dlg.OkDialog();
            });
            
            // Check for correct response to modification
            var docExplicit = WaitForDocumentChange(docOrig);
            var peptideExplicit = (PeptideDocNode) docExplicit.FindNode(pathPeptide);
            Assert.AreEqual(2, peptideExplicit.Children.Count);
            // Precursor m/z for light should be same
            AssertPrecursorMzAreEqaul(peptideVarMod, 0, peptideExplicit, 0);
            // Heavy should have changed
            AssertPrecursorMzIsModified(peptideExplicit, 0, peptideExplicit, 1, -3, 0.2);
            // Heavy should now be explicitly modified
            Assert.IsTrue(peptideExplicit.ExplicitMods.IsModified(IsotopeLabelType.heavy));

            // Remove carbamidomethyl cysteine implicit modification
            SetStaticModifications(names => new[] { variableMod.Name });
            var docNoImplicit = WaitForDocumentChange(docExplicit);
            var peptideNoImplicit = (PeptideDocNode)docNoImplicit.FindNode(pathPeptide);
            // Light should have gotten 57.0 lighter
            const double modCarbMz = 57.0/2;
            AssertPrecursorMzIsModified(peptideExplicit, 0, peptideNoImplicit, 0, modCarbMz, 0.1);
            // Heavy delta should not have changed
            AssertPrecursorMzIsModified(peptideNoImplicit, 0, peptideNoImplicit, 1, -3, 0.2);

            // Reset should still return to implicit heavy mods without removing
            // variable mods.
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.ResetMods();
                dlg.OkDialog();
            });
            var docReset = WaitForDocumentChange(docNoImplicit);
            var peptideReset = (PeptideDocNode)docReset.FindNode(pathPeptide);
            Assert.IsTrue(peptideReset.HasExplicitMods);
            Assert.IsFalse(peptideReset.ExplicitMods.IsModified(IsotopeLabelType.heavy));
            AssertPrecursorMzIsModified(peptideReset, 0, peptideReset, 1, -5, 0.2);

            // Explicitly add back the Carbamidomethyl Cysteine
            // Reset should still return to implicit heavy mods
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.SelectModification(IsotopeLabelType.light, sequence.IndexOf('C'), carbMod.Name);
                dlg.OkDialog();
            });
            var docExCarb = WaitForDocumentChange(docReset);
            var peptideExCarb = (PeptideDocNode)docExCarb.FindNode(pathPeptide);
            Assert.IsTrue(peptideExCarb.HasExplicitMods);
            Assert.IsFalse(peptideExCarb.HasVariableMods);
            AssertPrecursorMzAreEqaul(peptideVarMod, 0, peptideExCarb, 0);
            AssertPrecursorMzAreEqaul(peptideVarMod, 1, peptideExCarb, 1);

            // Reset at this point should completely remove explicit modifications
            // including oxidized M.
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.ResetMods();
                dlg.OkDialog();
            });
            var docResetImplicit = WaitForDocumentChange(docExCarb);
            var peptideResetImplicit = (PeptideDocNode) docResetImplicit.FindNode(pathPeptide);
            Assert.IsFalse(peptideResetImplicit.HasExplicitMods);
            AssertPrecursorMzIsModified(peptideOrig, 0, peptideResetImplicit, 0, modCarbMz, 0.1);
            AssertPrecursorMzIsModified(peptideOrig, 1, peptideResetImplicit, 1, modCarbMz, 0.1);

            // Turn off the variable modifications and explicitly modify using a variable mod
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.PickedStaticMods = new string[0];
                dlg.OkDialog();
            });

            var docNoStaticMods = WaitForDocumentChange(docResetImplicit);

            // Explicitly modify the first peptide
            var pathPeptideFirst = docNoStaticMods.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            var peptideUnmod = (PeptideDocNode) docNoStaticMods.FindNode(pathPeptideFirst);
            RunUI(() => SkylineWindow.SelectedPath = pathPeptideFirst);
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                var sequenceUnmod = peptideUnmod.Peptide.Sequence;
                dlg.SelectModification(IsotopeLabelType.light, sequenceUnmod.IndexOf('M'), variableMod.Name);
                dlg.OkDialog();
            });

            var docExplicitVarMod = WaitForDocumentChange(docNoStaticMods);
            var peptideExplicitVarMod = (PeptideDocNode) docExplicitVarMod.FindNode(pathPeptideFirst);
            Assert.IsTrue(peptideExplicitVarMod.HasExplicitMods);
            if (peptideExplicitVarMod.ExplicitMods.StaticModifications == null)
            {
                Assert.IsNotNull(peptideExplicitVarMod.ExplicitMods.StaticModifications);
                return; // For ReSharper
            }
            var varModPeptide = peptideExplicitVarMod.ExplicitMods.StaticModifications[0].Modification;
            Assert.AreEqual(variableMod.Name, varModPeptide.Name);
            // The modification instance on the peptide should not be marked as variable
            Assert.IsFalse(varModPeptide.IsVariable);
            var varModSettings = docExplicitVarMod.Settings.PeptideSettings.Modifications.StaticModifications[0];
            Assert.AreEqual(variableMod.Name, varModSettings.Name);
            Assert.IsTrue(varModSettings.IsExplicit);
            Assert.IsFalse(varModSettings.IsVariable);

            // Make sure this did not turn on the variable modification in the settings UI
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                Assert.AreEqual(0, dlg.PickedStaticMods.Length);
                dlg.OkDialog();
            });

            Directory.CreateDirectory(TestContext.TestDir);
            string saveFilePath = TestContext.GetTestPath("TestExplicitVariable.sky");
            WaitForProteinMetadataBackgroundLoaderCompletedUI(); // make sure doc is complete before save

            RunUI(() =>
                {
                    Assert.IsTrue(SkylineWindow.SaveDocument(saveFilePath));
                    SkylineWindow.NewDocument();
                    Assert.IsTrue(SkylineWindow.OpenFile(saveFilePath));
                });

            WaitForProteinMetadataBackgroundLoaderCompletedUI();
            var docRestored = SkylineWindow.Document;
            var pathPeptideFirstNew = docRestored.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            var peptideExplicitVarModNew = docRestored.FindNode(pathPeptideFirstNew);
            Assert.AreEqual(peptideExplicitVarMod, peptideExplicitVarModNew,
                "Saved peptide with explicit variable modification was not restored correctly.");
            Assert.IsTrue(Settings.Default.StaticModList.Contains(variableMod),
                "Expected variable modification has been removed from the global list.");
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static void AssertPrecursorMzAreEqaul(PeptideDocNode nodePep1, int index1, PeptideDocNode nodePep2, int index2)
// ReSharper restore SuggestBaseTypeForParameter
        {
            Assert.AreEqual(((TransitionGroupDocNode)nodePep1.Children[index1]).PrecursorMz,
                            ((TransitionGroupDocNode)nodePep2.Children[index2]).PrecursorMz);
        }

// ReSharper disable SuggestBaseTypeForParameter
        private static void AssertPrecursorMzIsModified(PeptideDocNode nodePep1, int index1, PeptideDocNode nodePep2, int index2, double deltaMass, double tolerance)
// ReSharper restore SuggestBaseTypeForParameter
        {
            Assert.AreEqual(((TransitionGroupDocNode) nodePep1.Children[index1]).PrecursorMz,
                            ((TransitionGroupDocNode) nodePep2.Children[index2]).PrecursorMz + deltaMass, tolerance);
        }
    }
}