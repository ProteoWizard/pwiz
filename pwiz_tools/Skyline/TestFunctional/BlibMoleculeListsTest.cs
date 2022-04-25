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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class BlibMoleculeListsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestBlibMoleculeLists()
        {
            TestFilesZip = @"TestFunctional\BlibMoleculeListsTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test the use of Molecule List Names as "Proteins" in .blib export/import
        /// They should round trip, resulting in a document with the same Molecule List names
        /// as the one that generate the spectral library
        /// </summary>
        protected override void DoTest()
        {

            // Export and check spectral library
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MoleculeGroups.sky")));
            var docOrig = WaitForDocumentLoaded();
            var moleculeLists = docOrig.MoleculeGroups.Select(g => g.Name).ToHashSet(); // Note the list names
            var exported = "MolListsTest";
            var exportedBlib = exported+ BiblioSpecLiteSpec.EXT;
            var exporteDFullPath = TestFilesDir.GetTestPath(exportedBlib);
            var libraryExporter = new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath);
            libraryExporter.ExportSpectralLibrary(exporteDFullPath, null);
            Assert.IsTrue(File.Exists(exporteDFullPath));

            var docAfter = NewDocumentFromSpectralLibrary(exported, exporteDFullPath);

            // Expect two molecule lists instead of the old single "Library Molecules" list
            var newMoleculeLists = docAfter.MoleculeGroups.Select(g => g.Name).ToArray(); 
            AssertEx.AreEqual(2, newMoleculeLists.Length);
            foreach (var name in newMoleculeLists)
            {
                AssertEx.IsTrue(moleculeLists.Contains(name));
            }
        }


    }
}
