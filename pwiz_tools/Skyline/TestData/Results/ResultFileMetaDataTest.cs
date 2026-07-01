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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Spectra;
using pwiz.CommonMsData;
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
            var openParams = new OpenMsDataFileParams { CentroidMs1 = true };
            using (var msDataFile = openParams.OpenLocalFile(msDataFileUri))
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

        /// <summary>
        /// Verifies that each spectrum's <see cref="SpectrumMetadata.OtherParams"/> (the mzML CV /
        /// user-param term bag) survives serialization to the ResultFileMetaData protobuf blob and
        /// reconstruction. Also verifies that spectra carrying no otherParams (e.g. an old cache written
        /// before this field existed) reconstruct to empty per-spectrum lists rather than crashing.
        /// The round-trip goes through the public <see cref="ResultFileMetaData.ToByteArray"/> /
        /// <see cref="ResultFileMetaData.FromByteArray"/> wrapper, which internally exercises
        /// <see cref="SpectrumMetadatas.ToProto"/> and the proto reconstruction constructor.
        /// </summary>
        [TestMethod]
        public void TestResultFileMetaDataOtherParams()
        {
            // A term that is shared by more than one spectrum, to exercise the global distinct pool.
            var basePeakIntensity = new SpectrumMetadataTerm(@"MS:1000505", @"base peak intensity",
                @"12345.6", @"number of detector counts", @"The intensity of the greatest peak in the mass spectrum.");
            // A user param with no unit and no definition (null fields must round-trip as null).
            var userParam = new SpectrumMetadataTerm(@"my custom flag", @"my custom flag", @"true", null);
            var totalIonCurrent = new SpectrumMetadataTerm(@"MS:1000285", @"total ion current",
                @"98765.4", @"number of detector counts", @"The sum of all the separate ion currents.");

            var spectra = new[]
            {
                new SpectrumMetadata(@"scan=1", 1.0)
                    .ChangeOtherParams(new[] { basePeakIntensity, userParam }),
                new SpectrumMetadata(@"scan=2", 2.0)
                    .ChangeOtherParams(new[] { basePeakIntensity, totalIonCurrent }),
                new SpectrumMetadata(@"scan=3", 3.0), // no otherParams at all
            };

            var roundTripped = RoundTrip(spectra);
            Assert.AreEqual(spectra.Length, roundTripped.Count);
            for (int i = 0; i < spectra.Length; i++)
            {
                AssertOtherParamsEqual(spectra[i].OtherParams, roundTripped[i].OtherParams);
            }

            // Back-compat: spectra with no otherParams at all (as an old cache blob would have) must
            // reconstruct to empty per-spectrum lists.
            var noParamSpectra = new[]
            {
                new SpectrumMetadata(@"scan=1", 1.0),
                new SpectrumMetadata(@"scan=2", 2.0),
                new SpectrumMetadata(@"scan=3", 3.0),
            };
            var fromOld = RoundTrip(noParamSpectra);
            Assert.AreEqual(noParamSpectra.Length, fromOld.Count);
            for (int i = 0; i < fromOld.Count; i++)
            {
                Assert.AreEqual(0, fromOld[i].OtherParams.Count);
            }
        }

        private static SpectrumMetadatas RoundTrip(IEnumerable<SpectrumMetadata> spectra)
        {
            var bytes = new ResultFileMetaData(spectra).ToByteArray();
            return ResultFileMetaData.FromByteArray(bytes).SpectrumMetadatas;
        }

        private static void AssertOtherParamsEqual(IList<SpectrumMetadataTerm> expected,
            IList<SpectrumMetadataTerm> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Accession, actual[i].Accession);
                Assert.AreEqual(expected[i].Name, actual[i].Name);
                Assert.AreEqual(expected[i].Value, actual[i].Value);
                Assert.AreEqual(expected[i].Unit, actual[i].Unit);
                Assert.AreEqual(expected[i].Definition, actual[i].Definition);
            }
        }
    }
}
