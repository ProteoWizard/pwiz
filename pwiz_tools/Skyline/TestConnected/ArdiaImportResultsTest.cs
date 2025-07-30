/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    /// <summary>
    /// This test expects specific files exist on a remote Ardia server and the test will fail
    /// if the files cannot be found.
    ///
    /// If the files go missing and need to be uploaded again, unzip this archive in the remote server's
    /// root directory:
    ///
    ///     perftests/TestConnected-ArdiaImportResultsTest.zip
    ///
    /// Use Ardia's "Upload Files" button to upload the Small/ directory. This creates the Small
    /// sequence and also uploads two files - BSA_min_21 and caffeicquinic acid MSMS.
    /// </summary>
    [TestClass]
    public class ArdiaImportResultsTest : AbstractFunctionalTestEx
    {
        private ArdiaAccount _account;
        private List<string[]> _openPaths;

        private readonly List<string[]> SmallPaths = new List<string[]>
        {
            new[] { "Skyline" },
            new[] { "Small" },
            new[] { "BSA_min_21", "caffeicquinic acid MSMS" }
        };

        private readonly List<string[]> LargePaths = new List<string[]>
        {
            new[] { "Skyline" },
            new[] { "ExtraLargeFiles" },
            new[] { "Test" },
            new[] { "astral", "Q_2014_0523_12_0_amol_uL_20mz" }
        };

        [TestMethod]
        public void TestArdiaSingleRole()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFunctionalTest.zip";

            _account = ArdiaTestUtil.GetTestAccount(ArdiaTestUtil.AccountType.SingleRole);
            _openPaths = SmallPaths;

            RunFunctionalTest();
        }

        [TestMethod]
        public void TestArdiaSingleRoleDeleteAfterImport()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFunctionalTest.zip";

            _account = ArdiaTestUtil.GetTestAccount(ArdiaTestUtil.AccountType.SingleRole).ChangeDeleteRawAfterImport(true);
            _openPaths = SmallPaths;

            RunFunctionalTest();
        }

        //[TestMethod]
        public void TestArdiaMultiRole()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFunctionalTest.zip";

            _account = ArdiaTestUtil.GetTestAccount();
            _openPaths = SmallPaths;

            RunFunctionalTest();
        }

        //[TestMethod]
        public void TestArdiaLargeFile()
        {
            if (!ArdiaTestUtil.EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            /*if (!RunPerfTests)
            {
                Console.Error.WriteLine("NOTE: skipping TestArdiaLargeFile because perftests are not enabled");
                return;
            }*/

            TestFilesZip = @"TestConnected\ArdiaFunctionalTest.zip";

            _account = ArdiaTestUtil.GetTestAccount(ArdiaTestUtil.AccountType.SingleRole);
            _openPaths = LargePaths;

            RunFunctionalTest();
        }

        /*[TestMethod]
        public void ConsoleArdiaImportTest()
        {
            TestFilesZip = @"TestConnected\ArdiaFunctionalTest.zip";
            TestFilesDirs = new[] { new TestFilesDir(TestContext, TestFilesZip) };

            string docPath = TestFilesDir.GetTestPath("small.sky");

            _account = ArdiaTestUtil.GetTestAccount();
            Settings.Default.RemoteAccountList.Add(_account);
            _openPaths = SmallPaths;
            var file1 = new ArdiaUrl(
                "ardia:path=Skyline%2FSmall%2520Files%2FUracil_Caffeine%2528Water%2529_Inj_Det_2_04&server=https%3A%2F%2Fhyperbridge.cmdtest.thermofisher.com&username=chambem2%40uw.edu&id=71b1450c-5edf-4ba9-9b61-0a0a1f14c8d7&resourceKey=sequences%2F6cfd3b36-3d31-4142-8d05-b5437f3740ec&storageId=2024%2F04%2F10%2Ff22f87fc-b61e-4c95-8a04-ce288138989f.raw&rawName=Uracil_Caffeine%28Water%29_Inj_Det_2_04.raw&rawSize=208048");
            var file2 = new ArdiaUrl(
                "ardia:path=Skyline%2FSmall%2520Files%2FReserpine_10%2520pg_%25C2%25B5L_2_08&server=https%3A%2F%2Fhyperbridge.cmdtest.thermofisher.com&username=chambem2%40uw.edu&id=455ea870-b57b-4a12-8c2b-eb814c38c12b&resourceKey=sequences%2F6cfd3b36-3d31-4142-8d05-b5437f3740ec&storageId=2024%2F04%2F10%2Fa24d9ecf-679b-4363-b934-4b8a88bfbcd9.raw&rawName=Reserpine_10%20pg_%C2%B5L_2_08.raw&rawSize=130452");

            // arguments that would normally be quoted on the command-line shouldn't be quoted here
            var settings = new[]
            {
                "--in=" + docPath,
                "--import-file=" + file1,
                "--import-file=" + file2,
                "--save"
            };

            string output = RunCommand(settings);
            StringAssert.Contains(output, "Imported file ____");

            SrmDocument doc = ResultsUtil.DeserializeDocument(docPath);
            foreach(var path in _openPaths.Last())
                Assert.IsTrue(doc.MeasuredResults.MSDataFilePaths.Any(p => p.GetFileName() == path));
        }*/

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("caffeicquinic acid.sky")));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
            RunUI(() => editAccountDlg.SetRemoteAccount(_account));

            // Click test button
            var testSuccessfulDlg = ShowDialog<MessageDlg>(() => editAccountDlg.TestSettings());
            OkDialog(testSuccessfulDlg, testSuccessfulDlg.OkDialog);
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);

            // PauseTest();

            foreach (var paths in _openPaths)
                OpenFile(openDataSourceDialog, paths);
            WaitForDocumentLoaded();
            WaitForClosedAllChromatogramsGraph();

            // short circuit for large file test
            if (ReferenceEquals(_openPaths, LargePaths))
                return;

            string rawFilepath = TestFilesDir.GetTestPath(_openPaths.Last().First() + ".raw");

            if (_account.DeleteRawAfterImport)
            {
                AssertEx.FileNotExists(rawFilepath); // file should have been deleted after importing
                return; // short circuit this test variant
            }

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("caffeicquinic acid.sky")));

            if (!_account.DeleteRawAfterImport)
            {
                // delete local RAW file to test that it gets redownloaded when clicking on the chromatogram to view a spectrum
                AssertEx.FileExists(rawFilepath);
                File.Delete(rawFilepath);
            }
            
            WaitForDocumentLoaded();

            RunUI(() => SkylineWindow.SelectElement(ElementRefs.FromObjectReference(ElementLocator.Parse("Molecule:/Molecules/Caffeicquinic acid"))));

            // Switch to the second Chromatogram graph
            var graphChrom = SkylineWindow.GetGraphChrom("caffeicquinic acid MSMS");
            RunUI(() =>
            {
                graphChrom.Activate();
                graphChrom.Focus();
            });

            // Must pass name here, otherwise ClickChromatogram always chooses the first graph
            ClickChromatogram(@"caffeicquinic acid MSMS", 0.0092, 3900000);

            GraphFullScan graphFullScan = FindOpenForm<GraphFullScan>();
            Assert.IsNotNull(graphFullScan);
            RunUI(() => graphFullScan.Close());

            // delete results and reimport to test using saved cookie
            RemoveResultsAndReimport();

            // corrupt the cookie (simulate it being expired) and try reimporting again
            /*ArdiaAccount.SetSessionCookieString(_account, "foobar" );
            _account.ResetAuthenticatedHttpClientFactory();

            // delete local files
            foreach (var rawName in _openPaths.Last())
                File.Delete(TestFilesDir.GetTestPath(rawName + ".raw"));

            Settings.Default.RemoteAccountList.Clear();
            Settings.Default.RemoteAccountList.Add(_account);
            RemoveResultsAndReimport();*/

            ArdiaAccount.ClearSessionCookieStrings();
        }

        private void RemoveResultsAndReimport()
        {
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.RemoveAllReplicates();
                dlg.OkDialog();
            });
            RunUI(() => SkylineWindow.SaveDocument());

            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
            foreach (var paths in _openPaths)
                OpenFile(openDataSourceDialog, paths);
            WaitForDocumentLoaded();
            WaitForClosedAllChromatogramsGraph();
        }

        private void OpenFile(OpenDataSourceDialog openDataSourceDialog, params string[] names)
        {
            WaitForConditionUI(() => names.All(n => openDataSourceDialog.ListItemNames.Contains(n)));
            RunUI(() =>
            {
                foreach (string name in names)
                    openDataSourceDialog.SelectFile(name);
                openDataSourceDialog.Open();
            });
        }
    }
}
