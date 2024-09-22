/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests creating a library, and ensures that the library can still be 
    /// read after doing "File > Share > Minimal".
    /// </summary>
    [TestClass]
    public class LibraryBuildShareTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestBuildLibraryShare()
        {
            TestFilesZip = @"TestFunctional\LibraryBuildShareTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            const string libraryName = "LibraryName";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("LibraryPath.blib");
                buildLibraryDlg.LibraryKeepRedundant = true;
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.AddInputFiles(new[] { TestFilesDir.GetTestPath("modless.pride.xml") });
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            Assert.IsTrue(WaitForCondition(() =>
                peptideSettingsUi.AvailableLibraries.Contains(libraryName)));
            RunUI(()=>peptideSettingsUi.PickedLibraries = new[] {libraryName});
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count);

            var libraryOriginal = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.First();
            var originalDataFiles = libraryOriginal.LibraryDetails.DataFiles;
            var originalLibPath = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.First().FilePath;

            string minimizedZipFile = TestFilesDir.GetTestPath("MinimizedDocument.sky.zip");
            RunUI(() =>
            {
                SkylineWindow.ImportFastaFile(TestFilesDir.GetTestPath("OneProtein.fasta"));
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("BeforeMinimize.sky"));
                SkylineWindow.ShareDocument(minimizedZipFile, ShareType.MINIMAL);
                SkylineWindow.OpenSharedFile(minimizedZipFile);
            });
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count);
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.First();
            var libraryKeys = library.Keys.ToArray();
            Assert.AreNotEqual(0, libraryKeys.Length);
            foreach (var key in libraryKeys)
            {
                var spectra = library.GetSpectra(key, null, LibraryRedundancy.all).ToArray();
                Assert.AreNotEqual(0, spectra.Length);
            }
            var minimizedLibPath = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.First().FilePath;

            // var originalDataFilesEnumerable = originalDataFiles.ToList();
            // var minimizedDataFilesEnumerable = library.LibraryDetails.DataFiles.ToList();
            // Assert.AreEqual(originalDataFilesEnumerable.Count, minimizedDataFilesEnumerable.Count);
            // foreach (SpectrumSourceFileDetails sourceFile in library.LibraryDetails.DataFiles)
            // {
            //     var originalSourceFile =
            //         originalDataFilesEnumerable.FirstOrDefault(s => s.FilePath.Equals(sourceFile.FilePath));
            //
            //     Assert.IsNotNull(originalSourceFile);
            //     Assert.AreEqual(originalSourceFile, sourceFile);
            // }


            var originalLibConnectionStr = new SQLiteConnectionStringBuilder { DataSource = originalLibPath }.ToString();
            var minimizedLibConnectionStr = new SQLiteConnectionStringBuilder { DataSource = minimizedLibPath }.ToString();

            CompareTable(originalLibConnectionStr, minimizedLibConnectionStr, "SpectrumSourceFiles");
            CompareTable(originalLibConnectionStr, minimizedLibConnectionStr, "ScoreTypes");
        }

        private static void CompareTable(string originalLibConnectionStr, string minimizedLibConnectionStr, string tableName)
        {
            List<string> originalLibColNames;
            List<string> originalLibRows;
            List<string> minimizedLibColNames;
            List<string> minimizedLibRows;
            var excludedCols = new[] { @"rowid", @"id" };
            using (var connection = new SQLiteConnection(originalLibConnectionStr))
            {
                connection.Open();
                Assert.IsTrue(SqliteOperations.TableExists(connection, tableName));
                originalLibColNames = SqliteOperations.GetColumnNamesFromTable(connection, tableName);
                originalLibRows = SqliteOperations.DumpTable(connection, tableName, null, null, excludedCols).ToList();
            }

            using (var connection = new SQLiteConnection(minimizedLibConnectionStr))
            {
                connection.Open();
                Assert.IsTrue(SqliteOperations.TableExists(connection, tableName));
                minimizedLibColNames = SqliteOperations.GetColumnNamesFromTable(connection, tableName);
                minimizedLibRows = SqliteOperations.DumpTable(connection, tableName, null, null, excludedCols).ToList();
            }

            CollectionAssert.AreEqual(originalLibColNames, minimizedLibColNames, 
                $"Column names are no the same in the original and minimized library for table {tableName}." +
                $"\nOriginal library columns: {TextUtil.LineSeparate(originalLibColNames)}" +
                $"\nMinimized library columns: {TextUtil.LineSeparate(minimizedLibColNames)}");
            if (originalLibRows.Count == minimizedLibRows.Count)
            {
                CollectionAssert.AreEqual(originalLibRows.ToList(), minimizedLibRows.ToList(),
                    $"Rows are no the same in the original and minimized library for table {tableName}." +
                    $"\nOriginal library rows: {TextUtil.LineSeparate(originalLibRows)}" +
                    $"\nMinimized library rows: {TextUtil.LineSeparate(minimizedLibRows)}");
            }
            else
            {
                foreach (var minimizedLibRow in minimizedLibRows)
                {
                    Assert.IsTrue(originalLibRows.Contains(minimizedLibRow), 
                        $"Original library does not contain a row in table {tableName} with the following values: {minimizedLibRow}" +
                        $"\n Original library rows: {TextUtil.LineSeparate(originalLibRows)}");
                }
            }
        }
    }
}
