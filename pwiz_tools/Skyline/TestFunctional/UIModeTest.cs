/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{   
    /// <summary>
    /// Functional test for UI Mode handling.
    /// </summary>
    [TestClass]
    public class UIModeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void UIModeSettingsTest()
        {
            TestFilesZip = @"TestFunctional\UIModeTest.zip";
            RunFunctionalTest();
        }


        /// <summary>
        /// Test tree state restoration from a persistent string. Tests for proper expansion and
        /// selection of nodes, and correct vertical scrolling
        /// </summary>
        protected override void DoTest()
        {
            // This test makes hard assumptions about the content of the document, so don't alter it with our small molecule test node
            TestSmallMolecules = false;
            // tests for a blank document
            RunUI(() =>
            {
                Settings.Default.UIMode = SrmDocument.DOCUMENT_TYPE.proteomic.ToString();
                SkylineWindow.NewDocument();
                Assert.IsTrue(SkylineWindow.IsCheckedButtonProteomicUI);
                Assert.IsFalse(SkylineWindow.IsCheckedButtonSmallMolUI);
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("blank.sky"));
                // reload file from persistent string
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("blank.sky"));
            });

            foreach (SrmDocument.DOCUMENT_TYPE uimode in Enum.GetValues(typeof(SrmDocument.DOCUMENT_TYPE)))
            {
                // Loading an empty doc shouldn't have any effect on UI mode
                TestUIModesFileLoadAction(uimode, // Initial UI mode
                    "blank.sky", // Doc to be loaded
                    uimode); // Resulting UI mode

                // Loading a purely proteomic doc should change UI mode to straight up proteomic
                TestUIModesFileLoadAction(uimode, // Initial UI mode
                    "Proteomic.sky", // Doc to be loaded
                    SrmDocument.DOCUMENT_TYPE.proteomic); // Resulting UI mode

                // Loading a purely small mol doc should change UI mode to straight up small mol
                TestUIModesFileLoadAction(uimode, // Initial UI mode
                    "SmallMol.sky", // Doc to be loaded
                    SrmDocument.DOCUMENT_TYPE.small_molecules); // Resulting UI mode

                // Loading a mixed doc should change UI mode to mixed
                TestUIModesFileLoadAction(uimode, // Initial UI mode
                    "Mixed.sky", // Doc to be loaded
                    SrmDocument.DOCUMENT_TYPE.mixed); // Resulting UI mode
            }

            // Test interaction of buttons in an empty document
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
                SkylineWindow.UIModeChanged(SrmDocument.DOCUMENT_TYPE.proteomic); // Set UI mode to proteomic
                Assert.AreEqual(SkylineWindow.ModeUIHelper.ModeUI, SrmDocument.DOCUMENT_TYPE.proteomic); // Should be proteomic mode
                SkylineWindow.ClickButtonProteomcUI(); // Unclick proteomic button
                Assert.AreEqual(SkylineWindow.ModeUIHelper.ModeUI, SrmDocument.DOCUMENT_TYPE.small_molecules); // Should flip to small mol mode
                SkylineWindow.ClickButtonSmallMolUI(); // Unclick small mol button
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.proteomic, SkylineWindow.ModeUIHelper.ModeUI); // Should flip back to proteomic mode
                SkylineWindow.ClickButtonSmallMolUI(); // Reclick small mol button
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.mixed, SkylineWindow.ModeUIHelper.ModeUI); // Should be mixed mode
                SkylineWindow.ClickButtonProteomcUI(); // Unclick proteomic button
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.small_molecules, SkylineWindow.ModeUIHelper.ModeUI); // Should flip to small mol mode
                SkylineWindow.ClickButtonProteomcUI(); // Reclick proteomic button
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.mixed, SkylineWindow.ModeUIHelper.ModeUI); // Should flip back to mixed mode
                SkylineWindow.ClickButtonSmallMolUI(); // Unclick small mol button
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.proteomic, SkylineWindow.ModeUIHelper.ModeUI); // Should flip back to proteomic mode
            });

            // Test interaction of buttons in non-empty documents
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.small_molecules);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.mixed);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.proteomic);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.mixed);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.mixed, SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.mixed);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.mixed, SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.mixed);

        }

        private void TestUIModesFileLoadAction(SrmDocument.DOCUMENT_TYPE initalModeUI, 
            string docName,
            SrmDocument.DOCUMENT_TYPE finalModeUI)
        {
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
                SkylineWindow.UIModeChanged(initalModeUI);
                Assert.AreEqual(SkylineWindow.ModeUIHelper.ModeUI, initalModeUI);
                VerifyButtonStates();

                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(docName));
                Assert.AreEqual(finalModeUI, SkylineWindow.ModeUIHelper.ModeUI);
                VerifyButtonStates();

                SkylineWindow.NewDocument();
                Assert.AreEqual(finalModeUI, SkylineWindow.ModeUIHelper.ModeUI);
                VerifyButtonStates();
            });
        }

        private static void VerifyButtonStates()
        {
            Assert.IsTrue(SkylineWindow.IsCheckedButtonProteomicUI ==
                          (SkylineWindow.ModeUIHelper.ModeUI != SrmDocument.DOCUMENT_TYPE.small_molecules)); // Checked if any proteomic data
            Assert.IsTrue(SkylineWindow.IsCheckedButtonSmallMolUI ==
                          (SkylineWindow.ModeUIHelper.ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic)); // Checked if any small mol data
            Assert.IsTrue(SkylineWindow.IsEnabledButtonProteomicUI ==
                          (SkylineWindow.DocumentUI.PeptideCount == 0)); // Disabled if any proteomic targets
            Assert.IsTrue(SkylineWindow.IsEnabledButtonSmallMolUI ==
                          (SkylineWindow.DocumentUI.CustomIonCount == 0)); // Disabled if any smallmol targets
        }

        private void TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE initalModeUI,
            SrmDocument.DOCUMENT_TYPE clickWhat,
            SrmDocument.DOCUMENT_TYPE finalModeUI)
        {
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
                var docName = initalModeUI == SrmDocument.DOCUMENT_TYPE.proteomic ? "Proteomic.sky" :
                    initalModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules ? "SmallMol.sky" :
                    "Mixed.sky";
                VerifyButtonStates();
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(docName));
                VerifyButtonStates();
                if (clickWhat == SrmDocument.DOCUMENT_TYPE.proteomic)
                    SkylineWindow.ClickButtonProteomcUI();
                else
                    SkylineWindow.ClickButtonSmallMolUI();
                VerifyButtonStates();
                Assert.AreEqual(SkylineWindow.ModeUIHelper.ModeUI, finalModeUI);
            });
        }



    }
}
