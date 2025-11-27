/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.GUI;
using pwiz.Skyline.Alerts;
using pwiz.SkylineTestUtil;
using System.Drawing;
using System.Linq;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AlertDlgIconsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlertDlgIcons()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testMessage = "The quick brown fox jumps over a lazy dog.";
            foreach (var message in new[]
                     {
                         testMessage,
                         string.Join(" ", Enumerable.Repeat(testMessage, 10)),
                         string.Join(" ", Enumerable.Repeat(testMessage, 100))
                     })
            {
                Size size = default;
                RunLongDlg<MessageDlg>(() => MessageDlg.Show(SkylineWindow, message), dlg =>
                {
                    PauseForScreenShot();
                    size = dlg.Size;
                }, dlg => dlg.OkDialog());
                Size sizeWithIcon = default;
                RunLongDlg<MessageDlg>(() => MessageDlg.Show(SkylineWindow, message, messageIcon: MessageIcons.Success, ignoreModeUI: false),
                    dlg =>
                    {
                        PauseForScreenShot();
                        sizeWithIcon = dlg.Size;
                    }, dlg => dlg.OkDialog());
                // The width of the form should be the same regardless of whether the icon is shown
                Assert.AreEqual(size.Width, sizeWithIcon.Width);
                // The height of the form might be larger with the icon shown
                AssertEx.IsLessThanOrEqual(size.Height, sizeWithIcon.Height);
            }
        }
    }
}
