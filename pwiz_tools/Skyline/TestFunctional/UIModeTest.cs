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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{   
    /// <summary>
    /// Functional test for UI Mode handling.
    /// </summary>
    [TestClass]
    public class UIModeTest : AbstractFunctionalTest
    {
        protected override bool ShowStartPage
        {
            get { return true; } // So our code for drawing user attention to UI mode select buttons fires
        }


        [TestMethod]
        public void UIModeSettingsTest()
        {
            TestFilesZip = @"TestFunctional\UIModeTest.zip";
            Settings.Default.UIMode = ""; // Start clean - should default to proteomic UI mode
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            // This test makes hard assumptions about the content of the document, so don't alter it with our small molecule test node
            TestSmallMolecules = false;

            TestPeptideToMoleculeText(); // Exercise the UI peptide->molecule translation code

            var startPage = WaitForOpenForm<StartPage>();
            Assert.IsTrue(startPage.GetModeUIHelper().HasModeUIExplainerToolTip); // Because of Settings.Default.UIMode = "" above
            RunUI(() => startPage.DoAction(skylineWindow => true)); // Start a new file
            WaitForOpenForm<SkylineWindow>();

            RunUI(() =>
            {
                Assert.AreEqual(SrmDocument.DOCUMENT_TYPE.proteomic, startPage.GetModeUIHelper().GetUIToolBarButtonState());
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("blank.sky"));
            });

            foreach (SrmDocument.DOCUMENT_TYPE uimode in Enum.GetValues(typeof(SrmDocument.DOCUMENT_TYPE)))
            {
                if (uimode == SrmDocument.DOCUMENT_TYPE.none)
                    continue;

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

                // Loading an empty doc shouldn't have any effect on UI mode
                TestUIModesFileLoadAction(uimode, // Initial UI mode
                    "blank.sky", // Doc to be loaded
                    uimode); // Resulting UI mode

                // Test behavior in an empty document
                RunUI(()=>
                {
                    SkylineWindow.NewDocument();
                    SkylineWindow.SetUIMode(uimode); 
                    Assert.AreEqual(uimode, SkylineWindow.GetModeUIHelper().ModeUI);
                });

                // Test per-ui-mode persistence of "peptide" settings tab choice
                var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() => Assume.AreEqual(peptideSettingsDlg.SelectedTab, (PeptideSettingsUI.TABS)uimode));
                OkDialog(peptideSettingsDlg, peptideSettingsDlg.CancelDialog);

            }

            // Test interaction of buttons in non-empty documents
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.small_molecules, false);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.proteomic, true);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.small_molecules, SrmDocument.DOCUMENT_TYPE.mixed, false);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.proteomic, false);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.small_molecules, true);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.proteomic, SrmDocument.DOCUMENT_TYPE.mixed, false);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.mixed, SrmDocument.DOCUMENT_TYPE.mixed, false);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.mixed, SrmDocument.DOCUMENT_TYPE.small_molecules, true);
            TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE.mixed, SrmDocument.DOCUMENT_TYPE.proteomic, true);

            // Verify operation of small-mol-only UI elements
            foreach (SrmDocument.DOCUMENT_TYPE uimode2 in Enum.GetValues(typeof(SrmDocument.DOCUMENT_TYPE)))
            {
                if (uimode2 == SrmDocument.DOCUMENT_TYPE.none)
                    continue;

                RunUI(() =>
                {
                    SkylineWindow.NewDocument();
                    SkylineWindow.SetUIMode(uimode2);
                });
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                Assert.AreEqual(uimode2 == SrmDocument.DOCUMENT_TYPE.small_molecules, peptideSettingsUI.SmallMoleculeLabelsTabEnabled);
                if (uimode2 == SrmDocument.DOCUMENT_TYPE.small_molecules)
                {
                    // Verify operation of internal standard list edit
                    RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Labels;
                    });
                }
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            }
        }

        private void TestUIModesFileLoadAction(SrmDocument.DOCUMENT_TYPE initialModeUI, 
            string docName,
            SrmDocument.DOCUMENT_TYPE finalModeUI)
        {
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
                SkylineWindow.SetUIMode(initialModeUI);
                Assert.AreEqual(initialModeUI,SkylineWindow.GetModeUIHelper().ModeUI);
                VerifyButtonStates();

                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(docName));
                Assert.AreEqual(finalModeUI, SkylineWindow.GetModeUIHelper().ModeUI);
                VerifyButtonStates();

                SkylineWindow.NewDocument();
                Assert.AreEqual(finalModeUI, SkylineWindow.GetModeUIHelper().ModeUI);
                VerifyButtonStates();
            });

            // Prepare to test per-ui-mode persistence of "peptide" settings tab choice
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                () => SkylineWindow.ShowPeptideSettingsUI((PeptideSettingsUI.TABS)finalModeUI));
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.CancelDialog);

        }

        private static void VerifyButtonStates()
        {
            WaitForDocumentLoaded();
            Assert.IsTrue(SkylineWindow.IsProteomicOrMixedUI ==
                          (SkylineWindow.GetModeUIHelper().ModeUI != SrmDocument.DOCUMENT_TYPE.small_molecules)); // Checked if any proteomic data
            Assert.IsTrue(SkylineWindow.IsSmallMoleculeOrMixedUI ==
                          (SkylineWindow.GetModeUIHelper().ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic)); // Checked if any small mol data
        }

        private void TestUIModesClickAction(SrmDocument.DOCUMENT_TYPE initalModeUI,
            SrmDocument.DOCUMENT_TYPE clickWhat,
            bool expectNewDocument)
        {
            var docType = SkylineWindow.Document.DocumentType;
            RunUI(() =>
            {
                SkylineWindow.NewDocument();
                SkylineWindow.SetUIMode(initalModeUI);
                var docName = initalModeUI == SrmDocument.DOCUMENT_TYPE.proteomic ? "Proteomic.sky" :
                    initalModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules ? "SmallMol.sky" :
                    "Mixed.sky";
                VerifyButtonStates();
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(docName));
                VerifyButtonStates();
                docType = SkylineWindow.DocumentUI.DocumentType;
            });
            if (initalModeUI != clickWhat && clickWhat != SrmDocument.DOCUMENT_TYPE.mixed && clickWhat != docType)
            {
                // Can't force a loaded document to be another type, so we offer to create a new one
                RunDlg<AlertDlg>(()=>SkylineWindow.ModeUIButtonClick(clickWhat), dlg=>dlg.ClickYes());
                RunUI(()=>Assume.IsFalse(SkylineWindow.DocumentUI.MoleculeGroups.Any()));
            }
            else
            {
                RunUI(()=>SkylineWindow.ModeUIButtonClick(clickWhat));
                Assume.IsFalse(expectNewDocument);
            }
            RunUI(() =>
            {
                SkylineWindow.ModeUIButtonClick(clickWhat);
                Assert.IsFalse(SkylineWindow.GetModeUIHelper().HasModeUIExplainerToolTip);
                VerifyButtonStates();
                var actualModeUI = SkylineWindow.GetModeUIHelper().ModeUI;
                Assert.AreEqual(clickWhat, actualModeUI);
            });
        }

        public void TestPeptideToMoleculeText()
        {

            // The basics
            TestTranslate("Protein", "Molecule List");
            TestTranslate("Proteins", "Molecule Lists");
            TestTranslate("Modified Sequence", "Molecule");
            TestTranslate("Peptide Sequence", "Molecule");

            // Can't test "Modified Peptide Sequence" translation until L10N
            if (Resources.PeptideToMoleculeText_Molecule.Equals("Molecule") || !Resources.PeptideToMoleculeText_Modified_Peptide_Sequence.Equals("Modified Peptide Sequence")) 
            {
                TestTranslate(Resources.PeptideToMoleculeText_Modified_Peptide_Sequence, Resources.PeptideToMoleculeText_Molecule);
                TestTranslate("Modified Peptide Sequence", "Molecule");
                TestTranslate("Modified peptide sequence", "Molecule");
                TestTranslate("modified peptide sequence", "molecule");
            }

            TestTranslate("Peptide List", "Molecule List");
            TestTranslate("Ion charges", "Ion adducts");
            TestTranslate(Resources.PeptideToMoleculeText_Peptide, Resources.PeptideToMoleculeText_Molecule);
            TestTranslate(Resources.PeptideToMoleculeText_Peptides, Resources.PeptideToMoleculeText_Molecules);
            TestTranslate(Resources.PeptideToMoleculeText_Protein, Resources.PeptideToMoleculeText_Molecule_List);
            TestTranslate(Resources.PeptideToMoleculeText_Proteins, Resources.PeptideToMoleculeText_Molecule_Lists);
            TestTranslate(Resources.PeptideToMoleculeText_Peptide_List, Resources.PeptideToMoleculeText_Molecule_List);
            TestTranslate(Resources.PeptideToMoleculeText_Modified_Sequence, Resources.PeptideToMoleculeText_Molecule);
            TestTranslate(Resources.PeptideToMoleculeText_Peptide_Sequence, Resources.PeptideToMoleculeText_Molecule);
            TestTranslate(Resources.PeptideToMoleculeText_Ion_charges, Resources.PeptideToMoleculeText_Ion_adducts);

            // Preserve keyboard accelerators where we can
            TestTranslate("&Modified Sequence:", "&Molecule:");
            TestTranslate("Prot&eins", "Mol&ecule Lists");
            TestTranslate("Protein&s", "Molecule Li&sts");
            TestTranslate("Peptide &List", "Molecule &List");
            TestTranslate("Ion ch&arges", "Ion &adducts");

            var mapper = new Helpers.PeptideToMoleculeTextMapper(SrmDocument.DOCUMENT_TYPE.mixed, new Helpers.ModeUIExtender(null));
            // Deal with keyboard accelerators that don't map cleanly
            var reserved = new HashSet<char>(); // Normally this would be populated by perusing a Form, make our own for test purposes
            mapper.InUseKeyboardAccelerators = reserved;
            Assert.AreEqual("&Choose Horse Molecule", mapper.TranslateString("Choose Horse &Peptide")); // No &P in result, use &C instead
            Assert.AreEqual("Choose &Horse Molecule", mapper.TranslateString("Choose Horse &Peptide")); // &C is now reserved, use &H
            foreach (var b in "Choose Horse Molecule") reserved.Add(char.ToLower(b)); // Everything is reserved, no accelerator possible
            Assert.AreEqual("Choose Horse Molecule", mapper.TranslateString("Choose Horse &Peptide"));

            // Don't want to accidentally change a prompt "Protein Molecule" to "Molecule List Molecule"
            TestTranslate("Protein Molecule", "Protein Molecule");
            var withBoth = string.Format("{0} {1}",
                Resources.PeptideToMoleculeText_Modified_Sequence, Resources.PeptideToMoleculeText_Molecule);
            TestTranslate(withBoth, withBoth);

        }

        private void TestTranslate(string input, string expected)
        {
            Assert.AreEqual(input, Helpers.PeptideToMoleculeTextMapper.Translate(input, false));
            Assert.AreEqual(input, Helpers.PeptideToMoleculeTextMapper.Translate(input, SrmDocument.DOCUMENT_TYPE.proteomic));
            var translated = Helpers.PeptideToMoleculeTextMapper.Translate(input, true);
            Assert.AreEqual(expected, translated, "original:" + input);
            translated = Helpers.PeptideToMoleculeTextMapper.Translate(input.ToLowerInvariant(), true);
            Assert.AreEqual(expected.ToLowerInvariant(), translated, "original:" + input);

            var formatIn = input + " {0} {1}";
            var formatExpected = expected + " {0} {1}";
            Assert.AreEqual(string.Format(formatExpected, "peptide", "protein"), Helpers.PeptideToMoleculeTextMapper.Format(formatIn, SrmDocument.DOCUMENT_TYPE.small_molecules, "peptide", "protein"));

        }

    }
}
