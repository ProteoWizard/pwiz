using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;

namespace CommonTest.DataBinding
{
    /// <summary>
    /// Summary description for AttributeTest
    /// </summary>
    // ReSharper disable LocalizableElement
    [TestClass]
    public class AttributeTest
    {
        [TestMethod]
        public void TestDataBindingChildDisplayName()
        {
            var dataSchema = new DataSchema();
            var coldescRoot = ColumnDescriptor.RootColumn(dataSchema, typeof (RowValue));
            var coldescRetentionTime = coldescRoot.ResolveChild("RetentionTime");
            var coldescMinRetentionTime = coldescRetentionTime.ResolveChild("Min");
            var coldescMeanRetentionTime = coldescRetentionTime.ResolveChild("Mean");
            Assert.AreEqual("MinRetentionTime", dataSchema.GetDisplayName(coldescMinRetentionTime));
            Assert.AreEqual("AverageRetentionTime", dataSchema.GetDisplayName(coldescMeanRetentionTime));
            var coldescParent = coldescRoot.ResolveChild("Parent");
            var coldescParentRetentionTime = coldescParent.ResolveChild("RetentionTime");
            var coldescParentMeanRetentionTime = coldescParentRetentionTime.ResolveChild("Mean");
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
