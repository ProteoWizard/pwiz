/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectrumFilterTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumFilterTransitionList()
        {
            TestFilesZip = @"TestFunctional\SpectrumFilterTransitionListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SpectrumFilterTransitionListTest.sky"));

            });
            RunDlg<ImportTransitionListColumnSelectDlg>(
                ()=>SkylineWindow.ImportMassList(TestFilesDir.GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
            Assert.AreEqual(14, SkylineWindow.Document.PeptideCount);
            Assert.AreNotEqual(1, SkylineWindow.Document.Peptides.First().TransitionGroupCount);
        }
    }
}
