using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PanoramaClientPublishTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPublishToPanorama()
        {
            // TestFilesZip = @"TestFunctional\AccessServerTest.zip";
            using (new FakeProsit(null))
            {
                RunFunctionalTest();
            }
        }

        public const string PANORAMA_SERVER = "https://localhost:8080/";
        public const string PANORAMA_FOLDER = "TestUpload";
        private const string USER_NAME = "user@user.edu";
        private const string PASSWORD = "password";

        public const int PIPELINE_JOB_ID = 42;

        public const int NO_ERROR = 0;
        public const int BAD_JSON = 1;
        public const int UPLOAD_ERROR = 2;
        public const int HEAD_REQUEST_ERROR = 3;
        public const int MOVE_REQUEST_ERROR = 4;

        private ToolOptionsUI ToolOptionsDlg { get; set; }

        protected override void DoTest()
        {
            TestDisplayLabKeyErrors();
        }

        private void TestDisplayLabKeyErrors()
        {
            // Remove all saved Panorama servers
            ToolOptionsDlg =
                ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));
            var editServerListDlg =
                ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            RunUI(editServerListDlg.ResetList);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);

            // Add a server
            AddServer(PANORAMA_SERVER, USER_NAME, PASSWORD);


            // Open a Skyline document
            var BAD_FILE_NAME = "Bad -file_name.sky";
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath(BAD_FILE_NAME));
            });
            WaitForDocumentLoaded();


            // Response returned by the server could not be parsed as JSON.
            TestInvalidJsonResponse();

            // TODO: Returned JSON does not have the required attribute(s).

            // LabKey returned an error in the JSON response while uploading the file.
            TestBadFileNameLabKeyError();

            // Give the file a name that LabKey will accept.
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("Valid-file_name.sky"));
            });
            WaitForDocumentLoaded();

            // File uploaded and imported successfully.
            TestSuccessfulUpload();
        }

        private static void TestSuccessfulUpload()
        {
            var publishDialog = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(
                new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                    PASSWORD, NO_ERROR)));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var docUploadedDlg = ShowDialog<MultiButtonMsgDlg>(shareTypeDlg.OkDialog);
            OkDialog(docUploadedDlg, docUploadedDlg.ClickNo);
        }

        private static void TestBadFileNameLabKeyError()
        {
            var publishDialog = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(
                new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                    PASSWORD, UPLOAD_ERROR)));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var errorDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            var expectedErrorPart = TextUtil.LineSeparate(
                Resources
                    .AbstractPanoramaPublishClient_UploadFileCompleted_There_was_an_error_uploading_the_file_,
                string.Empty,
                TestRequestHelper.GetLabKeyError(SkylineWindow.DocumentFilePath).ToString(),
                string.Format("URL: {0}{1}Bad -file_name", PANORAMA_SERVER, TestRequestHelper.GetFolderWebdavUrl(PANORAMA_FOLDER).TrimStart('/')));
            Assert.IsTrue(errorDlg.Message.Contains(expectedErrorPart),
                string.Format("Expected error to contain '{0}'.\nActual error: {1}", expectedErrorPart,
                    errorDlg.Message));
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private static void TestInvalidJsonResponse()
        {
            var publishDialog = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(
                new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                    PASSWORD, BAD_JSON)));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var errorDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            var expectedError = TextUtil.LineSeparate(
                PanoramaClient.Properties.Resources.BaseWebClient_ParseJsonResponse_Error_parsing_response_as_JSON_,
                string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                    @"Unexpected character encountered while parsing value: <. Path '', line 0, position 0."),
                string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_URL___0_,
                    PanoramaUtil.GetPipelineContainerUrl(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)),
                string.Format(PanoramaClient.Properties.Resources.BaseWebClient_ParseJsonResponse_Response___0_,
                    NoJsonResponseRequestHelper.NOT_JSON));
            Assert.AreEqual(expectedError, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private void AddServer(string server, string userEmail, string password)
        {
            ToolOptionsDlg =
                ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Panorama));
            var editServerListDlg =
                ShowDialog<EditListDlg<SettingsListBase<Server>, Server>>(ToolOptionsDlg.EditServers);
            var client = new TestLabKeyErrorPanoramaClient(new Uri(server), userEmail, password);
            var editServerDlg = ShowDialog<EditServerDlg>(editServerListDlg.AddItem);
            RunUI(() =>
            {
                editServerDlg.PanoramaClient = client;
                editServerDlg.URL = server;
                editServerDlg.Password = password;
                editServerDlg.Username = userEmail;
            });

            OkDialog(editServerDlg, editServerDlg.OkDialog);
            OkDialog(editServerListDlg, editServerListDlg.OkDialog);
            TryWaitForConditionUI(() => 1 == Settings.Default.ServerList.Count);
            RunUI(() => Assert.AreEqual(1, Settings.Default.ServerList.Count));
            OkDialog(ToolOptionsDlg, ToolOptionsDlg.OkDialog);
        }

        static JObject CreateFolder(string name, bool write, bool targeted)
        {
            JObject obj = new JObject();
            obj["name"] = name;
            obj["path"] = "/" + name + "/";
            obj["userPermissions"] = write ? 3 : 1;
            if (!write || !targeted)
            {
                // Create a writable subfolder if this folder is not writable, i.e. it is
                // not a targetedMS folder or the user does not have write permissions in this folder.
                // Otherwise, it will not get added to the folder tree (PublishDocumentDlg.AddChildContainers()).
                obj["children"] = new JArray(CreateFolder("Subfolder", true, true));
            }
            else
            {
                obj["children"] = new JArray();
            }

            obj["folderType"] = targeted ? "Targeted MS" : "Collaboration";
            obj["activeModules"] = targeted
                ? new JArray("MS0", "MS1", "TargetedMS", "MS3")
                : new JArray("MS0", "MS1", "MS3");
            return obj;
        }

        public class TestLabKeyErrorPanoramaClient : AbstractPanoramaClient
        {
            private int _errorType;
            private string _skyZipPathForUpload;

            public TestLabKeyErrorPanoramaClient(Uri serverUri, string username, string password) 
                : this(serverUri, username, password, NO_ERROR)
            {
            }

            public TestLabKeyErrorPanoramaClient(Uri serverUri, string username, string password, int errorType) : base(serverUri, username, password)
            {
                _errorType = errorType;
            }

            public override JToken GetInfoForFolders(string folder)
            {
                var testFolders = CreateFolder("LabKeyErrorsTest", true, false);
                testFolders["children"] = new JArray(CreateFolder(PANORAMA_FOLDER, true, true));
                return testFolders;
            }

            public override void DownloadFile(string fileUrl, string fileName, long fileSize, string realName, IProgressMonitor pm,
                IProgressStatus progressStatus)
            {
                throw new NotImplementedException();
            }

            public override JObject SupportedVersionsJson()
            {
                var obj = new JObject();
                obj["SKYD_version"] = ChromatogramCache.FORMAT_VERSION_CACHE.ToString();
                return obj;
            }

            protected override LabKeyError ParseUploadFileCompletedEventArgs(UploadFileCompletedEventArgs e)
            {
                return TestRequestHelper.GetLabKeyError(_skyZipPathForUpload);
            }

            public override Uri ValidateUri(Uri serverUri, bool tryNewProtocol = true)
            {
                return serverUri;
            }

            public override PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password)
            {
                return new PanoramaServer(serverUri, username, password);
            }

            public override PanoramaServer EnsureLogin(PanoramaServer pServer)
            {
                return new PanoramaServer(pServer.URI, pServer.Username, pServer.Password);
            }

            public override Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
            {
                _skyZipPathForUpload = zipFilePath;
                return base.SendZipFile(folderPath, zipFilePath, progressMonitor);
            }

            public override IRequestHelper GetRequestHelper(bool forUpload = false)
            {
                TestRequestHelper requestHelper;
                switch (_errorType)
                {
                    case BAD_JSON:
                        requestHelper = new NoJsonResponseRequestHelper();
                        break;
                    // case UPLOAD_ERROR:
                    //     requestHelper = new UploadErrorRequestHelper();
                    //     break;
                    default:
                        requestHelper = new TestRequestHelper();
                        break;
                }
                return requestHelper;
            }
        }

        public class TestLabKeyErrorPublishClient : AbstractPanoramaPublishClient
        {
            private TestLabKeyErrorPanoramaClient _panoramaClient;

            public TestLabKeyErrorPublishClient(Uri serverUri, string username, string password, int errorType)
            {
                _panoramaClient = new TestLabKeyErrorPanoramaClient(serverUri, username, password, errorType);
            }

            public override IPanoramaClient PanoramaClient => _panoramaClient;
        }

        public class NoJsonResponseRequestHelper : TestRequestHelper
        {
            public const string NOT_JSON = "<head><body>This is not JSON</body></head>";
            public override string DoGet(Uri uri)
            {
                return NOT_JSON;
            }
        }

        public class TestRequestHelper : AbstractRequestHelper
        {
            private UploadFileCompletedEventHandler _uploadCompletedEventHandler;
            // private UploadProgressChangedEventHandler _uploadProgressChangedEventHandler;

            public override string DoGet(Uri uri)
            {
                // Depending on the action in the Uri return an appropriate valid JSON response
                if (uri.Equals(PanoramaUtil.GetPipelineContainerUrl(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)))
                {
                    var json = new JObject();
                    json[@"webDavURL"] = GetFolderWebdavUrl(PANORAMA_FOLDER);
                    return json.ToString();
                }
                else if (uri.Equals(PanoramaUtil.GetPipelineJobStatusUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER,
                             PIPELINE_JOB_ID)))
                {
                    var jobStatus = new JObject
                    {
                        ["RowId"] = PIPELINE_JOB_ID,
                        [@"Status"] = "COMPLETE"
                    };
                    var rowsArr = new JArray { jobStatus };
                    var json = new JObject
                    {
                        ["rows"] = rowsArr
                    };
                    return json.ToString();
                }

                return new JObject
                {
                    ["Unrecognized"] = uri.ToString()
                }.ToString();
            }

            public static string GetFolderWebdavUrl(string panoramaFolder)
            {
                return "/_webdav/" + panoramaFolder + "/%40files/";
            }

            public override void AddHeader(string name, string value)
            {
            }

            public override void AddHeader(HttpRequestHeader header, string value)
            {
            }

            public override void RemoveHeader(string name)
            {
            }

            public override void AsyncUploadFile(Uri address, string method, string fileName)
            {
                Task.Run(async delegate
                {
                    await Task.Delay(TimeSpan.FromSeconds(1.0));
                    // Thread.Sleep(1000);
                    _uploadCompletedEventHandler.Invoke(this, null);
                });
            }

            public override void CancelAsyncUpload()
            {
            }

            public override void AddUploadFileCompletedEventHandler(UploadFileCompletedEventHandler handler)
            {
                _uploadCompletedEventHandler = handler;
            }

            public override void AddUploadProgressChangedEventHandler(UploadProgressChangedEventHandler handler)
            {
                // _uploadProgressChangedEventHandler = handler;
            }

            public override void Dispose()
            {
            }

            public override string DoPost(Uri uri, string postData)
            {
                throw new NotImplementedException();
            }

            public override void DoRequest(HttpWebRequest request, string method, string authHeader, string messageOnLabkeyError = null)
            {
                // Method can be HEAD, MOVE, DELETE.  Error can be thrown based on the error.
            }

            public override byte[] DoPost(Uri uri, NameValueCollection postData)
            {
                if (uri.Equals(PanoramaUtil.GetImportSkylineDocUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)))
                {

                    var child = new JObject
                    {
                        [@"RowId"] = PIPELINE_JOB_ID
                    };
                    var array = new JArray { child };
                    var json = new JObject
                    {
                        [@"UploadedJobDetails"] = array
                    };
                    return new UTF8Encoding().GetBytes(json.ToString());
                }
                else return Array.Empty<byte>();
            }

            public static LabKeyError GetLabKeyError(string documentFilePath)
            {
                var errResponse = GetLabKeyJsonResponse(Path.GetFileName(documentFilePath));
                return PanoramaUtil.GetIfErrorInResponse(errResponse);
            }

            private static JObject GetLabKeyJsonResponse(string filename)
            {
                var errResponse = new JObject();
                // From org.labkey.api.util.FileUtil:
                // if (Pattern.matches("(.*\\s--[^ ].*)|(.*\\s-[^- ].*)",s))
                //     return "Filename may not contain space followed by dash.";
                const string pattern = "(.*\\s--[^ ].*)|(.*\\s-[^- ].*)";
                var match = Regex.Match(filename, pattern);
                if (match.Success)
                {
                    errResponse[@"exception"] = "Filename may not contain space followed by dash.";
                    errResponse[@"status"] = 400;
                }
                return errResponse;
            }
        }
    }
}
