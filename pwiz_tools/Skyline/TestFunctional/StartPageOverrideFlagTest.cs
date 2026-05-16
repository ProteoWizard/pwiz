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
    /// Verifies that <see cref="Program.StartPageOverride"/> = true (from the
    /// --start-page=true launch flag) forces the StartPage to appear even when
    /// <see cref="Settings.Default"/>.ShowStartupForm would otherwise suppress it.
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

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            // ShowStartPage = true makes the test framework wait for StartPage,
            // but we flip ShowStartupForm off so the only thing surfacing the
            // StartPage is the override flag below.
            Settings.Default.ShowStartupForm = false;
            Program.StartPageOverride = true;
        }

        protected override void DoTest()
        {
            try
            {
                var startPage = WaitForOpenForm<StartPage>();
                Assert.IsNotNull(startPage);
                RunUI(() => startPage.DoAction(skylineWindow => true));
                WaitForOpenForm<SkylineWindow>();
            }
            finally
            {
                Program.StartPageOverride = null;
            }
        }
    }

    /// <summary>
    /// Verifies that <see cref="Program.StartPageOverride"/> = false (from the
    /// --start-page=false launch flag) forces the MainWindow to open directly,
    /// even when <see cref="Settings.Default"/>.ShowStartupForm would normally
    /// route the launch through the StartPage.
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

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();
            // ShowStartupForm = true would normally show the StartPage; the override
            // forces a direct MainWindow launch, bypassing the user preference.
            Settings.Default.ShowStartupForm = true;
            Program.StartPageOverride = false;
        }

        protected override void DoTest()
        {
            try
            {
                Assert.IsNull(FindOpenForm<StartPage>());
                Assert.IsNotNull(SkylineWindow);
            }
            finally
            {
                Program.StartPageOverride = null;
            }
        }
    }
}
