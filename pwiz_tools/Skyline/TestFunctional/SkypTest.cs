using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkypTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSkyp()
        {
            TestFilesZipPaths = new[] { @"TestFunctional\SkypTest.zip"};
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestSkypValid();

            TestSkypGetNonExistentPath();

            TestSkypOpen();  
        }

        private void TestSkypOpen()
        {
            var skyZipPath = TestContext.GetProjectDirectory(@"TestFunctional\LibraryShareTest.zip"); // Reusing ShareDocumentTest test file

            var skypPath = TestFilesDir.GetTestPath("test.skyp");       

            var skyp = SkypFile.Create(skypPath, new List<Server>());
            Assert.IsNull(skyp.Server);

            var server = new Server("http://fakepanoramalabkeyserver.org", "user", "password");
            skyp = SkypFile.Create(skypPath, new[] { server, new Server("http://anotherserver.org", null, null) });
            Assert.AreEqual(skyp.Server, server);

            var skyZipName = Path.GetFileName(skyZipPath);
            Assert.AreEqual(TestFilesDir.GetTestPath(skyZipName), skyp.DownloadPath);

            var skypSupport = new SkypSupport(SkylineWindow)
            {
                DownloadClientCreator = new DownloadClientCreatorThrowsError(skyZipPath, skyp.DownloadPath)
            };

            var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, null));
            Assert.IsTrue(errDlg.Message.Contains(string.Format(
                Resources
                    .SkypSupport_Download_You_may_have_to_add__0__as_a_Panorama_server_from_the_Tools___Options_menu_in_Skyline_,
                skyp.SkylineDocUri.Host)));

            RunUI(() => { errDlg.ClickCancel();});
            WaitForClosedForm(errDlg);

            skypSupport.DownloadClientCreator = new TestDownloadClientCreator(skyZipPath, skyp.DownloadPath);
            RunUI(() => skypSupport.Open(skypPath, null));
            WaitForDocumentLoaded();
            var skyZipNoExt = Path.GetFileNameWithoutExtension(skyZipPath);
            var explodedDir = TestFilesDir.GetTestPath(skyZipNoExt);
            Assert.AreEqual(Path.Combine(explodedDir, skyZipNoExt + SrmDocument.EXT), SkylineWindow.DocumentFilePath);
        }

        private void TestSkypGetNonExistentPath()
        {
            const string skyZip = "empty.sky.zip";

            var skyZipPath = TestFilesDir.GetTestPath(skyZip);
            
            Assert.AreEqual(skyZipPath, SkypFile.GetNonExistentPath(TestFilesDir.FullPath, skyZip));

            // Create the file so that GetNonExistentPath appends a (1) suffix to the file name
            using (File.Create(skyZipPath)) { }
            Assert.IsTrue(File.Exists(skyZipPath));

            skyZipPath = TestFilesDir.GetTestPath("empty(1).sky.zip");
            Assert.AreEqual(skyZipPath, SkypFile.GetNonExistentPath(TestFilesDir.FullPath, skyZip));

            // Create a empty(1) directory.
            // Now empty.sky.zip AND empty(1) directory exist in the folder.
            // empty(1).sky.zip does not exist, but opening a file by this name will extract the zip
            // in an empty(1)(1) since empty(1) exists. So we append a (2) suffix to fhe filename so 
            // that the zip is extracted in an empty(2) folder. 
            Directory.CreateDirectory(TestFilesDir.GetTestPath("empty(1)"));
            skyZipPath = TestFilesDir.GetTestPath("empty(2).sky.zip");
            Assert.AreEqual(skyZipPath, SkypFile.GetNonExistentPath(TestFilesDir.FullPath, skyZip));
        }

        private void TestSkypValid()
        {
            AssertEx.ThrowsException<InvalidDataException>(
                () => SkypFile.GetSkyFileUrl(new StringReader(STR_EMPTY_SKYP)),
                string.Format(
                    Resources.SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file___0___on_a_Panorama_server_,
                    SrmDocumentSharing.EXT_SKY_ZIP));

            var err =
                string.Format(
                    Resources
                        .SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive___0___in_the_skyp_file__Found__1__instead_,
                    SrmDocumentSharing.EXT_SKY_ZIP,
                    STR_INVALID_SKYP1);
            AssertEx.ThrowsException<InvalidDataException>(() => SkypFile.GetSkyFileUrl(new StringReader(STR_INVALID_SKYP1)), err);


            err = string.Format(Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, STR_INVALID_SKYP2);
            AssertEx.ThrowsException<InvalidDataException>(() => SkypFile.GetSkyFileUrl(new StringReader(STR_INVALID_SKYP2)), err);

            AssertEx.NoExceptionThrown<Exception>(() => SkypFile.GetSkyFileUrl(new StringReader(STR_VALID_SKYP)));
        }

        private const string STR_EMPTY_SKYP = "";
        private const string STR_INVALID_SKYP1 = @"http://panoramaweb.org/_webdav/Project/not_a_shared_zip.sky";
        private const string STR_INVALID_SKYP2 = @"C:\Project\not_a_shared_zip.sky.zip";
        private const string STR_VALID_SKYP = @"http://panoramaweb.org/_webdav/Project/shared_zip.sky.zip";
    }

    public class TestDownloadClient : IDownloadClient
    {
        private readonly string _srcPath;
        private readonly string _downloadPath;
        private readonly bool _error;

        public TestDownloadClient(string srcFile, string downloadPath, bool error = false)
        {
            _srcPath = srcFile;
            _downloadPath = downloadPath;
            _error = error;
            IsCancelled = false;
        }
    
        public void Download(Uri remoteFile, string downloadPath, string username, string password)
        {
            if (_error)
            {
                Error = new WebException(@"The remote server returned an error. " + SkypSupport.ERROR401);
                return;
            }
            Assert.AreEqual(_downloadPath, downloadPath);
            File.Copy(_srcPath, downloadPath);
        }

        public bool IsCancelled { get; }
        public bool IsError => Error != null;
        public Exception Error { get; set; }
    }

    internal class TestDownloadClientCreator : DownloadClientCreator
    {
        public TestDownloadClient TestDownloadClient { get; set; }

        public TestDownloadClientCreator(string skyZipPath, string skypDownloadPath)
        {
            TestDownloadClient = new TestDownloadClient(skyZipPath, skypDownloadPath);
        }

        public override IDownloadClient Create(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            return TestDownloadClient;
        }
    }

    internal class DownloadClientCreatorThrowsError : TestDownloadClientCreator
    {
        public DownloadClientCreatorThrowsError(string skyZipPath, string skypDownloadPath) : base(skyZipPath, skypDownloadPath)
        {
            TestDownloadClient = new TestDownloadClient(skyZipPath, skypDownloadPath, true); // This will throw an error
        }
    }
}
