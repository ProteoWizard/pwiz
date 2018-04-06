using System;
using System.IO;
using System.Linq;
using System.Xml;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SaveAsPreviousVersionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSaveAsPreviousVersion()
        {
            TestFilesZip = @"TestFunctional\SaveAsPreviousVersionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_Plasma.sky")));
            WaitForDocumentLoaded();
            String v36Path = TestFilesDir.GetTestPath("Version36.sky.zip");
            RunUI(()=>SkylineWindow.ShareDocument(v36Path, ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.V3_6)));
            using (ZipFile zipFile = new ZipFile(v36Path))
            {
                ZipEntry zipEntryDocument = zipFile.Entries.First(entry => entry.FileName == "Rat_Plasma.sky");
                using (var stream = zipEntryDocument.OpenReader())
                {
                    using (var xmlReader = new XmlTextReader(stream))
                    {
                        var xmlDocument = new XmlDocument();
                        xmlDocument.Load(xmlReader);
                        Assert.IsNotNull(xmlDocument.DocumentElement);
                        Assert.AreEqual("3.6", xmlDocument.DocumentElement.GetAttribute("format_version"));
                    }
                }
                ZipEntry zipEntrySkyd = zipFile.Entries.First(entry => entry.FileName == "Rat_Plasma.skyd");
                using (var stream = zipEntrySkyd.OpenReader())
                {
                    var memoryStream = new MemoryStream();
                    CopyStreamTo(stream, memoryStream);
                    CacheHeaderStruct cacheHeader = CacheHeaderStruct.Read(memoryStream);
                    Assert.AreEqual(CacheFormatVersion.Eleven, cacheHeader.formatVersion);
                }
            }
        }

        private static void CopyStreamTo(Stream input, Stream output)
        {
            byte[] buffer = new byte[65536];
            int bytesRead;
            while (0 != (bytesRead = input.Read(buffer, 0, buffer.Length)))
            {
                output.Write(buffer, 0, bytesRead);
            }
        }
    }
}
