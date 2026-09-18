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
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// End-to-end test of the "site-determining ions" filter button on the
    /// <see cref="PopupPickList"/>. Rather than checking the filter against the analyzer (which
    /// would be circular), it uses three peptides with hand-computed expectations:
    ///  - a RESOLVABLE peptide (two well-separated candidates, one modified) where a specific
    ///    short prefix ion is uniquely localizing and a specific ion spanning both candidates is
    ///    not - the filtered set must be a strict, non-empty subset containing the former and not
    ///    the latter, with no empty-state hint;
    ///  - an UNRESOLVABLE peptide (modified serine inside a contiguous run of candidate serines)
    ///    where no ion is unique, so the filtered set is empty and the empty-state hint shows;
    ///  - a NON-LOCALIZABLE peptide (no candidate residue) where the toggle button is hidden.
    ///
    /// Assertions are made on transition identities (ion type + ordinal) and booleans/counts,
    /// never on localized labels, so the test is translation-proof.
    /// </summary>
    [TestClass]
    public class SiteDeterminingIonFunctionalTest : AbstractFunctionalTest
    {
        // Resolvable: S0 A1 A2 A3 A4 A5 T6 A7 A8 K9 - candidates serine 0 and threonine 6; phospho on S0.
        private const string RESOLVABLE_SEQ = "SAAAAATAAK";
        private const int RESOLVABLE_PHOSPHO_INDEX = 0;
        // b3 spans [S0 A1 A2] - only the modified serine -> uniquely localizing (must be present).
        private const int UNIQUE_B_ORDINAL = 3;
        // b7 spans [S0..T6] - both candidates -> not localizing (must be absent when filtered).
        private const int SHARED_B_ORDINAL = 7;

        // Unresolvable: A0 A1 S2 S3 S4 A5 K6 - a run of three candidate serines; phospho on the middle one.
        private const string SERINE_RUN_SEQ = "AASSSAK";
        private const int SERINE_RUN_PHOSPHO_INDEX = 3;

        private const string UNMODIFIED_SEQ = "ELVIK"; // No candidate residue -> not localizable

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
            // mod, so it is not implicitly applied to every candidate residue).
            RunUI(() => Settings.Default.StaticModList.Add(PHOSPHO));

            // Offer the full b/y ion series (ordinal 1 .. last ion) so the hand-picked ions
            // (b3, b7) are always present in the unfiltered choice set.
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.SelectedTab = TransitionSettingsUI.TABS.Filter;
                dlg.PrecursorCharges = "2";
                dlg.ProductCharges = "1";
                dlg.FragmentTypes = "b, y";
                dlg.RangeFrom = Resources.TransitionFilter_FragmentStartFinders_ion_1;
                dlg.RangeTo = Resources.TransitionFilter_FragmentEndFinders_last_ion;
                dlg.SetAutoSelect = true;
                dlg.OkDialog();
            });

            // Add all three peptides, phosphorylating the two localizable ones on a specific residue.
            var docStart = SkylineWindow.Document;
            var docResolvable = AddPhosphoPeptide(docStart, RESOLVABLE_SEQ, RESOLVABLE_PHOSPHO_INDEX);
            var docRun = AddPhosphoPeptide(docResolvable, SERINE_RUN_SEQ, SERINE_RUN_PHOSPHO_INDEX);
            RunUI(() => SkylineWindow.Paste(UNMODIFIED_SEQ));
            WaitForDocumentChange(docRun);

            TestResolvablePeptide();
            TestUnresolvablePeptide();
            TestNonLocalizablePeptide();
        }

        /// <summary>
        /// Resolvable peptide: the toggle is offered, and turning it on leaves a strict, non-empty
        /// subset that includes the hand-picked uniquely-localizing b-ion (b3) and excludes the
        /// hand-picked shared b-ion (b7). No empty-state hint.
        /// </summary>
        private void TestResolvablePeptide()
        {
            SelectPrecursor(RESOLVABLE_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsTrue(tgNode.CanShowSiteDeterminingIons,
                    "Picker should offer site-determining ions for the resolvable precursor");
            });

            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                Assert.IsTrue(dlg.SiteDeterminingButtonVisible,
                    "Site-determining button should be visible for a localizable precursor");

                var fullChoices = dlg.VisibleChoices.ToArray();
                Assert.IsTrue(fullChoices.All(c => c.Id is Transition),
                    "All transition-group children should carry a Transition identity");
                var fullIndexes = new HashSet<int>(fullChoices.Select(c => c.Id.GlobalIndex));
                // Both hand-picked ions must exist in the full (unfiltered) set for the test to mean anything.
                Assert.IsTrue(HasIon(fullChoices, IonType.b, UNIQUE_B_ORDINAL), "Full set should contain b3");
                Assert.IsTrue(HasIon(fullChoices, IonType.b, SHARED_B_ORDINAL), "Full set should contain b7");

                dlg.SiteDeterminingFilter = true;
                var filteredChoices = dlg.VisibleChoices.ToArray();
                var filteredIndexes = new HashSet<int>(filteredChoices.Select(c => c.Id.GlobalIndex));

                Assert.IsTrue(filteredIndexes.Count > 0, "Expected at least one uniquely-localizing transition");
                Assert.IsTrue(filteredIndexes.Count < fullIndexes.Count,
                    "Uniquely-localizing set should be a strict subset of all transitions");
                Assert.IsTrue(filteredIndexes.IsSubsetOf(fullIndexes),
                    "Uniquely-localizing set should be a subset of all transitions");

                // Hand-picked expectations: b3 (spans only the modified S0) present; b7 (spans both
                // S0 and T6) absent.
                Assert.IsTrue(HasIon(filteredChoices, IonType.b, UNIQUE_B_ORDINAL),
                    "Filtered set should contain the uniquely-localizing b3 ion");
                Assert.IsFalse(HasIon(filteredChoices, IonType.b, SHARED_B_ORDINAL),
                    "Filtered set should not contain the shared b7 ion (spans both candidates)");

                Assert.IsFalse(dlg.SiteDeterminingEmptyHintVisible,
                    "Empty-state hint should be hidden when the filtered set is non-empty");

                dlg.SiteDeterminingFilter = false;
                dlg.OnCancel();
            });
        }

        /// <summary>
        /// Unresolvable peptide: the modified serine sits inside a contiguous run of candidate
        /// serines, so no ion uniquely localizes it. Turning the filter on empties the list and
        /// shows the empty-state hint, even though the peptide is localizable.
        /// </summary>
        private void TestUnresolvablePeptide()
        {
            SelectPrecursor(SERINE_RUN_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsTrue(tgNode.CanShowSiteDeterminingIons,
                    "Serine-run peptide is still localizable, so the toggle should be offered");
            });

            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.ApplyFilter(false);
                Assert.IsTrue(dlg.SiteDeterminingButtonVisible);

                dlg.SiteDeterminingFilter = true;
                var filteredChoices = dlg.VisibleChoices.ToArray();
                Assert.AreEqual(0, filteredChoices.Length,
                    "No ion uniquely localizes a serine inside a run - filtered set must be empty");
                Assert.IsTrue(dlg.SiteDeterminingEmptyHintVisible,
                    "Empty-state hint should show when the filter leaves no visible ions");

                dlg.SiteDeterminingFilter = false;
                dlg.OnCancel();
            });
        }

        /// <summary>
        /// Non-localizable peptide: no candidate residue, so the toggle button is hidden.
        /// </summary>
        private void TestNonLocalizablePeptide()
        {
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
        /// Pastes the given peptide and phosphorylates the residue at <paramref name="indexAA"/>,
        /// returning the document after the modification is applied.
        /// </summary>
        private SrmDocument AddPhosphoPeptide(SrmDocument docBefore, string sequence, int indexAA)
        {
            RunUI(() => SkylineWindow.Paste(sequence));
            var docPasted = WaitForDocumentChange(docBefore);

            SelectPrecursor(sequence);
            RunDlg<EditPepModsDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.SelectModification(IsotopeLabelType.light, indexAA, PHOSPHO.Name);
                dlg.OkDialog();
            });
            var docMod = WaitForDocumentChange(docPasted);

            var pepNode = FindPeptide(docMod, sequence);
            Assert.IsNotNull(pepNode.ExplicitMods, "Expected an explicit modification on {0}", sequence);
            return docMod;
        }

        /// <summary>
        /// True when the given choices include a transition of the given ion type and ordinal.
        /// </summary>
        private static bool HasIon(IEnumerable<DocNode> choices, IonType type, int ordinal)
        {
            return choices.Any(c => c.Id is Transition t && t.IonType == type && t.Ordinal == ordinal);
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
