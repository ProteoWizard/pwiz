/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ChargedLossesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChargedLosses()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(peptideSettingsUi.EditStaticMods);
            var editModDlg = ShowDialog<EditStaticModDlg>(editListDlg.AddItem);
            RunUI(()=>editModDlg.Modification = new StaticMod("FA2", "N", null, "C56H92N4O39"));
            var editLossDlg = ShowDialog<EditFragmentLossDlg>(editModDlg.AddLoss);
            RunUI(()=>
            {
                editLossDlg.Loss = new FragmentLoss("C42H69N3O30").ChangeCharge(2).ChangeInclusion(LossInclusion.Always);
            });
            OkDialog(editLossDlg, editLossDlg.OkDialog);
            editLossDlg = ShowDialog<EditFragmentLossDlg>(editModDlg.AddLoss);
            RunUI(()=>editLossDlg.Loss = new FragmentLoss("C48H79N3O34").ChangeCharge(2).ChangeInclusion(LossInclusion.Always));
            OkDialog(editLossDlg, editLossDlg.OkDialog);
            OkDialog(editModDlg, editModDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(()=>
            {
                transitionSettings.FragmentTypes = "p";
                transitionSettings.MaxMz = 2000;
            });
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            SetClipboardText("EEQYN[+1444.5]STYR+++");
            RunUI(SkylineWindow.Paste);
            VerifyTransitionMzs(SkylineWindow.Document);
            var saveFileName = TestContext.GetTestPath("ChargedLossesTest.sky");
            RunUI(()=>SkylineWindow.SaveDocument(saveFileName));
            RunUI(()=>SkylineWindow.OpenFile(saveFileName));
            VerifyTransitionMzs(SkylineWindow.Document);
        }

        private void VerifyTransitionMzs(SrmDocument document)
        {
            var transitionGroup = SkylineWindow.Document.Peptides.First().TransitionGroups.First();
            Assert.AreEqual(3, transitionGroup.PrecursorCharge);
            Assert.AreEqual(878.6867, transitionGroup.PrecursorMz, .01);
            var firstLossTransition = transitionGroup.Transitions.FirstOrDefault(tran =>
                tran.Losses != null && tran.Losses.Losses.First().LossIndex == 0);
            Assert.IsNotNull(firstLossTransition);
            Assert.AreEqual(1, firstLossTransition.Transition.Charge);
            Assert.AreEqual(1538.6439, firstLossTransition.Mz, .01);
            var secondLossTransition = transitionGroup.Transitions.FirstOrDefault(tran =>
                tran.Losses != null && tran.Losses.Losses.First().LossIndex == 1);
            Assert.IsNotNull(secondLossTransition);
            Assert.AreEqual(1, secondLossTransition.Transition.Charge);
            Assert.AreEqual(1392.5926, secondLossTransition.Mz, .01);
        }
    }
}
