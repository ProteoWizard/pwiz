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
using System.Reflection;
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
        private const string ASSEMBLY_NAME = @"Skyline-daily";
        private const string COMPANY_FOLDER = @"University_of_Washington";
        private const string INSTALLED_VERSION = @"26.1.1.209";
        private const string UNINSTALLED_VERSION = @"26.1.1.231";

        [TestMethod]
        public void TestClickOnceInstallations()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            VerifyDeploymentNameIdentifiesTheProduct();
            VerifyNameComesFromTheAssembly();
            VerifyEachCandidateCarriesItsOwnExecutableFolder();
            VerifySettingsWithoutAnInstallationAreNotCandidates();
            VerifyInstallationWithoutSettingsIsNotACandidate();
            VerifyOtherCompanyFoldersAreSearched();
            VerifyOtherApplicationsAreIgnored();
        }

        /// <summary>
        /// The registry entry is matched on its deployment manifest name, which ClickOnce takes
        /// from the assembly name, so that a display name which does not match the assembly
        /// cannot make the installation unfindable.
        /// </summary>
        private void VerifyDeploymentNameIdentifiesTheProduct()
        {
            // Real uninstall commands, read out of the registry.
            Assert.AreEqual(@"Skyline-daily.application", ClickOnceInstallations.GetDeploymentName(
                @"rundll32.exe dfshim.dll,ShArpMaintain Skyline-daily.application, Culture=neutral, PublicKeyToken=9286511f3362df93, processorArchitecture=msil"));
            Assert.AreEqual(@"AutoQC.application", ClickOnceInstallations.GetDeploymentName(
                @"rundll32.exe dfshim.dll,ShArpMaintain AutoQC.application, Culture=neutral, PublicKeyToken=9286511f3362df93, processorArchitecture=msil"));

            // Anything that is not a ClickOnce uninstall, so that an ordinary installer sharing
            // the name cannot be mistaken for one.
            Assert.IsNull(ClickOnceInstallations.GetDeploymentName(
                @"C:\Program Files\Skyline\uninstall.exe /S"));
            Assert.IsNull(ClickOnceInstallations.GetDeploymentName(
                @"MsiExec.exe /X{00000000-0000-0000-0000-000000000000}, Culture=neutral"));
            Assert.IsNull(ClickOnceInstallations.GetDeploymentName(@"rundll32.exe dfshim.dll"));
            Assert.IsNull(ClickOnceInstallations.GetDeploymentName(null));
        }

        /// <summary>
        /// The name is taken from the assembly handed in, so that SkylineCmd.exe and
        /// Skyline-daily.exe, which start different entry assemblies, look for the same product.
        /// </summary>
        private void VerifyNameComesFromTheAssembly()
        {
            var assembly = typeof(ClickOnceInstallations).Assembly;
            Assert.AreEqual(assembly.GetName().Name, new ClickOnceInstallations(assembly).AssemblyName);
        }

        /// <summary>
        /// The point of returning candidates rather than a single file. Two installations can be
        /// on disk at once, only one of them listed in Programs and Features, and each carries the
        /// executable folder its own Tools folder is in.
        /// </summary>
        private void VerifyEachCandidateCarriesItsOwnExecutableFolder()
        {
            var localAppData = CreateLocalAppData(@"TwoInstallations");
            var currentFolder = WriteInstallation(localAppData, INSTALLED_VERSION);
            var currentConfig = WriteConfig(localAppData, COMPANY_FOLDER, @"Skyline-daily.exe_Url_current",
                INSTALLED_VERSION);
            var removedFolder = WriteInstallation(localAppData, UNINSTALLED_VERSION);
            var removedConfig = WriteConfig(localAppData, COMPANY_FOLDER, @"Skyline-daily.exe_Url_removed",
                UNINSTALLED_VERSION);

            var candidates = ListCandidates(localAppData);
            Assert.AreEqual(2, candidates.Count);

            var current = candidates[INSTALLED_VERSION];
            Assert.AreEqual(currentFolder, current.ExecutableFolder);
            Assert.AreEqual(currentConfig, current.UserConfigFile);
            Assert.IsTrue(current.IsCurrentlyInstalled);

            // Still a candidate, and still paired with its own folder, even though Programs and
            // Features no longer lists it.
            var removed = candidates[UNINSTALLED_VERSION];
            Assert.AreEqual(removedFolder, removed.ExecutableFolder);
            Assert.AreEqual(removedConfig, removed.UserConfigFile);
            Assert.IsFalse(removed.IsCurrentlyInstalled);
        }

        /// <summary>
        /// The hazard this class exists for. A developer machine collects a settings folder for
        /// every folder Skyline has ever run from, hundreds of them, with versions higher than any
        /// installation's. None is an installation, so none has an executable folder to offer and
        /// none is a candidate.
        /// </summary>
        private void VerifySettingsWithoutAnInstallationAreNotCandidates()
        {
            var localAppData = CreateLocalAppData(@"DeveloperBuilds");
            WriteConfig(localAppData, COMPANY_FOLDER, @"Skyline-daily.exe_Url_developerbuild", @"26.1.1.238");
            WriteConfig(localAppData, COMPANY_FOLDER, @"Skyline-daily.exe_Url_olderbuild", @"25.1.1.401");

            Assert.AreEqual(0, ListCandidates(localAppData).Count);
        }

        /// <summary>
        /// An installation that was never run wrote no settings, and there is nothing to inherit
        /// from it.
        /// </summary>
        private void VerifyInstallationWithoutSettingsIsNotACandidate()
        {
            var localAppData = CreateLocalAppData(@"NeverRun");
            WriteInstallation(localAppData, INSTALLED_VERSION);

            Assert.AreEqual(0, ListCandidates(localAppData).Count);
        }

        private void VerifyOtherCompanyFoldersAreSearched()
        {
            var localAppData = CreateLocalAppData(@"OtherCompany");
            WriteInstallation(localAppData, INSTALLED_VERSION);
            var expected = WriteConfig(localAppData, @"Some_Other_Company", @"Skyline-daily.exe_Url_current",
                INSTALLED_VERSION);

            Assert.AreEqual(expected, ListCandidates(localAppData)[INSTALLED_VERSION].UserConfigFile);
        }

        private void VerifyOtherApplicationsAreIgnored()
        {
            var localAppData = CreateLocalAppData(@"OtherApplication");
            WriteInstallation(localAppData, INSTALLED_VERSION, @"Skyline");
            WriteInstallation(localAppData, INSTALLED_VERSION, @"AutoQC");
            WriteConfig(localAppData, COMPANY_FOLDER, @"Skyline.exe_Url_current", INSTALLED_VERSION);
            WriteConfig(localAppData, COMPANY_FOLDER, @"AutoQC.exe_Url_current", INSTALLED_VERSION);

            Assert.AreEqual(0, ListCandidates(localAppData).Count);
        }

        /// <summary>
        /// Candidates by version. InstalledVersions is always supplied, so that no case falls
        /// through to the registry of whatever machine the test is running on, and AssemblyName
        /// too, so the assembly handed to the constructor does not matter here.
        /// </summary>
        private static IDictionary<string, ClickOnceInstallations.Candidate> ListCandidates(string localAppData)
        {
            var ClickOnceInstallations = new StubClickOnceInstallations(typeof(ClickOnceInstallations).Assembly)
            {
                AssemblyName = ASSEMBLY_NAME,
                LocalApplicationDataFolder = localAppData,
                InstalledVersions = new[] { INSTALLED_VERSION }
            };
            return ClickOnceInstallations.ListCandidates().ToDictionary(candidate => candidate.Version);
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

        private static string WriteConfig(string localAppData, string companyFolder, string settingsFolder,
            string version)
        {
            var folder = Path.Combine(localAppData, companyFolder, settingsFolder, version);
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
            public StubClickOnceInstallations(Assembly assembly) : base(assembly)
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
