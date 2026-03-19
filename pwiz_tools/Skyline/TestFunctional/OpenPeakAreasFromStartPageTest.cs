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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests opening a Skyline document from the start page where the AreaReplicateGraphPane is displayed
    /// and has more than one pane. This exercises a bug where <see cref="pwiz.Common.SystemUtil.Caching.Receiver"/>
    /// was being constructed before the WindowsFormsSynchronizationContext was created.
    /// </summary>
    [TestClass]
    public class OpenPeakAreasFromStartPageTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOpenPeakAreasFromStartPage()
        {
            TestFilesZip = @"TestFunctional\OpenPeakAreasFromStartPage.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.OpenFile(TestFilesDir.GetTestPath("OpenPeakAreasFromStartPage.sky")));
            WaitForOpenForm<SkylineWindow>();
            WaitForGraphs();
        }
    }
}
