/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf // This would be in tutorial tests if it didn't require a massive download
{
    /// <summary>
    /// Verify small molecules multidimensional spectral libraries tutorial operation
    /// </summary>
    [TestClass]
    public class SmallMolLibrariesTutorialTest : AbstractFunctionalTestEx
    {
        private const string Flies_M = "Flies_Ctrl_M_A_001_Neg.d";
        private const string Flies_F = "Flies_Ctrl_F_A_018_Neg.d";
        private const string F_A_018 = "F_A_018";

        [TestMethod]
        public void TestSmallMoleculeLibrariesTutorial()
        {
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "SmallMolLibraries";

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/SmallMoleculeIMSLibraries.pdf";

            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/tutorials/SmallMoleculeLibraries.zip",
                @"TestPerf\SmallMolLibrariesTutorialViews.zip",
            };

            TestFilesPersistent = new[] { Flies_M, Flies_F };

            RunFunctionalTest();            
        }

        private string GetFullDataPath(string fileName)
        {
            return TestFilesDirs[0].GetTestPath("SmallMoleculeLibraries\\" + fileName);
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

            //   •	On the Settings menu, click Default.
            //   •	Click No on the form asking if you want to save the current settings.
            RunUI(() => SkylineWindow.ResetDefaultSettings());

            var doc = SkylineWindow.Document;

            //   •	On the Settings menu, click Transition Settings.
            //   •	Click the Filter tab.
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(() =>
                SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.Filter));

            //   •	This data was collected in negative ionization mode so [M+H] and[M +] can be removed from the Precursor Adducts and Fragment Adducts fields. However, they are harmless if left as is since the library we will use has only negative ion mode entries.
            //   •	In the Precursor Adducts field, enter “[M-H], [M+HCOO], [M+CH3COO]”. 
            //   •	In the Fragment Adducts field, enter “[M-]”.
            RunUI(() =>
            {
                transitionSettingsUI.SmallMoleculePrecursorAdducts = "[M-H], [M+HCOO], [M+CH3COO]";
                transitionSettingsUI.SmallMoleculeFragmentAdducts = "[M-]";
                transitionSettingsUI.SmallMoleculeFragmentTypes = "f, p";
                transitionSettingsUI.Left = SkylineWindow.Right + 20;
            });
            //   •	The Transition Settings form should now look like this:
            PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings: Filter", 3);

            RunUI(() =>
            {
                //   •	Click the Full-Scan tab in the Transition Settings form.
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                //   •	 In the MS1 filtering section, set the Isotope peaks included field to Count.
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                //   •	Set the Precursor mass analyzer field to TOF.
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;
                //   •	Enter “20,000” in the Resolving power field.
                transitionSettingsUI.PrecursorRes = 20000;
                //   •	In the MS/MS filtering section, set the Acquisition method field to DIA.
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                //   •	Set the Isolation scheme field to All Ions.
                transitionSettingsUI.IsolationSchemeName = IsolationScheme.SpecialHandlingType.ALL_IONS;
                //   •	Enter “20,000” in the Resolving power field.
                transitionSettingsUI.ProductRes = 20000;
                //   •	Check Use high-selectivity extraction.
                transitionSettingsUI.UseSelectiveExtraction = true;
            });

            //   The Transition Settings form should look like this:
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan", 4);
            //   •	Click the OK button.
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            doc = WaitForDocumentChange(doc);

            //   Adding and Exploring a Spectral Library
            // Before you can explore the library, Skyline must be directed to its location by adding your library of interest to the global list of libraries for document editing.
            //   To get started with the small molecule library containing Drosophila lipids perform the following steps:
            //   •	From the Settings menu, click Molecule Settings.
            //   •	Click the Library tab.
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(() =>
                SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Library));
            //   •	Click the Edit List button.
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            //   •	Click the Add button in the Edit Libraries form.
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            var drosophilaLipids = "Drosophila Lipids";
            RunUI(() =>
            {
                //   •	Enter “Drosophila Lipids” in the Name field of the Edit Library form.
                addLibDlg.LibraryName = drosophilaLipids;
                //   •	Click the Browse button.
                //   •	Navigate to the SmallMoleculeLibraries folder created earlier.
                //   •	Select the “Drosophila_Lipids_Neg.blib” file.
                addLibDlg.LibraryPath = GetFullDataPath("Drosophila_Lipids_Neg.blib");
            });
            //   •	Click the Open button.
            //   •	Click the OK button in the Edit Library form.
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            //   •	Click the OK button in the Edit Libraries form.
            OkDialog(libListDlg, libListDlg.OkDialog);

            //   The Libraries list in the Molecule Settings form should now contain the Drosophila Lipids library you just created.
            //   •	Check the Drosophila Lipids checkbox to tell Skyline to use this library in the current document.
            //   •	If you have any other libraries in this list checked, uncheck them now.
            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new[] {drosophilaLipids};
                peptideSettingsUI.Left = SkylineWindow.Right + 20;
            });
            //   The Molecule Settings form should now look like:
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Molecule Settings - Library", 6);
            //   •	Click the OK button in the Molecule Settings form.
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            doc = WaitForDocumentChangeLoaded(doc);

            //   To open the library explorer and view the contents of the library you just added, do the following: 
            //   •	From the View menu, click Spectral Libraries.
            var viewLibUI = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                viewLibUI.Left = SkylineWindow.Right + 20;
                viewLibUI.Top = SkylineWindow.Top;
            });

            //   The library explorer should now resemble the image below:
            PauseForScreenShot("Library Explorer (probably need to resize wider)", 7);

            //  To add all the molecules in the library to your target list:
            //   •	Click the Add All button.
            //   •	A popup window will then notify you that you will add 34 molecules, 38 precursors, and 246 transitions to the document. Click Add All.
            var confirmAddDlg = ShowDialog<AlertDlg>(viewLibUI.AddAllPeptides);
            using (new CheckDocumentState(1, 34, 38, 246))
            {
                RunUI(confirmAddDlg.OkDialog);
                doc = WaitForDocumentChangeLoaded(doc);
            }

            //   •	Close the Spectral Library Explorer window.
            OkDialog(viewLibUI, viewLibUI.CancelDialog);

            //   Your Skyline window should now resemble:
            RunUI(() => SkylineWindow.Size = new Size(951, 607));
            PauseForScreenShot("Populated Skyline window", 8);

            //Importing Results Chromatogram.Data
            //    In this section, you will import the Drosophila data without utilizing IMS filtering. This is an initial look at the data to see the impact of interference among lipids and their shared fragments. To import the data, perform the following steps:
            //   •	On the File menu, click Save (Ctrl+S).
            //   •	Save this document in the tutorial folder you created.
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDirs[0].GetTestPath("Tutorial.sky")));

            //   •	From the File menu, choose Import and click Results.
            using (new WaitDocumentChange(null, true))
            {
                var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
                var importResultsDlg1 = ShowDialog<ImportResultsDlg>(askDecoysDlg.ClickNo);
                //   •	Set the Files to import simultaneously field to Many.
                //   •	Check Show chromatograms during import.
                //   The Import Results form will appear as follows:
                RunUI(() =>
                {
                    importResultsDlg1.Top = SkylineWindow.Top;
                    importResultsDlg1.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot<ImportResultsDlg>("Import Results", 9);
                var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                    importResultsDlg1.GetDataSourcePathsFile(null));
                //   •	Click the OK button.
                //   The Import Results Files form will now show the.d files you have extracted into the tutorial folder:
                //   •	Select both .d files.
                RunUI(() =>
                {
                    var path = Path.GetDirectoryName(GetFullDataPath(Flies_M));
                    openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(path);
                    openDataSourceDialog1.SelectAllFileType(ExtAgilentRaw);
                    openDataSourceDialog1.Left = importResultsDlg1.Left;
                    openDataSourceDialog1.Top = importResultsDlg1.Bottom + 10;
                });
                PauseForScreenShot<OpenDataSourceDialog>("Import Results Files selection form", 10);
                OkDialog(openDataSourceDialog1, openDataSourceDialog1.Open);

                //   •	Click the Open button.
                //   •	The Import Results window will ask if you would like to remove the common prefix and suffix to shorten the file names used in Skyline.Click OK to accept the names “F_A_018” and “M_A_001”.
                //This should start the import and cause Skyline to show the Importing Results progress form:
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                OkDialog(importResultsNameDlg, importResultsNameDlg.OkDialog);
                var allChromatograms = WaitForOpenForm<AllChromatogramsGraph>();
                RunUI(() =>
                {
                    allChromatograms.Top = SkylineWindow.Top;
                    allChromatograms.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot<AllChromatogramsGraph>("Importing results form", 11);
            }

            WaitForGraphs();

            //Reviewing the Extracted Ion Chromatograms
            //    Once the files are imported, you can examine the chromatograms to evaluate interference from peaks with retention times and m/z values within the tolerance of your target list.
            RunUI(() =>
            {
                //   •	From the Edit menu, choose Expand All and click Molecules (Ctrl+D).
                SkylineWindow.ExpandPeptides();
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.all);
                //   •	From the View menu, choose Arrange Graphs and click Tiled (Ctrl+T).
                SkylineWindow.ArrangeGraphs(DisplayGraphsType.Row); // With just two panes, Tiled may go either row or column, force row 
                //   •	Right-click in a chromatogram graph and click Synchronize Zooming(leave if already checked).
                SkylineWindow.SynchronizeZooming(true);
                // •	Right-click in a chromatogram graph and click Legend to hide the legend.
                SkylineWindow.ShowChromatogramLegends(false);
                //   •	Right-click in a chromatogram graph, choose Transitions and click Split Graph (leave if already checked).
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.Size = new Size(1487, 786);
            });
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, propDlg =>
            {
                propDlg.IsPeakWidthRelative = false;
                propDlg.TimeRange = 7; // minutes
                propDlg.OkDialog();
            });
            //  •	Select the molecule PC(16:0_18:1) and your spectra should appear as: 
            FindNode("PC(16:0_18:1)");
            PauseForScreenShot("Chromatograms - prtsc-paste-edit", 12);

            RestoreViewOnScreen(13);
            var libraryMatchView = WaitForOpenForm<GraphSpectrum>();
            RunUI(() => libraryMatchView.ZoomXAxis(100, 400));
            PauseForScreenShot("Library Match", 13);

            //Since there are only 38 precursors in this document, you may want to review all 38 to get an overall feel for how the XIC look prior to IMS filtering.Before starting this review, do the following:
            //   •	On the View menu, choose Retention Times and click Replicate Comparison (F8).
            //   •	Attach the Retention Times view to the left of the Library Match view by clicking in the title bar and dragging until the mouse cursor is inside the left-side docking icon.
            RunUI(() =>
            {
                SkylineWindow.ShowGraphRetentionTime(true, GraphTypeSummary.replicate);
                //   •	Right-click in the Retention Times view and click Legend to hide the legend in this graph.
                SkylineWindow.ShowRTLegend(false);
            });

            //   •	From the View menu, choose Peak Areas and click Replicate Comparison (F7).
            RestoreViewOnScreen(14);

            //   •	Attach the Peak Areas view above the Retention Times view by clicking in the title bar and dragging until the mouse cursor is inside the up-side docking icon.
            //    You should end up with a similar layout to that below:
            RunUI(() =>
            {
                SkylineWindow.Size = new Size(1310, 786);
            });
            if (IsPauseForScreenShots)
            {
                // Change selected node away and back to adjust graphs
                TreeNode selectedNode = null;
                RunUI(() => selectedNode = SkylineWindow.SequenceTree.SelectedNode);
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextNode);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = selectedNode);
                WaitForGraphs();
                PauseForScreenShot("Main window", 14);
            }

            //Skyline often does a good job picking peaks and most integration boundaries do not need to be edited.However, there are a few isomer pairs that require some manual peak picking. 
            //    The first two cases are the lysophospholipids (LPC and LPE), which are phospholipids with one fatty acyl chain cleaved off.These molecules chromatographically separate depending on the sn- position of the single fatty acyl chain.Here the library match ID retention time markers can be utilized to determine the elution order of the LPC(0:0/18:2)/LPC(18:2/0:0) and LPE(0:0/16:0)/LPE(16:0/0:0) pairs.Drag the integration boundaries with your mouse to integrate the correct peaks.Note that male and female fruit flies have vastly different lysophospholipid profiles, which was also observed across almost all lysophospholipids in a larger Drosophila study.
            //    The final isomer pair is near the bottom of the document. PG(16:0_18:3) and PG(16:1_18:2) have different fatty acyl compositions, but the same total number of carbons and double bonds, causing them to share the same precursor formula and m/z value.Again, use the library match ID retention time markers to determine the correct peak for each lipid. To integrate the correct peaks, either drag the integration boundaries with your mouse or click the retention time above the peak apex.The product XIC may not be useful until IMS filtering is utilized.
            //

            //Understanding and Utilizing the IMS Separation
            //To this point, we have ignored the IMS dimension in this data.To better understand the IMS separation, you need to look at the underlying spectra from which these chromatograms were extracted by doing the following:
            //   •	On the View menu, choose Auto-Zoom and click Best Peak (F11).
            //   •	Select the molecule PE(16:1_18:3).
            FindNode("PE(16:1_18:3)");
            WaitForGraphs();
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, propDlg =>
            {
                propDlg.IsPeakWidthRelative = true;
                propDlg.TimeRange = 3.4; // widths
                propDlg.OkDialog();
            });
            WaitForGraphs();
            PauseForScreenShot("Chromatogram", 15);

            //   •	Hover the mouse cursor over the precursor chromatogram peak apex until a blue circle appears that tracks the mouse movement, and click on it.
            ClickChromatogram(F_A_018, 14.81, 162.1E3, PaneKey.PRECURSORS);
            //   This should bring up the Full-Scan view showing a familiar two-dimensional spectrum in profile mode:
            RunUI(() => SkylineWindow.GraphFullScan.SetSpectrum(true));
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(true));
            PauseForScreenShot("2D plot", 16);

            //   •	Click the Show 2D Spectrum button  to change the plot to a three-dimensional spectrum with drift time.
            RunUI(() => SkylineWindow.GraphFullScan.SetSpectrum(false));
            PauseForScreenShot("3D plot", 16);

            //   •	Click the Zoom to Selection button to see the entire 3D MS1 spectrum at the selected retention time.
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            PauseForScreenShot("3D plot full range", 17);

            //    This is a fairly typical MS1 spectrum for IMS-MS lipidomics data.You can get a better sense of the data by zooming into multiple areas on this plot.You can also select other lipids and click on the blue circle at the apex of each precursor chromatogram peak to see how this plot can differ with retention time. An interesting example is PE(O-18:0/16:1), which has distinct ion distributions showing correlations between m/z and drift time for different lipid classes.
            //    To inspect a relevant MS/MS spectrum:
            //   •	Re-select the molecule PE(16:1_18:3) if you navigated away from it to view other MS1 spectra.
            FindNode("PE(16:1_18:3)");
            //   •	Click on the Zoom to Selection button to zoom back in.
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(true));
            WaitForGraphs();
            //   •	Hover the mouse over the FA 18:3(+O) fragment chromatogram peak apex until a teal colored circle appears that tracks the mouse movement, and click on it.
            ClickChromatogram(F_A_018, 14.83, 120.5E3, PaneKey.PRODUCTS);
            //   The Full-Scan graph should change to:
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(true));
            PauseForScreenShot("3D plot MSMS zoomed", 18);


            //You can see that at least three visible ions are contributing to the extracted intensities at 33, 37, and 44 ms.This goes back to the nature of lipid fragmentation as previously discussed, where most lipids with an 18:3 fatty acyl chain will share this fragment.The complexity is increased for fatty acyl chains fragments with fewer double bonds, such as 18:2 at m/z 279, which may have multiple ions as well as isotopic overlap from the abundant 18:3 fragment at m/z 277 contributing to the extracted intensity.A similar observation can be made with the FA 16:1(+O) fragment.
            //   •	Click the Zoom to Selection button again to see the entire 3D MS/MS spectrum.
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            PauseForScreenShot("3D plot MSMS full range", 18);


            //    Reimporting Data with Drift Time Filtering

            // Prior to changing the settings and reimporting the data, you may want to save the current Skyline document and create a second file in order to compare the data before and after IMS filtering. To do so:
            //    •	On the File menu, click Save As...
            //    •	Save the file with a different name than your original Skyline document, such as “Drosophila_Lipids_Neg_IMS_Filtered”, in the tutorial folder you created.
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDirs[0].GetTestPath(@"Drosophila_Lipids_Neg_IMS_Filtered.sky")));
            RunUI(() => SkylineWindow.Width -= 300);

            //   •	From the Settings menu, click Transition Settings.
            //   •	Click the Ion Mobility tab.
            transitionSettingsUI = ShowDialog<TransitionSettingsUI>(() =>
                SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));

            //   •	Check Use spectral library ion mobility values when present.
            //   •	Set the Window Type field to Resolving power.
            //   •	In the Resolving power field, enter “50”.
            RunUI(() =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                transitionSettingsUI.IonMobilityControl.IsUseSpectralLibraryIonMobilities = true;
                transitionSettingsUI.IonMobilityControl.IonMobilityFilterResolvingPower = 50;
                transitionSettingsUI.IonMobilityControl.WindowWidthType =
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
                transitionSettingsUI.Left = SkylineWindow.Right + 20;
            });
            //   •	The Transition Settings form should now look like this:
            PauseForScreenShot<TransitionSettingsUI.IonMobilityTab>("Transition Settings: IonMobility", 20);            //The Transition Settings should now look like:


            //   •	Click the OK button.
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            WaitForDocumentChange(doc);

            //   The results must now be reimported with the newly applied IMS settings.
            //   •	From the Edit menu, click Manage Results.
            //   •	Click the Re-import button. “*” Should appear to the left of F_A_018.
            //   •	Select M_A_001 in the Manage Results view.
            //   •	Click the Re-import button. “*” Should appear to the left of M_A_001.
            //   •	Click the OK button.
            doc = SkylineWindow.Document;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.ToArray();
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });
            //   This should start the re-import and cause Skyline to show the Importing Results progress form.
            WaitForDocumentChangeLoaded(doc);

            //   •	Click any other lipid in the Target list and re-select PE(16:1_18:3) to update the chromatograms.
            FindNode("PC(16:0_18:1)");
            WaitForGraphs();
            FindNode("PE(16:1_18:3)");
            WaitForGraphs();
            //   To explore the filtered data, perform the following:
            //   •	Click on the apex of the blue precursor chromatogram to show the Full-Scan graph.
            //   •	Click the Zoom to Selection button.
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(true));
            //   The Full-Scan graph should now look something like this:
            ClickChromatogram(F_A_018, 14.807, 152.0E3, PaneKey.PRECURSORS);

            if (IsCoverShotMode)
            {
                RestoreCoverViewOnScreen();
                // Need to click again to get the full-scan graph populated after restoring view
                ClickChromatogram(F_A_018, 14.807, 152.0E3, PaneKey.PRECURSORS);
                TakeCoverShot();
                return;
            }

            PauseForScreenShot("Full scan graph with IM filtering", 21);

            // Note that if you were interested in lipids that are not present in the current spectral library, you can add to it manually or using LipidCreator. To access the LipidCreator plugin, do the following:
            //   •	From the Tools menu, click Tool Store.
            if (IsPauseForScreenShots)
            {
                RunUI(() =>
                {
                    SkylineWindow.GraphFullScan.Close();
                    SkylineWindow.Width -= 300;
                });
                var configureToolsDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
                //   •	Select LipidCreator.
                var pick = ShowDialog<ToolStoreDlg>(configureToolsDlg.AddFromWeb);
                RunUI(() =>
                {
                    pick.SelectTool("LipidCreator");
                    pick.Left = SkylineWindow.Right + 20;
                });
                PauseForScreenShot("LipidCreator in tool store", 22);
                RunUI(() => pick.CancelDialog());
                OkDialog(configureToolsDlg, configureToolsDlg.Cancel);
                //   •	Click the Install button.
            }

            //   The following steps can be taken to easily export an updated spectral library:
            //   •	From the File menu, choose Export and click Spectral Library.
            //   •	Enter a file name and click Save.

        }
    }
}
