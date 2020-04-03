/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    public partial class LongWaitDlg : FormEx, ILongWaitBroker
    {
        private readonly string _cancelMessage = string.Format(@" ({0})", Resources.LongWaitDlg_PerformWork_canceled);

        private const int MAX_HEIGHT = 500;
        private readonly int _originalFormHeight;
        private readonly int _originalMessageHeight;
        private Control _parentForm;
        private Exception _exception;
        private int _progressValue = -1;
        private string _message;
        private DateTime _startTime;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private ManualResetEvent _completionEvent;

        // these members should only be accessed in a block which locks on _lock
        #region synchronized members
        private readonly object _lock = new object();
        private bool _finished;
        private bool _windowShown;
        #endregion

        /// <summary>
        /// For operations where a change in the active document should
        /// cause the operation to fail.
        /// </summary>
        private readonly IDocumentContainer _documentContainer;

        public LongWaitDlg(IDocumentContainer documentContainer = null, bool allowCancel = true)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _documentContainer = documentContainer;

            btnCancel.Visible = btnCancel.Enabled = IsCancellable = allowCancel;

            if (!IsCancellable)
                Height -= Height - btnCancel.Bottom;
            _originalFormHeight = Height;
            _originalMessageHeight = labelMessage.Height;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public string Message
        {
            get { return _message; }
            set { _message = value; }
        }

        public int ProgressValue
        {
            get { return _progressValue; }
            set
            {
                Assume.IsTrue(value <= 100);
                _progressValue = value;
            }
        }

        public override string DetailedMessage
        {
            get { return string.Format(@"[{0}] {1} ({2}%)", Text, Message, ProgressValue); }
        }

        public bool IsCancellable { get; private set; }

        public void EnableCancelOption(bool enable)
        {
            // Work is done, but it's still nice to have the progress indicator visible for context while any final 
            // steps take place - but it's wrong to offer a cancellation option, so grey it out
            if (btnCancel != null && btnCancel.IsHandleCreated)
            {
                btnCancel.Invoke((Action) (() => btnCancel.Enabled = enable)); 
            }
        }

        public bool IsDocumentChanged(SrmDocument docOrig)
        {
            return _documentContainer != null && !ReferenceEquals(docOrig, _documentContainer.Document);
        }

        public DialogResult ShowDialog(Func<IWin32Window, DialogResult> show)
        {
            // If the window handle is created, show the message in its thread,
            // parented to it.  Otherwise, use the intended parent of this form.
            var parent = (IsHandleCreated ? this : _parentForm);
            DialogResult result = DialogResult.OK;
            parent.Invoke((Action) (() => result = show(parent)));
            return result;
        }

        public void SetProgressCheckCancel(int step, int totalSteps)
        {
            if (IsCanceled)
                throw new OperationCanceledException();
            ProgressValue = 100 * step / totalSteps;
        }

        public void PerformWork(Control parent, int delayMillis, Action performWork)
        {
            var indefiniteWaitBroker = new IndefiniteWaitBroker(performWork);
            PerformWork(parent, delayMillis, indefiniteWaitBroker.PerformWork);
        }

        public IProgressStatus PerformWork(Control parent, int delayMillis, Action<IProgressMonitor> performWork)
        {
            var progressWaitBroker = new ProgressWaitBroker(performWork);
            PerformWork(parent, delayMillis, progressWaitBroker.PerformWork);
            if (progressWaitBroker.IsCanceled)
                return progressWaitBroker.Status.Cancel();
            return progressWaitBroker.Status;
        }

        public void PerformWork(Control parent, int delayMillis, Action<ILongWaitBroker> performWork)
        {
            _startTime = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta
            _parentForm = parent;
            ManualResetEvent completionEvent = null;
            try
            {
                lock (this)
                {
                    Assume.IsNull(_completionEvent);
                    _completionEvent = completionEvent = new ManualResetEvent(false);
                }
//                Action<Action<ILongWaitBroker>> runner = RunWork;
//                _result = runner.BeginInvoke(performWork, runner.EndInvoke, null);
                ActionUtil.RunAsync(() => RunWork(performWork));

                // Wait as long as the caller wants before showing the progress
                // animation to the user.
//                _result.AsyncWaitHandle.WaitOne(delayMillis);

                // Return without notifying the user, if the operation completed
                // before the wait expired.
//                if (_result.IsCompleted)
                if (completionEvent.WaitOne(delayMillis))
                    return;

                progressBar.Value = Math.Max(0, _progressValue);
                UpdateLabelMessage();

                ShowDialog(parent);
            }
            finally
            {
                var x = _exception;

                // Get rid of this window before leaving this function
                Dispose();
                lock (this)
                {
                    if (completionEvent != null)
                    {
                        _completionEvent = null;
                    }
                }
                completionEvent?.Dispose();

                if (IsCanceled && null != x)
                {
                    if (x is OperationCanceledException || x.InnerException is OperationCanceledException)
                    {
                        x = null;
                    }
                }

                if (x != null)
                {
                    Helpers.WrapAndThrowException(x);
                }
            }
        }

        /// <summary>
        /// When this dialog is shown, it is necessary to check whether the background job has completed.
        /// If it has, then this dialog needs to be closed right now.
        /// If the background job has not yet completed, then this dialog will be closed by the finally
        /// block in <see cref="RunWork"/>.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            lock (_lock)
            {
                if (_finished)
                {
                    Close();
                }
                else
                {
                    _windowShown = true;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            lock (_lock)
            {
                if (!_finished)
                {
                    // If the user is trying to close this form, then treat it the 
                    // same as if they had hit "Cancel".
                    OnClickedCancel();
                    e.Cancel = true;
                    return;
                }
                _windowShown = false;
            }
            UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.NoProgress, null);
            base.OnFormClosing(e);
        }


        private void RunWork(Action<ILongWaitBroker> performWork)
        {
            try
            {
                // Called in a UI thread
                LocalizationHelper.InitThread();
                performWork(this);
            }
            catch (Exception x)
            {
                _exception = x;
            }
            finally
            {
                lock (_lock)
                {
                    _finished = true;
                    if (_windowShown)
                    {
                        BeginInvoke(new Action(FinishDialog));
                    }
                }

                lock (this)
                {
                    _completionEvent?.Set();
                }
            }
        }

        private void FinishDialog()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                var runningTime = DateTime.UtcNow.Subtract(_startTime);
                // Show complete status before returning.
                progressBar.Value = _progressValue = 100;
                UpdateLabelMessage();
                // Display the final complete status for one second, or 10% of the time the job ran for,
                // whichever is shorter
                int finalDelayTime = Math.Min(1000, (int) (runningTime.TotalMilliseconds/10));
                if (finalDelayTime > 0)
                {
                    timerClose.Interval = finalDelayTime;
                    timerClose.Enabled = true;
                    return;
                }
            }
            Close();
        }

        public bool IsCanceled
        {
            get { return _cancellationTokenSource.IsCancellationRequested; }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            OnClickedCancel();
        }

        private void OnClickedCancel()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                labelMessage.Text += _cancelMessage;
            }
        }

        private void timerUpdate_Tick(object sender, EventArgs e)
        {
            var progressValue = _progressValue;
            if (progressValue == -1)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Indeterminate, null);
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = progressValue;
                UpdateTaskbarProgress(TaskbarProgress.TaskbarStates.Normal, progressBar.Value);
            }

            UpdateLabelMessage();
        }

        private void UpdateLabelMessage()
        {
            if (_message == null)
            {
                return;
            }

            string newMessage = _message +
                                (_cancellationTokenSource.IsCancellationRequested ? _cancelMessage : string.Empty);
            if (Equals(newMessage, labelMessage.Text))
            {
                return;
            }

            labelMessage.Text = newMessage;
            int formGrowth = Math.Max(labelMessage.Height - _originalMessageHeight, 0);
            int newHeight = _originalFormHeight + Math.Min(formGrowth, MAX_HEIGHT);
            if (newHeight > Height)
            {
                Height = _originalFormHeight + formGrowth;
            }
        }

        protected virtual void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
        {
            if (Program.MainWindow != null)
                Program.MainWindow.UpdateTaskbarProgress(state, percentComplete);
            else if (Program.StartWindow != null)
                Program.StartWindow.UpdateTaskbarProgress(state, percentComplete);
        }

        private void timerClose_Tick(object sender, EventArgs e)
        {
            Close();
        }

        public CancellationToken CancellationToken { get { return _cancellationTokenSource.Token; } }

        private sealed class IndefiniteWaitBroker
        {
            private readonly Action _performWork;

            public IndefiniteWaitBroker(Action performWork)
            {
                _performWork = performWork;
            }

            public void PerformWork(ILongWaitBroker broker)
            {
                _performWork();
            }
        }

    }
}
