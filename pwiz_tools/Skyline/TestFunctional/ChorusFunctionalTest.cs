/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    //[TestClass]
    public class ChorusFunctionalTest : AbstractFunctionalTest
    {
        //[TestMethod]
        public void TestChorus()
        {
            Settings.Default.EnableChorus = true;
            Settings.Default.RemoteAccountList.Clear();
            TestFilesZip = @"TestFunctional\ChorusFunctionalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var chorusAccount = new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd");
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Hoofnagle_MSe_targeted.sky")));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var multiButtonMsgDlg = ShowDialog<MultiButtonMsgDlg>(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
            OkDialog(multiButtonMsgDlg, multiButtonMsgDlg.Btn1Click);
            var editChorusAccountDlg = WaitForOpenForm<EditRemoteAccountDlg>();
            RunUI(()=>
            {
                editChorusAccountDlg.SetRemoteAccount(chorusAccount);
            });
            OkDialog(editChorusAccountDlg, editChorusAccountDlg.OkDialog);
            Assert.AreEqual(chorusAccount.GetChorusUrl(), openDataSourceDialog.CurrentDirectory);
            RunUI(() =>
            {
                openDataSourceDialog.CurrentDirectory = chorusAccount.GetChorusUrl().AddPathPart("myFiles");
            });
            WaitForConditionUI(() => !openDataSourceDialog.WaitingForData);
            RunUI(() =>
            {
                openDataSourceDialog.SelectFile("2013_03_13_UWash_S1_MSE_Adj_001.raw.zip");
                openDataSourceDialog.SelectFile("2013_03_13_UWash_S2_MSE_Adj_001.raw.zip");
            });
            OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            ImportResultsNameDlg importResultsNameDlg = WaitForOpenForm<ImportResultsNameDlg>();
            OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
            WaitForConditionUI(() =>
                SkylineWindow.DocumentUI.Settings.HasResults &&
                SkylineWindow.DocumentUI.Settings.MeasuredResults.IsLoaded
                );
            Assert.AreEqual(2, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
        }
    }
}
