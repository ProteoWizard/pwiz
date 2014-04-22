using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    /// <summary>
    /// Summary description for AttributeTest
    /// </summary>
    // ReSharper disable LocalizableElement
    [TestClass]
    public class AttributeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingChildDisplayName()
        {
            var dataSchema = new DataSchema();
            var coldescRoot = ColumnDescriptor.RootColumn(dataSchema, typeof (RowValue));
            var coldescRetentionTime = coldescRoot.ResolveChild("RetentionTime");
            var coldescMinRetentionTime = coldescRetentionTime.ResolveChild("Min");
            var coldescMeanRetentionTime = coldescRetentionTime.ResolveChild("Mean");
            Assert.AreEqual("MinRetentionTime", dataSchema.GetColumnCaption(coldescMinRetentionTime).InvariantCaption);
            Assert.AreEqual("AverageRetentionTime", dataSchema.GetColumnCaption(coldescMeanRetentionTime).InvariantCaption);
            var coldescParent = coldescRoot.ResolveChild("Parent");
            var coldescParentRetentionTime = coldescParent.ResolveChild("RetentionTime");
            var coldescParentMeanRetentionTime = coldescParentRetentionTime.ResolveChild("Mean");
            Assert.AreEqual("Parent", dataSchema.GetColumnCaption(coldescParent).InvariantCaption);
            Assert.AreEqual("ParentRetentionTime", dataSchema.GetColumnCaption(coldescParentRetentionTime).InvariantCaption);
            Assert.AreEqual("ParentAverageRetentionTime", dataSchema.GetColumnCaption(coldescParentMeanRetentionTime).InvariantCaption);
        }

        class Stats
        {
            public double Min { get; set; }
            public double Max { get; set; }
            [InvariantDisplayName("Average")]
            public double Mean { get; set; }
        }
        class RowValue
        {
            [ChildDisplayName("{0}RetentionTime")]
            public Stats RetentionTime { get; set; }
            [ChildDisplayName("{0}Area")]
            public Stats Area { get; set; }
            [ChildDisplayName("Parent{0}")]
            public RowValue Parent { get; set; }
        }
    }
}
