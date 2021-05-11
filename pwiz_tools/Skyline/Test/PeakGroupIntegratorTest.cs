using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeakGroupIntegratorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPeakGroupIntegrator()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"Test\PeakGroupIntegratorTest.zip");
            var docPath = testFilesDir.GetTestPath("PeakGroupIntegratorTest.sky");
            var docContainer =
                new ResultsTestDocumentContainer(null, docPath);
            var doc = ResultsUtil.DeserializeDocument(docPath);
            doc = ResolveLibraries(doc, Path.GetDirectoryName(docPath));

            docContainer.SetDocument(doc, docContainer.Document, true);
            doc = docContainer.Document;
            Assert.IsTrue(doc.IsLoaded);
            var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms[0];
            var peakGroupIntegrator = PeakGroupIntegrator
                .GetPeakGroupIntegrators(doc, doc.GetPathTo((int) SrmDocument.Level.Molecules, 0), chromatogramSet, chromatogramSet.MSDataFileInfos[0]).FirstOrDefault();
            Assert.IsNotNull(peakGroupIntegrator);
            var peptideChromDataSets = peakGroupIntegrator.MakePeptideChromDataSets();
            Assert.IsNotNull(peptideChromDataSets);

        }

        private SrmDocument ResolveLibraries(SrmDocument document, string folderPath)
        {
            return document.ChangeSettings(document.Settings.ChangePeptideLibraries(lib =>
                lib.ChangeLibrarySpecs(lib.Libraries.Select(library =>
                    library.CreateSpec(Path.Combine(folderPath, Path.GetFileName(library.FileNameHint)))).ToList())
            ));
        }
    }
}
