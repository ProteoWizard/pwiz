/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests using the "Import Transition List" button on the Start Page
    /// </summary>
    [TestClass]
    public class StartPageImportTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageImportTransitionList()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage => true;

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            var startPageSettings = ShowDialog<StartPageSettingsUI>(() =>
                startPage.ClickWizardAction(Resources.SkylineStartup_SkylineStartup_Import_Transition_List));
            var insertTransitionListDlg = ShowDialog<InsertTransitionListDlg>(startPageSettings.OkDialog);
            string clipboardText = TextUtil.LineSeparate(string.Join(TextUtil.SEPARATOR_TSV_STR,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Precursor_Adduct,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge
                ),
                string.Join(TextUtil.SEPARATOR_TSV_STR,
                    "H2O10", "[M+]", "HO10", "1"));
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() =>
            {
                insertTransitionListDlg.TransitionListText = clipboardText;
            });
            OkDialog(columnSelectDlg, columnSelectDlg.OkDialog);
            Assert.AreEqual(1, SkylineWindow.Document.MoleculeTransitionCount);
            Assert.AreEqual("HO10", SkylineWindow.Document.MoleculeTransitions.First().CustomIon.Formula);
        }
    }
}
