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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;

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

        public void ConvertDocumentToSmallMolecules(RefinementSettings.ConvertToSmallMoleculesMode mode = RefinementSettings.ConvertToSmallMoleculesMode.formulas, 
            bool invertCharges = false, bool ignoreDecoys = false)
        {
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ModifyDocument("Convert to small molecules", document =>
            {
                var refine = new RefinementSettings();
                return refine.ConvertToSmallMolecules(document, mode, invertCharges, ignoreDecoys);
            }));
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
            ImportResultsAsync(dataFiles, lockMassParameters);
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

        public void ImportResultsAsync(string[] dataFiles, LockMassParameters lockMassParameters, string expectedErrorMessage = null)
        {
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
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
                OkDialog(importResultsDlg, importResultsDlg.OkDialog);
            }
            else
            {
                // Expect a Waters lockmass dialog to appear on OK
                WaitForConditionUI(() => importResultsDlg.NamedPathSets.Count() == dataFiles.Count());
                var lockmassDlg = ShowDialog<ImportResultsLockMassDlg>(importResultsDlg.OkDialog);
                RunUI(() =>
                {
                    lockmassDlg.LockmassPositive = lockMassParameters.LockmassPositive ?? 0;
                    lockmassDlg.LockmassNegative = lockMassParameters.LockmassNegative ?? 0;
                    lockmassDlg.LockmassTolerance = lockMassParameters.LockmassTolerance ?? 0;
                });
                OkDialog(lockmassDlg, lockmassDlg.OkDialog);
            }
        }

        /// <summary>
        /// Wait for the built library to be loaded, and contain the expected
        /// number of spectra.
        /// </summary>
        /// <param name="expectedSpectra">Number of spectra expected in the library</param>
        public static void WaitForLibrary(int expectedSpectra)
        {
            WaitForCondition(() =>
            {
                var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                return librarySettings.IsLoaded &&
                       librarySettings.Libraries.Count > 0 &&
                       librarySettings.Libraries[0].Keys.Count() == expectedSpectra;
            });
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

        public void EnableDocumentGridColumns(DocumentGridForm documentGrid, string viewName, int expectedRows, string[] colNames)
        {
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount == expectedRows)); // Let it initialize
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView,
                viewEditor =>
                {
                    foreach (var colName in colNames)
                    {
                        Assert.IsTrue(viewEditor.ChooseColumnsTab.TrySelect(PropertyPath.Parse(colName)));
                        viewEditor.ChooseColumnsTab.AddSelectedColumn();
                    }
                    viewEditor.ViewName = viewName;
                    viewEditor.OkDialog();
                });
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

        public class Tool : IDisposable
        {
            private readonly MovedDirectory _movedDirectory;

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
                _movedDirectory.Dispose();
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
            var graphChromatogram = SkylineWindow.GraphChromatograms.First();
            RunUI(() =>
            {
                graphChromatogram.TestMouseMove(x, y, paneKey);
                graphChromatogram.TestMouseDown(x, y, paneKey);
            });
            WaitForGraphs();
            CheckFullScanSelection(x, y, paneKey);
        }

        public static void CheckFullScanSelection(double x, double y, PaneKey? paneKey = null)
        {
            var graphChromatogram = SkylineWindow.GraphChromatograms.First();
            WaitForConditionUI(() => SkylineWindow.GraphFullScan != null && SkylineWindow.GraphFullScan.IsLoaded);
            Assert.IsTrue(graphChromatogram.TestFullScanSelection(x, y, paneKey));
        }
    }
}
