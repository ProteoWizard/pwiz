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
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

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

        public bool CreateFolder(string parentFolderPath, string folderName, IProgressMonitor progressMonitor, out ArdiaError serverError)
        {
            serverError = null;

            var uri = UriFromParts(ServerUri, PATH_CREATE_FOLDER);
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                var requestModel = CreateFolderRequest.Create(parentFolderPath, folderName);
                var jsonString = requestModel.ToJson();

                using var request = new StringContent(jsonString, Encoding.UTF8, HEADER_CONTENT_TYPE_FOLDER);
                using var httpClient = AuthenticatedHttpClient();

                var response = httpClient.PostAsync(uri.AbsolutePath, request).Result;

                statusCode = response.StatusCode;
                responseBody = response.Content.ReadAsStringAsync().Result;

                if (statusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                else
                {
                    serverError = ArdiaError.Create(statusCode, responseBody);
                    return false;
                }
            }
            catch (Exception e) when (e is HttpRequestException || 
                                      e is WebException || 
                                      e is TaskCanceledException { CancellationToken: { IsCancellationRequested: false } })
            {
                serverError = ArdiaError.Create(statusCode, responseBody);

                // throw ArdiaServerException.Create(e.Message, uri, serverError, e);
                throw new IOException(e.Message, e);
            }
            catch (AggregateException e) when (e.InnerException is WebException ||
                                               e.InnerException is HttpRequestException ||
                                               e.InnerException is TaskCanceledException { CancellationToken: { IsCancellationRequested: false } })
            {
                serverError = ArdiaError.Create(statusCode, responseBody);

                var innermostException = e.InnerException;
                while (innermostException.InnerException != null)
                {
                    innermostException = innermostException.InnerException;
                }

                var sb = new StringBuilder();
                sb.Append(e.InnerException.Message);
                sb.Append(@" - ");
                sb.Append(innermostException.Message);

                // throw ArdiaServerException.Create(sb.ToString(), uri, serverError, e.InnerException);
                throw new IOException(e.Message, e);
            }
            catch (Exception e)
            {
                // serverError = ArdiaError.Create(statusCode, responseBody);
                //
                // var message = ErrorMessageBuilder
                //     .Create(string.Format(ArdiaResources.CreateFolder_Error, parentFolderPath, folderName)).Uri(uri)
                //     .ServerError(serverError).ToString();

                // TODO: set detailed info about remote API call - uri, status code, response body
                throw new Exception(@"Unexpected error", e.InnerException);
            }
        }

        /// <summary>
        /// Delete the given folder from the Ardia server associated with this <see cref="ArdiaClient"/>.
        /// This uses .NET's deprecated <see cref="HttpWebRequest"/> API to make the DELETE call. .NET's preferred <see cref="HttpClient"/> 
        /// unexpectedly specifies the Content-Type header's "charset=utf-8" attribute, which causes the delete API to return an error
        /// code (400 Bad Request). <see cref="HttpWebRequest"/> does not automatically set charset and works correctly.
        /// </summary>
        /// <param name="folderPath">fully qualified path of the folder to delete</param>
        // TODO: is there a way to make this "test only" without just moving it into the test?
        public void DeleteFolder(string folderPath)
        {
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                var encodedFolderPath = Uri.EscapeDataString(folderPath);
                var deleteUrl = string.Format($@"{ServerUri.ToString().TrimEnd('/')}{PATH_DELETE_FOLDER}?folder={encodedFolderPath}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(deleteUrl);
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

                var response = (HttpWebResponse)httpWebRequest.GetResponse();

                statusCode = response.StatusCode;

                // Successful delete returns status code 204
                //      See: https://api.hyperbridge.cmdtest.thermofisher.com/navigation/api/swagger/index.html
                if (statusCode != HttpStatusCode.NoContent)
                {
                    var uri = new Uri(ServerUri.Host + PATH_DELETE_FOLDER);
                    var message = string.Format(ArdiaResources.DeleteFolder_Error, folderPath);

                    throw ArdiaServerException.Create(message, uri, response.StatusCode, string.Empty, null);
                }
            }
            catch (WebException e)
            {
                var message = string.Format(ArdiaResources.DeleteFolder_Error, folderPath);
                var uri = new Uri(ServerUri + PATH_DELETE_FOLDER);

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        // TODO: improve and test error handling throughout, incl. user-facing error messages as appropriate, bad JSON, file upload failure, invalid destination directory
        // CONSIDER: input validation? DestinationFolderPath must be an absolute path starting with "/".
        /// <summary>
        /// Upload a zip file to the Ardia API. The .zip file must contain Skyline files - including .sky, .sky.view, .skyl, etc.
        /// </summary>
        /// <param name="destinationFolderPath">Absolute path to a destination folder on the Ardia Platform</param>
        /// <param name="localZipFile">Absolute path to a local zip file to upload</param>
        /// <param name="progressMonitor"></param>
        /// <param name="newCreateDocument">model for the uploaded file</param>
        /// <returns>true if uploaded succeeded, false if upload was canceled. Will throw an exception </returns>
        public bool SendZipFile(string destinationFolderPath, string localZipFile, IProgressMonitor progressMonitor, out CreateDocumentResponse newCreateDocument)
        {
            newCreateDocument = null;

            // Step 1 of 3: Ardia API - Create Staged Document
            CreateStagedDocument(out var modelResponse);

            var uploadId = modelResponse.UploadId;
            var presignedUrl = modelResponse.Pieces[0].PresignedUrls[0];

            if (progressMonitor.IsCanceled)
            {
                return false;
            }

            // Step 2 of 3: AWS - Upload File
            StageFileOnAws(localZipFile, presignedUrl, out var uploadBytes);

            if (progressMonitor.IsCanceled)
            {
                return false;
            }

            // Step 3 of 3: Ardia API - Create Document
            var fileName = Path.GetFileName(localZipFile);
            CreateDocument(destinationFolderPath, fileName, uploadId, uploadBytes, out newCreateDocument);

            return true;
        }

        private void CreateStagedDocument(out StagedDocumentResponse modelResponse)
        {
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                var modelRequest = StageDocumentRequest.CreateSinglePieceDocument();
                var jsonString = modelRequest.ToJson();

                using var request = new StringContent(jsonString, Encoding.UTF8, APPLICATION_JSON);

                using var ardiaHttpClient = AuthenticatedHttpClient();
                var response = ardiaHttpClient.PostAsync(PATH_STAGE_DOCUMENT, request).Result;
                statusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                responseBody = response.Content.ReadAsStringAsync().Result;

                modelResponse = StagedDocumentResponse.FromJson(responseBody);
            }
            catch (Exception e)
            {
                var message = ArdiaResources.Ardia_FileUpload_StageDocumentError;
                var uri = new Uri(ServerUri + PATH_STAGE_DOCUMENT);

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        private static void StageFileOnAws(string zipFilePath, string presignedUrl, out long uploadBytes)
        {
            var awsUri = new Uri(presignedUrl);

            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                using var awsHttpClient = new HttpClient();
                var awsRequest = new HttpRequestMessage(HttpMethod.Put, awsUri);

                using var fileStream = File.OpenRead(zipFilePath);
                using var progressStream = new ProgressStream(fileStream);
                using var fileContent = new StreamContent(progressStream);
                awsRequest.Content = fileContent;

                var response = awsHttpClient.SendAsync(awsRequest).Result;
                statusCode = response.StatusCode;
                responseBody = response.Content.ReadAsStringAsync().Result;

                response.EnsureSuccessStatusCode();

                uploadBytes = progressStream.Position;
            }
            catch (Exception e)
            {
                var serverError = ArdiaError.Create(statusCode, responseBody);

                var errorMessage = ErrorMessageBuilder.Create(ArdiaResources.Ardia_FileUpload_UploadFileError)
                    .ServerError(serverError).Uri(awsUri).ToString();

                throw ArdiaServerException.Create(errorMessage, awsUri, serverError, e);
            }
        }

        private void CreateDocument(string destinationFolderPath, string destinationFileName, string uploadId, long uploadBytes, out CreateDocumentResponse ardiaCreateDocument)
        {
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                var modelRequest = new CreateDocumentRequest
                {
                    UploadId = uploadId,
                    Size = uploadBytes,
                    FileName = destinationFileName,
                    FilePath = destinationFolderPath
                };

                var jsonString = modelRequest.ToJson();
                using var request = new StringContent(jsonString);
                request.Headers.ContentType = new MediaTypeHeaderValue(APPLICATION_JSON);

                using var httpClient = AuthenticatedHttpClient();
                var response = httpClient.PostAsync(PATH_CREATE_DOCUMENT, request).Result;

                statusCode = response.StatusCode;
                responseBody = response.Content.ReadAsStringAsync().Result;

                response.EnsureSuccessStatusCode();

                ardiaCreateDocument = CreateDocumentResponse.FromJson(responseBody);
            }
            catch (Exception e)
            {
                var message = ArdiaResources.Ardia_FileUpload_CreateDocumentError;
                var uri = new Uri(ServerUri + PATH_CREATE_DOCUMENT);

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        public CreateDocumentResponse GetDocument(string documentId)
        {
            var uriString = string.Format(PATH_GET_DOCUMENT, documentId);

            using var httpClient = AuthenticatedHttpClient();
            var response = httpClient.GetAsync(uriString).Result;

            response.EnsureSuccessStatusCode();
            var responseBody = response.Content.ReadAsStringAsync().Result;

            var modelResponse = CreateDocumentResponse.FromJson(responseBody);
            return modelResponse;
        }

        public IList<RemoteItem> GetFolders(ArdiaUrl folderUrl, IProgressMonitor progressMonitor, out ArdiaError serverError)
        {
            serverError = null;
            Uri uri = null;
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                IList<RemoteItem> items;

                using var httpClient = AuthenticatedHttpClient();

                uri = GetFolderContentsUrl(folderUrl);

                var response = httpClient.GetAsync(uri).Result;

                statusCode = response.StatusCode;
                responseBody = response.Content.ReadAsStringAsync().Result;

                response.EnsureSuccessStatusCode();

                var jsonObject = JObject.Parse(responseBody);

                if (!(jsonObject[@"children"] is JArray itemsValue))
                {
                    items = ImmutableList<RemoteItem>.EMPTY;
                }
                else
                {
                    var parentPath = Account.GetPathFromFolderContentsUrl(uri.AbsoluteUri);

                    var foldersEnumerable = itemsValue.OfType<JObject>().
                        Where(IsArdiaFolderOrSequence).
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

                return items;
            }
            catch (Exception e) when (e is HttpRequestException ||
                                      e is WebException ||
                                      e is TaskCanceledException { CancellationToken: { IsCancellationRequested: false } })
            {
                serverError = ArdiaError.Create(statusCode, responseBody);

                // throw ArdiaServerException.Create(e.Message, uri, serverError, e);
                throw new IOException(e.Message, e);
            }
            catch (AggregateException e) when (e.InnerException is WebException ||
                                               e.InnerException is HttpRequestException ||
                                               e.InnerException is TaskCanceledException { CancellationToken: { IsCancellationRequested: false } })
            {
                serverError = ArdiaError.Create(statusCode, responseBody);

                var innermostException = e.InnerException;
                while (innermostException.InnerException != null)
                {
                    innermostException = innermostException.InnerException;
                }

                var sb = new StringBuilder();
                sb.Append(e.InnerException.Message);
                sb.Append(@" - ");
                sb.Append(innermostException.Message);

                // throw ArdiaServerException.Create(sb.ToString(), uri, serverError, e.InnerException);
                throw new IOException(sb.ToString(), e.InnerException);
            }
            catch (Exception e)
            {
                // serverError = ArdiaError.Create(statusCode, responseBody);
                //
                // var message = ErrorMessageBuilder
                //     .Create(string.Format(ArdiaResources.CreateFolder_Error, parentFolderPath, folderName)).Uri(uri)
                //     .ServerError(serverError).ToString();

                // TODO: set detailed info about remote API call - uri, status code, response body
                throw new Exception(@"Unexpected error", e);
            }

            bool IsArdiaFolderOrSequence(JObject f) => f[@"type"].Value<string>().Contains(@"folder");
        }

        private Uri GetFolderContentsUrl(ArdiaUrl ardiaUrl)
        {
            return new Uri(Account.GetFolderContentsUrl(ardiaUrl));
        }

        private static Uri UriFromParts(Uri host, string path)
        {
            return new Uri(host.ToString().TrimEnd('/') + path);
        }
    }

    // TODO - IMPORTANT: scrub access tokens from URI and responseBody
    // CONSIDER: can more be reused with Panorama's error reporting, including PanoramaServerException?
    public class ArdiaServerException : Exception
    {
        public static ArdiaServerException Create(string message, Uri uri, HttpStatusCode? statusCode, string responseBody, Exception inner)
        {
            var ardiaError = ArdiaError.Create(statusCode, responseBody);

            return Create(message, uri, ardiaError, inner);
        }

        public static ArdiaServerException Create(string message, Uri uri, ArdiaError serverError, Exception inner)
        {
            return new ArdiaServerException(message, uri, serverError, inner);
        }

        private ArdiaServerException(string message, Uri uri, ArdiaError serverError, Exception inner) 
            : base(message, inner)
        {
            Uri = uri;
            ServerError = serverError;
        }

        public Uri Uri { get; }
        public ArdiaError ServerError { get; }
    }

    public sealed class ErrorMessageBuilder
    {
        private readonly string _message;
        private string _messageDetail;
        private ArdiaError _serverError;
        private Uri _uri;

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

        public ErrorMessageBuilder Uri(Uri uri)
        {
            _uri = uri;
            return this;
        }

        public ErrorMessageBuilder ServerError(ArdiaError serverError)
        {
            _serverError = serverError;
            return this;
        }

        // CONSIDER: Panorama's ErrorMessageBuilder supports setting the entire response body and an
        //           exception message. Useful here too?
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_messageDetail) || _serverError != null)
            {
                sb.Append(ArdiaResources.Error_Prefix);

                if (!string.IsNullOrEmpty(_messageDetail)) 
                    sb.AppendLine(_messageDetail);
                
                if (_serverError != null) 
                    sb.AppendLine(_serverError.ToString());
            }

            if (_uri != null)
                sb.AppendLine($@"{ArdiaResources.Error_URL} {_uri}");

            return sb.Length > 0 ? CommonTextUtil.LineSeparate(_message, sb.ToString().TrimEnd()) : _message;
        }
    }
}