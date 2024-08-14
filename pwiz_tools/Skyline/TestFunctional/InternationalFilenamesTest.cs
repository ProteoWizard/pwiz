using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class InternationalFilenamesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInternationalFilenames()
        {
            TestFilesZip = @"TestFunctional\InternationalFilenamesTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            foreach (var testFilename in new[]
                     {
                         "Arabicعربي",
                         "English",
                         "FrenchFrançais",
                         "Japanese日本語",
                         "SimplifiedChinese简体中文",
                         "TraditionalChinese繁體中文",
                         "TurkishTürkçe",
                     })
            {
                RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("InternationalFilenamesTest.sky")));
                WaitForDocumentLoaded();
                var documentPath = TestFilesDir.GetTestPath(testFilename + ".sky");
                RunUI(()=>
                {
                    SkylineWindow.SaveDocument(documentPath);
                });
                AssertFileExists(documentPath);
                AssertFileExists(TestFilesDir.GetTestPath(testFilename + ".blib"));
                var sharedFilePath = TestFilesDir.GetTestPath(testFilename + ".sky.zip");
                RunUI(()=>
                {
                    SkylineWindow.ShareDocument(sharedFilePath, ShareType.COMPLETE);
                });
                AssertFileExists(sharedFilePath);
                RunUI(()=>SkylineWindow.OpenSharedFile(sharedFilePath));
                WaitForDocumentLoaded();
                AssertFileExists(Path.Combine(TestFilesDir.GetTestPath(testFilename), testFilename + ".sky"));
                AssertFileExists(Path.Combine(TestFilesDir.GetTestPath(testFilename), testFilename + ".blib"));
                var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.FirstOrDefault();
                Assert.IsNotNull(library);
                Assert.AreNotEqual(0, library.FileCount);
            }
        }

        private static void AssertFileExists(string path)
        {
            Assert.IsTrue(File.Exists(path), "File {0} does not exist", path);
        }
    }
}
