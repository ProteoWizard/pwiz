using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Dialect.Schema;
using Parquet;
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
            var items = new[]{Array.Empty<float>(), new float[1], null }.Select(array=>new MyObject(){FloatArray = new FormattableList<float>(array)}).ToList();
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
            var table = ParquetReader.ReadTableFromStream(stream);
            Assert.AreEqual(1, table.Schema.Fields.Count);
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
