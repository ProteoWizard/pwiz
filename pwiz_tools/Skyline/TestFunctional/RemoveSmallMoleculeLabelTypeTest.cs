/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that when you remove a label type from a small molecule document, the
    /// TransitionGroups whose label type no longer exists get removed from the document as well.
    /// </summary>
    [TestClass]
    public class RemoveSmallMoleculeLabelTypeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRemoveSmallMoleculeLabelType()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            const string transitionList = @"Precursor m/z	Precursor Charge	Product m/z	Product Charge	Label Type
800	1	420	1	light
803	1	420	1	heavy
900	1	500	1	light
901	1	501	1	heavy
";
            RunUI(()=>
            {
                SkylineWindow.Paste(transitionList);
            });
            // Make sure that some of the molecules have auto manage children set to "true" and some "false"
            // so that both code paths in PeptideDocNode.ChangeSettings get exercised
            for (int iMolecule = 0; iMolecule < SkylineWindow.Document.MoleculeCount; iMolecule++)
            {
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath =
                        SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, iMolecule);
                });
                bool autoManageChildren = 0 == (iMolecule % 2);
                RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, dlg =>
                {
                    dlg.AutoManageChildren = autoManageChildren;
                    dlg.OnOk();
                });
            }
            // Make sure that both "true" and "false" are represented in the AutoManageChildren values
            Assert.AreEqual(2, SkylineWindow.Document.Molecules.Select(mol=>mol.AutoManageChildren).Distinct().Count());

            // Make sure that we currently have both "heavy" and "light" label types
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeTransitionGroups.Select(tg=>tg.LabelType).Distinct().Count());

            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });

            // Replace the "heavy" label type with "heavy2" and "heavy3"
            RunDlg<EditLabelTypeListDlg>(peptideSettingsUi.EditSmallMoleculeInternalStandards, dlg =>
            {
                dlg.LabelTypeText = TextUtil.LineSeparate("heavy2", "heavy3");
                dlg.OkDialog();
            });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            
            // Make sure that the "heavy" precursors were removed from the document
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionGroups.Select(tg => tg.LabelType).Distinct().Count());

            AssertEx.RoundTrip(SkylineWindow.Document);
        }
    }
}
