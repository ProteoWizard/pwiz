/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using static pwiz.Skyline.FileUI.PublishDocumentDlgArdia;
using static pwiz.SkylineTestUtil.ArdiaTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public class ArdiaFileUploadTest : AbstractFunctionalTestEx
    {
        private const string TEST_RESULTS_DIRECTORY_NAME = @"ZZZ-Skyline";

        [TestMethod]
        public void TestArdiaFileUpload()
        {
            if (!EnableArdiaTests)
            {   
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            RunFunctionalTest();
        }

        // TODO: is there an API to verify Ardia account has permissions required to run test? Ex: upload files, delete files
        protected override void DoTest()
        {
            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            // Test setup - configure Ardia account
            var account = GetTestAccount(AccountType.SingleRole);

            OpenDocument("Basic.sky");

            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[0];
            AssertEx.IsTrue(!string.IsNullOrEmpty(account.Token));

            // Test scenarios 
            TestGetFolderPath();
            TestValidateFolderName();
            TestAccountHasCredentials(account);
            TestCreateArdiaError();

            Test_StageDocument_Request_SinglePart();
            Test_StageDocument_Request_MultiPart();
            Test_StageDocument_Response();

            // Test scenarios making remote API calls
            var setupClient = ArdiaClient.Create(account);

            // TODO: oof. Would be nice to have an abstraction for dealing with folder paths
            var folderName = $@"TestResults-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            var parentPath = $@"/{TEST_RESULTS_DIRECTORY_NAME}";
            var testResultsPath = $@"{parentPath}/{folderName}";

            // Create a folder holding results from this test run
            setupClient.CreateFolder(parentPath, folderName, null, out var error);
            Assert.IsNull(error);

            Test_ArdiaClient_GetFolders(account);
            Test_ArdiaClient_CreateFolder(account, testResultsPath);

            TestSuccessfulUpload(account, new[] { TEST_RESULTS_DIRECTORY_NAME, folderName });

            // TODO: keep directory if 1+ tests fail
            // Delete directory for this test run - disabled for now
            // finally
            // {
            //     setupClient.DeleteFolder(testResultsPath);
            // }
        }

        private static void Test_StageDocument_Response()
        {
            var stagedDocumentResponse = StagedDocumentResponse.FromJson(RESPONSE_JSON_CREATE_STAGED_DOCUMENT);

            Assert.AreEqual("97088887-788079", stagedDocumentResponse.UploadId);

            var piece = stagedDocumentResponse.Pieces[0];
            Assert.IsNotNull(stagedDocumentResponse.Pieces);
            Assert.AreEqual(1, stagedDocumentResponse.Pieces.Count);
            Assert.IsNotNull(piece);
            Assert.AreEqual(StageDocumentRequest.SINGLE_DOCUMENT, piece.PieceName);
            Assert.AreEqual("/foobar", piece.PiecePath);

            var presignedUrls = piece.PresignedUrls;
            Assert.IsNotNull(presignedUrls);
            Assert.AreEqual("https://www.example.com/", presignedUrls[0]);
        }

        private static void Test_StageDocument_Request_MultiPart()
        {
            var requestModel = StageDocumentRequest.Create();
            requestModel.AddPiece(@"foobar.sky.z01", 1024, 1024, true);
            requestModel.AddPiece(@"foobar.sky.z02", 2048, 3072, true);
            requestModel.AddPiece(@"foobar.sky.zip", 4096, 7168, true);

            Assert.IsNotNull(requestModel.Pieces);
            Assert.AreEqual(3, requestModel.Pieces.Count);
            Assert.AreEqual(@"foobar.sky.z01", requestModel.Pieces[0].PieceName);
            Assert.AreEqual(1024, requestModel.Pieces[0].PartSize);
            Assert.AreEqual(1024, requestModel.Pieces[0].Size);
            Assert.IsTrue(requestModel.Pieces[0].IsMultiPart);

            var json = requestModel.ToJson();
            Assert.AreEqual(@"{""Pieces"":[{""PieceName"":""foobar.sky.z01"",""PartSize"":1024,""Size"":1024,""IsMultiPart"":true},{""PieceName"":""foobar.sky.z02"",""PartSize"":2048,""Size"":3072,""IsMultiPart"":true},{""PieceName"":""foobar.sky.zip"",""PartSize"":4096,""Size"":7168,""IsMultiPart"":true}]}", json);
        }

        private static void Test_StageDocument_Request_SinglePart()
        {
            // Model used for upload of Skyline documents < 2GB 
            var requestModel = StageDocumentRequest.CreateSinglePieceDocument();

            Assert.IsNotNull(requestModel);
            Assert.IsNotNull(requestModel.Pieces);
            Assert.AreEqual(1, requestModel.Pieces.Count);
            Assert.AreEqual(StageDocumentRequest.SINGLE_DOCUMENT, requestModel.Pieces[0].PieceName);
            Assert.IsFalse(requestModel.Pieces[0].IsMultiPart);

            var json = requestModel.ToJson();

            // Properties with default values should not be included in serialized JSON
            Assert.AreEqual(@"{""Pieces"":[{""PieceName"":""[SingleDocument]""}]}", json);
        }

        private static void TestCreateArdiaError()
        {
            var message = ArdiaError.ReadErrorFromResponse(ERROR_MESSAGE_JSON);
            Assert.AreEqual(@"Item is Archived or Path already exists.", message);

            message = ArdiaError.ReadErrorFromResponse(ERROR_MESSAGE_XML);
            Assert.AreEqual(@"Your proposed upload exceeds the maximum allowed size", message);

            message = ArdiaError.ReadErrorFromResponse(@"Neither XML nor JSON.");
            Assert.AreEqual(@"Neither XML nor JSON.", message);

            var error = ArdiaError.Create(null, ERROR_MESSAGE_JSON);
            Assert.AreEqual(@"Item is Archived or Path already exists.", error.Message);

            error = ArdiaError.Create(null, ERROR_MESSAGE_XML);
            Assert.AreEqual(@"Your proposed upload exceeds the maximum allowed size", error.Message);

            error = ArdiaError.Create(null, @"Neither XML nor JSON.");
            Assert.AreEqual(@"Neither XML nor JSON.", error.Message);
        }

        private static void TestAccountHasCredentials(ArdiaAccount account)
        {
            var ardiaClient = ArdiaClient.Create(account);

            // Verify values needed to authenticate this account are available. These
            // asserts leak implementation details but are useful to avoid chasing 
            // test failures.
            Assert.IsNotNull(ArdiaCredentialHelper.GetApplicationCode(account));
            Assert.IsNotNull(ArdiaCredentialHelper.GetToken(account));

            Assert.IsTrue(ardiaClient.HasCredentials);
        }

        private static void TestValidateFolderName()
        {
            var result = ValidateFolderName(@"New Folder");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"A");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"ABCDEFGHIJKLMNOPQURSTUVWXYZabcedefhijklmnopqurstuvwxyz0123456789 -_");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"-----");
            Assert.AreEqual(ValidateInputResult.valid, result);
            
            result = ValidateFolderName(@"_____");
            Assert.AreEqual(ValidateInputResult.valid, result);

            result = ValidateFolderName(@"    ");
            Assert.AreEqual(ValidateInputResult.invalid_blank, result);

            result = ValidateFolderName(@" ");
            Assert.AreEqual(ValidateInputResult.invalid_blank, result);

            result = ValidateFolderName(@"New Folder <");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);

            result = ValidateFolderName(@"New :: Folder");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);

            result = ValidateFolderName(@"*New? Folder");
            Assert.AreEqual(ValidateInputResult.invalid_character, result);
        }

        private static void TestGetFolderPath()
        {
            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());
            var pathForTreeNode = publishDlg.GetFolderPath(@"ardia:server\Folder2\Folder3\Folder4\");

            Assert.AreEqual(@"/Folder2/Folder3/Folder4", pathForTreeNode);

            RunUI(() => publishDlg.Close());
            WaitForClosedForm(publishDlg);
        }

        private static void Test_ArdiaClient_GetFolders(ArdiaAccount account)
        {
            var ardiaClient = ArdiaClient.Create(account);

            // Successful if no exception thrown
            ardiaClient.GetFolders(account.GetRootArdiaUrl(), null, out var ardiaError);
            Assert.IsNull(ardiaError);
        }

        private static void Test_ArdiaClient_CreateFolder(ArdiaAccount account, string path)
        {
            var ardiaClient = ArdiaClient.Create(account);

            ardiaClient.CreateFolder(path, @"NewFolder01", null, out var serverError);
            Assert.IsNull(serverError);

            // Error - attempt to create folder with same name
            ardiaClient.CreateFolder(path, @"NewFolder01", null, out serverError);
            Assert.IsNotNull(serverError);
            Assert.AreEqual(HttpStatusCode.Conflict, serverError.StatusCode);

            ardiaClient.DeleteFolder(@$"{path}/NewFolder01");
        }

        private static void TestSuccessfulUpload(ArdiaAccount account, string[] folderPath) 
        {
            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());

            WaitForConditionUI(() => publishDlg.IsLoaded);

            RunUI(() =>
            {
                publishDlg.FoldersTree.Nodes[0].Expand();

                var treeNode = publishDlg.FindByName(publishDlg.FoldersTree.Nodes[0].Nodes, folderPath[0]);

                Assert.IsNotNull(treeNode, @$"Did not find TreeNode with text '{folderPath[0]}'.");

                treeNode.Expand();

                publishDlg.SelectItem(folderPath[1]);
            });

            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDlg.OkDialog);
            var docUploadedDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);
            OkDialog(docUploadedDlg, docUploadedDlg.ClickOk);

            Assert.AreEqual(@$"/{folderPath[0]}/{folderPath[1]}", publishDlg.DestinationPath);

            // Use new document's ID to make another API call checking document can be read from the server
            var documentId = publishDlg.PublishedDocument.DocumentId;

            var ardiaClient = ArdiaClient.Create(account);
            var ardiaDocument = ardiaClient.GetDocument(documentId);
            Assert.IsNotNull(ardiaDocument);
            Assert.AreEqual(documentId, ardiaDocument.DocumentId);
        }

        private static void RegisterRemoteServer(ArdiaAccount account) 
        {
            Assert.IsNotNull(account);

            var optionsDlg = ShowDialog<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Remote));
            var editRemoteAccountListDlg = ShowDialog<EditListDlg<SettingsListBase<RemoteAccount>, RemoteAccount>>(() => optionsDlg.EditRemoteAccounts());

            var addAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => editRemoteAccountListDlg.AddItem());
            RunUI(() => addAccountDlg.SetRemoteAccount(account));

            var testSuccessfulDlg = ShowDialog<MessageDlg>(() => addAccountDlg.TestSettings());
            OkDialog(testSuccessfulDlg, testSuccessfulDlg.OkDialog);
            OkDialog(addAccountDlg, addAccountDlg.OkDialog);
            OkDialog(editRemoteAccountListDlg, editRemoteAccountListDlg.OkDialog);
            OkDialog(optionsDlg, optionsDlg.OkDialog);
        }

        /// <summary>
        /// Example JSON error - returned by Ardia when trying to create a new folder
        /// </summary>
        private const string ERROR_MESSAGE_JSON = "{\"title\":\"Item is Archived or Path already exists.\",\"status\":409}\r\n";

        /// <summary>
        /// Example XML error - return by AWS when upload size exceeds 5GB
        /// </summary>
        private const string ERROR_MESSAGE_XML = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>EntityTooLarge</Code><Message>Your proposed upload exceeds the maximum allowed size</Message><ProposedSize>6451738112</ProposedSize><MaxSizeAllowed>5368709120</MaxSizeAllowed><RequestId>...string...</RequestId><HostId>...string...</HostId></Error>";

        private const string RESPONSE_JSON_CREATE_STAGED_DOCUMENT = "{pieces: [ { pieceName: \"[SingleDocument]\", piecePath: \"/foobar\", presignedUrls: [\"https://www.example.com/\"]} ], uploadId: \"97088887-788079\" }";
    }
}