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
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ModifyMoleculeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestModifyMolecule()
        {
            TestFilesZip = @"TestFunctional\ModifyMoleculeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ModifyMoleculeTest.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            });

            var doc = SkylineWindow.Document;
            var originalMolecule = doc.Molecules.First();
            Assert.AreEqual("C54H93O40N3", originalMolecule.CustomMolecule.Formula);
            int precursorTransitionCount = originalMolecule.TransitionGroups.First().Transitions.Count(t => t.IsMs1);
            Assert.AreEqual(5, precursorTransitionCount);
            int productTransitionCount = originalMolecule.TransitionGroups.First().Transitions.Count(t => !t.IsMs1);
            Assert.AreEqual(3, productTransitionCount);

            // Change the molecule formula to a much smaller molecule and make sure nothing bad happens.
            const string newFormula = "C17H27N1O13";
            RunDlg<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide, dlg =>
            {
                dlg.FormulaBox.Formula = newFormula;
                dlg.OkDialog();
            });
            doc = WaitForDocumentChange(doc);
            var newMolecule = doc.Molecules.First();
            Assert.AreEqual(newFormula, newMolecule.CustomMolecule.Formula);

            // Number of precursor transitions should be reduced, but number of product transitions should stay the same.
            Assert.AreEqual(3, newMolecule.TransitionGroups.First().Transitions.Count(t=>t.IsMs1));
            Assert.AreEqual(productTransitionCount, newMolecule.TransitionGroups.First().Transitions.Count(t => !t.IsMs1));
        }
    }
}
