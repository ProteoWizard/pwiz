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
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : Form
    {
        private readonly ScreenshotPreviewForm _screenshotPreviewForm;
        private readonly ScreenshotManager _screenshotManager;
        private readonly object _pauseLock = new object();
        private readonly Form _ownerForm;

        private string _linkUrl;
        private bool _showMatchingPage;
        private string _fileToSave;
        private string _description;
        private Control _screenshotForm;
        private bool _fullScreen;
        private Func<Bitmap, Bitmap> _processShot;
        private PauseAndContinueMode _currentMode = PauseAndContinueMode.PAUSE_AND_CONTINUE;

        private enum PauseAndContinueMode
        {
            PAUSE_AND_CONTINUE,
            PREVIEW_SCREENSHOT
        }

        public PauseAndContinueForm(ScreenshotManager screenshotManager, Form ownerForm = null)
        {
            InitializeComponent();
            // Form is not ready to be shown until we have initialized its state.
            // This is needed when form is created from Application.Run(form) which
            // automatically displays the form. 
            Opacity = 0;
            // A strange ordering bug in calculating the size of the boarder makes it necessary to remove
            // the ControlBox after the FormBorderStyle is set.
            ControlBox = false;

            _screenshotManager = screenshotManager;
            _ownerForm = ownerForm;
            _screenshotPreviewForm = new ScreenshotPreviewForm(this, _screenshotManager, _pauseLock);

        }

        //To be called from the test thread, will block until Continue is pressed
        public void Pause(string description = null, string fileToSave = null, string link = null, bool showMatchingPages = false, int? timeout = null,
            Control screenshotForm = null, bool fullScreen = false, Func<Bitmap, Bitmap> processShot = null)
        {
            _screenshotForm = screenshotForm;
            _fullScreen = fullScreen;
            _fileToSave = fileToSave;
            _processShot = processShot;
            _linkUrl = link;
            _showMatchingPage = showMatchingPages;
            _description = description;

            RunUI(this, RefreshViewState);
            lock (_pauseLock)
            {
                Monitor.Wait(_pauseLock, timeout ?? -1);
            }
        }

        //Allows for static creation of the form which uses the SkylineWindow as its parent and closes after continue
        public static void Pause(string description = null, string fileToSave = null, string link = null, bool showMatchingPages = false, int? timeout = null,
            Control screenshotForm = null, bool fullScreen = false, ScreenshotManager screenshotManager = null, Func<Bitmap, Bitmap> processShot = null)
        {
            ClipboardEx.UseInternalClipboard(false);
            if (SkylineWindow != null)
                RunUI(SkylineWindow, () => SkylineWindow.UseKeysOverride = false);

            Form parentWindow = FindSkylineWindow();
            RunUI(parentWindow, () =>
            {
                var pauseAndContinueForm = new PauseAndContinueForm(screenshotManager);
                pauseAndContinueForm.Pause(description, fileToSave, link, showMatchingPages, timeout,
                    screenshotForm, fullScreen, processShot);
                pauseAndContinueForm.Close();
            });

            ClipboardEx.UseInternalClipboard();
            if (SkylineWindow != null)
                RunUI(SkylineWindow, () => SkylineWindow.UseKeysOverride = true);
        }

        private static SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        private static Form FindSkylineWindow()
        {
            Form parentWindow = SkylineWindow;
            if (SkylineWindow == null)
                parentWindow = AbstractFunctionalTest.FindOpenForm<StartPage>();
            return parentWindow;
        }

        private static void RunUI(Form form, Action act)
        {
            form.Invoke(act);
        }

        public void SwitchToPauseAndContinue()
        {
            _currentMode = PauseAndContinueMode.PAUSE_AND_CONTINUE;
            _screenshotPreviewForm.Hide();
            Show(_ownerForm);
            FocusForm();
        }

        private void SwitchToPreview()
        {
            _currentMode = PauseAndContinueMode.PREVIEW_SCREENSHOT;
            Hide();
            _screenshotPreviewForm.UpdateViewState(_description, _screenshotForm, _fileToSave, _fullScreen, _processShot);
            _screenshotPreviewForm.Show(_ownerForm);
        }

        private void Continue()
        {
            Hide();

            // Start the tests again
            lock (_pauseLock)
            {
                Monitor.PulseAll(_pauseLock);
            }
        }

        private void GotoLink()
        {
            WebHelpers.OpenLink(_linkUrl);

            ActionUtil.RunAsync(() =>
            {
                Thread.Sleep(1000);
                FocusForm();
            });
        }

        private void FocusForm()
        {
            if (IsHandleCreated)
            {
                this.SetForegroundWindow();
                btnContinue.Focus();
            }
        }

        private bool CaptureScreenShot(bool save)
        {
            ScreenshotManager.ActivateScreenshotForm(_screenshotForm);

            if (save && FileEx.IsWriteLocked(_fileToSave))
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(string.Format("The file {0} is locked.", _fileToSave),
                    "Check that it is not open in another program such as TortoiseIDiff."));
                return false;
            }
            try
            {
                _screenshotManager.TakeShot(_screenshotForm, _fullScreen, save ? _fileToSave : null, _processShot);
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                return false;
            }
            FocusForm();
            return true;
        }

        private void saveScreenshotCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateScreenshotButtonLabels();
        }

        private void RefreshViewState()
        {

            if (!string.IsNullOrEmpty(_linkUrl))
            {
                lblDescriptionLink.Left = lblDescription.Left;
                lblDescription.Visible = false;
                lblDescriptionLink.Visible = true;
                if (string.IsNullOrEmpty(_description))
                    _description = "Show screenshot";
            }
            if (!string.IsNullOrEmpty(_description))
            {
                if (string.IsNullOrEmpty(_linkUrl))
                {
                    lblDescription.Text = _description;
                    toolTip1.SetToolTip(lblDescription, _description);
                }
                else
                {
                    lblDescriptionLink.Text = _description;
                    toolTip1.SetToolTip(lblDescriptionLink, _description);
                    lblDescriptionLink.TabStop = false;
                }
            }
            else
            {
                int delta = btnContinue.Top - lblDescription.Top;
                btnContinue.Top -= delta;
                Height -= delta;
                lblDescription.Visible = false;
            }

            // Finally make sure the button is fully visible
            Height += Math.Max(0, (btnContinue.Bottom + btnContinue.Left) - ClientRectangle.Bottom);

            if (_screenshotForm == null || _screenshotManager == null)
            {
                // Hide the screenshot buttons
                btnPreview.Visible =
                    btnScreenshot.Visible =
                        btnScreenshotAndContinue.Visible =
                            saveScreenshotCheckbox.Visible = false;

                Height -= saveScreenshotCheckbox.Bottom - btnContinue.Bottom;
            }

            UpdateScreenshotButtonLabels();
            PlaceForm();

            if (_currentMode == PauseAndContinueMode.PAUSE_AND_CONTINUE)
            {
                if(!Visible) Show(_ownerForm);
                FocusForm();

            } else if (_currentMode == PauseAndContinueMode.PREVIEW_SCREENSHOT)
            {
                _screenshotPreviewForm.UpdateViewState(_description, _screenshotForm, _fileToSave, _fullScreen, _processShot);
                _screenshotPreviewForm.Show(_ownerForm);
            }

            //Opacity was set to 0 in constructor
            Opacity = 1;
            if (_showMatchingPage) GotoLink();
        }

        private void PlaceForm()
        {
            const int spacing = 15;
            Form skylineWindow = FindSkylineWindow();
            var screen = (Screen)skylineWindow.Invoke(new Func<Screen>(() => Screen.FromControl(skylineWindow)));
            Left = skylineWindow.Left;
            if (skylineWindow.Top > screen.WorkingArea.Top + Height + spacing)
                Top = skylineWindow.Top - Height - spacing;
            else if (skylineWindow.Bottom + Height + spacing < screen.WorkingArea.Bottom)
                Top = skylineWindow.Bottom + spacing;
            else
            {
                Top = skylineWindow.Top;
                if (skylineWindow.Left > screen.WorkingArea.Top + Width + spacing)
                    Left = skylineWindow.Left - Width - spacing;
                else if (skylineWindow.Right + Width + spacing < screen.WorkingArea.Right)
                    Left = skylineWindow.Right + spacing;
                else
                {
                    // Can't fit on screen without overlap, so put in upper left of screen
                    // despite overlap
                    Top = screen.WorkingArea.Top;
                    Left = screen.WorkingArea.Left;
                }
            }
        }

        private void UpdateScreenshotButtonLabels()
        {
            btnScreenshot.Text = saveScreenshotCheckbox.Checked ? "Save &Screenshot" : "Take &Screenshot";
            btnScreenshotAndContinue.Text = saveScreenshotCheckbox.Checked ? "Save and C&ontinue" : "Take and C&ontinue";
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_screenshotPreviewForm != null && !_screenshotPreviewForm.IsDisposed)
            {
                _screenshotPreviewForm.Dispose();
            }
            base.OnFormClosed(e);
        }

        private void btnContinue_Click(object sender, EventArgs e)
        {
            Continue();
        }

        private void lblDescriptionLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GotoLink();
        }

        private void PauseAndContinueForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1)
                GotoLink();
        }

        private void btnScreenshot_Click(object sender, EventArgs e)
        {
            CaptureScreenShot(saveScreenshotCheckbox.Checked);
        }

        private void btnScreenshotAndContinue_Click(object sender, EventArgs e)
        {
            if (CaptureScreenShot(saveScreenshotCheckbox.Checked))
                Continue();
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            SwitchToPreview();
        }
    }
}
