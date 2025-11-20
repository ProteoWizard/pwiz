/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net; // HttpStatusCode
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline;
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
    public class PanoramaClientPublishTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestPublishToPanorama()
        {
            using (new FakeKoina(true, null))
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
        private const int ERROR_UPLOAD_FILE = 2;
        private const int ERROR_HEAD_REQUEST = 3;
        private const int ERROR_MOVE_REQUEST = 4;
        private const int ERROR_SUBMIT_PIPELINE_JOB = 5;
        private const int ERROR_CHECK_JOB_STATUS = 6;
        private const int ERROR_HTTP_CLIENT = 7;    // Pass through the HttpClientWithProgress errors

        /// <summary>
        /// Used for tests that short-circuit requests before <see cref="HttpClientWithProgress"/>,
        /// written before this class existed.
        /// </summary>
        /// <param name="errorType">A specific type of error expected to be thrown by a <see cref="TestRequestHelper"/> implementation</param>
        private static IPanoramaPublishClient CreateTestClient(int errorType = NO_ERROR)
        {
            return new TestLabKeyErrorPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                PASSWORD, errorType);
        }

        /// <summary>
        /// Used for tests with a true web client that need to test down to <see cref="HttpClientWithProgress"/>
        /// </summary>
        private static IPanoramaPublishClient CreateWebClient()
        {
            return new WebPanoramaPublishClient(new Uri(PANORAMA_SERVER), USER_NAME,
                PASSWORD);
        }

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
            var badFileName = "Bad -file_name.sky";
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath(badFileName));
            });
            WaitForDocumentLoaded();

            // LabKey would return an error in the JSON response if we try to upload a file with name
            // having a space followed by '-', followed by a non-space character (e.g. Bad -file_name.sky).
            TestBadFileNameLabKeyError(badFileName);

            // Give the file a name that LabKey will accept.
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(TestContext.GetTestResultsPath("Valid-file_name.sky"));
            });
            WaitForDocumentLoaded();


            // Response returned by the server could not be parsed as JSON.
            TestInvalidJsonResponse();

            TestFailAtFileUpload(); // Failure to upload file to server
            TestFailAtMethod(RequestType.HEAD); // HEAD request to check if sky.zip.part was uploaded
            TestFailAtMethod(RequestType.MOVE); // MOVE request to check if the sky.zip.part was successfully renamed to sky.zip

            // Fail at submitting a document import pipeline job
            TestFailSubmitImportJob();

            TestFailQueryPipelineJobStatus();

            // Additional integration tests for LongWaitDlg + HttpClientWithProgress
            TestUserCancelRetrievingFolders();
            TestNoNetworkRetrievingFolders();
            TestAuthenticationErrorRetrievingFolders();
            TestUserCancelDuringUpload();
            TestNoNetworkDuringUpload();
            TestPermissionErrorDuringDownload();

            // File uploaded and imported without error
            // Keep this test last, or the upload flow will include a message about the
            // file being last uploaded to the destination this upload succeeds on
            TestSuccessfulUpload();
            // CONSIDER: Maybe add a test of the message about last successful upload?
        }

        private static MessageDlg PublishError(IPanoramaPublishClient publishClient, out string shareZipFileName)
        {
            var publishDialog = ShowDialog<PublishDocumentDlgPanorama>(() => SkylineWindow.ShowPublishDlg(publishClient));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            shareZipFileName = Path.GetFileName(publishDialog.FileName);
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var errorDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            return errorDlg;
        }

        private static void TestBadFileNameLabKeyError(string badFileName)
        {
            var publishClient = CreateTestClient();
            // Check for a valid filename is done in Skyline even before the PublishDocumentDlgBase is displayed.
            // A MessageDlg with the error is displayed if the filename will be rejected by LabKey Server.
            var errorDlg = ShowDialog<MessageDlg>(() => SkylineWindow.ShowPublishDlg(publishClient));
            var expectedError = CommonTextUtil.LineSeparate(
                string.Format(
                    SkylineResources
                        .SkylineWindow_ShowPublishDlg__0__is_not_a_valid_file_name_for_uploading_to_Panorama_,
                    badFileName),
                string.Format(Resources.Error___0_,
                    PanoramaClient.Properties.Resources.PanoramaUtil_LabKeyAllowedFileName_File_name_may_not_contain_space_followed_by_dash));
            Assert.AreEqual(expectedError, errorDlg.Message);
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

        private static void TestFailAtFileUpload()
        {
            var publishClient = CreateTestClient(ERROR_UPLOAD_FILE);
            var errorDlg = PublishError(publishClient, out var shareZipFileName);
            Assert.AreEqual(FailOnFileUploadRequestHelper.GetExpectedError(GetTempZipFileName(shareZipFileName)),
                errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private static void TestFailAtMethod(RequestType requestType)
        {
            var publishClient = CreateTestClient((int)requestType);
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
            var publishClient = CreateTestClient(errorCode);
            var errorDlg = PublishError(publishClient, out _);
            Assert.AreEqual(expectedError, errorDlg.Message);
            OkDialog(errorDlg, errorDlg.OkDialog);
        }

        private static void TestSuccessfulUpload()
        {
            var publishDialog = ShowDialog<PublishDocumentDlgPanorama>(() => SkylineWindow.ShowPublishDlg(CreateTestClient()));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
            var docUploadedDlg = ShowDialog<MultiButtonMsgDlg>(shareTypeDlg.OkDialog);
            OkDialog(docUploadedDlg, docUploadedDlg.ClickNo);
        }

        private void TestUserCancelRetrievingFolders()
        {
            TestHttpClientCancellation(() => SkylineWindow.ShowPublishDlg(CreateWebClient()));
        }

        private void TestNoNetworkRetrievingFolders()
        {
            TestHttpClientWithNoNetwork(() => SkylineWindow.ShowPublishDlg(CreateWebClient()),
                TextUtil.LineSeparate(FileUIResources.PublishDocumentDlg_PublishDocumentDlgLoad_Failed_attempting_to_retrieve_information_from_the_following_servers_,
                    string.Empty,
                    PANORAMA_SERVER));
            // Dialog should not have been shown after the error
            var publishDialog = FindOpenForm<PublishDocumentDlgPanorama>();
            Assert.IsNull(publishDialog, "PublishDocumentDlgPanorama should not be shown after folder loading failure");
        }

        /// <summary>
        /// Verifies that HTTP 401 Unauthorized during EnsureLogin (before folder retrieval) shows appropriate authentication error.
        /// </summary>
        private void TestAuthenticationErrorRetrievingFolders()
        {
            using var helper = HttpClientTestHelper.SimulateHttp401();
            var ensureLoginUri = PanoramaUtil.GetEnsureLoginUri(new PanoramaServer(new Uri(PANORAMA_SERVER), USER_NAME, PASSWORD));
            
            TestMessageDlgShownContaining(
                () => SkylineWindow.ShowPublishDlg(CreateWebClient()),
                FileUIResources.PublishDocumentDlg_PublishDocumentDlgLoad_Failed_attempting_to_retrieve_information_from_the_following_servers_,
                PANORAMA_SERVER,
                PanoramaClient.Properties.Resources.UserState_GetErrorMessage_The_username_and_password_could_not_be_authenticated_with_the_panorama_server_,
                FileUIResources.PublishDocumentDlg_PublishDocumentDlgLoad_Go_to_Tools___Options___Panorama_tab_to_update_the_username_and_password_);
            
            // Dialog should not have been shown after the error
            var publishDialog = FindOpenForm<PublishDocumentDlgPanorama>();
            Assert.IsNull(publishDialog, "PublishDocumentDlgPanorama should not be shown after authentication failure");
        }

        private void TestUserCancelDuringUpload()
        {
            var shareTypeDlg = StartUploadToPanorama();
            TestHttpClientCancellation(shareTypeDlg.OkDialog);
        }

        private void TestNoNetworkDuringUpload()
        {
            var shareTypeDlg = StartUploadToPanorama();
            TestHttpClientWithNoNetworkEx(shareTypeDlg.OkDialog, 
                string.Format(PanoramaClient.Properties.Resources.AbstractPanoramaClient_GetWebDavPath_There_was_an_error_getting_the_WebDAV_url_for_folder___0__, PANORAMA_FOLDER + '/'));
        }

        /// <summary>
        /// Verifies that HTTP 403 Forbidden during WebDAV folder info retrieval shows appropriate permission error.
        /// This happens when getting the WebDAV URL before upload.
        /// </summary>
        private void TestPermissionErrorDuringDownload()
        {
            var uri = PanoramaUtil.GetPipelineContainerUrl(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER);
            var expectedError = string.Format(
                PanoramaClient.Properties.Resources.AbstractPanoramaClient_GetWebDavPath_There_was_an_error_getting_the_WebDAV_url_for_folder___0__,
                PANORAMA_FOLDER + '/');
            
            var shareTypeDlg = StartUploadToPanorama();
            using var helper = HttpClientTestHelper.SimulateHttp403();
            TestMessageDlgShownContaining(shareTypeDlg.OkDialog, expectedError, helper.GetExpectedMessage(uri));
        }

        private ShareTypeDlg StartUploadToPanorama()
        {
            // Use a web client to test a failure at the HttpClientWithProgress level
            var publishDialog = ShowDialog<PublishDocumentDlgPanorama>(() =>
                SkylineWindow.ShowPublishDlg(CreateTestClient(ERROR_HTTP_CLIENT)));
            WaitForCondition(() => publishDialog.IsLoaded);
            RunUI(() => { publishDialog.SelectItem(PANORAMA_FOLDER); });
            return ShowDialog<ShareTypeDlg>(publishDialog.OkDialog);
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
            return new BaseTestPanoramaClient.PanoramaFolder(name, write, targeted).ToJson();
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

            public TestLabKeyErrorPanoramaClient(Uri serverUri, string username, string password) 
                : this(serverUri, username, password, NO_ERROR)
            {
            }

            public TestLabKeyErrorPanoramaClient(Uri serverUri, string username, string password, int errorType) : base(serverUri, username, password)
            {
                _errorType = errorType;
            }

            public override JToken GetInfoForFolders(string folder, IProgressMonitor progressMonitor, IProgressStatus progressStatus)
            {
                var testFolders = BuildFolderJson("LabKeyErrorsTest", true, false);
                testFolders["children"] = new JArray(BuildFolderJson(PANORAMA_FOLDER, true, true));
                return testFolders;
            }

            public override void DownloadFile(string fileUrl, string fileName, long fileSize, string realName, IProgressMonitor progressMonitor,
                IProgressStatus progressStatus)
            {
                throw new InvalidOperationException();
            }

            public override JObject SupportedVersionsJson()
            {
                var obj = new JObject();
                obj["SKYD_version"] = ChromatogramCache.FORMAT_VERSION_CACHE.ToString();
                return obj;
            }

            protected override Uri ValidateUri(Uri serverUri, bool tryNewProtocol = true)
            {
                return serverUri;
            }

            protected override PanoramaServer ValidateServerAndUser(Uri serverUri, string username, string password)
            {
                return new PanoramaServer(serverUri, username, password);
            }

            public override PanoramaServer EnsureLogin(PanoramaServer pServer)
            {
                return new PanoramaServer(pServer.URI, pServer.Username, pServer.Password);
            }

            public override IRequestHelper GetRequestHelper()
            {
                IRequestHelper requestHelper = _errorType switch
                {
                    ERROR_BAD_JSON => new NoJsonResponseRequestHelper(),
                    ERROR_UPLOAD_FILE => new FailOnFileUploadRequestHelper(),
                    ERROR_HEAD_REQUEST => new FailOnDoRequestRequestHelper(RequestType.HEAD),
                    ERROR_MOVE_REQUEST => new FailOnDoRequestRequestHelper(RequestType.MOVE),
                    ERROR_SUBMIT_PIPELINE_JOB => new FailOnSubmitPipelineJobRequestHelper(),
                    ERROR_CHECK_JOB_STATUS => new FailOnCheckJobStatusRequestHelper(),
                    // Use an HttpPanoramaRequestHelper and expect the HttpClientWithProgress class to throw the exceptions
                    ERROR_HTTP_CLIENT => new HttpPanoramaRequestHelper(new PanoramaServer(ServerUri, Username, Password), _progressMonitor, _progressStatus),
                    _ => new TestRequestHelper()
                };
                return requestHelper;
            }
        }

        public class NoJsonResponseRequestHelper : TestRequestHelper
        {
            private const string NOT_JSON = @"<head><body>This is not JSON</body></head>";
            public override string DoGet(Uri uri)
            {
                return NOT_JSON;
            }

            public static string GetExpectedError()
            {
                return new ErrorMessageBuilder(PanoramaClient.Properties.Resources
                        .AbstractRequestHelper_ParseJsonResponse_Error_parsing_response_as_JSON_)
                    .ExceptionMessage(@"Unexpected character encountered while parsing value: <. Path '', line 0, position 0.")
                    .Uri(PanoramaUtil.GetPipelineContainerUrl(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER))
                    .Response(NOT_JSON)
                    .ToString();
            }
        }

        public class FailOnDoRequestRequestHelper : TestRequestHelper
        {
            private readonly RequestType _requestMethod;
            private static readonly string MSG_EXCEPTION = "Testing exception on {0} request";
            private static readonly string MSG_LABKEY_ERR = "This is the LabKey error on {0} request";
            // ReSharper disable once NotAccessedField.Local
            private LabKeyError _labkeyError;
            public FailOnDoRequestRequestHelper(RequestType requestMethod)
            {
                _requestMethod = requestMethod;
            }
            public override string GetResponse(Uri uri, string method, IDictionary<string, string> headers = null)
            {
                if (method.Equals(_requestMethod.ToString()))
                {
                    _labkeyError = new LabKeyError(string.Format(MSG_LABKEY_ERR, method), null);
                    var exceptionMessage = string.Format(MSG_EXCEPTION, method);
                    var labKeyErrorMessage = string.Format(MSG_LABKEY_ERR, method);
                    
                    throw CreateNetworkRequestExceptionWithLabKeyError(exceptionMessage, labKeyErrorMessage, uri);
                }

                return base.GetResponse(uri, method, headers);
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
                        mainError = PanoramaClient.Properties.Resources.AbstractPanoramaClient_RenameTempZipFile_There_was_an_error_renaming_the_temporary_zip_file_on_the_server_;
                        break;
                    default:
                        mainError = "UNKNOWN ERROR";
                        break;
                }

                var uri = new Uri(new Uri(PANORAMA_SERVER), GetFolderWebdavUrl(PANORAMA_FOLDER).TrimStart('/') + sharedZipFile);
                // When LabKey error exists, don't include the exception message (cleaner for users)
                // The LabKey error + status code provide all the information needed
                return new ErrorMessageBuilder(mainError)
                    .LabKeyError(new LabKeyError(string.Format(MSG_LABKEY_ERR, requestType), 500))
                    .Uri(uri)
                    .ToString();
            }
        }

        public class FailOnFileUploadRequestHelper : TestRequestHelper
        {
            public static LabKeyError GetLabKeyError()
            {
                return new LabKeyError("Couldn't create file on server", 500);
            }

            public static string GetExpectedError(string tempShareZipFile)
            {
                var uri = new Uri(new Uri(PANORAMA_SERVER), GetFolderWebdavUrl(PANORAMA_FOLDER).TrimStart('/') + tempShareZipFile);

                return new ErrorMessageBuilder(PanoramaClient.Properties.Resources
                        .AbstractPanoramaClient_UploadTempZipFile_There_was_an_error_uploading_the_file_)
                    .LabKeyError(GetLabKeyError())
                    .Uri(uri)
                    .ToString();
            }

            public override void DoAsyncFileUpload(Uri address, string method, string fileName, IDictionary<string, string> headers = null)
            {
                // For HttpPanoramaRequestHelper, we need to actually throw an exception with the LabKey error
                // Create a JSON response with the LabKey error
                var errorJson = new JObject
                {
                    ["exception"] = GetLabKeyError().ErrorMessage,
                    ["status"] = GetLabKeyError().Status
                };
                
                // Throw NetworkRequestException with the error response body
                // Use the LabKey error message directly - ErrorMessageBuilder will format it
                throw new NetworkRequestException(
                    GetLabKeyError().ErrorMessage,
                    (HttpStatusCode)GetLabKeyError().Status.Value,
                    address,
                    new HttpRequestException(GetLabKeyError().ErrorMessage),
                    errorJson.ToString());
            }
        }

        // TODO: Test NetworkRequestException vs no exception but exception in JSON.
        public class FailOnSubmitPipelineJobRequestHelper : TestRequestHelper
        {
            private static readonly string exceptionMessage = "Exception adding to the document import queue.";
            private static readonly string labkeyError = "This is the LabKey error.";

            public override byte[] DoPost(Uri uri, NameValueCollection postData)
            {
                if (uri.Equals(PanoramaUtil.GetImportSkylineDocUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER)))
                {
                    throw CreateNetworkRequestExceptionWithLabKeyError(exceptionMessage, labkeyError, uri);
                }
                return base.DoPost(uri, postData);
            }

            public static string GetExpectedError()
            {
                // When LabKey error exists, don't include the exception message (cleaner for users)
                return new ErrorMessageBuilder(PanoramaClient.Properties.Resources
                        .AbstractPanoramaClient_QueueDocUploadPipelineJob_There_was_an_error_adding_the_document_import_job_on_the_server_)
                    .LabKeyError(new LabKeyError(labkeyError, 500))
                    .Uri(PanoramaUtil.GetImportSkylineDocUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER))
                    .ToString();
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
                    throw CreateNetworkRequestExceptionWithLabKeyError(exceptionMessage, labkeyError, uri);
                }

                return base.DoGet(uri);
            }

            public static string GetExpectedError()
            {
                // When LabKey error exists, don't include the exception message (cleaner for users)
                return new ErrorMessageBuilder(PanoramaClient.Properties.Resources
                        .AbstractPanoramaClient_WaitForDocumentImportCompleted_There_was_an_error_getting_the_status_of_the_document_import_pipeline_job_)
                    .LabKeyError(new LabKeyError(labkeyError, 500))
                    .Uri(PanoramaUtil.GetPipelineJobStatusUri(new Uri(PANORAMA_SERVER), PANORAMA_FOLDER,
                        PIPELINE_JOB_ID))
                    .ToString();
            }
        }

        /// <summary>
        /// Helper method to create a NetworkRequestException with LabKey error JSON response.
        /// Used by test helpers to simulate server errors that HttpClientWithProgress would receive.
        /// </summary>
        private static NetworkRequestException CreateNetworkRequestExceptionWithLabKeyError(
            string exceptionMessage, string labKeyErrorMessage, Uri uri)
        {
            // Create a JSON response body with LabKey error info (as HttpClientWithProgress would receive)
            var errorJson = new JObject
            {
                {@"exception", labKeyErrorMessage},  // LabKey returns the error message in "exception" field
                {@"success", false},
                {@"status", 500}
            };
            
            return new NetworkRequestException(
                exceptionMessage,
                HttpStatusCode.InternalServerError,
                uri,
                new HttpRequestException(exceptionMessage),
                errorJson.ToString());
        }

        public class TestRequestHelper : AbstractRequestHelper
        {
            // private UploadFileCompletedEventHandler _uploadCompletedEventHandler;
            // private UploadProgressChangedEventHandler _uploadProgressChangedEventHandler;

            public override void SetProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
            {
                // Test helper - no-op for now
            }

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

            public override void AddHeader(HttpRequestHeader header, string value)
            {
            }

            public override void DoAsyncFileUpload(Uri address, string method, string fileName, IDictionary<string, string> headers = null)
            {
                Thread.Sleep(1000);
            }

            public override string GetResponse(Uri uri, string method, IDictionary<string, string> headers = null)
            {
                return string.Empty;
            }

            public override void Dispose()
            {
            }

            public override string DoPost(Uri uri, string postData)
            {
                throw new InvalidOperationException(); 
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
