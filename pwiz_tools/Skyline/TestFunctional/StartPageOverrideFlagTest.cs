/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that --start-page=true on the command line forces the StartPage to
    /// appear even when <see cref="Settings.Default"/>.ShowStartupForm would
    /// otherwise suppress it. Exercises <see cref="Program.Main"/>'s argument
    /// parsing end-to-end via the AbstractFunctionalTest LaunchArgs hook.
    /// </summary>
    [TestClass]
    public class StartPageOverrideTrueTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageOverrideTrue()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage => true;

        protected override string[] LaunchArgs => new[] { @"--start-page=true" };

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            // ShowStartPage = true makes the framework wait for the StartPage, but we
            // flip ShowStartupForm off so the only thing surfacing the StartPage is
            // --start-page=true.
            Settings.Default.ShowStartupForm = false;
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.DoAction(skylineWindow => true));
            WaitForOpenForm<SkylineWindow>();
        }
    }

    /// <summary>
    /// Verifies that --start-page=false on the command line forces the MainWindow
    /// to open directly, even when <see cref="Settings.Default"/>.ShowStartupForm
    /// would normally route the launch through the StartPage.
    /// </summary>
    [TestClass]
    public class StartPageOverrideFalseTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageOverrideFalse()
        {
            RunFunctionalTest();
        }

        protected override bool ShowStartPage => false;

        protected override string[] LaunchArgs => new[] { @"--start-page=false" };

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            // ShowStartupForm = true would normally show the StartPage; --start-page=false
            // forces a direct MainWindow launch, bypassing the user preference.
            Settings.Default.ShowStartupForm = true;
        }

        protected override void DoTest()
        {
            Assert.IsNull(FindOpenForm<StartPage>());
        }
    }

    /// <summary>
    /// Verifies that --opendoc combined with --start-page=true opens the
    /// MainWindow first and then surfaces the StartPage as a modal dialog on top.
    /// With a bare --opendoc (no path), the MainWindow comes up with an empty
    /// document and no file path; the StartPage is layered over it.
    /// </summary>
    [TestClass]
    public class StartPageOpenDocOverrideTrueTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestStartPageOpenDocOverrideTrue()
        {
            RunFunctionalTest();
        }

        // The framework waits for MainWindow first (ShowStartPage = false / default);
        // DoTest waits for the StartPage modal that OnShown surfaces afterward.
        protected override string[] LaunchArgs => new[] { @"--opendoc", @"--start-page=true" };

        protected override void DoTest()
        {
            // No --opendoc path -> empty new document, no file path.
            RunUI(() =>
            {
                Assert.IsNull(SkylineWindow.DocumentFilePath);
                Assert.AreEqual(0, SkylineWindow.Document.PeptideCount);
            });
            var startPage = WaitForOpenForm<StartPage>();
            // Closing the modal returns control to MainWindow; the framework cleans up.
            OkDialog(startPage, startPage.Close);
        }
    }
}
