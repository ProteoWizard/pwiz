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
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : Form
    {
        public PauseAndContinueForm(string description = null)
        {
            InitializeComponent();
            if (description != null)
            {
                lblDescription.Text = description;
            }
            else
            {
                int delta = btnContinue.Top - lblDescription.Top;
                btnContinue.Top -= delta;
                Height -= delta;
                lblDescription.Visible = false;
            }

            // Adjust dialog width to accommodate description.
            if (lblDescription.Width > btnContinue.Width)
                Width = lblDescription.Width + lblDescription.Left*2;

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

        private static readonly object _pauseLock = new object();

        public static void Show(string description = null)
        {
            ClipboardEx.UseInternalClipboard(false);

            RunUI(() =>
            {
                SkylineWindow.UseKeysOverride = false;
                var dlg = new PauseAndContinueForm(description) { Left = SkylineWindow.Left };
                const int spacing = 15;
                var screen = Screen.FromControl(SkylineWindow);
                if (SkylineWindow.Top > screen.WorkingArea.Top + dlg.Height + spacing)
                    dlg.Top = SkylineWindow.Top - dlg.Height - spacing;
                else if (SkylineWindow.Bottom + dlg.Height + spacing < screen.WorkingArea.Bottom)
                    dlg.Top = SkylineWindow.Bottom + spacing;
                else
                {
                    dlg.Top = SkylineWindow.Top;
                    if (SkylineWindow.Left > screen.WorkingArea.Top + dlg.Width + spacing)
                        dlg.Left = SkylineWindow.Left - dlg.Width - spacing;
                    else if (SkylineWindow.Right + dlg.Width + spacing < screen.WorkingArea.Right)
                        dlg.Left = SkylineWindow.Right + spacing;
                    else
                    {
                        // Can't fit on screen without overlap, so put in upper left of screen
                        // despite overlap
                        dlg.Top = screen.WorkingArea.Top;
                        dlg.Left = screen.WorkingArea.Left;
                    }
                }
                dlg.Show(SkylineWindow);
            });

            lock (_pauseLock)
            {
                // Wait for an event on the pause lock, when the form is closed
                Monitor.Wait(_pauseLock);
                ClipboardEx.UseInternalClipboard();
                RunUI(() => SkylineWindow.UseKeysOverride = true);
            }
        }

        protected static void RunUI(Action act)
        {
            SkylineWindow.Invoke(act);
        }

        private static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }
    }
}
