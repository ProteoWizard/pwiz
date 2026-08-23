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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;

namespace AutoQCTest
{
    /// <summary>
    /// Assembly-wide setup for AutoQCTest. The counterpart of
    /// SkylineBatchTest.SkylineBatchTestSetup, and needed for the same reason.
    /// </summary>
    [TestClass]
    public class AutoQCTestSetup
    {
        /// <summary>
        /// Stands a Skyline build in this checkout in for an administrative installation, when the
        /// machine has none.
        ///
        /// Without it the functional tests do not fail, they HANG. AutoQcConfigForm.Save() calls
        /// Validate(), which throws "Could not find a Skyline installation on this computer", and
        /// Save() answers that by opening a modal AlertDlg. The test drives Save() through
        /// AbstractBaseFunctionalTest.RunUI, which is a synchronous Control.Invoke, so the test
        /// thread waits for a delegate that is itself parked on a dialog nobody will dismiss.
        /// Measured before this: the suite stopped dead for 18 minutes at 0.02 CPU-seconds per
        /// 15s, with an AutoQC Loader window on screen.
        ///
        /// Consulted only as a fallback - FindAdministrativeInstallations uses it only when there
        /// is no real installation - so a provisioned machine is unaffected.
        /// </summary>
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext testContext)
        {
            SkylineInstallations.TestAdminSkylineCmdPath = FindBuiltSkylineCmd();

            // Publish into Settings now, so the fallback is in place before the first
            // configuration is validated rather than after.
            SkylineInstallations.FindSkyline();
        }

        /// <summary>
        /// The SkylineCmd.exe of a Skyline build in this checkout, or null when nothing has been
        /// built - in which case behaviour is exactly what it was.
        /// </summary>
        private static string FindBuiltSkylineCmd()
        {
            try
            {
                var cmdPath = Path.Combine(TestUtils.GetSkylineBinDirectory(),
                    SkylineInstallations.SkylineCmdExe);
                return File.Exists(cmdPath) ? cmdPath : null;
            }
            catch (Exception)
            {
                // GetSkylineBinDirectory throws when no Skyline has been built here. That is not
                // a setup failure - it just means there is no fallback to offer.
                return null;
            }
        }
    }
}
