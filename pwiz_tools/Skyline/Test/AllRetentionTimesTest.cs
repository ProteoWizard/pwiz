using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AllRetentionTimesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestAllRetentionTimesAlignment()
        {
            var fileTargetMatrix = new FileTargetMatrix<ImmutableList<double>>(new[]
                {
                    new RetentionTimeSource("File1", "library"),
                    new RetentionTimeSource("File2", "library")
                },
                new[] { new Target("ELVIS"), new Target("LIVES"), new Target("ELVISLIVES") },
                new []
                {
                    ImmutableList.ValueOf(new []{ImmutableList.Singleton(1.0), ImmutableList.Singleton(2.0)}),
                    ImmutableList.ValueOf(new []{ ImmutableList.Empty<double>(), ImmutableList.Singleton(4.0), }),
                    ImmutableList.ValueOf(new []{ImmutableList.Singleton(3.0), ImmutableList.Singleton(6.0)})
                }
            );
            var allRetentionTimes = new AllRetentionTimes(fileTargetMatrix);
            var alignedTimes =
                allRetentionTimes.GetAlignedRetentionTimes(new Target("ELVIS"), new MsDataFilePath("File1")).ToArray();
            const double delta = 1e-8;
            Assert.AreEqual(1, alignedTimes.Length);
            Assert.AreEqual(1.0, alignedTimes[0], delta);
            var alignedTimes2 = allRetentionTimes.GetAlignedRetentionTimes(new Target("LIVES"), new MsDataFilePath("File1")).ToArray();
            Assert.AreEqual(1, alignedTimes.Length);
            Assert.AreEqual(2.0, alignedTimes2[0], delta);
        }
    }
}
