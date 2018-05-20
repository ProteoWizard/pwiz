using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class TestUnifiPassword : AbstractUnitTest
    {
        /// <summary>
        /// Test which checks whether the environment variable "UNIFI_PASSWORD" is set.
        /// I am hoping this test fails on Team City since it will indicate that the server
        /// is set up for me to write my unifi tests in another branch.
        /// </summary>
        [TestMethod]
        public void TestUnifiPasswordEnvironmentVariable()
        {
            Assert.IsNull(Environment.GetEnvironmentVariable("UNIFI_PASSWORD"));
        }
    }
}
