/*
 * Original author: MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Localization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// End-to-end test of the "site-determining ions" filter button on the
    /// <see cref="PopupPickList"/>. Builds a peptide that carries an ambiguously-placed
    /// phospho (multiple candidate serines, one explicitly modified) and verifies that:
    ///  - the toggle button appears for the localizable precursor,
    ///  - toggling it narrows the visible transitions to exactly the site-determining set
    ///    computed by a freshly-constructed <see cref="SiteDeterminingIonAnalyzer"/>, and
    ///  - the button is hidden for an unmodified (non-localizable) peptide.
    ///
    /// Assertions are made on transition identities (ion type + cleavage offset) and
    /// booleans/counts, never on localized labels, so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class SiteDeterminingIonFunctionalTest : AbstractFunctionalTest
    {
        private const string LOCALIZABLE_SEQ = "SVSSPLSK"; // S0 V1 S2 S3 P4 L5 S6 K7 - four candidate serines
        private const int PHOSPHO_INDEX = 2;               // Explicit phospho on one interior serine
        private const string UNMODIFIED_SEQ = "ELVIK";     // No candidate serine -> not localizable

        private static readonly StaticMod PHOSPHO = new StaticMod("Phospho", "S,T,Y", null, true, "PO3H",
            LabelAtoms.None, RelativeRT.Matching, null, null, null);

        [TestMethod]
        public void TestSiteDeterminingIonFunctional()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Make phospho available for explicit placement (do NOT pick it as a document static
            // mod, so it is not implicitly applied to every serine).
            RunUI(() => Settings.Default.StaticModList.Add(PHOSPHO));

            // Include both b and y ions so the pick list offers a broad transition set.
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.SelectedTab = TransitionSettingsUI.TABS.Filter;
                dlg.PrecursorCharges = "2";
                dlg.ProductCharges = "1";
                dlg.FragmentTypes = "b, y";
                dlg.SetAutoSelect = true;
                dlg.OkDialog();
            });

            // Add the two peptides.
            var docStart = SkylineWindow.Document;
            RunUI(() => SkylineWindow.Paste(LOCALIZABLE_SEQ));
            var docPep1 = WaitForDocumentChange(docStart);
            RunUI(() => SkylineWindow.Paste(UNMODIFIED_SEQ));
            var docPep2 = WaitForDocumentChange(docPep1);

            // Explicitly phosphorylate exactly one serine of the localizable peptide.
            SelectPrecursor(LOCALIZABLE_SEQ);
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.SelectModification(IsotopeLabelType.light, PHOSPHO_INDEX, PHOSPHO.Name);
                dlg.OkDialog();
            });
            var docMod = WaitForDocumentChange(docPep2);

            // Sanity check: the modified peptide is now localizable.
            var pepNodeLoc = FindPeptide(docMod, LOCALIZABLE_SEQ);
            Assert.IsNotNull(pepNodeLoc.ExplicitMods);
            var analyzer = new SiteDeterminingIonAnalyzer(docMod.Settings, pepNodeLoc);
            Assert.IsTrue(analyzer.CanLocalize, "Expected the explicitly-phosphorylated peptide to be localizable");

            // Scenario 1 + 2: localizable precursor - button visible; filter narrows to the
            // site-determining set.
            SelectPrecursor(LOCALIZABLE_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsTrue(tgNode.CanShowSiteDeterminingIons,
                    "Picker should report the localizable precursor as showing site-determining ions");
            });

            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                Assert.IsTrue(dlg.SiteDeterminingButtonVisible,
                    "Site-determining button should be visible for a localizable precursor");

                // Full (unfiltered) transition choices, keyed by identity.
                var fullChoices = dlg.VisibleChoices.ToArray();
                Assert.IsTrue(fullChoices.All(c => c.Id is Transition),
                    "All transition-group children should carry a Transition identity");
                var fullIndexes = new HashSet<int>(fullChoices.Select(c => c.Id.GlobalIndex));

                // Turn the site-determining filter on and capture the narrowed set.
                dlg.SiteDeterminingFilter = true;
                var filteredChoices = dlg.VisibleChoices.ToArray();
                var filteredIndexes = new HashSet<int>(filteredChoices.Select(c => c.Id.GlobalIndex));

                // Strict, non-empty subset.
                Assert.IsTrue(filteredIndexes.Count > 0, "Expected at least one site-determining transition");
                Assert.IsTrue(filteredIndexes.Count < fullIndexes.Count,
                    "Site-determining set should be a strict subset of all transitions");
                Assert.IsTrue(filteredIndexes.IsSubsetOf(fullIndexes),
                    "Site-determining set should be a subset of all transitions");

                // The filtered set is exactly the transitions the analyzer flags as
                // site-determining (compared by transition identity, not label).
                foreach (var choice in fullChoices)
                {
                    var transition = (Transition) choice.Id;
                    bool expected = analyzer.IsSiteDetermining(transition);
                    bool actual = filteredIndexes.Contains(choice.Id.GlobalIndex);
                    Assert.AreEqual(expected, actual,
                        "Filter mismatch for {0} offset {1}", transition.IonType, transition.CleavageOffset);
                }

                dlg.SiteDeterminingFilter = false;
                dlg.OnCancel();
            });

            // Scenario 3: unmodified peptide - not localizable, button hidden.
            SelectPrecursor(UNMODIFIED_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsFalse(tgNode.CanShowSiteDeterminingIons,
                    "Unmodified peptide should not be localizable");
            });
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                Assert.IsFalse(dlg.SiteDeterminingButtonVisible,
                    "Site-determining button should be hidden for a non-localizable peptide");
                dlg.OnCancel();
            });
        }

        /// <summary>
        /// Selects the first precursor (transition group) of the peptide with the given
        /// unmodified sequence, using an identity path so the tree need not be pre-expanded.
        /// </summary>
        private static void SelectPrecursor(string sequence)
        {
            var doc = SkylineWindow.Document;
            var tgPath = FindTransitionGroupPath(doc, sequence);
            Assert.IsNotNull(tgPath, "Could not find precursor for sequence {0}", sequence);
            RunUI(() => SkylineWindow.SequenceTree.SelectedPath = tgPath);
            WaitForConditionUI(() => SkylineWindow.SequenceTree.SelectedNode is TransitionGroupTreeNode);
        }

        private static IdentityPath FindTransitionGroupPath(SrmDocument doc, string sequence)
        {
            foreach (var group in doc.MoleculeGroups)
            {
                foreach (var pep in group.Molecules)
                {
                    if (!pep.Peptide.IsCustomMolecule && pep.Peptide.Sequence == sequence)
                    {
                        var tranGroup = pep.TransitionGroups.First();
                        return new IdentityPath(group.PeptideGroup, pep.Peptide, tranGroup.TransitionGroup);
                    }
                }
            }
            return null;
        }

        private static PeptideDocNode FindPeptide(SrmDocument doc, string sequence)
        {
            return doc.Molecules.FirstOrDefault(
                pep => !pep.Peptide.IsCustomMolecule && pep.Peptide.Sequence == sequence);
        }
    }
}
