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

            var docSettings = SkylineWindow.Document.Settings;
            var modelFiles = new SkylineFile(SkylineWindow.Document, documentPath);

            // <root>/
            //      replicates/
            var modelReplicatesFolder = modelFiles.Folder<ReplicatesFolder>();
            Assert.IsTrue(ReferenceEquals(modelFiles.Files[0], modelReplicatesFolder));
            Assert.AreEqual(typeof(ReplicatesFolder), modelReplicatesFolder.GetType());
            Assert.AreEqual(docSettings.MeasuredResults.Chromatograms.Count, modelReplicatesFolder.Files.Count);

            // <root>/
            //      replicates/
            //          replicate1/
            //          replicate2/
            var docReplicate = docSettings.MeasuredResults.Chromatograms[0];
            var modelReplicate = modelReplicatesFolder.Files[0];
            Assert.AreEqual(typeof(Replicate), modelReplicate.GetType());
            Assert.IsTrue(ReferenceEquals(docReplicate, modelReplicate.Immutable));
            Assert.AreEqual(docReplicate.FileCount, modelReplicate.Files.Count);
            Assert.AreEqual(docReplicate.Id, modelReplicate.IdentityPath.GetIdentity(0));
            Assert.AreEqual(docReplicate.Name, modelReplicate.Name);
            Assert.AreEqual(Path.GetFileName(docReplicate.FilePath), modelReplicate.FileName);
            Assert.AreEqual(docSettings.MeasuredResults.Chromatograms[0].FilePath, modelReplicate.FilePath);

            // <root>/
            //      replicates/
            //          replicate1/
            //              replicate-sample-file.raw
            var docSampleFile = docReplicate.MSDataFileInfos[0];
            var modelSampleFile = modelReplicate.Files[0];
            Assert.AreEqual(typeof(ReplicateSampleFile), modelSampleFile.GetType());
            Assert.IsTrue(ReferenceEquals(docSampleFile, modelSampleFile.Immutable));
            Assert.AreEqual(docSampleFile.Id, modelSampleFile.IdentityPath.GetIdentity(1));
            Assert.AreEqual(docSampleFile.Name, modelSampleFile.Name);
            Assert.AreEqual(docSampleFile.FilePath.GetFilePath(), modelSampleFile.FilePath);

            // <root>/
            //      spectral-libraries/
            var modelLibrariesFolder = modelFiles.Folder<SpectralLibrariesFolder>();
            Assert.AreEqual(typeof(SpectralLibrariesFolder), modelLibrariesFolder.GetType());
            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs.Count, modelLibrariesFolder.Files.Count);

            // <root>/
            //      spectral-libraries/
            //          library1.blib
            var docLibrary = docSettings.PeptideSettings.Libraries.LibrarySpecs[0];
            var modelLibrary = modelLibrariesFolder.Files[0];
            Assert.AreEqual(typeof(SpectralLibrary), modelLibrary.GetType());
            Assert.IsTrue(ReferenceEquals(docSettings.PeptideSettings.Libraries.LibrarySpecs[0], modelLibrary.Immutable));
            Assert.AreEqual(docLibrary.Id, modelLibrary.IdentityPath.GetIdentity(0));
            Assert.AreEqual(docLibrary.Name, modelLibrary.Name);
            Assert.AreEqual(docLibrary.FilePath, modelLibrary.FilePath);

            // <root>/
            //      spectral-libraries/
            //          library2.blib
            docLibrary = docSettings.PeptideSettings.Libraries.LibrarySpecs[1];
            modelLibrary = modelLibrariesFolder.Files[1];
            Assert.IsTrue(ReferenceEquals(docLibrary, modelLibrary.Immutable));
            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs[1].Id, modelLibrary.IdentityPath.GetIdentity(0));
            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs[1].Name, modelLibrary.Name);
            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs[1].FilePath, modelLibrary.FilePath);

            //
            // Change property - make sure changed ChromSet's ID is the same but objects are not ReferenceEquals
            //
            const string newChromatogramName = "NEW REPLICATE NAME";
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

            docSettings = SkylineWindow.Document.Settings;
            modelFiles = new SkylineFile(SkylineWindow.Document, documentPath);

            var newDocReplicate = docSettings.MeasuredResults.Chromatograms[0];
            var newModelReplicate = modelFiles.Files[0].Files[0];

            Assert.AreEqual(typeof(Replicate), newModelReplicate.GetType());
            Assert.AreEqual(docReplicate.Id, newModelReplicate.IdentityPath.GetIdentity(0));
            Assert.AreEqual(newDocReplicate.Id, newModelReplicate.IdentityPath.GetIdentity(0));

            Assert.IsFalse(ReferenceEquals(docReplicate, newModelReplicate.Immutable));
            Assert.IsTrue(ReferenceEquals(newDocReplicate, newModelReplicate.Immutable));

            Assert.AreEqual(newDocReplicate.FileCount, newModelReplicate.Files.Count);
            Assert.AreEqual(newChromatogramName, newDocReplicate.Name);
            Assert.AreEqual(newDocReplicate.Name, newModelReplicate.Name);
            Assert.AreEqual(Path.GetFileName(newDocReplicate.FilePath), newModelReplicate.FileName);
            Assert.AreEqual(newDocReplicate.FilePath, newModelReplicate.FilePath);
        }
    }
}
