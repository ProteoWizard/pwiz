/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Class which starts a background thread which closes the first <see cref="LongWaitDlg"/> it sees.
    /// </summary>
    public class LongWaitDialogCanceler : IDisposable
    {
        private bool _disposed;

        public LongWaitDialogCanceler()
        {
            ActionUtil.RunAsync(ThreadProc, "Long Wait Dialog Canceler");
        }

        public void Dispose()
        {
            lock (this)
            {
                _disposed = true;
            }
        }

        private void ThreadProc()
        {
            while (true)
            {
                lock (this)
                {
                    if (_disposed)
                    {
                        return;
                    }
                }

                var longWaitDlg = FormUtil.OpenForms.OfType<LongWaitDlg>().FirstOrDefault();
                if (true == longWaitDlg?.IsHandleCreated)
                {
                    CommonActionUtil.SafeBeginInvoke(longWaitDlg, ()=>
                    {
                        longWaitDlg.CancelButton.PerformClick();
                    });
                    return;
                }
                Thread.Sleep(0);
            }
        }
    }
}
