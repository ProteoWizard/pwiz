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
                return;
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
            monitoringThread.Name = @"LongOperationRunnerBackgroundThread";
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
            public BackgroundThreadLongWaitDlg()
            {
                ShowInTaskbar = true;
            }

            /// <summary>
            /// Deliberately shows no taskbar progress, unlike every other <see cref="LongWaitDlg"/>.
            ///
            /// <para>This dialog runs its own message loop on a thread of its own, and that thread exits when the
            /// dialog closes. Owning a <see cref="TaskbarProgress"/> here (which is what this used to do) therefore
            /// creates the ITaskbarList3 COM object on a short-lived STA thread, and tearing that apartment down
            /// leaks roughly 400 bytes of native heap every time -- measured as dead-linear over hundreds of
            /// iterations, and NOT fixed by releasing the object first, because it is the apartment rather than the
            /// reference that is at fault.</para>
            ///
            /// <para>The base implementation cannot be used instead: it drives the MAIN window's taskbar progress,
            /// which would mean touching Program.MainWindow from this thread, and the main thread is blocked inside
            /// the operation this dialog is reporting on -- that is the whole reason the dialog is on its own thread
            /// -- so marshaling to it would deadlock.</para>
            ///
            /// <para>That leaves not showing it. This dialog only appears for the rare long operation started on the
            /// UI thread, and a progress overlay on its taskbar button is cosmetic; the dialog itself still shows the
            /// progress bar.</para>
            /// </summary>
            protected override void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
            {
            }
        }
    }
}
