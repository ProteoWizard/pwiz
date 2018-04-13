using System;
using System.IO;
using System.ServiceModel;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ListDataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestListDataExceptions()
        {
            var listDef = new ListDef("test").ChangeProperties(new[]
            {
                new AnnotationDef("text", AnnotationDef.AnnotationTargetSet.EMPTY, AnnotationDef.AnnotationType.text, null), 
                new AnnotationDef("number", AnnotationDef.AnnotationTargetSet.EMPTY, AnnotationDef.AnnotationType.number, null), 
                new AnnotationDef("true_false", AnnotationDef.AnnotationTargetSet.EMPTY, AnnotationDef.AnnotationType.true_false, null)
            });
            var columns = new ColumnData[]
            {
                new ColumnData.Strings(new[] {"one", "two", "three", "four"}),
                new ColumnData.Doubles(new double?[] {1, 2, 3, 4}),
                new ColumnData.Booleans(new[] {true, false, true, false}),
            };
            var listData = new ListData(listDef, columns);
            Assert.IsNotNull(listData.ChangeListDef(listDef.ChangeIdProperty("text"), null));
            Assert.IsNotNull(listData.ChangeListDef(listDef.ChangeIdProperty("number"), null));
            EnsureDetailException<ListExceptionDetail>(() => listData.ChangeListDef(listDef.ChangeIdProperty("true_false"), null));
            VerifySerialization(listData);
        }

        public void VerifySerialization(ListData listData)
        {
            var xmlSerializer = new XmlSerializer(typeof(ListData));
            var stream = new MemoryStream();
            xmlSerializer.Serialize(stream, listData);
            stream.Seek(0, SeekOrigin.Begin);
            var roundTrip = xmlSerializer.Deserialize(stream);
            Assert.AreEqual(listData, roundTrip);
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            document = document.ChangeSettings(
                document.Settings.ChangeDataSettings(
                document.Settings.DataSettings.ChangeListDefs(new[] {listData})));
            AssertEx.Serializable(document);
        }

        private static T EnsureDetailException<T>(Func<object> func)
        {
            return EnsureDetailException<T>(new Action(()=>func()));
        }

        private static T EnsureDetailException<T>(Action action)
        {
            try
            {
                action();
            }
            catch (CommonException ex)
            {
                var iCommonException = ex as ICommonException<T>;
                Assert.IsNotNull(iCommonException);
                return iCommonException.ExceptionDetail;
            }
            Assert.Fail("Exception expected");
            return default(T);
        }
    }
}
