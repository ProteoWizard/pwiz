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
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Properties;
using TestRunnerLib.PInvoke;

namespace pwiz.SkylineTestUtil
{
    public partial class PauseAndContinueForm : Form, IPauseTestController
    {
        private readonly Form _ownerForm;
        private readonly object _pauseLock = new object();

        private readonly ScreenshotManager _screenshotManager;

        // User display and screenshot file information
        private int _screenshotNum;
        private string _description;
        private string _linkUrl;
        private string _imageUrl;
        private string _fileToShow;
        private string _fileToSave;
        // Information for taking a screenshot
        private Control _screenshotForm;
        private bool _fullScreen;
        private Func<Bitmap, Bitmap> _processShot;

        private PauseAndContinueMode _currentMode;

        private ScreenshotPreviewForm _screenshotPreviewForm;

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

            _currentMode = screenshotManager != null && TestUtilSettings.Default.ShowPreview
                ? PauseAndContinueMode.PREVIEW_SCREENSHOT
                : PauseAndContinueMode.PAUSE_AND_CONTINUE;
        }

        /// <summary>
        /// Allows for static creation of the form for one time use without screenshot support.
        /// </summary>
        public static void Show(string description)
        {
            var instance = new PauseAndContinueForm(null);
            instance.ShowInternal(description);
            RunUI(instance, () => instance.Close());
        }

        /// <summary>
        /// Shows this form. Called from the "Functional test" thread. Blocks until Continue is clicked.
        /// </summary>
        public void Show(string description, int screenshotNum, int? timeout,
            Control screenshotForm, bool fullScreen, Func<Bitmap, Bitmap> processShot)
        {
            ShowInternal(_screenshotManager.ScreenshotDescription(screenshotNum, description),
                screenshotNum, timeout, screenshotForm, fullScreen, processShot);
        }

        private void ShowInternal(string description, int screenshotNum = 0, int? timeout = null,
            Control screenshotForm = null, bool fullScreen = false, Func<Bitmap, Bitmap> processShot = null)
        {
            _screenshotNum = screenshotNum;
            _description = description;
            _fileToShow = _screenshotManager?.ScreenshotSourceFile(screenshotNum);
            _fileToSave = _screenshotManager?.ScreenshotDestFile(screenshotNum);
            _linkUrl = _screenshotManager?.ScreenshotUrl(screenshotNum);
            _imageUrl = _screenshotManager?.ScreenshotImgUrl(screenshotNum);
            _screenshotForm = screenshotForm;
            _fullScreen = fullScreen;
            _processShot = processShot;

            // Allow full manual interaction with Skyline once in pause mode
            // Testing will have UseKeysOverride set to true to prevent accidental
            // keyboard interaction with the running test.
            RunUI(Program.MainWindow, () => Program.MainWindow.UseKeysOverride = false);
            ClipboardEx.UseInternalClipboard(false);
            lock (_pauseLock)
            {
                RunUI(_ownerForm, RefreshAndShow);
                Monitor.Wait(_pauseLock, timeout ?? -1);
            }
            ClipboardEx.UseInternalClipboard();
            RunUI(Program.MainWindow, () => Program.MainWindow.UseKeysOverride = true);

            // Record that the screenshot happened in the console output to make
            // it easier to set a starting screenshot in a subsequent test.
            Console.WriteLine(_description);
        }

        private static Form FindOwnerForm()
        {
            Form skylineWindow = Program.MainWindow;
            return skylineWindow != null ? skylineWindow : AbstractFunctionalTest.FindOpenForm<StartPage>();
        }

        private static void RunUI(Form form, Action act)
        {
            form.Invoke(act);
        }

        private void SwitchToPreview()
        {
            TestUtilSettings.Default.ShowPreview = true;
            TestUtilSettings.Default.Save();

            _currentMode = PauseAndContinueMode.PREVIEW_SCREENSHOT;

            EnsurePreviewForm();

            Hide();

            _screenshotPreviewForm.ShowOrUpdate();
        }

        private void EnsurePreviewForm()
        {
            // First ensure the HWND for this form is created by accessing its Handle
            var ensureHandle = Handle;

            _screenshotPreviewForm ??= new ScreenshotPreviewForm(this, _screenshotManager);
        }

        private void Continue()
        {
            TestUtilSettings.Default.Save();
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
            UpdateButtonVisibility();
            UpdateDescriptionLabels();
            PlaceForm();

            if (_currentMode == PauseAndContinueMode.PAUSE_AND_CONTINUE)
            {
                if (!Visible)
                    Show(_ownerForm);
                FocusForm();
            } 
            else if (_currentMode == PauseAndContinueMode.PREVIEW_SCREENSHOT)
            { 
                EnsurePreviewForm();

                _screenshotPreviewForm.ShowOrUpdate();
            }
        }

        private void UpdateButtonVisibility()
        {
            if (_screenshotForm == null || _screenshotManager == null)
            {
                // Hide the preview button
                btnPreview.Visible =  false;
                Height -=  btnPreview.Bottom - btnContinue.Bottom;
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
            // CONSIDER: Do this in OnClosing() instead?
            TestUtilSettings.Default.Save();

            if (_screenshotPreviewForm is { IsDisposed: false })
            {
                _screenshotPreviewForm.Invoke((Action) (() => _screenshotPreviewForm.Dispose()));
                _screenshotPreviewForm.WaitForCompletion();
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

        void IPauseTestController.Continue()
        {
            Invoke((Action)Continue);
        }

        void IPauseTestController.ShowPauseForm()
        {
            TestUtilSettings.Default.ShowPreview = false;
            TestUtilSettings.Default.Save();

            _currentMode = PauseAndContinueMode.PAUSE_AND_CONTINUE;
            Invoke((Action)(() =>
            {
                Show(_ownerForm);
                FocusForm();
            }));
        }

        public int ScreenshotNum => _screenshotNum;
        public string Description => _description;
        public string LinkUrl => _linkUrl;
        public string ImageUrl => _imageUrl;
        public string FileToShow => _fileToShow;
        public string FileToSave => _fileToSave;
        public Control ScreenshotControl => _screenshotForm;
        public bool FullScreen => _fullScreen;
        public Func<Bitmap, Bitmap> ProcessShot => _processShot;
    }
}
