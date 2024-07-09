/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class EncyclopeDiaHelpersTest : AbstractUnitTest
    {
        const string TEST_ZIP_PATH = @"Test\EncyclopeDiaHelpersTest.zip";

        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestConvertFastaToKoinaInputCsv()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string koinaCsvOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33.csv");
            string koinaExpectedCsvOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected.csv");
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705.fasta");
            var testConfig = new EncyclopeDiaHelpers.FastaToKoinaInputCsvConfig
            {
                DefaultCharge = 3,
                DefaultNCE = 33,
                MinCharge = 2,
                MaxCharge = 3,
                MinMz = 690,
                MaxMz = 705
            };
            var pm = new CommandProgressMonitor(new StringWriter(), new ProgressStatus());
            IProgressStatus status = new ProgressStatus();
            EncyclopeDiaHelpers.ConvertFastaToKoinaInputCsv(fastaFilepath, koinaCsvOutputFilepath, pm, ref status, testConfig);
            Assert.IsTrue(File.Exists(koinaCsvOutputFilepath));
            AssertEx.NoDiff(File.ReadAllText(koinaExpectedCsvOutputFilepath), File.ReadAllText(koinaCsvOutputFilepath));
        }
    }
}
