/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportFailureTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestImportFailure()
        {
            Run(@"TestFunctional\ImportFailureTest.zip");
        }

        private const string SUCCEEDS_FILE_NAME = "succeeds.mzML";
        private const string SUCCEEDS2_FILE_NAME = "succeeds2.mzML";
        private const string FAILS_FILE_NAME = "fails.mzML";

        protected override void DoTest()
        {
            OpenDocument("Bovine_std_curated_seq_small2.sky");
            string succeedsFile = TestFilesDir.GetTestPath(SUCCEEDS_FILE_NAME);
            string succeeds2File = TestFilesDir.GetTestPath(SUCCEEDS2_FILE_NAME);
            string failsFile = TestFilesDir.GetTestPath(FAILS_FILE_NAME);
            File.Copy(succeedsFile, succeeds2File);
            var docOriginal = WaitForDocumentLoaded();

            // Cancel after failure
            var docCancel = ImportFailure(docOriginal, dlg => dlg.BtnCancelClick(), FAILS_FILE_NAME, SUCCEEDS_FILE_NAME);
            Assert.IsFalse(docCancel.Settings.HasResults);
            
            // Skip after failure
            var docSkip = ImportFailure(docCancel, dlg => dlg.Btn1Click(), FAILS_FILE_NAME, SUCCEEDS_FILE_NAME);
            Assert.IsTrue(docSkip.Settings.HasResults);
            Assert.AreEqual(1, docSkip.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS_FILE_NAME),
                docSkip.Settings.MeasuredResults.Chromatograms[0].Name);

            // Retry after failure
            ImportResultsAsync(FAILS_FILE_NAME, SUCCEEDS2_FILE_NAME);
            var dlgImportFailed = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(dlgImportFailed, () => dlgImportFailed.Btn0Click());
            var dlgImportFailed2 = WaitForOpenForm<MultiButtonMsgDlg>();
            var docBeforeSuccess = SkylineWindow.Document;
            File.Copy(succeedsFile, failsFile, true);
            RunUI(() => dlgImportFailed2.Btn0Click());
            var docAfterSuccess = WaitForDocumentChangeLoaded(docBeforeSuccess);
            Assert.AreEqual(3, docAfterSuccess.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(FAILS_FILE_NAME),
                docAfterSuccess.Settings.MeasuredResults.Chromatograms[1].Name);
        }

        private SrmDocument ImportFailure(SrmDocument doc, Action<MultiButtonMsgDlg> act, params string[] dataFiles)
        {
            ImportResultsAsync(dataFiles);
            var dlgImportFailed = WaitForOpenForm<MultiButtonMsgDlg>();
            RunUI(() => act(dlgImportFailed));
            return WaitForDocumentChangeLoaded(doc);
        }
    }
}