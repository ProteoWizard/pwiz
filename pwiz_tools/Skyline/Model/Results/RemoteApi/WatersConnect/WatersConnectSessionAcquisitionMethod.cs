/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.WatersConnect
{
    public class WatersConnectSessionAcquisitionMethod : WatersConnectSession
    {
        public WatersConnectSessionAcquisitionMethod(WatersConnectAccount account) : base(account)
        {
        }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            if (!(remoteUrl is WatersConnectUrl wcUrl))
                throw new ArgumentException();


            if (wcUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_only)
                return AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException);

            //  Also return the next for WatersConnectUrl.ItemType.folder_child_folders_sample_sets since that is the default
             if (wcUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods)
             {
                 RemoteServerException ex1, ex2 = null;
                var res = AsyncFetch(GetRootContentsUrl(), GetFolders, out ex1) &&
                        AsyncFetch(GetAcquisitionMethodsUrl(wcUrl), GetAcquisitionMethods, out ex2);
                remoteException = ex1 ?? ex2;
                return res;
            }

             throw new Exception("Url type mismatch: " + wcUrl.Type);
        }

        private ImmutableList<WatersConnectAcquisitionMethodObject> GetAcquisitionMethods(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;

            if (IsGetAcquisitionMethodsNotAllowed(response))
            {
                return ImmutableList<WatersConnectAcquisitionMethodObject>.Singleton(WatersConnectAcquisitionMethodObject.NO_ACCESS_INDICATOR);
            }

            string responseBody = response.Content.ReadAsStringAsync().Result;
            EnsureSuccess(response.StatusCode, responseBody);
            
            var itemsValue = JArray.Parse(responseBody);
            if (itemsValue == null)
            {
                return ImmutableList<WatersConnectAcquisitionMethodObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>()
                .Select(f => new WatersConnectAcquisitionMethodObject(f)));
        }

        public void UploadMethod(Model.WatersConnect.MethodModel method, IProgressMonitor progressMonitor)
        {
            JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
            var json = JsonConvert.SerializeObject(method,
                Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var requestUri = GetAquisitionMethodUploadUrl();
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            if (progressMonitor == null)
                request.Content = new StringContent(json);
            else
            {
                var status = new ProgressStatus(string.Format("Uploading method {0} to {1}", method.Name, WatersConnectAccount.ServerUrl));
                var progressStream = new ProgressStream(new StringStream(json));
                progressMonitor.UpdateProgress(status);
                progressStream.SetProgressMonitor(progressMonitor, status, true);
                request.Content = new StreamContent(progressStream);
            }
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(@"application/json");
            request.Content.Headers.ContentType.CharSet = @"utf-8";
            var response = httpClient.SendAsync(request).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            EnsureSuccess(response.StatusCode, responseBody);
        }
        // TODO [RC] Make sure we really need this logic. Access level is read as part of 
        // the folder JSON
        private bool IsGetAcquisitionMethodsNotAllowed(HttpResponseMessage response)
        {
            try
            {
                string responseBody = response.Content.ReadAsStringAsync().Result;
                var responseParsedRoot = JObject.Parse(responseBody);

                var errorsJToken = responseParsedRoot.GetValue(@"errors");

                if (errorsJToken == null)
                {
                    return false;
                }

                var errorsJArray = (JArray)errorsJToken;

                foreach (var error in errorsJArray)
                {
                    var errorCode = error[@"errorCode"];

                    var errorCodeString = errorCode.ToString();

                    if (string.Equals(errorCodeString, @"NoFolderAccess"))
                    {
                        return true;
                    }
                }

                // "errors\":[{\"errorCode\":\"NoFolderAccess\",\"details\":\"The user does not have access to the given folder.\"}]}"

                return false;


            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {
            var watersConnectUrl = (WatersConnectUrl) parentUrl;

            ImmutableList<WatersConnectFolderObject> folders;
            if (TryGetData(GetRootContentsUrl(), out folders))
            {
                foreach (var folderObject in folders)
                {
                    if (watersConnectUrl.EncodedPath != null && folderObject.ParentId == null)
                        continue; // path is set but FolderOrSampleSetId may be null

                    if (folderObject.ParentId == watersConnectUrl.FolderOrSampleSetId)
                    {
                        var childUrl = watersConnectUrl.ChangeFolderOrSampleSetId(folderObject.Id)
                            .ChangeType(folderObject.AccessTypeReadTrue
                                ? WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods
                                : WatersConnectUrl.ItemType.folder_child_folders_only)
                            .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] { folderObject.Name }));
                        yield return new RemoteItem(childUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0, folderObject);
                    }
                }
            }
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods)
            {
                ImmutableList<WatersConnectAcquisitionMethodObject> acquisitionMethods;

                if (TryGetData(GetAcquisitionMethodsUrl(watersConnectUrl), out acquisitionMethods))
                {
                    if (acquisitionMethods.Any(method =>
                            method == WatersConnectAcquisitionMethodObject.NO_ACCESS_INDICATOR))
                    {
                        yield return
                            new RemoteItem(null, "No access to acquisition methods in this folder.",
                                DataSourceUtil.NO_ACCESS, DateTime.Now, Int64.MinValue);
                    }
                    else
                    {
                        foreach (var acquisitionMethod in acquisitionMethods)
                        {
                            if (Guid.TryParse(acquisitionMethod.MethodVersionId, out var versionGuid))
                            {
                                var methodUrl = new WatersConnectAcquisitionMethodUrl(watersConnectUrl.ToString())
                                    .ChangeAcquisitionMethodId(acquisitionMethod.Id)
                                    .ChangeMethodVersionId(versionGuid)
                                    .ChangePathParts(watersConnectUrl.GetPathParts()
                                        .Concat(new[] { acquisitionMethod.Name }))
                                    .ChangeModifiedTime(acquisitionMethod.ModifiedDateTime);

                                yield return new RemoteItem(methodUrl, acquisitionMethod.Name,
                                    DataSourceUtil.TYPE_WATERS_ACQUISITION_METHOD, null, 0, acquisitionMethod);
                            }
                        }
                    }
                }
            }
        }

        public override void RetryFetchContents(RemoteUrl remoteUrl)
        {
            var watersConnectUrl = (WatersConnectUrl) remoteUrl;
            RetryFetch(GetRootContentsUrl(), GetFolders); 
            RetryFetch(GetAcquisitionMethodsUrl(watersConnectUrl), GetFolders);
        }
        
        private Uri GetAcquisitionMethodsUrl(WatersConnectUrl watersConnectUrl)
        {
            if (null == watersConnectUrl?.FolderOrSampleSetId)
            {
                //  NO Folder Id passed in.  Assume all Method Templates are in a folder
                return null;
            }

            //  Note: The GUID ‘17C19CE93BBA488A975B1E14AAAA0B9B’ is a fixed ID for the Acquisition Method type.
            //  v1.0 uses 'methodTypeId'
            //  v2.0 uses 'methodTypeIds' which is comma delimited entries

            var folderIdWithId = "";

            if (!string.IsNullOrEmpty(watersConnectUrl.FolderOrSampleSetId))
            {
                folderIdWithId = string.Format(@"&folderId={0}", watersConnectUrl.FolderOrSampleSetId);
            }

            string url = @"/waters_connect/v2.0/published-methods?methodTypeIds=17C19CE93BBA488A975B1E14AAAA0B9B" + folderIdWithId;


            //  TODO  [RC] Check if method definition can be downloaded
            // url += @"&expand=definition";

            return new Uri(WatersConnectAccount.ServerUrl + url);
        }

        private Uri GetAquisitionMethodUploadUrl()
        {
            return new Uri(WatersConnectAccount.ServerUrl + @"/waters_connect/v1.0/acq-method-versions");
        }
    }

    class StringStream : Stream
    {
        private readonly MemoryStream _memoryStream;
        private readonly string _string;
        public StringStream(string str)
        {
            _string = str;
            _memoryStream = new MemoryStream();
            var writer = new StreamWriter(_memoryStream);
            writer.Write(str);
            writer.Flush();
            _memoryStream.Position = 0;
        }
        public override void Flush()
        {
            _memoryStream.Flush();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _memoryStream.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            _memoryStream.SetLength(value);
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _memoryStream.Read(buffer, offset, count);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _memoryStream.Length;
        public override long Position
        {
            get => _memoryStream.Position;
            set => _memoryStream.Position = value;
        }
    }

}
