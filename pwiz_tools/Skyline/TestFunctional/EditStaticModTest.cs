/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests EditStaticModDlg.
    /// </summary>
    [TestClass]
    public class EditStaticModTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEditStaticMod()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Bring up add modification dialog
            Settings.Default.StaticModsShowMore = false;
            var peptideSettingsUI = ShowPeptideSettings();
            var editModsDlg = ShowEditStaticModsDlg(peptideSettingsUI);
            // If editing an existing modification the name is shown in a textbox, not a dropdown.
            RunDlg<EditStaticModDlg>(editModsDlg.EditItem, editModDlg =>
                {
                    Assert.IsTrue(editModDlg.TextNameVisible);
                    Assert.IsFalse(editModDlg.ComboNameVisible);
                    editModDlg.DialogResult = DialogResult.Cancel;
                });
            // Check that the dropdown shows the correct number of items depending on less/more being shown.
            StaticMod mod = null;
            RunDlg<EditStaticModDlg>(editModsDlg.AddItem, editModDlg =>
            {
                Assert.AreEqual(UniMod.DictStructuralModNames.Count, editModDlg.ListAvailableMods().Count);
                editModDlg.ToggleLessMore();
                Assert.AreEqual(UniMod.DictStructuralModNames.Count + UniMod.DictHiddenStructuralModNames.Count,
                    editModDlg.ListAvailableMods().Count);
                editModDlg.ToggleLessMore();
                editModDlg.SetModification("Phospho (ST)");
                Assert.IsTrue(editModDlg.ShowLoss);
                Assert.AreEqual("HO3P", editModDlg.Formula);
                mod = editModDlg.Modification;
                editModDlg.OkDialog();
            });
            RunUI(editModsDlg.OkDialog);
            WaitForClosedForm(editModsDlg);
            // Check that the modification added to the document matches the modification in UniMod.
            RunUI(() => 
            { 
                Assert.IsTrue(Settings.Default.StaticModList.Contains(mod));
                StaticMod uniMod;
                Assert.IsTrue(UniMod.DictStructuralModNames.TryGetValue(mod.Name, out uniMod));
                Assert.AreEqual(mod, uniMod.ChangeVariable(true));
            });
            // Repeat for heavy modifications.
            var editModsDlg2 = ShowEditHeavyModsDlg(peptideSettingsUI);
            RunDlg<EditStaticModDlg>(editModsDlg2.AddItem, editModDlg =>
            {
                editModDlg.ToggleLessMore();
                Assert.AreEqual(UniMod.DictIsotopeModNames.Count + UniMod.DictHiddenIsotopeModNames.Count,
                    editModDlg.ListAvailableMods().Count);
                editModDlg.ToggleLessMore();
                Assert.AreEqual(UniMod.DictIsotopeModNames.Count, editModDlg.ListAvailableMods().Count);
                editModDlg.SetModification("Label:18O(1) (C-term)");
                Assert.IsFalse(editModDlg.ShowLoss);
                mod = editModDlg.Modification;
                editModDlg.OkDialog();
            });
            RunUI(editModsDlg2.OkDialog);
            WaitForClosedForm(editModsDlg2);
            RunUI(() =>
            {
                Assert.IsTrue(Settings.Default.HeavyModList.Contains(mod));
                StaticMod uniMod;
                Assert.IsTrue(UniMod.DictIsotopeModNames.TryGetValue(mod.Name, out uniMod));
                Assert.AreEqual(mod, uniMod);
                peptideSettingsUI.OkDialog();
            });
            WaitForClosedForm(peptideSettingsUI);
        }
    }
}
