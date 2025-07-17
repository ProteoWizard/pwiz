using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class UriBuilderTest : AbstractUnitTest
    {
        private const string VALID_HOST = "https://ardia-core-int.cmdtest.thermofisher.com";
        private const string PATH_1 = "/app/data-explorer";
        private const string PATH_2 = "folders/8935b4af-015c-4ece-82a5-a812cd5a1626";

        [TestMethod]
        public void TestUriFromParts()
        {
            var host = new Uri(VALID_HOST);
            Assert.AreEqual(VALID_HOST + PATH_1, ArdiaClient.UriFromParts(host, PATH_1).AbsoluteUri);
            Assert.AreEqual(VALID_HOST + PATH_1 + "/" + PATH_2, ArdiaClient.UriFromParts(host, PATH_1, PATH_2).AbsoluteUri);


        }

        [TestMethod]
        public void TestUriWithParams()
        {
            var host = new Uri(VALID_HOST);
            var queryParams = new Dictionary<string, string> { ["param 1"] = "value 1" };
            Assert.AreEqual(VALID_HOST + PATH_1 + $"?{Uri.EscapeDataString("param 1")}={Uri.EscapeDataString("value 1")}",
                ArdiaClient.UriWithParams(ArdiaClient.UriFromParts(host, PATH_1), queryParams).AbsoluteUri);
            queryParams.Add("param 2", "value 2");
            Assert.AreEqual(
                VALID_HOST + PATH_1 +
                $"?{Uri.EscapeDataString("param 1")}={Uri.EscapeDataString("value 1")}&{Uri.EscapeDataString("param 2")}={Uri.EscapeDataString("value 2")}",
                ArdiaClient.UriWithParams(ArdiaClient.UriFromParts(host, PATH_1), queryParams).AbsoluteUri);
        }
    }
}
