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


            var originalLibConnectionStr = new SQLiteConnectionStringBuilder { DataSource = originalLibPath }.ToString();
            var minimizedLibConnectionStr = new SQLiteConnectionStringBuilder { DataSource = minimizedLibPath }.ToString();

            // Test for Issue 1022: idFileName column values are not set in minimized libraries
            CompareTable("SpectrumSourceFiles", originalLibConnectionStr, minimizedLibConnectionStr);
            CompareTable("ScoreTypes", originalLibConnectionStr, minimizedLibConnectionStr);
        }

        private static void CompareTable(string tableName, string originalLibConnectionStr, string minimizedLibConnectionStr)
        {
            var excludedCols = new[] { @"rowid", @"id" };

            List<string> originalLibColNames;
            List<string> originalLibRows;
            
            using (var connection = new SQLiteConnection(originalLibConnectionStr))
            {
                connection.Open();
                Assert.IsTrue(SqliteOperations.TableExists(connection, tableName));
                originalLibColNames = SqliteOperations.GetColumnNamesFromTable(connection, tableName);
                originalLibRows = SqliteOperations.DumpTable(connection, tableName, ", ", null, excludedCols).ToList();
            }
            originalLibColNames.Sort();
            originalLibRows.Sort();

            List<string> minimizedLibColNames;
            List<string> minimizedLibRows;
            using (var connection = new SQLiteConnection(minimizedLibConnectionStr))
            {
                connection.Open();
                Assert.IsTrue(SqliteOperations.TableExists(connection, tableName));
                minimizedLibColNames = SqliteOperations.GetColumnNamesFromTable(connection, tableName);
                minimizedLibRows = SqliteOperations.DumpTable(connection, tableName, ", ", null, excludedCols).ToList();
            }
            minimizedLibColNames.Sort();
            minimizedLibRows.Sort();

            // Compare column names
            CollectionAssert.AreEqual(originalLibColNames, minimizedLibColNames,
                TextUtil.LineSeparate(
                    $"Column names for table {tableName} are not the same in the original and minimized libraries.",
                    "Original library columns:", TextUtil.LineSeparate(originalLibColNames),
                    "Minimized library columns:", TextUtil.LineSeparate(minimizedLibColNames)));

            // Compare row values. Minimized library can have fewer rows than the original library (e.g. ScoreTypes table)
            foreach (var minimizedLibRow in minimizedLibRows)
            {
                Assert.IsTrue(originalLibRows.Contains(minimizedLibRow), 
                    TextUtil.LineSeparate(
                        $"Original library does not contain a row in table {tableName} with the following values:", minimizedLibRow,
                        "Original library rows:" , TextUtil.LineSeparate(originalLibRows),
                        "Minimized library rows:", TextUtil.LineSeparate(minimizedLibRows)));
            }
        }
    }
}
