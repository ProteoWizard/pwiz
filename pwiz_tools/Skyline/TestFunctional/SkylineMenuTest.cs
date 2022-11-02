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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkylineMenuTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSkylineMenu()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));
            AssertMenuItemAvailableIs("reintegrateToolStripMenuItem", true);
            AssertMenuItemAvailableIs("generateDecoysMenuItem", false);
            AssertMenuItemAvailableIs("acceptProteinsMenuItem", false);
            AssertMenuItemAvailableIs("associateFASTAMenuItem", false);
            AssertMenuItemAvailableIs("renameProteinsMenuItem", false);
            AssertMenuItemAvailableIs("sortProteinsByAccessionToolStripMenuItem", false);
            AssertMenuItemAvailableIs("sortProteinsByPreferredNameToolStripMenuItem", false);
            AssertMenuItemAvailableIs("sortProteinsByGeneToolStripMenuItem", false);
            AssertMenuItemAvailableIs("acceptPeptidesMenuItem", false);
            AssertMenuItemAvailableIs("removeEmptyPeptidesMenuItem", true);
            AssertMenuItemAvailableIs("viewProteinsMenuItem", false);
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.proteomic));
            AssertMenuItemAvailableIs("reintegrateToolStripMenuItem", true);
            AssertMenuItemAvailableIs("generateDecoysMenuItem", true);
            AssertMenuItemAvailableIs("acceptProteinsMenuItem", true);
            AssertMenuItemAvailableIs("associateFASTAMenuItem", true);
            AssertMenuItemAvailableIs("renameProteinsMenuItem", true);
            AssertMenuItemAvailableIs("sortProteinsByAccessionToolStripMenuItem", true);
            AssertMenuItemAvailableIs("sortProteinsByPreferredNameToolStripMenuItem", true);
            AssertMenuItemAvailableIs("sortProteinsByGeneToolStripMenuItem", true);
            AssertMenuItemAvailableIs("acceptPeptidesMenuItem", true);
            AssertMenuItemAvailableIs("removeEmptyPeptidesMenuItem", true);
            AssertMenuItemAvailableIs("viewProteinsMenuItem", true);
        }

        protected void AssertMenuItemAvailableIs(string menuItemName, bool expectedAvailable)
        {
            var menuItem = FindItemByName(SkylineWindow.MainMenuStrip.Items, menuItemName);
            Assert.IsNotNull(menuItem);
            Assert.AreEqual(expectedAvailable, menuItem.Available);
        }

        private ToolStripItem FindItemByName(ToolStripItemCollection items, string name)
        {
            foreach (var item in items.Cast<ToolStripItem>())
            {
                if (item.Name == name)
                {
                    return item;
                }

                if (item is ToolStripDropDownItem toolStripDropDownItem)
                {
                    var childResult = FindItemByName(toolStripDropDownItem.DropDownItems, name);
                    if (childResult != null)
                    {
                        return childResult;
                    }
                }
            }

            return null;
        }
    }
}
