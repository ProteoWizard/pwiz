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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

// TODO: error handling - consider adding more information about the remote API call to exceptions (esp. unexpected ones) including uri, status code, and response body
// TODO: error handling - check strings (esp. responseBody) for tokens and scrub if present
// TODO: error handling - what happens if there's a problem reading or writing JSON? Handle JSONException?
// TODO: check (1) host available and (2) token can make API calls
// TODO: add code inspection making sure response.EnsureSuccessStatusCode() is not used
// TODO: is there a way to make GetDocument and DeleteFolder test-only without just moving them into the test?
// CONSIDER: error handling - what can be reused with Panorama? Ex: PanoramaServerException, ErrorMessageBuilder?
namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    /// <summary>
    /// To enable verbose HTTP logging:
    ///     https://mikehadlow.blogspot.com/2012/07/tracing-systemnet-to-debug-http-clients.html
    /// </summary>
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public sealed class ArdiaClient
    {
        // RFC 3986 specified path separator for URIs
        // CONSIDER: add UrlBuilder class to Skyline. Related PRs also define this constant and helper methods narrowly scoped to a remote server vendor
        public const string URL_PATH_SEPARATOR = @"/";

        public const int DEFAULT_PART_SIZE_MB = 64;
        public const int DEFAULT_PART_SIZE_BYTES = DEFAULT_PART_SIZE_MB * 1024 * 1024;

        private const string HEADER_CONTENT_TYPE_APPLICATION_JSON = @"application/json";
        private const string HEADER_CONTENT_TYPE_ZIP_COMPRESSED   = @"application/x-zip-compressed";
        private const string HEADER_CONTENT_TYPE_FOLDER           = @"application/vnd.thermofisher.luna.folder";
        private const string COOKIE_BFF_HOST = "Bff-Host";
        private const string HEADER_APPLICATION_CODE = "applicationCode";

        private const string PATH_STAGE_DOCUMENT             = @"/session-management/bff/document/api/v1/stagedDocuments";
        private const string PATH_CREATE_DOCUMENT            = @"/session-management/bff/document/api/v1/documents";
        private const string PATH_GET_DOCUMENT               = @"/session-management/bff/document/api/v1/documents/{0}";
        private const string PATH_COMPLETE_MULTIPART_REQUEST = @"/session-management/bff/document/api/v1/documents/completeMultiPartRequest";
        private const string PATH_CREATE_FOLDER              = @"/session-management/bff/navigation/api/v1/folders";
        private const string PATH_DELETE_FOLDER              = @"/session-management/bff/navigation/api/v1/folders/folderPath";
        private const string PATH_STORAGE_INFO               = @"/session-management/bff/raw-data/api/v1/rawdata/storageInfo";
        private const string PATH_SESSION_COOKIE             = @"/session-management/bff/session-management/api/v1/SessionManagement/sessioncookie";
        private const string PATH_GET_PARENT_BY_PATH         = @"/session-management/bff/navigation/api/v1/navigation/path";

        // CONSIDER: throw IOException as a placeholder for improving the experience of editing Remote Account settings. It should
        //           not be possible to exit the settings editor when the account is invalid.
        public static ArdiaClient Create(ArdiaAccount account)
        {
            var applicationCode = ArdiaCredentialHelper.GetApplicationCode(account);
            if (string.IsNullOrEmpty(applicationCode))
            {
                throw new IOException(ArdiaResources.Error_InvalidToken);
            }

            var token = ArdiaCredentialHelper.GetToken(account);
            if (token.IsNullOrEmpty())
            {
                throw new IOException(ArdiaResources.Error_InvalidToken);
            }

            var ardiaUrl = account.GetRootArdiaUrl();
            var serverUrl = ardiaUrl.ServerApiUrl;

            return new ArdiaClient(account, serverUrl, applicationCode, token);
        }

        private ArdiaClient(ArdiaAccount account, string serverUrl, string applicationCode, EncryptedToken token)
        {
            Account = account;
            ServerUri = new Uri(serverUrl);
            ApplicationCode = applicationCode;
            Token = token;
            UploadPartSizeMBs = DEFAULT_PART_SIZE_MB;
        }

        public int UploadPartSizeMBs { get; private set; }
        public int UploadPartSizeBytes => UploadPartSizeMBs * 1024 * 1024;

        // CONSIDER: enforce this can only be used from tests
        /// <summary>
        /// ONLY USE IN TESTS. Configure the max part size for multipart uploads.
        /// </summary>
        /// <param name="partSizeMBs"></param>
        public void ChangePartSizeForTests(int partSizeMBs)
        {
            Assume.IsTrue(IsValidPartSize(partSizeMBs), @"Max part size must be greater than or equal to 5 and less than 2048");

            UploadPartSizeMBs = partSizeMBs;
        }

        public static bool IsValidPartSize(int partSizeMBs)
        {
            return partSizeMBs >= 5 && partSizeMBs < 2048;
        }

        public bool HasCredentials => !string.IsNullOrWhiteSpace(ApplicationCode) && !Token.IsNullOrEmpty();
        private ArdiaAccount Account { get; }
        private string ApplicationCode { get; }
        private EncryptedToken Token { get; }
        private Uri ServerUri { get; }

        /// <summary>
        /// Configure an <see cref="HttpClient"/> callers can use to access the Ardia API.
        /// </summary>
        /// <returns>A client that includes headers / cookies necessary to make authenticated calls to the API.</returns>
        private HttpClient AuthenticatedHttpClient()
        {
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(ServerUri, new Cookie(COOKIE_BFF_HOST, Token.Decrypted));

            var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;

            var client = new HttpClient(handler, false);
            client.BaseAddress = ServerUri;
            client.DefaultRequestHeaders.Add(HEADER_APPLICATION_CODE, ApplicationCode);
            client.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), HEADER_CONTENT_TYPE_APPLICATION_JSON);

            return client;
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/navigation/api/swagger/index.html
        public ArdiaResult CreateFolder(string parentFolderPath, string folderName, IProgressMonitor progressMonitor)
        {
            var uri = UriFromParts(ServerUri, PATH_CREATE_FOLDER);
            
            HttpStatusCode? statusCode = null;

            try
            {
                var requestModel = CreateFolderRequest.Create(parentFolderPath, folderName);
                var jsonString = requestModel.ToJson();

                using var request = new StringContent(jsonString, Encoding.UTF8, HEADER_CONTENT_TYPE_FOLDER);
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.PostAsync(uri.AbsolutePath, request).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.Created)
                {
                    return ArdiaResult.Success();
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult.Failure(message.ToString(), statusCode, e);
            }
        }

        /// <summary>
        /// Delete the given folder from the Ardia server associated with this <see cref="ArdiaClient"/>.
        /// This uses .NET's deprecated <see cref="HttpWebRequest"/> API to make the DELETE call. .NET's preferred <see cref="HttpClient"/> 
        /// unexpectedly specifies the Content-Type header's "charset=utf-8" attribute, which causes the delete API to return an error
        /// code (400 Bad Request). <see cref="HttpWebRequest"/> does not automatically set charset and works correctly.
        ///
        /// API documentation:https://api.ardia-core-int.cmdtest.thermofisher.com/navigation/api/swagger/index.html
        /// </summary>
        /// <param name="folderPath">fully qualified path of the folder to delete</param>
        public ArdiaResult DeleteFolder(string folderPath)
        {
            var encodedFolderPath = Uri.EscapeDataString(folderPath);
            var uri = UriFromParts(ServerUri, string.Format($@"{PATH_DELETE_FOLDER}?folder={encodedFolderPath}"));

            HttpStatusCode? statusCode = null;

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                httpWebRequest.Method = HttpMethod.Delete.ToString();
                httpWebRequest.ContentType = HEADER_CONTENT_TYPE_FOLDER;
                httpWebRequest.ContentLength = 0;
                httpWebRequest.Headers[HEADER_APPLICATION_CODE] = ApplicationCode;
                httpWebRequest.CookieContainer = new CookieContainer();

                var cookie = new Cookie(COOKIE_BFF_HOST, Token.Decrypted)
                {
                    Domain = ServerUri.Host
                };

                httpWebRequest.CookieContainer.Add(cookie);

                using var response = (HttpWebResponse)httpWebRequest.GetResponse();

                statusCode = response.StatusCode;
                var responseBody = ReadAsString(response.GetResponseStream());

                if (statusCode == HttpStatusCode.NoContent)
                {
                    return ArdiaResult.Success();
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult.Failure(message.ToString(), statusCode, e);
            }
        }

        /// <summary>
        /// Upload a Skyline document archive to the Ardia API. The archive must be created by SrmDocumentSharing prior to calling this method and will contain 
        /// many files including .sky, .sky.view, .skyl, .skyd, and more. <see cref="SrmDocumentArchive"/> contains paths to the pieces of the .zip file. Skyline
        /// documents smaller than <see cref="DEFAULT_PART_SIZE_BYTES"/> will be a single .sky.zip file (aka: one "part"). Skyline document archives larger than
        /// <see cref="DEFAULT_PART_SIZE_BYTES"/> will be multipart .zip archives and could be split across many (> 1,000) parts.
        /// 
        /// Callers are responsible for cleaning up temporary files or directories created when archiving the Skyline document.
        /// </summary>
        /// <param name="srmDocumentArchive">Info about the Skyline document archive</param>
        /// <param name="destinationFolderPath">Absolute path to a destination folder on the Ardia Platform</param>
        /// <param name="progressMonitor">Progress monitor reporting what could be a long process (> 1hr) uploading the archive.</param>
        /// <returns>an <see cref="ArdiaResult"/>. If successful, the value is a <see cref="CreateDocumentResponse"/>. If failure, the value is null and the result includes info about the error.</returns>
        public ArdiaResult<CreateDocumentResponse> PublishDocument(SrmDocumentArchive srmDocumentArchive, string destinationFolderPath, IProgressMonitor progressMonitor)
        {
            Assume.IsTrue(destinationFolderPath.StartsWith(@"/"));
            Assume.IsTrue(srmDocumentArchive.PartCount > 0);

            IProgressStatus progressStatus = new ProgressStatus(); 

            // Step 1 of 4: Ardia API - Create Staged Multi-part Document
            var document = StageDocumentRequest.Create(srmDocumentArchive.TotalSize, UploadPartSizeMBs);
            var resultStaging = CreateStagedDocument(document);

            if (progressMonitor.IsCanceled)
            {
                return ArdiaResult<CreateDocumentResponse>.Canceled;
            }
            else if (resultStaging.IsFailure)
            {
                return ArdiaResult<CreateDocumentResponse>.Failure(resultStaging);
            }

            // Step 2 of 4: AWS - Upload 1 or more files to AWS
            var presignedUrls = resultStaging.Value.Pieces[0].PresignedUrls;
            var resultUpload = UploadParts(srmDocumentArchive, presignedUrls, progressMonitor, progressStatus);

            if (progressMonitor.IsCanceled)
            {
                return ArdiaResult<CreateDocumentResponse>.Canceled;
            }
            else if (resultUpload.IsFailure)
            {
                return ArdiaResult<CreateDocumentResponse>.Failure(resultUpload);
            }

            progressStatus = progressStatus.ChangeMessage(ArdiaResources.FileUpload_Status_CreatingDocument);
            progressMonitor.UpdateProgress(progressStatus);

            // Step 3 of 4: Ardia API - Complete multipart upload
            if (srmDocumentArchive.IsMultipart)
            {
                var storagePath = resultStaging.Value.Pieces[0].StoragePath;
                var multiPartId = resultStaging.Value.Pieces[0].MultiPartId;
                var eTagList = resultUpload.Value.Parts.Select(item => item.ETag).ToList();

                var completeMultipartUploadRequest = CompleteMultiPartUploadRequest.Create(storagePath, multiPartId, eTagList);
                var resultCompleteMultipartUpload = CompleteMultiPartUpload(completeMultipartUploadRequest);

                if (progressMonitor.IsCanceled)
                {
                    return ArdiaResult<CreateDocumentResponse>.Canceled;
                }
                else if (resultCompleteMultipartUpload.IsFailure)
                {
                    return ArdiaResult<CreateDocumentResponse>.Failure(resultStaging);
                }
            }

            // Step 4 of 4: Ardia API - Create Document
            var uploadedBytes = resultUpload.Value.Parts.Select(item => item.BytesUploaded).Sum();
            var requestDocument = new CreateDocumentRequest
            {
                UploadId = resultStaging.Value.UploadId,
                Size = uploadedBytes,
                FileName = srmDocumentArchive.ArchiveFileName,
                FilePath = destinationFolderPath
            };

            var resultCreateDocument = CreateDocument(requestDocument);

            progressStatus.Complete();

            return resultCreateDocument;
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/document/api/swagger/index.html
        private ArdiaResult<StagedDocumentResponse> CreateStagedDocument(StageDocumentRequest modelRequest)
        {
            var uri = UriFromParts(ServerUri, PATH_STAGE_DOCUMENT);

            HttpStatusCode? statusCode = null;

            try
            {
                var jsonString = modelRequest.ToJson();
                using var content = new StringContent(jsonString, Encoding.UTF8, HEADER_CONTENT_TYPE_APPLICATION_JSON);

                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.PostAsync(uri.AbsolutePath, content).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    var model = StagedDocumentResponse.FromJson(responseBody);
                    return ArdiaResult<StagedDocumentResponse>.Success(model);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<StagedDocumentResponse>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<StagedDocumentResponse>.Failure(message.ToString(), statusCode, e);
            }
        }

        // CONSIDER: upload parts in parallel?
        private static ArdiaResult<UploadPartsResponse> UploadParts(SrmDocumentArchive srmDocumentArchive, IList<string> presignedUrls, IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            Assume.IsTrue(srmDocumentArchive.PartCount == presignedUrls.Count);

            var uploadResponse = UploadPartsResponse.Create();

            using var awsHttpClient = new HttpClient();

            foreach (var part in srmDocumentArchive.Parts)
            {
                var segmentProgressMonitor = SegmentProgressMonitor.Create(progressMonitor, uploadResponse.UploadedBytes, part.SizeInBytes, srmDocumentArchive.TotalSize);

                var uri = new Uri(presignedUrls[part.Index]);

                HttpStatusCode? statusCode = null;

                var stopwatch = new Stopwatch();
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Put, uri);

                    using var fileStream = File.OpenRead(part.FilePath);
                    using var progressStream = new ProgressStream(fileStream);
                    progressStream.SetProgressMonitor(segmentProgressMonitor, progressStatus, false);

                    using var fileContent = new StreamContent(progressStream);
                    request.Content = fileContent;

                    stopwatch.Start();
                    var response = awsHttpClient.SendAsync(request).Result;
                    stopwatch.Stop();

                    statusCode = response.StatusCode;

                    if (statusCode == HttpStatusCode.OK)
                    {
                        var partResponse = UploadPartResponse.Create(progressStream.Position, response.Headers.ETag.Tag);
                        uploadResponse.Parts.Add(partResponse);
                    }
                    else
                    {
                        var responseBody = response.Content.ReadAsStringAsync().Result;

                        var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).
                            ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode).
                            ErrorDetail(@"Request - timeout: {0}s", awsHttpClient.Timeout.Seconds).
                            ErrorDetail(@"Request - part {0} of {1}", part.ReadableIndex, srmDocumentArchive.PartCount).
                            ErrorDetail(@"Request - part size: {0:N0} bytes", part.SizeInBytes).
                            ErrorDetail(@"Request - uploaded: {0:N0} bytes", segmentProgressMonitor.BytesWritten).
                            ErrorDetail(@"Request - elapsed time: {0:F2}s", stopwatch.ElapsedMilliseconds / (decimal)1000).
                            ErrorDetail(@"Request - upload speed {0:N1} MB / s", segmentProgressMonitor.BytesWritten / (decimal)(1024 * 1024) / stopwatch.ElapsedMilliseconds * 1000);

                        return ArdiaResult<UploadPartsResponse>.Failure(message.ToString(), statusCode, null);
                    }
                }
                catch (Exception e) when (ShouldHandleException(e))
                {
                    if(stopwatch.IsRunning) 
                        stopwatch.Stop();

                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).
                        ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode).
                        ErrorDetail(@"Request - timeout: {0}s", awsHttpClient.Timeout.Seconds).
                        ErrorDetail(@"Request - part {0} of {1}", part.ReadableIndex, srmDocumentArchive.PartCount).
                        ErrorDetail(@"Request - part size: {0:N0} bytes", part.SizeInBytes).
                        ErrorDetail(@"Request - uploaded: {0:N0} bytes", segmentProgressMonitor.BytesWritten).
                        ErrorDetail(@"Request - elapsed time: {0:F2}s", stopwatch.ElapsedMilliseconds / (decimal)1000).
                        ErrorDetail(@"Request - upload speed {0:N1} MB / s", segmentProgressMonitor.BytesWritten / (decimal)(1024*1024) / stopwatch.ElapsedMilliseconds*1000);

                    return ArdiaResult<UploadPartsResponse>.Failure(message.ToString(), statusCode, e);
                }

                if (progressMonitor.IsCanceled)
                {
                    return ArdiaResult<UploadPartsResponse>.Canceled;
                }
            }

            return ArdiaResult<UploadPartsResponse>.Success(uploadResponse);
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/document/api/swagger/index.html
        private ArdiaResult CompleteMultiPartUpload(CompleteMultiPartUploadRequest modelRequest)
        {
            var uri = UriFromParts(ServerUri, PATH_COMPLETE_MULTIPART_REQUEST);

            HttpStatusCode? statusCode = null;

            try
            {
                var json = modelRequest.ToJson();
                using var content = new StringContent(json, Encoding.UTF8, HEADER_CONTENT_TYPE_APPLICATION_JSON);

                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.PutAsync(uri.AbsolutePath, content).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    return ArdiaResult.Success();
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/document/api/swagger/index.html
        private ArdiaResult<CreateDocumentResponse> CreateDocument(CreateDocumentRequest modelRequest) 
        {
            var uri = UriFromParts(ServerUri, PATH_CREATE_DOCUMENT);

            HttpStatusCode? statusCode = null;

            try
            {
                var jsonString = modelRequest.ToJson();

                using var request = new StringContent(jsonString);
                request.Headers.ContentType = new MediaTypeHeaderValue(HEADER_CONTENT_TYPE_ZIP_COMPRESSED);

                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.PostAsync(uri.AbsolutePath, request).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.Created)
                {
                    var modelResponse = CreateDocumentResponse.FromJson(responseBody);
                    return ArdiaResult<CreateDocumentResponse>.Success(modelResponse);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).
                        ErrorDetailFromResponseBody(responseBody).
                        ErrorDetail(ArdiaResources.Error_FilePath, modelRequest.FilePath).
                        ErrorDetail(ArdiaResources.Error_FileName, modelRequest.FileName).
                        ErrorDetail(ArdiaResources.Error_UploadId, modelRequest.UploadId).
                        ErrorDetail(ArdiaResources.Error_Size, modelRequest.Size.ToString(@"N0")).
                        Uri(uri).
                        StatusCode(statusCode);

                    return ArdiaResult<CreateDocumentResponse>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<CreateDocumentResponse>.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/document/api/swagger/index.html
        public ArdiaResult<CreateDocumentResponse> GetDocument(string documentId)
        {
            var uri = UriFromParts(ServerUri, string.Format(PATH_GET_DOCUMENT, documentId));

            HttpStatusCode? statusCode = null;

            try
            {
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.GetAsync(uri.AbsolutePath).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    var model = CreateDocumentResponse.FromJson(responseBody);
                    return ArdiaResult<CreateDocumentResponse>.Success(model);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<CreateDocumentResponse>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<CreateDocumentResponse>.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/navigation/api/swagger/index.html
        public ArdiaResult<IList<RemoteItem>> GetFolders(ArdiaUrl folderUrl, IProgressMonitor progressMonitor)
        {
            var uri = GetFolderContentsUrl(Account, folderUrl);

            HttpStatusCode? statusCode = null;

            try
            {
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.GetAsync(uri).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    var jsonObject = JObject.Parse(responseBody);

                    IList<RemoteItem> items;
                    if (!(jsonObject[@"children"] is JArray itemsValue))
                    {
                        items = ImmutableList<RemoteItem>.EMPTY;
                    }
                    else
                    {
                        var parentPath = Account.GetPathFromFolderContentsUrl(uri.AbsoluteUri);

                        var foldersEnumerable = itemsValue.OfType<JObject>().
                            Where(f => f[@"type"]!.Value<string>().Contains(@"folder")).
                            Select(folder => new ArdiaFolderObject(folder, parentPath));

                        items = new List<RemoteItem>();
                        foreach (var folderObject in foldersEnumerable)
                        {
                            if (folderObject.ParentId == "" || folderObject.ParentId.TrimStart('/') == folderUrl.EncodedPath)
                            {
                                var childUrl = folderUrl.ChangeId(folderObject.Id);
                                var childUrlPathParts = folderUrl.GetPathParts().Concat(new[] { folderObject.Name });
                                var baseChildUrl = childUrl.ChangePathParts(childUrlPathParts);

                                // CONSIDER: marshal JSON directly to RemoteItem. ArdiaSession marshals in two steps -
                                //           JSON to ArdiaFolderObject then to RemoteItem. Can simplify here.
                                var item = new RemoteItem(baseChildUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0, folderObject.HasChildren);

                                items.Add(item);
                            }
                        }
                    }

                    return ArdiaResult<IList<RemoteItem>>.Success(items);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<IList<RemoteItem>>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<IList<RemoteItem>>.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/session-management/api/swagger/index.html
        public ArdiaResult CheckSession()
        {
            var uri = UriFromParts(ServerUri, PATH_SESSION_COOKIE);

            HttpStatusCode? statusCode = null;
            try
            {
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.GetAsync(uri.AbsolutePath).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    return ArdiaResult.Success();
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/raw-data/api/swagger/index.html
        public ArdiaResult<StorageInfoResponse> GetServerStorageInfo()
        {
            var uri = UriFromParts(ServerUri, PATH_STORAGE_INFO);

            HttpStatusCode? statusCode = null;
            try
            {
                using var httpClient = AuthenticatedHttpClient();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(HEADER_CONTENT_TYPE_APPLICATION_JSON));

                using var response = httpClient.GetAsync(uri.AbsolutePath).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    return ArdiaResult<StorageInfoResponse>.Success(StorageInfoResponse.Create(responseBody));
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<StorageInfoResponse>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<StorageInfoResponse>.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.ardia-core-int.cmdtest.thermofisher.com/navigation/api/swagger/index.html
        public ArdiaResult<GetParentFolderResponse> GetFolderByGetParentFolderByPath(string path)
        {
            var uri = UriFromParts(ServerUri, $@"{PATH_GET_PARENT_BY_PATH}?itemPath={Uri.EscapeDataString(path)}");

            HttpStatusCode? statusCode = null;
            try
            {
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.GetAsync(uri).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    var responseModel = GetParentFolderResponse.FromJson(responseBody, new Uri(Account.ServerUrl));
                    return ArdiaResult<GetParentFolderResponse>.Success(responseModel);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<GetParentFolderResponse>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<GetParentFolderResponse>.Failure(message.ToString(), statusCode, e);
            }
        }

        private static Uri GetFolderContentsUrl(ArdiaAccount account, ArdiaUrl ardiaUrl)
        {
            return new Uri(account.GetFolderContentsUrl(ardiaUrl));
        }

        private static Uri UriFromParts(Uri host, string path)
        {
            return new Uri(host.ToString().TrimEnd('/') + path);
        }



        private static bool ShouldHandleException(Exception exception)
        {
            // In this case, look at the wrapped exception
            if (exception is AggregateException)
            {
                exception = exception.InnerException;
            }

            // Note: ArdiaClient uses raw HttpClient (not HttpClientWithProgress), so WebException 
            // could appear as InnerException of HttpRequestException for DNS failures, etc.
            return exception is HttpRequestException || 
                   exception is WebException || 
                   // Indicates the request timed out
                   exception is TaskCanceledException { CancellationToken: { IsCancellationRequested: true } };
        }

        private static string ReadAsString(Stream stream, Encoding encoding = null)
        {
            if (stream != null)
            {
                encoding ??= Encoding.UTF8;
                using var reader = new StreamReader(stream, encoding);
                return reader.ReadToEnd();
            }
            else return string.Empty;
        }
    }

    internal class SegmentProgressMonitor : IProgressMonitor
    {
        private const int ONE_MB = 1024 * 1024;

        private readonly IProgressMonitor _progressMonitor;
        private readonly decimal _startPercent;
        private readonly decimal _endPercent;
        private readonly long _totalBytes;

        internal static SegmentProgressMonitor Create(IProgressMonitor progressMonitor, long startBytes, long sizeBytes, long totalBytes)
        {
            var segmentStartPercent = startBytes / (decimal)totalBytes * 100;
            var segmentEndPercent = (startBytes + sizeBytes) / (decimal)totalBytes * 100;

            return new SegmentProgressMonitor(progressMonitor, totalBytes, segmentStartPercent, segmentEndPercent);
        }

        internal SegmentProgressMonitor(IProgressMonitor progressMonitor, long totalBytes, decimal startPercent, decimal endPercent)
        {
            _progressMonitor = progressMonitor;
            _totalBytes = totalBytes;
            _startPercent = startPercent;
            _endPercent = endPercent;
            BytesWritten = 0L;
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            var totalPercentComplete = _startPercent + (_endPercent - _startPercent) * (decimal)(status.PercentComplete / 100.0);
            var currentBytes = (long)Math.Round(totalPercentComplete / 100 * _totalBytes);

            BytesWritten = currentBytes;

            BytesWritten = currentBytes;

            status = status.ChangePercentComplete((int)Math.Round(totalPercentComplete));

            // TODO: tailor message for KB vs. MB vs. GB?
            var msg = string.Format(ArdiaResources.FileUpload_Status_ProgressWithSize, totalPercentComplete, currentBytes / ONE_MB, _totalBytes / ONE_MB);
            status = status.ChangeMessage(msg);

            return _progressMonitor.UpdateProgress(status);
        }

        public bool IsCanceled => _progressMonitor.IsCanceled;
        public bool HasUI => _progressMonitor.HasUI;
        public long BytesWritten;
    }

    public sealed class ErrorMessageBuilder
    {
        private const string INDENT = "    "; // tab is too much, no space is too little

        private readonly string _message;
        private string _messageDetail;
        private Uri _uri;
        private HttpStatusCode? _statusCode;
        private readonly List<string> _extraInfo;

        public static ErrorMessageBuilder Create(string error)
        {
            return new ErrorMessageBuilder(error);
        }

        private ErrorMessageBuilder(string message)
        {
            _message = message;
            _extraInfo = new List<string>();
        }

        public ErrorMessageBuilder ErrorDetail(string messageDetail)
        {
            _messageDetail = messageDetail;
            return this;
        }

        public ErrorMessageBuilder ErrorDetailFromException(Exception e)
        {
            var exception = e is AggregateException ? e.InnerException : e;

            var strings = new List<string>();
            while (exception != null)
            {
                strings.Add(exception.Message);
                exception = exception.InnerException;
            }

            return ErrorDetail(string.Join(@" - ", strings));
        }

        public ErrorMessageBuilder ErrorDetailFromResponseBody(string responseBody)
        {
            return ErrorDetail(ReadErrorMessageFromResponse(responseBody));
        }

        public ErrorMessageBuilder ErrorDetail(string format, string value)
        {
            _extraInfo.Add(string.Format(format, value));
            return this;
        }

        public ErrorMessageBuilder ErrorDetail(string format, int value)
        {
            _extraInfo.Add(string.Format(format, value));
            return this;
        }

        public ErrorMessageBuilder ErrorDetail(string format, int value1, int value2)
        {
            _extraInfo.Add(string.Format(format, value1, value2));
            return this;
        }

        public ErrorMessageBuilder ErrorDetail(string format, long value)
        {
            _extraInfo.Add(string.Format(format, value));
            return this;
        }

        public ErrorMessageBuilder ErrorDetail(string format, decimal value)
        {
            _extraInfo.Add(string.Format(format, value));
            return this;
        }

        public ErrorMessageBuilder Uri(Uri uri)
        {
            _uri = uri;
            return this;
        }

        public ErrorMessageBuilder StatusCode(HttpStatusCode? statusCode)
        {
            _statusCode = statusCode;
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(_message);

            if (!string.IsNullOrEmpty(_messageDetail))
            {
                sb.Append(INDENT);
                sb.Append(ArdiaResources.Error_Detail);
                sb.Append(_messageDetail);
                sb.AppendLine();
            }

            if (_uri != null)
            {
                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_Host, _uri.Host);
                sb.AppendLine(); 

                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_Path, _uri.AbsolutePath);
                sb.AppendLine();
            }

            if (_statusCode != null)
            {
                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_StatusCode, _statusCode.ToString(), (int)_statusCode);
                sb.AppendLine();
            }

            if (_extraInfo.Count > 0)
            {
                foreach (var item in _extraInfo)
                {
                    sb.Append(INDENT);
                    sb.Append(item);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public static string ReadErrorMessageFromResponse(string responseBody)
        {
            // Response body might be JSON - ex: errors from JSON
            try
            {
                /* Example error message:
                 {
                    "type":"https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    "title":"One or more validation errors occurred.",
                    "status":400,
                    "errors":{
                        "Pieces[0].PartSize":[
                            "'Part Size' must be greater than or equal to '5'."
                        ]
                    },
                    "traceId":"00-5ba80912e8eb5499ba8c560f810b9853-b2ab21672f142dac-00"
                }
                 */
                var jsonResponse = JObject.Parse(responseBody);

                var sb = new StringBuilder();
                if (jsonResponse[@"title"] != null)
                {
                    sb.Append(jsonResponse[@"title"]);
                }

                // TODO: read specific fields from the errors object
                if (jsonResponse[@"errors"] != null)
                {
                    sb.Append(jsonResponse[@"errors"]);
                }

                return sb.ToString();
            }
            catch (JsonReaderException)
            {
                // ignore
            }

            // Response body might be XML - ex: errors from AWS
            try
            {
                var doc = XDocument.Parse(responseBody);
                var messageElement = doc.Element(@"Error")?.Element(@"Message");
                if (messageElement != null)
                {
                    return messageElement.Value;
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return string.Empty;
        }
    }
}