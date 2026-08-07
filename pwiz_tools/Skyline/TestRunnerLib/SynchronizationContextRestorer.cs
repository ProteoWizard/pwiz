/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace TestRunnerLib
{
    /// <summary>
    /// Puts the plain <see cref="SynchronizationContext"/> back on the current thread when
    /// disposed, so that a test which constructs a WindowsForms control does not change how
    /// every test after it in the same process behaves.
    ///
    /// Constructing any control installs a <see cref="WindowsFormsSynchronizationContext"/>.
    /// It takes no handle, no message loop and no Application.Run, and disposing the control
    /// does not undo it. What it leaves behind is a context whose Post goes to a message queue
    /// that nothing is pumping, so any later test which blocks waiting on an async API from
    /// the test thread deadlocks instead of finishing. A parquet read is one of those, which
    /// is how a report test hung on TeamCity while passing every time it was run on its own.
    ///
    /// Use it around the control:
    ///
    ///     using (new SynchronizationContextRestorer())
    ///     {
    ///         var control = new SomeControl();
    ///         ...
    ///     }
    /// </summary>
    public sealed class SynchronizationContextRestorer : IDisposable
    {
        public void Dispose()
        {
            RestorePlain();
        }

        /// <summary>
        /// True if this context posts back to one particular thread, which is what makes
        /// blocking on an async API from that thread a deadlock. The plain base class is what
        /// a thread which has never touched WindowsForms has, and its Post goes to the thread
        /// pool, where it cannot deadlock anything.
        /// </summary>
        public static bool IsThreadAffine(SynchronizationContext context)
        {
            return context != null && context.GetType() != typeof(SynchronizationContext);
        }

        /// <summary>
        /// Puts the plain <see cref="SynchronizationContext"/> back, the way Application.Run
        /// does when its message loop ends.
        ///
        /// This goes through WindowsForms rather than only calling SetSynchronizationContext
        /// because WindowsForms remembers the context it replaced and will not install a
        /// second time while that record is set. Replacing the context without clearing the
        /// record leaves WindowsForms believing it is still installed, so no control
        /// constructed later in the process installs anything. The thread is safe either way,
        /// but the check in RunTests would then report only whichever test got there first,
        /// however many were at fault.
        /// </summary>
        public static void RestorePlain()
        {
            var uninstall = typeof(WindowsFormsSynchronizationContext).GetMethod(@"Uninstall",
                BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(bool) }, null);
            uninstall?.Invoke(null, new object[] { false });

            // Uninstall does nothing unless a WindowsForms context is current, and puts back
            // whatever was there before, which may be null or may be nothing at all.
            var context = SynchronizationContext.Current;
            if (context == null || context.GetType() != typeof(SynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }
    }
}
