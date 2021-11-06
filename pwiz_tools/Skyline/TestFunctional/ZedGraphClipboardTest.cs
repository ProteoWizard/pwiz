/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ZedGraphClipboardTest : AbstractFunctionalTest
    {
        // Using the system clipboard causes intermittent failures, since the clipboard might
        // be locked by other applications
        private bool _canUseSystemClipboard = false;
        [TestMethod]
        public void TestZedGraphClipboard()
        {
            TestFilesZip = @"TestFunctional\ZedGraphClipboardTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            try
            {
                DoClipboardTest();
            }
            finally
            {
                ClipboardEx.UseInternalClipboard();
            }
        }

        protected void DoClipboardTest()
        {
            string copyMenuText = Resources.ZedGraphClipboard_CreateCopyMenuItems_Copy;
            string copyMetafileMenuText = Resources.CopyEmfToolStripMenuItem_CopyEmfToolStripMenuItem_Copy_Metafile;
            string copyDataMenuText = Resources.CopyGraphDataToolStripMenuItem_CopyGraphDataToolStripMenuItem_Copy_Data;
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ZedGraphClipboardTest.sky")));
            WaitForDocumentLoaded();
            var graphChromatogram = FindOpenForm<GraphChromatogram>();
            ClipboardEx.SetText("hello");
            Assert.IsFalse(HasClipboardFormat(DataFormats.Bitmap));
            ClickContextMenuItem(graphChromatogram.GraphControl, copyMenuText);
            Assert.IsTrue(HasClipboardFormat(DataFormats.Bitmap));

            // Switch to using the system clipboard for the rest of the test so that we can test the "Copy Metafile"
            // menu item as well as testing the message when the clipboard is locked.
            // Only use the system clipboard if RunPerfTests is true, because tests using the system clipboard can be annoying.
            if (_canUseSystemClipboard)
            {
                ClipboardEx.UseInternalClipboard(false);
            }
            RetryActionIfMessage(() => ClipboardHelper.SetClipboardText(graphChromatogram, "hello"));
            RunUI(() =>
            {
                Assert.IsTrue(HasClipboardFormat(DataFormats.UnicodeText));
                Assert.IsFalse(HasClipboardFormat(DataFormats.Bitmap));
                Assert.IsFalse(HasClipboardFormat(DataFormats.EnhancedMetafile));
            });
            ClickContextMenuItem(graphChromatogram.GraphControl, copyMenuText);
            RunUI(() =>
            {
                Assert.IsFalse(HasClipboardFormat(DataFormats.UnicodeText));
                Assert.IsTrue(HasClipboardFormat(DataFormats.Bitmap));
                Assert.IsFalse(HasClipboardFormat(DataFormats.EnhancedMetafile));
            });
            if (_canUseSystemClipboard)
            {
                ClickContextMenuItem(graphChromatogram.GraphControl, copyMetafileMenuText);
                RunUI(() =>
                {
                    Assert.IsFalse(HasClipboardFormat(DataFormats.UnicodeText));
                    Assert.IsFalse(HasClipboardFormat(DataFormats.Bitmap));
                    Assert.IsTrue(HasClipboardFormat(DataFormats.EnhancedMetafile));
                });
            }
            ClickContextMenuItem(graphChromatogram.GraphControl, copyDataMenuText);
            RunUI(() =>
            {
                Assert.IsTrue(HasClipboardFormat(DataFormats.UnicodeText));
                Assert.IsFalse(HasClipboardFormat(DataFormats.Bitmap));
                Assert.IsFalse(HasClipboardFormat(DataFormats.EnhancedMetafile));
            });
            if (_canUseSystemClipboard)
            {
                ClickCopyItemWithLockedClipboard(ShowContextMenuItem(graphChromatogram.GraphControl, copyMenuText));
                ClickCopyItemWithLockedClipboard(ShowContextMenuItem(graphChromatogram.GraphControl, copyMetafileMenuText));
                ClickCopyItemWithLockedClipboard(ShowContextMenuItem(graphChromatogram.GraphControl, copyDataMenuText));
            }
        }

        /// <summary>
        /// Perform an action. If the action ends up bringing up a message box, then OK the message and retry again.
        /// </summary>
        /// <param name="action">The action to be performed on the UI thread which might fail and cause a message box to appear</param>
        /// <param name="setupAction">Optional action to be performed on the test thread each time before <paramref name="action"/> is invoked.</param>
        private void RetryActionIfMessage(Action action, Action setupAction = null)
        {
            int retry = 0;
            while (true)
            {
                setupAction?.Invoke();
                bool success = false;
                SkylineWindow.BeginInvoke(new Action(() =>
                {
                    action();
                    success = true;
                }));
                WaitForCondition(() => success || null != FindOpenForm<AlertDlg>());
                if (success)
                {
                    return;
                }
                retry++;
                if (retry >= 10)
                {
                    Assert.Fail("Failed to do clipboard action after {0} attempts", retry);
                }
                var alertDlg = FindOpenForm<AlertDlg>();
                Assert.IsNotNull(alertDlg);
                Console.Out.WriteLine("Failed, found message {0}, Retry #{1}", alertDlg.Message, retry);
                OkDialog(alertDlg, alertDlg.OkDialog);
            }
        }

        private void ClickCopyItemWithLockedClipboard(ToolStripMenuItem menuItem)
        {
            RunWithLockedClipboard(() =>
            {
                var alertDlg = ShowDialog<AlertDlg>(menuItem.PerformClick);
                string messageText = alertDlg.Message;
                Assert.AreEqual(ClipboardHelper.GetCopyErrorMessage(), messageText);
                OkDialog(alertDlg, alertDlg.OkDialog);
            });
        }

        private void ClickContextMenuItem(ZedGraphControl zedGraphControl, string menuItemText)
        {
            ToolStripMenuItem menuItem = null;
            RetryActionIfMessage(()=>
            {
                Assert.IsNotNull(menuItem);
                menuItem.PerformClick();
            }, () =>
            {
                menuItem = ShowContextMenuItem(zedGraphControl, menuItemText);
                Assert.IsNotNull(menuItem);
            });
        }

        /// <summary>
        /// Displays the context menu and then finds the menu item with the specified menu item text.
        /// Note that even if "SkylineOffscreen" is true, the menu ends up flashing on the screen since
        /// context menus always want to reposition themselves to be on the visible part of the screen.
        /// </summary>
        private ToolStripMenuItem ShowContextMenuItem(Control control, string menuItemText)
        {
            ToolStripMenuItem menuItem = null;
            RunUI(() =>
            {
                Assert.IsNotNull(control.ContextMenuStrip);
                control.ContextMenuStrip.Show(control, new Point());
                var matchingMenuItems = control.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                    .Where(item => item.Text == menuItemText).ToList();
                Assert.AreEqual(1, matchingMenuItems.Count);
                menuItem = matchingMenuItems[0];
            });
            Assert.IsNotNull(menuItem);
            return menuItem;
        }

        private bool HasClipboardFormat(string formatName)
        {
            return ClipboardEx.GetClipboardFormats().Contains(formatName);
        }

        /// <summary>
        /// Launches a background thread which locks the clipboard, and while the clipboard
        /// is locked, executes the specified action.
        /// Waits until the clipboard is unlocked before returning.
        /// </summary>
        private void RunWithLockedClipboard(Action action)
        {
            ClipboardLockingForm clipboardLockingForm = null;
            var thread = new Thread(() =>
            {
                try
                {
                    clipboardLockingForm = new ClipboardLockingForm();
                    Application.Run(clipboardLockingForm);
                }
                catch (Exception e)
                {
                    Program.ReportException(e);
                }
            })
            {
                Name = "Clipboard Locking Thread"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            WaitForCondition(() => null != FindOpenForm<ClipboardLockingForm>());
            action();
            clipboardLockingForm.BeginInvoke(new Action(()=>clipboardLockingForm.Close()));
            WaitForCondition(() => null == FindOpenForm<ClipboardLockingForm>());
        }

        /// <summary>
        /// Form which locks the clipboard when it is shown, and unlocks the clipboard when it is closed.
        /// </summary>
        sealed class ClipboardLockingForm : FormEx
        {
            [DllImport("user32.dll", EntryPoint = "OpenClipboard", SetLastError = true)]
            private static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool CloseClipboard();

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool EmptyClipboard();
            public ClipboardLockingForm()
            {
                Text = @"System Clipboard Is Locked";
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                int retry = 0;
                while (true)
                {
                    if (OpenClipboard(Handle))
                    {
                        break;
                    }

                    retry++;
                    if (retry >= 10)
                    {
                        Assert.Fail("Failed to open clipboard after {0} attempts", retry);
                    }
                    Console.Out.WriteLine("Failed to open clipboard. Retry #{0}", retry);
                    Thread.Sleep(10);
                }
                bool emptyClipboardResult = EmptyClipboard();
                AssertEx.IsTrue(emptyClipboardResult);
            }

            protected override void OnHandleDestroyed(EventArgs e)
            {
                AssertEx.IsTrue(CloseClipboard());
                base.OnHandleDestroyed(e);
            }
        }
    }
}
