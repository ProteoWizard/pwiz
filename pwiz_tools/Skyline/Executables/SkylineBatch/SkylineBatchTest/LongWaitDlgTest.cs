/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;

namespace SkylineBatchTest
{
    [TestClass]
    public class LongWaitDlgTest : AbstractSkylineBatchUnitTest
    {
        /// <summary>
        /// LongWaitOperation starts its work before ShowDialog creates the dialog's window, so a
        /// fast operation can call Finish while there is no window handle. Finish used to throw
        /// there, which skipped the caller's completion callback and left the dialog open forever.
        /// </summary>
        [TestMethod]
        public void TestFinishBeforeDialogShown()
        {
            Exception failure = null;
            var closedByItself = false;
            var thread = new Thread(() =>
            {
                try
                {
                    using var parent = new Form();
                    using var dlg = new LongWaitDlg(parent, @"Test", @"Working");
                    Assert.IsFalse(dlg.IsHandleCreated);
                    dlg.Finish();
                    dlg.ShowDialog(parent);
                    closedByItself = true;
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            // A regression leaves ShowDialog waiting for a close that never comes.
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)), @"LongWaitDlg did not close after Finish");
            if (failure != null)
                throw new AssertFailedException(@"LongWaitDlg.Finish failed before the dialog was shown", failure);
            Assert.IsTrue(closedByItself);
        }
    }
}
