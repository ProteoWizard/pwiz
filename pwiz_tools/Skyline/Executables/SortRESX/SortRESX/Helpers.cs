/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SortRESX
{
    public static class Helpers
    {
        /// <summary>
        /// Try an action that might throw an exception commonly related to a file move or delete.
        /// If it fails, sleep for the indicated period and try again.
        /// 
        /// N.B. "TryTwice" is a historical misnomer since it actually defaults to trying four times,
        /// but the intent is clear: try more than once. Further historical note: formerly this only
        /// handled IOException, but in looping tests we also see UnauthorizedAccessException as a result
        /// of file locks that haven't been released yet.
        /// </summary>
        /// <param name="action">action to try</param>
        /// <param name="loopCount">how many loops to try before failing</param>
        /// <param name="milliseconds">how long (in milliseconds) to wait before the action is retried</param>
        public static void TryTwice(Action action, int loopCount = 4, int milliseconds = 500)
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException exIO)
                {
                    ReportExceptionForRetry(milliseconds, exIO, i, loopCount);
                }
                catch (UnauthorizedAccessException exUA)
                {
                    ReportExceptionForRetry(milliseconds, exUA, i, loopCount);
                }
            }

            // Try the last time, and let the exception go.
            action();
        }
        private static void ReportExceptionForRetry(int milliseconds, Exception x, int loopCount, int maxLoopCount)
        {
            Trace.WriteLine(string.Format(@"Encountered the following exception (attempt {0} of {1}):", loopCount, maxLoopCount));
            Trace.WriteLine(x.Message);
            Thread.Sleep(milliseconds);
        }
    }
}
