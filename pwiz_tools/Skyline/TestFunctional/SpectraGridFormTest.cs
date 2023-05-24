/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Spectra;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectraGridFormTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectraGridForm()
        {
            TestFilesZip = @"TestFunctional\SpectraGridFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var spectraGridForm = ShowDialog<SpectrumGridForm>(SkylineWindow.ViewMenu.ShowSpectraGridForm);
            RunUI(() =>
            {
                spectraGridForm.AddFile(new MsDataFilePath(TestFilesDir.GetTestPath("SpectrumClassFilterTest.mzML")));
            });
            WaitForConditionUI(spectraGridForm.IsComplete);
            Assert.AreEqual(3, spectraGridForm.DataGridView.RowCount);
            OkDialog(spectraGridForm, spectraGridForm.Close);
        }
    }
}
