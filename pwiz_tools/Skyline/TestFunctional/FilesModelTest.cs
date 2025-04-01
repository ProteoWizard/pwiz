/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    internal class FilesModelTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestFilesModel()
        {
            // These test files are large (90MB) so reuse rather than duplicate
            TestFilesZip = @"TestFunctional\FilesTreeFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var documentPath = TestFilesDirs[0].GetTestPath("Rat_plasma.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForOpenForm<SkylineWindow>();
            WaitForDocumentLoaded();

            var settings = SkylineWindow.Document.Settings;
            var files = new RootFileNode(SkylineWindow.Document, documentPath);
            var originalChromatogram = settings.MeasuredResults.Chromatograms[0];

            // <root>/replicates/
            Assert.AreEqual(typeof(ReplicatesFolder), files.Files[0].GetType());
            Assert.AreEqual(settings.MeasuredResults.Chromatograms.Count, files.Files[0].Files.Count);

            // <root>/replicates/replicate1/
            //                                (reminder, replicate is a "folder" containing sample files)
            Assert.AreEqual(typeof(Replicate), files.Files[0].Files[0].GetType());
            Assert.IsTrue(ReferenceEquals(settings.MeasuredResults.Chromatograms[0], files.Files[0].Files[0].Immutable));
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].FileCount, files.Files[0].Files[0].Files.Count);
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].Id, files.Files[0].Files[0].IdentityPath.GetIdentity(0));
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].Name, files.Files[0].Files[0].Name);
            Assert.AreEqual(Path.GetFileName(settings.MeasuredResults.Chromatograms[0].FilePath), files.Files[0].Files[0].FileName);
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].FilePath, files.Files[0].Files[0].FilePath);

            // <root>/replicates/replicate1/replicate-sample-file.raw
            Assert.AreEqual(typeof(ReplicateSampleFile), files.Files[0].Files[0].Files[0].GetType());
            Assert.IsTrue(ReferenceEquals(settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0], files.Files[0].Files[0].Files[0].Immutable));
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].Id, files.Files[0].Files[0].Files[0].IdentityPath.GetIdentity(1));
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].Name, files.Files[0].Files[0].Files[0].Name);
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath.GetFilePath(), files.Files[0].Files[0].Files[0].FilePath);

            // <root>/spectral-libraries/
            Assert.AreEqual(typeof(SpectralLibrariesFolder), files.Files[1].GetType());
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs.Count, files.Files[1].Files.Count);

            // <root>/spectral-libraries/library1.blib
            Assert.AreEqual(typeof(SpectralLibrary), files.Files[1].Files[0].GetType());
            Assert.IsTrue(ReferenceEquals(settings.PeptideSettings.Libraries.LibrarySpecs[0], files.Files[1].Files[0].Immutable));
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[0].Id, files.Files[1].Files[0].IdentityPath.GetIdentity(0));
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[0].Name, files.Files[1].Files[0].Name);
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[0].FilePath, files.Files[1].Files[0].FilePath);

            // <root>/spectral-libraries/library2.blib
            Assert.IsTrue(ReferenceEquals(settings.PeptideSettings.Libraries.LibrarySpecs[1], files.Files[1].Files[1].Immutable));
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[1].Id, files.Files[1].Files[1].IdentityPath.GetIdentity(0));
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[1].Name, files.Files[1].Files[1].Name);
            Assert.AreEqual(settings.PeptideSettings.Libraries.LibrarySpecs[1].FilePath, files.Files[1].Files[1].FilePath);

            //
            // Change property - make sure changed ChromSet's Id is the same but objects are not ReferenceEquals
            //
            const string newChromatogramName = "NEW CHROMATOGRAM SET NAME";
            RunUI(() =>
            {
                SkylineWindow.ModifyDocument("Rename replicate in unit test", srmDoc =>
                {
                    var chromatogram = SkylineWindow.Document.MeasuredResults.Chromatograms[0];
                    var chromatogramChangedName = (ChromatogramSet)chromatogram.ChangeName(newChromatogramName);

                    var chromatograms = SkylineWindow.Document.MeasuredResults.Chromatograms.ToArray();
                    chromatograms[0] = chromatogramChangedName;
                    var measuredResults = SkylineWindow.Document.MeasuredResults.ChangeChromatograms(chromatograms);
                    return SkylineWindow.Document.ChangeMeasuredResults(measuredResults);
                });
            });

            settings = SkylineWindow.Document.Settings;
            files = new RootFileNode(SkylineWindow.Document, documentPath);

            Assert.AreEqual(typeof(Replicate), files.Files[0].Files[0].GetType());

            Assert.AreEqual(originalChromatogram.Id, files.Files[0].Files[0].IdentityPath.GetIdentity(0));
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].Id, files.Files[0].Files[0].IdentityPath.GetIdentity(0));

            Assert.IsFalse(ReferenceEquals(originalChromatogram, files.Files[0].Files[0].Immutable));
            Assert.IsTrue(ReferenceEquals(settings.MeasuredResults.Chromatograms[0], files.Files[0].Files[0].Immutable));

            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].FileCount, files.Files[0].Files[0].Files.Count);
            Assert.AreEqual(newChromatogramName, settings.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].Name, files.Files[0].Files[0].Name);
            Assert.AreEqual(Path.GetFileName(settings.MeasuredResults.Chromatograms[0].FilePath), files.Files[0].Files[0].FileName);
            Assert.AreEqual(settings.MeasuredResults.Chromatograms[0].FilePath, files.Files[0].Files[0].FilePath);
        }
    }
}
