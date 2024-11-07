/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportResultsFilesRestoreTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestImportResultsFilesRestore()
        {
            Run(@"TestFunctional\ImportResultsFilesRestoreTest.zip");
        }

        private const string DATA_FILE_NAME = "small.mzML";

        protected override void DoTest()
        {
            var doc = OpenDocument("test.sky");
            string docFile = SkylineWindow.DocumentFilePath;
            string importFile = TestFilesDir.GetTestPath(@"level1\level2\" + DATA_FILE_NAME);
            string importNewFile = TestFilesDir.GetTestPath(@"level1\level2\" +
                                                            Path.GetFileNameWithoutExtension(DATA_FILE_NAME) + "-new" +
                                                            Path.GetExtension(DATA_FILE_NAME));
            File.Copy(importFile, importNewFile);
            File.SetLastWriteTime(importNewFile, DateTime.Now);

            RunUI(() => Settings.Default.AutoShowAllChromatogramsGraph = false);    // Unimportant for this test

            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var importResultsFilesDlg = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var dataDir = new MsDataFilePath(Path.GetDirectoryName(importFile));
            RunUI(() => importResultsFilesDlg.CurrentDirectory = dataDir);

            Size saveSize = importResultsFilesDlg.Size;
            saveSize.Width += 100;
            saveSize.Height += 50;

            // Test initial state and make some changes
            RunUI(() =>
            {
                Assert.AreEqual(View.List, importResultsFilesDlg.ListView);
                Assert.AreEqual(0, importResultsFilesDlg.ListSortColumnIndex);
                Assert.AreEqual(SortOrder.None, importResultsFilesDlg.ListSortOrder);
                Assert.AreEqual(DATA_FILE_NAME, importResultsFilesDlg.ListItemNames.First());

                importResultsFilesDlg.Size = saveSize;
                importResultsFilesDlg.ListView = View.Details;
                importResultsFilesDlg.SetListViewSort(3, SortOrder.Descending);

                var firstFile = importResultsFilesDlg.ListItemNames.First();
                Assert.AreEqual(Path.GetFileName(importNewFile), firstFile);

                importResultsFilesDlg.SelectFile(firstFile);
            });
            OkDialog(importResultsFilesDlg, importResultsFilesDlg.Open);

            doc = WaitForDocumentChangeLoaded(doc);
            WaitForConditionUI(() => SkylineWindow.ImportingResultsWindow.Finished);

            // Make sure the settings are persisted and can be restored after saving
            Settings.Default.Save();
            Settings.Default.OpenDataSourceState = null;
            Settings.Default.Reload();

            // Test that changes are restored when the form is next shown on the same path
            importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            importResultsFilesDlg = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);

            RunUI(() =>
            {
                Assert.AreEqual(dataDir, importResultsFilesDlg.CurrentDirectory);
                Assert.AreEqual(saveSize, importResultsFilesDlg.Size);
                Assert.AreEqual(View.Details, importResultsFilesDlg.ListView);
                Assert.AreEqual(3, importResultsFilesDlg.ListSortColumnIndex);
                Assert.AreEqual(SortOrder.Descending, importResultsFilesDlg.ListSortOrder);
                Assert.AreEqual(Path.GetFileName(importNewFile), importResultsFilesDlg.ListItemNames.First());

                importResultsFilesDlg.ListView = View.Tile;

                importResultsFilesDlg.SelectFile(importResultsFilesDlg.ListItemNames.Last());
            });

            OkDialog(importResultsFilesDlg, importResultsFilesDlg.Open);

            doc = WaitForDocumentChangeLoaded(doc);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            WaitForConditionUI(() => SkylineWindow.ImportingResultsWindow.Finished);

            // Test that the starting path is not restored when the document path changes
            // but everything else is.
            RunUI(() =>
            {
                string newPath = SkylineWindow.DocumentFilePath;
                newPath = newPath.Substring(0, newPath.Length - SrmDocument.EXT.Length) + "-new" + SrmDocument.EXT;
                SkylineWindow.SaveDocument(newPath);
            });

            importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            importResultsFilesDlg = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);

            RunUI(() =>
            {
                Assert.AreEqual(new MsDataFilePath(Path.GetDirectoryName(docFile)), importResultsFilesDlg.CurrentDirectory);
                Assert.AreEqual(saveSize, importResultsFilesDlg.Size);
                Assert.AreEqual(View.Tile, importResultsFilesDlg.ListView);
            });
            OkDialog(importResultsFilesDlg, importResultsFilesDlg.CancelButton.PerformClick);
            OkDialog(importResultsDlg, importResultsDlg.CancelDialog);
        }

        private SrmDocument ImportFailure(SrmDocument doc, params string[] dataFiles)
        {
            // Keep import progress window open after failure.
            ImportResultsAsync(dataFiles);
            doc = WaitForDocumentChangeLoaded(doc);
            WaitForConditionUI(() => SkylineWindow.ImportingResultsWindow.Finished);
            return doc;
        }
    }
}