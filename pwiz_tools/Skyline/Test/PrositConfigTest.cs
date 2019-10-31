using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PrositConfigTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPrositConfigSerialization()
        {
            var prositConfig = new PrositConfig()
            {
                Server = "myhost",
                ClientCertificate = @"-----BEGIN CERTIFICATE-----
Stuff
-----END CERTIFICATE-----
",
                ClientKey = @"-----BEGIN RSA PRIVATE KEY-----
Other Stuff
-----END RSA PRIVATE KEY-----
",
                RootCertificate = @"-----BEGIN CERTIFICATE-----
Even More Stuff
-----END CERTIFICATE-----
"

            };
            var xmlSerializer = new XmlSerializer(typeof(PrositConfig));
            var myStream = new MemoryStream();
            xmlSerializer.Serialize(myStream, prositConfig);
            myStream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(myStream);
            Assert.IsInstanceOfType(roundTrip, typeof(PrositConfig));
            TestContext.WriteLine(Encoding.UTF8.GetString(myStream.ToArray()));
        }
        [TestMethod]
        public void TestGetPrositConfig()
        {
            var prositConfig = PrositConfig.GetPrositConfig();
            Assert.IsNotNull(prositConfig);
            Assert.IsFalse(string.IsNullOrEmpty(prositConfig.Server));
        }
    }
}
