using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;

namespace CommonTest.DataBinding
{
    /// <summary>
    /// Summary description for AttributeTest
    /// </summary>
    [TestClass]
    public class AttributeTest
    {
        [TestMethod]
        public void TestChildDisplayName()
        {
            var dataSchema = new DataSchema();
            var coldescRoot = new ColumnDescriptor(dataSchema, typeof (RowValue));
            var coldescRetentionTime = new ColumnDescriptor(coldescRoot, "RetentionTime");
            var coldescMinRetentionTime = new ColumnDescriptor(coldescRetentionTime, "Min");
            var coldescMeanRetentionTime = new ColumnDescriptor(coldescRetentionTime, "Mean");
            Assert.AreEqual("MinRetentionTime", dataSchema.GetDisplayName(coldescMinRetentionTime));
            Assert.AreEqual("AverageRetentionTime", dataSchema.GetDisplayName(coldescMeanRetentionTime));
            var coldescParent = new ColumnDescriptor(coldescRoot, "Parent");
            var coldescParentRetentionTime = new ColumnDescriptor(coldescParent, "RetentionTime");
            var coldescParentMeanRetentionTime = new ColumnDescriptor(coldescParentRetentionTime, "Mean");
            Assert.AreEqual("Parent", dataSchema.GetDisplayName(coldescParent));
            Assert.AreEqual("ParentRetentionTime", dataSchema.GetDisplayName(coldescParentRetentionTime));
            Assert.AreEqual("ParentAverageRetentionTime", dataSchema.GetDisplayName(coldescParentMeanRetentionTime));
        }

        class Stats
        {
            public double Min { get; set; }
            public double Max { get; set; }
            [DisplayName("Average")]
            public double Mean { get; set; }
        }
        class RowValue
        {
            [ChildDisplayName(Format = "{0}RetentionTime")]
            public Stats RetentionTime { get; set; }
            [ChildDisplayName(Format = "{0}Area")]
            public Stats Area { get; set; }
            [ChildDisplayName(Format = "Parent{0}")]
            public RowValue Parent { get; set; }
        }
    }
}
