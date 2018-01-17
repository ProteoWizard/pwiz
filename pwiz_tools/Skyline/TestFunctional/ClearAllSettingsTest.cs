/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ClearAllSettingsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestClearAllSettings()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Settings.Default.MruList.Add(@"C:\test1");
            Settings.Default.MruList.Add(@"C:\path\to\filename.txt");
            Settings.Default.MruList.Add(@"D:\path\to\another\file.sky");
            Assert.AreEqual(3, Settings.Default.MruList.Count);
            Settings.Default.AnnotationColor = 5;
            Settings.Default.StaticModList.Add(UniMod.GetModification("Oxidation (M)", true));
            Assert.AreEqual(2, Settings.Default.StaticModList.Count);
            Settings.Default.HeavyModList.Add(UniMod.GetModification("Label:13C(6) (C-term K)", false));
            Assert.AreEqual(1, Settings.Default.HeavyModList.Count);

            var toolsOptions = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);
            RunUI(() => toolsOptions.SelectedTab = ToolOptionsUI.TABS.Miscellaneous);

            RunDlg<MultiButtonMsgDlg>(toolsOptions.ResetAllSettings, messageDlg => messageDlg.BtnCancelClick());

            Assert.AreEqual(5, Settings.Default.AnnotationColor);
            Assert.AreEqual(3, Settings.Default.MruList.Count);
            Assert.AreEqual(2, Settings.Default.StaticModList.Count);
            Assert.AreEqual(1, Settings.Default.HeavyModList.Count);

            RunDlg<MultiButtonMsgDlg>(toolsOptions.ResetAllSettings, messageDlg => messageDlg.Btn1Click());

            Assert.AreEqual(0, Settings.Default.AnnotationColor);
            Assert.AreEqual(0, Settings.Default.MruList.Count);
            Assert.AreEqual(1, Settings.Default.StaticModList.Count);
            Assert.AreEqual(0, Settings.Default.HeavyModList.Count);

            OkDialog(toolsOptions, toolsOptions.OkDialog);
        }
    }
}