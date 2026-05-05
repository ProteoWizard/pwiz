/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Background watchdog that dismisses any <see cref="ThreadExceptionDialog"/> that appears
    /// during test teardown. A modal ThreadExceptionDialog hangs the test runner indefinitely;
    /// this canceler turns the hang into a logged failure by force-closing the dialog and
    /// recording its exception text in <see cref="Program.TestExceptions"/>.
    ///
    /// Why: A ThreadExceptionDialog can appear when WinForms catches an exception inside a
    /// reentrant WndProc dispatch (e.g. <see cref="Form.WmClose"/> calling
    /// <see cref="EventWaitHandle.Set"/> on a disposed SafeWaitHandle during teardown). When
    /// that happens, <see cref="Application.ThreadException"/> is sometimes bypassed and the
    /// dialog appears anyway, blocking the UI thread in a nested message loop.
    /// </summary>
    public class ThreadExceptionDialogCanceler : IDisposable
    {
        private bool _disposed;

        public ThreadExceptionDialogCanceler()
        {
            ActionUtil.RunAsync(ThreadProc, @"Thread Exception Dialog Canceler");
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

                try
                {
                    DismissOpenDialogs();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(@"ThreadExceptionDialogCanceler error: {0}", ex);
                }

                Thread.Sleep(500);
            }
        }

        private static void DismissOpenDialogs()
        {
            var dialogs = FormUtil.OpenForms.OfType<ThreadExceptionDialog>().ToList();
            foreach (var dialog in dialogs)
            {
                if (dialog.IsDisposed || !dialog.IsHandleCreated)
                {
                    continue;
                }

                Console.WriteLine(@"*** ThreadExceptionDialog detected during test teardown - dismissing");
                Program.AddTestException(new InvalidOperationException(
                    string.Format(@"ThreadExceptionDialog appeared during test teardown: {0}",
                        TryGetDialogText(dialog))));

                CommonActionUtil.SafeBeginInvoke(dialog, () =>
                {
                    try
                    {
                        // Setting DialogResult on a modal form posts WM_CLOSE asynchronously
                        // (PostMessage), which avoids the synchronous reentrant Form.Close() path
                        // that triggered the original hang.
                        if (!dialog.IsDisposed)
                        {
                            dialog.DialogResult = DialogResult.Cancel;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(@"Failed to dismiss ThreadExceptionDialog: {0}", ex);
                    }
                });
            }
        }

        private static string TryGetDialogText(Form dialog)
        {
            try
            {
                // ThreadExceptionDialog's main message text is in a TextBox child control.
                var textBox = dialog.Controls.OfType<TextBox>().FirstOrDefault(tb => tb.Multiline);
                if (textBox != null && !string.IsNullOrEmpty(textBox.Text))
                {
                    return textBox.Text;
                }
            }
            catch
            {
                // Ignore - best-effort diagnostic.
            }
            return dialog.Text ?? @"<no text>";
        }
    }
}
