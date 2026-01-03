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
        /// Safely invokes an action on the UI thread via BeginInvoke.
        /// Returns false if the control is null, has no handle, or its parent form
        /// is closing/disposing (to avoid deadlock from handle recreation).
        /// </summary>
        public static bool SafeBeginInvoke(Control control, Action action)
        {
            if (control == null || !control.IsHandleCreated)
            {
                return false;
            }

            // Check for CommonFormEx early shutdown signal to avoid deadlock.
            // When BeginInvoke is called on a closing form, .NET may try to recreate
            // the handle, which requires the UI thread - causing deadlock if the UI
            // thread is waiting for the background thread to complete.
            var parentForm = control.FindForm();
            if (parentForm is CommonFormEx formEx && formEx.IsClosingOrDisposing)
            {
                return false;
            }

            try
            {
                control.BeginInvoke(action);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
