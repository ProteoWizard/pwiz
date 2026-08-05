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
using System.Threading;
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
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineReportTest.zip");
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
            string invariantName = nameof(ColumnCaptions.PeptideRetentionTime);

            var columnNames = GetParquetColumnNames(outPath);
            string message = string.Format(@"Columns of {0} were: {1}",
                Path.GetFileName(outPath), string.Join(@", ", columnNames));
            Assert.AreEqual(expectInvariant, columnNames.Contains(invariantName), message);
            Assert.AreEqual(!expectInvariant, columnNames.Contains(localizedName), message);
        }

        /// <summary>
        /// ParquetReader has no synchronous API, so this waits on the async one, which a test
        /// can afford to do. What it cannot do is wait while a SynchronizationContext is
        /// installed, because the continuation gets posted back to this thread while it is
        /// blocked waiting for it, and neither ever runs. TestRunner's thread has one once a
        /// functional test has run.
        /// </summary>
        private static IList<string> GetParquetColumnNames(string path)
        {
            var saveContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var schema = ParquetReader.ReadSchemaAsync(path).GetAwaiter().GetResult();
                return schema.GetDataFields().Select(dataField => dataField.Name).ToList();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saveContext);
            }
        }
    }
}
