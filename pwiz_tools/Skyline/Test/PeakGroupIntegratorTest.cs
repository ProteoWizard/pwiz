﻿using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
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
                .GetPeakGroupIntegrators(doc, doc.GetPathTo((int) SrmDocument.Level.Molecules, 0), chromatogramSet,
                    chromatogramSet.MSDataFileInfos[0]).FirstOrDefault();
            Assert.IsNotNull(peakGroupIntegrator);
            var peptideChromDataSets = peakGroupIntegrator.MakePeptideChromDataSets();
            Assert.IsNotNull(peptideChromDataSets);
            peptideChromDataSets.PickChromatogramPeaks(new ExplicitPeakBounds(56.5, 57.5, 0));
            var dataSchema = new SkylineDataSchema(docContainer, DataSchemaLocalizer.INVARIANT);
            var precursor = new Precursor(dataSchema,
                new IdentityPath(peakGroupIntegrator.PeptideIdentityPath,
                    peakGroupIntegrator.ComparableGroup.First().TransitionGroup));
            var replicate = new Replicate(dataSchema, 0);
            var resultFile = new ResultFile(replicate, chromatogramSet.MSDataFileInfos.First().FileId, 0);
            var precursorResult = new PrecursorResult(precursor, resultFile);
            var chromatogramGroupInfos = peptideChromDataSets.MakeChromatogramGroupInfos().ToList();
            var scores = precursorResult.GetPeakScores(chromatogramGroupInfos[0]).ToList();
            Assert.IsNotNull(scores);
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
