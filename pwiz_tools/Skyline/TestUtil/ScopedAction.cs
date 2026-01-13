/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Sonnet 4.5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// General-purpose RAII (Resource Acquisition Is Initialization) helper for testing.
    /// Executes an optional initialization action in the constructor and a required
    /// dispose action when the scope exits, eliminating the need to create specialized
    /// IDisposable classes for simple scope-based cleanup patterns.
    /// </summary>
    /// <example>
    /// // Simple cleanup action:
    /// using (new ScopedAction(() => CleanupSomething()))
    /// {
    ///     // Test code
    /// }
    ///
    /// // With initialization and cleanup:
    /// using (new ScopedAction(
    ///     disposeAction: () => Settings.Default.SomeSetting = false,
    ///     initAction: () => Settings.Default.SomeSetting = true))
    /// {
    ///     // Test with setting enabled
    /// }
    ///
    /// // Lambda expressions for more complex scenarios:
    /// using (new ScopedAction(
    ///     disposeAction: () =>
    ///     {
    ///         // Multiple cleanup steps
    ///         RestoreState();
    ///         CloseResources();
    ///     },
    ///     initAction: () =>
    ///     {
    ///         // Multiple initialization steps
    ///         SaveState();
    ///         OpenResources();
    ///     }))
    /// {
    ///     // Test code
    /// }
    /// </example>
    public class ScopedAction : IDisposable
    {
        private readonly Action _disposeAction;

        /// <summary>
        /// Creates a scoped action that will execute the dispose action when the scope exits.
        /// </summary>
        /// <param name="initAction">Optional action to execute immediately in the constructor</param>
        /// <param name="disposeAction">Action to execute when Dispose() is called (required)</param>
        public ScopedAction(Action initAction, Action disposeAction)
        {
            _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            initAction?.Invoke();
        }

        public ScopedAction(Action disposeAction)
            : this(null, disposeAction)
        {
        }

        public void Dispose()
        {
            _disposeAction();
        }
    }
}
