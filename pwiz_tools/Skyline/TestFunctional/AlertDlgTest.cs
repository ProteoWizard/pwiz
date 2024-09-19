/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AlertDlgTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlertDlg()
        {
            TestFilesZip = @"TestFunctional\AlertDlgTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SmallMoleculePredictedRetentionTime.sky"));
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.small_molecules, SkylineWindow.Document.DocumentType);
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.small_molecules, SkylineWindow.ModeUI);
            });

            // When we try to import results, there will be an alert saying that the retention time filter has said to use
            // predicted retention time but there is no predictor.
            // Make sure that the message says to go to "Molecule Settings" and not "Peptide Settings" since this is a small
            // molecule document.
            RunDlg<AlertDlg>(SkylineWindow.ImportResults, alertDlg=>
            {
                Assert.AreEqual(Program.Name, alertDlg.Text);
                var peptideMessage = Resources.SkylineWindow_CheckRetentionTimeFilter_NoPredictionAlgorithm;
                var expectedMessage =
                    Helpers.PeptideToMoleculeTextMapper.Translate(peptideMessage,
                        SrmDocument.DOCUMENT_TYPE.small_molecules);
                Assert.AreNotEqual(peptideMessage, expectedMessage);

                // "alertDlg.Message" contains the original, untranslated message
                // Find the "labelMessage" control to get the text that has been translated by "PeptideToMoleculeTextMapper".
                var labelMessage = alertDlg.Controls.Find("labelMessage", true).OfType<Label>().Single();
                var actualMessage = labelMessage.Text;

                Assert.AreEqual(expectedMessage, actualMessage);
                alertDlg.OkDialog();
            });
        }
    }
}
