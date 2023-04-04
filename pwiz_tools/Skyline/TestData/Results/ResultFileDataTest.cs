using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class ResultFileDataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestResultFileData()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"TestData\Results\MsxTest.zip");
            TestFilesDirs = new[]
            {
                testFilesDir
            };
            var msDataFileUri = new MsDataFilePath(testFilesDir.GetTestPath("MsxTest.mzML"));
            using (var msDataFile = msDataFileUri.OpenMsDataFile(true, false, false, false, false))
            {
                var spectrumMetadatas = Enumerable.Range(0, msDataFile.SpectrumCount)
                    .Select(i => msDataFile.GetSpectrumMetadata(i)).ToList();
                Assert.AreNotEqual(0, spectrumMetadatas.Count);
                var resultFileData = new ResultFileMetaData(spectrumMetadatas);
                var bytes = resultFileData.ToByteArray();
                var resultFileData2 = ResultFileMetaData.FromByteArray(bytes);
                AssertEx.AreEqual(resultFileData.SpectrumMetadatas, resultFileData2.SpectrumMetadatas);
            }
        }
    }
}
