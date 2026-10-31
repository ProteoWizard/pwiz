/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// The Osprey.exe a test runs, for the Osprey Search wizard and for Build Library's AlphaPeptDeep
    /// source, which has Osprey predict the library: the one seam, so every test that predicts a
    /// library finds Osprey the same way.
    /// </summary>
    public static class OspreyTestUtil
    {
        /// <summary>
        /// Names a local Osprey.exe to test against, for a developer's loop; without it the test
        /// looks for an Osprey build beside the Skyline source and then downloads the pinned package.
        /// </summary>
        public const string ENV_OSPREY_EXE = @"SKYLINE_TEST_OSPREY_EXE";

        /// <summary>TeamCity's name for the commit a build checks out, set in every build's environment.</summary>
        public const string ENV_TEAMCITY_REVISION = @"BUILD_VCS_NUMBER";

        /// <summary>Registers <paramref name="ospreyExe"/> as the Osprey Skyline runs, in place of any earlier registration.</summary>
        public static void RegisterOsprey(string ospreyExe)
        {
            AbstractFunctionalTest.RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.Osprey))
                    Settings.Default.SearchToolList.Remove(Settings.Default.SearchToolList[SearchToolType.Osprey]);
                Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.Osprey,
                    ospreyExe, string.Empty, Path.GetDirectoryName(ospreyExe), false));
            });
        }

        /// <summary>
        /// The Osprey.exe to test: the one SKYLINE_TEST_OSPREY_EXE names, else a build in the Osprey
        /// project beside the Skyline source (the developer's loop), else a build downloaded from
        /// TeamCity (the nightly's loop, as SkylineNightly fetches SkylineTester): on TeamCity the
        /// Osprey build of the same commit when it has finished, so a pull request that changes Osprey
        /// and Skyline together is tested with its own Osprey, else the latest successful master build.
        /// </summary>
        public static string EnsureOsprey(TestContext testContext)
        {
            string fromEnvironment = Environment.GetEnvironmentVariable(ENV_OSPREY_EXE);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                AssertEx.FileExists(fromEnvironment, ENV_OSPREY_EXE + @" names a file that does not exist");
                return fromEnvironment;
            }
            foreach (var configuration in new[] { @"Debug", @"Release" })
            {
                string sibling = Path.GetFullPath(testContext.GetProjectDirectory(
                    Path.Combine(@"..", @"Osprey", @"Osprey", @"bin", @"x64", configuration, @"net10.0", OspreyHelpers.OSPREY_EXE)));
                if (File.Exists(sibling))
                    return sibling;
            }
            // Neither: the latest successful Osprey build on TeamCity, as SkylineNightly fetches
            // SkylineTester. Its URL is used as given, since the tool mirror holds no Osprey package.
            AbstractFunctionalTest.RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.Osprey))
                    Settings.Default.SearchToolList.Remove(Settings.Default.SearchToolList[SearchToolType.Osprey]);
            });
            string revision = Environment.GetEnvironmentVariable(ENV_TEAMCITY_REVISION);
            if (string.IsNullOrEmpty(revision) || !OspreyHelpers.TryGetRevisionBuildDownloadInfo(revision, out var downloadInfo))
                downloadInfo = OspreyHelpers.GetLatestBuildDownloadInfo();
            Console.WriteLine(@"# Osprey for this test: {0}", downloadInfo.DownloadUrl);
            bool savedUseOriginalUrls = Skyline.Program.UseOriginalURLs;
            try
            {
                Skyline.Program.UseOriginalURLs = true;
                AssertEx.IsTrue(SimpleFileDownloader.DownloadRequiredFiles(new[] { downloadInfo }, new SilentProgressMonitor()),
                    @"failed to download Osprey from " + downloadInfo.DownloadUrl);
            }
            finally
            {
                Skyline.Program.UseOriginalURLs = savedUseOriginalUrls;
            }
            AssertEx.FileExists(downloadInfo.ToolPath);
            return downloadInfo.ToolPath;
        }
    }
}
