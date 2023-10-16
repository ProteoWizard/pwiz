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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.Database;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class EncyclopeDiaHelpersTest : AbstractUnitTest
    {
        const string TEST_ZIP_PATH = @"Test\EncyclopeDiaHelpersTest.zip";

        [TestMethod, NoParallelTesting(TestExclusionReason.SHARED_DIRECTORY_WRITE)]
        public void TestConvertFastaToPrositInputCsv()
        {
            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string prositCsvOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33.csv");
            string prositExpectedCsvOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected.csv");
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705.fasta");
            var testConfig = new EncyclopeDiaHelpers.FastaToPrositInputCsvConfig
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
            EncyclopeDiaHelpers.ConvertFastaToPrositInputCsv(fastaFilepath, prositCsvOutputFilepath, pm, ref status, testConfig);
            Assert.IsTrue(File.Exists(prositCsvOutputFilepath));
            AssertEx.NoDiff(File.ReadAllText(prositExpectedCsvOutputFilepath), File.ReadAllText(prositCsvOutputFilepath));
        }

        [TestMethod]
        public void TestPrositOutputToEncyclopediaLibraries()
        {
            // Wine has mysterious issues running EncylopeDia, but SkylineCmd currently can't run EncyclopeDia anyway, so skip these tests
            if (ProcessEx.IsRunningOnWine)
                return;

            TestFilesDir = new TestFilesDir(TestContext, TEST_ZIP_PATH);
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705.fasta");
            string dlibFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33.dlib");
            string elibFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33.elib");
            string elibQuantFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-quant-elib.elib");
            var columnTolerances = new Dictionary<int, double>() { { -1, 0.000005 } };  // Allow some numerical wiggle in any column numerical column ("-1" means all columns)
            IProgressStatus status = new ProgressStatus();

            // test prosit output to dlib
            {
                string prositBlibOutputFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-output.blib");
                string dlibExpectedTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-dlib.tsv");
                var pm = new CommandProgressMonitor(new StringWriter(), new ProgressStatus());
                EncyclopeDiaHelpers.ConvertPrositOutputToDlib(prositBlibOutputFilepath, fastaFilepath, dlibFilepath, pm, ref status);
                Assert.IsTrue(File.Exists(dlibFilepath));
                var actual = SqliteOperations.DumpTable(dlibFilepath, "entries");
                //string dlibActualTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-dlib-actual.tsv");
                //File.WriteAllLines(dlibActualTsvFilepath, actual);
                AssertEx.NoDiff(File.ReadAllText(dlibExpectedTsvFilepath), string.Join("\n", actual), null, columnTolerances);
            }

            // test generate libraries
            {
                string elibExpectedTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-elib.tsv");
                string elibQuantExpectedTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-quant-elib.tsv");
                var testConfig = new EncyclopeDiaHelpers.EncyclopeDiaConfig
                {
                    PercolatorTrainingFDR = 0.5,
                    PercolatorThreshold = 0.5,
                    MinNumOfQuantitativePeaks = 1,
                    NumberOfQuantitativePeaks = 1,
                    V2scoring = false
                };
                var narrowWindowFiles = new MsDataFileUri[]
                {
                    new MsDataFilePath(TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_4mz_narrow_3.mzML")),
                    new MsDataFilePath(TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_4mz_narrow_4.mzML")),
                };
                var wideWindowFiles = new MsDataFileUri[]
                {
                    new MsDataFilePath(TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1c.mzML")),
                    new MsDataFilePath(TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1d.mzML")),
                };
                var pm = new CommandProgressMonitor(new StringWriter(), new ProgressStatus());
                EncyclopeDiaHelpers.Generate(dlibFilepath, elibFilepath, elibQuantFilepath, fastaFilepath, testConfig,
                    narrowWindowFiles, wideWindowFiles, pm, pm, CancellationToken.None, new ProgressStatus());

                var actual = SqliteOperations.DumpTable(elibFilepath, "entries", sortColumns: new[] { "PrecursorMz" })
                    .Concat(SqliteOperations.DumpTable(elibFilepath, "peptidescores", sortColumns: new[] { "PeptideModSeq", "PrecursorCharge" }))
                    .Concat(SqliteOperations.DumpTable(elibFilepath, "retentiontimes", sortColumns: new[] { "SourceFile", "Library", "Actual" }))
                    .Concat(SqliteOperations.DumpTable(elibFilepath, "peptidequants", sortColumns: new [] { "PeptideModSeq", "PrecursorCharge", "SourceFile"}));
                //string elibActualTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-elib-actual.tsv");
                //File.WriteAllLines(elibActualTsvFilepath, actual);
                AssertEx.NoDiff(File.ReadAllText(elibExpectedTsvFilepath), string.Join("\n", actual), null, columnTolerances);

                var actualQuant = SqliteOperations.DumpTable(elibQuantFilepath, "entries", sortColumns: new[] { "PrecursorMz" })
                    .Concat(SqliteOperations.DumpTable(elibQuantFilepath, "peptidescores", sortColumns: new[] { "PeptideModSeq", "PrecursorCharge" }, excludeColumns: new[] { "SourceFile", "PosteriorErrorProbability" }))
                    .Concat(SqliteOperations.DumpTable(elibQuantFilepath, "retentiontimes", sortColumns: new[] { "SourceFile", "Library", "Actual" }))
                    .Concat(SqliteOperations.DumpTable(elibQuantFilepath, "peptidequants", sortColumns: new[] { "PeptideModSeq", "PrecursorCharge", "SourceFile" }));
                //string elibQuantActualTsvFilepath = TestFilesDir.GetTestPath("pan_human_library_690to705-z3_nce33-expected-quant-elib-actual.tsv");
                //File.WriteAllLines(elibQuantActualTsvFilepath, actualQuant);
                AssertEx.NoDiff(File.ReadAllText(elibQuantExpectedTsvFilepath), string.Join("\n", actualQuant), null, columnTolerances);
            }
            TestFilesDir.Cleanup();
        }
    }
}
