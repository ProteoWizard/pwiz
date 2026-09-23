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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Utilities for running Actions.
    /// </summary>
    public static class ActionUtil
    {
        /// <summary>
        /// Run an action asynchronously with locale initialization and exception handling.
        /// Delegates to <see cref="CommonActionUtil.RunAsync"/> which handles thread init,
        /// OperationCanceledException, and exception reporting via the injected reporter.
        /// </summary>
        public static Thread RunAsync(Action action, string threadName = null)
        {
            return CommonActionUtil.RunAsync(() =>
            {
                try
                {
                    action();
                }
                // LoadCanceledException extends IOException (not OperationCanceledException)
                // and carries an IProgressStatus used by the results loading pipeline.
                // It cannot be unified with OperationCanceledException without refactoring
                // how ChromCacheBuilder and other callers extract status from the exception.
                catch (LoadCanceledException) {}
            }, threadName);
        }

        /// <summary>
        /// Calls a function with no SynchronizationContext installed on this thread, and puts
        /// the original one back when it returns.
        ///
        /// This is what makes it safe to block on an async-only API from a thread whose
        /// SynchronizationContext posts back to that same thread, which is what a WindowsForms
        /// UI thread has. Blocking on such a thread deadlocks as soon as the library resumes
        /// on the caller's context: the continuation is posted to a thread already blocked
        /// waiting for the result, so neither ever runs. With no context installed the
        /// continuations resume on the thread pool instead, and the blocking call finishes.
        ///
        /// Parquet.Net's reader is one of these. ParquetReader.CreateAsync(...).GetAwaiter()
        /// .GetResult() deadlocks on any thread with such a context, and an ordinary warm read
        /// is enough to trigger it. Its writer does not, so exporting needs nothing.
        ///
        /// Note that this only removes the deadlock, not the blocking. The calling thread
        /// still waits, so this is not a way to do slow work on the UI thread.
        /// </summary>
        public static T CallWithoutSynchronizationContext<T>(Func<T> func)
        {
            var saveContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                return func();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saveContext);
            }
        }
    }
}
