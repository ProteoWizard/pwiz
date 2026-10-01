/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ParquetReportExporterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestConvertToStorageType()
        {
            // Boxing a Nullable<T> with HasValue produces a boxed T, so assert
            // against the underlying value type rather than the nullable wrapper.
            var nullableDateTime = ParquetReportExporter.ConvertToStorageType(DateTime.UtcNow, typeof(DateTime?));
            Assert.IsInstanceOfType(nullableDateTime, typeof(DateTime));
            var nullableFloat = ParquetReportExporter.ConvertToStorageType(1f, typeof(float?));
            Assert.IsInstanceOfType(nullableFloat, typeof(float?));
        }

        [TestMethod]
        public void TestParquetArrays()
        {
            var items = new[]{Array.Empty<float>(), new float[1], null }.Select(array=>new MyObject(){FloatArray = new FormattableList<float>(array)}).ToList();
            var stream = ExportReport(items, nameof(MyObject.FloatArray));
            ReadParquet(stream, reader =>
            {
                Assert.AreEqual(1, reader.Schema.Fields.Count);
                // Exercise the data-read path so an array/list write or decode regression
                // would surface here instead of only in downstream consumers.
                using var groupReader = reader.OpenRowGroupReader(0);
                var dataField = reader.Schema.GetDataFields().Single();
                using var col = groupReader.ReadRawColumnDataBaseAsync(dataField).GetAwaiter().GetResult();
                // One level per row: the empty list, the one element list and the null list
                CollectionAssert.AreEqual(new[] { 0, 0, 0 }, col.RepetitionLevels.ToArray());
                CollectionAssert.AreEqual(new[] { 1, 3, 0 }, col.DefinitionLevels.ToArray());
            });
        }

        [TestMethod]
        public void TestParquetTimestamps()
        {
            // Every DateTimeKind with the same digits, which are whole milliseconds
            // because that is the precision written.
            var ticks = new DateTime(2026, 3, 15, 9, 30, 0, 123).Ticks;
            var kinds = new[] { DateTimeKind.Unspecified, DateTimeKind.Local, DateTimeKind.Utc };
            var dateTimes = kinds.Select(kind => (DateTime?)new DateTime(ticks, kind)).Append(null).ToArray();
            var items = dateTimes.Select(dateTime => new TimestampObject
            {
                WallClockTime = dateTime,
                MomentTime = dateTime
            }).ToList();
            var stream = ExportReport(items, nameof(TimestampObject.WallClockTime), nameof(TimestampObject.MomentTime));
            ReadParquet(stream, reader =>
            {
                using var groupReader = reader.OpenRowGroupReader(0);
                // Wall-clock times have no zone, so the digits are written unchanged
                // whatever their Kind, and the output is the same on every computer.
                var wallClockTimes = ReadTimestampColumn(reader, groupReader,
                    nameof(TimestampObject.WallClockTime), false);
                // Moments in time are stored as UTC, so only a local time is converted.
                var momentTimes = ReadTimestampColumn(reader, groupReader,
                    nameof(TimestampObject.MomentTime), true);
                for (int i = 0; i < dateTimes.Length; i++)
                {
                    var expectedMoment = dateTimes[i]?.Kind == DateTimeKind.Local
                        ? dateTimes[i].Value.ToUniversalTime()
                        : dateTimes[i];
                    AssertEx.AreEqual(dateTimes[i]?.Ticks, wallClockTimes[i]?.Ticks);
                    AssertEx.AreEqual(expectedMoment?.Ticks, momentTimes[i]?.Ticks);
                }
            });
        }

        private static MemoryStream ExportReport<T>(IList<T> items, params string[] propertyNames)
        {
            var viewSpec = new ViewSpec().SetColumns(propertyNames.Select(name =>
                new ColumnSpec(PropertyPath.Root.Property(name))));
            var stream = new MemoryStream();
            var dataSchema = SkylineDataSchema.MemoryDataSchema(new SrmDocument(SrmSettingsList.GetDefault()),
                DataSchemaLocalizer.INVARIANT);
            var viewInfo = new ViewInfo(dataSchema, typeof(T), viewSpec);
            var rowItemExporter = new ParquetReportExporter();
            IProgressStatus status = new ProgressStatus();
            RowFactories.ExportReport(CancellationToken.None, stream, viewInfo, null, new StaticRowSource(items),
                rowItemExporter, new SilentProgressMonitor(), ref status);
            stream.Position = 0;
            return stream;
        }

        private static void ReadParquet(Stream stream, Action<ParquetReader> check)
        {
            // Parquet.Net's reader resumes on the caller's SynchronizationContext, and this
            // test runs on the thread which every other test in the process shares, so it
            // reads without one. The reader holds nothing of its own over a caller's stream,
            // so it is not disposed.
            ActionUtil.CallWithoutSynchronizationContext(() =>
            {
                check(ParquetReader.CreateAsync(stream).GetAwaiter().GetResult());
            });
        }

        private static DateTime?[] ReadTimestampColumn(ParquetReader reader, ParquetRowGroupReader groupReader,
            string name, bool isAdjustedToUtc)
        {
            var field = reader.Schema.GetDataFields().Single(f => f.Name == name);
            var dateTimeField = field as DateTimeDataField;
            Assert.IsNotNull(dateTimeField);
            AssertEx.AreEqual(DateTimeFormat.Timestamp, dateTimeField.DateTimeFormat);
            AssertEx.AreEqual(isAdjustedToUtc, dateTimeField.IsAdjustedToUTC);
            var values = new DateTime?[groupReader.RowCount];
            groupReader.ReadAsync(field, values.AsMemory()).GetAwaiter().GetResult();
            return values;
        }

        class MyObject
        {
            public FormattableList<float> FloatArray
            {
                get;
                set;
            }
        }

        class TimestampObject
        {
            public DateTime? WallClockTime { get; set; }
            [UtcTimestamp]
            public DateTime? MomentTime { get; set; }
        }
    }
}
