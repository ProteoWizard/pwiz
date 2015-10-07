/*
 * Original author: Tahmina Baker <tabaker .at. u.washington.edu>,
 *                  UWPR, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Timer=System.Windows.Forms.Timer;

namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryNotification : FormEx
    {
        private const int ANIMATION_DURATION = 1000;
        private const int DISPLAY_DURATION = 10000;

        private readonly FormAnimator _animator;
        private readonly Timer _displayTimer;
        private readonly String _libraryName;

        public event EventHandler<ExploreLibraryEventArgs> ExploreLibrary;
        public event EventHandler NotificationComplete;

        public BuildLibraryNotification(String libraryName)
        {
            InitializeComponent();

            _libraryName = libraryName;
            LibraryNameLabel.Text = string.Format(Resources.BuildLibraryNotification_BuildLibraryNotification_Library__0__, _libraryName);

            var showParams = new FormAnimator.AnimationParams(
                                    FormAnimator.AnimationMethod.slide, 
                                    FormAnimator.AnimationDirection.up, 
                                    0);
            var hideParams = new FormAnimator.AnimationParams(
                                    FormAnimator.AnimationMethod.blend, 
                                    FormAnimator.AnimationDirection.up, 
                                    0);
            _animator = new FormAnimator(this, showParams, hideParams)
                {ShowParams = {Duration = ANIMATION_DURATION}};

            // Not sure why this is necessary, but sometimes the form doesn't
            // appear without it.
            Opacity = 1;

            _displayTimer = new Timer();
            _displayTimer.Tick += OnDisplayTimerEvent;
            _displayTimer.Interval = DISPLAY_DURATION;
        }

        /// <summary>
        /// Does not work with the way this form gets shown, but it is
        /// here to prove it was tried.
        /// </summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void Notify()
        {
            LocalizationHelper.InitThread();

            // Start the timer that will count how long to display it
            _displayTimer.Start();

            // Start the message pump
            // This call returns when ExitThread is called
            Application.Run(this);
        }

        public void Remove()
        {
            if (IsHandleCreated)
            {
                try
                {
                    // Make sure this happens on the right thread.
                    BeginInvoke((Action) OnRemove);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void OnRemove()
        {
            _displayTimer.Stop();
            _animator.Release();
            Close();
            Dispose();
        }

        private void CloseNotification(bool animate)
        {
            _displayTimer.Stop();
            _animator.HideParams.Duration = animate ? ANIMATION_DURATION : 0;
            Hide();
            
            if (NotificationComplete != null)
                NotificationComplete.Invoke(this, new EventArgs());
        }

        private void OnDisplayTimerEvent(object sender, EventArgs e)
        {
            CloseNotification(true);
        }

        private void NotificationCloseButton_Click(object sender, EventArgs e)
        {
            CloseNotification(false);
        }

        private void ViewLibraryLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            CloseNotification(false);
            if (ExploreLibrary != null)
                ExploreLibrary.Invoke(this, new ExploreLibraryEventArgs(_libraryName));
        }
    }

    public sealed class ExploreLibraryEventArgs : EventArgs
    {
        public ExploreLibraryEventArgs(string libraryName)
        {
            LibraryName = libraryName;
        }

        public string LibraryName { get; private set; }
    }

    public interface INotificationContainer
    {
        Point NotificationAnchor { get; }
    }

    public interface ILibraryBuildNotificationContainer : INotificationContainer
    {
        LibraryManager LibraryManager { get; }
    }

    public sealed class LibraryBuildNotificationHandler
    {
        private const int PADDING = 8;

        public LibraryBuildNotificationHandler(Form notificationContainer)
        {
            notificationContainer.Closing += notificationContainerForm_Closing;
            notificationContainer.Move += notifactionContainerForm_Move;
            NotificationContainerForm = notificationContainer;
            NotificationContainer = (ILibraryBuildNotificationContainer) notificationContainer;
        }

        private Form NotificationContainerForm { get; set; }
        private ILibraryBuildNotificationContainer NotificationContainer { get; set; }

        private BuildLibraryNotification Notification { get; set; }

        private Point NotificationAnchor
        {
            get
            {
                Point anchor = NotificationContainer.NotificationAnchor;
                anchor.X += PADDING;
                anchor.Y -= PADDING;
                return anchor;
            }
        }

        private void InvokeAction(Action action)
        {
            // Make sure the notification container form has not already been
            // destroyed on its own thread, before trying to post a message to the
            // thread.
            try
            {
                NotificationContainerForm.Invoke(action);
            }
            catch (ObjectDisposedException)
            {
                // The main window may close during an attempt to activate it,
                // and cause this exception.  Hard to figure out anything to do
                // but catch and ignore it.  Would be lots nicer, if it were
                // possible to show a .NET form without it activating itself.
                // Again, using NativeWindow is too much for this feature right now.
                // It does not help to test IsDisposed before calling Invoke.
            }
        }

        private void notification_ExploreLibrary(object sender, ExploreLibraryEventArgs e)
        {
            InvokeAction(() => ShowViewLibraryUI(e.LibraryName));
        }

        private void ShowViewLibraryUI(String libName)
        {
            var indexPepSetUI = Program.MainWindow.OwnedForms.IndexOf(form => form is PeptideSettingsUI);
            if (indexPepSetUI != -1)
            {
                ((PeptideSettingsUI)Program.MainWindow.OwnedForms[indexPepSetUI]).ShowViewLibraryDlg(libName);
            }          
            var indexViewLibDlg = Program.MainWindow.OwnedForms.IndexOf(form => form is ViewLibraryDlg);
            if (indexViewLibDlg == -1)
            {
                var dlg = new ViewLibraryDlg(NotificationContainer.LibraryManager, libName, Program.MainWindow)
                              {Owner = Program.MainWindow};
                dlg.Show();
            }
            else
            {
                ViewLibraryDlg viewLibDlg = (ViewLibraryDlg) Program.MainWindow.OwnedForms[indexViewLibDlg];
                viewLibDlg.Activate();
                viewLibDlg.ChangeSelectedLibrary(libName);
            }
        }

        private void notification_Activated(object sender, EventArgs e)
        {
            // Ugh. The library build notification form will activate itself.
            // To do better, we would have to use a NativeWindow for the notification,
            // like CustomTip, but that is just too much work for this.  So,
            // we just do our best to return activation to the topmost open form.
            InvokeAction(TopMostApplicationForm.Activate);
        }

        private void notification_Shown(object sender, EventArgs e)
        {
            Form form = (Form) sender;
            // If the application is not active when the form is shown, then the form
            // can end up underneath the application window.  This hack fixes that issue.
            form.TopMost = true;

            // Remove the activation hook, since it can cause problems after this.
            form.Activated -= notification_Activated;
        }

        private Form TopMostApplicationForm
        {
            get
            {
                for (int i = Application.OpenForms.Count - 1; i >= 0; i--)
                {
                    Form form = Application.OpenForms[i];
                    if (form is BuildLibraryNotification)
                        continue;
                    return form;
                }
                // Should never happen, but to be safe at least return this
                // Skyline window.
                return NotificationContainerForm;
            }
        }

        private void notification_NotificationComplete(object sender, EventArgs e)
        {
            RemoveLibraryBuildNotification();
        }

        private void notifactionContainerForm_Move(object sender, EventArgs e)
        {
            RemoveLibraryBuildNotification();
        }

        private void notificationContainerForm_Closing(object sender, CancelEventArgs e)
        {
            RemoveLibraryBuildNotification();
        }

        public void RemoveLibraryBuildNotification()
        {
            lock (this)
            {
                if (Notification != null)
                {
                    Notification.Shown -= notification_Shown;
                    Notification.Activated -= notification_Activated;
                    Notification.Remove();
                    Notification = null;
                }
            }
        }

        public void LibraryBuildCompleteCallback(IAsyncResult ar)
        {
            var buildState = (LibraryManager.BuildState)ar.AsyncState;
            bool success = buildState.BuildFunc.EndInvoke(ar);

            if (success)
            {
                lock (this)
                {
                    RemoveLibraryBuildNotification();

                    var frm = new BuildLibraryNotification(buildState.LibrarySpec.Name);
                    frm.Activated += notification_Activated;
                    frm.Shown += notification_Shown;
                    frm.ExploreLibrary += notification_ExploreLibrary;
                    frm.NotificationComplete += notification_NotificationComplete;
                    Point anchor = NotificationAnchor;
                    frm.Left = anchor.X;
                    frm.Top = anchor.Y - frm.Height;
                    if (!string.IsNullOrEmpty(buildState.ExtraMessage))
                    {
                        NotificationContainerForm.BeginInvoke(new Action(() => MessageDlg.Show(NotificationContainerForm, buildState.ExtraMessage)));
                    }

                    Thread th = new Thread(frm.Notify) { Name = "BuildLibraryNotification", IsBackground = true }; // Not L10N
                    th.SetApartmentState(ApartmentState.STA);
                    th.Start();

                    Notification = frm;
                }
            }
        }
    }
}
