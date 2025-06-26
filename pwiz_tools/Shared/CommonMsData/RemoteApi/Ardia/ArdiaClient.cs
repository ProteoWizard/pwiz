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
// TODO: error handling - what can be reused with Panorama? Ex: PanoramaServerException, ErrorMessageBuilder?
// TODO: error handling - check strings (esp. responseBody) for tokens and scrub if present
// TODO: error handling - what happens if there's a problem reading or writing JSON? Handle JSONException?
// TODO: is there a way to make GetDocument and DeleteFolder test-only without just moving them into the test?
namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    /// <summary>
    /// To enable verbose HTTP logging:
    ///     https://mikehadlow.blogspot.com/2012/07/tracing-systemnet-to-debug-http-clients.html
    /// </summary>
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class ArdiaClient
    {
        // RFC 3986 specified path separator for URIs
        // CONSIDER: add UrlBuilder class to Skyline. Related PRs also define this constant and helper methods narrowly scoped to a remote server vendor
        public const string URL_PATH_SEPARATOR = @"/";

        private const string APPLICATION_JSON = @"application/json";
        private const string COOKIE_BFF_HOST = "Bff-Host";
        private const string HEADER_APPLICATION_CODE = "applicationCode";
        private const string HEADER_CONTENT_TYPE_FOLDER = @"application/vnd.thermofisher.luna.folder";

        private const string PATH_STAGE_DOCUMENT  = @"/session-management/bff/document/api/v1/stagedDocuments";
        private const string PATH_CREATE_DOCUMENT = @"/session-management/bff/document/api/v1/documents";
        private const string PATH_GET_DOCUMENT    = @"/session-management/bff/document/api/v1/documents/{0}";
        private const string PATH_CREATE_FOLDER   = @"/session-management/bff/navigation/api/v1/folders";
        private const string PATH_DELETE_FOLDER   = @"/session-management/bff/navigation/api/v1/folders/folderPath";

        public const long MAX_UPLOAD_SIZE = int.MaxValue;
        public const long MAX_UPLOAD_SIZE_GB = 2;

        public static ArdiaClient Create(ArdiaAccount account)
        {
            var ardiaUrl = account.GetRootArdiaUrl();
            var serverApiUrl = ardiaUrl.ServerApiUrl;

            var applicationCode = ArdiaCredentialHelper.GetApplicationCode(account);

            var token = ArdiaCredentialHelper.GetToken(account);

            return new ArdiaClient(account, serverApiUrl, applicationCode, token);
        }

        private ArdiaClient(ArdiaAccount account, string serverUrl, string applicationCode, string token)
        {
            Account = account;
            ServerUri = new Uri(serverUrl);
            ApplicationCode = applicationCode;
            Token = token;
        }

        public bool HasCredentials => !string.IsNullOrWhiteSpace(ApplicationCode) && !string.IsNullOrWhiteSpace(Token);
        private ArdiaAccount Account { get; }
        private string ApplicationCode { get; }
        private string Token { get; }
        private Uri ServerUri { get; }

        /// <summary>
        /// Configure an <see cref="HttpClient"/> callers can use to access the Ardia API.
        /// </summary>
        /// <returns>A client that includes headers / cookies necessary to make authenticated calls to the API.</returns>
        private HttpClient AuthenticatedHttpClient()
        {
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(ServerUri, new Cookie(COOKIE_BFF_HOST, Token));

            var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;

            var client = new HttpClient(handler, false);
            client.BaseAddress = ServerUri;
            client.DefaultRequestHeaders.Add(HEADER_APPLICATION_CODE, ApplicationCode);
            client.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), APPLICATION_JSON);

            return client;
        }

        // API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/navigation/api/swagger/index.html
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
        /// API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/navigation/api/swagger/index.html
        /// </summary>
        /// <param name="folderPath">fully qualified path of the folder to delete</param>
        public ArdiaResult DeleteFolder(string folderPath)
        {
            var encodedFolderPath = Uri.EscapeDataString(folderPath);
            var uri = UriFromParts(ServerUri, string.Format($"{PATH_DELETE_FOLDER}?folder={encodedFolderPath}"));

            HttpStatusCode? statusCode = null;

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
                httpWebRequest.Method = HttpMethod.Delete.ToString();
                httpWebRequest.ContentType = HEADER_CONTENT_TYPE_FOLDER;
                httpWebRequest.ContentLength = 0;
                httpWebRequest.Headers[HEADER_APPLICATION_CODE] = ApplicationCode;
                httpWebRequest.CookieContainer = new CookieContainer();

                var cookie = new Cookie(COOKIE_BFF_HOST, Token)
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
        /// Upload a zip file to the Ardia API. The .zip file must contain Skyline files - including .sky, .sky.view, .skyl, etc.
        /// </summary>
        /// <param name="destinationFolderPath">Absolute path to a destination folder on the Ardia Platform</param>
        /// <param name="localZipFile">Absolute path to a local zip file to upload</param>
        /// <param name="progressMonitor"></param>
        /// <returns>an <see cref="ArdiaResult"/>. If successful, the value is a <see cref="CreateDocumentResponse"/>. If failure, the value is null and the result includes info about the error.</returns>
        public ArdiaResult<CreateDocumentResponse> PublishDocument(string destinationFolderPath, string localZipFile, IProgressMonitor progressMonitor)
        {
            Assume.IsTrue(destinationFolderPath.StartsWith("/"));

            // Step 1 of 3: Ardia API - Create Staged Document
            var document = StageDocumentRequest.CreateSinglePieceDocument();
            var resultStaging = CreateStagedDocument(document);

            if (progressMonitor.IsCanceled)
            {
                return ArdiaResult<CreateDocumentResponse>.Canceled;
            }

            // Step 2 of 3: AWS - Upload File
            var presignedUrl = resultStaging.Value.Pieces[0].PresignedUrls[0];
            var resultUpload = UploadFile(localZipFile, presignedUrl);

            if (progressMonitor.IsCanceled)
            {
                return ArdiaResult<CreateDocumentResponse>.Canceled;
            }

            // Step 3 of 3: Ardia API - Create Document
            var requestDocument = new CreateDocumentRequest
            {
                UploadId = resultStaging.Value.UploadId,
                Size = resultUpload.Value,
                FileName = Path.GetFileName(localZipFile),
                FilePath = destinationFolderPath
            };

            var resultDocument = CreateDocument(requestDocument);

            return ArdiaResult<CreateDocumentResponse>.Success(resultDocument.Value);
        }

        // API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
        private ArdiaResult<StagedDocumentResponse> CreateStagedDocument(StageDocumentRequest modelRequest)
        {
            var uri = UriFromParts(ServerUri, PATH_STAGE_DOCUMENT);

            HttpStatusCode? statusCode = null;

            try
            {
                var jsonString = modelRequest.ToJson();

                using var request = new StringContent(jsonString, Encoding.UTF8, APPLICATION_JSON);
                using var httpClient = AuthenticatedHttpClient();
                using var response = httpClient.PostAsync(uri.AbsolutePath, request).Result;

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

        /// <summary>
        /// Upload a local archive to AWS.
        /// </summary>
        /// <param name="zipFilePath">Path to the local archive</param>
        /// <param name="presignedUrl">URL for where to put the archive</param>
        /// <returns>the size of the upload in bytes</returns>
        private static ArdiaResult<long> UploadFile(string zipFilePath, string presignedUrl)
        {
            var uri = new Uri(presignedUrl);

            HttpStatusCode? statusCode = null;

            try
            {
                using var awsHttpClient = new HttpClient();
                using var awsRequest = new HttpRequestMessage(HttpMethod.Put, uri);

                using var fileStream = File.OpenRead(zipFilePath);
                using var progressStream = new ProgressStream(fileStream);
                using var fileContent = new StreamContent(progressStream);

                awsRequest.Content = fileContent;

                var response = awsHttpClient.SendAsync(awsRequest).Result;

                statusCode = response.StatusCode;
                var responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.OK)
                {
                    return ArdiaResult<long>.Success(progressStream.Position);
                }
                else
                {
                    var message = ErrorMessageBuilder.Create(ArdiaResources.Error_StatusCode_Unexpected).ErrorDetailFromResponseBody(responseBody).Uri(uri).StatusCode(statusCode);
                    return ArdiaResult<long>.Failure(message.ToString(), statusCode, null);
                }
            }
            catch (Exception e) when (ShouldHandleException(e))
            {
                var message = ErrorMessageBuilder.Create(ArdiaResources.Error_ProblemCommunicatingWithServer).ErrorDetailFromException(e).Uri(uri).StatusCode(statusCode);
                return ArdiaResult<long>.Failure(message.ToString(), statusCode, e);
            }
        }

        // API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
        private ArdiaResult<CreateDocumentResponse> CreateDocument(CreateDocumentRequest modelRequest) 
        {
            var uri = UriFromParts(ServerUri, PATH_CREATE_DOCUMENT);

            HttpStatusCode? statusCode = null;

            try
            {
                var jsonString = modelRequest.ToJson();

                using var request = new StringContent(jsonString);
                request.Headers.ContentType = new MediaTypeHeaderValue(APPLICATION_JSON);

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

        // API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/document/api/swagger/index.html
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

        // API documentation: https://api.hyperbridge.cmdtest.thermofisher.com/navigation/api/swagger/index.html
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
                            Where(f => f[@"type"].Value<string>().Contains(@"folder")).
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

            return exception is HttpRequestException || 
                   exception is WebException || 
                   // apparently, this is how to detect network timeout when using HttpClient
                   exception is TaskCanceledException { CancellationToken: { IsCancellationRequested: false } };
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
    
    public sealed class ErrorMessageBuilder
    {
        private const string INDENT = "  "; // tab is too much, no space is too little

        private readonly string _message;
        private string _messageDetail;
        private Uri _uri;
        private HttpStatusCode? _statusCode;

        public static ErrorMessageBuilder Create(string error)
        {
            return new ErrorMessageBuilder(error);
        }

        private ErrorMessageBuilder(string message)
        {
            _message = message;
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
                sb.Append(Environment.NewLine);
            }

            if (_uri != null)
            {
                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_Host, _uri.Host);
                sb.Append(Environment.NewLine);

                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_Path, _uri.AbsolutePath);
                sb.Append(Environment.NewLine);
            }

            if (_statusCode != null)
            {
                sb.Append(INDENT);
                sb.AppendFormat(ArdiaResources.Error_StatusCode, _statusCode.ToString(), (int)_statusCode);
                sb.Append(Environment.NewLine);
            }

            return sb.ToString();
        }

        public static string ReadErrorMessageFromResponse(string responseBody)
        {
            // Response body might be JSON - ex: errors from JSON
            try
            {
                var jsonResponse = JObject.Parse(responseBody);
                if (jsonResponse?[@"title"] != null)
                {
                    return jsonResponse[@"title"].ToString();
                }
            }
            catch (JsonReaderException)
            {
                // ignore
            }

            // Response body might be XML - ex: errors from AWS
            try
            {
                var doc = XDocument.Parse(responseBody);
                var messageElement = doc.Element("Error")?.Element("Message");
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