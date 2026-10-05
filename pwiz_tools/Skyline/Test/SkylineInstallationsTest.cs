/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="SkylineInstallations"/>, which chooses the other installations a
    /// user is offered settings from.
    /// </summary>
    [TestClass]
    public class SkylineInstallationsTest : AbstractUnitTest
    {
        private static readonly string INSTALLATIONS_FOLDER = Path.Combine(Path.GetTempPath(), @"Installations");

        /// <summary>
        /// Neither this installation nor a newer one, nor one whose version cannot be read, is
        /// offered. The rest are ordered with the ones Programs and Features lists first, then
        /// by version, compared as versions rather than as text.
        /// </summary>
        [TestMethod]
        public void TestSkylineInstallations()
        {
            var own = CreateInstallation(@"Own", @"26.1.1.100", true);
            var installations = new StubSkylineInstallations(
                own,
                CreateInstallation(@"Newer", @"26.1.1.101", true),
                CreateInstallation(@"Unreadable", @"not a version", true),
                CreateInstallation(@"Older99", @"26.1.1.99", false),
                CreateInstallation(@"SameVersion", @"26.1.1.100", false),
                CreateInstallation(@"Listed", @"26.1.1.9", true))
            {
                CurrentVersion = new Version(26, 1, 1, 100),
                OwnUserConfigFile = own.UserConfigFile
            };

            Assert.AreEqual(string.Join(@", ", @"Listed", @"SameVersion", @"Older99"),
                string.Join(@", ", installations.ListOtherInstallations()
                    .Select(installation => Path.GetFileName(installation.ExecutableFolder))));
        }

        private static SkylineInstallation CreateInstallation(string folderName, string version, bool isCurrentlyInstalled)
        {
            var folder = Path.Combine(INSTALLATIONS_FOLDER, folderName);
            return new SkylineInstallation
            {
                Version = version,
                ExecutableFolder = folder,
                UserConfigFile = Path.Combine(folder, @"user.config"),
                IsCurrentlyInstalled = isCurrentlyInstalled
            };
        }

        private class StubSkylineInstallations : SkylineInstallations
        {
            private readonly IList<SkylineInstallation> _installations;

            public StubSkylineInstallations(params SkylineInstallation[] installations)
            {
                _installations = installations;
            }

            protected override IEnumerable<SkylineInstallation> FindInstallations()
            {
                return _installations;
            }
        }
    }
}
