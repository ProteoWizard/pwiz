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
using System.IO;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public static class TryHelper
    {
        private const int defaultLoopCount = 4;
        private const int defaultMilliseconds = 500;

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
        /// <param name="hint">text to show in debug trace on failure</param>
        public static void TryTwice(Action action, int loopCount = defaultLoopCount, int milliseconds = defaultMilliseconds, string hint = null)
        {
            for (int i = 1; i<loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException exIO)
                {
                    ReportExceptionForRetry(milliseconds, exIO, i, loopCount, hint);
                }
                catch (UnauthorizedAccessException exUA)
                {
                    ReportExceptionForRetry(milliseconds, exUA, i, loopCount, hint);
                }
            }
            DetailedTrace.WriteLine(string.Format(@"Final attempt ({0} of {1}):", loopCount, loopCount), true);
            // Try the last time, and let the exception go.
            action();
        }

        public static void TryTwice(Action action, string hint)
        {
            TryTwice(action, defaultLoopCount, defaultMilliseconds, hint);
        }

        private static void ReportExceptionForRetry(int milliseconds, Exception x, int loopCount, int maxLoopCount, string hint)
        {
            DetailedTrace.WriteLine(string.Format(@"Encountered the following exception on attempt {0} of {1}{2}:", loopCount, maxLoopCount,
                string.IsNullOrEmpty(hint) ? string.Empty : (@" of action " + hint)));
            DetailedTrace.WriteLine(x.ToString());
            if (RunningResharperAnalysis || IsParallelClient)
            {
                DetailedTrace.WriteLine(IsParallelClient ?
                    $@"We're running under a virtual machine, which may throw off timing - adding some extra sleep time":
                    $@"We're running under ReSharper analysis, which may throw off timing - adding some extra sleep time");
                // Allow up to 5 sec extra time when running code coverage or other analysis
                milliseconds += (5000 * (loopCount)) / maxLoopCount; // Each loop a little more desperate
            }
            DetailedTrace.WriteLine(string.Format(@"Sleeping {0} ms then retrying...", milliseconds));
            Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// Detects the use of ReSharper code coverage (dotCover), memory profiling (dotMemory),
        /// or performance profiling (dotTrace), which may affect timing in tests.
        /// Per https://youtrack.jetbrains.com/issue/PROF-1093:
        /// "Set JETBRAINS_DPA_AGENT_ENABLE=0 environment variable for user apps started from dotTrace,
        /// and JETBRAINS_DPA_AGENT_ENABLE=1 in case of dotCover and dotMemory."
        /// </summary>
        public static bool RunningResharperAnalysis => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"JETBRAINS_DPA_AGENT_ENABLE"));

        /// <summary>
        /// Detects if running as a parallel test client in SkylineTester.
        /// </summary>
        public static bool IsParallelClient => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(@"SKYLINE_TESTER_PARALLEL_CLIENT_ID"));

        /// <summary>
        /// Try an action that might throw an exception.  If it does, sleep for a little while and
        /// try the action one more time.  This oddity is necessary because certain file system
        /// operations (like moving a directory) can fail due to temporary file locks held by
        /// anti-virus software.
        /// </summary>
        /// <typeparam name="TEx">type of exception to catch</typeparam>
        /// <param name="action">action to try</param>
        /// <param name="loopCount">how many loops to try before failing</param>
        /// <param name="milliseconds">how long (in milliseconds) to wait before the action is retried</param>
        /// <param name="hint">text to show in debug trace on failure</param>
        public static void Try<TEx>(Action action, int loopCount = defaultLoopCount, int milliseconds = defaultMilliseconds, string hint = null) where TEx : Exception
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (TEx x)
                {
                    ReportExceptionForRetry(milliseconds, x, i, loopCount, hint);
                }
            }
            DetailedTrace.WriteLine(string.Format(@"Final attempt ({0} of {1}):", loopCount, loopCount), true);
            // Try the last time, and let the exception go.
            action();
        }
    }
}
