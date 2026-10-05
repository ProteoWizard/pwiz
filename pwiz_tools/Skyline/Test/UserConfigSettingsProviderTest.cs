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

using System;
using System.Configuration;
using System.IO;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="UserConfigSettingsProvider"/>, which puts user scoped settings in a
    /// user.config beside the executable instead of under %LOCALAPPDATA%.
    /// </summary>
    [TestClass]
    public class UserConfigSettingsProviderTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\UserConfigSettingsProviderTest.zip";
        private const string SECTION_NAME = @"pwiz.SkylineTest.FakeSettings";

        [TestMethod]
        public void TestUserConfigSettingsProvider()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            VerifyNonOwnersGetSettingsOfTheirOwn();
            VerifyEachInstallationGetsItsOwnPersonalFolder();
            VerifyFolderOwnersFileDecidesWhoSharesSettings();
            VerifyUnreadableFileFallsBackToDefaults();
        }

        /// <summary>
        /// An installation folder the user owns keeps its settings beside the executable. One
        /// the user does not, like System32, which belongs to TrustedInstaller much as a per
        /// machine install belongs to Administrators, sends them to a folder of the user's own.
        /// </summary>
        private void VerifyNonOwnersGetSettingsOfTheirOwn()
        {
            var ownedFolder = TestFilesDir.GetTestPath(@"Owned");
            Directory.CreateDirectory(ownedFolder);
            Assert.IsTrue(UserConfigSettingsProvider.IsOwnedByCurrentUser(ownedFolder));
            Assert.AreEqual(ownedFolder, UserConfigSettingsProvider.GetConfigFolder(ownedFolder));

            var systemFolder = Environment.SystemDirectory;
            Assert.IsFalse(UserConfigSettingsProvider.IsOwnedByCurrentUser(systemFolder));
            Assert.AreEqual(UserConfigSettingsProvider.GetPersonalConfigFolder(systemFolder),
                UserConfigSettingsProvider.GetConfigFolder(systemFolder));
        }

        /// <summary>
        /// A per machine install directly under Program Files keeps its plain folder name.
        /// Anywhere else, including Program Files (x86), where Skyline is never installed, the
        /// name gets a checksum of the path, so folders that share a name get different personal
        /// folders, while the same folder spelled differently does not. Every installation's
        /// personal folder sits in one folder under %LOCALAPPDATA%, whatever that is called.
        /// </summary>
        private void VerifyEachInstallationGetsItsOwnPersonalFolder()
        {
            const string productFolderName = @"ExampleProductName";

            var programFilesInstall = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), productFolderName);
            var programFilesPersonalFolder = UserConfigSettingsProvider.GetPersonalConfigFolder(programFilesInstall);
            Assert.AreEqual(productFolderName, Path.GetFileName(programFilesPersonalFolder));
            var personalRoot = Path.GetDirectoryName(programFilesPersonalFolder);
            Assert.AreEqual(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.GetDirectoryName(personalRoot));

            var x86Install = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), productFolderName);
            VerifyPersonalFolderIsMadeUnique(personalRoot, productFolderName, x86Install);

            const string buildFolderName = @"net10.0-windows";
            var debugFolder = TestFilesDir.GetTestPath(Path.Combine(@"Debug", buildFolderName));
            var releaseFolder = TestFilesDir.GetTestPath(Path.Combine(@"Release", buildFolderName));
            var debugPersonalFolder = VerifyPersonalFolderIsMadeUnique(personalRoot, buildFolderName, debugFolder);
            var releasePersonalFolder = VerifyPersonalFolderIsMadeUnique(personalRoot, buildFolderName, releaseFolder);
            Assert.AreNotEqual(debugPersonalFolder, releasePersonalFolder);

            // The folder name keeps the case it was launched with, which Windows ignores.
            Assert.IsTrue(string.Equals(debugPersonalFolder,
                UserConfigSettingsProvider.GetPersonalConfigFolder(debugFolder.ToUpperInvariant()),
                StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(debugPersonalFolder,
                UserConfigSettingsProvider.GetPersonalConfigFolder(debugFolder + Path.DirectorySeparatorChar));
        }

        private static string VerifyPersonalFolderIsMadeUnique(string personalRoot, string folderName, string installationFolder)
        {
            var personalFolder = UserConfigSettingsProvider.GetPersonalConfigFolder(installationFolder);
            Assert.AreEqual(personalRoot, Path.GetDirectoryName(personalFolder));
            var personalFolderName = Path.GetFileName(personalFolder);
            Assert.IsTrue(personalFolderName.StartsWith(folderName + @"_"), personalFolderName);
            Assert.IsTrue(uint.TryParse(personalFolderName.Substring(folderName.Length + 1), out _), personalFolderName);
            return personalFolder;
        }

        /// <summary>
        /// A folderowners.txt overrides the folder's owner: only the users it lists keep their
        /// settings beside the executable, by bare name or DOMAIN\name, and an empty one lists
        /// nobody. Every case here is in a folder the current user owns, so each "not listed"
        /// result comes from the file alone.
        /// </summary>
        private void VerifyFolderOwnersFileDecidesWhoSharesSettings()
        {
            var folder = TestFilesDir.GetTestPath(@"Listed");
            Directory.CreateDirectory(folder);
            var ownersFile = Path.Combine(folder, UserConfigSettingsProvider.FOLDER_OWNERS_FILE_NAME);
            var domainUserName = WindowsIdentity.GetCurrent().Name;
            var parts = domainUserName.Split('\\');
            var domain = parts[0];
            var userName = parts[1];

            VerifySharesSettings(folder, ownersFile,string.Empty, false);
            VerifySharesSettings(folder, ownersFile,userName, true);
            VerifySharesSettings(folder, ownersFile,userName.ToUpperInvariant(), true);
            VerifySharesSettings(folder, ownersFile,domainUserName, true);
            VerifySharesSettings(folder, ownersFile,domain + @"/" + userName, true);
            VerifySharesSettings(folder, ownersFile,@"  " + userName + @"  ", true);
            VerifySharesSettings(folder, ownersFile,@"someoneelse" + Environment.NewLine + userName, true);
            VerifySharesSettings(folder, ownersFile,@"someoneelse", false);
            VerifySharesSettings(folder, ownersFile,@"OTHERDOMAIN\" + userName, false);

            // A file that cannot be read lists nobody, even when it names the current user.
            File.WriteAllText(ownersFile, userName);
            using (new FileStream(ownersFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsFalse(UserConfigSettingsProvider.IsFolderOwner(folder));
            }
            Assert.IsTrue(UserConfigSettingsProvider.IsFolderOwner(folder));

            File.Delete(ownersFile);
            Assert.IsTrue(UserConfigSettingsProvider.IsFolderOwner(folder));
        }

        private static void VerifySharesSettings(string folder, string ownersFile, string contents, bool expectedShared)
        {
            File.WriteAllText(ownersFile, contents);
            Assert.AreEqual(expectedShared, UserConfigSettingsProvider.IsFolderOwner(folder), contents);
            var expectedFolder = expectedShared ? folder : UserConfigSettingsProvider.GetPersonalConfigFolder(folder);
            Assert.AreEqual(expectedFolder, UserConfigSettingsProvider.GetConfigFolder(folder));
        }

        /// <summary>
        /// A damaged user.config must not stop the program from starting.
        /// </summary>
        private void VerifyUnreadableFileFallsBackToDefaults()
        {
            var provider = CreateProvider(@"Corrupt");
            File.WriteAllText(provider.ConfigFilePath, @"<configuration><userSettings>truncated");
            var values = provider.GetPropertyValues(CreateContext(SECTION_NAME), CreateProperties());
            Assert.AreEqual(@"defaultText", values[@"TextSetting"].PropertyValue);

            // And saving over it produces a file that reads back.
            values[@"TextSetting"].PropertyValue = @"recovered";
            provider.SetPropertyValues(CreateContext(SECTION_NAME), values);
            Assert.AreEqual(@"recovered",
                CreateProvider(@"Corrupt").GetPropertyValues(CreateContext(SECTION_NAME), CreateProperties())[@"TextSetting"]
                    .PropertyValue);
        }

        private UserConfigSettingsProvider CreateProvider(string folderName)
        {
            var folder = TestFilesDir.GetTestPath(folderName);
            Directory.CreateDirectory(folder);
            var provider = new UserConfigSettingsProvider { ConfigFolder = folder };
            provider.Initialize(null, null);
            return provider;
        }

        private static SettingsContext CreateContext(string sectionName)
        {
            return new SettingsContext { [@"GroupName"] = sectionName };
        }

        private static SettingsPropertyCollection CreateProperties()
        {
            var properties = new SettingsPropertyCollection();
            properties.Add(CreateProperty(@"TextSetting", typeof(string), @"defaultText",
                SettingsSerializeAs.String));
            return properties;
        }

        private static SettingsProperty CreateProperty(string name, Type propertyType, object defaultValue,
            SettingsSerializeAs serializeAs)
        {
            var property = new SettingsProperty(name)
            {
                PropertyType = propertyType,
                SerializeAs = serializeAs,
                DefaultValue = defaultValue
            };
            property.Attributes.Add(typeof(UserScopedSettingAttribute), new UserScopedSettingAttribute());
            return property;
        }
    }
}
