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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Localization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// End-to-end test of uniquely localizing ion support in the Targets tree, the
    /// <see cref="PopupPickList"/> icons, the UniquelyLocalizing report column and the
    /// Transition Settings - Filter setting. Rather than checking against the analyzer (which
    /// would be circular), it uses three peptides with hand-computed expectations:
    ///  - a RESOLVABLE peptide (two well-separated candidates, one modified) where a specific
    ///    short prefix ion is uniquely localizing and a specific ion spanning both candidates is
    ///    not - the unique set must be a strict, non-empty subset containing the former and not
    ///    the latter;
    ///  - an UNRESOLVABLE peptide (modified serine inside a contiguous run of candidate serines)
    ///    where no ion is unique;
    ///  - a NON-LOCALIZABLE peptide (no candidate residue) where nothing is unique.
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
        // b7 spans [S0..T6] - both candidates -> not localizing (never badged or added).
        private const int SHARED_B_ORDINAL = 7;

        // Unresolvable: A0 A1 S2 S3 S4 A5 K6 - a run of three candidate serines; phospho on the middle one.
        private const string SERINE_RUN_SEQ = "AASSSAK";
        private const int SERINE_RUN_PHOSPHO_INDEX = 3;

        private const string UNMODIFIED_SEQ = "ELVIK"; // No candidate residue -> not localizable

        private static readonly StaticMod PHOSPHO = new StaticMod("Phospho", "S,T,Y", null, false, "PO3H",
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
            TestUniquelyLocalizingColumn();
            TestIncludeLocalizingIonsSetting();
            TestLibraryMatchTooltip();
            TestLibraryFilteredRanking();
        }

        /// <summary>
        /// The Library Match peak tooltip names the modification an ion uniquely localizes. Uses a
        /// synthetic spectrum with peaks at the b3 and b7 m/z of the resolvable precursor, ranked
        /// the way the Library Match window ranks a library spectrum, so no library file is needed:
        /// the b3 tooltip carries the row and the b7 tooltip does not.
        /// </summary>
        private void TestLibraryMatchTooltip()
        {
            RunUI(() =>
            {
                var doc = SkylineWindow.Document;
                var tgPath = FindTransitionGroupPath(doc, RESOLVABLE_SEQ);
                var nodePep = (PeptideDocNode) doc.FindNode(tgPath.Parent);
                var nodeGroup = (TransitionGroupDocNode) doc.FindNode(tgPath);
                var peaks = MakeSyntheticSpectrum(nodeGroup);
                var spectrumInfo = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(peaks,
                    IsotopeLabelType.light, nodeGroup, doc.Settings, nodePep.ExplicitMods, false, -1);
                var graphItem = new SpectrumGraphItem(nodePep, nodeGroup, null, spectrumInfo, string.Empty)
                {
                    SrmSettings = doc.Settings
                };
                var rowUnique = GraphsResources.GraphSpectrum_ToolTip_Uniquely_localizes + "\t" + PHOSPHO.Name;
                var tipUnique = GetPeakTooltipText(graphItem, spectrumInfo, IonType.b, UNIQUE_B_ORDINAL);
                Assert.IsTrue(tipUnique.Contains(rowUnique),
                    "b3 tooltip should name the localized modification, got:\n{0}", tipUnique);
                var tipShared = GetPeakTooltipText(graphItem, spectrumInfo, IonType.b, SHARED_B_ORDINAL);
                Assert.IsFalse(tipShared.Contains(GraphsResources.GraphSpectrum_ToolTip_Uniquely_localizes),
                    "b7 tooltip should not claim to localize, got:\n{0}", tipShared);
            });
        }

        /// <summary>
        /// With library pick set to "filter", the spectrum ranker applies the transition filter and
        /// the auto-pick takes only ranked ions, so a uniquely localizing ion outside the fragment
        /// range must rank when the setting is on, or it could never be selected in that mode.
        /// </summary>
        private void TestLibraryFilteredRanking()
        {
            var doc = SkylineWindow.Document;
            var tgPath = FindTransitionGroupPath(doc, RESOLVABLE_SEQ);
            var nodePep = (PeptideDocNode) doc.FindNode(tgPath.Parent);
            var nodeGroup = (TransitionGroupDocNode) doc.FindNode(tgPath);
            var peaks = MakeSyntheticSpectrum(nodeGroup);

            var tranSettings = doc.Settings.TransitionSettings;
            var settingsOn = doc.Settings.ChangeTransitionSettings(
                tranSettings.ChangeLibraries(tranSettings.Libraries.ChangePick(TransitionLibraryPick.filter)));
            Assert.IsTrue(settingsOn.TransitionSettings.Filter.IncludeLocalizingIons);
            var settingsOff = settingsOn.ChangeTransitionSettings(
                settingsOn.TransitionSettings.ChangeFilter(tranSettings.Filter.ChangeIncludeLocalizingIons(false)));

            Assert.IsTrue(IsRanked(peaks, nodeGroup, nodePep, settingsOn, IonType.b, UNIQUE_B_ORDINAL),
                "b3 should rank past the fragment range when localizing ions are included");
            Assert.IsFalse(IsRanked(peaks, nodeGroup, nodePep, settingsOff, IonType.b, UNIQUE_B_ORDINAL),
                "b3 lies outside the fragment range and should not rank without the setting");
            Assert.IsTrue(IsRanked(peaks, nodeGroup, nodePep, settingsOff, IonType.b, SHARED_B_ORDINAL),
                "b7 lies inside the fragment range and should rank regardless");
        }

        /// <summary>
        /// A two-peak spectrum at the b3 and b7 m/z of the precursor, standing in for a library
        /// spectrum so no library file is needed.
        /// </summary>
        private static SpectrumPeaksInfo MakeSyntheticSpectrum(TransitionGroupDocNode nodeGroup)
        {
            var mzUnique = nodeGroup.Transitions.First(t => IsIon(t, IonType.b, UNIQUE_B_ORDINAL)).Mz;
            var mzShared = nodeGroup.Transitions.First(t => IsIon(t, IonType.b, SHARED_B_ORDINAL)).Mz;
            return new SpectrumPeaksInfo(new[]
            {
                new SpectrumPeaksInfo.MI { Mz = mzUnique, Intensity = 100 },
                new SpectrumPeaksInfo.MI { Mz = mzShared, Intensity = 50 }
            });
        }

        /// <summary>
        /// True when ranking the spectrum with the transition filter applied gives the ion a rank.
        /// </summary>
        private static bool IsRanked(SpectrumPeaksInfo peaks, TransitionGroupDocNode nodeGroup, PeptideDocNode nodePep,
            SrmSettings settings, IonType type, int ordinal)
        {
            var spectrumInfo = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(peaks,
                IsotopeLabelType.light, nodeGroup, settings, nodePep.ExplicitMods, true, -1);
            return spectrumInfo.PeaksRanked.Any(rmi =>
                rmi.MatchedIons.Any(mfi => mfi.IonType == type && mfi.Ordinal == ordinal));
        }

        /// <summary>
        /// Renders the Library Match tooltip for the matched peak of the given ion, off screen, and
        /// returns its text with cells separated by tabs and rows by newlines.
        /// </summary>
        private static string GetPeakTooltipText(SpectrumGraphItem graphItem, LibraryRankedSpectrumInfo spectrumInfo,
            IonType type, int ordinal)
        {
            var rmi = spectrumInfo.PeaksMatched.FirstOrDefault(r =>
                r.MatchedIons.Any(mfi => mfi.IonType == type && mfi.Ordinal == ordinal));
            Assert.IsNotNull(rmi, "Spectrum did not match {0}{1}", type, ordinal);
            var tip = new GraphSpectrum.ToolTipImplementation(rmi, graphItem);
            using var bitmap = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bitmap);
            tip.RenderTip(g, new Size(1000, 1000), false);
            return tip.ToolTipText;
        }

        /// <summary>
        /// The Transition Settings - Filter checkbox selects uniquely localizing ions document-wide.
        /// With the ion range starting above the precursor m/z, b3 of the resolvable peptide is not
        /// selected by the range alone; turning the setting on adds it to the auto-managed precursor
        /// (every added ion below the precursor m/z is uniquely localizing), while b7, which spans
        /// both candidates, stays a plain range pick and the serine-run peptide, where no ion is
        /// unique, gains nothing. The Targets tree marks the unique ions with the badged icon.
        /// </summary>
        private void TestIncludeLocalizingIonsSetting()
        {
            var docBefore = SkylineWindow.Document;
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.SelectedTab = TransitionSettingsUI.TABS.Filter;
                dlg.RangeFrom = DocSettingsResources.TransitionFilter_FragmentStartFinders_m_z_precursor;
                Assert.IsFalse(dlg.IncludeLocalizingIons, "Localizing ions should be off by default");
                dlg.OkDialog();
            });
            var docRange = WaitForDocumentChange(docBefore);
            Assert.IsFalse(HasTransition(docRange, RESOLVABLE_SEQ, IonType.b, UNIQUE_B_ORDINAL),
                "b3 lies below the precursor m/z and should not be selected by the range alone");
            Assert.IsTrue(HasTransition(docRange, RESOLVABLE_SEQ, IonType.b, SHARED_B_ORDINAL),
                "b7 lies above the precursor m/z and should be selected by the range");
            int countRunBefore = CountTransitions(docRange, SERINE_RUN_SEQ);

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, dlg =>
            {
                dlg.SelectedTab = TransitionSettingsUI.TABS.Filter;
                dlg.IncludeLocalizingIons = true;
                dlg.OkDialog();
            });
            var docLocalizing = WaitForDocumentChange(docRange);
            Assert.IsTrue(docLocalizing.Settings.TransitionSettings.Filter.IncludeLocalizingIons);
            Assert.IsTrue(HasTransition(docLocalizing, RESOLVABLE_SEQ, IonType.b, UNIQUE_B_ORDINAL),
                "b3 uniquely localizes the phospho and should be selected once localizing ions are included");
            Assert.IsTrue(HasTransition(docLocalizing, RESOLVABLE_SEQ, IonType.b, SHARED_B_ORDINAL),
                "b7 should still be selected by the range");

            // Every selected product ion outside the range (below the precursor m/z) must be unique.
            var tgPath = FindTransitionGroupPath(docLocalizing, RESOLVABLE_SEQ);
            var tranGroupNode = (TransitionGroupDocNode) docLocalizing.FindNode(tgPath);
            var analyzer = new SiteDeterminingIonAnalyzer(FindPeptide(docLocalizing, RESOLVABLE_SEQ));
            int countAdded = 0;
            foreach (var nodeTran in tranGroupNode.Transitions.Where(t => !t.Transition.IsPrecursor()))
            {
                if (nodeTran.Mz >= tranGroupNode.PrecursorMz)
                    continue;
                Assert.IsTrue(analyzer.IsUniqueToPrecursor(nodeTran.Transition),
                    "{0} is outside the ion range and should only be selected because it uniquely localizes the modification", nodeTran.Transition);
                countAdded++;
            }
            Assert.IsTrue(countAdded > 0, "Expected uniquely localizing ions below the precursor m/z to be added");
            Assert.AreEqual(countRunBefore, CountTransitions(docLocalizing, SERINE_RUN_SEQ),
                "No ion uniquely localizes a serine inside a run, so nothing should be added");

            // The Targets tree marks unique ions with the badged fragment icon and plain range picks without it.
            SelectPrecursor(RESOLVABLE_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                tgNode.Expand();
                var tranNodes = tgNode.Nodes.OfType<TransitionTreeNode>().ToList();
                var nodeUnique = tranNodes.First(n => IsIon(n.DocNode, IonType.b, UNIQUE_B_ORDINAL));
                Assert.IsTrue(nodeUnique.IsUniquelyLocalizing);
                Assert.AreEqual((int) SequenceTree.ImageId.fragment_unique, nodeUnique.ImageIndex);
                var nodeShared = tranNodes.First(n => IsIon(n.DocNode, IonType.b, SHARED_B_ORDINAL));
                Assert.IsFalse(nodeShared.IsUniquelyLocalizing);
                Assert.AreEqual((int) SequenceTree.ImageId.fragment, nodeShared.ImageIndex);
            });

            // The setting round-trips through the document XML and validates against the schema.
            AssertEx.Serializable(docLocalizing);
        }

        /// <summary>
        /// Verifies the reporting <see cref="pwiz.Skyline.Model.Databinding.Entities.Transition"/>
        /// column <c>UniquelyLocalizing</c>: for every transition of the resolvable precursor it
        /// must match <see cref="SiteDeterminingIonAnalyzer.IsUniqueToPrecursor(Transition)"/>, and in
        /// particular it is TRUE for the hand-picked uniquely-localizing b3 ion and FALSE for the
        /// shared b7 ion. Assertions are on booleans only.
        /// </summary>
        private void TestUniquelyLocalizingColumn()
        {
            var doc = SkylineWindow.Document;
            var dataSchema = SkylineDataSchema.MemoryDataSchema(doc, DataSchemaLocalizer.INVARIANT);
            var tgPath = FindTransitionGroupPath(doc, RESOLVABLE_SEQ);
            Assert.IsNotNull(tgPath, "Could not find precursor for sequence {0}", RESOLVABLE_SEQ);
            var tranGroupNode = (TransitionGroupDocNode) doc.FindNode(tgPath);
            var pepNode = FindPeptide(doc, RESOLVABLE_SEQ);
            var analyzer = new SiteDeterminingIonAnalyzer(pepNode);

            bool sawUnique = false, sawShared = false;
            foreach (var tranNode in tranGroupNode.Transitions)
            {
                var tran = tranNode.Transition;
                var tranPath = new IdentityPath(tgPath, tran);
                var entity = new pwiz.Skyline.Model.Databinding.Entities.Transition(dataSchema, tranPath);
                bool expected = analyzer.IsUniqueToPrecursor(tran);
                Assert.AreEqual(expected, entity.UniquelyLocalizing,
                    "UniquelyLocalizing column should match the analyzer for {0}", tran);

                if (tran.IonType == IonType.b && tran.Ordinal == UNIQUE_B_ORDINAL)
                {
                    Assert.IsTrue(entity.UniquelyLocalizing,
                        "b3 spans only the modified serine and should be uniquely localizing");
                    sawUnique = true;
                }
                if (tran.IonType == IonType.b && tran.Ordinal == SHARED_B_ORDINAL)
                {
                    Assert.IsFalse(entity.UniquelyLocalizing,
                        "b7 spans both candidates and should not be uniquely localizing");
                    sawShared = true;
                }
            }
            Assert.IsTrue(sawUnique, "Document did not contain the b3 ion");
            Assert.IsTrue(sawShared, "Document did not contain the b7 ion");
        }

        /// <summary>
        /// Resolvable peptide: the hand-picked uniquely-localizing b-ion (b3) is badged in the
        /// pick list and the hand-picked shared b-ion (b7) is not.
        /// </summary>
        private void TestResolvablePeptide()
        {
            SelectPrecursor(RESOLVABLE_SEQ);
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                var choices = tgNode.GetChoices(false).ToArray();
                Assert.IsTrue(choices.All(c => c.Id is Transition),
                    "All transition-group children should carry a Transition identity");
                var choiceUnique = FindIon(choices, IonType.b, UNIQUE_B_ORDINAL);
                var choiceShared = FindIon(choices, IonType.b, SHARED_B_ORDINAL);
                Assert.IsTrue(tgNode.IsUniquelyLocalizing(choiceUnique),
                    "b3 spans only the modified serine, so it should uniquely localize");
                Assert.IsFalse(tgNode.IsUniquelyLocalizing(choiceShared),
                    "b7 spans both candidates, so it should not uniquely localize");
                int countUnique = choices.Count(tgNode.IsUniquelyLocalizing);
                Assert.IsTrue(0 < countUnique && countUnique < choices.Length,
                    "Uniquely-localizing ions should be a strict, non-empty subset of all ions");

                // The pick list draws the same badged icon as the tree for a uniquely localizing ion
                var imageList = SkylineWindow.SequenceTree.ImageList;
                AssertSameImage(imageList.Images[(int) SequenceTree.ImageId.fragment_unique],
                    tgNode.GetPickTypeImage(choiceUnique));
                AssertSameImage(imageList.Images[(int) SequenceTree.ImageId.fragment],
                    tgNode.GetPickTypeImage(choiceShared));
            });
        }

        /// <summary>
        /// Unresolvable peptide: the modified serine sits inside a contiguous run of candidate
        /// serines, so no ion uniquely localizes it and nothing is badged, even though the
        /// peptide is localizable.
        /// </summary>
        private void TestUnresolvablePeptide()
        {
            SelectPrecursor(SERINE_RUN_SEQ);
            AssertNoUniquelyLocalizingIons("No ion uniquely localizes a serine inside a run");
        }

        /// <summary>
        /// Non-localizable peptide: no candidate residue, so nothing is badged.
        /// </summary>
        private void TestNonLocalizablePeptide()
        {
            SelectPrecursor(UNMODIFIED_SEQ);
            AssertNoUniquelyLocalizingIons("Unmodified peptide should not be localizable");
        }

        private void AssertNoUniquelyLocalizingIons(string message)
        {
            RunUI(() =>
            {
                var tgNode = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                var choices = tgNode.GetChoices(false).ToArray();
                Assert.IsTrue(choices.Length > 0, "Expected at least one ion choice");
                Assert.IsFalse(choices.Any(tgNode.IsUniquelyLocalizing), message);
                var imagePlain = SkylineWindow.SequenceTree.ImageList.Images[(int) SequenceTree.ImageId.fragment];
                foreach (var choice in choices)
                    AssertSameImage(imagePlain, tgNode.GetPickTypeImage(choice));
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

        private static bool IsIon(TransitionDocNode nodeTran, IonType type, int ordinal)
        {
            return nodeTran.Transition.IonType == type && nodeTran.Transition.Ordinal == ordinal;
        }

        /// <summary>
        /// True when the document's precursor for the sequence holds a transition of the given ion
        /// type and ordinal.
        /// </summary>
        private static bool HasTransition(SrmDocument doc, string sequence, IonType type, int ordinal)
        {
            var tgPath = FindTransitionGroupPath(doc, sequence);
            Assert.IsNotNull(tgPath, "Could not find precursor for sequence {0}", sequence);
            var tranGroupNode = (TransitionGroupDocNode) doc.FindNode(tgPath);
            return tranGroupNode.Transitions.Any(t => IsIon(t, type, ordinal));
        }

        private static int CountTransitions(SrmDocument doc, string sequence)
        {
            var tgPath = FindTransitionGroupPath(doc, sequence);
            Assert.IsNotNull(tgPath, "Could not find precursor for sequence {0}", sequence);
            return ((TransitionGroupDocNode) doc.FindNode(tgPath)).TransitionCount;
        }

        private static DocNode FindIon(IEnumerable<DocNode> choices, IonType type, int ordinal)
        {
            var choice = choices.FirstOrDefault(c => c.Id is Transition t && t.IonType == type && t.Ordinal == ordinal);
            Assert.IsNotNull(choice, "Choices did not contain {0}{1}", type, ordinal);
            return choice;
        }

        /// <summary>
        /// Pixel-level comparison, because <see cref="ImageList.ImageCollection"/> hands out a
        /// fresh copy on every access, so reference equality cannot be used.
        /// </summary>
        private static void AssertSameImage(Image expected, Image actual)
        {
            Assert.AreEqual(expected.Size, actual.Size);
            using var bmpExpected = new Bitmap(expected);
            using var bmpActual = new Bitmap(actual);
            for (int x = 0; x < bmpExpected.Width; x++)
            {
                for (int y = 0; y < bmpExpected.Height; y++)
                    Assert.AreEqual(bmpExpected.GetPixel(x, y), bmpActual.GetPixel(x, y), "Pixel ({0}, {1}) differs", x, y);
            }
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
