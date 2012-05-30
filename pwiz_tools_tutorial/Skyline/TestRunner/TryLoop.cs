/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

namespace TestRunner
{
    class TryLoop
    {
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
        public static void Try<TEx>(Action action, int loopCount, int milliseconds) where TEx : Exception
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (TEx)
                {
                    Thread.Sleep(milliseconds);
                }
            }

            // Try the last time, and let the exception go.
            action();
        }

        // Default sleep time to 500 milliseconds.
        public static void Try<TEx>(Action action, int loopCount) where TEx : Exception
        {
            Try<TEx>(action, loopCount, 500);
        }
    }
}
