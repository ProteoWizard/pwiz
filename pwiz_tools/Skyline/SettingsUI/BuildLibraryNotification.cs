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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Timer=System.Windows.Forms.Timer;

namespace pwiz.Skyline.SettingsUI
{
    public partial class BuildLibraryNotification : FormEx
    {
        private const int ANIMATION_DURATION = 1000;
        private const int DISPLAY_DURATION = 10000;

        private readonly Thread _thread;
        private readonly FormAnimator _animator;
        private readonly Timer _displayTimer;
        private readonly String _libraryName;

        public event EventHandler<ExploreLibraryEventArgs> ExploreLibrary;
        public event EventHandler NotificationComplete;

        public BuildLibraryNotification(String libraryName)
        {
            InitializeComponent();

            // WINDOWS 10 UPDATE HACK: Because Windows 10 update version 1803 causes unparented non-ShowInTaskbar windows to leak GDI and User handles
            ShowInTaskbar = Program.FunctionalTest;

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

            _thread = BackgroundEventThreads.CreateThreadForAction(Notify);
            _thread.Name = "BuildLibraryNotification"; // Not L10N
            _thread.IsBackground = true;

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

        public void Start()
        {
            Assume.IsFalse(_thread.IsAlive);    // Called only once

            _thread.Start();
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

                    _thread.Join(); // Wait for the thread to complete
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
        void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act, Func<SrmDocument, SrmDocument, AuditLogEntry> logFunc);
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
                dlg.Show(Program.MainWindow);
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
                return FormUtil.FindTopLevelOpenForm(f => f is BuildLibraryNotification) ??
                    NotificationContainerForm;
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

            if (success && NotificationContainerForm.IsHandleCreated)
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
                    NotificationContainerForm.BeginInvoke(new Action(() =>
                    {
                        if (!string.IsNullOrEmpty(buildState.ExtraMessage))
                        {
                            MessageDlg.Show(TopMostApplicationForm, buildState.ExtraMessage);
                        }
                        if (buildState.IrtStandard != null && buildState.IrtStandard != IrtStandard.NULL && AddIrts(buildState))
                        {
                            AddRetentionTimePredictor(buildState);
                        }
                    }));
                    frm.Start();
                    Notification = frm;
                }
            }
        }

        private bool AddIrts(LibraryManager.BuildState buildState)
        {
            try
            {
                Library lib;
                ProcessedIrtAverages processed = null;
                var initialMessage = Resources.LibraryBuildNotificationHandler_LibraryBuildCompleteCallback_Adding_iRTs_to_library;
                using (var longWait = new LongWaitDlg { Text = Resources.LibraryBuildNotificationHandler_LibraryBuildCompleteCallback_Adding_iRTs_to_library })
                {
                    var status = longWait.PerformWork(TopMostApplicationForm, 800, monitor =>
                    {
                        var initStatus = new ProgressStatus(initialMessage).ChangeSegments(0, 2);
                        monitor.UpdateProgress(initStatus);
                        lib = NotificationContainer.LibraryManager.TryGetLibrary(buildState.LibrarySpec) ??
                              NotificationContainer.LibraryManager.LoadLibrary(buildState.LibrarySpec, () => new DefaultFileLoadMonitor(monitor));
                        foreach (var stream in lib.ReadStreams)
                            stream.CloseStream();
                        if (longWait.IsCanceled)
                            return;
                        var irtProviders = lib.RetentionTimeProvidersIrt.ToArray();
                        if (!irtProviders.Any())
                            irtProviders = lib.RetentionTimeProviders.ToArray();
                        processed = RCalcIrt.ProcessRetentionTimes(monitor, irtProviders, irtProviders.Length,
                                                                   buildState.IrtStandard.Peptides.ToArray(), new DbIrtPeptide[0]);
                    });
                    if (status.IsCanceled)
                        return false;
                    if (status.IsError)
                        throw status.ErrorException;
                }

                using (var resultsDlg = new AddIrtPeptidesDlg(AddIrtPeptidesLocation.spectral_library, processed))
                {
                    if (resultsDlg.ShowDialog(TopMostApplicationForm) != DialogResult.OK)
                        return false;
                }

                var recalibrate = false;
                if (processed.CanRecalibrateStandards(buildState.IrtStandard.Peptides))
                {
                    using (var dlg = new MultiButtonMsgDlg(
                        TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_,
                            Resources.LibraryGridViewDriver_AddToLibrary_This_can_improve_retention_time_alignment_under_stable_chromatographic_conditions_),
                        MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                    {
                        if (dlg.ShowDialog(TopMostApplicationForm) == DialogResult.Yes)
                            recalibrate = true;
                    }
                }

                var processedDbIrtPeptides = processed.DbIrtPeptides.ToArray();
                if (!processedDbIrtPeptides.Any())
                    return false;

                using (var longWait = new LongWaitDlg {Text = Resources.LibraryBuildNotificationHandler_LibraryBuildCompleteCallback_Adding_iRTs_to_library})
                {
                    ImmutableList<DbIrtPeptide> newStandards = null;
                    var status = longWait.PerformWork(TopMostApplicationForm, 800, monitor =>
                    {
                        if (recalibrate)
                        {
                            monitor.UpdateProgress(new ProgressStatus().ChangeSegments(0, 2));
                            newStandards = ImmutableList.ValueOf(processed.RecalibrateStandards(buildState.IrtStandard.Peptides));
                            processed = RCalcIrt.ProcessRetentionTimes(
                                monitor, processed.ProviderData.Select(data => data.Value.RetentionTimeProvider),
                                processed.ProviderData.Count, newStandards.ToArray(), new DbIrtPeptide[0]);
                        }
                        var irtDb = IrtDb.CreateIrtDb(buildState.LibrarySpec.FilePath);
                        irtDb.AddPeptides(monitor, (newStandards ?? buildState.IrtStandard.Peptides).Concat(processedDbIrtPeptides).ToList());
                    });
                    if (status.IsError)
                        throw status.ErrorException;
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(TopMostApplicationForm,
                    TextUtil.LineSeparate(Resources.LibraryBuildNotificationHandler_LibraryBuildCompleteCallback_An_error_occurred_trying_to_add_iRTs_to_the_library_, x.Message), x);
                return false;
            }
            return true;
        }

        private void AddRetentionTimePredictor(LibraryManager.BuildState buildState)
        {
            var predictorName = Helpers.GetUniqueName(buildState.LibrarySpec.Name, Settings.Default.RetentionTimeList.Select(rt => rt.Name).ToArray());
            using (var addPredictorDlg = new AddRetentionTimePredictorDlg(predictorName, buildState.LibrarySpec.FilePath, false))
            {
                if (addPredictorDlg.ShowDialog(TopMostApplicationForm) == DialogResult.OK)
                {
                    Settings.Default.RTScoreCalculatorList.Add(addPredictorDlg.Calculator);
                    Settings.Default.RetentionTimeList.Add(addPredictorDlg.Regression);
                    NotificationContainer.ModifyDocument(Resources.LibraryBuildNotificationHandler_AddRetentionTimePredictor_Add_retention_time_predictor,
                        doc => doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(predict =>
                            predict.ChangeRetentionTime(addPredictorDlg.Regression))), SkylineWindow.SettingsLogFunction);
                }
            }
        }
    }
}
