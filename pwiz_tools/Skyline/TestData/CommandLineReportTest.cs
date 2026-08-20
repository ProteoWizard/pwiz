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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineRunner;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.SkylineTestData
{
    /// <summary>
    /// Tests exporting reports from the command line.
    /// </summary>
    [TestClass]
    public class CommandLineReportTest : AbstractUnitTestEx
    {
        /// <summary>
        /// The report these tests export is one of the defaults, so that nothing here has to
        /// define it. It is the one default which has a "Quantification" column, whose value
        /// has no parquet storage type of its own and is therefore written as text, which is
        /// the only way the culture a parquet file was exported in can be seen at all.
        ///
        /// The tests reset the settings so the defaults are present under the localized names
        /// <see cref="Resources"/> gives them. They are added the first time
        /// Settings.Default.PersistedViews is read.
        /// </summary>
        private static string ReportName => Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results;

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
            string docPath = GetTestDocumentPath();
            ExportAndVerifyColumnNames(docPath, @"default", null, true);
            ExportAndVerifyColumnNames(docPath, @"bare", @"--report-invariant", true);
            ExportAndVerifyColumnNames(docPath, @"true", @"--report-invariant=true", true);
            ExportAndVerifyColumnNames(docPath, @"false", @"--report-invariant=false", false);
        }

        /// <summary>
        /// Resets the settings so <see cref="ReportName"/> is present, and returns the
        /// document to export. The export runs in this process, so the settings it reads are
        /// the ones reset here.
        /// </summary>
        private string GetTestDocumentPath()
        {
            Settings.Default.Reset();
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineReportTest.zip");
            string docPath = TestFilesDir.GetTestPath(@"Rat_plasma.sky");
            AssertEx.FileExists(docPath);
            return docPath;
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
            string docPath = GetTestDocumentPath();
            foreach (var reportFormat in new[] { ReportFormat.csv, ReportFormat.tsv })
            {
                // A comma cannot separate the columns of a file whose numbers are
                // written with a comma, so a comma-decimal culture gets a semicolon.
                char invariantSeparator = reportFormat == ReportFormat.csv
                    ? TextUtil.SEPARATOR_CSV
                    : TextUtil.SEPARATOR_TSV;
                char localizedSeparator = reportFormat == ReportFormat.csv
                    ? TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture)
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

        private string ExportReport(string docPath, ReportFormat reportFormat, string fileSuffix,
            string invariantArgument)
        {
            string outPath = TestFilesDir.GetTestPath(string.Format(@"Rat_plasma_{0}_{1}.{2}",
                reportFormat, fileSuffix, reportFormat));
            AssertEx.FileNotExists(outPath);

            var arguments = new List<string>
            {
                @"--in=" + docPath,
                @"--report-name=" + ReportName,
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
            Assert.AreEqual(ExpectedQuantification(docPath, expectInvariant),
                GetParquetFirstValue(outPath, quantificationColumn), message);
        }

        /// <summary>
        /// What the export should write for the "Quantification" column of the first row,
        /// taken from the document rather than written down here. The value has no storage
        /// type of its own, so it is written by calling ToString on it, which formats the
        /// number with the current culture and looks the "Normalized Area" caption up in the
        /// current language. The invariant export does both of those under the invariant
        /// culture, which also falls back to the neutral (English) resources.
        /// </summary>
        private string ExpectedQuantification(string docPath, bool expectInvariant)
        {
            var quantification = GetFirstPeptideResult(docPath).Quantification;
            // Without this a row with no quantification would compare an empty expectation
            // against an empty column and pass without checking any formatting.
            Assert.IsNotNull(quantification.Value,
                "The first row has no quantification, so this test has nothing to compare.");
            return expectInvariant
                ? LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () => quantification.ToString())
                : quantification.ToString();
        }

        /// <summary>
        /// What the export should write for the "PeptideRetentionTime" column of the first
        /// row. The invariant export writes the round trip form of the number and the
        /// localized export writes the display format, so these differ in precision as well
        /// as in the decimal separator.
        /// </summary>
        private string ExpectedRetentionTime(string docPath, bool expectInvariant)
        {
            double retentionTime = GetFirstPeptideResult(docPath).PeptideRetentionTime.Value;
            return expectInvariant
                ? retentionTime.ToString(Formats.RoundTrip, CultureInfo.InvariantCulture)
                : retentionTime.ToString(Formats.RETENTION_TIME, CultureInfo.CurrentCulture);
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
            Assert.AreEqual(captions.Length, values.Length, message);

            // Looking the columns up by caption is also what says the file is separated by the
            // character it is supposed to be: a wrong separator leaves the whole line in one
            // field, and then none of these captions is found.
            Assert.AreEqual(ExpectedRetentionTime(docPath, expectInvariant),
                values[IndexOfColumn(captions, expectInvariant
                    ? nameof(ColumnCaptions.PeptideRetentionTime)
                    : ColumnCaptions.PeptideRetentionTime, message)], message);
            Assert.AreEqual(ExpectedQuantification(docPath, expectInvariant),
                values[IndexOfColumn(captions, expectInvariant
                    ? nameof(ColumnCaptions.Quantification)
                    : ColumnCaptions.Quantification, message)], message);
        }

        private static int IndexOfColumn(IList<string> captions, string caption, string message)
        {
            int index = captions.IndexOf(caption);
            Assert.AreNotEqual(-1, index,
                "No column captioned {0}. {1}", caption, message);
            return index;
        }

        private PeptideResult GetFirstPeptideResult(string docPath)
        {
            using var stream = File.OpenRead(docPath);
            var srmDocument = (SrmDocument)new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            var dataSchema = SkylineDataSchema.MemoryDataSchema(srmDocument, DataSchemaLocalizer.INVARIANT);
            return new Peptide(dataSchema, srmDocument.GetPathTo((int)SrmDocument.Level.Molecules, 0)).Results.Values.First();
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
        /// can afford to do. What it cannot do is wait while a SynchronizationContext that
        /// posts to this thread is installed: Parquet.Net's read path resumes on the caller's
        /// context, so the continuation is posted back to a thread which is blocked waiting
        /// for it, and neither ever runs. TestRunner's thread acquires such a context as soon
        /// as any earlier unit test constructs a WindowsForms control, and keeps it until a
        /// functional test happens to run, which is how this hung on TeamCity. Reading without
        /// one is load-bearing, not a precaution: an ordinary warm read is enough to deadlock.
        /// </summary>
        private static T ReadParquet<T>(string path, Func<ParquetReader, T> readFunc)
        {
            return ActionUtil.CallWithoutSynchronizationContext(() =>
            {
                using var reader = ParquetReader.CreateAsync(path).GetAwaiter().GetResult();
                return readFunc(reader);
            });
        }
    }
}
