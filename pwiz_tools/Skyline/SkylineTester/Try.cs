/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace SkylineTester
{
    static class Try
    {
        /// <summary>
        /// Try an action multiple times in the event of an exception.  If it continues
        /// to fail, throw or ignore the exception.
        /// </summary>
        /// <typeparam name="TEx">Type of exception to ignore.</typeparam>
        /// <param name="action">Action to run.</param>
        /// <param name="loopCount">How many times to try if exception is being thrown.</param>
        /// <param name="throwOnFailure">True to throw the final exception, false to ignore it.</param>
        /// <param name="milliseconds">Time to sleep between run attempts.</param>
        public static void Multi<TEx>(Action action, int loopCount = 4, bool throwOnFailure = true, int milliseconds = 500)
            where TEx : Exception
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
            if (throwOnFailure)
                action();
        }
    }
}
