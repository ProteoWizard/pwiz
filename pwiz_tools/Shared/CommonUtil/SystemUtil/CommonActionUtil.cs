/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    public static class CommonActionUtil
    {
        public static Thread RunAsync(Action action)
        {
            var thread = new Thread(() => RunNow(action));
            thread.Start();
            return thread;
        }

        public static void RunNow(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        public static void HandleException(Exception exception)
        {
            if (exception == null)
            {
                return;
            }
            Messages.WriteAsyncDebugMessage(@"Unhandled Exception: {0}", exception); // N.B. see TraceWarningListener for output details
        }

        /// <summary>
        /// Queues an action for asynchronous execution on the control's UI thread via
        /// <see cref="Control.BeginInvoke(Delegate)"/>. Returns false if the action could
        /// not be queued (e.g. the control has no handle or has been disposed), guaranteeing
        /// the action will never execute. Callers can use this to clean up resources that
        /// the action's execution would otherwise have handled.
        /// </summary>
        /// <returns>true if the action was successfully queued; false if it was not and will never execute.</returns>
        public static bool SafeBeginInvoke(Control control, Action action)
        {
            if (control == null || !control.IsHandleCreated)    // TIME-OF-CHECK
            {
                return false;
            }

            // Check for early shutdown signal to avoid deadlock.
            // This function significantly closes the time-of-check to time-of-use window,
            // but does not eliminate it. The try-catch below handles an ObjectDisposedException.
            // However, if the object's handle is destroyed after the IsHandleCreated check but before
            // it is fully disposed, the BeginInvoke call below can cause a deadlock with .NET trying
            // to recreate the handle. We see these in our nightly stress tests. So, it is better
            // to protect against this earlier, and not rely entirely on this function.
            var parentForm = control.FindForm();
            if (parentForm is IClosingAware closingAware && closingAware.IsClosingOrDisposing)
            {
                return false;
            }

            try
            {
                control.BeginInvoke(action);    // TIME-OF-USE
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
