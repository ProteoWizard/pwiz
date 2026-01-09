using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ParquetRowItemExporterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestConvertToStorageType()
        {
            var dateTimeOffset = ParquetRowItemExporter.ConvertToStorageType(DateTime.UtcNow, typeof(DateTimeOffset));
            Assert.IsInstanceOfType(dateTimeOffset, typeof(DateTimeOffset));
            var nullableFloat = ParquetRowItemExporter.ConvertToStorageType(1f, typeof(float?));
            Assert.IsInstanceOfType(nullableFloat, typeof(float?));
        }

        [TestMethod]
        public void TestParquetArrays()
        {
            var floats = new[] { 5.3f };
            var items = new[]
            {
                new FormattableList<float>(Array.Empty<float>()),
                new FormattableList<float>(floats),
                null
            }.Select(array=>new MyObject{FloatArray = array}).ToList();
            var viewSpec = new ViewSpec().SetColumns(new[]
            {
                new ColumnSpec(PropertyPath.Root.Property(nameof(MyObject.FloatArray)))
            });
            var stream = new MemoryStream();
            var dataSchema = SkylineDataSchema.MemoryDataSchema(new SrmDocument(SrmSettingsList.GetDefault()),
                DataSchemaLocalizer.INVARIANT);
            var viewInfo = new ViewInfo(dataSchema, typeof(MyObject), viewSpec);
            var rowItemExporter = new ParquetRowItemExporter();
            IProgressStatus status = new ProgressStatus();
            RowFactories.ExportReport(CancellationToken.None, stream, viewInfo, null, new StaticRowSource(items), rowItemExporter, new SilentProgressMonitor(), ref status);
            stream.Position = 0;

            // Read using the new ParquetReader API
            using var reader = ParquetReader.CreateAsync(stream).Result;
            Assert.AreEqual(1, reader.Schema.Fields.Count);

            // Read the row group
            using var rowGroupReader = reader.OpenRowGroupReader(0);
            var dataField = reader.Schema.Fields[0] as DataField;
            Assert.IsNotNull(dataField, "Schema field should be a DataField");
            Assert.IsTrue(dataField.IsArray, "DataField should be an array type");

            var dataColumn = rowGroupReader.ReadColumnAsync(dataField).Result;

            // With DataField array type, empty/null arrays produce no data elements
            // Only the non-empty array (row 1 with [5.3f]) contributes data
            Assert.AreEqual(1, dataColumn.Data.Length, "Should have 1 data element");
            Assert.AreEqual(floats[0], (float)dataColumn.Data.GetValue(0), "Data value should match");
            Assert.AreEqual(1, dataColumn.RepetitionLevels.Length, "Should have 1 rep level");
            Assert.AreEqual(0, dataColumn.RepetitionLevels[0], "Rep level 0 indicates start of list");
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
