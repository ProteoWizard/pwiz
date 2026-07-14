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
    /// tutorial action through the in-process <see cref="IJsonToolService"/> (see <see cref="McpConnectorTest"/>)
    /// -- both to reproduce the tutorial and to verify the connector is capable enough to run it end-to-end.
    ///
    /// The screenshots it captures (with IsPauseForScreenShots) are written to the draft tutorial folder
    /// (see <see cref="McpConnectorTest.TutorialDocumentationFolder"/>); the matching HTML is at
    /// Documentation\Tutorial-Drafts\DIAtoSRM\en\index.html.
    /// </summary>
    [TestClass]
    public class DiaToSrmTutorialTest : McpConnectorTest
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

            // One ZIP holding exactly what the tutorial needs, downloaded to a persistent local cache and reused
            // across runs. It repackages the webinar's four downloads (Webinar22.zip plus a ~3 GB library zip per
            // gas-phase replicate): the .mzML runs already sit under mzmls\LibA|LibB|LibC -- the layout the
            // multi-injection import of Step 1.9 wants, one subfolder per replicate -- so nothing has to be copied
            // into place at run time, and the files the webinar ships but the tutorial never reads (the PRTC .raw
            // runs -- PRTC.sky has no results) are left out.
            TestFilesZip = @"https://proteome.gs.washington.edu/~nicksh/test/Webinar22_DIAtoSRM.zip";

            // The gas-phase fractionated runs are huge (~36 GB of .mzML); extract them once to a shared persistent
            // location and reuse them in place across runs rather than re-extracting every run. (The .elib is
            // deliberately not persisted -- loading it creates a .elibc cache beside it, which would trip the
            // "persistent files were modified" check.)
            TestFilesPersistent = new[] { @".mzML" };

            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDir.GetTestPath(Path.Combine("Webinar22", relativePath));
        }

        /// <summary>
        /// The folder holding the three gas-phase-fractionated replicates (mzmls\LibA|LibB|LibC, each a subfolder
        /// of six m/z-range injections) that Step 1.9 imports as multi-injection replicates. It is built from the
        /// PERSISTENT files dir rather than through <see cref="GetTestPath"/>, because GetTestPath redirects a
        /// persistent FILE (one whose path contains ".mzML") but not the DIRECTORY that holds them.
        /// </summary>
        private string MzmlReplicateFolder => Path.Combine(
            TestFilesDir.PersistentFilesDir ?? TestFilesDir.FullPath, "Webinar22", "mzmls");

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
        private string GettingStarted()
        {
            // The tutorial begins by reverting to default settings, but the test starts from a clean default
            // document so that step is unnecessary here (and it is not a numbered screenshot). Just ensure the
            // Proteomics interface, the way the protein icon in the corner indicates.
            McpConnector.SetUiMode("proteomic");

            // Open the Start Page (File > Start). This opens a dialog rather than completing, so resolve the page it
            // opened straight from the menu action's ActionResult.FormId (ResolveModal) instead of waiting for it.
            var startPage = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<SkylineWindow>("fileToolStripMenuItem", "startPageMenuItem")));
            PauseForScreenShot(startPage, "Start Page -- Import DIA Peptide Search"); // s-01

            // Click the "Import DIA Peptide Search" tile (matched by its localized caption from the resx). It
            // first shows a "You must save this document before importing a peptide search" message (OK/Cancel);
            // accepting it (OK) brings up the native Save As dialog. Save as DIA_to_SRM_Tutorial.sky and the wizard
            // opens. Each of these gestures opens the NEXT dialog rather than completing, so none is AssertComplete.
            // The tile's click is POSTED, and the prompt is then shown by the startup frame -- so it is not up when
            // the click returns. WAIT for it (GetOpenFormId does not).
            McpConnector.ClickFormButton(startPage, StartupResources.StartPage_PopulateWizardPanel_Import_DIA_Peptide_Search);
            McpConnector.DismissWithAcceptButton(WaitForMcpConnectorForm<MultiButtonMsgDlg>());

            // The Start Page tile sets DialogResult and returns; the "must save" prompt and this Save dialog are then
            // shown by the startup frame (StartupActions), NOT by a counted connector gesture -- so accepting the
            // prompt completes (its window is gone) before the Save dialog appears. Wait for it rather than assume it.
            var saveDlg = WaitForNativeFileDialog();
            McpConnector.SetFormValue(saveDlg, "FileName", GetTestPath("DIA_to_SRM_Tutorial.sky"));
            McpConnector.DismissWithAcceptButton(saveDlg);

            return GetOpenFormId<ImportPeptideSearchDlg>();
        }

        /// <summary>
        /// Step 1.1-1.2 of the wizard: point the Build Spectral Library page at the existing GPF DIA
        /// chromatogram library (s-02), then move to the Extract Chromatograms page and leave its result files
        /// empty -- the gas-phase fractionated results are imported later as multi-injection replicates (s-03).
        /// </summary>
        private void BuildLibraryAndExtractChromatograms(string wizard)
        {
            // Build Spectral Library page: choose "Use existing" (which reveals the library path box), then set
            // the path to the EncyclopeDIA .elib. Controls are addressed by their localized captions, pulled
            // from the resources so the test works in any UI language. Each connector verb waits out its posted
            // action, so the next sees its effect.
            AssertComplete(McpConnector.ClickFormButton(wizard, GetLocalizedText<BuildPeptideSearchLibraryControl>("radioExistingLibrary")));
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<BuildPeptideSearchLibraryControl>("lblLibraryPath"),
                GetTestPath("CSF_GPFLib_QRcombined.elib")));
            PauseForScreenShot(wizard, "Build Spectral Library -- use existing library"); // s-02
            AssertComplete(McpConnector.ClickFormButton(wizard, GetLocalizedText<ImportPeptideSearchDlg>("btnNext")));

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
        private void ConfigureTransitionAndFullScanSettings(string wizard)
        {
            // 1.2 -> advance past Extract Chromatograms. With no result files added, the wizard warns that it
            // will create a template document with no imported results and asks whether to continue; the
            // multi-injection GPF results are imported later (Step 1.9). Accept() presses the prompt's default
            // (Yes) without keying on a localized caption.
            // Next's click is POSTED, so the prompt is not up when it returns -- WAIT for the prompt. Accepting it
            // advances the wizard, which we DO expect to complete.
            McpConnector.ClickFormButton(wizard, WizardNextButton);
            AssertComplete(McpConnector.DismissWithAcceptButton(WaitForMcpConnectorForm<MultiButtonMsgDlg>()));

            // 1.3 Add Modifications: no modifications were used in the search, so just move on. No WaitForControl --
            // the accept above is assumed to have settled the Add Modifications page.
            PauseForScreenShot(wizard, "Add Modifications"); // s-04
            AssertComplete(McpConnector.ClickFormButton(wizard, WizardNextButton));

            // 1.4 Configure Transition Settings. Text fields (charges/types/m-z/tolerance/counts) are
            // language-neutral and addressed by their localized labels; the two ion-range combo boxes are set
            // by their (currently English) item text -- see the localization note at the bottom of this file.
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblPrecursorCharges"), "2, 3"));
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblIonCharges"), "1, 2"));
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblIonTypes"), "y, b"));
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("label1"), "ion 3"));     // product ions from
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("label2"), "last ion"));  // product ions to
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("label3"), "50"));        // min m/z
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("label6"), "2000"));      // max m/z
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblTolerance"), 0.005.ToString(CultureInfo.CurrentCulture)));
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblIonCount"), "8"));    // pick N product ions
            // The min-product-ions box sits between two unit labels; the connector pairs a caption-less field
            // with the label before it in tab order, which here is "product ions" (lblIonCountUnits), not the
            // "min product ions" suffix label that follows the box.
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<TransitionSettingsControl>("lblIonCountUnits"), "3"));
            PauseForScreenShot(wizard, "Configure Transition Settings"); // s-05
            AssertComplete(McpConnector.ClickFormButton(wizard, WizardNextButton));

            // 1.5 Configure Full-Scan Settings: the defining DIA choices are a Centroided product mass analyzer
            // and the Results-only isolation scheme (set below). The mass-accuracy value and the "use only scans
            // within N minutes of MS/MS IDs" retention-time filter are left at the DIA wizard's defaults: their
            // fields are relabeled at runtime / have split unit labels, so they are not cleanly addressable by
            // caption yet (a remaining item to wire up, possibly needing a connector tweak).
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<FullScanSettingsControl>("label22"), "Centroided"));        // product mass analyzer
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<FullScanSettingsControl>("labelIsolationScheme"), "Results only"));
            PauseForScreenShot(wizard, "Configure Full-Scan Settings"); // s-06
            AssertComplete(McpConnector.ClickFormButton(wizard, WizardNextButton));
        }

        /// <summary>
        /// Step 1.6-1.7: on the Import FASTA page choose Trypsin with 0 missed cleavages and the human FASTA
        /// (s-07), Finish, then configure the Associate Proteins dialog (s-08) and capture the populated Targets
        /// view once the wizard builds the document (s-09).
        /// </summary>
        private void ImportFastaAndAssociateProteins(string wizard)
        {
            // 1.6 Import FASTA (required): Trypsin [KR | P] / 0 missed cleavages (enzyme names are settings, not
            // localized), then browse to the human FASTA through the native Open dialog.
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<ImportFastaControl>("label3"), "Trypsin [KR | P]"));   // enzyme
            AssertComplete(McpConnector.SetFormValue(wizard, GetLocalizedText<ImportFastaControl>("label2"), "0"));                  // max missed cleavages
            // Browse opens the native Open dialog (a dialog, so it does not complete); accept it to load the FASTA.
            McpConnector.ClickFormButton(wizard, GetLocalizedText<ImportFastaControl>("browseFastaBtn"));
            var fastaDlg = WaitForNativeFileDialog();
            McpConnector.SetFormValue(fastaDlg, "FileName", GetTestPath("uniprot_human_25apr2019.fasta"));
            McpConnector.DismissWithAcceptButton(fastaDlg);
            PauseForScreenShot(wizard, "Import FASTA"); // s-07

            // Finish the wizard. Building peptides from the FASTA brings up the Associate Proteins dialog.
            McpConnector.ClickFormButton(wizard, WizardFinishButton);

            // 1.7 Associate Proteins: create protein groups first (which relabels the shared-peptides options to
            // their grouped form), then drop shared (non-unique) peptides. The combo item text comes straight
            // from the localized EnumNames resource, so it matches exactly in any language. Min peptides per
            // protein stays at its default of 1 (the connector does not yet address the NumericUpDown holding it).
            // Gap: clicking Finish completes the gesture, but the FASTA digestion that raises Associate Proteins
            // runs asynchronously afterward, so the dialog is not open on return -- wait for it (not GetOpenFormId).
            var associate = WaitForMcpConnectorForm<AssociateProteinsDlg>();
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
            var groupProteinsCheckBox = McpConnector.GetControls(associate).First(control => Equals(control.Path.Type, @"CheckBox")).Path;
            WaitForAction(() => McpConnector.PerformAction(groupProteinsCheckBox, @"set_value", "true"));
            // Drop shared (non-unique) peptides via the dialog's only combo box; the value is the localized
            // EnumNames resource, so it matches in any language. Address it by type and position only (clearing
            // the path's Text) -- the way a client would target "the only combo box" -- because enabling
            // grouping re-lays-out the dialog and changes the label the combo would otherwise be matched by.
            var sharedPeptidesCombo = McpConnector.GetControls(associate)
                .Single(control => Equals(control.Path.Type, @"ComboBox")).Path.ChangeText(null);
            WaitForAction(() => McpConnector.PerformAction(sharedPeptidesCombo, @"set_value", EnumNames.SharedPeptidesGroup_Removed));
            // Each option change recomputes the parsimony on a background thread and DISABLES the OK button while it
            // works; Accept would otherwise click a still-disabled OK (a no-op that leaves the dialog open yet
            // reports complete), so wait for the OK button to re-enable first.
            WaitForControlEnabled(associate, @"Button");
            PauseForScreenShot(associate, "Associate Proteins"); // s-08
            AssertComplete(McpConnector.DismissWithAcceptButton(associate));

            // Capture the populated Targets view of the main window (assumed built by the accept above).
            PauseForScreenShot(GetOpenFormId<SkylineWindow>(), "Targets populated"); // s-09
        }

        /// <summary>
        /// Step 1.8: add the PRTC indexed retention-time standards (which were not in the spectral library) by
        /// importing them from an existing document (File &gt; Import &gt; Document), so they are extracted along
        /// with everything else. Capture the populated Targets view (s-10) and save.
        /// </summary>
        private void ImportPrtcDocument()
        {
            // Import Document opens the native Open dialog (a dialog), so it does not complete; resolve it from the
            // menu action's ActionResult, then accept it to import.
            var importDlg = ResolveModal(McpConnector.ClickMainMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importDocumentMenuItem")));
            McpConnector.SetFormValue(importDlg, "FileName", GetTestPath("PRTC.sky"));
            McpConnector.DismissWithAcceptButton(importDlg);
            PauseForScreenShot(GetOpenFormId<SkylineWindow>(), "Targets with PRTC added"); // s-10

            // Save blocks in a modal "Saving..." progress dialog until the (large) document is written; the
            // connector's menu click rides through that progress dialog and is expected to complete, so the
            // save is finished on return.
            AssertComplete(McpConnector.ClickMainMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem")));
        }

        /// <summary>
        /// Step 1.9: import the gas-phase fractionated DIA runs as multi-injection replicates -- one replicate
        /// per LibA/LibB/LibC subfolder, each with its six m/z-range injections -- declining decoys and keeping
        /// the full folder names (s-11), then extract the chromatograms. This is the heavy data step; it is why
        /// the test lives in TestPerf and only runs with perftests on.
        /// </summary>
        private void ImportGpfDiaResults()
        {
            // Import Results opens a dialog (so it does not complete): either a conditional "no decoys -- add
            // them?" prompt or the Import Results dialog. Because the menu-item verb returns only once one of them
            // is up, query the open forms directly (no WaitForCondition) and decline the decoy prompt if present.
            McpConnector.ClickMainMenuItem(MenuPath<SkylineWindow>(
                "fileToolStripMenuItem", "importToolStripMenuItem", "importResultsMenuItem"));
            var decoyPrompt = McpConnector.GetOpenForms().FirstOrDefault(f => Equals(f.Type, nameof(MultiButtonMsgDlg)));
            if (decoyPrompt != null)
                McpConnector.ClickFormButton(decoyPrompt.Id, UiElement.NormalizeLabel(MultiButtonMsgDlg.BUTTON_NO));

            // Import Results: each subfolder of the chosen directory becomes a replicate whose data files are
            // its injections. Choose that option (s-11), then OK opens the native folder browser.
            var importResults = GetOpenFormId<ImportResultsDlg>();
            AssertComplete(McpConnector.ClickFormButton(importResults, GetLocalizedText<ImportResultsDlg>("radioCreateMultipleMulti")));
            PauseForScreenShot(importResults, "Import Results -- multi-injection replicates in directories"); // s-11
            // Accepting Import Results opens the native Browse-For-Folder dialog; resolve it from the accept's
            // ActionResult.FormId (its LibA/LibB/LibC subfolders are the replicates). The connector selects the
            // folder by path; its controlId is ignored.
            var folderDlg = ResolveModal(McpConnector.DismissWithAcceptButton(importResults));
            McpConnector.SetFormValue(folderDlg, @"Folder", MzmlReplicateFolder);

            // Accepting the folder dialog opens the ImportResultsNameDlg; resolve it from that accept's
            // ActionResult.FormId. The replicate names share a common prefix; keep the full folder names (Do not
            // remove), then OK.
            var nameDlg = ResolveModal(McpConnector.DismissWithAcceptButton(folderDlg));
            AssertComplete(McpConnector.ClickFormButton(nameDlg, GetLocalizedText<ImportResultsNameDlg>("radioDontRemove")));
            // Accept closes the dialog and starts the (slow) chromatogram extraction from the gas-phase runs. Its
            // progress dialog is transient, so the accept completes as soon as that dialog goes away -- but the
            // extraction itself keeps running in the background. Everything downstream reads PEAK AREAS (the CV
            // histogram, and the consistency refine that keeps peptides under a 30% CV), and a peptide whose
            // chromatograms have not loaded yet has no CV at all -- so refining here would silently drop EVERY
            // peptide. Wait the extraction out. It is the heavy step of the tutorial (tens of GB of gas-phase
            // runs), hence the long timeout.
            AssertComplete(McpConnector.DismissWithAcceptButton(nameDlg));
            WaitForDocumentLoaded(60 * 60 * 1000);
            // Save (rides its "Saving..." progress dialog); expected to complete.
            AssertComplete(McpConnector.ClickMainMenuItem(MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveMenuItem")));
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
            AssertComplete(McpConnector.ClickMainMenuItem(MenuPath<ViewMenu>(
                "viewToolStripMenuItem", "peakAreasMenuItem", "areaCVHistogramMenuItem")));
            var cvHistogram = GetMcpConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram, "Peak Areas -- CV Histogram"); // s-12

            // Raise the CV-cutoff line to 30% through the histogram's right-click Properties dialog. The graph has no
            // menu bar and no toolbar, so naming no control reaches its RIGHT-CLICK menu -- the only menu it has.
            McpConnector.ClickControlMenuItem(cvHistogram, string.Empty,
                GetLocalizedText<PeakAreasContextMenu>("areaPropsContextMenuItem"));
            var cvProperties = GetOpenFormId<AreaCVToolbarProperties>();
            AssertComplete(McpConnector.SetFormValue(cvProperties, GetLocalizedText<AreaCVToolbarProperties>("label2"), "30")); // CV cutoff
            AssertComplete(McpConnector.DismissWithAcceptButton(cvProperties));
            var cvHistogram30 = GetMcpConnectorGraph(GraphsResources.Extensions_CustomToString_CV_Histogram);
            PauseForScreenShot(cvHistogram30, "CV Histogram -- 30% cutoff"); // s-13

            // 2.2 Refine > Advanced opens the RefineDlg (a dialog), so the menu-item verb does not complete -- resolve
            // the dialog from its ActionResult.FormId.
            var refine = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem")));
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabDocument"));
            AssertComplete(McpConnector.SetFormValue(refine, GetLocalizedText<RefineDlg>("label1"), "2")); // Min peptides per protein
            PauseForScreenShot(refine, "Refine -- Document tab"); // s-14

            // Consistency tab: keep only peptides under 30% CV across the replicates. The other options the
            // tutorial lists are already this document's defaults, so only the cutoff needs setting -- Transition
            // type is "Products" (the sole transition type present, so RefineDlg leaves that combo disabled),
            // Normalize to defaults to "None", and Summed transitions defaults to "all".
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabConsistency"));
            AssertComplete(McpConnector.SetFormValue(refine, GetLocalizedText<RefineDlg>("labelCV"), "30")); // CV cutoff %
            PauseForScreenShot(refine, "Refine -- Consistency tab"); // s-15
            // Accept runs the refine (dropping the peptides/proteins that fail the filters, possibly via a progress
            // dialog). Accept completes when the dialog closes, but the refine's background reintegration keeps
            // loading -- Gap: Save As below requires a fully-loaded document, so wait for that load here.
            AssertComplete(McpConnector.DismissWithAcceptButton(refine));
            WaitForDocumentLoaded();

            // The consistency filter drops every peptide whose CV it cannot compute, so a document whose peak areas
            // are missing refines away to NOTHING -- and the next step then fails obscurely, on a protein list that
            // matches an empty document. Fail here instead, where the cause is.
            RunUI(() => Assert.AreNotEqual(0, SkylineWindow.Document.MoleculeGroupCount,
                "The CV refine removed every protein. Were the chromatograms fully loaded?"));

            // Save As opens the native Save dialog (a dialog), so the menu-item verb does not complete -- resolve the
            // dialog from its ActionResult.FormId.
            var saveDlg = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem")));
            McpConnector.SetFormValue(saveDlg, "FileName", GetTestPath("DIA_to_SRM_Tutorial-filtered.sky"));
            McpConnector.DismissWithAcceptButton(saveDlg);
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
            var acceptProteins = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<RefineMenu>("refineToolStripMenuItem", "acceptProteinsMenuItem")));
            AssertComplete(McpConnector.SetFormValue(acceptProteins, GetLocalizedText<RefineProteinListDlg>("label1"),
                File.ReadAllText(GetTestPath("target_proteins.txt"))));
            AssertComplete(McpConnector.ClickFormButton(acceptProteins, GetLocalizedText<RefineProteinListDlg>("proteinNames"))); // match by Names
            PauseForScreenShot(acceptProteins, "Accept Proteins -- paste protein list"); // s-16

            // OK checks the pasted names against the document; because some target proteins are not in the
            // document, a prompt lists them and asks whether to continue. This click opens that prompt (a dialog),
            // so it does not complete; resolve the prompt immediately.
            // Gap: OK checks the pasted names against the document on a background pass, then raises the "not in
            // document" prompt asynchronously -- so it is not open on return; wait for it.
            McpConnector.ClickFormButton(acceptProteins, GetLocalizedText<RefineProteinListDlg>("btnOk"));
            var notInDocument = WaitForMcpConnectorForm<MultiButtonMsgDlg>();
            PauseForScreenShot(notInDocument, "Proteins not in document"); // s-17
            // Accept (OK) dismisses the prompt, which lets Accept Proteins run the refine that drops the unlisted
            // proteins. Same gap as RefineByCv: the refine's background reintegration keeps loading after Accept
            // returns, so wait for the document before the next refine.
            AssertComplete(McpConnector.DismissWithAcceptButton(notInDocument));
            WaitForDocumentLoaded();

            // 3.2 Peptide ranked intensity filtering: Refine > Advanced, Results tab -- keep each protein's 2 best
            // peptides and each peptide's 5 best transitions by ranked peak intensity. The two rank boxes are
            // caption-less and paired with the label before each in tab order ("Max peptide peak rank:" and
            // "Max transition peak rank:"). The other Results-tab options are left at their defaults, as the
            // tutorial notes they would not change this document.
            var refine = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<RefineMenu>("refineToolStripMenuItem", "refineAdvancedMenuItem")));
            SelectTab(refine, GetLocalizedText<RefineDlg>("tabResults"));
            AssertComplete(McpConnector.SetFormValue(refine, GetLocalizedText<RefineDlg>("label8"), "2"));           // max peptide peak rank
            AssertComplete(McpConnector.SetFormValue(refine, GetLocalizedText<RefineDlg>("labelMaxPeakRank"), "5")); // max transition peak rank
            PauseForScreenShot(refine, "Refine -- Results tab"); // s-18
            // Accept runs the refine (dropping the lower-ranked peptides/transitions, possibly via a progress
            // dialog); its background reintegration keeps loading after Accept returns, so wait for the document
            // before the Save As below (which requires a fully-loaded document).
            AssertComplete(McpConnector.DismissWithAcceptButton(refine));
            WaitForDocumentLoaded();

            // Expand all proteins (a synchronous menu action) -- expected to complete.
            AssertComplete(McpConnector.ClickMainMenuItem(MenuPath<EditMenu>(
                "editToolStripMenuItem", "expandAllToolStripMenuItem", "expandProteinsMenuItem")));
            PauseForScreenShot(GetOpenFormId<SkylineWindow>(), "Targets -- SRM peptide targets"); // s-19

            // Save As opens the native Save dialog (a dialog), so the menu-item verb does not complete -- resolve the
            // dialog from its ActionResult.FormId.
            var saveDlg = ResolveModal(McpConnector.ClickMainMenuItem(
                MenuPath<SkylineWindow>("fileToolStripMenuItem", "saveAsMenuItem")));
            McpConnector.SetFormValue(saveDlg, "FileName", GetTestPath("SRM_targets.sky"));
            McpConnector.DismissWithAcceptButton(saveDlg);
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
