using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class SpectrumListTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSpectrumList()
        {
            // var spectrumList = SpectrumList.ReadSpectrumList(CancellationToken.None,
            //     new MsDataFilePath(@"D:\bugs\lheil2\ms3\20220228_LRH_HeLa_Method0_MS3_1.raw"));
            // Assert.AreNotEqual(0, spectrumList.SpectrumMetadatas.Count);
        }
    }
}
