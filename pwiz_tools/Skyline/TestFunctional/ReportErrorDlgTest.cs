/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReportErrorDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReportErrorDlg()
        {
            TestFilesZip = @"TestFunctional\StartPageTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        { 
            var skylineWindow = WaitForOpenForm<SkylineWindow>();
            RunUI(() => skylineWindow.OpenFile(TestFilesDir.GetTestPath("StartPageTest.sky")));            
            ReportException(new Exception());
            var reportErrorDlg = WaitForOpenForm<ReportErrorDlg>();
            RunDlg<DetailedReportErrorDlg>(reportErrorDlg.OkDialog, skippedReportForm2 =>
            {
                Assert.IsNotNull(skippedReportForm2);
                Assert.IsTrue(skippedReportForm2.ScreenShots.Count > 0);
                skippedReportForm2.IsTest = true;
                skippedReportForm2.SetFormProperties(true, true, "yuval@uw.edu", "text");
                skippedReportForm2.OkDialog(false);
            });

            ReportException(new Exception());
            var reportErrorDlg2 = WaitForOpenForm<ReportErrorDlg>();
            var skippedReportForm= ShowDialog<DetailedReportErrorDlg>(reportErrorDlg2.OkDialog);
            RunUI(()=>
            {
                Assert.IsNotNull(skippedReportForm);
                Assert.IsTrue(skippedReportForm.ScreenShots.Count > 0);
                skippedReportForm.IsTest = true;
                skippedReportForm.SetFormProperties(true, true, "yuval", "text");
            });
            var messageDlg = ShowDialog<MessageDlg>(() => skippedReportForm.OkDialog(false));
            RunUI(() =>
            {
                Assert.AreEqual(messageDlg.Message, Resources.SkippedReportErrorDlg_btnOK_Click_No_Email);
                messageDlg.OkDialog();
            });
            RunUI(() =>
            {
                Assert.IsNotNull(skippedReportForm);
                Assert.IsTrue(skippedReportForm.ScreenShots.Count > 0);
                skippedReportForm.IsTest = true;
                skippedReportForm.SetFormProperties(true, true, "yuval@uw.edu", "text");
                skippedReportForm.OkDialog(false);
            });
        }

        private void ReportException(Exception x)
        {
            RunUI(() =>
            {
                using (new StoreExceptions())
                {
                    Program.ReportException(x);
                }
            });
        }

        private class StoreExceptions : IDisposable
        {
            private readonly List<Exception> _programExceptions;
            public StoreExceptions()
            {
                _programExceptions = Program.TestExceptions;
                Program.TestExceptions = null;
            }

            public void Dispose()
            {
                Program.TestExceptions = _programExceptions;
            }
        }
    }
}
