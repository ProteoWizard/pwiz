using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lists;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ListDefTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSerializeListDef()
        {
            var listDef = new ListDef("my name")
                .ChangeDisplayProperty("prop1")
                .ChangeIdProperty("prop2")
                .ChangeProperties(new[]
                {
                    new AnnotationDef("ann1",
                        AnnotationDef.AnnotationTargetSet.OfValues(AnnotationDef.AnnotationTarget.peptide,
                            AnnotationDef.AnnotationTarget.precursor_result),
                        AnnotationDef.AnnotationType.text, new[] {"one", "two"}),
                });
            var roundTrip = RoundTrip(listDef);
            Assert.AreEqual(listDef, roundTrip);
        }

        [TestMethod]
        public void TestSerializeListData()
        {
            var listData = new ListData(new ListDef("my name")
                    .ChangeProperties(new[]
                    {
                        new AnnotationDef("prop1", AnnotationDef.AnnotationTargetSet.EMPTY,
                            AnnotationDef.AnnotationType.number, ImmutableList.Empty<string>()),
                        new AnnotationDef("prop2", AnnotationDef.AnnotationTargetSet.EMPTY,
                            AnnotationDef.AnnotationType.text, ImmutableList.Empty<string>()),
                        new AnnotationDef("prop3", AnnotationDef.AnnotationTargetSet.EMPTY,
                            AnnotationDef.AnnotationType.true_false, ImmutableList.Empty<string>()),
                    })
                , new ColumnData[]
                {
                    new ColumnData.Doubles(new double?[]{1.0, 2, null}),
                    new ColumnData.Strings(new[]{"one","two","three"}), 
                    new ColumnData.Booleans(new []{true, false, true}), 
                });
            var roundTrip = RoundTrip(listData);
            Assert.AreEqual(listData, roundTrip);
        }

        public static T RoundTrip<T>(T listDef)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            var stream = new MemoryStream();
            xmlSerializer.Serialize(stream, listDef);
            stream.Seek(0, SeekOrigin.Begin);
            var roundTrip = (T) xmlSerializer.Deserialize(stream);
            return roundTrip;
        }
    }
}
