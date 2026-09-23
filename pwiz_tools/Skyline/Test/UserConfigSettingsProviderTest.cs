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

using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
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
