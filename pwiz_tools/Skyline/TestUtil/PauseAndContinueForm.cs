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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : Form
    {
        private readonly Thread _screenshotPreviewThread;
        private readonly ScreenshotPreviewForm _screenshotPreviewForm;
        private readonly ScreenshotManager _screenshotManager;
        private readonly Form _ownerForm;
        private readonly object _pauseLock = new object();
        private readonly ManualResetEvent _screenshotPreviewHandleReadyEvent = new ManualResetEvent(false);

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

        public PauseAndContinueForm(ScreenshotManager screenshotManager)
        {
            InitializeComponent();
            // A strange ordering bug in calculating the size of the boarder makes it necessary to remove
            // the ControlBox after the FormBorderStyle is set.
            ControlBox = false;

            _screenshotManager = screenshotManager;
            _ownerForm = FindOwnerForm();
            _screenshotPreviewForm = new ScreenshotPreviewForm(this, _screenshotManager, _pauseLock);
            _screenshotPreviewThread = new Thread(StartApplicationThread);
        }

        //To be called from the test thread, will block until Continue is pressed
        public void Show(string description, string fileToSave = null, string link = null, bool showMatchingPages = false, int? timeout = null,
            Control screenshotForm = null, bool fullScreen = false, Func<Bitmap, Bitmap> processShot = null)
        {
            _screenshotForm = screenshotForm;
            _fullScreen = fullScreen;
            _fileToSave = fileToSave;
            _processShot = processShot;
            _linkUrl = link;
            _showMatchingPage = showMatchingPages;
            _description = description;

            //RunUI(SkylineWindow, () => SkylineWindow.UseKeysOverride = false); //determine if this is needed
            lock (_pauseLock)
            {
                RunUI(_ownerForm, RefreshAndShow);
                Monitor.Wait(_pauseLock, timeout ?? -1);
            }
            //RunUI(SkylineWindow, () => SkylineWindow.UseKeysOverride = true);
        }

        //Allows for static creation of the form for one time use
        public static void Show(string description, string fileToSave = null, string link = null, bool showMatchingPages = false, int? timeout = null,
            Control screenshotForm = null, bool fullScreen = false, ScreenshotManager screenshotManager = null, Func<Bitmap, Bitmap> processShot = null)
        {
            var pauseAndContinueForm = new PauseAndContinueForm(screenshotManager);
            pauseAndContinueForm.Show(description, fileToSave, link, showMatchingPages, timeout, screenshotForm, fullScreen, processShot);
            RunUI(pauseAndContinueForm, () => pauseAndContinueForm.Close());
        }

        private static Form FindOwnerForm()
        {
            Form skylineWindow = Program.MainWindow;
            return skylineWindow != null ? skylineWindow : AbstractFunctionalTest.FindOpenForm<StartPage>();
        }

        private void StartApplicationThread()
        {
            _screenshotPreviewForm.HandleCreated += (sender, args) => _screenshotPreviewHandleReadyEvent.Set();
            Application.Run(_screenshotPreviewForm);
        }

        private static void RunUI(Form form, Action act)
        {
            form.Invoke(act);
        }

        public void SwitchToPauseAndContinue()
        {
            _currentMode = PauseAndContinueMode.PAUSE_AND_CONTINUE;
            _screenshotPreviewForm.Invoke((Action) (() => _screenshotPreviewForm.Hide()));
            Show(_ownerForm);
            FocusForm();
        }

        private void SwitchToPreview()
        {
            _currentMode = PauseAndContinueMode.PREVIEW_SCREENSHOT;
            if (!_screenshotPreviewThread.IsAlive)
            {
                _screenshotPreviewThread.SetApartmentState(ApartmentState.STA);
                _screenshotPreviewThread.Start();
                _screenshotPreviewHandleReadyEvent.WaitOne(); //blocks momentarily for the screenshot preview handle to be ready
            }

            Hide();
            _screenshotPreviewForm.Show(_description, _linkUrl, _screenshotForm, _fileToSave, _fullScreen, _processShot, false);
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

        private void RefreshAndShow()
        {
            UpdateDescriptionLabels();
            UpdateButtonVisibility();
            PlaceForm();

            if (_currentMode == PauseAndContinueMode.PAUSE_AND_CONTINUE)
            {
                if(!Visible) Show(_ownerForm);
                FocusForm();

            } else if (_currentMode == PauseAndContinueMode.PREVIEW_SCREENSHOT)
            { 
                _screenshotPreviewForm.Show(_description, _linkUrl, _screenshotForm, _fileToSave, _fullScreen, _processShot, true);
            }

            if (_showMatchingPage) GotoLink();
        }

        private void UpdateButtonVisibility()
        {
            if (_screenshotForm == null || _screenshotManager == null)
            {
                // Hide the preview button
                btnPreview.Visible =  false;
                Height -=  btnContinue.Bottom;
            }
        }

        private void UpdateDescriptionLabels()
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
        }

        private void PlaceForm()
        {
            const int spacing = 15;
            Form targetWindow = _ownerForm;
            var screen = (Screen)targetWindow.Invoke(new Func<Screen>(() => Screen.FromControl(targetWindow)));
            Left = targetWindow.Left;
            if (targetWindow.Top > screen.WorkingArea.Top + Height + spacing)
                Top = targetWindow.Top - Height - spacing;
            else if (targetWindow.Bottom + Height + spacing < screen.WorkingArea.Bottom)
                Top = targetWindow.Bottom + spacing;
            else
            {
                Top = targetWindow.Top;
                if (targetWindow.Left > screen.WorkingArea.Top + Width + spacing)
                    Left = targetWindow.Left - Width - spacing;
                else if (targetWindow.Right + Width + spacing < screen.WorkingArea.Right)
                    Left = targetWindow.Right + spacing;
                else
                {
                    // Can't fit on screen without overlap, so put in upper left of screen
                    // despite overlap
                    Top = screen.WorkingArea.Top;
                    Left = screen.WorkingArea.Left;
                }
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }    // Don't take activation away from SkylineWindow
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_screenshotPreviewForm != null && !_screenshotPreviewForm.IsDisposed)
            {
                _screenshotPreviewForm.Invoke((Action) (() => _screenshotPreviewForm.Dispose()));
            }

            if (_screenshotPreviewThread.IsAlive)
            {
                _screenshotPreviewThread.Join();
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

        private void btnPreview_Click(object sender, EventArgs e)
        {
            SwitchToPreview();
        }
    }
}
