using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromGroupHeaderInfoTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestChromGroupHeaderInfoSizeOf()
        {
            Assert.AreEqual(ChromGroupHeaderInfo.SizeOf, Marshal.SizeOf<ChromGroupHeaderInfo>());
            Assert.AreEqual(ChromGroupHeaderInfo.SizeOf, ChromGroupHeaderInfo.GetStructSize(CacheFormatVersion.CURRENT));
        }
    }
}
