/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharedBatchTest
{
    /// <summary>
    /// Root base class for all batch tools unit tests (AutoQC and SkylineBatch).
    /// Provides MSTest TestContext and helper methods for creating test files
    /// in the TestResults directory (not source tree).
    ///
    /// All unit tests SHOULD derive from this base class (or project-specific
    /// extensions) to ensure consistent test file management and avoid source
    /// tree pollution.
    ///
    /// Project-specific test base classes (AbstractAutoQcUnitTest, AbstractSkylineBatchUnitTest)
    /// extend this to add project-specific helpers while maintaining a shared foundation.
    /// </summary>
    public abstract class AbstractUnitTest
    {
        /// <summary>
        /// MSTest TestContext - provides access to test name and TestResults directory.
        /// Automatically set by MSTest framework before each test method runs.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Waits for a condition to become true, polling at regular intervals.
        /// This is useful for tests that need to wait for asynchronous operations to complete.
        /// Throws an assertion failure if the timeout is exceeded.
        /// </summary>
        /// <param name="condition">Function that returns true when the desired condition is met</param>
        /// <param name="timeout">Maximum time to wait for the condition</param>
        /// <param name="timestep">Interval between condition checks (in milliseconds)</param>
        /// <param name="errorMessage">Message to include in the assertion failure if timeout is exceeded</param>
        protected static void WaitForCondition(Func<bool> condition, TimeSpan? timeout = null, int timestep = 100, string errorMessage = null)
        {
            timeout ??= TimeSpan.FromSeconds(5);
            // Too hard to debug when this is based on actual time. So just do a fixed number of loops.
            int timeLoops = (int)(timeout.Value.TotalMilliseconds / timestep);
            for (int i = 0; i < timeLoops; i++)
            {
                if (condition())
                    return;
                Thread.Sleep(timestep);
            }
            Assert.Fail($"Timeout waiting for condition after {timeout}: {errorMessage}");
        }
    }
}
