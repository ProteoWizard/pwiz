/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class ResultFileMetaDataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestResultFileMetaData()
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
