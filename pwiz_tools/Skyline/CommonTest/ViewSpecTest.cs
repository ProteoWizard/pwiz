using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    /// <summary>
    /// Summary description for ViewSpecTest
    /// </summary>
    [TestClass]
    public class ViewSpecTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestViewSpecXml()
        {
            var viewSpecList =
                new ViewSpecList(new[]
                {
                    new ViewSpec().SetName("ViewName")
                        .SetColumns(new[]
                        {new ColumnSpec(PropertyPath.Root), new ColumnSpec(PropertyPath.Parse("A.B")),}),
                });
            var xmlSerializer = new XmlSerializer(typeof (ViewSpecList));
            var stream = new MemoryStream();
            xmlSerializer.Serialize(stream, viewSpecList);
            stream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(stream);
            Assert.AreEqual(viewSpecList, roundTrip);
        }
    }
}
