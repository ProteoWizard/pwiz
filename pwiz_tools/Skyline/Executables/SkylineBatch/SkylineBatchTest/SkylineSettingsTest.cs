using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkylineBatch;

namespace SkylineBatchTest
{


    [TestClass]
    public class SkylineSettingsTest
    {
        [TestMethod]
        public async Task TestSkylineVersionComparison()
        {
            var skylineSettings = TestUtils.GetTestSkylineSettings();

            Assert.IsTrue(await skylineSettings.HigherVersion("0.100000.100.10000"),
                "Expected the developer version of Skyline to be higher than 0.100000.100.10000");
            Assert.IsTrue(await skylineSettings.HigherVersion(ConfigRunner.ALLOW_NEWLINE_SAVE_VERSION),
                "Expected the developer version of Skyline to be higher than " + ConfigRunner.ALLOW_NEWLINE_SAVE_VERSION);
            Assert.IsTrue(!await skylineSettings.HigherVersion("10000.0.0.0"),
                "Expected the developer version of Skyline to be lower than 10000.0.0.0");
        }
    }
}
