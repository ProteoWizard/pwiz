/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests that a ClickOnce update finds the previous version's user.config, which the
    /// .NET settings system cannot do for itself once ClickOnce awareness is gone (net8).
    /// See <see cref="UserConfigMigrator"/> for why the file is where it is.
    /// </summary>
    [TestClass]
    public class UserConfigMigratorTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\UserConfigMigratorTest.zip";

        private const string PRODUCT = "Skyline-daily";
        private const string OTHER_PRODUCT = "SkylineTester";
        private const string STORE_FOLDER = "skyl..tion_a58fdacee3bae943_001a.0002_0ab912e460c3db70";
        private const string OTHER_STORE_FOLDER = "othe..tion_a58fdacee3bae943_001a.0002_1234567890abcdef";

        private const string CURRENT_VERSION = "26.2.1.237";
        private const string CLICKONCE_VERSION = "26.1.1.238";
        private const string SIBLING_VERSION = "26.1.1.237";
        private const string NEWER_VERSION = "27.1.1.001";

        private string _root;
        private string _companyDirectory;
        private string _storeRoot;
        private string _currentConfigPath;

        [TestMethod]
        public void TestUserConfigMigration()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            _root = TestFilesDir.GetTestPath("configs");
            _companyDirectory = Path.Combine(_root, "pwiz");
            _storeRoot = Path.Combine(_root, "Apps", "2.0");
            _currentConfigPath = ConfigPath(ProductDirectory(PRODUCT, "ch4sk0ht"), CURRENT_VERSION);

            // The ClickOnce data directory this deployment inherited from the net472 version it
            // replaced, and an older net8 version's own settings folder. Both are valid sources.
            WriteClickOnceConfig(STORE_FOLDER, CLICKONCE_VERSION, typeof(Settings).FullName);
            WriteConfig(ConfigPath(ProductDirectory(PRODUCT, "x2ysvltp"), SIBLING_VERSION),
                typeof(Settings).FullName);

            // Distractors: a different application whose deployment folder does not match, a
            // different product under the same company, and a version above the running one.
            WriteClickOnceConfig(OTHER_STORE_FOLDER, CLICKONCE_VERSION, typeof(Settings).FullName);
            WriteConfig(ConfigPath(ProductDirectory(OTHER_PRODUCT, "zzzzzzzz"), CLICKONCE_VERSION),
                typeof(Settings).FullName);
            WriteConfig(ConfigPath(ProductDirectory(PRODUCT, "n3w3rrrr"), NEWER_VERSION),
                typeof(Settings).FullName);

            // The highest version below the running one wins, matching Upgrade() semantics.
            var migrator = CreateMigrator(Path.Combine(_storeRoot, "AAAAAAAA.AAA", "BBBBBBBB.BBB", STORE_FOLDER));
            AssertEx.IsTrue(migrator.Migrate());
            AssertEx.IsNull(migrator.Error);
            AssertEx.AreEqual(ClickOnceConfigPath(STORE_FOLDER, CLICKONCE_VERSION), migrator.SourceConfigPath);
            AssertEx.FileExists(_currentConfigPath);
            AssertEx.AreEqual(File.ReadAllText(ClickOnceConfigPath(STORE_FOLDER, CLICKONCE_VERSION)),
                File.ReadAllText(_currentConfigPath));

            // Running again must not overwrite the settings the user has since changed.
            File.WriteAllText(_currentConfigPath, "<configuration />");
            var second = CreateMigrator(Path.Combine(_storeRoot, "AAAAAAAA.AAA", "BBBBBBBB.BBB", STORE_FOLDER));
            AssertEx.IsFalse(second.Migrate());
            AssertEx.AreEqual("<configuration />", File.ReadAllText(_currentConfigPath));

            // With the ClickOnce data directory gone, the older net8 sibling is next in line.
            File.Delete(_currentConfigPath);
            Directory.Delete(Path.GetDirectoryName(ClickOnceConfigPath(STORE_FOLDER, CLICKONCE_VERSION)), true);
            var third = CreateMigrator(Path.Combine(_storeRoot, "AAAAAAAA.AAA", "BBBBBBBB.BBB", STORE_FOLDER));
            AssertEx.IsTrue(third.Migrate());
            AssertEx.AreEqual(ConfigPath(ProductDirectory(PRODUCT, "x2ysvltp"), SIBLING_VERSION),
                third.SourceConfigPath);

            // A build run from its own output directory keeps its own settings.
            File.Delete(_currentConfigPath);
            var notInstalled = CreateMigrator(Path.Combine(_root, "bin", "Release"));
            AssertEx.IsFalse(notInstalled.Migrate());
            AssertEx.IsFalse(File.Exists(_currentConfigPath));

            // A file that is not one of this application's settings files is never copied.
            File.Delete(ConfigPath(ProductDirectory(PRODUCT, "x2ysvltp"), SIBLING_VERSION));
            WriteConfig(ConfigPath(ProductDirectory(PRODUCT, "x2ysvltp"), SIBLING_VERSION),
                "some.other.Settings");
            var wrongSection = CreateMigrator(Path.Combine(_storeRoot, "AAAAAAAA.AAA", "BBBBBBBB.BBB", STORE_FOLDER));
            AssertEx.IsFalse(wrongSection.Migrate());
            AssertEx.IsNull(wrongSection.Error);
        }

        private UserConfigMigrator CreateMigrator(string applicationDirectory)
        {
            return new UserConfigMigrator
            {
                CurrentConfigPath = _currentConfigPath,
                ApplicationDirectory = applicationDirectory,
                ClickOnceStoreRoot = _storeRoot,
                SectionName = typeof(Settings).FullName
            };
        }

        private string ProductDirectory(string product, string hash)
        {
            return Path.Combine(_companyDirectory, product + "_Url_" + hash);
        }

        private static string ConfigPath(string productDirectory, string version)
        {
            return Path.Combine(productDirectory, version, "user.config");
        }

        private string ClickOnceConfigPath(string storeFolder, string version)
        {
            return Path.Combine(_storeRoot, "Data", "PPPPPPPP.PPP", "QQQQQQQQ.QQQ", storeFolder,
                "Data", version, "user.config");
        }

        private void WriteClickOnceConfig(string storeFolder, string version, string sectionName)
        {
            WriteConfig(ClickOnceConfigPath(storeFolder, version), sectionName);
        }

        private static void WriteConfig(string path, string sectionName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<configuration>\r\n" +
                "    <userSettings>\r\n" +
                "        <" + sectionName + ">\r\n" +
                "            <setting name=\"UIMode\" serializeAs=\"String\">\r\n" +
                "                <value>" + Path.GetFileName(Path.GetDirectoryName(path)) + "</value>\r\n" +
                "            </setting>\r\n" +
                "        </" + sectionName + ">\r\n" +
                "    </userSettings>\r\n" +
                "</configuration>\r\n");
        }
    }
}
