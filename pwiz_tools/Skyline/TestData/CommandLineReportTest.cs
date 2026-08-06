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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineRunner;
using pwiz.SkylineTestUtil;
using PersistedViews = pwiz.Skyline.Model.PersistedViews;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Tests exporting reports from the command line.
    /// </summary>
    [TestClass]
    public class CommandLineReportTest : AbstractUnitTestEx
    {
        private const string REPORT_NAME = "CommandLineReportTest";

        /// <summary>
        /// The exports are done in this culture so that the expected values below are the
        /// same no matter which language the test run is using. Skyline has no resources
        /// for French, so the captions and messages stay in English, and the only thing
        /// that changes is the number formatting, which is what these tests are about.
        /// </summary>
        private static readonly CultureInfo LOCALIZED_CULTURE = CultureInfo.GetCultureInfo("fr-FR");

        // The first row of REPORT_NAME for Rat_plasma.sky.
        private const string SEQUENCE = "CSLPRPWALTFSYGR";
        private const string INVARIANT_RETENTION_TIME = "30.817350387573242";
        private const string LOCALIZED_RETENTION_TIME = "30,82";
        private const string INVARIANT_QUANTIFICATION = "Normalized Area: 1.3746E+5";
        private const string LOCALIZED_QUANTIFICATION = "Normalized Area: 1,3746E+5";

        /// <summary>
        /// Exports the report to parquet with each form of the "--report-invariant" argument,
        /// and checks the column names and the culture-dependent value in the resulting file.
        /// Parquet is written to be read by other programs, so it is invariant unless asked
        /// otherwise, which is the opposite of the text formats.
        ///
        /// This also verifies the thread-safety of "CalibrationCurveFitter", since the
        /// "Quantification" column goes through one and parquet export is multi-threaded.
        /// </summary>
        [TestMethod]
        public void TestParquetReportInvariant()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineReportTest.zip");
            string docPath = TestFilesDir.GetTestPath(@"Rat_plasma.sky");
            AssertEx.FileExists(docPath);
            using (new CurrentCultureSetter(LOCALIZED_CULTURE))
            {
                AddTestReport();
                try
                {
                    ExportAndVerifyColumnNames(docPath, @"default", null, true);
                    ExportAndVerifyColumnNames(docPath, @"bare", @"--report-invariant", true);
                    ExportAndVerifyColumnNames(docPath, @"true", @"--report-invariant=true", true);
                    ExportAndVerifyColumnNames(docPath, @"false", @"--report-invariant=false", false);
                }
                finally
                {
                    Settings.Default.PersistedViews.ResetDefaults();
                }
            }
        }

        /// <summary>
        /// Exports the report to both text formats with each form of the "--report-invariant"
        /// argument. Unlike parquet, the text formats are localized unless asked otherwise.
        /// The column separator of a .csv file depends on the culture the numbers are being
        /// written in, because a culture that writes "1,5" cannot also separate columns with
        /// commas.
        /// </summary>
        [TestMethod]
        public void TestTextReportInvariant()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineReportTest.zip");
            string docPath = TestFilesDir.GetTestPath(@"Rat_plasma.sky");
            AssertEx.FileExists(docPath);
            using (new CurrentCultureSetter(LOCALIZED_CULTURE))
            {
                AddTestReport();
                try
                {
                    foreach (var reportFormat in new[] { ReportFormat.csv, ReportFormat.tsv })
                    {
                        // A comma cannot separate the columns of a file whose numbers are
                        // written with a comma, so the localized .csv gets a semicolon.
                        char invariantSeparator = reportFormat == ReportFormat.csv
                            ? TextUtil.SEPARATOR_CSV
                            : TextUtil.SEPARATOR_TSV;
                        char localizedSeparator = reportFormat == ReportFormat.csv
                            ? TextUtil.SEPARATOR_CSV_INTL
                            : TextUtil.SEPARATOR_TSV;
                        ExportAndVerifyTextValues(docPath, reportFormat, @"default", null,
                            localizedSeparator, false);
                        ExportAndVerifyTextValues(docPath, reportFormat, @"bare", @"--report-invariant",
                            invariantSeparator, true);
                        ExportAndVerifyTextValues(docPath, reportFormat, @"true", @"--report-invariant=true",
                            invariantSeparator, true);
                        ExportAndVerifyTextValues(docPath, reportFormat, @"false", @"--report-invariant=false",
                            localizedSeparator, false);
                    }
                }
                finally
                {
                    Settings.Default.PersistedViews.ResetDefaults();
                }
            }
        }

        /// <summary>
        /// Adds the report that the tests export. It has a column whose value has no storage
        /// type of its own and is therefore written as text, which is the only way the culture
        /// the report is being exported in can be seen in a parquet file.
        /// </summary>
        private static void AddTestReport()
        {
            var resultsPath = PropertyPath.Root.Property(nameof(Peptide.Results));
            var peptideResultPath = resultsPath.DictionaryValues();
            var viewSpec = new ViewSpec()
                .SetName(REPORT_NAME)
                .SetRowType(typeof(Peptide))
                .SetSublistId(resultsPath.LookupAllItems())
                .SetColumns(new[]
                {
                    new ColumnSpec(PropertyPath.Root.Property(nameof(Peptide.Sequence))),
                    new ColumnSpec(peptideResultPath.Property(nameof(PeptideResult.PeptideRetentionTime))),
                    new ColumnSpec(peptideResultPath.Property(nameof(PeptideResult.Quantification)))
                });
            var viewSpecList = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
            Settings.Default.PersistedViews.SetViewSpecList(PersistedViews.MainGroup.Id,
                viewSpecList.AddOrReplaceViews(new[] { new ViewSpecLayout(viewSpec, ViewLayoutList.EMPTY) }));
        }

        private string ExportReport(string docPath, ReportFormat reportFormat, string fileSuffix,
            string invariantArgument)
        {
            string outPath = TestFilesDir.GetTestPath(string.Format(@"Rat_plasma_{0}_{1}.{2}",
                reportFormat, fileSuffix, reportFormat));
            AssertEx.FileNotExists(outPath);

            var arguments = new List<string>
            {
                @"--in=" + docPath,
                @"--report-name=" + REPORT_NAME,
                @"--report-file=" + outPath,
                @"--report-format=" + reportFormat
            };
            if (invariantArgument != null)
            {
                arguments.Add(invariantArgument);
            }

            var output = RunCommand(arguments.ToArray());
            Assert.IsFalse(output.ReadLines().Any(ErrorChecker.IsErrorLine),
                "Unexpected error found in output:\r\n{0}", output);
            AssertEx.FileExists(outPath);
            return outPath;
        }

        private void ExportAndVerifyColumnNames(string docPath, string fileSuffix, string invariantArgument,
            bool expectInvariant)
        {
            string outPath = ExportReport(docPath, ReportFormat.parquet, fileSuffix, invariantArgument);

            var columnNames = GetParquetColumnNames(outPath);
            string message = string.Format(@"Columns of {0} were: {1}",
                Path.GetFileName(outPath), string.Join(@", ", columnNames));
            Assert.AreEqual(expectInvariant,
                columnNames.Contains(nameof(ColumnCaptions.PeptideRetentionTime)), message);
            Assert.AreEqual(!expectInvariant,
                columnNames.Contains(SanitizedCaption(ColumnCaptions.PeptideRetentionTime)), message);

            // The retention time is stored as a number, so only the "Quantification" column,
            // which has no storage type of its own and is written as text, can show which
            // culture the values were formatted in.
            string quantificationColumn = expectInvariant
                ? nameof(ColumnCaptions.Quantification)
                : SanitizedCaption(ColumnCaptions.Quantification);
            Assert.AreEqual(expectInvariant ? INVARIANT_QUANTIFICATION : LOCALIZED_QUANTIFICATION,
                GetParquetFirstValue(outPath, quantificationColumn), message);
        }

        private void ExportAndVerifyTextValues(string docPath, ReportFormat reportFormat, string fileSuffix,
            string invariantArgument, char expectedSeparator, bool expectInvariant)
        {
            string outPath = ExportReport(docPath, reportFormat, fileSuffix, invariantArgument);

            var lines = File.ReadAllLines(outPath);
            string message = string.Format(@"First two lines of {0} were:\r\n{1}",
                Path.GetFileName(outPath), string.Join(@"\r\n", lines.Take(2)));
            var captions = lines[0].Split(expectedSeparator);
            var values = lines[1].Split(expectedSeparator);
            // A wrong separator leaves the whole line in one field, so this is what says the
            // file is separated by the character it is supposed to be.
            Assert.AreEqual(3, captions.Length, message);
            Assert.AreEqual(3, values.Length, message);

            Assert.AreEqual(expectInvariant
                ? nameof(ColumnCaptions.PeptideRetentionTime)
                : ColumnCaptions.PeptideRetentionTime, captions[1], message);
            Assert.AreEqual(expectInvariant
                ? nameof(ColumnCaptions.Quantification)
                : ColumnCaptions.Quantification, captions[2], message);

            Assert.AreEqual(SEQUENCE, values[0], message);
            Assert.AreEqual(expectInvariant ? INVARIANT_RETENTION_TIME : LOCALIZED_RETENTION_TIME,
                values[1], message);
            Assert.AreEqual(expectInvariant ? INVARIANT_QUANTIFICATION : LOCALIZED_QUANTIFICATION,
                values[2], message);
        }

        /// <summary>
        /// The localized caption goes through the same sanitizing as any other parquet column
        /// name, which replaces the spaces in it.
        /// </summary>
        private static string SanitizedCaption(string caption)
        {
            return ParquetReportExporter.MakeValidColumnNames(new[] { caption }).Single();
        }

        private static IList<string> GetParquetColumnNames(string path)
        {
            return ReadParquet(path, reader => reader.Schema.GetDataFields()
                .Select(dataField => dataField.Name).ToList());
        }

        private static string GetParquetFirstValue(string path, string columnName)
        {
            return ReadParquet(path, reader =>
            {
                var dataField = reader.Schema.GetDataFields()
                    .FirstOrDefault(field => Equals(columnName, field.Name));
                Assert.IsNotNull(dataField, "No column named {0} in {1}", columnName, Path.GetFileName(path));
                using var groupReader = reader.OpenRowGroupReader(0);
                var data = groupReader.ReadColumnAsync(dataField).GetAwaiter().GetResult().Data;
                Assert.AreNotEqual(0, data.Length, "No rows in {0}", Path.GetFileName(path));
                return data.GetValue(0) as string;
            });
        }

        /// <summary>
        /// ParquetReader has no synchronous API, so this waits on the async one, which a test
        /// can afford to do. What it cannot do is wait while a SynchronizationContext is
        /// installed, because the continuation gets posted back to this thread while it is
        /// blocked waiting for it, and neither ever runs. TestRunner's thread has one once a
        /// functional test has run.
        /// </summary>
        private static T ReadParquet<T>(string path, System.Func<ParquetReader, T> readFunc)
        {
            var saveContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                using var reader = ParquetReader.CreateAsync(path).GetAwaiter().GetResult();
                return readFunc(reader);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saveContext);
            }
        }
    }
}
