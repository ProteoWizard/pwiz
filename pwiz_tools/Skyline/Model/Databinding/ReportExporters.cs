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
            foreach (var extension in extensionsToTry)
            {
                if (TextUtil.EXT_CSV.Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    var separator = TextUtil.GetCsvSeparator(dataSchemaLocalizer.FormatProvider);
                    return new DsvReportExporter(CreateDsvWriter(dataSchemaLocalizer, separator));
                }

                if (TextUtil.EXT_PARQUET.Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return new ParquetReportExporter();
                }
            }

            return new DsvReportExporter(CreateDsvWriter(dataSchemaLocalizer, TextUtil.SEPARATOR_TSV));
        }

        public static IReportExporter ForSeparator(DataSchemaLocalizer dataSchemaLocalizer, char separator)
        {
            return new ReplicatePivotDsvReportExporter(CreateDsvWriter(dataSchemaLocalizer, separator));
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
