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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfImportHundredsOfReplicatesTest : AbstractFunctionalTest
    {
        const int REPLICATE_COUNT_TO_TEST = 1500;
        [TestMethod]
        [Timeout(TestTimeout.Infinite)]
        public void TestImportHundredsOfReplicates()
        {
            TestFilesZip = @"TestPerf\PerfImportHundredsOfReplicatesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test_b.sky")));
            string templateFile = TestFilesDir.GetTestPath("mzmlfile.template");
            for (int i = 0; i < REPLICATE_COUNT_TO_TEST; i++)
            {
                string destFileName = TestFilesDir.GetTestPath("clone" + i.ToString("0000") + ".mzML");
                File.Copy(templateFile, destFileName);
            }
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            pwiz.Skyline.Properties.Settings.Default.AutoShowAllChromatogramsGraph = true;
            RunUI(()=>
            {
                importResultsDlg.ImportSimultaneousIndex = 2;
            });
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() =>
            {
                openDataSourceDialog.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(templateFile));
                openDataSourceDialog.SelectAllFileType("mzML");
            });
            
            OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            WaitForDocumentLoaded(7200 * 1000);
        }
    }
}
