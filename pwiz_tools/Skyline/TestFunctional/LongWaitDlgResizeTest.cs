/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LongWaitDlgResizeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLongWaitDlgResize()
        {
            // IsPauseForScreenShots = true;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var longWaitDlg = ShowDialog<LongWaitDlg>(() =>
            {
                using (var longWaitDlg2 = new LongWaitDlg())
                {
                    longWaitDlg2.PerformWork(SkylineWindow, 1, (ILongWaitBroker longWaitBroker) =>
                    {
                        while (!longWaitDlg2.IsCanceled)
                        {
                            longWaitDlg2.CancellationToken.WaitHandle.WaitOne();
                        }
                    });
                }
            });
            Size originalSize = default(Size);
            RunUI(() => { originalSize = longWaitDlg.Size; });
            PauseForScreenShot();
            SetMessage(longWaitDlg, "TheQuickBrownFoxJumpsOverTheFirstLazyDog"
                                    +"ThenTheQuickBrownFoxJumpsOverTheSecondLazyDog"
                                    +"ThenTheQuickBrownFoxJumpsOverTheThirdLazyDog");
            Size newSize = default(Size);
            RunUI(()=>newSize = longWaitDlg.Size);
            PauseForScreenShot();
            Assert.AreEqual(originalSize.Width, newSize.Width);
            Assert.AreNotEqual(originalSize.Height, newSize.Height);
            Assert.IsTrue(newSize.Height > originalSize.Height);
            OkDialog(longWaitDlg, longWaitDlg.Close);
        }

        private void SetMessage(LongWaitDlg longWaitDlg, string text)
        {
            longWaitDlg.Message = text;
            WaitForConditionUI(() =>
            {
                var labelMessage = (Label) longWaitDlg.Controls.Find("labelMessage", true).FirstOrDefault();
                Assert.IsNotNull(labelMessage);
                return labelMessage.Text == text;
            });
        }
    }
}
