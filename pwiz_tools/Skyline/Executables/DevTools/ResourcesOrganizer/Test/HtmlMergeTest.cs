using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class HtmlMergeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestHtmlMerge()
        {
            string folder = SaveManifestResourcesWithSubfolders(typeof(HtmlMergeTest), "en", "ja", "zh-CHS");
            var htmlFile = HtmlFile.ReadFolder(folder, "Skyline Data Independent Acquisition");
            Assert.IsNotNull(htmlFile);
        }
    }
}
