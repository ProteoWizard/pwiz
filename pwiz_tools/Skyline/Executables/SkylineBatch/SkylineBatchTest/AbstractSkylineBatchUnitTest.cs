/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    /// <summary>
    /// Base class for SkylineBatch unit tests. Provides TestContext and helper methods
    /// for creating test files in the TestResults directory (not source tree).
    /// 
    /// All unit tests SHOULD derive from this base class to ensure consistent
    /// test file management and avoid source tree pollution.
    /// </summary>
    public abstract class AbstractSkylineBatchUnitTest
    {
        /// <summary>
        /// MSTest TestContext - provides access to test name and TestResults directory.
        /// Automatically set by MSTest framework before each test method runs.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets a test-specific path in the TestResults directory.
        /// Creates path like: TestResults/&lt;TestName&gt;/&lt;relativePath&gt;
        /// </summary>
        protected string GetTestResultsPath(string relativePath = null)
        {
            return TestUtils.GetTestResultsPath(TestContext, relativePath);
        }

        /// <summary>
        /// Creates a Logger that writes to the TestResults directory for this test.
        /// </summary>
        protected Logger GetTestLogger(string logSubfolder = "")
        {
            return TestUtils.GetTestLogger(TestContext, logSubfolder);
        }

        /// <summary>
        /// Creates a test ConfigRunner with a test configuration and logger in TestResults.
        /// </summary>
        protected ConfigRunner GetTestConfigRunner(string configName = "name")
        {
            return new ConfigRunner(TestUtils.GetTestConfig(configName), GetTestLogger());
        }

        /// <summary>
        /// Creates a test ConfigManager with a logger in TestResults and three test configs.
        /// </summary>
        protected SkylineBatch.SkylineBatchConfigManager GetTestConfigManager()
        {
            var testConfigManager = new SkylineBatch.SkylineBatchConfigManager(GetTestLogger());
            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("one"));
            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("two"));
            testConfigManager.UserAddConfig(TestUtils.GetTestConfig("three"));
            return testConfigManager;
        }
    }
}
