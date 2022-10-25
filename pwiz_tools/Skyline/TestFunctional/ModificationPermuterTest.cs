/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ModificationPermuterTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestModificationPermuter()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText(
                    TextUtil.LineSeparate("SKYLINE", "LAUREL", "YANNY", "BALLISTICALLY"));
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            Assert.AreEqual(4, SkylineWindow.Document.PeptideCount);

            // Turn off "Auto manage children" on the second peptide and make sure the appropriate precursors still get added.
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 1);
            });
            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
            {
                dlg.AutoManageChildren = false;
                dlg.OnOk();
            });
            CollectionAssert.AreEqual(new[] {true, false, true, true},
                SkylineWindow.Document.Peptides.Select(p => p.AutoManageChildren).ToList());

            var permuteIsotopeModificationsDlg =
                ShowDialog<PermuteIsotopeModificationsDlg>(SkylineWindow.ShowPermuteIsotopeModificationsDlg);
            RunDlg<EditStaticModDlg>(permuteIsotopeModificationsDlg.AddIsotopeModification, editStaticModDlg =>
            {
                if (!Settings.Default.StaticModsShowMore)
                {
                    editStaticModDlg.ToggleLessMore();
                }
                editStaticModDlg.SetModification("Label:2H(3) (L)");
                editStaticModDlg.OkDialog();
            });
            OkDialog(permuteIsotopeModificationsDlg, permuteIsotopeModificationsDlg.OkDialog);
            var expectedHeavyLabelTypeNames = new List<string>();
            expectedHeavyLabelTypeNames.Add(IsotopeLabelType.HEAVY_NAME);
            expectedHeavyLabelTypeNames.AddRange(Enumerable.Range(1, 3).Select(i => IsotopeLabelType.HEAVY_NAME + i));
            var peptideModifications = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            CollectionAssert.AreEquivalent(expectedHeavyLabelTypeNames,
                peptideModifications.GetHeavyModifications().Select(typedMods => typedMods.LabelType.Name).ToList());
            foreach (var peptide in SkylineWindow.Document.Peptides)
            {
                int leucineCount = peptide.Peptide.Sequence.Count(c => c == 'L');
                Assert.AreEqual(leucineCount + 1, peptide.TransitionGroupCount);

                var fullyLabeledPrecursor =
                    peptide.TransitionGroups.FirstOrDefault(tg => tg.LabelType.Name == IsotopeLabelType.HEAVY_NAME);
                if (leucineCount == 0)
                {
                    Assert.IsNull(fullyLabeledPrecursor);
                    continue;
                }
                Assert.IsNotNull(fullyLabeledPrecursor);
                var fullyHeavyMods = peptide.ExplicitMods.GetHeavyModifications()
                    .FirstOrDefault(typedMods => typedMods.LabelType.Name == IsotopeLabelType.HEAVY_NAME);
                Assert.IsNotNull(fullyHeavyMods);
                Assert.AreEqual(leucineCount, fullyHeavyMods.Modifications.Count);
                for (int i = 1; i < leucineCount; i++)
                {
                    var labelTypeName = IsotopeLabelType.HEAVY_NAME + i;
                    var partialHeavyMods = peptide.ExplicitMods.GetHeavyModifications()
                        .FirstOrDefault(typedMods => typedMods.LabelType.Name == labelTypeName);
                    Assert.IsNotNull(partialHeavyMods);
                    Assert.AreEqual(i, partialHeavyMods.Modifications.Count);
                }
            }
            permuteIsotopeModificationsDlg =
                ShowDialog<PermuteIsotopeModificationsDlg>(SkylineWindow.ShowPermuteIsotopeModificationsDlg);
            RunUI(()=>permuteIsotopeModificationsDlg.SimplePermutation = false);
            OkDialog(permuteIsotopeModificationsDlg, permuteIsotopeModificationsDlg.OkDialog);
            peptideModifications = SkylineWindow.Document.Settings.PeptideSettings.Modifications;
            expectedHeavyLabelTypeNames.Clear();
            expectedHeavyLabelTypeNames.Add(IsotopeLabelType.HEAVY_NAME);
            expectedHeavyLabelTypeNames.AddRange(Enumerable.Range(1, (int) Math.Pow(2, 4)- 2).Select(i => IsotopeLabelType.HEAVY_NAME + i));
            CollectionAssert.AreEquivalent(expectedHeavyLabelTypeNames,
                peptideModifications.GetHeavyModifications().Select(typedMods => typedMods.LabelType.Name).ToList());
            foreach (var peptide in SkylineWindow.Document.Peptides)
            {
                int leucineCount = peptide.Peptide.Sequence.Count(c => c == 'L');
                AssertEx.AreEqual(Math.Pow(2, leucineCount), peptide.TransitionGroupCount);

                var fullyLabeledPrecursor =
                    peptide.TransitionGroups.FirstOrDefault(tg => tg.LabelType.Name == IsotopeLabelType.HEAVY_NAME);
                if (leucineCount == 0)
                {
                    Assert.IsNull(fullyLabeledPrecursor);
                    continue;
                }
                Assert.IsNotNull(fullyLabeledPrecursor);
                var fullyHeavyMods = peptide.ExplicitMods.GetHeavyModifications()
                    .FirstOrDefault(typedMods => typedMods.LabelType.Name == IsotopeLabelType.HEAVY_NAME);
                Assert.IsNotNull(fullyHeavyMods);
                Assert.AreEqual(leucineCount, fullyHeavyMods.Modifications.Count);
                Assert.AreEqual(Math.Pow(2, leucineCount) - 1, peptide.ExplicitMods.GetHeavyModifications().Count());
                var modificationIndexSets = new HashSet<ImmutableList<int>>();
                var fullyLabeledIndexes =
                    ImmutableList.ValueOf(fullyHeavyMods.Modifications.Select(mod => mod.IndexAA));
                Assert.AreEqual(leucineCount, fullyLabeledIndexes.Count);
                Assert.AreEqual(fullyLabeledIndexes.Count, fullyLabeledIndexes.Distinct().Count());
                modificationIndexSets.Add(fullyLabeledIndexes);
                foreach (var partialModifications in peptide.ExplicitMods.GetHeavyModifications())
                {
                    if (partialModifications.LabelType.Name == IsotopeLabelType.HEAVY_NAME)
                    {
                        continue;
                    }

                    var modificationIndexes =
                        ImmutableList.ValueOf(partialModifications.Modifications.Select(mod => mod.IndexAA));
                    CollectionAssert.IsSubsetOf(modificationIndexes.ToList(), fullyLabeledIndexes.ToList());
                    Assert.IsTrue(modificationIndexSets.Add(modificationIndexes));
                }
                Assert.AreEqual(Math.Pow(2, leucineCount) - 1, modificationIndexSets.Count);
            }

            CollectionAssert.AreEqual(new[] {true, false, true, true},
                SkylineWindow.Document.Peptides.Select(p => p.AutoManageChildren).ToList());
            var filePath = TestContext.GetTestResultsPath("ModificationPermuterTest.sky");
            RunUI(()=>
            {
                SkylineWindow.SaveDocument(filePath);
                SkylineWindow.OpenFile(filePath);
            });
        }
    }
}
