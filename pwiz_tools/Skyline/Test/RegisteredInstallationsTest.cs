/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests for <see cref="RegisteredInstallations"/>, which finds the Skylines that an
    /// installer registered in Programs and Features.
    /// </summary>
    [TestClass]
    public class RegisteredInstallationsTest : AbstractUnitTest
    {
        private const string TEST_ZIP_PATH = @"Test\RegisteredInstallationsTest.zip";
        private const string DAILY = @"Skyline-daily";
        private const string RELEASE = @"Skyline";
        private const string INSTALLED_VERSION = @"26.2.1.100";

        [TestMethod]
        public void TestRegisteredInstallations()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            var installed = WriteInstallation(@"Installed", DAILY, true);
            var neverRun = WriteInstallation(@"NeverRun", DAILY, false);
            var otherProduct = WriteInstallation(@"OtherProduct", RELEASE, true);
            var uninstallCommand = @"""" + Path.Combine(installed, @"unins000.exe") + @"""";
            var entries = new[]
            {
                // Registered with a trailing separator, the way Inno Setup writes InstallLocation
                new RegisteredInstallations.UninstallEntry
                {
                    DisplayVersion = INSTALLED_VERSION,
                    InstallLocation = installed + Path.DirectorySeparatorChar,
                    UninstallCommand = uninstallCommand
                },
                // The same installation seen through the other registry view
                new RegisteredInstallations.UninstallEntry
                {
                    DisplayVersion = INSTALLED_VERSION,
                    InstallLocation = installed,
                    UninstallCommand = uninstallCommand
                },
                // Installed but never run, so it has no settings to offer
                new RegisteredInstallations.UninstallEntry
                {
                    DisplayVersion = @"26.2.1.101",
                    InstallLocation = neverRun,
                    UninstallCommand = uninstallCommand
                },
                // The other product, which is a different search
                new RegisteredInstallations.UninstallEntry
                {
                    DisplayVersion = @"26.1.0.100",
                    InstallLocation = otherProduct,
                    UninstallCommand = uninstallCommand
                },
                // Some other program, registered without a location
                new RegisteredInstallations.UninstallEntry { DisplayVersion = @"1.0" }
            };

            var found = new StubRegisteredInstallations(DAILY, entries).ListInstallations().ToList();
            Assert.AreEqual(1, found.Count);
            var installation = found[0];
            Assert.AreEqual(DAILY, installation.ProductName);
            Assert.AreEqual(INSTALLED_VERSION, installation.Version);
            Assert.AreEqual(installed, installation.ExecutableFolder);
            Assert.AreEqual(Path.Combine(installed, @"user.config"), installation.UserConfigFile);
            Assert.IsTrue(installation.IsCurrentlyInstalled);
            Assert.IsTrue(installation.CanUninstall);
            Assert.AreEqual(uninstallCommand, installation.UninstallCommand);

            var otherFound = new StubRegisteredInstallations(RELEASE, entries).ListInstallations().ToList();
            Assert.AreEqual(1, otherFound.Count);
            Assert.AreEqual(RELEASE, otherFound[0].ProductName);
            Assert.AreEqual(otherProduct, otherFound[0].ExecutableFolder);

            // A registered folder is one whether or not it has been run, and however the
            // separator and case were written
            var registered = new StubRegisteredInstallations(DAILY, entries);
            AssertEx.IsTrue(registered.IsInstallationFolder(installed));
            AssertEx.IsTrue(registered.IsInstallationFolder(neverRun + Path.DirectorySeparatorChar));
            AssertEx.IsTrue(registered.IsInstallationFolder(otherProduct.ToUpperInvariant()));
            AssertEx.IsFalse(registered.IsInstallationFolder(TestFilesDir.GetTestPath(@"Unregistered")));
            AssertEx.IsFalse(registered.IsInstallationFolder(null));
        }

        /// <summary>
        /// An installation folder, holding the executable and, when it has been run, the
        /// settings it wrote beside that.
        /// </summary>
        private string WriteInstallation(string name, string productName, bool withSettings)
        {
            var folder = TestFilesDir.GetTestPath(name);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, productName + @".exe"), string.Empty);
            if (withSettings)
                File.WriteAllText(Path.Combine(folder, @"user.config"), @"<configuration><userSettings /></configuration>");
            return folder;
        }

        private class StubRegisteredInstallations : RegisteredInstallations
        {
            private readonly IEnumerable<UninstallEntry> _entries;

            public StubRegisteredInstallations(string productName, IEnumerable<UninstallEntry> entries)
                : base(productName)
            {
                _entries = entries;
            }

            protected override IEnumerable<UninstallEntry> ReadUninstallEntries()
            {
                return _entries;
            }
        }
    }
}
