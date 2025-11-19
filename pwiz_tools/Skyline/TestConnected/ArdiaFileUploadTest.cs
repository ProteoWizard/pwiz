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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Windows.Forms;
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
        private const string TEST_RESULTS_DIRECTORY = @"ZZZ-Skyline-TestResults";

        private AccountType _accountType;

        [TestMethod]
        public void TestArdiaFileUpload()
        {
            if (!EnableArdiaTests)
            {   
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            _accountType = AccountType.SingleRole;
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestArdiaFileUploadMultiRole()
        {
            if (!EnableArdiaTests)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because username/password for Ardia is not configured in environment variables");
                return;
            }

            TestFilesZip = @"TestConnected\ArdiaFileUploadTest.zip";

            _accountType = AccountType.MultiRole;
            RunFunctionalTest();
        }

        // TODO: is there an API to verify Ardia account has permissions required to run test? Ex: upload files, delete files
        protected override void DoTest()
        {
            //
            // Model tests
            //
            TestAvailableStorageLabel();
            TestValidateFolderName();
            TestCreateArdiaError();
            TestDefaultPartSize();
            Test_StageDocument_Request_SmallDocument();
            Test_StageDocument_Request_LargeDocument();
            Test_StageDocument_Response();
            Test_StageDocument_Request_CompleteMultipartUpload();

            //
            // Setup for UI, API, and end-to-end tests
            //
            Assert.IsFalse(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(0, Settings.Default.RemoteAccountList.Count);

            var account = GetTestAccount(_accountType);

            OpenDocument("Basic.sky");

            RegisterRemoteServer(account);
            Assert.IsTrue(SkylineWindow.HasRegisteredArdiaAccount);
            Assert.AreEqual(1, Settings.Default.RemoteAccountList.Count);

            account = (ArdiaAccount)Settings.Default.RemoteAccountList[0];
            AssertEx.IsTrue(!account.Token.IsNullOrEmpty(), "Ardia account does not have a token.");

            //
            // UI tests
            //
            TestAccountHasCredentials(account);
            TestGetFolderPath();

            //
            // API tests
            //
            var client = ArdiaClient.Create(account);

            // Make sure (1) the Ardia server is available and (2) the token can be used to call the API.
            // Better to fail a specific check now than to fail a network call when the cause is less obvious.
            if (client.CheckSession().IsFailure)
            {
                Console.Error.WriteLine("NOTE: skipping Ardia test because the server {0} is unavailable", account.ServerUrl);
                Assert.Fail(@"Ardia server {0} is unavailable. Failing the test.", account.ServerUrl);
                return;
            }

            // CONSIDER: a folder path abstraction would be useful here...
            // Create a folder for this test run
            var folderName = $@"TestResults-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            var parentPath = $@"/{TEST_RESULTS_DIRECTORY}";
            var testResultsPath = $@"{parentPath}/{folderName}";

            var result = client.CreateFolder(parentPath, folderName, null);
            Assert.IsTrue(result.IsSuccess);

            //
            // ArdiaClient tests
            //
            Test_ArdiaClient_GetFolders(account, client);
            Test_ArdiaClient_CreateFolder(account, client, testResultsPath);
            Test_ArdiaClient_GetStorageInfo(account, client);

            //
            // End-to-end UI tests, including PublishDocumentDlgArdia
            //
            TestSuccessfulUpload(account, client, new[] { TEST_RESULTS_DIRECTORY, folderName });

            // Multipart upload with max part size = 5MB
            // TODO: make sure the TemporaryDirectory created during upload is cleaned up
            OpenDocument("MOBIE Quant Oct 2024_2024-10-09_13-20-22\\MOBIE Quant Oct 2024.sky");
            TestSuccessfulUpload(account, client, new[] { TEST_RESULTS_DIRECTORY, folderName }, 5);

            // TODO: CreateFolder test
            // TestCreateFolder(account, client, new[] {TEST_RESULTS_DIRECTORY, folderName, @"NewFolder01"});
            // TestSuccessfulUpload(account, client, new[] { TEST_RESULTS_DIRECTORY, folderName, @"NewFolder01" });

            //
            // Cleanup remote server data created during this test run
            //
            // TODO: keep directory if 1+ tests fail
            // Delete test result folder, keep if 1+ tests fail (disabled for now)
            // finally
            // {
            //     client.DeleteFolder(testResultsPath);
            // }
        }
        
        private static void TestAvailableStorageLabel()
        {
            const string jsonUnlimitedStorage = @"{}";
            var model = StorageInfoResponse.Create(jsonUnlimitedStorage);
            Assert.IsTrue(model.IsUnlimited);
            Assert.IsNull(model.TotalSpace);
            Assert.IsNull(model.AvailableFreeSpace);
            Assert.IsTrue(model.HasAvailableStorageFor(long.MaxValue));
            Assert.AreEqual(ArdiaResources.FileUpload_AvailableFreeSpace_Unlimited, model.AvailableFreeSpaceLabel);

            const string jsonLimitedStorage = @"{""totalSpace"":86056948916224,""availableFreeSpace"":74475300380672}";
            model = StorageInfoResponse.Create(jsonLimitedStorage);
            Assert.IsFalse(model.IsUnlimited);
            Assert.AreEqual(86056948916224, model.TotalSpace);
            Assert.AreEqual(74475300380672, model.AvailableFreeSpace);
            Assert.IsTrue(model.HasAvailableStorageFor(100000));
            Assert.IsFalse(model.HasAvailableStorageFor(999999999999999));

            // For now, only checking EN to have a check for formatting a number into label text
            if(IsLanguageEN())
                Assert.AreEqual(@"Available Storage: 67.73 TB", model.AvailableFreeSpaceLabel);

            const string jsonZeros = @"{""totalSpace"":0,""availableFreeSpace"":0}";
            model = StorageInfoResponse.Create(jsonZeros);
            Assert.IsFalse(model.IsUnlimited);
            Assert.AreEqual(0, model.TotalSpace);
            Assert.AreEqual(0, model.AvailableFreeSpace);

            const string jsonSmallNumbers = @"{""totalSpace"":11,""availableFreeSpace"":2}";
            model = StorageInfoResponse.Create(jsonSmallNumbers);
            Assert.IsFalse(model.IsUnlimited);
            Assert.AreEqual(11, model.TotalSpace);
            Assert.AreEqual(2, model.AvailableFreeSpace);

            // For now, only checking EN because this tests the underlying math and not language-specific formatting
            if (IsLanguageEN())
            {
                // Test formatting for all supported sizes
                Assert.AreEqual(@"0 B", StorageInfoResponse.AvailableBytesToString(0));
                Assert.AreEqual(@"768 B", StorageInfoResponse.AvailableBytesToString(768));
                Assert.AreEqual(@"750 KB", StorageInfoResponse.AvailableBytesToString(768204));
                Assert.AreEqual(@"732.62 MB", StorageInfoResponse.AvailableBytesToString(768204283));
                Assert.AreEqual(@"715.45 GB", StorageInfoResponse.AvailableBytesToString(768204283985));
                Assert.AreEqual(@"67.73 TB", StorageInfoResponse.AvailableBytesToString(74475300380672));
                Assert.AreEqual(@"698.68 TB", StorageInfoResponse.AvailableBytesToString(768204283985100));
                Assert.AreEqual(@"682.30 PB", StorageInfoResponse.AvailableBytesToString(768204283985100234));
                Assert.AreEqual(@"8,192.00 PB", StorageInfoResponse.AvailableBytesToString(long.MaxValue));
            }
        }

        private void Test_StageDocument_Request_CompleteMultipartUpload()
        {
            var eTags = new List<string> { "673889e3e0ea", "b24ef41f1022", "d37f2f3c36f3", "8082d9440527" };
            var model = CompleteMultiPartUploadRequest.Create("2025/07/02/2cae3", "KbTcKnGXt6.asdfeqef--", eTags);

            Assert.AreEqual("2025/07/02/2cae3", model.StoragePath);
            Assert.AreEqual("KbTcKnGXt6.asdfeqef--", model.MultiPartId);
            Assert.AreEqual(4, model.PartTags.Count);

            // Test first and last PartTag and check PartNumber is correct since its numbering starts at 1
            Assert.AreEqual(1, model.PartTags[0].PartNumber);
            Assert.AreEqual("673889e3e0ea", model.PartTags[0].ETag);
            Assert.AreEqual(4, model.PartTags[3].PartNumber);
            Assert.AreEqual("8082d9440527", model.PartTags[3].ETag);

            // var json = model.ToJson();
            // Assert.AreEqual(REQUEST_JSON_COMPLETE_MULTIPART_UPLOAD, json);
        }

        /// <summary>
        /// Check the default part size of a multipart upload.
        /// </summary>
        private static void TestDefaultPartSize()
        {
            Assert.IsTrue(ArdiaClient.IsValidPartSize(ArdiaClient.DEFAULT_PART_SIZE_MB));
            Assert.AreEqual(ArdiaClient.DEFAULT_PART_SIZE_BYTES, ArdiaClient.DEFAULT_PART_SIZE_MB * 1024 * 1024);
        }

        private static void Test_StageDocument_Response()
        {
            var stagedDocumentResponse = StagedDocumentResponse.FromJson(RESPONSE_JSON_CREATE_STAGED_DOCUMENT);

            Assert.AreEqual("97088887-788079", stagedDocumentResponse.UploadId);

            var piece = stagedDocumentResponse.Pieces[0];
            Assert.IsNotNull(stagedDocumentResponse.Pieces);
            Assert.AreEqual(1, stagedDocumentResponse.Pieces.Count);
            Assert.IsNotNull(piece);
            Assert.AreEqual(StageDocumentRequest.DEFAULT_PIECE_NAME, piece.PieceName);
            Assert.AreEqual("/foobar", piece.StoragePath);

            var presignedUrls = piece.PresignedUrls;
            Assert.IsNotNull(presignedUrls);
            Assert.AreEqual("https://www.example.com/", presignedUrls[0]);
        }

        private static void Test_StageDocument_Request_SmallDocument()
        {
            const long fileSize = 8 * 1024; // 8K

            // Create model for 1-part Skyline document
            var requestModel = StageDocumentRequest.Create(fileSize, ArdiaClient.DEFAULT_PART_SIZE_MB);

            Assert.IsNotNull(requestModel);
            Assert.IsNotNull(requestModel.Pieces);
            Assert.AreEqual(1, requestModel.Pieces.Count);
            Assert.AreEqual(StageDocumentRequest.DEFAULT_PIECE_NAME, requestModel.Pieces[0].PieceName);
            Assert.IsFalse(requestModel.Pieces[0].IsMultiPart);

            // Make sure attributes with default values are omitted from serialized JSON
            var json = requestModel.ToJson();
            Assert.AreEqual(@"{""Pieces"":[{""PieceName"":""[SingleDocument]""}]}", json);
        }

        private static void Test_StageDocument_Request_LargeDocument()
        {
            const long fileSize = 6451738112; // > 6GB

            // Create model for multipart Skyline document
            var requestModel = StageDocumentRequest.Create(fileSize, ArdiaClient.DEFAULT_PART_SIZE_MB);

            Assert.IsNotNull(requestModel.Pieces);
            Assert.AreEqual(1, requestModel.Pieces.Count);

            Assert.IsTrue(requestModel.Pieces[0].IsMultiPart);
            Assert.AreEqual(StageDocumentRequest.DEFAULT_PIECE_NAME, requestModel.Pieces[0].PieceName);
            Assert.AreEqual(fileSize, requestModel.Pieces[0].Size);
            Assert.AreEqual(ArdiaClient.DEFAULT_PART_SIZE_MB, requestModel.Pieces[0].PartSize);

            var json = requestModel.ToJson();
            Assert.AreEqual($@"{{""Pieces"":[{{""PieceName"":""[SingleDocument]"",""IsMultiPart"":true,""Size"":6451738112,""PartSize"":{ArdiaClient.DEFAULT_PART_SIZE_MB}}}]}}", json);
        }

        private static void TestCreateArdiaError()
        {
            var message = ErrorMessageBuilder.ReadErrorMessageFromResponse(ERROR_MESSAGE_JSON);
            Assert.AreEqual(@"Item is Archived or Path already exists.", message);

            message = ErrorMessageBuilder.ReadErrorMessageFromResponse(ERROR_MESSAGE_XML);
            Assert.AreEqual(@"Your proposed upload exceeds the maximum allowed size", message);

            message = ErrorMessageBuilder.ReadErrorMessageFromResponse(@"Neither XML nor JSON.");
            Assert.AreEqual(string.Empty, message);
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

        private static void Test_ArdiaClient_GetFolders(ArdiaAccount account, ArdiaClient client)
        {
            var result = client.GetFolders(account.GetRootArdiaUrl(), null);
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Value);
        }

        private static void Test_ArdiaClient_CreateFolder(ArdiaAccount account, ArdiaClient client, string path)
        {
            var result = client.CreateFolder(path, @"NewFolder01", null);
            Assert.IsTrue(result.IsSuccess);

            // Error - attempt to create folder with same name
            result = client.CreateFolder(path, @"NewFolder01", null);
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(HttpStatusCode.Conflict, result.ErrorStatusCode);

            result = client.DeleteFolder(@$"{path}/NewFolder01");
            Assert.IsTrue(result.IsSuccess);
        }

        private void Test_ArdiaClient_GetStorageInfo(ArdiaAccount account, ArdiaClient client)
        {
            var storageInfo = client.GetServerStorageInfo();

            Assert.IsTrue(storageInfo.IsSuccess);
        }

        private static void TestSuccessfulUpload(ArdiaAccount account, ArdiaClient client, string[] folderPath, int? maxPartSizeMb = null) 
        {
            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());

            if (maxPartSizeMb != null)
            {
                publishDlg.MaxPartSize = maxPartSizeMb.Value;
            }

            WaitForConditionUI(() => publishDlg.IsLoaded);
            WaitForConditionUI(() => publishDlg.AvailableStorageLabel.Visible);

            RunUI(() =>
            {
                Assert.IsFalse(publishDlg.AnonymousServersCheckboxVisible);
                Assert.AreEqual(ArdiaResources.FileUpload_AvailableFreeSpace_Unlimited, publishDlg.AvailableStorageLabel.Text);

                publishDlg.FoldersTree.Nodes[0].Expand();

                var treeNode = publishDlg.FindByName(publishDlg.FoldersTree.Nodes[0].Nodes, folderPath[0]);

                Assert.IsNotNull(treeNode, @$"Did not find TreeNode with text '{folderPath[0]}'.");

                treeNode.Expand();

                publishDlg.SelectItem(folderPath[1]);
            });

            var shareTypeDlg = ShowDialog<ShareTypeDlg>(publishDlg.OkDialog);
            var docUploadedDlg = ShowDialog<MessageDlg>(shareTypeDlg.OkDialog);

            // CONSIDER: in this case, MessageDlg displays when publishing is successful AND when there's an error. 
            //           Calling OkDialog(...) works in both cases - which causes downstream, less obvious errors.
            //           Is there a better way to detect when a MessageDlg indicates an error without looking at 
            //           the message string?
            Assert.AreEqual(ArdiaResources.FileUpload_Success, docUploadedDlg.Message, @"Error publishing the document to Ardia");

            OkDialog(docUploadedDlg, docUploadedDlg.ClickOk);
            Assert.IsNotNull(publishDlg.DestinationPath);
            Assert.AreEqual(@$"/{folderPath[0]}/{folderPath[1]}", publishDlg.DestinationPath);

            // Use new document's ID to make another API call checking document can be read from the server
            Assert.IsNotNull(publishDlg.PublishedDocument);
            var documentId = publishDlg.PublishedDocument.DocumentId;

            var ardiaClient = ArdiaClient.Create(account);
            var result = ardiaClient.GetDocument(documentId);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Value);
            Assert.AreEqual(documentId, result.Value.DocumentId);
        }

        // See RenameProteinsTest for example of editing TreeNode's label
        private static void TestCreateFolder(ArdiaAccount account, ArdiaClient client, string[] folderPath)
        {
            var newFolderName = folderPath.Last();

            var publishDlg = ShowDialog<PublishDocumentDlgArdia>(() => SkylineWindow.PublishToArdia());
            WaitForConditionUI(() => publishDlg.IsLoaded);

            // Expand node for Ardia server
            RunUI(() => { publishDlg.FoldersTree.Nodes[0].Expand(); });

            // Expand each folder in the path
            RunUI(() =>
            {
                var treeNode = publishDlg.FindByName(publishDlg.FoldersTree.SelectedNode.Nodes, folderPath[0]);
                Assert.IsNotNull(treeNode, $@"Did not find expected TreeNode with text '{folderPath[0]}");
                treeNode.Expand();
            });

            RunUI(() =>
            {
                var treeNode = publishDlg.FindByName(publishDlg.FoldersTree.SelectedNode.Nodes, folderPath[1]);
                Assert.IsNotNull(treeNode, $@"Did not find expected TreeNode with text '{folderPath[1]}");
                treeNode.Expand();
            });

            // Create new folder
            RunUI(() =>
            {
                publishDlg.CreateFolder();

                // TODO: simulate editing the new tree node's label - likely needs P/Invoke

                var labelEditEventArgs = new NodeLabelEditEventArgs(publishDlg.FoldersTree.SelectedNode, newFolderName) { CancelEdit = false };
                publishDlg.TreeViewFolders_AfterLabelEdit(publishDlg.FoldersTree, labelEditEventArgs);

                var newFolderTreeNode = publishDlg.FindByName(publishDlg.FoldersTree.SelectedNode.Parent.Nodes, newFolderName);

                Assert.IsNotNull(newFolderTreeNode);
                Assert.AreEqual(newFolderName, newFolderTreeNode.Text);
            });

            // Make sure new folder is available
            // TODO: add folder IDs to TreeNode.Tag
            var folderId = string.Empty; // publishDlg.FoldersTree.SelectedNode;

            // var result = client.GetFolder(folderId);
        }

        public static void RegisterRemoteServer(ArdiaAccount account) 
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

        private static bool IsLanguageEN()
        {
            return Equals("en", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        }

        /// <summary>
        /// Example JSON error - returned by Ardia when trying to create a new folder
        /// </summary>
        private const string ERROR_MESSAGE_JSON = "{\"title\":\"Item is Archived or Path already exists.\",\"status\":409}\r\n";

        /// <summary>
        /// Example XML error - return by AWS when upload size exceeds 5GB
        /// </summary>
        private const string ERROR_MESSAGE_XML = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>EntityTooLarge</Code><Message>Your proposed upload exceeds the maximum allowed size</Message><ProposedSize>6451738112</ProposedSize><MaxSizeAllowed>5368709120</MaxSizeAllowed><RequestId>...string...</RequestId><HostId>...string...</HostId></Error>";

        private const string RESPONSE_JSON_CREATE_STAGED_DOCUMENT = "{pieces: [ { pieceName: \"[SingleDocument]\", storagePath: \"/foobar\", presignedUrls: [\"https://www.example.com/\"]} ], uploadId: \"97088887-788079\" }";

        // private const string FOOBAR = @"{""pieces"":[{""multiPartId"":""KbTcKnGXt6.asdfeqef--"",""pieceName"":""[SingleDocument]"",""presignedUrls"":[""https://www.example.com/test-only-upload-url-001"",""https://www.example.com/test-only-upload-url-002"",""https://www.example.com/test-only-upload-url-003"", ""https://www.example.com/test-only-upload-url-004""],""storagePath"":""2025/07/02/2cae3""}],""uploadId"":""6d5b0093-5521-4225-a6ba-bdf604d0fc1e""}";
        // private const string REQUEST_JSON_COMPLETE_MULTIPART_UPLOAD = @"'{""StoragePath"": ""2025/07/02/2cae3"", ""MultiPartId"": ""KbTcKnGXt6.asdfeqef--"", ""PartTags"": [{""ETag"": ""673889e3e0ea"", ""PartNumber"": 1}, {""ETag"": ""b24ef41f1022"", ""PartNumber"": 2}, {""ETag"": ""d37f2f3c36f3"", ""PartNumber"": 3}, {""ETag"": ""8082d9440527"", ""PartNumber"": 4}]}";
        //                                                              '{"StoragePath": "2025/07/02/2cae3", "MultiPartId": "KbTcKnGXt6.asdfeqef--", "PartTags": [{"ETag": "673889e3e0ea", "PartNumber": 1}, {"ETag": "b24ef41f1022", "PartNumber": 2}, {"ETag": "d37f2f3c36f3", "PartNumber": 3}, {"ETag": "8082d9440527", "PartNumber": 4}]}>. 
    }
}