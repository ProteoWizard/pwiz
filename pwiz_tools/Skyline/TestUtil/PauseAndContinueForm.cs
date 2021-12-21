/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : Form
    {
        private readonly string _linkUrl;
        private readonly bool _showMatchingPage;
        private Form _screenshotForm;
        private ScreenshotManager _screenshotManager;

        public PauseAndContinueForm(string description = null, string link = null, bool showMatchingPages = false, Form screenshotForm = null, ScreenshotManager screenshotManager = null)
        {
            InitializeComponent();
            _screenshotForm = screenshotForm;
            _screenshotManager = screenshotManager;

            if (_screenshotForm != null && _screenshotManager != null)
            {
                if (_screenshotForm is DockableForm dockableForm && dockableForm.DockState != DockState.Floating) 
                {
                    // If this dockable window isn't a floating window, then caller meant to screenshot the Skyline window
                    var parent = _screenshotForm.ParentForm;
                    if (parent != null)
                    {
                        _screenshotForm = parent;
                    }
                }
                // Show the copy buttons
                btnCopyToClipBoard.Visible = btnCopyToClipBoard.Enabled = true;
                if ((_screenshotForm is GraphSummary zgControl) && zgControl.GraphControl != null)
                {
                    btnCopyMetafileToClipboard.Visible = btnCopyMetafileToClipboard.Enabled = true; // Control is a metafile provider
                }
            }
            _linkUrl = link;
            if (!string.IsNullOrEmpty(link))
            {
                _showMatchingPage = showMatchingPages;
                lblDescriptionLink.Left = lblDescription.Left;
                lblDescription.Visible = false;
                lblDescriptionLink.Visible = true;
                if (string.IsNullOrEmpty(description))
                    description = "Show screenshot";
            }
            if (description != null)
            {
                if (link == null)
                    lblDescription.Text = description;
                else
                    lblDescriptionLink.Text = description;
            }
            else
            {
                int delta = btnContinue.Top - lblDescription.Top;
                btnContinue.Top -= delta;
                Height -= delta;
                lblDescription.Visible = false;
            }

            int descriptionWidth = link == null ? lblDescription.Width : lblDescriptionLink.Width;
            // Adjust dialog width to accommodate description.
            if (descriptionWidth > btnContinue.Width)
                Width = descriptionWidth + lblDescription.Left*2;

            // Finally make sure the button is fully visible
            Height += Math.Max(0, (btnContinue.Bottom + btnContinue.Left) - ClientRectangle.Bottom);
        }

        private void btnContinue_Click(object sender, EventArgs e)
        {
            Close();

            // Start the tests again
            lock (_pauseLock)
            {
                Monitor.PulseAll(_pauseLock);
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
        }

        private static readonly object _pauseLock = new object();

        public static void Show(string description = null, string link = null, bool showMatchingPages = false, int? timeout = null, Form screenshotForm = null, ScreenshotManager screenshotManager = null)
        {
            ClipboardEx.UseInternalClipboard(false);

            Form parentWindow = SkylineWindow;
            if (SkylineWindow != null)
                SkylineWindow.UseKeysOverride = false;
            else
                parentWindow = AbstractFunctionalTest.FindOpenForm<StartPage>();

            RunUI(parentWindow, () =>
            {
                var dlg = new PauseAndContinueForm(description, link, showMatchingPages, screenshotForm, screenshotManager) { Left = parentWindow.Left };
                const int spacing = 15;
                var screen = Screen.FromControl(parentWindow);
                if (parentWindow.Top > screen.WorkingArea.Top + dlg.Height + spacing)
                    dlg.Top = parentWindow.Top - dlg.Height - spacing;
                else if (parentWindow.Bottom + dlg.Height + spacing < screen.WorkingArea.Bottom)
                    dlg.Top = parentWindow.Bottom + spacing;
                else
                {
                    dlg.Top = parentWindow.Top;
                    if (parentWindow.Left > screen.WorkingArea.Top + dlg.Width + spacing)
                        dlg.Left = parentWindow.Left - dlg.Width - spacing;
                    else if (parentWindow.Right + dlg.Width + spacing < screen.WorkingArea.Right)
                        dlg.Left = parentWindow.Right + spacing;
                    else
                    {
                        // Can't fit on screen without overlap, so put in upper left of screen
                        // despite overlap
                        dlg.Top = screen.WorkingArea.Top;
                        dlg.Left = screen.WorkingArea.Left;
                    }
                }
                dlg.Show(parentWindow);
            });

            lock (_pauseLock)
            {
                // Wait for an event on the pause lock, when the form is closed
                if (!Monitor.Wait(_pauseLock, timeout ?? -1))
                {
                    // Close the form programmatically if timeout is exceeded
                    var form = FormUtil.OpenForms.FirstOrDefault(f => f is PauseAndContinueForm && f.IsHandleCreated);
                    if (form != null)
                        form.Close();
                }
                ClipboardEx.UseInternalClipboard();
                if (SkylineWindow != null)
                    RunUI(SkylineWindow, () => SkylineWindow.UseKeysOverride = true);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            if (_showMatchingPage)
                GotoLink();
            base.OnShown(e);
        }

        protected static void RunUI(Form form, Action act)
        {
            form.Invoke(act);
        }

        private static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        private void lblDescriptionLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GotoLink();
        }

        private void PauseAndContinueForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
                GotoLink();
        }

        public void GotoLink()
        {
            WebHelpers.OpenLink(_linkUrl);

            ActionUtil.RunAsync(() =>
            {
                Thread.Sleep(1000);

                if (IsHandleCreated)
                {
                    Invoke(new Action(() =>
                    {
                        SetForegroundWindow(Handle);
                        btnContinue.Focus();
                    }));
                }
            });
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private void btnCopyToClipboard_Click(object sender, EventArgs e)
        {
            // Copy current window image to clipboard, with clean edges
            _screenshotForm.Focus();
            _screenshotManager.TakeNextShot(_screenshotForm);
        }

        private void btnCopyMetaFileToClipboard_Click(object sender, EventArgs e)
        {
            _screenshotForm.Focus();
            if (_screenshotForm is GraphSummary zgControl)
            {
                CopyEmfToolStripMenuItem.CopyEmf(zgControl.GraphControl);
            }
        }
    }
}
