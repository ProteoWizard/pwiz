/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
