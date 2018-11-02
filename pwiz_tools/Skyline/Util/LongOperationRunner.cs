/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Wrapper around LongWaitDlg which allows running cancellable actions on the event thread.
    /// The LongWaitDlg gets created and displayed on a background thread.
    /// </summary>
    public class LongOperationRunner
    {
        private static int DEFAULT_DELAY_MILLIS = 1000;
        public LongOperationRunner()
        {
            DelayMillis = DEFAULT_DELAY_MILLIS;
        }
        public Control ParentControl { get; set; }
        public string JobTitle { get; set; }
        public int DelayMillis { get; set; }
        public bool ExecutesJobOnBackgroundThread { get; set; }

        public void Run(Action<ILongWaitBroker> action)
        {
            if (ExecutesJobOnBackgroundThread)
            {
                RunOnBackgroundThread(action);
            }
            RunOnThisThread(action);
        }

        public T CallFunction<T>(Func<ILongWaitBroker, T> func)
        {
            T returnValue = default(T);
            Run(progressMonitor =>
            {
                returnValue = func(progressMonitor);
            });
            return returnValue;
        }
        
        private void RunOnThisThread(Action<ILongWaitBroker> performWork)
        {
            LongWaitDlg longWaitDlg = null;
            ProgressWaitBroker progressWaitBroker;
            AutoResetEvent dlgCreated = new AutoResetEvent(false);
            AutoResetEvent workFinished = new AutoResetEvent(false);
            Thread monitoringThread = BackgroundEventThreads.CreateThreadForAction(() =>
            {
                var dlgCreatedEvent = dlgCreated;
                try
                {
                    using (longWaitDlg = new BackgroundThreadLongWaitDlg())
                    {
                        InitLongWaitDlg(longWaitDlg);
                        longWaitDlg.ShowInTaskbar = true;
                        if (ParentControl != null)
                        {
                            var parentBounds = ParentControl.Bounds;
                            longWaitDlg.StartPosition = FormStartPosition.Manual;
                            longWaitDlg.Top = (parentBounds.Top + parentBounds.Bottom - longWaitDlg.Height)/2;
                            longWaitDlg.Left = (parentBounds.Left + parentBounds.Right - longWaitDlg.Width)/2;
                        }
                        progressWaitBroker = new ProgressWaitBroker(lwb =>
                        {
                            workFinished.WaitOne();
                            workFinished.Dispose();
                        });
                        dlgCreatedEvent.Set();
                        dlgCreatedEvent = null;
                        longWaitDlg.PerformWork(null, DelayMillis, progressWaitBroker.PerformWork);
                    }
                }
                finally
                {
                    if (dlgCreatedEvent != null)
                        dlgCreated.Set();
                }
            });
            monitoringThread.Name = "LongOperationRunnerBackgroundThread"; // Not L10N
            monitoringThread.Start();
            dlgCreated.WaitOne();
            dlgCreated.Dispose();
            try
            {
                performWork(longWaitDlg);
            }
            finally
            {
                workFinished.Set();
            }
        }

        private void RunOnBackgroundThread(Action<ILongWaitBroker> action)
        {
            using (var longWaitDlg = new LongWaitDlg())
            {
                InitLongWaitDlg(longWaitDlg);
                longWaitDlg.PerformWork(ParentControl, DelayMillis, action);
            }
        }

        private void InitLongWaitDlg(LongWaitDlg longWaitDlg)
        {
            if (JobTitle != null)
            {
                longWaitDlg.Text = JobTitle;
            }
        }

        protected class BackgroundThreadLongWaitDlg : LongWaitDlg
        {
            private readonly TaskbarProgress _taskbarProgress = new TaskbarProgress();

            public BackgroundThreadLongWaitDlg()
            {
                ShowInTaskbar = true;
            }

            protected override void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
            {
                _taskbarProgress.SetState(Handle, state);
                if (percentComplete.HasValue)
                {
                    _taskbarProgress.SetValue(Handle, percentComplete.Value, 100);
                }
            }
        }
    }
}
