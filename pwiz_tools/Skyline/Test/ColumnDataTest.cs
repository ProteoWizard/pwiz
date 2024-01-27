using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections.Transpositions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ColumnDataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestColumnDataForValues()
        {
            VerifyColumn(ColumnData.ForValues(Array.Empty<int>()));
            VerifyColumn(ColumnData.ForValues(new[]{1,2}));
            VerifyColumn(ColumnData.ForValues(new[]{1,1}));
            VerifyColumn(ColumnData.ForValues(new []{new object(), null}));
        }

        public static void VerifyColumn<T>(ColumnData<T> columnData)
        {
            if (columnData is ColumnData<T>.List list)
            {
                Assert.AreNotEqual(0, list.RowCount);
                Assert.AreNotEqual(1, list.RowCount);
                var uniqueValues = list.ToImmutableList().Distinct().ToList();
                Assert.AreNotEqual(0, uniqueValues.Count);
                Assert.AreNotEqual(1, uniqueValues.Count);
            }
        }
    }
}
