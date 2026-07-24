/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Shows and dismisses the native common OpenFileDialog once per run, with NO Skyline code -- it is an
    /// <see cref="AbstractUnitTest"/>, so TestRunner runs it (and its pass-1 leak check) without ever starting
    /// Skyline. The point is diagnostic: the connector's native-dialog functional tests (e.g.
    /// TestNativeMessageBox*) are reported as heap-memory leaks on some machines, and this isolates the common
    /// file dialog itself. If this bare test grows the process's Win32 heaps the same way -- and it uses none of
    /// our own code to do it -- then the growth is Windows' shell caches for the file dialog, not a leak in the
    /// connector. The DevTool HeapProbe measures the same thing outside the test harness; this brings it inside,
    /// so its heap deltas are directly comparable to the functional tests' under the same 20 KB threshold.
    /// </summary>
    [TestClass]
    public class NativeOpenFileDialogLeakTest : AbstractUnitTest
    {
        private const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class

        [TestMethod]
        public void TestNativeOpenFileDialogLeak()
        {
            // One dialog per run, to match a single native-dialog functional test run.
            ShowAndDismissOpenFileDialog();
        }

        /// <summary>
        /// Shows a native OpenFileDialog and dismisses it from another thread (the only thread free to do so,
        /// since ShowDialog blocks). The dialog is shown on a dedicated STA thread because
        /// <see cref="OpenFileDialog.ShowDialog()"/> requires single-threaded apartment state, which a TestRunner
        /// worker thread does not guarantee.
        /// </summary>
        private void ShowAndDismissOpenFileDialog()
        {
            // Close the dialog the moment it appears (found by enumerating top-level windows, needing no
            // cooperation from the thread showing it).
            var dismisser = new Thread(DismissDialogWhenShown) { IsBackground = true, Name = @"dialog dismisser" };
            dismisser.Start();

            Exception showError = null;
            var shower = new Thread(() =>
            {
                try
                {
                    using (var dlg = new OpenFileDialog())
                    {
                        // A valid starting folder, so the dialog does not spend time restoring a most-recently-used
                        // location that varies from run to run.
                        dlg.InitialDirectory = Path.GetTempPath();
                        dlg.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    showError = ex;
                }
            }) { IsBackground = true, Name = @"dialog shower" };
            shower.SetApartmentState(ApartmentState.STA);
            shower.Start();

            Assert.IsTrue(shower.Join(TimeSpan.FromSeconds(30)),
                @"The native Open dialog was not dismissed within 30 seconds.");
            dismisser.Join(TimeSpan.FromSeconds(5));
            if (showError != null)
                throw showError;
        }

        /// <summary>
        /// Polls for this process's native dialog window and sends it WM_CLOSE, which cancels the OpenFileDialog
        /// the way its title-bar close button would. Returns quietly if none appears in time -- the shower thread's
        /// join then times out and fails the test.
        /// </summary>
        private static void DismissDialogWhenShown()
        {
            var processId = (uint) Process.GetCurrentProcess().Id;
            for (int i = 0; i < 1000; i++) // up to ~10 seconds
            {
                foreach (var hwnd in User32.EnumWindows())
                {
                    if (!User32.IsWindowVisible(hwnd) || User32.GetClassName(hwnd) != DIALOG_CLASS_NAME)
                        continue;
                    User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                    if (windowProcessId != processId)
                        continue;
                    // Let the dialog finish coming up before closing it.
                    Thread.Sleep(30);
                    User32.SendMessage(hwnd, User32.WinMessageType.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                Thread.Sleep(10);
            }
        }
    }
}
