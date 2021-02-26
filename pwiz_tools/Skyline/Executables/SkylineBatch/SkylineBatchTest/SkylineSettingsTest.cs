using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{


    [TestClass]
    public class SkylineSettingsTest
    {
        [TestMethod]
        public void TestSkylineVersionComparison()
        {
            var skylineSettings = TestUtils.GetTestSkylineSettings();
            Assert.IsTrue(skylineSettings.HigherVersion("0.100000.100.10000"),
                "Expected the developer version of Skyline to be higher than 0.100000.100.10000");
            Assert.IsTrue(skylineSettings.HigherVersion(CommandWriter.ALLOW_NEWLINE_SAVE_VERSION),
                "Expected the developer version of Skyline to be higher than " + CommandWriter.ALLOW_NEWLINE_SAVE_VERSION);
            Assert.IsTrue(!skylineSettings.HigherVersion("10000.0.0.0"),
                "Expected the developer version of Skyline to be lower than 10000.0.0.0");
        }
    }
}
