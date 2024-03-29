using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ErrorIfPersistentFilesDirModifiedTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void TestErrorIfPersistentFilesDirModified()
        {
            TestFilesZip = "https://skyline.ms/tutorials/OptimizeCEMzml.zip";
            TestFilesPersistent = new[] { "CE_Vantage" };
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, TestContext.TestName, TestFilesPersistent);
            string persistentFile = TestFilesDir.GetTestPath("OptimizeCEMzml/CE_Vantage_15mTorr_0001.mzML");
            string persistentFileCopy = persistentFile + ".copy";
            File.Copy(persistentFile, persistentFileCopy);
            try
            {
                AssertEx.ThrowsException<IOException>(() => TestFilesDir.Cleanup(),
                    ex => StringAssert.Contains(ex.Message, $"PersistentFilesDir ({TestFilesDir.PersistentFilesDir}) has been modified"));
            }
            finally
            {
                File.Delete(persistentFileCopy);
            }
        }
    }
}
