/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that libraries can still be loaded even if it is not possible to write to the library cache file.
    /// </summary>
    [TestClass]
    public class LibraryCacheTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLibraryCache()
        {
            TestFilesZip = @"TestFunctional\LibraryCacheTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testBytes = Encoding.UTF8.GetBytes("Hello, world");
            var blibPath = TestFilesDir.GetTestPath("rat_cmp_20.blib");
            const string blibName = "MyBiblioSpecLibrary";
            var blibCachePath = Path.ChangeExtension(blibPath, BiblioSpecLiteLibrary.EXT_CACHE);

            // Lock the cache file so that the library load code will not be able to use it
            var blibCacheStream = new FileStream(blibCachePath, FileMode.Create);
            blibCacheStream.Write(testBytes, 0, testBytes.Length);
            blibCacheStream.Flush();
            Assert.AreEqual(testBytes.Length, new FileInfo(blibCachePath).Length);

            var elibPath = TestFilesDir.GetTestPath("elibtest.elib");
            const string elibName = "MyEncyclopeDIALibrary";
            var elibCachePath = EncyclopeDiaLibrary.GetLibraryCachePath(elibPath);

            // Lock the cache file for the EncyclopeDIA library
            var elibCacheStream = new FileStream(elibCachePath, FileMode.Create);
            elibCacheStream.Write(testBytes, 0, testBytes.Length);
            elibCacheStream.Flush();
            Assert.AreEqual(testBytes.Length, new FileInfo(elibCachePath).Length);

            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            RunDlg<EditLibraryDlg>(libListDlg.AddItem, addLibDlg=>
            {
                addLibDlg.LibraryName = blibName;
                addLibDlg.LibraryPath = blibPath;
                addLibDlg.OkDialog();
            });
            RunDlg<EditLibraryDlg>(libListDlg.AddItem, addLibDlg =>
            {
                addLibDlg.LibraryName = elibName;
                addLibDlg.LibraryPath = elibPath;
                addLibDlg.OkDialog();
            });

            OkDialog(libListDlg, libListDlg.OkDialog);
            peptideSettingsUi.PickedLibraries = peptideSettingsUi.PickedLibraries.Append(blibName).Append(elibName).ToArray();
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            var blibLibrary = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.FirstOrDefault(lib =>
                lib.Name == blibName);
            Assert.IsNotNull(blibLibrary);
            Assert.IsTrue(blibLibrary.IsLoaded);
            Assert.AreNotEqual(0, blibLibrary.SpectrumCount);
            Assert.AreEqual(testBytes.Length, new FileInfo(blibCachePath).Length);

            var elibLibrary =
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.FirstOrDefault(lib =>
                    lib.Name == elibName);
            Assert.IsNotNull(elibLibrary);
            Assert.AreNotEqual(0, elibLibrary.SpectrumCount);
            Assert.AreEqual(testBytes.Length, new FileInfo(elibCachePath).Length);

            blibCacheStream.Dispose();
            elibCacheStream.Dispose();
        }
    }
}
