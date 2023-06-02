using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using AlertDlg = pwiz.Skyline.Alerts.AlertDlg;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SkypTest : AbstractFunctionalTest
    {
        private const string _serverUrlInSkyp = "http://fakepanoramalabkeyserver.org/";
        private const string _userInSkyp = "no-name@no-name.org";
        private const string _password = "password";
        private static readonly Server _matchingServer = new Server(_serverUrlInSkyp, _userInSkyp, _password);

        // Server with the same URL as the server in the skyp file but different username
        private const string _altUser = "another-" + _userInSkyp; 
        private static readonly Server _altMatchingServer = new Server(_serverUrlInSkyp, _altUser, _password);

        private const string _anotherServerUrl = "http://anotherserver.org/";
        private static readonly Server _anotherServer = new Server(_anotherServerUrl, _userInSkyp, _password);

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

            TestSkypOpenErrors();
        }

        private void TestSkypOpenErrors()
        {
            // ----------------------  test with a skyp file with only a URL -----------------------------
            TestOpenErrorsSimpleSkyp();

            // ---------------------- test with a skyp file with URL, FileSize and DownloadingUser ------
            TestOpenErrorsExtendedSkyp();
        }

        private void TestSkypOpen()
        {
            var skyZipPath = TestContext.GetProjectDirectory(@"TestFunctional\LibraryShareTest.zip"); // Reusing ShareDocumentTest test file
            var skypPath = TestFilesDir.GetTestPath("test.skyp");
            var skyp = SkypFile.Create(skypPath, new List<Server>());
            var skyZipName = Path.GetFileName(skyZipPath);
            Assert.AreEqual(TestFilesDir.GetTestPath(skyZipName), skyp.DownloadPath);
            var skypSupport = new SkypSupport(SkylineWindow)
            {
                DownloadClientCreator =
                    new TestDownloadClientCreator(skyZipPath, skyp, false, false)
            };
            RunUI(() => skypSupport.Open(skypPath, null));
            WaitForDocumentLoaded();
            var skyZipNoExt = Path.GetFileNameWithoutExtension(skyZipPath);
            var explodedDir = TestFilesDir.GetTestPath(skyZipNoExt);
            Assert.AreEqual(Path.Combine(explodedDir, skyZipNoExt + SrmDocument.EXT), SkylineWindow.DocumentFilePath);
        }

        private void TestOpenErrorsExtendedSkyp()
        {
            // Contents of test-extended.skyp:
            // http://fakepanoramalabkeyserver.org/labkey/_webdav/Test%20Project/%40files/Fake%20Document.sky.zip
            // FileSize: 100
            // DownloadingUser: no-name@no-name.org
            var skypPath2 = TestFilesDir.GetTestPath("test-extended.skyp");
            var skyp = SkypFile.Create(skypPath2, new List<Server>());
            Assert.IsNull(skyp.ServerMatch);
            Assert.AreEqual(_userInSkyp, skyp.DownloadingUser);
            Assert.IsNotNull(skyp.Size);
            Assert.AreEqual(new Uri("http://fakepanoramalabkeyserver.org/labkey/_webdav/Test%20Project/%40files/Fake%20Document.sky.zip"), skyp.SkylineDocUri);
            Assert.AreEqual("Fake Document.sky.zip", skyp.GetSkylineDocName());
            Assert.AreEqual("http://fakepanoramalabkeyserver.org/labkey/_webdav/Test Project/", skyp.GetDocUrlNoName());
            Assert.AreEqual("http://fakepanoramalabkeyserver.org/", skyp.GetServerName());

            
            // 1. Server in the skyp file does not match a saved Panorama server. Expect to see an error about adding a new Panorama 
            //    server in Skyline. Username in the EditServerDlg should be the username from the skyp file
            TestSkypOpenWithError(skypPath2,  new[] { _anotherServer },
                null, // No match in existing servers
                false, // No username mismatch since there is no match in existing servers
                skyp.DownloadingUser, // Expect username from the skyp file in EditServerDlg
                _serverUrlInSkyp, 
                TestDownloadClient.ERROR401,
                true // Expect to see EditServerDlg 
            );
        
        
            // 2. Server in the skyp file matches a saved Panorama server. If we get a 401 (Unauthorized) error it means that the saved
            //    credentials are invalid. Expect a message about updating the credentials of the saved Panorama server.
            TestSkypOpenWithError(skypPath2, new[] { _matchingServer, _anotherServer },
                _matchingServer, // Has match in existing servers
                false, // username in skyp matches the username in saved credentials for the server
                _userInSkyp,
                _serverUrlInSkyp,
                TestDownloadClient.ERROR401,
                true // Expect to see EditServerDlg 
            );


            // 3. Server in the skyp file matches a saved Panorama server. If we get a 403 (Forbidden) error it means that the saved
            //    credentials are invalid. The username in the skyp file is the same as the saved username for the server. 
            //    EditServerDlg should not be shown in this case
            TestSkypOpenWithError(skypPath2, new[] { _matchingServer, _anotherServer },
                _matchingServer, // Has match in existing servers
                false, // username in skyp matches the username in saved credentials for the server
                _userInSkyp,
                null, // Don't expect to see EditServerDlg
                TestDownloadClient.ERROR403,
                false // Do not expect to see the EditServerDlg
            );


            // 4. Server in the skyp file matches a saved Panorama server. If we get a 403 (Forbidden) error it means that the
            //    user in the saved credentials does not have enough permissions. Username in the skyp file is different from
            //    the username in the saved credentials.
            //    Expect a message about updating the credentials of the saved Panorama server.
            //    Username displayed in the EditServerDlg should be the username from the skyp file.
            TestSkypOpenWithError(skypPath2, new[] { _altMatchingServer, _anotherServer },
                _altMatchingServer, // Has match in existing servers
                true, // username in skyp does not match the username in saved credentials for the server
                skyp.DownloadingUser, // username from the skyp file
                _serverUrlInSkyp,
                TestDownloadClient.ERROR403,
                true // Expect to see EditServerDlg 
            );

            // 5. Server in the skyp file matches a saved Panorama server. If we get a 401 (Unauthorized) error it means that the saved
            //    credentials are invalid. Username in the skyp file is not the same as the username saved for the server.
            //    Expect a message about updating the credentials of the saved Panorama server.
            //    Username displayed in the EditServerDlg should be the username from the skyp file.
            TestSkypOpenWithError(skypPath2, new[] { _altMatchingServer, _anotherServer },
                _altMatchingServer, // Has match in existing servers
                true, // username in skyp does not match the username in saved credentials for the server
                skyp.DownloadingUser, // username from the skyp file
                _serverUrlInSkyp,
                TestDownloadClient.ERROR401,
                true // Expect to see EditServerDlg 
            );
        }

        private void TestOpenErrorsSimpleSkyp()
        {
            // Contents of test.skyp:
            // http://fakepanoramalabkeyserver.org/LibraryShareTest.zip
            var skypPath = TestFilesDir.GetTestPath("test.skyp");

            var skyp = SkypFile.Create(skypPath, new List<Server>());
            Assert.IsNull(skyp.ServerMatch);
            Assert.IsNull(skyp.DownloadingUser);
            Assert.AreEqual(new Uri("http://fakepanoramalabkeyserver.org/LibraryShareTest.zip"), skyp.SkylineDocUri);
            Assert.AreEqual("LibraryShareTest.zip", skyp.GetSkylineDocName());
            Assert.AreEqual("http://fakepanoramalabkeyserver.org/LibraryShareTest.zip", skyp.GetDocUrlNoName());
            Assert.AreEqual("http://fakepanoramalabkeyserver.org/", skyp.GetServerName());

            // 1. Server in the skyp file does not match a saved Panorama server. Expect to see an error about adding a new Panorama 
            //    server in Skyline.
            TestSkypOpenWithError(skypPath, new[] { _anotherServer },
                null, // No match in existing servers
                false, // no username mismatch since skyp does not have a DownloadingUser
                string.Empty, // No username; we are adding a new server
                _serverUrlInSkyp,
                TestDownloadClient.ERROR401,
                true // Expect to see EditServerDlg 
            ); 
            
            
            // 2. Server in the skyp file matches a saved Panorama server. If we get a 401 (Unauthorized) error it means that the saved
            //    credentials are invalid. Expect a message about updating the credentials of a saved Panorama server.
            TestSkypOpenWithError(skypPath, new[] { _matchingServer, _anotherServer },
                _matchingServer, // Has match in existing servers
                false, // no username mismatch since skyp does not have a DownloadingUser
                _userInSkyp,
                _serverUrlInSkyp,
                TestDownloadClient.ERROR401,
                true // Expect to see EditServerDlg 
            );
            
            
            // 3. Server in the skyp file matches a saved Panorama server. If we get a 403 (Forbidden) error it means that the saved user does
            //    not have adequate permissions for the requested resource on the Panorama server. EditServerDlg should not be shown.
            TestSkypOpenWithError(skypPath, new[] { _matchingServer, _anotherServer },
                _matchingServer, // Has match in existing servers
                false, // no username mismatch since skyp does not have a DownloadingUser
                _userInSkyp,
                null, // Don't expect to see EditServerDlg
                TestDownloadClient.ERROR403,
                false // Don't expect to see EditServerDlg
            );


            testFailOnAddSameServer();
        }

        private void testFailOnAddSameServer()
        {
            var skypPath = TestFilesDir.GetTestPath("test.skyp");
            var existingServers = new[] { _anotherServer };

            Settings.Default.ServerList.Clear();
            Settings.Default.ServerList.AddRange(existingServers);

            var skyp = SkypFile.Create(skypPath, existingServers);
            Assert.IsNull(skyp.ServerMatch); // No match found in existing servers
            
            var skypSupport = new SkypSupport(SkylineWindow)
            {
                DownloadClientCreator =
                    new TestDownloadClientCreator(null, skyp, true, false) // 401 error; server not saved in Skyline
            };
            var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, existingServers));
            string expectedErr =
                string.Format(
                    Resources
                        .SkypDownloadException_GetMessage_Would_you_like_to_add__0__as_a_Panorama_server_in_Skyline_,
                    skyp.GetServerName());
            
            Assert.IsTrue(errDlg.Message.Contains(expectedErr));
            Assert.IsTrue(errDlg.Message.Contains(TestDownloadClient.ERROR401));

            IPanoramaClient testClient = new AllValidPanoramaClient();

            var editServerDlg = ShowDialog<EditServerDlg>(errDlg.ClickOk);
            RunUI(() =>
            {
                editServerDlg.PanoramaClient = testClient;
                Assert.AreEqual(editServerDlg.URL, _serverUrlInSkyp);
                Assert.AreEqual(editServerDlg.Username, string.Empty);
                Assert.AreEqual(editServerDlg.Password, string.Empty);

                editServerDlg.URL = _anotherServerUrl; // Change the URL to a server that already exists
                editServerDlg.Username = _userInSkyp;
                editServerDlg.Password = _password;
            });

            RunDlg<AlertDlg>(editServerDlg.OkDialog, errorDlg =>
            {
                Assert.AreEqual(string.Format(Resources.EditServerDlg_OkDialog_The_server__0__already_exists_, _anotherServerUrl), errorDlg.Message);
                errorDlg.OkDialog();
            });
            OkDialog(editServerDlg, editServerDlg.CancelDialog);

            Settings.Default.ServerList.Clear();
        }

        private void TestSkypOpenWithError(string skypPath, Server[] existingServers,
            Server matchingServer,
            bool usernameMismatch, string expectedUserNameInEditServerDlg,
            string expectedUriInEditServerDlg,
            string errorCode,
            bool expectEditServerDlg)
        {
            Settings.Default.ServerList.Clear();
            Settings.Default.ServerList.AddRange(existingServers);

            var skyp = SkypFile.Create(skypPath, existingServers);
            if (matchingServer != null)
                Assert.AreEqual(skyp.ServerMatch, matchingServer); // Match found in existing servers 
            else Assert.IsNull(skyp.ServerMatch); // No match found in existing servers
            Assert.AreEqual(usernameMismatch, skyp.UsernameMismatch());


            bool err401 = TestDownloadClient.ERROR401.Equals(errorCode);
            bool err403 = TestDownloadClient.ERROR403.Equals(errorCode);

            var skypSupport = new SkypSupport(SkylineWindow)
            {
                DownloadClientCreator =
                    new TestDownloadClientCreator(null, skyp, err401, err403)
            };
            var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, existingServers));
            var fullErrMsg = errDlg.Message;
            Assert.IsTrue(fullErrMsg.Contains(errorCode));

            if (err401)
            {
                if (matchingServer == null)
                {
                    var expected = string.Format(
                        Resources
                            .SkypDownloadException_GetMessage_Would_you_like_to_add__0__as_a_Panorama_server_in_Skyline_,
                        skyp.GetServerName());
                    AssertErrorContains(expected, fullErrMsg);
                }
                else
                {
                    var expected = string.Format(
                        Resources
                            .SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_invalid_,
                        skyp.GetServerName());
                    AssertErrorContains(expected, fullErrMsg);

                    AssertErrorContains(Resources.SkypDownloadException_GetMessage_Would_you_like_to_update_the_credentials_, fullErrMsg);

                    if (skyp.UsernameMismatch())
                    {
                        expected = string.Format(
                            Resources
                                .SkypDownloadException_GetMessage_The_skyp_file_was_downloaded_by_the_user__0___Credentials_saved_in_Skyline_for_this_server_are_for_the_user__1__,
                            skyp.DownloadingUser, _altUser);
                        AssertErrorContains(expected, fullErrMsg);
                    }
                }
            }
            else if (err403)
            {
                if (skyp.UsernameMismatch())
                {
                    var expected = string.Format(
                        Resources
                            .SkypDownloadException_GetMessage_Credentials_saved_in_Skyline_for_the_Panorama_server__0__are_for_the_user__1___This_user_does_not_have_permissions_to_download_the_file__The_skyp_file_was_downloaded_by__2__,
                        skyp.GetServerName(), _altUser, skyp.DownloadingUser);
                    AssertErrorContains(expected, fullErrMsg);

                    AssertErrorContains(
                        Resources.SkypDownloadException_GetMessage_Would_you_like_to_update_the_credentials_,
                        fullErrMsg);

                }
                else
                {
                    var expected = string.Format(
                        Resources.SkypSupport_Download_You_do_not_have_permissions_to_download_this_file_from__0__,
                        skyp.GetServerName());
                    AssertErrorContains(expected, fullErrMsg);
                }
            }


            if (expectEditServerDlg)
            {
                RunDlg<EditServerDlg>(errDlg.OkDialog, editServerDlg =>
                {
                    Assert.AreEqual(editServerDlg.URL, expectedUriInEditServerDlg);
                    Assert.AreEqual(editServerDlg.Username, expectedUserNameInEditServerDlg);
                    editServerDlg.CancelDialog();
                });
            }
            else
            {
                OkDialog(errDlg, errDlg.OkDialog);
            }

            Settings.Default.ServerList.Clear();
        }

        private static void AssertErrorContains(string expected, string FullError)
        {
            Assert.IsTrue(FullError.Contains(expected),
                TextUtil.LineSeparate(string.Format("Expected error contains: {0}", expected),
                    string.Format("Error message was: {0}.", FullError)));
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
                () => SkypFile.CreateForTest(STR_EMPTY_SKYP),
                string.Format(
                    Resources.SkypFile_GetSkyFileUrl_File_does_not_contain_the_URL_of_a_shared_Skyline_archive_file___0___on_a_Panorama_server_,
                    SrmDocumentSharing.EXT_SKY_ZIP));

            var err =
                string.Format(
                    Resources.SkypFile_GetSkyFileUrl_Expected_the_URL_of_a_shared_Skyline_document_archive_file___0____Found_filename__1__instead_in_the_URL__2__,
                    SrmDocumentSharing.EXT_SKY_ZIP,
                    "not_a_shared_zip.sky",
                    STR_INVALID_SKYP1);
            AssertEx.ThrowsException<InvalidDataException>(() => SkypFile.CreateForTest(STR_INVALID_SKYP1), err);


            err = string.Format(Resources.SkypFile_GetSkyFileUrl__0__is_not_a_valid_URL_on_a_Panorama_server_, STR_INVALID_SKYP2);
            AssertEx.ThrowsException<InvalidDataException>(() => SkypFile.CreateForTest(STR_INVALID_SKYP2), err);

            SkypFile skyp1 = null;
            AssertEx.NoExceptionThrown<Exception>(() => skyp1 = SkypFile.CreateForTest(STR_VALID_SKYP));
            Assert.AreEqual(new Uri(STR_VALID_SKYP), skyp1.SkylineDocUri);
            Assert.IsNull(skyp1.Size);
            Assert.IsNull(skyp1.DownloadingUser);


            var skyp2 = SkypFile.CreateForTest(STR_VALID_SKYP_EXTENDED);
            Assert.AreEqual(new Uri(STR_VALID_SKYP_LOCALHOST), skyp2.SkylineDocUri);
            Assert.AreEqual(LOCALHOST, skyp2.GetServerName());
            Assert.AreEqual(skyp2.Size, 100);
            Assert.AreEqual(skyp2.DownloadingUser, "no-name@no-name.edu");

            var skyp3 = SkypFile.CreateForTest(STR_INVALID_SIZE_SKYP3);
            Assert.AreEqual(new Uri(STR_VALID_SKYP_LOCALHOST), skyp3.SkylineDocUri);
            Assert.IsFalse(skyp3.Size.HasValue);
            Assert.AreEqual(skyp3.DownloadingUser, "no-name@no-name.edu");

        }

        private const string STR_EMPTY_SKYP = "";
        private const string STR_INVALID_SKYP1 = @"http://panoramaweb.org/_webdav/Project/not_a_shared_zip.sky";
        private const string STR_INVALID_SKYP2 = @"C:\Project\not_a_shared_zip.sky.zip";
        private const string STR_VALID_SKYP = @"https://panoramaweb.org/_webdav/Project/shared_zip.sky.zip";

        private const string LOCALHOST = "http://localhost:8080/";
        private const string STR_VALID_SKYP_LOCALHOST = LOCALHOST + "labkey/_webdav/Project/shared_zip.sky.zip";
        private const string STR_VALID_SKYP_EXTENDED =
            STR_VALID_SKYP_LOCALHOST + "\n\rFileSize:100\n\rDownloadingUser:no-name@no-name.edu";

        private const string STR_INVALID_SIZE_SKYP3 =
            STR_VALID_SKYP_LOCALHOST + "\n\rFileSize:invalid\n\rDownloadingUser:no-name@no-name.edu";
    }

    public class TestDownloadClient : IDownloadClient
    {
        private readonly string _srcPath;
        protected readonly SkypFile _skyp;
        private IProgressMonitor ProgressMonitor { get; }
        private IProgressStatus ProgressStatus { get; set; }

        public const string ERROR401 = "(401) Unauthorized";
        public const string ERROR403 = "(403) Forbidden";

        public TestDownloadClient(string srcFile, SkypFile skyp, IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            _srcPath = srcFile;
            _skyp = skyp;
            IsCancelled = false;
            ProgressMonitor = progressMonitor;
            ProgressStatus = progressStatus;
        }
    
        public void Download(SkypFile skyp)
        {
            if (Error != null)
            {
                ProgressMonitor.UpdateProgress(ProgressStatus = ProgressStatus.ChangeErrorException(Error));
                return;
            }
            Assert.AreEqual(_skyp.DownloadPath, skyp.DownloadPath);
            File.Copy(_srcPath, skyp.DownloadPath);
        }

        public bool IsCancelled { get; }
        public bool IsError => Error != null;
        public Exception Error { get; set; }
    }

    public class TestDownloadClientError401 : TestDownloadClient
    {
        public TestDownloadClientError401(SkypFile skyp, IProgressMonitor progressMonitor, IProgressStatus progressStatus) : 
            base(null, skyp, progressMonitor, progressStatus)
        {
            Error = new SkypDownloadException(SkypDownloadException.GetMessage(_skyp, new Exception(ERROR401), HttpStatusCode.Unauthorized), HttpStatusCode.Unauthorized, null);
        }
    }

    public class TestDownloadClientError403 : TestDownloadClient
    {
        public TestDownloadClientError403(SkypFile skyp, IProgressMonitor progressMonitor, IProgressStatus progressStatus) :
            base(null, skyp, progressMonitor, progressStatus)
        {
            Error = new SkypDownloadException(SkypDownloadException.GetMessage(_skyp, new Exception(ERROR403), HttpStatusCode.Forbidden), HttpStatusCode.Forbidden, null);
        }
    }

    internal class TestDownloadClientCreator : DownloadClientCreator
    {
        private string _skyZipPath;
        private SkypFile _skyp;
        private bool _401Error;
        private bool _403Error;

        public TestDownloadClientCreator(string skyZipPath, SkypFile skyp, bool error401, bool error403)
        {
            _skyZipPath = skyZipPath;
            _skyp = skyp;
            _401Error = error401;
            _403Error = error403;
        }

        public override IDownloadClient Create(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            if (_401Error)
            {
                return new TestDownloadClientError401(_skyp, progressMonitor, progressStatus);
            }

            else if (_403Error)
            {
                return new TestDownloadClientError403(_skyp, progressMonitor, progressStatus);
            }
            else
            {
                return new TestDownloadClient(_skyZipPath, _skyp, progressMonitor, progressStatus);
            }
        }
    }

    class AllValidPanoramaClient : IPanoramaClient
    {
        public Uri ServerUri { get { return null; } }

        public ServerState GetServerState()
        {
           return ServerState.VALID;
        }

        public UserState IsValidUser(string username, string password)
        {
            return UserState.VALID;
        }

        public FolderState IsValidFolder(string folderPath, string username, string password)
        {
            throw new NotImplementedException();
        }

        public FolderOperationStatus CreateFolder(string parentPath, string folderName, string username, string password)
        {
            throw new NotImplementedException();
        }

        public FolderOperationStatus DeleteFolder(string folderPath, string username, string password)
        {
            throw new NotImplementedException();
        }

        public JToken GetInfoForFolders(PanoramaServer server, string folder)
        {
            throw new NotImplementedException();
        }

    }
}
