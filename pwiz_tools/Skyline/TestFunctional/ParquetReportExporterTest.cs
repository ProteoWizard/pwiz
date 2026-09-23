/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using pwiz.Common.DataBinding;
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
            var nullableDateTime = new ParquetReportExporter.StorageType(typeof(DateTime?)).ConvertValue(DateTime.UtcNow);
            Assert.IsInstanceOfType(nullableDateTime, typeof(DateTime));
            var nullableFloat = new ParquetReportExporter.StorageType(typeof(float?)).ConvertValue(1f);
            Assert.IsInstanceOfType(nullableFloat, typeof(float?));
        }

        [TestMethod]
        public void TestParquetArrays()
        {
            var items = new[]{Array.Empty<float>(), new float[1], null }.Select(array=>new MyObject(){FloatArray = new FormattableList<float>(array)}).ToList();
            var viewSpec = new ViewSpec().SetColumns(new[]
            {
                new ColumnSpec(PropertyPath.Root.Property(nameof(MyObject.FloatArray)))
            });
            var stream = new MemoryStream();
            var dataSchema = SkylineDataSchema.MemoryDataSchema(new SrmDocument(SrmSettingsList.GetDefault()),
                DataSchemaLocalizer.INVARIANT);
            var viewInfo = new ViewInfo(dataSchema, typeof(MyObject), viewSpec);
            var rowItemExporter = new ParquetReportExporter();
            IProgressStatus status = new ProgressStatus();
            RowFactories.ExportReport(CancellationToken.None, stream, viewInfo, null, new StaticRowSource(items),
                rowItemExporter, new SilentProgressMonitor(), ref status);
            stream.Position = 0;
            // Parquet.Net's reader resumes on the caller's SynchronizationContext, and this
            // test runs on the thread which every other test in the process shares, so it
            // reads without one.
            ActionUtil.CallWithoutSynchronizationContext(() =>
            {
                using var reader = ParquetReader.CreateAsync(stream).GetAwaiter().GetResult();
                Assert.AreEqual(1, reader.Schema.Fields.Count);
                // Exercise the data-read path so an array/list write or decode regression
                // would surface here instead of only in downstream consumers.
                using var groupReader = reader.OpenRowGroupReader(0);
                var dataField = reader.Schema.GetDataFields().Single();
                var col = groupReader.ReadColumnAsync(dataField).GetAwaiter().GetResult();
                Assert.IsNotNull(col.Data);
                return true;
            });
            VerifyWriterExceptionPropagates(viewInfo, items);
        }

        /// <summary>
        /// An exception on the writer thread, such as a full disk, has to come back to the caller
        /// as the original exception instead of hanging the export waiting for the writer.
        /// </summary>
        private void VerifyWriterExceptionPropagates(ViewInfo viewInfo, IList<MyObject> items)
        {
            IProgressStatus status = new ProgressStatus();
            AssertEx.ThrowsException<IOException>(() => RowFactories.ExportReport(CancellationToken.None,
                new FailAfterMagicStream(), viewInfo, null, new StaticRowSource(items), new ParquetReportExporter(),
                new SilentProgressMonitor(), ref status));
        }

        /// <summary>
        /// Accepts the "PAR1" magic which creating the ParquetWriter writes, and fails the first
        /// write after that, which is the first row group.
        /// </summary>
        private class FailAfterMagicStream : MemoryStream
        {
            private const int MAGIC_LENGTH = 4;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Length + count > MAGIC_LENGTH)
                {
                    throw new IOException(@"Simulated disk full");
                }
                base.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(buffer, offset, count);
                return Task.CompletedTask;
            }
        }

        class MyObject
        {
            public FormattableList<float> FloatArray
            {
                get;
                set;
            }
        }
    }
}
