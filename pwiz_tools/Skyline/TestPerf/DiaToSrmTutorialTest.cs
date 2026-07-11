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
            // Showing the CV Histogram graph is a docked graph (not a modal), so the menu-item verb is expected
            // to complete; the graph is then resolvable immediately.
            AssertComplete(Connector.InvokeMenuItem(MenuPath<ViewMenu>(
                "viewToolStripMenuItem", "peakAreasMenuItem", "areaCVHistogramMenuItem")));
            var cvHistogram = GetConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram, "Peak Areas -- CV Histogram"); // s-12

            // Raise the CV-cutoff line to 30% through the histogram's right-click Properties dialog. (The
            // context-menu verb is void; it opens the properties dialog, resolved immediately below.)
            Connector.InvokeContextMenuItem(cvHistogram.FormId, string.Empty,
                GetLocalizedText<PeakAreasContextMenu>("areaPropsContextMenuItem"));
            var cvProperties = GetConnectorForm<AreaCVToolbarProperties>();
            AssertComplete(cvProperties.SetValue(GetLocalizedText<AreaCVToolbarProperties>("label2"), "30")); // CV cutoff
            AssertComplete(Connector.Accept(cvProperties.FormId));
            var cvHistogram30 = GetConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram30, "CV Histogram -- 30% cutoff"); // s-13

            // 2.2 Refine > Advanced opens the RefineDlg (a dialog), so the menu-item verb does not complete.
            Connector.InvokeMenuItem(MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem"));
            var refine = GetConnectorForm<RefineDlg>();
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabDocument"));
            AssertComplete(refine.SetValue(GetLocalizedText<RefineDlg>("label1"), "2")); // Min peptides per protein
            PauseForScreenShot(refine, "Refine -- Document tab"); // s-14

            // Consistency tab: keep only peptides under 30% CV across the replicates. The other options the
            // tutorial lists are already this document's defaults, so only the cutoff needs setting -- Transition
            // type is "Products" (the sole transition type present, so RefineDlg leaves that combo disabled),
            // Normalize to defaults to "None", and Summed transitions defaults to "all".
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabConsistency"));
            AssertComplete(refine.SetValue(GetLocalizedText<RefineDlg>("labelCV"), "30")); // CV cutoff %
            PauseForScreenShot(refine, "Refine -- Consistency tab"); // s-15
            // Accept runs the refine (dropping the peptides/proteins that fail the filters, possibly via a progress
            // dialog). Accept completes when the dialog closes, but the refine's background reintegration keeps
            // loading -- Gap: Save As below requires a fully-loaded document, so wait for that load here.
            AssertComplete(Connector.Accept(refine.FormId));
            WaitForDocumentLoaded();

            // Save As opens the native Save dialog (a dialog), so the menu-item verb does not complete.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem"));
            var saveDlg = GetNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("DIA_to_SRM_Tutorial-filtered.sky"));
            Connector.Accept(saveDlg.FormId);
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
            var acceptProteins = GetConnectorForm<RefineProteinListDlg>();
            AssertComplete(acceptProteins.SetValue(GetLocalizedText<RefineProteinListDlg>("label1"),
                File.ReadAllText(GetTestPath("target_proteins.txt"))));
            AssertComplete(acceptProteins.ClickButton(GetLocalizedText<RefineProteinListDlg>("proteinNames"))); // match by Names
            PauseForScreenShot(acceptProteins, "Accept Proteins -- paste protein list"); // s-16

            // OK checks the pasted names against the document; because some target proteins are not in the
            // document, a prompt lists them and asks whether to continue. This click opens that prompt (a dialog),
            // so it does not complete; resolve the prompt immediately.
            // Gap: OK checks the pasted names against the document on a background pass, then raises the "not in
            // document" prompt asynchronously -- so it is not open on return; wait for it.
            acceptProteins.ClickButton(GetLocalizedText<RefineProteinListDlg>("btnOk"));
            var notInDocument = WaitForConnectorForm<MultiButtonMsgDlg>();
            PauseForScreenShot(notInDocument, "Proteins not in document"); // s-17
            // Accept (OK) dismisses the prompt, which lets Accept Proteins run the refine that drops the unlisted
            // proteins. Same gap as RefineByCv: the refine's background reintegration keeps loading after Accept
            // returns, so wait for the document before the next refine.
            AssertComplete(Connector.Accept(notInDocument.FormId));
            WaitForDocumentLoaded();

            // 3.2 Peptide ranked intensity filtering: Refine > Advanced, Results tab -- keep each protein's 2 best
            // peptides and each peptide's 5 best transitions by ranked peak intensity. The two rank boxes are
            // caption-less and paired with the label before each in tab order ("Max peptide peak rank:" and
            // "Max transition peak rank:"). The other Results-tab options are left at their defaults, as the
            // tutorial notes they would not change this document.
            Connector.InvokeMenuItem(MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem"));
            var refine = GetConnectorForm<RefineDlg>();
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabResults"));
            AssertComplete(refine.SetValue(GetLocalizedText<RefineDlg>("label8"), "2"));           // max peptide peak rank
            AssertComplete(refine.SetValue(GetLocalizedText<RefineDlg>("labelMaxPeakRank"), "5")); // max transition peak rank
            PauseForScreenShot(refine, "Refine -- Results tab"); // s-18
            // Accept runs the refine (dropping the lower-ranked peptides/transitions, possibly via a progress
            // dialog); its background reintegration keeps loading after Accept returns, so wait for the document
            // before the Save As below (which requires a fully-loaded document).
            AssertComplete(Connector.Accept(refine.FormId));
            WaitForDocumentLoaded();

            // Expand all proteins (a synchronous menu action) -- expected to complete.
            AssertComplete(Connector.InvokeMenuItem(MenuPath<EditMenu>(
                "editToolStripMenuItem", "expandAllToolStripMenuItem", "expandProteinsMenuItem")));
            PauseForScreenShot(GetConnectorForm<SkylineWindow>(), "Targets -- SRM peptide targets"); // s-19

            // Save As opens the native Save dialog (a dialog), so the menu-item verb does not complete.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem"));
            var saveDlg = GetNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("SRM_targets.sky"));
            Connector.Accept(saveDlg.FormId);
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

            // Open the Start Page (File > Start). This opens a dialog rather than completing, so resolve the page
            // it opened right away (GetConnectorForm) instead of waiting.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "startPageMenuItem"));
            var startPage = GetConnectorForm<StartPage>();
            PauseForScreenShot(startPage, "Start Page -- Import DIA Peptide Search"); // s-01

            // Click the "Import DIA Peptide Search" tile (matched by its localized caption from the resx). It
            // first shows a "You must save this document before importing a peptide search" message (OK/Cancel);
            // accepting it (OK) brings up the native Save As dialog. Save as DIA_to_SRM_Tutorial.sky and the wizard
            // opens. Each of these gestures opens the NEXT dialog rather than completing, so none is AssertComplete.
            startPage.ClickButton(StartupResources.StartPage_PopulateWizardPanel_Import_DIA_Peptide_Search);
            Connector.Accept(GetConnectorForm<MultiButtonMsgDlg>().FormId);

            var saveDlg = GetNativeFileDialog();
            saveDlg.SetValue("FileName", GetTestPath("DIA_to_SRM_Tutorial.sky"));
            Connector.Accept(saveDlg.FormId);

            return GetConnectorForm<ImportPeptideSearchDlg>();
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
            AssertComplete(wizard.ClickButton(GetLocalizedText<BuildPeptideSearchLibraryControl>("radioExistingLibrary")));
            AssertComplete(wizard.SetValue(GetLocalizedText<BuildPeptideSearchLibraryControl>("lblLibraryPath"),
                GetTestPath("CSF_GPFLib_QRcombined.elib")));
            PauseForScreenShot(wizard, "Build Spectral Library -- use existing library"); // s-02
            AssertComplete(wizard.ClickButton(GetLocalizedText<ImportPeptideSearchDlg>("btnNext")));

            // Extract Chromatograms page: nothing to add here (results are imported later), so just capture it.
            // Clicking Next above loaded the library and swaps this page in; the experiment assumes the click has
            // fully settled that transition on return (no WaitForControl), which the screenshot then relies on.
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
            // Next opens the "no results files, continue?" prompt (a dialog), so it does not complete; accepting
            // that prompt advances the wizard, which we DO expect to complete.
            wizard.ClickButton(WizardNextButton);
            AssertComplete(Connector.Accept(GetConnectorForm<MultiButtonMsgDlg>().FormId));

            // 1.3 Add Modifications: no modifications were used in the search, so just move on. No WaitForControl --
            // the accept above is assumed to have settled the Add Modifications page.
            PauseForScreenShot(wizard, "Add Modifications"); // s-04
            AssertComplete(wizard.ClickButton(WizardNextButton));

            // 1.4 Configure Transition Settings. Text fields (charges/types/m-z/tolerance/counts) are
            // language-neutral and addressed by their localized labels; the two ion-range combo boxes are set
            // by their (currently English) item text -- see the localization note at the bottom of this file.
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblPrecursorCharges"), "2, 3"));
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCharges"), "1, 2"));
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonTypes"), "y, b"));
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label1"), "ion 3"));     // product ions from
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label2"), "last ion"));  // product ions to
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label3"), "50"));        // min m/z
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("label6"), "2000"));      // max m/z
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblTolerance"), 0.005.ToString(CultureInfo.CurrentCulture)));
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCount"), "8"));    // pick N product ions
            // The min-product-ions box sits between two unit labels; the connector pairs a caption-less field
            // with the label before it in tab order, which here is "product ions" (lblIonCountUnits), not the
            // "min product ions" suffix label that follows the box.
            AssertComplete(wizard.SetValue(GetLocalizedText<TransitionSettingsControl>("lblIonCountUnits"), "3"));
            PauseForScreenShot(wizard, "Configure Transition Settings"); // s-05
            AssertComplete(wizard.ClickButton(WizardNextButton));

            // 1.5 Configure Full-Scan Settings: the defining DIA choices are a Centroided product mass analyzer
            // and the Results-only isolation scheme (set below). The mass-accuracy value and the "use only scans
            // within N minutes of MS/MS IDs" retention-time filter are left at the DIA wizard's defaults: their
            // fields are relabeled at runtime / have split unit labels, so they are not cleanly addressable by
            // caption yet (a remaining item to wire up, possibly needing a connector tweak).
            AssertComplete(wizard.SetValue(GetLocalizedText<FullScanSettingsControl>("label22"), "Centroided"));        // product mass analyzer
            AssertComplete(wizard.SetValue(GetLocalizedText<FullScanSettingsControl>("labelIsolationScheme"), "Results only"));
            PauseForScreenShot(wizard, "Configure Full-Scan Settings"); // s-06
            AssertComplete(wizard.ClickButton(WizardNextButton));
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
            AssertComplete(wizard.SetValue(GetLocalizedText<ImportFastaControl>("label3"), "Trypsin [KR | P]"));   // enzyme
            AssertComplete(wizard.SetValue(GetLocalizedText<ImportFastaControl>("label2"), "0"));                  // max missed cleavages
            // Browse opens the native Open dialog (a dialog, so it does not complete); accept it to load the FASTA.
            wizard.ClickButton(GetLocalizedText<ImportFastaControl>("browseFastaBtn"));
            var fastaDlg = GetNativeFileDialog();
            fastaDlg.SetValue("FileName", GetTestPath("uniprot_human_25apr2019.fasta"));
            Connector.Accept(fastaDlg.FormId);
            PauseForScreenShot(wizard, "Import FASTA"); // s-07

            // Finish the wizard. Building peptides from the FASTA brings up the Associate Proteins dialog.
            wizard.ClickButton(WizardFinishButton);

            // 1.7 Associate Proteins: create protein groups first (which relabels the shared-peptides options to
            // their grouped form), then drop shared (non-unique) peptides. The combo item text comes straight
            // from the localized EnumNames resource, so it matches exactly in any language. Min peptides per
            // protein stays at its default of 1 (the connector does not yet address the NumericUpDown holding it).
            // Gap: clicking Finish completes the gesture, but the FASTA digestion that raises Associate Proteins
            // runs asynchronously afterward, so the dialog is not open on return -- wait for it (not GetConnectorForm).
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
            // These two options are addressed by structured Path (the controls have no caption), so they go through
            // the PerformAction escape hatch, which stays fire-and-forget (no ActionResult to AssertComplete). It
            // has no completion signal, so each is wrapped in WaitForAction to wait the posted gesture out -- without
            // it, the next poll races an in-flight gesture (GetControls returns null mid-gesture).
            var groupProteinsCheckBox = associate.GetControls().First(control => Equals(control.Path.Type, @"CheckBox")).Path;
            WaitForAction(() => associate.PerformAction(groupProteinsCheckBox, UiActions.SetValue, "true"));
            // Drop shared (non-unique) peptides via the dialog's only combo box; the value is the localized
            // EnumNames resource, so it matches in any language. Address it by type and position only (clearing
            // the path's Text) -- the way a client would target "the only combo box" -- because enabling
            // grouping re-lays-out the dialog and changes the label the combo would otherwise be matched by.
            var sharedPeptidesCombo = associate.GetControls()
                .Single(control => Equals(control.Path.Type, @"ComboBox")).Path.ChangeText(null);
            WaitForAction(() => associate.PerformAction(sharedPeptidesCombo, UiActions.SetValue, EnumNames.SharedPeptidesGroup_Removed));
            // Each option change recomputes the parsimony on a background thread and DISABLES the OK button while it
            // works; Accept would otherwise click a still-disabled OK (a no-op that leaves the dialog open yet
            // reports complete), so wait for the OK button to re-enable first.
            WaitForControlEnabled(associate, @"Button");
            PauseForScreenShot(associate, "Associate Proteins"); // s-08
            AssertComplete(Connector.Accept(associate.FormId));

            // Capture the populated Targets view of the main window (assumed built by the accept above).
            PauseForScreenShot(GetConnectorForm<SkylineWindow>(), "Targets populated"); // s-09
        }

        /// <summary>
        /// Step 1.8: add the PRTC indexed retention-time standards (which were not in the spectral library) by
        /// importing them from an existing document (File &gt; Import &gt; Document), so they are extracted along
        /// with everything else. Capture the populated Targets view (s-10) and save.
        /// </summary>
        private void ImportPrtcDocument()
        {
            // Import Document opens the native Open dialog (a dialog), so it does not complete; accept it to import.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importDocumentMenuItem"));
            var importDlg = GetNativeFileDialog();
            importDlg.SetValue("FileName", GetTestPath("PRTC.sky"));
            Connector.Accept(importDlg.FormId);
            PauseForScreenShot(GetConnectorForm<SkylineWindow>(), "Targets with PRTC added"); // s-10

            // Save blocks in a modal "Saving..." progress dialog until the (large) document is written; the
            // connector's InvokeMenuItem rides through that progress dialog and is expected to complete, so the
            // save is finished on return.
            AssertComplete(Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem")));
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

            // Import Results opens a dialog (so it does not complete): either a conditional "no decoys -- add
            // them?" prompt or the Import Results dialog. Because the menu-item verb returns only once one of them
            // is up, query the open forms directly (no WaitForCondition) and decline the decoy prompt if present.
            Connector.InvokeMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importResultsMenuItem"));
            var decoyPrompt = Connector.GetOpenForms().FirstOrDefault(f => Equals(f.Type, nameof(MultiButtonMsgDlg)));
            if (decoyPrompt != null)
                Connector.ClickFormButton(decoyPrompt.Id, UiElement.NormalizeLabel(MultiButtonMsgDlg.BUTTON_NO));

            // Import Results: each subfolder of the chosen directory becomes a replicate whose data files are
            // its injections. Choose that option (s-11), then OK opens the native folder browser.
            var importResults = GetConnectorForm<ImportResultsDlg>();
            AssertComplete(importResults.ClickButton(GetLocalizedText<ImportResultsDlg>("radioCreateMultipleMulti")));
            PauseForScreenShot(importResults, "Import Results -- multi-injection replicates in directories"); // s-11
            Connector.Accept(importResults.FormId);

            // Pick the assembled mzmls folder in the native Browse-For-Folder dialog (its LibA/LibB/LibC
            // subfolders are the replicates). The connector selects the folder by path; its controlId is ignored.
            var folderDlg = GetNativeFolderDialog();
            folderDlg.SetValue(@"Folder", mzmlFolder);
            Connector.Accept(folderDlg.FormId);

            // The replicate names share a common prefix; keep the full folder names (Do not remove), then OK.
            var nameDlg = GetConnectorForm<ImportResultsNameDlg>();
            AssertComplete(nameDlg.ClickButton(GetLocalizedText<ImportResultsNameDlg>("radioDontRemove")));
            // Accept closes the dialog and starts the (slow) chromatogram extraction from the gas-phase runs. The
            // experiment assumes it completes on return -- no WaitForDocumentLoaded -- which for a heavy background
            // import is a prime place to find the assumption failing.
            AssertComplete(Connector.Accept(nameDlg.FormId));
            // Save (rides its "Saving..." progress dialog); expected to complete.
            AssertComplete(Connector.InvokeMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem")));
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
