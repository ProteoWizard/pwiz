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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that saving a file with <see cref="ResultFileMetaData" />
    /// gets properly converted back to the old <see cref="MsDataFileScanIds" /> when doing
    /// a "File > Share" to an older version of Skyline.
    /// </summary>
    [TestClass]
    public class ResultFileMetadataBackCompatTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestResultFileMetadataBackCompat()
        {
            TestFilesZip = @"TestFunctional\ResultFileMetadataBackCompatTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ResultFileMetadataBackCompatTest.sky")));
            WaitForDocumentLoaded();
            var originalInfo = new MeasuredResultsInfo(SkylineWindow.Document.Settings.MeasuredResults);
            Assert.AreEqual(2, originalInfo.ChromCachedFiles.Count);

            Assert.IsFalse(originalInfo.ChromCachedFiles[0].HasResultFileData);
            Assert.IsNull(originalInfo.ResultFileMetaDataList[0]);
            Assert.IsNotNull(originalInfo.MsDataFileScanIdsList[0]);

            Assert.IsTrue(originalInfo.ChromCachedFiles[1].HasResultFileData);
            Assert.IsNotNull(originalInfo.ResultFileMetaDataList[1]);
            Assert.IsNotNull(originalInfo.MsDataFileScanIdsList[1]);
            VerifyEquivalent(originalInfo.MsDataFileScanIdsList[1], originalInfo.ResultFileMetaDataList[1]);

            var oldFormatZipPath = TestFilesDir.GetTestPath("OldFormat.sky.zip");
            RunUI(() => SkylineWindow.ShareDocument(oldFormatZipPath,
                ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.V22_2)));
            RunUI(()=>SkylineWindow.OpenSharedFile(oldFormatZipPath));
            WaitForDocumentLoaded();
            var oldFormatInfo = new MeasuredResultsInfo(SkylineWindow.Document.Settings.MeasuredResults);

            Assert.AreEqual(2, oldFormatInfo.ChromCachedFiles.Count);
            Assert.IsFalse(oldFormatInfo.ChromCachedFiles[0].HasResultFileData);
            Assert.IsFalse(oldFormatInfo.ChromCachedFiles[0].HasResultFileData);
            VerifyEquivalent(oldFormatInfo.MsDataFileScanIdsList[1], originalInfo.ResultFileMetaDataList[1]);
        }

        /// <summary>
        /// Verifies that the spectrum identifiers in the <see cref="MsDataFileScanIds" /> are
        /// the same as the ones in the <see cref="ResultFileMetaData" />.
        /// </summary>
        private void VerifyEquivalent(MsDataFileScanIds msDataFileScanIds, ResultFileMetaData resultFileMetadata)
        {
            for (int i = 0; i < resultFileMetadata.SpectrumMetadatas.Count; i++)
            {
                Assert.AreEqual(resultFileMetadata.SpectrumMetadatas[i].Id, msDataFileScanIds.GetMsDataFileSpectrumId(i), "Mismatch on scan #{0}", i);
            }
        }

        internal class MeasuredResultsInfo
        {
            public MeasuredResultsInfo(MeasuredResults measuredResults)
            {
                MeasuredResults = measuredResults;
                ChromCachedFiles = measuredResults.CachedFileInfos.ToList();
                MsDataFileScanIdsList = ChromCachedFiles.Select(file =>
                    measuredResults.LoadMSDataFileScanIds(file.FilePath, out _)).ToList();
                ResultFileMetaDataList = ChromCachedFiles
                    .Select(file => measuredResults.GetResultFileMetaData(file.FilePath)).ToList();
            }
            public MeasuredResults MeasuredResults { get; }
            public IList<ChromCachedFile> ChromCachedFiles { get; }
            public IList<MsDataFileScanIds> MsDataFileScanIdsList { get; }
            public IList<ResultFileMetaData> ResultFileMetaDataList { get; }
        }
    }
}
