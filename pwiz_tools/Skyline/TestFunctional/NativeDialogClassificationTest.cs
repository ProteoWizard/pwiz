/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that <see cref="NativeDialog.Create"/> never reports a native dialog as a kind it is not, at ANY
    /// moment of the dialog's life -- while the shell is still building it, while it is shown, and while the shell
    /// is tearing it down. A common file dialog is built in stages on the classic template: the Save dialog carries
    /// the classic file-name combo for its first moments, then destroys it, and creates its own file-name field
    /// well over a hundred milliseconds later; before that field exists the dialog has none of the controls that
    /// say which dialog it is, and looked like a message box -- which was ready the moment its window was shown
    /// and refuses to take a file name. Whether that window is ever SHOWN in that state depends on the machine (it was
    /// on TeamCity, intermittently, failing TestNativeMessageBox with "Setting values is not supported for native
    /// dialog Dialog:Save As"), so this does not wait for the shell to show the dialog: it watches the window from
    /// the moment it exists, and asserts that every kind reported for it is the right one or "not yet".
    /// </summary>
    [TestClass]
    public class NativeDialogClassificationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNativeDialogClassification()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // A folder the dialogs can open in without a slow network round trip.
            var initialDirectory = Path.GetTempPath();

            AssertClassifiedOnlyAs<NativeSaveFileDialog>(() =>
            {
                using var dlg = new SaveFileDialog();
                dlg.InitialDirectory = initialDirectory;
                dlg.FileName = @"Document.sky";
                dlg.OverwritePrompt = true;
                dlg.ShowDialog(SkylineWindow);
            });
            AssertClassifiedOnlyAs<NativeOpenFileDialog>(() =>
            {
                using var dlg = new OpenFileDialog();
                dlg.InitialDirectory = initialDirectory;
                dlg.ShowDialog(SkylineWindow);
            });
            // The multiselect flavour of the Open dialog ("Add Input Files") is built the same way.
            AssertClassifiedOnlyAs<NativeOpenFileDialog>(() =>
            {
                using var dlg = new OpenFileDialog();
                dlg.Multiselect = true;
                dlg.InitialDirectory = initialDirectory;
                dlg.ShowDialog(SkylineWindow);
            });
            // A message box is the generic dialog -- what the file dialogs above must not be mistaken for -- and
            // is still reported as one.
            AssertClassifiedOnlyAs<NativeDialog>(
                () => MessageBox.Show(SkylineWindow, @"body", @"Save As", MessageBoxButtons.OKCancel)); // Purposely using MessageBox here
        }

        /// <summary>
        /// Shows a native dialog and cancels it, and asserts that for the whole time its window existed
        /// <see cref="NativeDialog.Create"/> reported it either as exactly <typeparamref name="TDlg"/> or as not
        /// ready (null) -- never as another kind -- and as <typeparamref name="TDlg"/> at some point.
        /// </summary>
        private void AssertClassifiedOnlyAs<TDlg>(Action showDlg) where TDlg : NativeDialog
        {
            var watch = new ClassificationWatch();
            RunLongNativeDlg<TDlg>(showDlg, dlg => dlg.DismissWithCancelButton());
            // The shell tears the dialog down after ShowDialog has returned: watch that too.
            WaitForCondition(() => !ClassificationWatch.FindDialogWindows().Any());
            var kinds = watch.Stop();

            var expected = typeof(TDlg);
            var wrong = kinds.Where(kind => kind != null && kind != expected).ToList();
            Assert.AreEqual(0, wrong.Count, @"The {0} was reported as {1}.", expected.Name,
                string.Join(@", ", wrong.Select(kind => kind.Name)));
            Assert.IsTrue(kinds.Contains(expected), @"The {0} was never reported.", expected.Name);
        }

        /// <summary>
        /// Records every kind <see cref="NativeDialog.Create"/> reports for the "#32770" windows of this process --
        /// shown or not -- from construction until <see cref="Stop"/>, from a thread of its own so the test thread
        /// is free to drive the dialog. Create is window-manager reads only, so it is safe to call from here while
        /// the dialog's modal loop runs on the UI thread.
        /// </summary>
        private sealed class ClassificationWatch
        {
            private readonly HashSet<Type> _kinds = new HashSet<Type>();
            private readonly Thread _thread;
            private volatile bool _stop;

            public ClassificationWatch()
            {
                _thread = new Thread(Watch) { IsBackground = true };
                _thread.Start();
            }

            private void Watch()
            {
                while (!_stop)
                {
                    foreach (var hwnd in FindDialogWindows())
                    {
                        var kind = NativeDialog.Create(hwnd, CancellationToken.None)?.GetType();
                        lock (_kinds)
                            _kinds.Add(kind);
                    }
                    Thread.Yield();
                }
            }

            /// <summary>Stops watching and returns every kind seen (null for "not ready").</summary>
            public ICollection<Type> Stop()
            {
                _stop = true;
                _thread.Join();
                lock (_kinds)
                    return _kinds.ToList();
            }

            /// <summary>This process's "#32770" windows, shown or not -- the whole life of a dialog, not just the
            /// part <see cref="NativeDialog.GetOpenDialogs"/> reports.</summary>
            public static IList<IntPtr> FindDialogWindows()
            {
                var processId = Kernel32.GetCurrentProcessId();
                return User32.EnumWindows().Where(hwnd =>
                {
                    User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                    return windowProcessId == processId && User32.GetClassName(hwnd) == @"#32770";
                }).ToList();
            }
        }
    }
}
