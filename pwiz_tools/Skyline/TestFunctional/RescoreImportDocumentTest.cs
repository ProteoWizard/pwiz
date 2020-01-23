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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreImportDocumentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRescoreImportDocument()
        {
            TestFilesZip = @"TestFunctional\RescoreImportDocumentTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ImportTo.sky")));
            WaitForDocumentLoaded();
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg => dlg.Rescore(false));
            WaitForDocumentLoaded();
            var importDocResultsDlg = ShowDialog<ImportDocResultsDlg>(() =>
                SkylineWindow.ImportFiles(TestFilesDir.GetTestPath("ImportFrom.sky")));
            RunUI(() =>
            {
                importDocResultsDlg.Action = MeasuredResults.MergeAction.add;
                importDocResultsDlg.IsMergePeptides = true;
            });
            OkDialog(importDocResultsDlg, importDocResultsDlg.OkDialog);
            WaitForDocumentLoaded();
        }
    }
}
