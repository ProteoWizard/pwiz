/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;
using System;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>The formats that "--report-format" can name on the command-line.</summary>
    public enum ReportFormat { csv, tsv, parquet }

    public static class ReportExporters
    {
        /// <summary>
        /// Returns a IReportExporter appropriate for the specified filename extension.
        /// </summary>
        /// <param name="dataSchemaLocalizer">determines whether to override number formatting with <see cref="Formats.RoundTrip"/>.</param>
        /// <param name="extensionsToTry">Typically a list of the following extensions:
        /// <list type="number">
        /// <item><description>The filename extension of the file being saved</description></item>
        /// <item><description>The option chosen in the "Save as type" dropdown in the SaveFileDialog</description></item>
        /// <item><description>The default extension if no match for the previous ones</description></item>
        /// </list></param>
        public static IReportExporter ForFilenameExtension(DataSchemaLocalizer dataSchemaLocalizer, params string[] extensionsToTry)
        {
            return ForFormat(dataSchemaLocalizer, FormatForFilenameExtension(extensionsToTry));
        }

        /// <summary>
        /// The format <see cref="ForFilenameExtension"/> would use. Callers that have to know
        /// the format before they can choose a <see cref="DataSchemaLocalizer"/> need this,
        /// since the localizer is what <see cref="ForFilenameExtension"/> takes.
        /// </summary>
        public static ReportFormat FormatForFilenameExtension(params string[] extensionsToTry)
        {
            foreach (var extension in extensionsToTry)
            {
                if (TextUtil.EXT_CSV.Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return ReportFormat.csv;
                }

                if (TextUtil.EXT_PARQUET.Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return ReportFormat.parquet;
                }
            }

            return ReportFormat.tsv;
        }

        public static IReportExporter ForSeparator(DataSchemaLocalizer dataSchemaLocalizer, char separator)
        {
            return new ReplicatePivotDsvReportExporter(CreateDsvWriter(dataSchemaLocalizer, separator));
        }

        /// <summary>
        /// Returns an IReportExporter for an explicitly requested format, ignoring the
        /// extension of the file being written.
        /// </summary>
        public static IReportExporter ForFormat(DataSchemaLocalizer dataSchemaLocalizer, ReportFormat reportFormat)
        {
            switch (reportFormat)
            {
                case ReportFormat.csv:
                    // The localizer's separator, not TextUtil.CsvSeparator, so that
                    // --report-invariant gets a comma whatever the OS culture is, and
                    // --report-format=csv agrees with exporting to a .csv file name.
                    return new ReplicatePivotDsvReportExporter(CreateDsvWriter(dataSchemaLocalizer,
                        TextUtil.GetCsvSeparator(dataSchemaLocalizer.FormatProvider)));
                case ReportFormat.tsv:
                    return new ReplicatePivotDsvReportExporter(CreateDsvWriter(dataSchemaLocalizer, TextUtil.SEPARATOR_TSV));
                case ReportFormat.parquet:
                    return new ParquetReportExporter();
                default:
                    throw new ArgumentOutOfRangeException(nameof(reportFormat));
            }
        }

        private static DsvWriter CreateDsvWriter(DataSchemaLocalizer dataSchemaLocalizer, char separator)
        {
            var dsvWriter = new DsvWriter(dataSchemaLocalizer.FormatProvider, dataSchemaLocalizer.Language, separator);
            if (ReferenceEquals(dataSchemaLocalizer, DataSchemaLocalizer.INVARIANT))
            {
                dsvWriter.NumberFormatOverride = Formats.RoundTrip;
            }

            return dsvWriter;
        }
    }
}
