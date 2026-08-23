/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;
using SkylineBatch;

namespace SkylineBatchTest
{
    /// <summary>
    /// Assembly-wide setup for SkylineBatchTest.
    /// </summary>
    [TestClass]
    public class SkylineBatchTestSetup
    {
        /// <summary>
        /// Supplies the two things this suite silently required of the machine it ran on: an R
        /// installation and a Skyline installation. Neither was documented, and without them the
        /// suite did not fail - it stopped on modal dialogs nothing could dismiss.
        ///
        /// **R**: nearly every fixture is built through <see cref="TestUtils"/>, which asks
        /// <see cref="RInstallations.GetMostRecentInstalledRVersion"/> for a version string, and
        /// that throws when nothing is installed. These tests only check that a version is
        /// recorded; they do not execute R, so mock versions are faithful.
        /// <see cref="TestUtils.SetupMockRInstallations"/> was written for exactly this ("Use
        /// this to run tests on TeamCity clients or machines without R installed") and had never
        /// been called. The three tests that genuinely RUN R still need it installed.
        ///
        /// **Skyline**: the functional tests drive the real application, which refuses to start,
        /// and then refuses to save a configuration, without one.
        ///
        /// Both are consulted only as fallbacks - mock R versions apply only when real detection
        /// finds none, and the Skyline path only when there is no administrative install - so a
        /// fully provisioned machine behaves exactly as it did before.
        /// </summary>
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext testContext)
        {
            TestUtils.SetupMockRInstallations();
            SkylineInstallations.TestAdminSkylineCmdPath = FindBuiltSkylineCmd();

            // Publish into Settings now. The fallback feeds FindAdministrativeInstallations, and
            // nothing reads it until something calls FindSkyline() - so without this the first
            // configurations a test reads are still typed against an absent installation and fail
            // validation with "Could not find a Skyline installation on this computer".
            SkylineInstallations.FindSkyline();
        }

        /// <summary>
        /// The SkylineCmd.exe of a Skyline build in this checkout, or null when nothing has been
        /// built - in which case behaviour is exactly what it was.
        ///
        /// These tests' supported home is Skyline's own output directory, where SkylineCmd.exe
        /// sits beside them; the batch-tool build scripts run them from the test project's own
        /// bin directory, where it does not.
        /// </summary>
        private static string FindBuiltSkylineCmd()
        {
            var skylineDir = TestUtils.GetSkylineDir();
            if (skylineDir == null)
                return null;

            var cmdPath = Path.Combine(skylineDir, SkylineInstallations.SkylineCmdExe);
            return File.Exists(cmdPath) ? cmdPath : null;
        }
    }
}
