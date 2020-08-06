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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests showing the PeptideSettings dialog, and then uses reflection to verify
    /// that the private "TopLevelControl" property in <see cref="ToolTip"/> gets set to
    /// the correct value.
    /// </summary>
    [TestClass]
    public class PeptideSettingsTooltipTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeptideSettingsTooltip()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            var fieldComponents = typeof(PeptideSettingsUI).GetField("components", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fieldComponents);
            var propTopLevelControl = typeof(ToolTip).GetProperty("TopLevelControl", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(propTopLevelControl);
            int success = 0;
            int failures = 0;
            for (int i = 0; i < 5; i++)
            {
                var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(()=>peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Quantification);
                var components = fieldComponents.GetValue(peptideSettingsUi) as IContainer;
                Assert.IsNotNull(components);
                var tooltip = components.Components.OfType<ToolTip>().FirstOrDefault();
                Assert.IsNotNull(tooltip);
                var topLevelControl = propTopLevelControl.GetValue(tooltip);
                Assert.IsNotNull(topLevelControl);
                if (topLevelControl is Form)
                {
                    success++;
                }
                else
                {
                    failures++;
                }
                OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            }
            Assert.AreEqual(0, failures);
        }
    }
}
