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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="ClickOnceInstallations"/>, which lists the ClickOnce installations a
    /// newly installed Skyline could inherit settings and external tools from.
    /// </summary>
    [TestClass]
    public class ClickOnceInstallationsTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\ClickOnceInstallationsTest.zip";
        private const string ASSEMBLY_NAME = @"ExampleProductName";
        private const string INSTALLED_VERSION = @"26.1.1.209";
        private const string UNINSTALLED_VERSION = @"26.1.1.231";
        private const string UNINSTALL_COMMAND = @"rundll32.exe dfshim.dll,ShArpMaintain " + ASSEMBLY_NAME +
            @".application, Culture=neutral, PublicKeyToken=9286511f3362df93, processorArchitecture=msil";

        /// <summary>
        /// A ClickOnce installation's settings are in the data store folder named like its
        /// installation folder. With two installations on disk, each must be found with its own
        /// installation folder (where its Tools are) and its own user.config. The one Programs
        /// and Features no longer lists is still offered, but has nothing to uninstall.
        /// </summary>
        [TestMethod]
        public void TestInstallationsArePairedWithTheirOwnSettings()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var localAppData = CreateLocalAppData(@"TwoInstallations");
            var currentFolder = WriteInstallation(localAppData, INSTALLED_VERSION);
            var currentConfig = WriteConfig(localAppData, currentFolder, INSTALLED_VERSION);
            var removedFolder = WriteInstallation(localAppData, UNINSTALLED_VERSION);
            var removedConfig = WriteConfig(localAppData, removedFolder, UNINSTALLED_VERSION);

            var installations = FindInstallations(localAppData);
            Assert.AreEqual(2, installations.Count);

            var current = installations[INSTALLED_VERSION];
            Assert.AreEqual(ASSEMBLY_NAME, current.ProductName);
            Assert.AreEqual(currentFolder, current.ExecutableFolder);
            Assert.AreEqual(currentConfig, current.UserConfigFile);
            Assert.IsTrue(current.IsCurrentlyInstalled);
            // Listed in Programs and Features, so it can be uninstalled, with the command found there.
            Assert.IsTrue(current.CanUninstall);
            Assert.AreEqual(UNINSTALL_COMMAND, current.UninstallCommand);

            var removed = installations[UNINSTALLED_VERSION];
            Assert.AreEqual(removedFolder, removed.ExecutableFolder);
            Assert.AreEqual(removedConfig, removed.UserConfigFile);
            Assert.IsFalse(removed.IsCurrentlyInstalled);
            Assert.IsFalse(removed.CanUninstall);
        }

        /// <summary>
        /// Skyline run from a build folder keeps its settings in a per version folder under
        /// %LOCALAPPDATA%\(company), and a developer build can have the same version as an
        /// installation. Those settings are not the installation's, so the installation, having
        /// no settings of its own, is not offered.
        /// </summary>
        [TestMethod]
        public void TestDeveloperBuildSettingsAreNotOffered()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var localAppData = CreateLocalAppData(@"DeveloperBuilds");
            WriteInstallation(localAppData, INSTALLED_VERSION);
            var buildSettingsFolder = Path.Combine(localAppData, @"University_of_Washington",
                ASSEMBLY_NAME + @".exe_Url_developerbuild", INSTALLED_VERSION);
            Directory.CreateDirectory(buildSettingsFolder);
            File.WriteAllText(Path.Combine(buildSettingsFolder, @"user.config"),
                @"<configuration><userSettings /></configuration>");

            Assert.AreEqual(0, FindInstallations(localAppData).Count);
        }

        /// <summary>
        /// An installation that was never run wrote no settings, so there is nothing to import.
        /// </summary>
        [TestMethod]
        public void TestNeverRunInstallationIsNotOffered()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var localAppData = CreateLocalAppData(@"NeverRun");
            WriteInstallation(localAppData, INSTALLED_VERSION);

            Assert.AreEqual(0, FindInstallations(localAppData).Count);
        }

        /// <summary>
        /// Includes a product whose name is the start of this one's, as the release channel's
        /// name is the start of the daily channel's.
        /// </summary>
        [TestMethod]
        public void TestOtherProductsAreNotOffered()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            const string prefixName = @"Example";
            var localAppData = CreateLocalAppData(@"OtherApplication");
            WriteConfig(localAppData, WriteInstallation(localAppData, INSTALLED_VERSION, prefixName),
                INSTALLED_VERSION);
            WriteConfig(localAppData, WriteInstallation(localAppData, INSTALLED_VERSION, @"AutoQC"),
                INSTALLED_VERSION);

            Assert.AreEqual(0, FindInstallations(localAppData).Count);
        }

        /// <summary>
        /// Installations found, by version. InstalledVersions is always supplied, so that no case falls
        /// through to the registry of whatever machine the test is running on.
        /// </summary>
        private static IDictionary<string, SkylineInstallation> FindInstallations(string localAppData)
        {
            var clickOnceInstallations = new StubClickOnceInstallations
            {
                LocalApplicationDataFolder = localAppData,
                InstalledVersions = new Dictionary<string, string> { { INSTALLED_VERSION, UNINSTALL_COMMAND } }
            };
            return clickOnceInstallations.ListCandidates().ToDictionary(installation => installation.Version);
        }

        private string CreateLocalAppData(string name)
        {
            var localAppData = TestFilesDir.GetTestPath(name);
            Directory.CreateDirectory(localAppData);
            return localAppData;
        }

        /// <summary>
        /// A ClickOnce installation folder, at the depth the real store puts one. The folder is
        /// named for the version, which is what <see cref="StubClickOnceInstallations"/> reports in
        /// place of the executable's version resource.
        /// </summary>
        private static string WriteInstallation(string localAppData, string version,
            string assemblyName = ASSEMBLY_NAME)
        {
            var folder = Path.Combine(localAppData, @"Apps\2.0", @"ABCDEFGH.IJK", @"LMNOPQRS.TUV",
                assemblyName + @"_" + version);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, assemblyName + @".exe"), string.Empty);
            Directory.CreateDirectory(Path.Combine(folder, @"Tools"));
            return folder;
        }

        /// <summary>
        /// The user.config ClickOnce keeps for the installation, in the data store folder named
        /// like the installation folder.
        /// </summary>
        private static string WriteConfig(string localAppData, string installationFolder, string version)
        {
            var folder = Path.Combine(localAppData, @"Apps\2.0\Data", @"WXYZABCD.EFG", @"HIJKLMNO.PQR",
                Path.GetFileName(installationFolder), @"Data", version);
            Directory.CreateDirectory(folder);
            var configFile = Path.Combine(folder, @"user.config");
            File.WriteAllText(configFile, @"<configuration><userSettings /></configuration>");
            return configFile;
        }

        /// <summary>
        /// Reports a version for the made up executables the test writes, which have no version
        /// resource of their own. The installation folder is named for its version.
        /// </summary>
        private class StubClickOnceInstallations : ClickOnceInstallations
        {
            public StubClickOnceInstallations() : base(ASSEMBLY_NAME)
            {
            }

            protected override string ReadExecutableVersion(string executableFolder)
            {
                var folderName = Path.GetFileName(executableFolder) ?? string.Empty;
                int versionStart = folderName.LastIndexOf('_');
                return versionStart < 0 ? null : folderName.Substring(versionStart + 1);
            }
        }
    }
}
