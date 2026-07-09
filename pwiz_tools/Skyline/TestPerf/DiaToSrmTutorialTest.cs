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

using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace TestPerf
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
            // library zips (~3 GB each) hold the gas-phase fractionated mzML runs that Step 1.9 imports.
            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/webinars/Webinar22.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libA.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libB.zip",
                @"https://skyline.ms/webinars/Webinar22_dia_libC.zip",
            };

            // The gas-phase fractionated runs are huge (~36 GB of .mzML); extract them once to a shared
            // persistent location and reuse them in place across runs rather than re-extracting every run.
            // (Takes effect once the GPF library zips and the results-import step below are re-enabled. The
            // .elib is deliberately not persisted -- loading it creates a .elibc cache beside it, which would
            // trip the "persistent files were modified" check.)
            TestFilesPersistent = new[] { @".mzML" };

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
            BuildLibraryAndExtractChromatograms(wizard);    // s-02, s-03
            ConfigureTransitionAndFullScanSettings(wizard); // s-04, s-05, s-06
            ImportFastaAndAssociateProteins(wizard);        // s-07, s-08, s-09
            ImportPrtcDocument();                           // s-10
            ImportGpfDiaResults();                          // s-11
            RefineByCv();                                   // s-12 .. s-15
            FilterPeptidesForProteins();                    // s-16 .. s-19

            // TODO(connector tutorial): the remaining steps are filled in incrementally, each driven through
            // the IJsonToolService and each ending in a PauseForScreenShot whose number matches the s-NN.png
            // referenced by the HTML. The remaining mapping is:
            //   Step 4  iRT calculator + scheduling + export .......... s-20 .. s-31

            // Never return while a background chromatogram load is still in flight: the framework's end-of-test
            // SwitchDocument to a blank document is retried only twice and fails against active background
            // processing, which leaves the modified document in place and raises a "save changes?" prompt that
            // wedges teardown. (A connector action's WaitForDocumentLoaded can return in the brief gap before a
            // save-triggered reload registers, so settle once more here, after every step, before returning.)
            WaitForDocumentLoaded(60 * 60 * 1000);
        }

        /// <summary>
        /// Step 2 (s-12 .. s-15): survey the %CV distribution (View &gt; Peak Areas &gt; CV Histogram) and raise
        /// its cutoff line to 30%, then refine with Refine &gt; Advanced -- 2 peptides per protein (Document tab)
        /// and a 30%-CV consistency filter on summed product transitions (Consistency tab) -- and save the
        /// filtered document. Everything is driven through the connector, including the graph's right-click
        /// Properties menu.
        /// </summary>
        private void RefineByCv()
        {
            // 2.1 Show the CV histogram of the peptide peak areas. The Peak Areas graph window titles it
            // "Peak Areas - CV Histogram", which is how the connector finds it among the open graphs.
            Connector.InvokeMenuItem(MenuPath<ViewMenu>(
                "viewToolStripMenuItem", "peakAreasMenuItem", "areaCVHistogramMenuItem"));
            var cvHistogram = WaitForConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram, "Peak Areas -- CV Histogram"); // s-12

            // Raise the CV-cutoff line to 30% through the histogram's right-click Properties dialog.
            Connector.InvokeContextMenuItem(cvHistogram.FormId, string.Empty,
                GetLocalizedText<PeakAreasContextMenu>("areaPropsContextMenuItem"));
            var cvProperties = WaitForConnectorForm<AreaCVToolbarProperties>();
            cvProperties.SetValue(GetLocalizedText<AreaCVToolbarProperties>("label2"), "30"); // CV cutoff
            cvProperties.Accept();
            var cvHistogram30 = WaitForConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram30, "CV Histogram -- 30% cutoff"); // s-13

            // 2.2 Refine > Advanced. Document tab: at least 2 peptides per protein.
            Connector.InvokeMenuItem(MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem"));
            var refine = WaitForConnectorForm<RefineDlg>();
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabDocument"));
            refine.SetValue(GetLocalizedText<RefineDlg>("label1"), "2"); // Min peptides per protein
            PauseForScreenShot(refine, "Refine -- Document tab"); // s-14

            // Consistency tab: keep only peptides under 30% CV across the replicates. The other options the
            // tutorial lists are already this document's defaults, so only the cutoff needs setting -- Transition
            // type is "Products" (the sole transition type present, so RefineDlg leaves that combo disabled),
            // Normalize to defaults to "None", and Summed transitions defaults to "all".
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabConsistency"));
            refine.SetValue(GetLocalizedText<RefineDlg>("labelCV"), "30"); // CV cutoff %
            PauseForScreenShot(refine, "Refine -- Consistency tab"); // s-15
            // Accept runs the refine (it drops the peptides/proteins that fail the filters and can show a
            // progress dialog); the connector's Accept waits it out before returning.
            refine.Accept();
            WaitForDocumentLoaded();

            // Save the filtered document under a new name.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem"));
            var saveDlg = WaitForNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("DIA_to_SRM_Tutorial-filtered.sky"));
            saveDlg.Accept();
            // The native Save dialog's Accept posts the save (a modal "Saving..." dialog for the large document)
            // as a Win32 message, not a tracked connector action, so WaitForAction does not apply and
            // WaitForDocumentLoaded tracks loading, not saving. Wait for the save to clear the dirty flag, or it
            // is still running when the next step (and teardown) begins.
            WaitForConditionUI(5 * 60 * 1000, () => !SkylineWindow.Dirty);
        }

        /// <summary>
        /// Step 3 (s-16 .. s-19): narrow the document to a subset of proteins of interest. Refine &gt; Accept
        /// Proteins keeps only the proteins named in target_proteins.txt (matched by Name), confirming the prompt
        /// that lists the names not present in the document (s-16, s-17). Then Refine &gt; Advanced -- Results tab
        /// -- caps each protein at its 2 best peptides and each peptide at its 5 best transitions by ranked peak
        /// intensity (s-18), Edit &gt; Expand All &gt; Proteins shows the result (s-19), and the document is saved
        /// as SRM_targets.sky. Everything is driven through the connector.
        /// </summary>
        private void FilterPeptidesForProteins()
        {
            // 3.1 Protein filtering: Refine > Accept Proteins keeps only the proteins named in target_proteins.txt.
            // The tutorial opens that file, copies its contents, and pastes them into the dialog; here the file is
            // read directly and set as the "Proteins to keep" text (the caption-less multiline box is paired with
            // the label before it in tab order -- "Proteins to keep:"). "Names" is the default match mode; select
            // it to match the tutorial.
            Connector.InvokeMenuItem(MenuPath<RefineMenu>("refineToolStripMenuItem", "acceptProteinsMenuItem"));
            var acceptProteins = WaitForConnectorForm<RefineProteinListDlg>();
            acceptProteins.SetValue(GetLocalizedText<RefineProteinListDlg>("label1"),
                File.ReadAllText(GetTestPath("target_proteins.txt")));
            acceptProteins.ClickButton(GetLocalizedText<RefineProteinListDlg>("proteinNames")); // match by Names
            PauseForScreenShot(acceptProteins, "Accept Proteins -- paste protein list"); // s-16

            // OK checks the pasted names against the document; because some of the target proteins are not in the
            // document, a prompt lists them and asks whether to continue. ClickButton auto-waits, but because this
            // click opens a nested modal the wait returns as soon as that prompt appears (it does not block on it).
            acceptProteins.ClickButton(GetLocalizedText<RefineProteinListDlg>("btnOk"));
            var notInDocument = WaitForConnectorForm<MultiButtonMsgDlg>();
            PauseForScreenShot(notInDocument, "Proteins not in document"); // s-17
            // Accept (OK) dismisses the prompt, which lets Accept Proteins run the refine that drops the unlisted
            // proteins; the connector's Accept waits it out, then let the document settle.
            notInDocument.Accept();
            WaitForDocumentLoaded();

            // 3.2 Peptide ranked intensity filtering: Refine > Advanced, Results tab -- keep each protein's 2 best
            // peptides and each peptide's 5 best transitions by ranked peak intensity. The two rank boxes are
            // caption-less and paired with the label before each in tab order ("Max peptide peak rank:" and
            // "Max transition peak rank:"). The other Results-tab options are left at their defaults, as the
            // tutorial notes they would not change this document.
            Connector.InvokeMenuItem(MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem"));
            var refine = WaitForConnectorForm<RefineDlg>();
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabResults"));
            refine.SetValue(GetLocalizedText<RefineDlg>("label8"), "2");           // max peptide peak rank
            refine.SetValue(GetLocalizedText<RefineDlg>("labelMaxPeakRank"), "5"); // max transition peak rank
            PauseForScreenShot(refine, "Refine -- Results tab"); // s-18
            // Accept runs the refine (dropping the lower-ranked peptides/transitions and possibly showing a
            // progress dialog); the connector's Accept waits it out before continuing.
            refine.Accept();
            WaitForDocumentLoaded();

            // Expand all proteins so the screenshot shows the peptides kept under each target.
            Connector.InvokeMenuItem(MenuPath<EditMenu>(
                "editToolStripMenuItem", "expandAllToolStripMenuItem", "expandProteinsMenuItem"));
            PauseForScreenShot(WaitForConnectorForm<SkylineWindow>(), "Targets -- SRM peptide targets"); // s-19

            // Save the SRM target document under a new name.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem"));
            var saveDlg = WaitForNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("SRM_targets.sky"));
            saveDlg.Accept();
            // As in RefineByCv, the native Save dialog posts the save as a Win32 message (not a tracked connector
            // action), so wait for the dirty flag to clear rather than a tracked action or a document reload.
            WaitForConditionUI(5 * 60 * 1000, () => !SkylineWindow.Dirty);
        }

        /// <summary>The wizard's Next button caption, localized and normalized so it matches in any language
        /// (on the last page the wizard relabels this same button to "Finish" -- see <see cref="WizardFinishButton"/>).</summary>
        private string WizardNextButton => GetLocalizedText<ImportPeptideSearchDlg>("btnNext");

        /// <summary>The caption the wizard puts on its Next button on the last (Import FASTA) page. It is set in
        /// code from a resource string (not the designer), so it is read from that string and normalized the
        /// same way the connector normalizes a button label.</summary>
        private static string WizardFinishButton =>
            UiElement.NormalizeLabel(PeptideSearchResources.ImportPeptideSearchDlg_NextPage_Finish);

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
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "startPageMenuItem"));
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
            // from the resources so the test works in any UI language. Each connector verb waits out its posted
            // action, so the next sees its effect.
            wizard.ClickButton(GetLocalizedText<BuildPeptideSearchLibraryControl>("radioExistingLibrary"));
            wizard.SetValue(GetLocalizedText<BuildPeptideSearchLibraryControl>("lblLibraryPath"),
                GetTestPath("CSF_GPFLib_QRcombined.elib"));
            PauseForScreenShot(wizard, "Build Spectral Library -- use existing library"); // s-02
            wizard.ClickButton(GetLocalizedText<ImportPeptideSearchDlg>("btnNext"));

            // Extract Chromatograms page: nothing to add here (results are imported later), so just capture it.
            // Clicking Next loaded the library and swaps this page in asynchronously, so wait for it before the
            // screenshot. Clicking Next here would prompt to continue without results; that is driven in Step 1.3+.
            WaitForControl(wizard, nameof(ImportResultsDIAControl));
            PauseForScreenShot(wizard, "Extract Chromatograms"); // s-03
        }

        /// <summary>
        /// Steps 1.3-1.5: advance off the empty Extract Chromatograms page (confirming the "no results files"
        /// prompt), pass through Add Modifications unchanged (s-04), set the Transition Settings the tutorial
        /// prescribes (s-05), and the Full-Scan Settings (s-06).
        /// </summary>
        private void ConfigureTransitionAndFullScanSettings(IFormElement wizard)
        {
            // 1.2 -> advance past Extract Chromatograms. With no result files added, the wizard warns that it
            // will create a template document with no imported results and asks whether to continue; the
            // multi-injection GPF results are imported later (Step 1.9). Accept() presses the prompt's default
            // (Yes) without keying on a localized caption.
            wizard.ClickButton(WizardNextButton);
            WaitForConnectorForm<MultiButtonMsgDlg>().Accept();

            // 1.3 Add Modifications: no modifications were used in the search, so just move on.
            WaitForControl(wizard, nameof(MatchModificationsControl));
            PauseForScreenShot(wizard, "Add Modifications"); // s-04
            wizard.ClickButton(WizardNextButton);

            // 1.4 Configure Transition Settings. Text fields (charges/types/m-z/tolerance/counts) are
            // language-neutral and addressed by their localized labels; the two ion-range combo boxes are set
            // by their (currently English) item text -- see the localization note at the bottom of this file.
            WaitForControl(wizard, nameof(TransitionSettingsControl));
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblPrecursorCharges"), "2, 3");
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCharges"), "1, 2");
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonTypes"), "y, b");
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label1"), "ion 3");     // product ions from
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label2"), "last ion");  // product ions to
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label3"), "50");        // min m/z
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label6"), "2000");      // max m/z
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblTolerance"), 0.005.ToString(CultureInfo.CurrentCulture));
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCount"), "8");    // pick N product ions
            // The min-product-ions box sits between two unit labels; the connector pairs a caption-less field
            // with the label before it in tab order, which here is "product ions" (lblIonCountUnits), not the
            // "min product ions" suffix label that follows the box.
            wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCountUnits"), "3");
            PauseForScreenShot(wizard, "Configure Transition Settings"); // s-05
            wizard.ClickButton(WizardNextButton);

            // 1.5 Configure Full-Scan Settings: the defining DIA choices are a Centroided product mass analyzer
            // and the Results-only isolation scheme (set below). The mass-accuracy value and the "use only scans
            // within N minutes of MS/MS IDs" retention-time filter are left at the DIA wizard's defaults: their
            // fields are relabeled at runtime / have split unit labels, so they are not cleanly addressable by
            // caption yet (a remaining item to wire up, possibly needing a connector tweak).
            WaitForControl(wizard, nameof(FullScanSettingsControl));
            wizard.SetValue(GetLocalizedText<FullScanSettingsControl>("label22"), "Centroided");        // product mass analyzer
            wizard.SetValue(GetLocalizedText<FullScanSettingsControl>("labelIsolationScheme"), "Results only");
            PauseForScreenShot(wizard, "Configure Full-Scan Settings"); // s-06
            wizard.ClickButton(WizardNextButton);
        }

        /// <summary>
        /// Step 1.6-1.7: on the Import FASTA page choose Trypsin with 0 missed cleavages and the human FASTA
        /// (s-07), Finish, then configure the Associate Proteins dialog (s-08) and capture the populated Targets
        /// view once the wizard builds the document (s-09).
        /// </summary>
        private void ImportFastaAndAssociateProteins(IFormElement wizard)
        {
            // 1.6 Import FASTA (required): Trypsin [KR | P] / 0 missed cleavages (enzyme names are settings, not
            // localized), then browse to the human FASTA through the native Open dialog.
            WaitForControl(wizard, nameof(ImportFastaControl));
            wizard.SetValue(GetLocalizedText<ImportFastaControl>("label3"), "Trypsin [KR | P]");   // enzyme
            wizard.SetValue(GetLocalizedText<ImportFastaControl>("label2"), "0");                  // max missed cleavages
            wizard.ClickButton(GetLocalizedText<ImportFastaControl>("browseFastaBtn"));
            var fastaDlg = WaitForNativeFileDialog();
            fastaDlg.SetValue("FileName", GetTestPath("uniprot_human_25apr2019.fasta"));
            fastaDlg.Accept();
            WaitForCondition(() => !Connector.GetOpenForms().Any(f => f.IsNative));
            PauseForScreenShot(wizard, "Import FASTA"); // s-07

            // Finish the wizard. Building peptides from the FASTA brings up the Associate Proteins dialog.
            wizard.ClickButton(WizardFinishButton);

            // 1.7 Associate Proteins: create protein groups first (which relabels the shared-peptides options to
            // their grouped form), then drop shared (non-unique) peptides. The combo item text comes straight
            // from the localized EnumNames resource, so it matches exactly in any language. Min peptides per
            // protein stays at its default of 1 (the connector does not yet address the NumericUpDown holding it).
            var associate = WaitForConnectorForm<AssociateProteinsDlg>();
            // The checkbox and the shared-peptides combo have no caption the connector can match (each option's
            // label is a plain Label shadowed by a "?" help link, and the checkbox's caption is a separate
            // sibling). So identify them the way a connector client reading the tutorial would -- by their type
            // and position rather than any internal control name -- and act on them through their structured
            // Path: "Create protein groups" is the dialog's first checkbox, and there is only one combo box.
            // Create protein groups first: this is the dialog's first checkbox. Checking it relabels the
            // shared-peptides options to their grouped form and re-lays-out the dialog, so the combo's Path is
            // looked up only afterward (a Path captured before would embed a now-stale label). Min peptides per
            // protein stays at its default of 1.
            var groupProteinsCheckBox = associate.GetControls().First(control => Equals(control.Path.Type, @"CheckBox")).Path;
            WaitForAction(() => associate.PerformAction(groupProteinsCheckBox, UiActions.SetValue, "true"));
            // Drop shared (non-unique) peptides via the dialog's only combo box; the value is the localized
            // EnumNames resource, so it matches in any language. Address it by type and position only (clearing
            // the path's Text) -- the way a client would target "the only combo box" -- because enabling
            // grouping re-lays-out the dialog and changes the label the combo would otherwise be matched by.
            var sharedPeptidesCombo = associate.GetControls()
                .Single(control => Equals(control.Path.Type, @"ComboBox")).Path.ChangeText(null);
            WaitForAction(() => associate.PerformAction(sharedPeptidesCombo, UiActions.SetValue,
                EnumNames.SharedPeptidesGroup_Removed));
            // Each option change recomputes the protein association on a background thread, during which the
            // dialog disables its OK button (its first button); wait for that to finish -- so the screenshot
            // shows the final result counts and Accept() actually closes the dialog -- then accept.
            WaitForControlEnabled(associate, @"Button");
            PauseForScreenShot(associate, "Associate Proteins"); // s-08
            associate.Accept();

            // The wizard closes and builds the document; capture the populated Targets view of the main window.
            WaitForCondition(() => !Connector.GetOpenForms().Any(f => f.Type == nameof(ImportPeptideSearchDlg)));
            WaitForDocumentLoaded();
            PauseForScreenShot(WaitForConnectorForm<SkylineWindow>(), "Targets populated"); // s-09
        }

        /// <summary>
        /// Step 1.8: add the PRTC indexed retention-time standards (which were not in the spectral library) by
        /// importing them from an existing document (File &gt; Import &gt; Document), so they are extracted along
        /// with everything else. Capture the populated Targets view (s-10) and save.
        /// </summary>
        private void ImportPrtcDocument()
        {
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importDocumentMenuItem"));
            var importDlg = WaitForNativeFileDialog();
            importDlg.SetValue("FileName", GetTestPath("PRTC.sky"));
            importDlg.Accept();
            WaitForDocumentLoaded();
            PauseForScreenShot(WaitForConnectorForm<SkylineWindow>(), "Targets with PRTC added"); // s-10

            // Save blocks in a modal "Saving..." progress dialog until the (large) document is written; the
            // connector's InvokeMenuItem waits out that posted action (riding through the progress dialog), so the
            // next step does not run -- and the test does not end -- while the save is still in progress.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem"));
        }

        /// <summary>
        /// Step 1.9: import the gas-phase fractionated DIA runs as multi-injection replicates -- one replicate
        /// per LibA/LibB/LibC subfolder, each with its six m/z-range injections -- declining decoys and keeping
        /// the full folder names (s-11), then extract the chromatograms. This is the heavy data step; it is why
        /// the test lives in TestPerf and only runs with perftests on.
        /// </summary>
        private void ImportGpfDiaResults()
        {
            var mzmlFolder = AssembleMzmlReplicateFolder();

            Connector.InvokeMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importResultsMenuItem"));
            // Some documents first get a "no decoys -- add them?" prompt; it is conditional, so handle it only
            // if it appears (decline with No) before the Import Results dialog -- wait for whichever shows first.
            WaitForCondition(() => Connector.GetOpenForms().Any(f =>
                Equals(f.Type, nameof(MultiButtonMsgDlg)) || Equals(f.Type, nameof(ImportResultsDlg))));
            var decoyPrompt = Connector.GetOpenForms().FirstOrDefault(f => Equals(f.Type, nameof(MultiButtonMsgDlg)));
            if (decoyPrompt != null)
                JsonUiService.ResolveForm(decoyPrompt.Id).ClickButton(UiElement.NormalizeLabel(MultiButtonMsgDlg.BUTTON_NO));

            // Import Results: each subfolder of the chosen directory becomes a replicate whose data files are
            // its injections. Choose that option (s-11), then OK opens the native folder browser.
            var importResults = WaitForConnectorForm<ImportResultsDlg>();
            importResults.ClickButton(GetLocalizedText<ImportResultsDlg>("radioCreateMultipleMulti"));
            PauseForScreenShot(importResults, "Import Results -- multi-injection replicates in directories"); // s-11
            importResults.Accept();

            // Pick the assembled mzmls folder in the native Browse-For-Folder dialog (its LibA/LibB/LibC
            // subfolders are the replicates). The connector selects the folder by path; its controlId is ignored.
            var folderDlg = WaitForNativeFolderDialog();
            folderDlg.SetValue(@"Folder", mzmlFolder);
            folderDlg.Accept();

            // The replicate names share a common prefix; keep the full folder names (Do not remove), then OK.
            var nameDlg = WaitForConnectorForm<ImportResultsNameDlg>();
            nameDlg.ClickButton(GetLocalizedText<ImportResultsNameDlg>("radioDontRemove"));
            nameDlg.Accept();

            // Accept posts the import fire-and-forget, so WaitForDocumentLoaded on its own can return in the gap
            // before the import has registered (the document still reads loaded). First wait until the results
            // actually exist (the replicates have been added), then wait until they finish loading -- extracting
            // chromatograms from the gas-phase fractionated runs (18 ~2 GB mzML files) is slow.
            WaitForConditionUI(60 * 60 * 1000, () => SkylineWindow.DocumentUI.MeasuredResults != null);
            WaitForDocumentLoaded(60 * 60 * 1000);
            // Save blocks in a modal "Saving..." progress dialog until the (large) document is written; the
            // connector's InvokeMenuItem waits out that posted action (riding through the progress dialog), so the
            // next step does not run -- and the test does not end -- while the save is still in progress.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem"));
        }

        /// <summary>
        /// The three gas-phase-fractionated runs ship as three separate download zips (one replicate each), so
        /// gather them under a single parent folder -- the layout the multi-injection import expects (each
        /// subfolder a replicate). Copy the files (a directory junction is unreliable on some machines),
        /// skipping any already copied so reruns are cheap. Returns the assembled parent folder.
        /// </summary>
        private string AssembleMzmlReplicateFolder()
        {
            var mzmlFolder = TestFilesDirs[0].GetTestPath(Path.Combine("Webinar22", "mzmls"));
            for (int i = 0; i < 3; i++)
            {
                var lib = @"Lib" + (char)('A' + i);
                // The .mzML runs are persisted (TestFilesPersistent), so they live under the shared persistent
                // dir, not the per-run extraction dir; GetTestPath only redirects file paths, not the directory.
                var libFilesDir = TestFilesDirs[i + 1];
                var sourceDir = Path.Combine(libFilesDir.PersistentFilesDir ?? libFilesDir.FullPath,
                    @"Webinar22", @"mzml", lib);
                var destDir = Path.Combine(mzmlFolder, lib);
                Directory.CreateDirectory(destDir);
                foreach (var sourceFile in Directory.GetFiles(sourceDir))
                {
                    var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));
                    // Skip a file that is already fully copied (same size). Otherwise copy via a temp name and
                    // move it into place, so an interrupted copy never leaves a partial file that a later run
                    // would trust -- which would wedge the import reading a truncated mzML.
                    if (File.Exists(destFile) && new FileInfo(destFile).Length == new FileInfo(sourceFile).Length)
                        continue;
                    var tempFile = destFile + @".copying";
                    File.Copy(sourceFile, tempFile, true);
                    File.Delete(destFile);
                    File.Move(tempFile, destFile);
                }
            }
            return mzmlFolder;
        }

        // NOTE on localization of combo boxes: the connector's ComboBox.SetValue matches an item by its exact
        // visible text (FindStringExact). Where a localized resource string for the item is available it is used
        // (e.g. the shared-peptides option via EnumNames), but the ion-range, product-mass-analyzer and
        // isolation-scheme values above are still English literals and would not match in ja/zh-CHS. The text
        // fields, labels, menu paths and Next/Finish buttons are already localized. Making those last combo
        // values language-neutral is the remaining multi-language gap for these steps (resolve by reading each
        // item's localized text, or by teaching the connector to select a combo item by index).
    }
}
