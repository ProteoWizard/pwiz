using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Parquet;
using Parquet.Schema;
using Parquet.Serialization;
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
            var items = new[]
            {
                new FormattableList<float>(Array.Empty<float>()),
                new FormattableList<float>(new float[1]),
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
            var listField = reader.Schema.Fields[0] as ListField;
            Assert.IsNotNull(listField, "Schema field should be a ListField");

            var dataColumn = rowGroupReader.ReadColumnAsync(listField.Item).Result;

            // Verify definition levels distinguish empty vs null arrays
            // Row 0: empty array (def level 1) - list exists but no elements
            // Row 1: array with one element (def level 2) - list exists and element is non-null
            // Row 2: null array (def level 0) - list is null
            Assert.AreEqual(3, dataColumn.DefinitionLevels.Length);
            Assert.AreEqual(1, dataColumn.DefinitionLevels[0], "Empty array should have definition level 1");
            Assert.AreEqual(2, dataColumn.DefinitionLevels[1], "Array with element should have definition level 2");
            Assert.AreEqual(0, dataColumn.DefinitionLevels[2], "Null array should have definition level 0");
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
