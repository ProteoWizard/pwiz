using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class OnDemandFeatureCalculatorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestForcedIntegrationForUserIntegratedPeaks()
        {
            using (var testFilesDir = new TestFilesDir(TestContext, @"Test\OnDemandFeatureCalculatorTest.zip"))
            {
                SrmDocument document;
                using (var stream = File.OpenRead(testFilesDir.GetTestPath("OnDemandFeatureCalculatorTest.sky")))
                {
                    document = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                }

                var chromatogramCache = ChromatogramCache.Load(
                    testFilesDir.GetTestPath("OnDemandFeatureCalculatorTest.skyd"), new ProgressStatus(),
                    new DefaultFileLoadMonitor(new SilentProgressMonitor()), false);

            }
        }
    }
}
