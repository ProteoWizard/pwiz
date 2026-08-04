/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineRunner;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Tests exporting reports from the command line.
    /// </summary>
    [TestClass]
    public class CommandLineReportTest : AbstractUnitTestEx
    {
        /// <summary>
        /// The column name that the invariant language setting produces. Unlike the localized
        /// caption this is a stable identifier, since it is what scripts reading the exported
        /// file have to refer to.
        /// </summary>
        private const string INVARIANT_RETENTION_TIME = "PeptideRetentionTime";

        /// <summary>
        /// Exports the "Peptide Ratio Results" report to parquet with each form of the
        /// "--report-invariant" argument, and checks the column names in the resulting file.
        /// Parquet is written to be read by other programs, so it is invariant unless asked
        /// otherwise, which is the opposite of the text formats.
        ///
        /// This also verifies the thread-safety of "CalibrationCurveFitter", since the report
        /// has calibration curve columns and parquet export is multi-threaded.
        /// </summary>
        [TestMethod]
        public void TestParquetReportInvariant()
        {
            TestFilesZip = @"https://skyline.ms/tutorials/LiveReports.zip";
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZipPaths[0]);
            string docPath = TestFilesDir.GetTestPath(@"Rat_plasma.sky");
            AssertEx.FileExists(docPath);

            ExportAndVerifyColumnNames(docPath, @"default", null, true);
            ExportAndVerifyColumnNames(docPath, @"bare", @"--report-invariant", true);
            ExportAndVerifyColumnNames(docPath, @"true", @"--report-invariant=true", true);
            ExportAndVerifyColumnNames(docPath, @"false", @"--report-invariant=false", false);
        }

        private void ExportAndVerifyColumnNames(string docPath, string fileSuffix, string invariantArgument,
            bool expectInvariant)
        {
            string outPath = TestFilesDir.GetTestPath(string.Format(@"Rat_plasma_{0}.parquet", fileSuffix));
            AssertEx.FileNotExists(outPath);

            var arguments = new List<string>
            {
                @"--in=" + docPath,
                @"--report-name=" + Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results,
                @"--report-file=" + outPath,
                @"--report-format=" + ReportFormat.parquet
            };
            if (invariantArgument != null)
            {
                arguments.Add(invariantArgument);
            }

            var output = RunCommand(arguments.ToArray());
            Assert.IsFalse(output.ReadLines().Any(ErrorChecker.IsErrorLine),
                "Unexpected error found in output:\r\n{0}", output);
            AssertEx.FileExists(outPath);

            // The localized caption goes through the same sanitizing as any other column name,
            // which replaces the spaces in it
            string localizedName = ParquetReportExporter
                .MakeValidColumnNames(new[] { ColumnCaptions.PeptideRetentionTime }).Single();
            string expected = expectInvariant ? INVARIANT_RETENTION_TIME : localizedName;
            string unexpected = expectInvariant ? localizedName : INVARIANT_RETENTION_TIME;

            var columnNames = GetParquetColumnNames(outPath);
            string message = string.Format(@"Columns of {0} were: {1}",
                Path.GetFileName(outPath), string.Join(@", ", columnNames));
            AssertEx.IsTrue(columnNames.Contains(expected),
                string.Format(@"Expected a column named {0}. {1}", expected, message));
            AssertEx.IsFalse(columnNames.Contains(unexpected),
                string.Format(@"Did not expect a column named {0}. {1}", unexpected, message));
        }

        private static IList<string> GetParquetColumnNames(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = ParquetReader.CreateAsync(stream).ConfigureAwait(false).GetAwaiter().GetResult();
            return reader.Schema.GetDataFields().Select(dataField => dataField.Name).ToList();
        }
    }
}
