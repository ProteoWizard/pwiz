/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LongWaitTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLongWait()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Test non-cancellable dialog.
            using (var longWaitDlg = new LongWaitDlg(null, false))
            {
                longWaitDlg.PerformWork(Program.MainWindow, 20, () =>
                {
                    for (int i = 0; i < 100; i += 10)
                    {
                        Thread.Sleep(5);
                        longWaitDlg.ProgressValue = i;
                    }
                });
            }

            // Show cancellable dialog if we're running this Form from SkylineTester.
            if (Program.PauseForms != null && Program.PauseForms.Contains(typeof(LongWaitDlg).Name))
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.PerformWork(Program.MainWindow, 0, () =>
                    {
                        WaitForOpenForm<LongWaitDlg>();
                        longWaitDlg.CancelButton.PerformClick();
                    });
                    Assert.IsTrue(longWaitDlg.IsCanceled);
                }
            }
        }
    }
}
