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

        private const int NO_ERROR = 0;
        private const int ERROR_BAD_JSON = 1;
        private const int ERROR_BAD_FILE_NAME = 2;
        private const int ERROR_HEAD_REQUEST = 3;
        private const int ERROR_MOVE_REQUEST = 4;
        private const int ERROR_SUBMIT_PIPELINE_JOB = 5;
        private const int ERROR_CHECK_JOB_STATUS = 6;


        public enum RequestType
        {
            HEAD = ERROR_HEAD_REQUEST,
            MOVE = ERROR_MOVE_REQUEST
        }

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


            // Open a new Skyline document, and save it with a name that LabKey would reject.
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("Bad -file_name.sky"));
            });
            WaitForDocumentLoaded();

            // LabKey would return an error in the JSON response if we try to upload a file with name
            // having a space followed by '-', followed by a non-space character (e.g. Bad -file_name.sky).
            TestBadFileNameLabKeyError();

            // Give the file a name that LabKey will accept.
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("Valid-file_name.sky"));
            });
            WaitForDocumentLoaded();


            // Response returned by the server could not be parsed as JSON.
            TestInvalidJsonResponse();

            // TODO: Returned JSON does not have the required attribute(s).

            
            TestFailAtMethod(RequestType.HEAD); // HEAD request to check if sky.zip.part was uploaded
            TestFailAtMethod(RequestType.MOVE); // MOVE request to check if the sky.zip.part was successfully renamed to sky.zip

            // Fail at submitting a document import pipeline job
            TestFailSubmitImportJob();

            TestFailQueryPipelineJobStatus();

            // File uploaded and imported without error
            TestSuccessfulUpload();
        }

        private static MessageDlg PublishError(IPanoramaPublishClient publishClient, out string shareZipFileName)
        {
            var publishDialog = ShowDialog<PublishDocumentDlg>(() => SkylineWindow.ShowPublishDlg(publishClient));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            shareZipFileName = Path.GetFileName(publishDialog.FileName);
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var errorDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            return errorDlg;
        }

        private static void TestBadFileNameLabKeyError()
        {
            var publishClient = new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                PASSWORD, ERROR_BAD_FILE_NAME);
            var errorDlg = PublishError(publishClient, out var shareZipFileName);

            Assert.AreEqual(BadFileNameRequestHelper.GetExpectedUploadError(GetTempZipFileName(shareZipFileName)), errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private static string GetTempZipFileName(string shareZipFileName)
        {
            return shareZipFileName + ".part";
        }

        private static void TestInvalidJsonResponse()
        {
            TestFail(ERROR_BAD_JSON, NoJsonResponseRequestHelper.GetExpectedError());
        }

        private static void TestFailAtMethod(RequestType requestType)
        {
            var publishClient = new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                PASSWORD, (int)requestType);
            var errorDlg = PublishError(publishClient, out var shareZipFileName);
            Assert.AreEqual(FailOnDoRequestRequestHelper.GetExpectedError(GetTempZipFileName(shareZipFileName), requestType),
                errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private static void TestFailSubmitImportJob()
        {
            TestFail(ERROR_SUBMIT_PIPELINE_JOB, FailOnSubmitPipelineJobRequestHelper.GetExpectedError());
        }

        private static void TestFailQueryPipelineJobStatus()
        {
            TestFail(ERROR_CHECK_JOB_STATUS, FailOnCheckJobStatusRequestHelper.GetExpectedError());
        }

        private static void TestFail(int errorCode, string expectedError)
        {
            var publishClient = new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                PASSWORD, errorCode);
            var errorDlg = PublishError(publishClient, out _);
            Assert.AreEqual(expectedError, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
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

        static JObject BuildFolderJson(string name, bool write, bool targeted)
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
                obj["children"] = new JArray(BuildFolderJson("Subfolder", true, true));
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

        public class TestLabKeyErrorPublishClient : AbstractPanoramaPublishClient
        {
            private TestLabKeyErrorPanoramaClient _panoramaClient;

            public TestLabKeyErrorPublishClient(Uri serverUri, string username, string password, int errorType)
            {
                _panoramaClient = new TestLabKeyErrorPanoramaClient(serverUri, username, password, errorType);
            }

            public override IPanoramaClient PanoramaClient => _panoramaClient;
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
                var testFolders = BuildFolderJson("LabKeyErrorsTest", true, false);
                testFolders["children"] = new JArray(BuildFolderJson(PANORAMA_FOLDER, true, true));
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
                return BadFileNameRequestHelper.GetLabKeyError(_skyZipPathForUpload);
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
                    case ERROR_BAD_JSON:
                        requestHelper = new NoJsonResponseRequestHelper();
                        break;
                    case ERROR_BAD_FILE_NAME:
                        requestHelper = new BadFileNameRequestHelper();
                        break;
                    case ERROR_HEAD_REQUEST:
                        requestHelper = new FailOnDoRequestRequestHelper(RequestType.HEAD);
                        break;
                    case ERROR_MOVE_REQUEST:
                        requestHelper = new FailOnDoRequestRequestHelper(RequestType.MOVE);
                        break;
                    case ERROR_SUBMIT_PIPELINE_JOB:
                        requestHelper = new FailOnSubmitPipelineJobRequestHelper();
                        break;
                    case ERROR_CHECK_JOB_STATUS:
                        requestHelper = new FailOnCheckJobStatusRequestHelper();
                        break;
                    default:
                        requestHelper = new TestRequestHelper();
                        break;
                }
                return requestHelper;
            }
        }

        public class NoJsonResponseRequestHelper : TestRequestHelper
        {
            private const string NOT_JSON = "<head><body>This is not JSON</body></head>";
            public override string DoGet(Uri uri)
            {
                return NOT_JSON;
            }

            public static string GetExpectedError()
            {
                return TextUtil.LineSeparate(
                    PanoramaClient.Properties.Resources.BaseWebClient_ParseJsonResponse_Error_parsing_response_as_JSON_,
                    string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                        @"Unexpected character encountered while parsing value: <. Path '', line 0, position 0."),
                    string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_URL___0_,
                        PanoramaUtil.GetPipelineContainerUrl(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)),
                    string.Format(PanoramaClient.Properties.Resources.BaseWebClient_ParseJsonResponse_Response___0_, NOT_JSON));
            }
        }

        public class FailOnDoRequestRequestHelper : TestRequestHelper
        {
            private readonly RequestType _requestMethod;
            private static readonly string MSG_EXCEPTION = "Testing exception on {0} request";
            private static readonly string MSG_LABKEY_ERR = "This is the LabKey error on {0} request";
            private LabKeyError _labkeyError;
            public FailOnDoRequestRequestHelper(RequestType requestMethod)
            {
                _requestMethod = requestMethod;
            }
            public override string GetResponse(HttpWebRequest request)
            {
                if (request.Method.Equals(_requestMethod.ToString()))
                {
                    _labkeyError = new LabKeyError(string.Format(MSG_LABKEY_ERR, request.Method), null);
                    throw new WebException(string.Format(MSG_EXCEPTION, request.Method)); 
                }

                return base.GetResponse(request);
            }

            public override LabKeyError GetLabkeyErrorFromWebException(WebException e)
            {
                return _labkeyError;
            }

            public static string GetExpectedError(string sharedZipFile, RequestType requestType)
            {
                string mainError;
                switch (requestType)
                {
                    case RequestType.HEAD:
                        mainError = PanoramaClient.Properties.Resources
                            .AbstractPanoramaClient_ConfirmFileOnServer_File_was_not_uploaded_to_the_server__Please_try_again__or_if_the_problem_persists__please_contact_your_Panorama_server_administrator_;
                        break;
                    case RequestType.MOVE:
                        mainError = PanoramaClient.Properties.Resources.AbstractPanoramaClient_RenameTempZipFile_There_was_an_error_renaming_the_temporary_zip_file_on_the_server;
                        break;
                    default:
                        mainError = "UNKNOWN ERROR";
                        break;
                }

                var expectedError = TextUtil.LineSeparate(
                    mainError,
                    string.Empty,
                    string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                        string.Format(MSG_EXCEPTION, requestType.ToString())),
                    string.Format(MSG_LABKEY_ERR, requestType.ToString()),
                    string.Format("URL: {0}{1}{2}", PANORAMA_SERVER, GetFolderWebdavUrl(PANORAMA_FOLDER).TrimStart('/'),
                        sharedZipFile)
                );

                return expectedError;
            }
        }

        public class BadFileNameRequestHelper : TestRequestHelper
        {
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

            public static string GetExpectedUploadError(string sharedZipFile)
            {
                var labkeyError = GetLabKeyError(sharedZipFile);
                if (labkeyError != null)
                {
                    return TextUtil.LineSeparate(
                        PanoramaClient.Properties.Resources
                            .AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file,
                        string.Empty,
                        string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                            labkeyError),
                        string.Format("URL: {0}{1}{2}", PANORAMA_SERVER,
                            GetFolderWebdavUrl(PANORAMA_FOLDER).TrimStart('/'), sharedZipFile));
                }

                return string.Format("No Error for file {0}", sharedZipFile);
            }
        }

        // TODO: Test WebException vs no exception but exception in JSON.
        public class FailOnSubmitPipelineJobRequestHelper : TestRequestHelper
        {
            private static readonly string exceptionMessage = "Exception adding to the document import queue.";
            private static readonly string labkeyError = "This is the LabKey error.";

            public override byte[] DoPost(Uri uri, NameValueCollection postData)
            {
                if (uri.Equals(PanoramaUtil.GetImportSkylineDocUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)))
                {
                    throw new  WebException(exceptionMessage);
                }
                return base.DoPost(uri, postData);
            }

            public override LabKeyError GetLabkeyErrorFromWebException(WebException e)
            {
                return e.Message.Equals(exceptionMessage) ? new LabKeyError(labkeyError, null) : null;
            }

            public static string GetExpectedError()
            {
                return TextUtil.LineSeparate(
                    PanoramaClient.Properties.Resources
                        .AbstractPanoramaClient_QueueDocUploadPipelineJob_There_was_an_error_adding_the_document_import_job_on_the_server,
                    string.Empty,
                    string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                        exceptionMessage),
                    labkeyError,
                    string.Format("URL: {0}",
                        PanoramaUtil.GetImportSkylineDocUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)));
            }
        }

        public class FailOnCheckJobStatusRequestHelper : TestRequestHelper
        {
            private static readonly string exceptionMessage = "Exception checking the pipeline job status.";
            private static readonly string labkeyError = "This is the LabKey error.";
            public override string DoGet(Uri uri)
            {
                if (uri.Equals(PanoramaUtil.GetPipelineJobStatusUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER, PIPELINE_JOB_ID)))
                {
                    throw new WebException(exceptionMessage);
                }

                return base.DoGet(uri);
            }

            public override LabKeyError GetLabkeyErrorFromWebException(WebException e)
            {
                return e.Message.Equals(exceptionMessage) ? new LabKeyError(labkeyError, null) : null;
            }

            public static string GetExpectedError()
            {
                return TextUtil.LineSeparate(
                    PanoramaClient.Properties.Resources
                        .AbstractPanoramaClient_WaitForDocumentImportCompleted_There_was_an_error_getting_the_status_of_the_document_import_pipeline_job,
                    string.Empty,
                    string.Format(PanoramaClient.Properties.Resources.GenericState_AppendErrorAndUri_Error___0_,
                        exceptionMessage),
                    labkeyError,
                    string.Format("URL: {0}",
                        PanoramaUtil.GetPipelineJobStatusUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER, PIPELINE_JOB_ID)));
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

            public override LabKeyError GetLabkeyErrorFromWebException(WebException e)
            {
                throw new NotImplementedException();
            }

            public override string GetResponse(HttpWebRequest request)
            {
                return string.Empty;
            }

            public override void Dispose()
            {
            }

            public override string DoPost(Uri uri, string postData)
            {
                throw new NotImplementedException();
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
        }
    }
}
