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
            using var reader = ParquetReader.CreateAsync(stream).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.AreEqual(1, reader.Schema.Fields.Count);
            // Exercise the data-read path so an array/list write or decode regression
            // would surface here instead of only in downstream consumers.
            using var groupReader = reader.OpenRowGroupReader(0);
            var dataField = reader.Schema.GetDataFields().Single();
            var col = groupReader.ReadColumnAsync(dataField).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.IsNotNull(col.Data);
        }

        /// <summary>
        /// ParquetReportExporter waits on an async-only API from the thread it is called on,
        /// which deadlocks if the continuation is posted back to that thread while it is
        /// blocked. That does not happen because Parquet.Net does not resume on the caller's
        /// SynchronizationContext, so the exporter does nothing to defend against it. This is
        /// what makes that assumption safe to rely on: it installs a context and fails if the
        /// export ever posts to it, which is the signal to stop relying on it.
        /// </summary>
        [TestMethod]
        public void TestParquetExportIgnoresSynchronizationContext()
        {
            var items = new[] { Array.Empty<float>(), new float[1], null }
                .Select(array => new MyObject { FloatArray = new FormattableList<float>(array) }).ToList();
            var viewSpec = new ViewSpec().SetColumns(new[]
            {
                new ColumnSpec(PropertyPath.Root.Property(nameof(MyObject.FloatArray)))
            });
            var dataSchema = SkylineDataSchema.MemoryDataSchema(new SrmDocument(SrmSettingsList.GetDefault()),
                DataSchemaLocalizer.INVARIANT);
            var viewInfo = new ViewInfo(dataSchema, typeof(MyObject), viewSpec);

            var context = new RecordingSynchronizationContext();
            var saveContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            var stream = new AsyncSuspendingStream();
            try
            {
                // Prove the context actually sees continuations scheduled from this thread.
                // Without this the test could pass because the detector is broken rather than
                // because the exporter behaves.
                AsyncSuspendingStream.CompleteOnAnotherThread(() => { })
                    .ContinueWith(ignored => { }, CancellationToken.None, TaskContinuationOptions.None,
                        TaskScheduler.FromCurrentSynchronizationContext())
                    .GetAwaiter().GetResult();
                Assert.AreNotEqual(0, context.PostCount,
                    "The SynchronizationContext never received a continuation even from a deliberate one, so this test cannot detect anything.");
                context.ResetPostCount();

                IProgressStatus status = new ProgressStatus();
                RowFactories.ExportReport(CancellationToken.None, stream, viewInfo, null,
                    new StaticRowSource(items), new ParquetReportExporter(), new SilentProgressMonitor(), ref status);
                Assert.AreNotEqual(0, stream.Length);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(saveContext);
            }

            Assert.AreNotEqual(0, stream.AsyncOperationCount,
                "The export never performed an asynchronous stream operation, so it had nothing to suspend on.");
            Assert.AreEqual(0, context.PostCount,
                "ParquetReportExporter posted {0} continuation(s) to the SynchronizationContext of the thread it was called on. This test lets them run, but a caller that is blocked waiting for the export would never run them, and the export would hang.",
                context.PostCount);
        }

        /// <summary>
        /// Counts continuations posted to it, and then runs them on the thread pool so that a
        /// failure is an assert rather than a hung test.
        /// </summary>
        private class RecordingSynchronizationContext : SynchronizationContext
        {
            private int _postCount;

            public int PostCount => _postCount;

            public void ResetPostCount()
            {
                Interlocked.Exchange(ref _postCount, 0);
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref _postCount);
                ThreadPool.QueueUserWorkItem(ignored => d(state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref _postCount);
                d(state);
            }
        }

        /// <summary>
        /// A stream whose asynchronous operations always finish on another thread. A
        /// MemoryStream completes them inline, which means the awaits never suspend and no
        /// continuation is ever posted, so the test would pass without proving anything.
        /// </summary>
        private class AsyncSuspendingStream : Stream
        {
            private readonly MemoryStream _memoryStream = new MemoryStream();
            private int _asyncOperationCount;

            public int AsyncOperationCount => _asyncOperationCount;

            private void RecordAsyncOperation()
            {
                Interlocked.Increment(ref _asyncOperationCount);
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => true;
            public override long Length => _memoryStream.Length;

            public override long Position
            {
                get => _memoryStream.Position;
                set => _memoryStream.Position = value;
            }

            public override void Flush()
            {
                _memoryStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _memoryStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _memoryStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _memoryStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _memoryStream.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                RecordAsyncOperation();
                return CompleteOnAnotherThread(() => _memoryStream.Write(buffer, offset, count));
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                RecordAsyncOperation();
                return CompleteOnAnotherThread(() => _memoryStream.Flush());
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                int read = 0;
                RecordAsyncOperation();
                return CompleteOnAnotherThread(() => read = _memoryStream.Read(buffer, offset, count))
                    .ContinueWith(ignored => read, cancellationToken, TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
            }

            /// <summary>
            /// Returns a task that is guaranteed not to be complete when it is returned, so that
            /// awaiting it always suspends.
            /// </summary>
            public static Task CompleteOnAnotherThread(Action action)
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                ThreadPool.QueueUserWorkItem(ignored =>
                {
                    Thread.Sleep(1);
                    try
                    {
                        action();
                        taskCompletionSource.SetResult(true);
                    }
                    catch (Exception exception)
                    {
                        taskCompletionSource.SetException(exception);
                    }
                });
                return taskCompletionSource.Task;
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
