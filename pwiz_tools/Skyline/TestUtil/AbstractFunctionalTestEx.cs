/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using ZedGraph;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// An intermediate base class containing simplified functions for functional unit tests.
    /// </summary>
    public abstract class AbstractFunctionalTestEx : AbstractFunctionalTest
    {
        /// <summary>
        /// Run with optional zip file.
        /// </summary>
        /// <param name="zipFile">Path to zip file</param>
        public void Run(string zipFile = null)
        {
            if (zipFile != null)
                TestFilesZip = zipFile;
            RunFunctionalTest();
        }

        /// <summary>
        /// Open a document and wait for loading completion.
        /// </summary>
        /// <param name="documentPath">File path of document</param>
        public void OpenDocument(string documentPath)
        {
            var documentFile = TestFilesDir.GetTestPath(documentPath);
            WaitForCondition(() => File.Exists(documentFile));
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();
        }

        public void OpenDocumentNoWait(string documentPath)
        {
            var documentFile = TestFilesDir.GetTestPath(documentPath);
            WaitForCondition(() => File.Exists(documentFile));
            SkylineWindow.BeginInvoke((Action) (() => SkylineWindow.OpenFile(documentFile)));
        }

        public static void CheckConsistentLibraryInfo(SrmDocument doc = null)
        {
            foreach (var nodeGroup in (doc ?? SkylineWindow.Document).MoleculeTransitionGroups)
            {
                if (nodeGroup.HasLibInfo && nodeGroup.Transitions.Any() && nodeGroup.Transitions.All(nodeTran => !nodeTran.HasLibInfo))
                    Assert.Fail("Inconsistent library information");
            }
        }

        public void ConvertDocumentToSmallMolecules(RefinementSettings.ConvertToSmallMoleculesMode mode = RefinementSettings.ConvertToSmallMoleculesMode.formulas,
            RefinementSettings.ConvertToSmallMoleculesChargesMode invertCharges =  RefinementSettings.ConvertToSmallMoleculesChargesMode.none, 
            bool ignoreDecoys = false)
        {
            var doc = WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ModifyDocument("Convert to small molecules", document =>
            {
                var refine = new RefinementSettings();
                var path = Path.GetDirectoryName(SkylineWindow.DocumentFilePath);
                var smallMolDoc = refine.ConvertToSmallMolecules(document, path, mode, invertCharges, ignoreDecoys);
                CheckConsistentLibraryInfo(smallMolDoc);
                return smallMolDoc;
            }));
            WaitForDocumentChange(doc);

            var newDocFileName =
                SkylineWindow.DocumentFilePath.Contains(BiblioSpecLiteSpec.DotConvertedToSmallMolecules) ?
                SkylineWindow.DocumentFilePath :
                SkylineWindow.DocumentFilePath.Replace(".sky", BiblioSpecLiteSpec.DotConvertedToSmallMolecules + ".sky");
            RunUI(() => SkylineWindow.SaveDocument(newDocFileName));
            WaitForCondition(() => File.Exists(newDocFileName));
            RunUI(() => SkylineWindow.OpenFile(newDocFileName));
            WaitForDocumentLoaded();
            CheckConsistentLibraryInfo();

            Thread.Sleep(1000);
        }

        /// <summary>
        /// Import results from one or more data files.
        /// </summary>
        /// <param name="dataFiles">List of data file paths</param>
        /// <param name="lockMassParameters">For Waters lockmass correction</param>
        /// <param name="waitForLoadSeconds">Timeout in seconds</param>
        /// <param name="expectedErrorMessage">anticipated error dialog message, if any</param>
        public void ImportResults(string[] dataFiles, LockMassParameters lockMassParameters, int waitForLoadSeconds = 420, string expectedErrorMessage = null)
        {
            ImportResultsAsync(dataFiles, lockMassParameters, expectedErrorMessage);
            if (expectedErrorMessage != null)
                return;
            WaitForConditionUI(waitForLoadSeconds*1000,
                () => {
                    var document = SkylineWindow.DocumentUI;
                    return document.Settings.HasResults && document.Settings.MeasuredResults.IsLoaded;
                });
        }

        public void ImportResults(string dataFile, LockMassParameters lockMassParameters, int waitForLoadSeconds = 420, string expectedErrorMessage = null)
        {
            ImportResults(new[] { dataFile }, lockMassParameters, waitForLoadSeconds, expectedErrorMessage);
        }

        public void ImportResults(params string[] dataFiles)
        {
            ImportResults(dataFiles, null);
        }

        public void ImportResultsAsync(params string[] dataFiles)
        {
            ImportResultsAsync(dataFiles, null);
        }

        public void ImportResultsAsync(string[] dataFiles, LockMassParameters lockMassParameters, string expectedErrorMessage = null, bool? removeFix = null)
        {
            var doc = SkylineWindow.Document;
            ImportResultsDlg importResultsDlg;
            if (!Equals(doc.Settings.TransitionSettings.FullScan.AcquisitionMethod, FullScanAcquisitionMethod.DIA) ||
                doc.MoleculeGroups.Any(nodeGroup => nodeGroup.IsDecoy))
            {
                importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            }
            else
            {
                var askDecoysDlg = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ImportResults);
                importResultsDlg = ShowDialog<ImportResultsDlg>(askDecoysDlg.ClickNo);
            }
            RunUI(() =>
            {
                var filePaths = dataFiles.Select(dataFile => TestFilesDirs[0].GetTestPath(dataFile)).ToArray();
                importResultsDlg.NamedPathSets =
                    importResultsDlg.GetDataSourcePathsFileReplicates(filePaths.Select(MsDataFileUri.Parse));
            });
            if (expectedErrorMessage != null)
            {
                var dlg = WaitForOpenForm<MessageDlg>();
                Assert.IsTrue(dlg.DetailMessage.Contains(expectedErrorMessage));
                dlg.CancelDialog();
            }
            else if (lockMassParameters == null)
            {
                if (removeFix.HasValue)
                {
                    RunDlg<ImportResultsNameDlg>(importResultsDlg.OkDialog, resultsNames =>
                    {
                        if (removeFix.Value)
                            resultsNames.YesDialog();
                        else
                            resultsNames.NoDialog();
                    });
                }
                else
                {
                    OkDialog(importResultsDlg, importResultsDlg.OkDialog);
                }
            }
            else
            {
                // Expect a Waters lockmass dialog to appear on OK
                WaitForConditionUI(() => importResultsDlg.NamedPathSets.Length == dataFiles.Length);
                var lockmassDlg = ShowDialog<ImportResultsLockMassDlg>(importResultsDlg.OkDialog);
                RunUI(() =>
                {
                    lockmassDlg.LockmassPositive = lockMassParameters.LockmassPositive ?? 0;
                    lockmassDlg.LockmassNegative = lockMassParameters.LockmassNegative ?? 0;
                    lockmassDlg.LockmassTolerance = lockMassParameters.LockmassTolerance ?? 0;
                });
                OkDialog(lockmassDlg, lockmassDlg.OkDialog);
            }
            if (expectedErrorMessage == null)
                WaitForDocumentChange(doc);
        }

        public void WaitForRegression()
        {
            WaitForGraphs();
            WaitForConditionUI(() => SkylineWindow.RTGraphController != null);
            WaitForPaneCondition<RTLinearRegressionGraphPane>(SkylineWindow.RTGraphController.GraphSummary, pane => !pane.IsCalculating);
        }

        /// <summary>
        /// Wait for the built library to be loaded, and contain the expected
        /// number of spectra.
        /// </summary>
        /// <param name="expectedSpectra">Number of spectra expected in the library</param>
        /// <param name="libIndex">Index of library to wait for</param>
        public static void WaitForLibrary(int expectedSpectra, int libIndex = 0)
        {
            TryWaitForCondition(() =>
            {
                var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                return librarySettings.IsLoaded &&
                       librarySettings.Libraries.Count > libIndex &&
                       librarySettings.Libraries[libIndex].Keys.Count() == expectedSpectra;
            });
            var librarySettingsFinal = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            var libraries = librarySettingsFinal.Libraries;
            Assert.IsTrue(librarySettingsFinal.IsLoaded, string.Format("Libraries not loaded: {0}", librarySettingsFinal.IsNotLoadedExplained));
            Assert.IsTrue(libraries.Count > libIndex, string.Format("Library count {0} does not support the index {1}.", libraries.Count, libIndex));
            Assert.AreEqual(expectedSpectra, libraries[libIndex].Keys.Count());
        }

        public static void AddLibrary(LibrarySpec libSpec, Library lib)
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var libspecList = new List<LibrarySpec>(SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs);
            libspecList.Add(libSpec);
            // ReSharper disable once UseObjectOrCollectionInitializer
            var liblist = new List<Library>(SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries);
            liblist.Add(lib);

            RunUI(() => SkylineWindow.ModifyDocument("Add lib", doc =>
                doc.ChangeSettings(SkylineWindow.Document.Settings.ChangePeptideLibraries(libs => libs.ChangeLibrarySpecs(libspecList).ChangeLibraries(liblist)))));

            SkylineWindow.Document.Settings.UpdateLists(SkylineWindow.DocumentFilePath);
        }

        public TransitionSettingsUI ShowTransitionSettings(TransitionSettingsUI.TABS tab)
        {
            var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                fullScanDlg.SelectedTab = tab;
            });
            return fullScanDlg;
        }

        public DataGridViewColumn FindDocumentGridColumn(DocumentGridForm documentGrid, string colName)
        {
            WaitForConditionUI(() => documentGrid.IsComplete && documentGrid.FindColumn(PropertyPath.Parse(colName)) != null);
            return documentGrid.FindColumn(PropertyPath.Parse(colName));
        }

        public void EnableDocumentGridColumns(DocumentGridForm documentGrid, string viewName, int expectedRowsInitial, 
            string[] additionalColNames = null,
            string newViewName = null,
            int? expectedRowsFinal = null)
        {
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount >= expectedRowsInitial)); // Let it initialize
            if (additionalColNames != null)
            {
                RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView,
                    viewEditor =>
                    {
                        foreach (var colName in additionalColNames)
                        {
                            AssertEx.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse(colName)));
                            viewEditor.ChooseColumnsTab.AddSelectedColumn();
                        }
                        viewEditor.ViewName = newViewName ?? viewName;
                        viewEditor.OkDialog();
                    });
                WaitForCondition(() => (documentGrid.RowCount == (expectedRowsFinal??expectedRowsInitial))); // Let it initialize
            }
        }

        public void SetCellValue(DataGridView dataGridView, int rowIndex, int columnIndex, object value)
        {
            using (new WaitDocumentChange())
            {
                dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[columnIndex];
                dataGridView.BeginEdit(true);
                dataGridView.CurrentCell.Value = value;
                dataGridView.EndEdit();
            }
        }

        /// <summary>
        /// Split or collapse multiple panes in the chromatogram graph.
        /// </summary>
        /// <param name="split">True to split panes, false for single pane</param>
        public static void ShowSplitChromatogramGraph(bool split)
        {
            RunUI(() => SkylineWindow.ShowSplitChromatogramGraph(split));
            WaitForGraphs();
        }

        /// <summary>
        /// Close the spectrum graph.
        /// </summary>
        public void CloseSpectrumGraph()
        {
            RunUI(() => SkylineWindow.ShowGraphSpectrum(false));
            WaitForGraphs();
        }

        public void OpenAndChangeAreaCVProperties(GraphSummary graphSummary, Action<AreaCVToolbarProperties> action)
        {
            RunDlg<AreaCVToolbarProperties>(() => SkylineWindow.ShowAreaCVPropertyDlg(graphSummary), d =>
            {
                action(d);
                d.OK();
            });
            UpdateGraphAndWait(SkylineWindow.GraphPeakArea);
        }

        public void UpdateGraphAndWait(GraphSummary graph)
        {
            RunUI(() => { graph.UpdateUI(); });
            WaitForGraphs();
        }

        public int GetRowCount(FoldChangeGrid grid)
        {
            var count = -1;
            RunUI(() => count = grid.DataboundGridControl.RowCount);
            return count;
        }

        public void WaitForVolcanoPlotPointCount(FoldChangeGrid grid, int expected)
        {
            WaitForConditionUI(() => expected == grid.DataboundGridControl.RowCount && grid.DataboundGridControl.IsComplete,
                string.Format("Expecting {0} points found {1}", expected, GetRowCount(grid)));
        }

        public GroupComparisonDef FindGroupComparison(string name)
        {
            GroupComparisonDef def = null;
            RunUI(() =>
            {
                def = SkylineWindow.DocumentUI.Settings.DataSettings.GroupComparisonDefs.FirstOrDefault(g =>
                    g.Name == name);
            });

            return def;
        }

        public GroupComparisonDef CreateGroupComparison(string name, string controlGroupAnnotation, string controlGroupValue, string compareValue)
        {
            var dialog = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);

            RunUI(() =>
            {
                dialog.TextBoxName.Text = name;
                dialog.ComboControlAnnotation.SelectedItem = controlGroupAnnotation;
            });

            WaitForConditionUI(() => dialog.ComboControlValue.Items.Count > 0);

            RunUI(() =>
            {
                dialog.ComboControlValue.SelectedItem = controlGroupValue;
                dialog.ComboCaseValue.SelectedItem = compareValue;
                dialog.RadioScopePerProtein.Checked = false;
            });

            OkDialog(dialog, dialog.OkDialog);

            return FindGroupComparison(name);
        }

        public GroupComparisonDef CreateGroupComparison(string name, string controlGroupAnnotation,
            string controlGroupValue, string compareValue, string identityAnnotation)
        {
            var dialog = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);

            RunUI(() =>
            {
                dialog.TextBoxName.Text = name;
                dialog.ComboControlAnnotation.SelectedItem = controlGroupAnnotation;
            });

            WaitForConditionUI(() => dialog.ComboControlValue.Items.Count > 0);

            RunUI(() =>
            {
                dialog.ComboControlValue.SelectedItem = controlGroupValue;
                dialog.ComboCaseValue.SelectedItem = compareValue;
                dialog.ComboIdentityAnnotation.SelectedItem = identityAnnotation;
                dialog.RadioScopePerProtein.Checked = false;
            });

            OkDialog(dialog, dialog.OkDialog);

            return FindGroupComparison(name);
        }

        public void ChangeGroupComparison(Control owner, string name, Action<EditGroupComparisonDlg> action)
        {
            GroupComparisonDef def = null;
            RunDlg<EditGroupComparisonDlg>(() => def = Settings.Default.GroupComparisonDefList.EditItem(owner,
                FindGroupComparison(name),
                Settings.Default.GroupComparisonDefList, SkylineWindow), d =>
            {
                action(d);
                d.OkDialog();
            });

            RunUI(() =>
            {
                int index = Settings.Default.GroupComparisonDefList.ToList().FindIndex(g => g.Name == name);
                if (index >= 0)
                {
                    Settings.Default.GroupComparisonDefList[index] = def;
                    SkylineWindow.ModifyDocument(Resources.SkylineWindow_AddGroupComparison_Add_Fold_Change,
                        doc => doc.ChangeSettings(
                            doc.Settings.ChangeDataSettings(
                                doc.Settings.DataSettings.AddGroupComparisonDef(
                                    def))));
                }
            });
        }

        /// <summary>
        /// Create a new document and wait for it to load
        /// </summary>
        /// <param name="forced"></param>
        public void LoadNewDocument(bool forced)
        {
            RunUI(() => SkylineWindow.NewDocument(forced));
            WaitForDocumentLoaded();
        }

        public class Tool : IDisposable
        {
            private readonly MovedDirectory _movedDirectory;
            private readonly string _toolPath;

            public Tool(
                string zipInstallerPath,
                string toolName,
                string toolPath,
                string toolArguments,
                string toolInitialDirectory,
                bool toolOutputToImmediateWindow,
                string toolReport)
            {
                Settings.Default.ToolList.Clear();

                _movedDirectory = new MovedDirectory(ToolDescriptionHelpers.GetToolsDirectory(), Skyline.Program.StressTest);
                _toolPath = toolPath;
                RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, configureToolsDlg =>
                {
                    configureToolsDlg.RemoveAllTools();
                    configureToolsDlg.InstallZipTool(zipInstallerPath);
                    Assert.AreEqual(toolName, configureToolsDlg.textTitle.Text);
                    Assert.AreEqual(toolPath, configureToolsDlg.textCommand.Text);
                    Assert.AreEqual(toolArguments, configureToolsDlg.textArguments.Text);
                    Assert.AreEqual(toolInitialDirectory, configureToolsDlg.textInitialDirectory.Text);
                    Assert.AreEqual(toolOutputToImmediateWindow ? CheckState.Checked : CheckState.Unchecked, configureToolsDlg.cbOutputImmediateWindow.CheckState);
                    Assert.AreEqual(toolReport, configureToolsDlg.comboReport.SelectedItem);
                    string toolDir = configureToolsDlg.ToolDir;
                    Assert.IsTrue(Directory.Exists(toolDir));
                    configureToolsDlg.OkDialog();
                });
            }

            public void Dispose()
            {
                var processName = Path.GetFileNameWithoutExtension(_toolPath);
                using (new ProcessKiller(processName)) // Make sure tool process is closed and file handles released
                {
                    // Get rid of our temp files
                    _movedDirectory.Dispose();
                }
            }

            public void Run()
            {
                RunUI(() =>
                {
                    SkylineWindow.PopulateToolsMenu();
                    SkylineWindow.RunTool(0);
                });
            }
        }

        public static void ClickChromatogram(double x, double y, PaneKey? paneKey = null)
        {
            ClickChromatogram(null, x, y, paneKey);
        }

        public static void ClickChromatogram(string graphName, double x, double y, PaneKey? paneKey = null)
        {
            WaitForGraphs();
            var graphChromatogram = GetGraphChrom(graphName);
            // Wait as long as 2 seconds for mouse move to produce a highlight point
            bool overHighlight = false;
            const int sleepCycles = 20;
            const int sleepInterval = 100;
            for (int i = 0; i < sleepCycles; i++)
            {
                RunUI(() => graphChromatogram.TestMouseMove(x, y, paneKey));
                RunUI(() => overHighlight = graphChromatogram.IsOverHighlightPoint(x, y, paneKey));
                if (overHighlight)
                    break;
                Thread.Sleep(sleepInterval);
            }
            RunUI(() => AssertEx.IsTrue(graphChromatogram.IsOverHighlightPoint(x, y, paneKey),
                string.Format("Full-scan dot not present after {0} tries in {1} seconds", sleepCycles, sleepInterval*sleepCycles/1000.0)));
            RunUI(() => graphChromatogram.TestMouseDown(x, y, paneKey));
            WaitForGraphs();
            CheckFullScanSelection(graphName, x, y, paneKey);
        }

        public static void CheckFullScanSelection(double x, double y, PaneKey? paneKey = null)
        {
            CheckFullScanSelection(null, x, y, paneKey);
        }

        public static void CheckFullScanSelection(string graphName, double x, double y, PaneKey? paneKey = null)
        {
            var graphChromatogram = GetGraphChrom(graphName);
            WaitForConditionUI(() => SkylineWindow.GraphFullScan != null && SkylineWindow.GraphFullScan.IsLoaded);
            Assert.AreEqual(string.Empty, graphChromatogram.TestFullScanSelection(x, y, paneKey));
        }

        private static GraphChromatogram GetGraphChrom(string graphName)
        {
            return graphName != null
                ? SkylineWindow.GetGraphChrom(graphName)
                : SkylineWindow.GraphChromatograms.First();
        }

        public static void ZoomXAxis(ZedGraphControl graphControl, double min, double max)
        {
            ZoomAxis(graphControl, pane => pane.XAxis.Scale, min, max);
        }

        public static void ZoomYAxis(ZedGraphControl graphControl, double min, double max)
        {
            ZoomAxis(graphControl, pane => pane.YAxis.Scale, min, max);
        }

        private static void ZoomAxis(ZedGraphControl graphControl, Func<GraphPane, Scale> getScale, double min, double max)
        {
            var pane = graphControl.GraphPane;
            var scale = getScale(pane);
            scale.Min = min;
            scale.Max = max;
            new ZoomState(pane, ZoomState.StateType.Zoom).ApplyState(pane);

            using (var graphics = graphControl.CreateGraphics())
            {
                foreach (MSGraphPane graphPane in graphControl.MasterPane.PaneList.OfType<MSGraphPane>())
                {
                    graphPane.SetScale(graphics);
                }
            }
            graphControl.Refresh();
        }

        public void AddFastaToBackgroundProteome(BuildBackgroundProteomeDlg proteomeDlg, string fastaFile, int repeats)
        {
            RunDlg<MessageDlg>(
                () => proteomeDlg.AddFastaFile(TestFilesDirs[0].GetTestPath(fastaFile)),
                messageDlg =>
                {
                    Assert.AreEqual(
                        string.Format(Resources.BuildBackgroundProteomeDlg_AddFastaFile_The_added_file_included__0__repeated_protein_sequences__Their_names_were_added_as_aliases_to_ensure_the_protein_list_contains_only_one_copy_of_each_sequence_,
                        repeats), messageDlg.Message);
                    messageDlg.OkDialog();
                });
            
        }

        public void AddReplicateAnnotation(DocumentSettingsDlg documentSettingsDlg,
                                            string annotationName,
                                            AnnotationDef.AnnotationType annotationType = AnnotationDef.AnnotationType.text,
                                            IList<string> annotationValues = null,
                                            int? pausePage = null)
        {
            AddAnnotation(documentSettingsDlg, annotationName, annotationType, annotationValues,
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate),
                    pausePage);
        }

        public void AddAnnotation(DocumentSettingsDlg documentSettingsDlg,
                                            string annotationName,
                                            AnnotationDef.AnnotationType annotationType,
                                            IList<string> annotationValues,
                                            AnnotationDef.AnnotationTargetSet annotationTargets,
                                            int? pausePage = null)
        {
            var annotationsListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>
                (documentSettingsDlg.EditAnnotationList);
            RunUI(annotationsListDlg.SelectLastItem);
            var annotationDefDlg = ShowDialog<DefineAnnotationDlg>(annotationsListDlg.AddItem);

            RunUI(() =>
            {
                annotationDefDlg.AnnotationName = annotationName;
                annotationDefDlg.AnnotationType = annotationType;
                if (annotationValues != null)
                    annotationDefDlg.Items = annotationValues;
                annotationDefDlg.AnnotationTargets = annotationTargets;
            });

            if (pausePage.HasValue)
            {
                RunUI(() => annotationDefDlg.Height = 442);  // Shorter for screenshots
                PauseForScreenShot<DefineAnnotationDlg>("Define Annotation form - " + annotationName, pausePage.Value);
            }

            OkDialog(annotationDefDlg, annotationDefDlg.OkDialog);
            OkDialog(annotationsListDlg, annotationsListDlg.OkDialog);
        }

        protected IEnumerable<string> GetCoefficientStrings(EditPeakScoringModelDlg editDlg)
        {
            for (int i = 0; i < editDlg.PeakCalculatorsGrid.Items.Count; i++)
            {
                double? weight = editDlg.PeakCalculatorsGrid.Items[i].Weight;
                if (weight.HasValue)
                    yield return string.Format(CultureInfo.InvariantCulture, "{0:F04}", weight.Value);
                else
                    yield return " null ";  // To help values line up
            }
        }

        public static int CheckDocumentResultsGridValuesRecordedCount;
        public void CheckDocumentResultsGridFieldByName(DocumentGridForm documentGrid, string name, int row, double? expected, string msg = null, bool recordValues = false)
        {
            var col = name.StartsWith("TransitionResult") ?
                FindDocumentGridColumn(documentGrid, "Results!*.Value." + name.Split('.')[1]) :
                FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            double? actual = null;
            RunUI(() =>
            {
                actual = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as double?;
            });
            if (!recordValues)
            {
                var failmsg = name + " on row " + row + " " + (msg ?? string.Empty);
                AssertEx.AreEqual(expected.HasValue, actual.HasValue, failmsg);
                AssertEx.AreEqual(expected ?? 0, actual ?? 0, 0.005, failmsg);
            }
            else
            {
                if (CheckDocumentResultsGridValuesRecordedCount > 0)
                {
                    Console.Write(@", ");
                }
                if (++CheckDocumentResultsGridValuesRecordedCount % 18 == 0)
                {
                    Console.WriteLine();
                }
                if (actual.HasValue)
                {
                    Console.Write(@"{0:0.##}", actual);
                }
                else
                {
                    Console.Write(@"null");
                }
            }
        }

        public void CheckDocumentResultsGridFieldByName(DocumentGridForm documentGrid, string name, int row, string expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as string;
                AssertEx.AreEqual(expected, val, name + (msg ?? string.Empty));
            });
        }

        protected const string MIXED_TRANSITION_LIST_REPORT_NAME = "Mixed Transition List";
        protected void EnsureMixedTransitionListReport()
        {
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
            var viewsToAdd = @"<views>
  <view name='Mixed Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*' uimode='mixed'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.ModifiedSequence' />
    <column name='Precursor.Peptide.MoleculeName' />
    <column name='Precursor.Peptide.MoleculeFormula' />
    <column name='Precursor.IonFormula' />
    <column name='Precursor.NeutralFormula' />
    <column name='Precursor.Adduct' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='ExplicitCollisionEnergy' />
    <column name='Precursor.Peptide.ExplicitRetentionTime' />
    <column name='Precursor.Peptide.ExplicitRetentionTimeWindow' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
    <column name='ProductIonFormula' />
    <column name='ProductNeutralFormula' />
    <column name='ProductAdduct' />
    <column name='FragmentIonType' />
    <column name='FragmentIonOrdinal' />
    <column name='CleavageAa' />
    <column name='LossNeutralMass' />
    <column name='Losses' />
    <column name='LibraryRank' />
    <column name='LibraryIntensity' />
    <column name='IsotopeDistIndex' />
    <column name='IsotopeDistRank' />
    <column name='IsotopeDistProportion' />
    <column name='FullScanFilterWidth' />
    <column name='IsDecoy' />
    <column name='ProductDecoyMzShift' />
  </view>
</views>";
            var viewSpecListToAdd = (ViewSpecList) new XmlSerializer(typeof(ViewSpecList)).Deserialize(new StringReader(viewsToAdd));
            Settings.Default.PersistedViews.SetViewSpecList(PersistedViews.MainGroup.Id, viewSpecList.AddOrReplaceViews(viewSpecListToAdd.ViewSpecLayouts));
        }

        public DocumentGridForm EnableDocumentGridIonMobilityResultsColumns(int? expectedRowCount = null)
        {
            /* Add these IMS related columns to the standard mixed transition list report
                <column name="Results!*.Value.PrecursorResult.CollisionalCrossSection" />
                <column name="Results!*.Value.PrecursorResult.IonMobilityMS1" />
                <column name="Results!*.Value.IonMobilityFragment" />
                <column name="Results!*.Value.PrecursorResult.IonMobilityUnits" />
                <column name="Results!*.Value.PrecursorResult.IonMobilityWindow" />
                <column name="Results!*.Value.Chromatogram.ChromatogramIonMobility" />
                <column name="Results!*.Value.Chromatogram.ChromatogramIonMobilityExtractionWidth" />
                <column name="Results!*.Value.Chromatogram.ChromatogramIonMobilityUnits" />
            */

            EnsureMixedTransitionListReport();
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            EnableDocumentGridColumns(documentGrid,
                MIXED_TRANSITION_LIST_REPORT_NAME,
                SkylineWindow.Document.MoleculeTransitionCount,
                new[]
                {
                    // Completely new columns
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityMS1",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.IonMobilityFragment",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityUnits",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityWindow",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Chromatogram.ChromatogramIonMobility",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Chromatogram.ChromatogramIonMobilityExtractionWidth",
                    "Proteins!*.Peptides!*.Precursors!*.Transitions!*.Results!*.Value.Chromatogram.ChromatogramIonMobilityUnits"
                },
                null,
                expectedRowCount ?? SkylineWindow.Document.MoleculeTransitionCount * (SkylineWindow.Document.MeasuredResults?.Chromatograms.Count ?? 1));
            return documentGrid;
        }
        
        protected static void RenameReplicate(ManageResultsDlg manageResultsDlg, int replicateIndex, string newName)
        {
            RunUI(() => manageResultsDlg.SelectedChromatograms = new[]
            {
                SkylineWindow.DocumentUI.Settings.MeasuredResults.Chromatograms[replicateIndex]
            });
            RunDlg<RenameResultDlg>(manageResultsDlg.RenameResult, renameDlg =>
            {
                renameDlg.ReplicateName = newName;
                renameDlg.OkDialog();
            });
        }
    }
}
