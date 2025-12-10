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
    public class FilesModelTest : AbstractFunctionalTest
    { 
        [TestMethod]
        public void TestFilesModel()
        {
            // These test files are large (30MB) so reuse rather than duplicate
            TestFilesZipPaths = new[]
            {
                @"TestFunctional\FilesTreeFormTest.zip",
                @"https://skyline.ms/tutorials/GroupedStudies.zip"  // Rat_plasma.sky
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            FilesTreeFormTest.PrepareRatPlasmaFile(TestFilesDirs[0], TestFilesDirs[1]);
            var documentPath = TestFilesDirs[0].GetTestPath(FilesTreeFormTest.RAT_PLASMA_FILE_NAME);

            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForOpenForm<SkylineWindow>();
            WaitForDocumentLoaded();

            var modelFiles = SkylineFile.Create(SkylineWindow.Document, documentPath);

            var docSettings = SkylineWindow.Document.Settings;

            // <root>/
            //      Chromatogram Cache
            //      replicates/
            var modelReplicatesFolder = modelFiles.Folder<ReplicatesFolder>();
            Assert.AreSame(modelFiles.Files[3], modelReplicatesFolder);

            Assert.IsInstanceOfType(modelReplicatesFolder, typeof(ReplicatesFolder));
            Assert.AreEqual(docSettings.MeasuredResults.Chromatograms.Count, modelReplicatesFolder.Files.Count);

            // <root>/
            //      replicates/
            //          replicate1/
            //          replicate2/
            var docReplicate = docSettings.MeasuredResults.Chromatograms[0];
            var modelReplicate = modelReplicatesFolder.Files[0];

            Assert.IsInstanceOfType(modelReplicate, typeof(Replicate));
            Assert.AreEqual(docReplicate.Id, modelReplicate.IdentityPath.GetIdentity(0));
            Assert.AreEqual(docReplicate, Replicate.LoadChromSetFromDocument(SkylineWindow.Document, (Replicate)modelReplicate));
            Assert.AreEqual(docReplicate.FileCount, modelReplicate.Files.Count);
            Assert.AreEqual(docReplicate.Name, modelReplicate.Name);
            Assert.AreEqual(Path.GetFileName(docReplicate.FilePath), modelReplicate.FileName);
            Assert.AreEqual(docSettings.MeasuredResults.Chromatograms[0].FilePath, modelReplicate.FilePath);

            // <root>/
            //      replicates/
            //          replicate1/
            //              replicate-sample-file.raw
            var docSampleFile = docReplicate.MSDataFileInfos[0];
            var modelSampleFile = modelReplicate.Files[0];

            Assert.IsInstanceOfType(modelSampleFile, typeof(ReplicateSampleFile));
            Assert.AreEqual(docSampleFile.Id, modelSampleFile.IdentityPath.GetIdentity(1));
            Assert.AreEqual(docSampleFile.Name, modelSampleFile.Name);
            Assert.AreEqual(docSampleFile.FilePath.GetFilePath(), modelSampleFile.FilePath);

            // <root>/
            //      spectral-libraries/
            var modelLibrariesFolder = modelFiles.Folder<SpectralLibrariesFolder>();

            Assert.IsInstanceOfType(modelLibrariesFolder, typeof(SpectralLibrariesFolder));
            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs.Count, modelLibrariesFolder.Files.Count);

            // <root>/
            //      spectral-libraries/
            //          library1.blib
            var docLibrary = docSettings.PeptideSettings.Libraries.LibrarySpecs[0];
            var modelLibrary = modelLibrariesFolder.Files[0];

            Assert.IsInstanceOfType(modelLibrary, typeof(SpectralLibrary));
            Assert.AreEqual(docLibrary.Id, modelLibrary.IdentityPath.GetIdentity(0));
            Assert.AreSame(docLibrary, SpectralLibrary.LoadLibrarySpecFromDocument(SkylineWindow.Document, (SpectralLibrary)modelLibrary));
            Assert.AreEqual(docLibrary.Name, modelLibrary.Name);
            Assert.AreEqual(docLibrary.FilePath, modelLibrary.FilePath);

            // <root>/
            //      spectral-libraries/
            //          library2.blib
            docLibrary = docSettings.PeptideSettings.Libraries.LibrarySpecs[1];
            modelLibrary = modelLibrariesFolder.Files[1];

            Assert.AreEqual(docSettings.PeptideSettings.Libraries.LibrarySpecs[1].Id, modelLibrary.IdentityPath.GetIdentity(0));
            Assert.AreSame(docLibrary, SpectralLibrary.LoadLibrarySpecFromDocument(SkylineWindow.Document, (SpectralLibrary)modelLibrary));
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

            modelFiles = SkylineFile.Create(SkylineWindow.Document, documentPath);
            docSettings = SkylineWindow.Document.Settings;

            var newDocReplicate = docSettings.MeasuredResults.Chromatograms[0];
            var newModelReplicate = modelFiles.Files[3].Files[0];

            Assert.IsInstanceOfType(newModelReplicate, typeof(Replicate));
            Assert.AreEqual(docReplicate.Id, newModelReplicate.IdentityPath.GetIdentity(0));
            Assert.AreEqual(newDocReplicate.Id, newModelReplicate.IdentityPath.GetIdentity(0));

            Assert.AreNotSame(docReplicate, Replicate.LoadChromSetFromDocument(SkylineWindow.Document, (Replicate)newModelReplicate));
            Assert.AreSame(newDocReplicate, Replicate.LoadChromSetFromDocument(SkylineWindow.Document, (Replicate)newModelReplicate));

            Assert.AreEqual(newDocReplicate.FileCount, newModelReplicate.Files.Count);
            Assert.AreEqual(newChromatogramName, newDocReplicate.Name);
            Assert.AreEqual(newDocReplicate.Name, newModelReplicate.Name);
            Assert.AreEqual(Path.GetFileName(newDocReplicate.FilePath), newModelReplicate.FileName);
            Assert.AreEqual(newDocReplicate.FilePath, newModelReplicate.FilePath);
        }
    }
}
