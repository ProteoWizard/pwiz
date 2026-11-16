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
    /// Tests for <see cref="PortableSettingsProvider"/>, which puts user scoped settings in a
    /// user.config beside the executable instead of under %LOCALAPPDATA%.
    /// </summary>
    [TestClass]
    public class PortableSettingsProviderTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\PortableSettingsProviderTest.zip";
        private const string SECTION_NAME = @"pwiz.SkylineTest.FakeSettings";

        /// <summary>
        /// A folder the user created, as with a per user install or a build, keeps the user's
        /// settings beside the executable.
        /// </summary>
        [TestMethod]
        public void TestOwnerKeepsSettingsBesideExecutable()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var ownedFolder = TestFilesDir.GetTestPath(@"Owned");
            Directory.CreateDirectory(ownedFolder);
            Assert.IsTrue(PortableSettingsProvider.IsOwnedByCurrentUser(ownedFolder));
            Assert.AreEqual(ownedFolder, PortableSettingsProvider.GetConfigFolder(ownedFolder));
        }

        /// <summary>
        /// A folderowners.txt overrides the folder's owner: only the users it lists, one per
        /// line, keep their settings beside the executable, by bare name or DOMAIN\name, and an
        /// empty one lists nobody. Every case here is in a folder the current user owns, so each
        /// "not listed" result comes from the file alone.
        /// </summary>
        [TestMethod]
        public void TestFolderOwnersFileDecidesWhoSharesSettings()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var folder = TestFilesDir.GetTestPath(@"Listed");
            Directory.CreateDirectory(folder);
            var ownersFile = Path.Combine(folder, PortableSettingsProvider.FOLDER_OWNERS_FILE_NAME);
            var domainUserName = WindowsIdentity.GetCurrent().Name;
            var userName = domainUserName.Split('\\')[1];

            VerifySharesSettings(folder, ownersFile, string.Empty, false);
            VerifySharesSettings(folder, ownersFile, userName, true);
            VerifySharesSettings(folder, ownersFile, domainUserName, true);
            VerifySharesSettings(folder, ownersFile, @"someoneelse" + Environment.NewLine + userName, true);
            VerifySharesSettings(folder, ownersFile, @"someoneelse", false);
            VerifySharesSettings(folder, ownersFile, @"OTHERDOMAIN\" + userName, false);

            // A file that cannot be read lists nobody, even when it names the current user.
            File.WriteAllText(ownersFile, userName);
            using (new FileStream(ownersFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsFalse(PortableSettingsProvider.IsFolderOwner(folder));
            }
            Assert.IsTrue(PortableSettingsProvider.IsFolderOwner(folder));

            File.Delete(ownersFile);
            Assert.IsTrue(PortableSettingsProvider.IsFolderOwner(folder));
        }

        private static void VerifySharesSettings(string folder, string ownersFile, string contents, bool expectedShared)
        {
            File.WriteAllText(ownersFile, contents);
            Assert.AreEqual(expectedShared, PortableSettingsProvider.IsFolderOwner(folder), contents);
            var expectedFolder = expectedShared ? folder : PortableSettingsProvider.GetPersonalConfigFolder(folder);
            Assert.AreEqual(expectedFolder, PortableSettingsProvider.GetConfigFolder(folder));
        }

        /// <summary>
        /// A damaged user.config must not stop the program from starting.
        /// </summary>
        [TestMethod]
        public void TestUnreadableSettingsFallBackToDefaults()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
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

        private PortableSettingsProvider CreateProvider(string folderName)
        {
            var folder = TestFilesDir.GetTestPath(folderName);
            Directory.CreateDirectory(folder);
            var provider = new PortableSettingsProvider
            {
                ConfigFilePath = Path.Combine(folder, PortableSettingsProvider.CONFIG_FILE_NAME)
            };
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
