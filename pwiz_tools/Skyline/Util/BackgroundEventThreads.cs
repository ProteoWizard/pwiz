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

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Helper methods for showing forms on threads other than the main thread.
    /// This class takes care of intalling a thread exception handler.  It also
    /// calls LocalizationHelper.InitThread.
    /// </summary>
    public static class BackgroundEventThreads
    {
        /// <summary>
        /// Shows a form on a new background thread.
        /// The formConstructor gets invoked on the thread that the form is going
        /// to be shown on. It is always best practice to construct forms on the same
        /// thread as they are going to be shown, because certain controls, such as
        /// <see cref="System.Windows.Forms.Timer"/> expect that they have been constructed
        /// on that thread.
        /// </summary>
        public static void ShowFormOnBackgroundThread(Func<Form> formConstructor)
        {
            var thread = CreateThreadForAction(() =>
            {
                using (var form = formConstructor())
                {
                    Application.Run(form);
                }
            });
            thread.Start();
        }

        /// <summary>
        /// Constructs a thread which, when run, installs a ThreadException handler, and 
        /// calls LocalizationHelper.InitThread, and runs the specified action.
        /// </summary>
        public static Thread CreateThreadForAction(Action action)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += (sender, args) => Program.ReportException(args.Exception);
                    LocalizationHelper.InitThread();
                    action();
                }
                catch (Exception e)
                {
                    Program.ReportException(e);
                }
                finally
                {
                    Application.ExitThread();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            return thread;
        }
    }
}
