using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromatogramSetTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSerializeChromatogramImportTime()
        {
            var chromatogramSet = new ChromatogramSet("Test", new MsDataFileUri[] { MsDataFilePath.EMPTY });
            chromatogramSet.ChangeMSDataFileInfos(new[] { chromatogramSet.MSDataFileInfos[0].ChangeImportTime(DateTime.UtcNow) });
            var roundTrip = AssertEx.RoundTrip(chromatogramSet);
            Assert.AreEqual(chromatogramSet.MSDataFileInfos[0].ImportTime, roundTrip.MSDataFileInfos[0].ImportTime);
        }
    }
}
