/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that "File > Save As" keeps an explicitly chosen "align to library" target working
    /// when the library it names is the document library. Saving to a new name renames the document
    /// library, and the alignment target names its library, so unless the target is renamed in step
    /// it no longer matches any library in the document and retention time alignment silently stops
    /// working. Documents which use the default alignment target do not show this, because the
    /// default resolves to whichever library supports alignment rather than to a library by name.
    /// </summary>
    [TestClass]
    public class SaveAsAlignmentTargetTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSaveAsAlignmentTarget()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeManagerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky")));
            WaitForDocumentLoaded();

            string docLibName = GetDocumentLibraryName(SkylineWindow.Document);
            Assert.AreEqual("ThreeReplicates", docLibName);

            // Align to the document library by name, rather than relying on the default target.
            RunUI(() => SkylineWindow.ModifyDocument("Align to document library", doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeImputation(
                        doc.Settings.PeptideSettings.Imputation.ChangeAlignmentTarget(
                            AlignmentTargetSpec.Library.ChangeName(docLibName)))))));
            WaitForDocumentLoaded();
            AssertAlignsToLibrary(SkylineWindow.Document, docLibName);

            const string newName = "Renamed";
            string renamedPath = TestFilesDir.GetTestPath(newName + SrmDocument.EXT);
            RunUI(() => Assert.IsTrue(SkylineWindow.SaveDocument(renamedPath)));
            WaitForDocumentLoaded();

            // The alignment target must have followed the document library to its new name.
            Assert.AreEqual(newName, GetDocumentLibraryName(SkylineWindow.Document));
            AssertAlignsToLibrary(SkylineWindow.Document, newName);

            // The renamed target must be persisted, and still resolve when the file is reopened.
            Assert.AreEqual(newName,
                ResultsUtil.DeserializeDocument(renamedPath).Settings.GetAlignmentTargetSpec().Name);
            RunUI(() => SkylineWindow.OpenFile(renamedPath));
            WaitForDocumentLoaded();
            AssertAlignsToLibrary(SkylineWindow.Document, newName);
        }

        /// <summary>
        /// Asserts that the document aligns to the library named <paramref name="expectedLibraryName"/>,
        /// and that the target actually resolves to that library rather than silently failing to match.
        /// </summary>
        private static void AssertAlignsToLibrary(SrmDocument document, string expectedLibraryName)
        {
            var spec = document.Settings.GetAlignmentTargetSpec();
            Assert.AreEqual(AlignmentTargetSpec.Library.Type, spec.Type);
            Assert.AreEqual(expectedLibraryName, spec.Name);
            Assert.IsTrue(document.Settings.TryGetAlignmentTarget(out var alignmentTarget),
                "Alignment target naming library {0} did not resolve to a library in the document",
                expectedLibraryName);
            Assert.IsInstanceOfType(alignmentTarget, typeof(AlignmentTarget.LibraryTarget));
        }

        private static string GetDocumentLibraryName(SrmDocument document)
        {
            return document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .First(spec => spec != null && spec.IsDocumentLibrary).Name;
        }
    }
}
