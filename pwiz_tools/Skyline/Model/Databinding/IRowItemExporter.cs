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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;
using System;
using System.IO;

namespace pwiz.Skyline.Model.Databinding
{
    public interface IRowItemExporter
    {
        void Export(IProgressMonitor progressMonitor, ref IProgressStatus status, Stream stream,
            RowItemEnumerator rowItemEnumerator);
    }

    public static class RowItemExporters
    {
        public static IRowItemExporter Create(DataSchemaLocalizer dataSchemaLocalizer, string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (".csv".Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                var separator = TextUtil.GetCsvSeparator(dataSchemaLocalizer.FormatProvider);
                return new RowItemExporter(dataSchemaLocalizer, CreateDsvWriter(dataSchemaLocalizer, separator));
            }

            if (".parquet".Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return new ParquetRowItemExporter();
            }

            return new RowItemExporter(dataSchemaLocalizer, CreateDsvWriter(dataSchemaLocalizer, TextUtil.SEPARATOR_TSV));
        }

        public static DsvWriter CreateDsvWriter(DataSchemaLocalizer dataSchemaLocalizer, char separator)
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
