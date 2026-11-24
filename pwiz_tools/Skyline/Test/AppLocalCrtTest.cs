/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pwiz.Util.Misc;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The test process must use a VC++ runtime at least as new as the one staged beside it, as
    /// shipping Skyline does. A test that opens a real file dialog can load a shell extension
    /// carrying an older msvcp140, and if that copy were loaded first, every native vendor library
    /// loaded afterwards would bind to it - Bruker timsdata 5.x crashes against anything older than
    /// 14.40. This fails if TestRunner stops calling <see cref="AppLocalCrt.Pin"/> first, or
    /// something loads an older runtime before it.
    /// </summary>
    [TestClass]
    public class AppLocalCrtTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMsvcp140IsPinned()
        {
            string appLocal = Path.Combine(AppContext.BaseDirectory, @"msvcp140.dll");
            Assert.IsTrue(File.Exists(appLocal), string.Format(@"No app-local runtime at {0}.", appLocal));
            Assert.IsFalse(AppLocalCrt.IsBelowMinimum(appLocal),
                string.Format(@"The app-local msvcp140.dll {0} is older than {1}.", appLocal, AppLocalCrt.MinimumMsvcp140Version));

            string loaded = AppLocalCrt.LoadedMsvcp140Path();
            Assert.IsNotNull(loaded, @"No msvcp140.dll is loaded in the test process.");
            Assert.IsTrue(FileVersion(loaded) >= FileVersion(appLocal),
                string.Format(@"The process uses msvcp140.dll {0} from {1}, older than the app-local {2}.",
                    FileVersion(loaded), loaded, FileVersion(appLocal)));
        }

        private static Version FileVersion(string path)
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
    }
}
