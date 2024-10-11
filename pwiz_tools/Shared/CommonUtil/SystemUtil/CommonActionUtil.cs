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
        public static void RunAsync(Action action)
        {
            // ReSharper disable ObjectCreationAsStatement
            new Thread(() =>
            {
                RunNow(action);
            }).Start();
            // ReSharper restore ObjectCreationAsStatement
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

        public static bool SafeBeginInvoke(Control control, Action action)
        {
            if (control == null || !control.IsHandleCreated)
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
