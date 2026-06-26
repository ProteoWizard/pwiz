/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.ToolsUI;
using SkylineTool;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Tutorial test for "Using DIA Data to Create SRM Methods" (Skyline Webinar 22). It performs every
    /// tutorial action through the in-process <see cref="IJsonToolService"/> (see <see cref="McpTutorialTest"/>)
    /// -- both to reproduce the tutorial and to verify the connector is capable enough to run it end-to-end.
    ///
    /// The screenshots it captures (with IsPauseForScreenShots) are written to the draft tutorial folder
    /// (see <see cref="McpTutorialTest.TutorialDocumentationFolder"/>); the matching HTML is at
    /// Documentation\Tutorial-Drafts\DIAtoSRM\en\index.html.
    /// </summary>
    [TestClass]
    public class DiaToSrmTutorialTest : McpTutorialTest
    {
        // While this tutorial is a draft, its screenshots live under Documentation\Tutorial-Drafts. Removing
        // this override (and moving the HTML folder) publishes it to Documentation\Tutorials.
        protected override string TutorialDocumentationFolder => "Tutorial-Drafts";

        // The workflow is still being built out, so there is no stable recorded audit log to compare against
        // yet. Once the tutorial is complete, record the log (IsRecordAuditLogForTutorials) and remove this.
        public override bool AuditLogCompareLogs => false;

        [TestMethod]
        public void TestDiaToSrmTutorial()
        {
            // Set true to look at / regenerate tutorial screenshots.
            // IsPauseForScreenShots = true;
            CoverShotName = "DIAtoSRM";

            // The framework downloads each ZIP to a persistent local cache and reuses it across runs. The DIA
            // library files are large (~3 GB each); the gas-phase fractionated mzML runs they contain are
            // needed to extract chromatograms in Step 1.9.
            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/webinars/Webinar22.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libA.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libB.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libC.zip",
            };

            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(Path.Combine("Webinar22", relativePath));
        }

        protected override void DoTest()
        {
            StartToolService();

            var wizard = GettingStarted();
            BuildLibraryAndExtractChromatograms(wizard); // s-02, s-03

            // TODO(connector tutorial): the remaining wizard/steps are filled in incrementally, each driven
            // through the IJsonToolService and each ending in a PauseForScreenShot whose number matches the
            // s-NN.png referenced by the HTML. The remaining mapping is:
            //   1.3-1.6 Modifications / Transition / Full-Scan / FASTA .. s-04 .. s-07
            //   1.7     Associate Proteins ............................ s-08, s-09
            //   1.8     Import Document (PRTC) ......................... s-10
            //   1.9     Import Results (multi-injection GPF DIA) ....... s-11
            //   Step 2  CV Histogram + Refine > Advanced ............... s-12 .. s-15
            //   Step 3  Accept Proteins + ranked-intensity refine ..... s-16 .. s-19
            //   Step 4  iRT calculator + scheduling + export .......... s-20 .. s-31

            // WIP: end cleanly until the rest of the wizard is driven.
            wizard.Close();
            WaitForCondition(() => !Connector.GetOpenForms().Any(f => f.Type == nameof(ImportPeptideSearchDlg)));
        }

        /// <summary>
        /// "Getting Started": select the Proteomics interface, then open the Import DIA Peptide Search wizard
        /// from the Start Page (screenshot s-01) -- all through the connector. Returns the opened wizard.
        /// </summary>
        private IFormElement GettingStarted()
        {
            // The tutorial begins by reverting to default settings, but the test starts from a clean default
            // document so that step is unnecessary here (and it is not a numbered screenshot). Just ensure the
            // Proteomics interface, the way the protein icon in the corner indicates.
            Connector.SetUiMode("proteomic");

            // Open the Start Page (File > Start). The menu path is built from the localized, normalized
            // menu-item captions (read from the SkylineWindow resources) so it matches in any UI language.
            // The document is unmodified, so this raises no save prompt.
            Connector.InvokeMenuItem(GetLocalizedText<SkylineWindow>("fileToolStripMenuItem") + @" > " +
                                     GetLocalizedText<SkylineWindow>("startPageMenuItem"));
            var startPage = WaitForConnectorForm<StartPage>();
            PauseForScreenShot(startPage, "Start Page -- Import DIA Peptide Search"); // s-01

            // Click the "Import DIA Peptide Search" tile (matched by its localized caption from the resx). It
            // first shows a "You must save this document before importing a peptide search" message (OK/Cancel);
            // Accept() presses its default (OK) button without keying on a localized caption, which brings up
            // the native Save As dialog. Save as DIA_to_SRM_Tutorial.sky and the wizard opens.
            startPage.ClickButton(StartupResources.StartPage_PopulateWizardPanel_Import_DIA_Peptide_Search);
            WaitForConnectorForm<MultiButtonMsgDlg>().Accept();

            var saveDlg = WaitForNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("DIA_to_SRM_Tutorial.sky"));
            saveDlg.Accept();

            return WaitForConnectorForm<ImportPeptideSearchDlg>();
        }

        /// <summary>
        /// Step 1.1-1.2 of the wizard: point the Build Spectral Library page at the existing GPF DIA
        /// chromatogram library (s-02), then move to the Extract Chromatograms page and leave its result files
        /// empty -- the gas-phase fractionated results are imported later as multi-injection replicates (s-03).
        /// </summary>
        private void BuildLibraryAndExtractChromatograms(IFormElement wizard)
        {
            // Build Spectral Library page: choose "Use existing" (which reveals the library path box), then set
            // the path to the EncyclopeDIA .elib. Controls are addressed by their localized captions, pulled
            // from the resources so the test works in any UI language. Wait out each posted action so the next
            // sees its effect.
            WaitForAction(() => wizard.ClickButton(GetLocalizedText<BuildPeptideSearchLibraryControl>("radioExistingLibrary")));
            WaitForAction(() => wizard.SetValue(GetLocalizedText<BuildPeptideSearchLibraryControl>("lblLibraryPath"),
                GetTestPath("CSF_GPFLib_QRcombined.elib")));
            PauseForScreenShot(wizard, "Build Spectral Library -- use existing library"); // s-02
            WaitForAction(() => wizard.ClickButton(GetLocalizedText<ImportPeptideSearchDlg>("btnNext")));

            // Extract Chromatograms page: nothing to add here (results are imported later), so just capture it.
            // Clicking Next loaded the library and swaps this page in asynchronously, so wait for it before the
            // screenshot. Clicking Next here would prompt to continue without results; that is driven in Step 1.3+.
            WaitForControl(wizard, nameof(ImportResultsDIAControl));
            PauseForScreenShot(wizard, "Extract Chromatograms"); // s-03
        }
    }
}
