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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;
using pwiz.PanoramaClient;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    public class ArdiaClient
    {
        private const string APPLICATION_JSON = @"application/json";
        private const string COOKIE_BFF_HOST = "Bff-Host";
        private const string HEADER_APPLICATION_CODE = "applicationCode";

        private const string PATH_STAGE_DOCUMENT = @"/session-management/bff/document/api/v1/stagedDocuments";
        private const string PATH_CREATE_DOCUMENT = @"/session-management/bff/document/api/v1/documents";

        public static ArdiaClient Instance(ArdiaAccount account)
        {
            var ardiaUrl = account.GetRootArdiaUrl();

            // stored in user.config for the provided ArdiaAccount
            string applicationCode = null;
            {
                // NOTE: settings for the Ardia account are stored under a hostname that differs from URL
                //       for the Ardia API so use this to read the config but do not use to call the API.
                var entryKey = new Uri(ardiaUrl.ServerUrl).Host;
                if (Settings.Default.ArdiaRegistrationCodeEntries.TryGetValue(entryKey, out var entry))
                {
                    applicationCode = entry.ClientApplicationCode;
                }
            }

            // stored in memory (not in user.config)
            var token = ArdiaAccount.GetSessionCookieString(account);
            var serverApiUrl = ardiaUrl.ServerApiUrl;

            return new ArdiaClient(serverApiUrl, applicationCode, token);
        }

        private ArdiaClient(string serverUrl, string applicationCode, string token)
        {
            ServerUri = new Uri(serverUrl);
            ApplicationCode = applicationCode;
            Token = token;
        }

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

            var client = new HttpClient(handler);
            client.BaseAddress = ServerUri;
            client.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), APPLICATION_JSON);
            client.DefaultRequestHeaders.Add(HEADER_APPLICATION_CODE, ApplicationCode);

            return client;
        }

        // TODO: how to user IProgressMonitor for a series of HTTP requests?
        // CONSIDER: improve error handling throughout, including parsing response body into JSON
        public Uri SendZipFile(string folderPath, string zipFilePath, IProgressMonitor progressMonitor)
        {
            Uri uploadedDocumentUri;

            // Step 1 of 3: Ardia API - Create Staged Document
            HttpResponseMessage response = null;

            string singleDocumentPresignedUrl;
            string uploadId;
            try
            {
                // To enable verbose HTTP logging in .NET
                //      https://mikehadlow.blogspot.com/2012/07/tracing-systemnet-to-debug-http-clients.html
                var modelRequest = ArdiaStageDocumentRequest.Create();
                modelRequest.AddSingleDocumentPiece();

                var jsonString = JsonConvert.SerializeObject(modelRequest);
                using var request = new StringContent(jsonString, Encoding.UTF8, APPLICATION_JSON);
                using var ardiaHttpClient = AuthenticatedHttpClient();

                response = ardiaHttpClient.PostAsync(PATH_STAGE_DOCUMENT, request).Result;
                response.EnsureSuccessStatusCode();

                var responseString = response.Content.ReadAsStringAsync().Result;
                var modelResponse = JsonConvert.DeserializeObject<ArdiaStagedDocumentResponse>(responseString);

                singleDocumentPresignedUrl = modelResponse.Pieces[0].PresignedUrls[0];
                uploadId = modelResponse.UploadId;
            }
            catch (Exception e)
            {
                var uri = new Uri(ServerUri + PATH_STAGE_DOCUMENT);
                throw ArdiaServerException.CreateWithResponseDisposal(ArdiaResources.Ardia_FileUpload_UploadError, uri, response, e);
            }

            // Step 2 of 3: AWS - Upload File
            response = null;

            int uploadBytes;
            try
            {
                var awsUri = new Uri(singleDocumentPresignedUrl);

                var awsHttpClient = new HttpClient();
                var awsRequest = new HttpRequestMessage(HttpMethod.Put, awsUri);

                var bytes = File.ReadAllBytes(zipFilePath);
                uploadBytes = bytes.Length;

                var contentBytes = new ByteArrayContent(bytes);
                awsRequest.Content = contentBytes;

                response = awsHttpClient.SendAsync(awsRequest).Result;
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                var uri = new Uri(singleDocumentPresignedUrl);
                throw ArdiaServerException.CreateWithResponseDisposal(ArdiaResources.Ardia_FileUpload_UploadError, uri, response, e);
            }

            // Step 3 of 3: Ardia API - Create Document
            response = null;
            try
            {
                var modelRequest = new ArdiaDocumentRequest
                {
                    UploadId = uploadId,
                    Size = uploadBytes,
                    FileName = Path.GetFileName(zipFilePath),
                    FilePath = folderPath
                };

                var jsonString = JsonConvert.SerializeObject(modelRequest);
                using var request = new StringContent(jsonString);
                request.Headers.ContentType = new MediaTypeHeaderValue(APPLICATION_JSON);

                using var ardiaHttpClient = AuthenticatedHttpClient();

                response = ardiaHttpClient.PostAsync(PATH_CREATE_DOCUMENT, request).Result;
                response.EnsureSuccessStatusCode();

                var responseBody = response.Content.ReadAsStringAsync().Result;
                var ardiaDocumentResponse = JsonConvert.DeserializeObject<ArdiaDocumentResponse>(responseBody);
                uploadedDocumentUri = new Uri(ardiaDocumentResponse.PresignedUrls.First());

            }
            catch (Exception e)
            {
                var uri = new Uri(ServerUri + PATH_CREATE_DOCUMENT);
                throw ArdiaServerException.CreateWithResponseDisposal(ArdiaResources.Ardia_FileUpload_UploadError, uri, response, e);
            }

            return uploadedDocumentUri;
        }
    }

    // TODO - IMPORTANT: scrub access tokens from URI and responseBody
    // CONSIDER: error message tailored to problems parsing the response as JSON
    // CONSIDER: can more be reused with Panorama's error reporting, including PanoramaServerException?
    public class ArdiaServerException : Exception
    {
        public HttpStatusCode? HttpStatus { get; }
        private string ResponseBody { get; }

        public static ArdiaServerException CreateWithResponseDisposal(string message, Uri uri, HttpResponseMessage response, Exception e)
        {
            var statusCode = response.StatusCode;
            var responseBody = response.Content.ReadAsStringAsync().Result;

            var errorMessage = new ErrorMessageBuilder(message).ExceptionMessage(e.Message).Uri(uri).Response(responseBody).ToString();

            response?.Dispose();

            return new ArdiaServerException(statusCode, errorMessage, responseBody, e);
        }

        private ArdiaServerException(HttpStatusCode status, string message, string responseBody, Exception e) : base(message, e)
        {
            HttpStatus = status;
            ResponseBody = responseBody;
        }

        public string HttpErrorSummary()
        {
            var sb = new StringBuilder();
            if (HttpStatus != null)
            {
                sb.Append((int)HttpStatus);
                sb.Append(HttpStatus.ToString());
            }

            sb.Append(ResponseBody);

            return sb.ToString();
        }
    }
}