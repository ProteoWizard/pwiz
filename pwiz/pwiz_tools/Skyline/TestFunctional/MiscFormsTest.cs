/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class MiscFormsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMiscForms()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Show About dialog.
            using (var about = new AboutDlg())
            {
                RunDlg<AboutDlg>(
                    () => about.ShowDialog(Program.MainWindow),
                    a => a.Close());
            }

            // Show Alert link dialog.
            RunDlg<AlertLinkDlg>(
                () => WebHelpers.ShowLinkFailure(Program.MainWindow, "http://skyline.maccosslab.org"),
                d => d.Close());

            // Show shutdown report dialog
            Assert.IsFalse(ReportShutdownDlg.HadUnexpectedShutdown(true));
            try
            {
                throw new IOException("Something to report");
            }
            catch (Exception x)
            {
                ReportShutdownDlg.SaveExceptionFile(x, true);
            }
            Assert.IsTrue(ReportShutdownDlg.HadUnexpectedShutdown(true));
            using (var reportShutdownDlg = new ReportShutdownDlg())
            {
                RunDlg<ReportShutdownDlg>(
                    () => reportShutdownDlg.ShowDialog(),
                    d => d.Close());
            }
            Assert.IsFalse(ReportShutdownDlg.HadUnexpectedShutdown(true));

            // Show upgrade dialog
            using (var dlg = new UpgradeDlg(Program.LICENSE_VERSION_CURRENT - 1))
            {
                RunDlg<UpgradeDlg>(
                    () => dlg.ShowDialog(),
                    d => d.Close());
            }
        }
    }
}
