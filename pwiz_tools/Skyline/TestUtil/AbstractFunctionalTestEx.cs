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
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using System.Xml.Serialization;
using DigitalRune.Windows.Docking;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.MSGraph;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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
        public SrmDocument OpenDocument(string documentPath)
        {
            // In a test it's possible to programatically open a document while forms like
            // PeptideSettingsUI or TransitionSettingsUI are open, but this isn't possible
            // in actual UI use and will doubtless lead to confusing test behavior.
            var unexpectedOpenForms = FindOpenForms<Form>().Where(f => f.Modal).Select(form => form.Name).ToList();
            AssertEx.AreEqual(0, unexpectedOpenForms.Count, $@"Can't open a document when other dialogs are still open: {CommonTextUtil.LineSeparate(unexpectedOpenForms)}");

            string documentFile = documentPath; // Default to assuming an absolute path
            // Check for relative path in test files dirs
            foreach (var testFileDir in TestFilesDirs)
            {
                documentFile = testFileDir.GetTestPath(documentPath);
                if (File.Exists(documentFile))
                {
                    break;
                }
            }

            if (documentPath.EndsWith(@".zip", true, CultureInfo.InvariantCulture))
            {
                RunUI(() => SkylineWindow.OpenSharedFile(documentFile));
            }
            else
            {
                RunUI(() => SkylineWindow.OpenFile(documentFile));
            }
            return WaitForDocumentLoaded();
        }

        public void OpenDocumentNoWait(string documentPath)
        {
            var documentFile = TestFilesDir.GetTestPath(documentPath);
            WaitForCondition(() => File.Exists(documentFile));
            SkylineWindow.BeginInvoke((Action) (() => SkylineWindow.OpenFile(documentFile)));
        }

        /// <summary>
        /// Restore the document to its original state using the undo buffer.
        /// Much faster than reopening from disk since it swaps in-memory immutable trees.
        /// </summary>
        public SrmDocument RestoreOriginalDocument(int version = 0)
        {
            using var _ = new WaitDocumentChange(null, true);
            RunUI(() => SkylineWindow.UndoAll(version));
            return SkylineWindow.Document;
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
            if (!SkylineWindow.ShouldPromptForDecoys(SkylineWindow.Document))
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
                dlg.CancelButton.PerformClick();
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

        public void EnableDocumentGridColumns(DocumentGridForm documentGrid, string viewName, int? expectedRowsInitial, 
            string[] additionalColNames = null,
            string newViewName = null,
            int? expectedRowsFinal = null)
        {
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount >= (expectedRowsInitial??0))); // Let it initialize
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
                WaitForCondition(() => (documentGrid.RowCount == (expectedRowsFinal??expectedRowsInitial??0))); // Let it initialize
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

        public GroupComparisonDef CreateGroupComparison(string name, string controlGroupAnnotation, string controlGroupValue, string compareValue, string identityAnnotation = null)
        {
            var dialog = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);

            RunUI(() =>
            {
                dialog.TextBoxName.Text = name;
                dialog.ControlAnnotation = controlGroupAnnotation;
            });

            WaitForConditionUI(() => dialog.ControlValueOptions.Any());

            RunUI(() =>
            {
                dialog.ControlValue = controlGroupValue;
                dialog.CaseValue = compareValue;
                if (identityAnnotation != null)
                {
                    dialog.IdentityAnnotation = identityAnnotation;
                }
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

        public static void TestHttpClientCancellation(Action actionToCancel)
        {
            // This should get canceled silently without showing a MessageDlg.
            // While it is difficult to test for not showing something without waiting,
            // if a MessageDlg were shown, that would cause a failure in subsequent tests.
            using (HttpClientTestHelper.SimulateCancellationClickWithException())
            {
                TestCancellationWithoutMessageDlg(actionToCancel);
            }
        }

        public static void TestCancellationWithoutMessageDlg(Action actionToCancel)
        {
            SkylineWindow.BeginInvoke(actionToCancel);
            // This wait triggered reliably with a failure that showed a message.
            // Even if it does not, the test will fail later, but may be more confusing
            // to debug, which is the reason for adding this assertion.
            var messageDlg = TryWaitForOpenForm<MessageDlg>(200);
            Assert.IsNull(messageDlg, string.Format("Unexpected MessageDlg: {0}", messageDlg?.Message));
        }


        public static void TestHttpClientWithNoNetwork(Action actionToFail, string prefix = null)
        {
            TestHttpClientWithNoNetwork(actionToFail, (expectedMessage, actualMessage) =>
            {
                if (prefix != null)
                    expectedMessage = TextUtil.LineSeparate(prefix, expectedMessage);

                Assert.AreEqual(expectedMessage, actualMessage);
            });
        }

        public static void TestHttpClientWithNoNetworkEx(Action actionToFail, params string[] extraParts)
        {
            TestHttpClientWithNoNetwork(actionToFail, (expectedMessage, actualMessage) =>
            {
                AssertEx.Contains(actualMessage, expectedMessage);
                AssertEx.Contains(actualMessage, extraParts);
            });
        }

        public static void TestHttpClientWithNoNetwork(Action actionToFail, Action<string, string> validateMessage)
        {
            using var helper = HttpClientTestHelper.SimulateNoNetworkInterface();
            var expectedMessage = helper.GetExpectedMessage();
            TestMessageDlgShown(actionToFail, actualMessage => validateMessage(expectedMessage, actualMessage));
        }

        public static void TestMessageDlgShown(Action actionToShow, string expectedMessage)
        {
            TestMessageDlgShown(actionToShow, actualMessage =>
                Assert.AreEqual(expectedMessage, actualMessage));
        }

        public static void TestMessageDlgShownContaining(Action actionToShow, params string[] parts)
        {
            TestMessageDlgShown(actionToShow, actualMessage =>
                AssertEx.Contains(actualMessage, parts));
        }

        public static void TestMessageDlgShown(Action actionToShow, Action<string> validateMessage)
        {
            // Cannot use RunDlg here because it requires actionShow to complete.
            var errDlg = ShowDialog<MessageDlg>(actionToShow);
            RunUI(() => validateMessage(errDlg.Message));
            OkDialog(errDlg, errDlg.OkDialog);
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

                _movedDirectory = new MovedDirectory(ToolDescriptionHelpers.GetToolsDirectory(), Program.StressTest);
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

        public static void ClickChromatogram(string graphName, double x, double y, PaneKey? paneKey = null, double? titleTime = null)
        {
            WaitForGraphs();
            var graphChromatogram = GetGraphChrom(graphName);
            MouseOverChromatogramInternal(graphChromatogram, x, y, paneKey);
            RunUI(() => graphChromatogram.TestMouseDown(x, y, paneKey));
            WaitForGraphs();
            CheckFullScanSelection(graphName, x, y, paneKey, titleTime);
        }

        public static void MouseOverChromatogram(double x, double y, PaneKey? paneKey = null)
        {
            MouseOverChromatogram(null, x, y, paneKey);
        }

        public static void MouseOverChromatogram(string graphName, double x, double y, PaneKey? paneKey = null)
        {
            WaitForGraphs();
            var graphChromatogram = GetGraphChrom(graphName);
            MouseOverChromatogramInternal(graphChromatogram, x, y, paneKey);
        }

        private static void MouseOverChromatogramInternal(GraphChromatogram graphChromatogram, double x, double y, PaneKey? paneKey)
        {
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
                string.Format("Full-scan dot not present after {0} tries in {1} seconds", sleepCycles,
                    sleepInterval * sleepCycles / 1000.0)));
        }

        public static void CheckFullScanSelection(double x, double y, PaneKey? paneKey = null, double? titleTime = null)
        {
            CheckFullScanSelection(null, x, y, paneKey, titleTime);
        }

        public static void CheckFullScanSelection(string graphName, double x, double y, PaneKey? paneKey = null, double? titleTime = null)
        {
            var graphChromatogram = GetGraphChrom(graphName);
            WaitForConditionUI(() => SkylineWindow.GraphFullScan != null && SkylineWindow.GraphFullScan.IsLoaded);
            if (titleTime.HasValue)
            {
                // Good idea to check the title for a tutorial screenshot
                var matchTime = Regex.Match(SkylineWindow.GraphFullScan.TitleText, @".([0-9.,]+) [\w]+.$");
                Assert.IsTrue(matchTime.Success);
                Assert.AreEqual(titleTime.Value, double.Parse(matchTime.Groups[1].Value));
            }
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

        protected static void ResizeFloatingFrame(DockableForm dockableForm, int? width, int? height)
        {
            Assert.AreEqual(DockState.Floating, dockableForm.DockState);
            var parentForm = dockableForm.ParentForm;
            Assert.IsNotNull(parentForm);
            ResizeFormOnScreen(parentForm, width, height);
        }

        protected static void ResizeFormOnScreen(Form parentForm, int? width, int? height)
        {
            if (Program.SkylineOffscreen)
                return;

            if (width.HasValue)
                parentForm.Width = width.Value;
            if (height.HasValue)
                parentForm.Height = height.Value;
            FormEx.ForceOnScreen(parentForm);
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
                                            bool pause = false)
        {
            AddAnnotation(documentSettingsDlg, annotationName, annotationType, annotationValues,
                    AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate),
                    pause);
        }

        public void AddAnnotation(DocumentSettingsDlg documentSettingsDlg,
                                            string annotationName,
                                            AnnotationDef.AnnotationType annotationType,
                                            IList<string> annotationValues,
                                            AnnotationDef.AnnotationTargetSet annotationTargets,
                                            bool pause = false)
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

            if (pause)
            {
                RunUI(() => annotationDefDlg.Height = 442);  // Shorter for screenshots
                PauseForScreenShot<DefineAnnotationDlg>("Define Annotation form - " + annotationName);
            }

            OkDialog(annotationDefDlg, annotationDefDlg.OkDialog);
            OkDialog(annotationsListDlg, annotationsListDlg.OkDialog);
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

        public void CheckDocumentResultsGridFieldByName(DocumentGridForm documentGrid, string name, int row, string expected, string msg = null, bool recordValues = false)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            string actual = null;
            RunUI(() =>
            {
                actual = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as string;
            });
            if (recordValues)
            {
                Console.Write($@",{actual}");
            }
            else
            {
                AssertEx.AreEqual(expected, actual, name + (msg ?? string.Empty));
            }
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

        public static void SetIonMobilityResolvingPowerUI(TransitionSettingsUI transitionSettingsUi, double rp)
        {
            RunUI(() =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.IonMobility;
                transitionSettingsUi.IonMobilityControl.WindowWidthType =
                    IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power;
                transitionSettingsUi.IonMobilityControl.IonMobilityFilterResolvingPower = rp;
            });
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

        public static SrmDocument NewDocumentFromSpectralLibrary(string libName, string libFullPath)
        {
            // Remove current document
            RunUI(() => SkylineWindow.NewDocument(true));
            // Now import the named library and populate document from that
            return AddToDocumentFromSpectralLibrary(libName, libFullPath);
        }

        // Import a spectral library and add its contents to current document
        public static SrmDocument AddToDocumentFromSpectralLibrary(string libName, string libFullPath)
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            RunUI(() => peptideSettingsUI.TabControlSel = PeptideSettingsUI.TABS.Library);

            Assert.IsNotNull(peptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);

            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                addLibUI.LibraryName = libName;
                addLibUI.LibraryPath = libFullPath;
                addLibUI.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);

            // Make sure the libraries actually show up in the peptide settings dialog before continuing.
            WaitForConditionUI(() => peptideSettingsUI.AvailableLibraries.Length > 0);
            // Library gets added to the document below by the ViewLibraryDlg form
            RunUI(() => Assert.IsFalse(peptideSettingsUI.IsSettingsChanged));

            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Add all the molecules in the library
            RunUI(() => SkylineWindow.ViewSpectralLibraries());
            var viewLibraryDlg = FindOpenForm<ViewLibraryDlg>();
            var docBefore = WaitForProteinMetadataBackgroundLoaderCompletedUI();

            ShowAndDismissDlg<MultiButtonMsgDlg>(viewLibraryDlg.AddAllPeptides, messageDlg =>
            {
                var addLibraryMessage =
                    string.Format(
                        Resources.ViewLibraryDlg_CheckLibraryInSettings_The_library__0__is_not_currently_added_to_your_document,
                        libName);
                StringAssert.StartsWith(messageDlg.Message, addLibraryMessage);
                messageDlg.DialogResult = DialogResult.Yes;
            });
            var filterPeptidesDlg = WaitForOpenForm<FilterMatchedPeptidesDlg>();
            ShowAndDismissDlg<MultiButtonMsgDlg>(filterPeptidesDlg.OkDialog, addLibraryPepsDlg => { addLibraryPepsDlg.Btn1Click(); });
            OkDialog(filterPeptidesDlg, filterPeptidesDlg.OkDialog);

            var docAfterAdd = WaitForDocumentChange(docBefore);

            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);

            return docAfterAdd;
        }


        /// <summary>
        /// Helper class for tests to show and dispose of a <see cref="DocumentationViewer"/>.
        /// </summary>
        public class DocumentationViewerHelper : IDisposable
        {
            private readonly string _originalDirectory;

            public DocumentationViewerHelper(TestContext testContext, Action showViewer)
            {
                _originalDirectory = DocumentationViewer.TestWebView2EnvironmentDirectory;
                DocumentationViewer.TestWebView2EnvironmentDirectory = testContext.GetTestResultsPath(@"WebView2");
                Directory.CreateDirectory(DocumentationViewer.TestWebView2EnvironmentDirectory);

                DocViewer = ShowDialog<DocumentationViewer>(showViewer);

                // Wait for the document to load completely in WebView2
                WaitForConditionUI(() => DocViewer.GetWebView2HtmlContent(100).Contains("<table"));
            }
            
            public DocumentationViewer DocViewer { get; }

            public void Dispose()
            {
                OkDialog(DocViewer, DocViewer.Close);
                
                // Give folder clean-up an extra 2 seconds to complete
                TryWaitForCondition(2000, CleanupTestDataFolder);
                DocumentationViewer.TestWebView2EnvironmentDirectory = _originalDirectory;
            }

            private bool CleanupTestDataFolder()
            {
                var testDataFolder = DocumentationViewer.TestWebView2EnvironmentDirectory;
                // Clean up test data folder if it was created
                if (Directory.Exists(testDataFolder))
                {
                    // Give WebView2 more time to release file handles
                    Thread.Sleep(200);

                    // Force garbage collection to help release any remaining handles
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Try to delete with retry logic for locked files
                    try
                    {
                        TryHelper.TryTwice(() => Directory.Delete(testDataFolder, true), 5, 200, @"Failed to cleanup WebView2 test folder");
                    }
                    catch
                    {
                        // Ignore and expect the test to fail with a useful message about why this folder cannot be removed
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
