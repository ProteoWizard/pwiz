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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    /// <summary>
    /// To enable verbose logging of HTTP request + response in .NET:
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
        private const string PATH_CREATE_FOLDER   = @"/session-management/bff/navigation/api/v1/folders";
        private const string PATH_DELETE_FOLDER   = @"/session-management/bff/navigation/api/v1/folders/folderPath";

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

        public void CreateFolder(string parentFolderPath, string folderName, IProgressMonitor progressMonitor)
        {
            HttpStatusCode? statusCode = null;
            var responseBody = string.Empty;

            try
            {
                var requestModel = CreateFolderRequest.Create(parentFolderPath, folderName);

                var jsonString = JsonConvert.SerializeObject(requestModel);
                using var request = new StringContent(jsonString, Encoding.UTF8, HEADER_CONTENT_TYPE_FOLDER);

                using var httpClient = AuthenticatedHttpClient();

                var response = httpClient.PostAsync(PATH_CREATE_FOLDER, request).Result;
                statusCode = response.StatusCode;
                responseBody = response.Content.ReadAsStringAsync().Result;
                
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                var uri = UriFromParts(ServerUri, PATH_CREATE_FOLDER); 
                var message = string.Format(ArdiaResources.CreateFolder_Error, parentFolderPath, folderName);

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        /// <summary>
        /// Delete the given folder from the Ardia server associated with this <see cref="ArdiaClient"/>.
        /// This uses .NET's deprecated <see cref="HttpWebRequest"/> API to make the DELETE call. .NET's preferred <see cref="HttpClient"/> 
        /// unexpectedly specifies the Content-Type header's "charset=utf-8" attribute, which causes the delete API to return an error
        /// code (400 Bad Request). <see cref="HttpWebRequest"/> does not automatically set charset and works correctly.
        /// </summary>
        /// <param name="folderPath">fully qualified path of the folder to delete</param>
        // TODO: report the Content-Type charset issue to Ardia
        // TODO: is there a way to make this "test only" without just moving it into the test?
        public void DeleteFolder(string folderPath)
        {
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

                // Successful delete returns status code 204
                //      See: https://api.hyperbridge.cmdtest.thermofisher.com/navigation/api/swagger/index.html
                if (response.StatusCode != HttpStatusCode.NoContent)
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
                var statusCode = (e.Response as HttpWebResponse)?.StatusCode;
                var responseBody = string.Empty;

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
        /// <param name="uploadUri">URI referencing the location of the uploaded file</param>
        /// <returns>true if uploaded succeeded, false if upload was canceled. Will throw an exception </returns>
        public bool SendZipFile(string destinationFolderPath, string localZipFile, IProgressMonitor progressMonitor, out Uri uploadUri)
        {
            // Step 1 of 3: Ardia API - Create Staged Document
            CreateStagedDocument(out var presignedUrl, out var uploadId);

            if (progressMonitor.IsCanceled)
            {
                uploadUri = null;
                return false;
            }

            // Step 2 of 3: AWS - Upload File
            StageFileOnAws(localZipFile, presignedUrl, out var uploadBytes);

            if (progressMonitor.IsCanceled)
            {
                uploadUri = null;
                return false;
            }

            // Step 3 of 3: Ardia API - Create Document
            var fileName = Path.GetFileName(localZipFile);
            CreateDocument(destinationFolderPath, fileName, uploadId, uploadBytes, out uploadUri);

            return true;
        }

        private void CreateStagedDocument(out string presignedUrl, out string uploadId)
        {
            HttpStatusCode? statusCode = null;

            try
            {
                var modelRequest = StageDocumentRequest.Create();
                modelRequest.AddSingleDocumentPiece();

                var jsonString = JsonConvert.SerializeObject(modelRequest);
                using var request = new StringContent(jsonString, Encoding.UTF8, APPLICATION_JSON);

                using var ardiaHttpClient = AuthenticatedHttpClient();
                var response = ardiaHttpClient.PostAsync(PATH_STAGE_DOCUMENT, request).Result;
                statusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                var responseString = response.Content.ReadAsStringAsync().Result;
                var modelResponse = JsonConvert.DeserializeObject<StagedDocumentResponse>(responseString);

                presignedUrl = modelResponse.Pieces[0].PresignedUrls[0];
                uploadId = modelResponse.UploadId;
            }
            catch (Exception e)
            {
                var message = ArdiaResources.Ardia_FileUpload_StageDocumentError;
                var uri = new Uri(ServerUri + PATH_STAGE_DOCUMENT);
                var responseBody = string.Empty;

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        private static void StageFileOnAws(string zipFilePath, string presignedUrl, out int uploadBytes)
        {
            HttpStatusCode? statusCode = null;

            try
            {
                var awsUri = new Uri(presignedUrl);

                var awsHttpClient = new HttpClient();
                var awsRequest = new HttpRequestMessage(HttpMethod.Put, awsUri);

                var bytes = File.ReadAllBytes(zipFilePath);
                uploadBytes = bytes.Length;

                var contentBytes = new ByteArrayContent(bytes);
                awsRequest.Content = contentBytes;

                var response = awsHttpClient.SendAsync(awsRequest).Result;
                statusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                var message = ArdiaResources.Ardia_FileUpload_UploadFileError;
                var uri = new Uri(presignedUrl);
                var responseBody = string.Empty;

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        private void CreateDocument(string destinationFolderPath, string destinationFileName, string uploadId, int uploadBytes, out Uri uploadedUri)
        {
            HttpStatusCode? statusCode = null;

            try
            {
                var modelRequest = new DocumentRequest
                {
                    UploadId = uploadId,
                    Size = uploadBytes,
                    FileName = destinationFileName,
                    FilePath = destinationFolderPath
                };

                var jsonString = JsonConvert.SerializeObject(modelRequest);
                using var request = new StringContent(jsonString);
                request.Headers.ContentType = new MediaTypeHeaderValue(APPLICATION_JSON);

                using var httpClient = AuthenticatedHttpClient();
                var response = httpClient.PostAsync(PATH_CREATE_DOCUMENT, request).Result;
                statusCode = response.StatusCode;
                response.EnsureSuccessStatusCode();

                var responseBody = response.Content.ReadAsStringAsync().Result;
                var ardiaDocumentResponse = JsonConvert.DeserializeObject<DocumentResponse>(responseBody);

                uploadedUri = new Uri(ardiaDocumentResponse.PresignedUrls.First());
            }
            catch (Exception e)
            {
                var message = ArdiaResources.Ardia_FileUpload_CreateDocumentError;
                var uri = new Uri(ServerUri + PATH_CREATE_DOCUMENT);
                var responseBody = string.Empty;

                throw ArdiaServerException.Create(message, uri, statusCode, responseBody, e);
            }
        }

        // CONSIDER: marshal JSON directly to RemoteItem. ArdiaSession marshals in two steps -
        //           JSON to ArdiaFolderObject then to RemoteItem. Can simplify here.
        public IList<RemoteItem> GetFolders(ArdiaUrl folderUrl, IProgressMonitor progressMonitor)
        {
            IList<RemoteItem> items;

            using var httpClient = AuthenticatedHttpClient();

            var requestUrl = GetFolderContentsUrl(folderUrl); 

            var response = httpClient.GetAsync(requestUrl).Result;
            response.EnsureSuccessStatusCode();

            var responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            if (!(jsonObject[@"children"] is JArray itemsValue))
            {
                items = ImmutableList<RemoteItem>.EMPTY;
            }
            else
            {
                var parentPath = Account.GetPathFromFolderContentsUrl(requestUrl.AbsoluteUri);

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

                        var item = new RemoteItem(baseChildUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0, folderObject.HasChildren);

                        items.Add(item);
                    }
                }
            }

            return items;

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

    // TODO: refactor ErrorMessageBuilder out of Panorama and use here?
    // TODO - IMPORTANT: scrub access tokens from URI and responseBody
    // CONSIDER: error message tailored to problems parsing the response as JSON
    // CONSIDER: can more be reused with Panorama's error reporting, including PanoramaServerException?
    public class ArdiaServerException : Exception
    {
        public static ArdiaServerException Create(string skylineMessage, Uri uri, HttpStatusCode? statusCode, string responseBody, Exception inner)
        {
            var statusCodeStr = statusCode != null ? statusCode.ToString() : string.Empty;
            var innerMessage = inner != null ? inner.Message : string.Empty;
            var serverMessage = GetIfErrorInResponse(responseBody);

            var exceptionMessage = $@"{skylineMessage} {serverMessage} {uri} {statusCodeStr} {responseBody} {innerMessage}";

            return new ArdiaServerException(exceptionMessage, uri, statusCode, responseBody, inner);
        }

        public Uri Uri { get; }
        public HttpStatusCode? HttpStatus { get; }
        public string ResponseBody { get; }

        private ArdiaServerException(string message, Uri uri, HttpStatusCode? status, string responseBody, Exception inner) 
            : base(message, inner)
        {
            Uri = uri;
            HttpStatus = status;
            ResponseBody = responseBody;
        }

        internal static string GetIfErrorInResponse(string responseBody)
        {
            try
            {
                var jsonResponse = JObject.Parse(responseBody);
                if (jsonResponse?[@"title"] != null)
                {
                    return jsonResponse[@"title"].ToString();
                }
            }
            catch (JsonReaderException e)
            {
                // ignore
            }

            return string.Empty;
        }
    }
}