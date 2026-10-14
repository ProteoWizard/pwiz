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

namespace pwiz.Common.SystemUtil
{
    public static class CommonActionUtil
    {
        /// <summary>
        /// Callback for reporting unhandled exceptions from background threads.
        /// Set by the host application (e.g. Skyline sets this to Program.ReportException)
        /// to surface exceptions in the UI instead of swallowing them.
        /// </summary>
        public static Action<Exception> ExceptionReporter { get; set; }

        public static Thread RunAsync(Action action, string threadName = null)
        {
            var thread = new Thread(() => RunNow(action, threadName));
            thread.Start();
            return thread;
        }

        public static void RunNow(Action action, string threadName = null)
        {
            try
            {
                LocalizationHelper.InitThread(threadName);
                action();
            }
            catch (OperationCanceledException) {}
            catch (Exception e)
            {
                HandleException(e);
            }
        }

        // CONSIDER: Currently silently swallows unhandled exceptions for processes that
        // don't set an ExceptionReporter. All EXEs using CommonActionUtil should be required
        // to explicitly set an ExceptionReporter (even a silent one) before calling RunAsync.
        // See https://github.com/ProteoWizard/pwiz/issues/4128
        public static void HandleException(Exception exception)
        {
            if (exception == null)
                return;

            try
            {
                ExceptionReporter?.Invoke(exception);
            }
            catch (Exception)
            {
                // Prevent failures in the reporter from crashing the background thread
            }
        }
    }
}
