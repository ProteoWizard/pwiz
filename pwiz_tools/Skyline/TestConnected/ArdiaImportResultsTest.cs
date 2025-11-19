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
    /// This test requires certain files are available on a remote Ardia server. The test will fail
    /// if those files cannot be found.
    ///
    /// If the files need to be uploaded again (for example, they were deleted or the test refers to
    /// a new Ardia server without them), unzip this archive in the remote server's
    /// root directory:
    ///
    ///     {panorama-server}/MacCoss/software/perftests/TestConnected-ArdiaImportResultsTest.zip
    ///
    /// Use Ardia's "Upload Files" button to upload the Small/ directory. This creates the Small
    /// sequence and also uploads two files - BSA_min_21 and caffeicquinic acid MSMS.
    /// </summary>
    [TestClass]
    public class ArdiaTest : AbstractFunctionalTestEx
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
            /*ArdiaAccount.SetToken(_account, "foobar" );
            _account.ResetAuthenticatedHttpClientFactory();

            // delete local files
            foreach (var rawName in _openPaths.Last())
                File.Delete(TestFilesDir.GetTestPath(rawName + ".raw"));

            Settings.Default.RemoteAccountList.Clear();
            Settings.Default.RemoteAccountList.Add(_account);
            RemoveResultsAndReimport();*/

            SkylineWindow.ClearArdiaAccountTokens();
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
