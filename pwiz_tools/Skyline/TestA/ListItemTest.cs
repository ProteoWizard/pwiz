using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class ListItemTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTypeBuilder()
        {
            var listItemTypes = ListItemTypes.INSTANCE;
            const string listName = "My&Li.s&/\\t  \t\r\n";
            var type = listItemTypes.GetListItemType(listName);
            Assert.IsNotNull(type);
//            var constructor =
//            Assert.IsNotNull(constructor);
//            var listItemId = new ListItemId(100);
//            var listData = new ListData(new ListDef(listName), new ColumnData[0]);
//            var instance = constructor.Invoke(new object[] {listData, listItemId});
//            Assert.IsNotNull(instance);
//            Assert.IsInstanceOfType(instance, typeof(ListItem));
//            Assert.AreNotEqual(typeof(ListItem), instance.GetType());
//            var listItem = (ListItem) instance;
//            Assert.AreEqual(listName, listItemTypes.GetListName(listItem.GetType()));
        }
    }
}
