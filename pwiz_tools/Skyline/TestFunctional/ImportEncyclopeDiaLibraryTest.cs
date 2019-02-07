using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportEncyclopeDiaLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportEncyclopeDiaLibrary()
        {
            TestFilesZip = @"TestFunctional\ImportEncyclopeDiaLibraryTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("ImportLibraryTest.sky")));
        }
    }
}
