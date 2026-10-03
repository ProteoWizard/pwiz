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
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
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
        private const string OTHER_SECTION_NAME = @"pwiz.SkylineTest.OtherFakeSettings";

        [TestMethod]
        public void TestUserConfigSettingsProvider()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            VerifyDefaultLocation();
            VerifyFolderOwnership();
            VerifyPersonalFolderNames();
            VerifyFolderOwnersFile();
            VerifySkylineSettingsUseProvider();
            VerifyDefaultsWhenNoFile();
            VerifyRoundTrip();
            VerifyOnlyChangedSettingsAreStored();
            VerifySectionsDoNotDisturbEachOther();
            VerifyUnreadableFileFallsBackToDefaults();
            VerifyReset();
        }

        /// <summary>
        /// Out of the box the file sits next to the assembly, which in an installation is the
        /// folder holding Skyline.exe, Skyline-daily.exe and SkylineCmd.exe.
        /// </summary>
        private void VerifyDefaultLocation()
        {
            var provider = new UserConfigSettingsProvider();
            var expectedFolder = Path.GetDirectoryName(typeof(UserConfigSettingsProvider).Assembly.Location);
            Assert.AreEqual(expectedFolder, provider.ConfigFolder);
            Assert.AreEqual(Path.Combine(expectedFolder, @"user.config"), provider.ConfigFilePath);
        }

        /// <summary>
        /// An installation folder the user owns keeps its settings beside the executable. One
        /// the user does not, like System32, which belongs to TrustedInstaller much as a per
        /// machine install belongs to Administrators, sends them to a folder of the user's own.
        /// </summary>
        private void VerifyFolderOwnership()
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
        private void VerifyPersonalFolderNames()
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
            VerifyChecksumFolderName(personalRoot, productFolderName, x86Install);

            const string buildFolderName = @"net10.0-windows";
            var debugFolder = TestFilesDir.GetTestPath(Path.Combine(@"Debug", buildFolderName));
            var releaseFolder = TestFilesDir.GetTestPath(Path.Combine(@"Release", buildFolderName));
            var debugPersonalFolder = VerifyChecksumFolderName(personalRoot, buildFolderName, debugFolder);
            var releasePersonalFolder = VerifyChecksumFolderName(personalRoot, buildFolderName, releaseFolder);
            Assert.AreNotEqual(debugPersonalFolder, releasePersonalFolder);

            // The folder name keeps the case it was launched with, which Windows ignores.
            Assert.IsTrue(string.Equals(debugPersonalFolder,
                UserConfigSettingsProvider.GetPersonalConfigFolder(debugFolder.ToUpperInvariant()),
                StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(debugPersonalFolder,
                UserConfigSettingsProvider.GetPersonalConfigFolder(debugFolder + Path.DirectorySeparatorChar));
        }

        private static string VerifyChecksumFolderName(string personalRoot, string folderName, string installationFolder)
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
        private void VerifyFolderOwnersFile()
        {
            var folder = TestFilesDir.GetTestPath(@"Listed");
            Directory.CreateDirectory(folder);
            var ownersFile = Path.Combine(folder, UserConfigSettingsProvider.FOLDER_OWNERS_FILE_NAME);
            var domainUserName = WindowsIdentity.GetCurrent().Name;
            var parts = domainUserName.Split('\\');
            var domain = parts[0];
            var userName = parts[1];

            VerifyFolderOwnersFile(folder, ownersFile, string.Empty, false);
            VerifyFolderOwnersFile(folder, ownersFile, userName, true);
            VerifyFolderOwnersFile(folder, ownersFile, userName.ToUpperInvariant(), true);
            VerifyFolderOwnersFile(folder, ownersFile, domainUserName, true);
            VerifyFolderOwnersFile(folder, ownersFile, domain + @"/" + userName, true);
            VerifyFolderOwnersFile(folder, ownersFile, @"  " + userName + @"  ", true);
            VerifyFolderOwnersFile(folder, ownersFile, @"someoneelse" + Environment.NewLine + userName, true);
            VerifyFolderOwnersFile(folder, ownersFile, @"someoneelse", false);
            VerifyFolderOwnersFile(folder, ownersFile, @"OTHERDOMAIN\" + userName, false);

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

        private static void VerifyFolderOwnersFile(string folder, string ownersFile, string contents, bool expectedOwner)
        {
            File.WriteAllText(ownersFile, contents);
            Assert.AreEqual(expectedOwner, UserConfigSettingsProvider.IsFolderOwner(folder), contents);
            var expectedFolder = expectedOwner ? folder : UserConfigSettingsProvider.GetPersonalConfigFolder(folder);
            Assert.AreEqual(expectedFolder, UserConfigSettingsProvider.GetConfigFolder(folder));
        }

        /// <summary>
        /// The attribute lives on the hand written half of the partial Settings class. Should the
        /// settings designer ever regenerate over it, or the attribute get dropped, Skyline would
        /// silently go back to reading %LOCALAPPDATA%, so assert the wiring directly.
        /// </summary>
        private void VerifySkylineSettingsUseProvider()
        {
            var attribute = typeof(Skyline.Properties.Settings)
                .GetCustomAttribute<SettingsProviderAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(typeof(UserConfigSettingsProvider).AssemblyQualifiedName,
                attribute.ProviderTypeName);
        }

        private void VerifyDefaultsWhenNoFile()
        {
            var provider = CreateProvider(@"NoFileYet");
            Assert.IsFalse(File.Exists(provider.ConfigFilePath));
            var values = provider.GetPropertyValues(CreateContext(SECTION_NAME), CreateProperties());
            Assert.AreEqual(@"defaultText", values[@"TextSetting"].PropertyValue);
            Assert.AreEqual(7, values[@"NumberSetting"].PropertyValue);
        }

        private void VerifyRoundTrip()
        {
            var provider = CreateProvider(@"RoundTrip");
            var context = CreateContext(SECTION_NAME);

            var written = provider.GetPropertyValues(context, CreateProperties());
            written[@"TextSetting"].PropertyValue = @"changed";
            written[@"NumberSetting"].PropertyValue = 42;
            var listValue = new StringCollection();
            listValue.AddRange(new[] { @"first", @"second" });
            written[@"ListSetting"].PropertyValue = listValue;
            provider.SetPropertyValues(context, written);

            Assert.IsTrue(File.Exists(provider.ConfigFilePath));
            // The Xml serialized setting has to be stored as an element, the way
            // LocalFileSettingsProvider stores it, not as escaped text.
            var storedList = ReadSettingElement(provider, SECTION_NAME, @"ListSetting");
            Assert.AreEqual(@"Xml", (string) storedList.Attribute(@"serializeAs"));
            Assert.IsNotNull(storedList.Element(@"value")?.Elements().FirstOrDefault());

            // A second provider, reading the file fresh, sees what the first one wrote.
            var read = CreateProvider(@"RoundTrip").GetPropertyValues(context, CreateProperties());
            Assert.AreEqual(@"changed", read[@"TextSetting"].PropertyValue);
            Assert.AreEqual(42, read[@"NumberSetting"].PropertyValue);
            CollectionAssert.AreEqual(listValue, (StringCollection) read[@"ListSetting"].PropertyValue);
        }

        /// <summary>
        /// Skyline has several hundred settings and a user changes a handful, so only the changed
        /// ones belong in the file. The other half of that rule matters more: once a setting has
        /// been written, a later save that does not touch it must not drop it.
        /// </summary>
        private void VerifyOnlyChangedSettingsAreStored()
        {
            var provider = CreateProvider(@"OnlyChanged");
            var context = CreateContext(SECTION_NAME);
            var values = provider.GetPropertyValues(context, CreateProperties());
            values[@"TextSetting"].PropertyValue = @"changed";
            provider.SetPropertyValues(context, values);

            Assert.IsNotNull(ReadSettingElement(provider, SECTION_NAME, @"TextSetting"));
            Assert.IsNull(ReadSettingElement(provider, SECTION_NAME, @"NumberSetting"));

            // Save again from a fresh read, changing something else. The value stored above is
            // not dirty this time round, and must survive anyway.
            var reopened = CreateProvider(@"OnlyChanged");
            var reloaded = reopened.GetPropertyValues(context, CreateProperties());
            reloaded[@"NumberSetting"].PropertyValue = 99;
            reopened.SetPropertyValues(context, reloaded);

            var final = CreateProvider(@"OnlyChanged").GetPropertyValues(context, CreateProperties());
            Assert.AreEqual(@"changed", final[@"TextSetting"].PropertyValue);
            Assert.AreEqual(99, final[@"NumberSetting"].PropertyValue);
        }

        /// <summary>
        /// Several settings classes share one user.config, each in its own section, so saving one
        /// must leave the others alone.
        /// </summary>
        private void VerifySectionsDoNotDisturbEachOther()
        {
            var provider = CreateProvider(@"TwoSections");

            var firstContext = CreateContext(SECTION_NAME);
            var firstValues = provider.GetPropertyValues(firstContext, CreateProperties());
            firstValues[@"TextSetting"].PropertyValue = @"fromFirst";
            provider.SetPropertyValues(firstContext, firstValues);

            var secondContext = CreateContext(OTHER_SECTION_NAME);
            var secondValues = provider.GetPropertyValues(secondContext, CreateProperties());
            secondValues[@"TextSetting"].PropertyValue = @"fromSecond";
            provider.SetPropertyValues(secondContext, secondValues);

            var reread = CreateProvider(@"TwoSections");
            Assert.AreEqual(@"fromFirst",
                reread.GetPropertyValues(firstContext, CreateProperties())[@"TextSetting"].PropertyValue);
            Assert.AreEqual(@"fromSecond",
                reread.GetPropertyValues(secondContext, CreateProperties())[@"TextSetting"].PropertyValue);
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

        private void VerifyReset()
        {
            var provider = CreateProvider(@"Reset");
            var context = CreateContext(SECTION_NAME);
            var values = provider.GetPropertyValues(context, CreateProperties());
            values[@"TextSetting"].PropertyValue = @"changed";
            provider.SetPropertyValues(context, values);
            Assert.IsNotNull(ReadSettingElement(provider, SECTION_NAME, @"TextSetting"));

            provider.Reset(context);
            Assert.AreEqual(@"defaultText",
                provider.GetPropertyValues(context, CreateProperties())[@"TextSetting"].PropertyValue);
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
            properties.Add(CreateProperty(@"NumberSetting", typeof(int), @"7", SettingsSerializeAs.String));
            properties.Add(CreateProperty(@"ListSetting", typeof(StringCollection), null,
                SettingsSerializeAs.Xml));
            return properties;
        }

        private static SettingsProperty CreateProperty(string name, System.Type propertyType, object defaultValue,
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

        private static XElement ReadSettingElement(UserConfigSettingsProvider provider, string sectionName,
            string settingName)
        {
            var document = XDocument.Load(provider.ConfigFilePath);
            var section = document.Root?.Element(@"userSettings")?.Element(sectionName);
            return section?.Elements(@"setting")
                .FirstOrDefault(el => settingName == (string) el.Attribute(@"name"));
        }
    }
}
