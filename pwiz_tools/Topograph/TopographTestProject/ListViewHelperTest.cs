using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Controls;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for ListViewManagerTest
    /// </summary>
    [TestClass]
    public class ListViewHelperTest
    {
        [TestMethod]
        public void TestMoveItems()
        {
            Assert.IsTrue(new[] {1,3,2}
                .SequenceEqual(ListViewHelper.MoveItems(
                    Enumerable.Range(1,3), new[]{2}, true)));
            Assert.IsTrue(new[] {2,1,3}
                .SequenceEqual(ListViewHelper
                .MoveItems(Enumerable.Range(1,3), new[]{0}, false)));
        }
    }
}
