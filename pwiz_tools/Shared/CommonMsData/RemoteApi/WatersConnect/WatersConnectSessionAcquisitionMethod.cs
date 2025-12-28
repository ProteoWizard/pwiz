/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>
 *
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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectSessionAcquisitionMethod : WatersConnectSession
    {
        public static readonly string GET_METHODS_ENDPOINT = @"/waters_connect/v2.0/published-methods?methodTypeIds=17C19CE93BBA488A975B1E14AAAA0B9B";
        public static readonly string UPLOAD_METHOD_ENDPOINT = @"/waters_connect/method-develop/v1.0/tandem/create-mrm-methods";

        public WatersConnectSessionAcquisitionMethod(WatersConnectAccount account) : base(account)
        {
        }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            if (!(remoteUrl is WatersConnectUrl wcUrl))
                throw new ArgumentException();

            // Since this is acquisition methods session we should receive folder_with_methods URL, otherwise there is something wrong in the caller code.
            Assume.AreEqual(WatersConnectUrl.ItemType.folder_with_methods, wcUrl.Type,  @"Url type mismatch: " + wcUrl.Type);

            RemoteServerException ex1, ex2 = null;
            var res1 = AsyncFetch(GetRootContentsUrl(), GetFolders, out ex1);

            ImmutableList<WatersConnectFolderObject> folders;
            bool canReadMethods = false;
            if (TryGetData(GetRootContentsUrl(), out folders))
            {
                var currentFolder = folders.FirstOrDefault(f => f.Path.Equals(WebUtility.UrlDecode(wcUrl.EncodedPath)));
                canReadMethods = currentFolder?.CanRead ?? false;
            }
            if (canReadMethods)
            {
                var res2 = AsyncFetch(GetAcquisitionMethodsUrl(wcUrl), GetAcquisitionMethods, out ex2);
                remoteException = ex1 ?? ex2;
                return res1 && res2;
            }

            remoteException = ex1;
            return res1;
        }

        private ImmutableList<WatersConnectAcquisitionMethodObject> GetAcquisitionMethods(Uri requestUri)
        {
            var response = _httpClient.GetAsync(requestUri).Result;
            EnsureSuccess(response);

            string responseBody = response.Content.ReadAsStringAsync().Result;
            
            var itemsValue = JArray.Parse(responseBody);
            if (itemsValue.Count == 0)
            {
                return ImmutableList<WatersConnectAcquisitionMethodObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>()
                .Select(f => new WatersConnectAcquisitionMethodObject(f)));
        }

        public string UploadMethod(string methodName, string methodJson, IProgressMonitor progressMonitor)
        {
            var requestUri = GetAquisitionMethodUploadUrl();
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            if (progressMonitor == null)
                request.Content = new StringContent(methodJson);
            else
            {
                // we want to show progress during upload since it can be a long operation
                var status = new ProgressStatus(string.Format(WatersConnectResources.WatersConnectSessionAcquisitionMethod_UploadMethod_Uploading_method__0__to__1_,
                    methodName, WatersConnectAccount.ServerUrl));
                // here the StringStream is just a wrapper. It is used because the ProgressStream requires an underlying stream for its constructor.
                var progressStream = new ProgressStream(new MemoryStream(Encoding.UTF8.GetBytes(methodJson)));
                progressMonitor.UpdateProgress(status);
                progressStream.SetProgressMonitor(progressMonitor, status, true);
                request.Content = new StreamContent(progressStream);
            }
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(@"application/json");
            request.Content.Headers.ContentType.CharSet = @"utf-8";
            var response = _httpClient.SendAsync(request).Result;
            EnsureSuccess(response);
            return response.Content?.ReadAsStringAsync().Result;
        }
        
        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {

            var watersConnectUrl = (WatersConnectUrl)parentUrl;
            if (TryGetFolderByUrl(watersConnectUrl, out var folder))
                watersConnectUrl = watersConnectUrl.ChangeFolderOrSampleSetId(folder.Id);

            ImmutableList<WatersConnectFolderObject> folders;
            if (TryGetData(GetRootContentsUrl(), out folders))
            {
                foreach (var folderObject in folders)
                {
                    if (folderObject.ParentId == watersConnectUrl.FolderOrSampleSetId || 
                        string.IsNullOrEmpty(watersConnectUrl.EncodedPath) && string.IsNullOrEmpty(folderObject.ParentId)) //root folder condition
                    {
                        var childUrl = watersConnectUrl.ChangeFolderOrSampleSetId(folderObject.Id)
                            .ChangeType(WatersConnectUrl.ItemType.folder_with_methods)
                            .ChangePathPartsOnly(watersConnectUrl.GetPathParts().Concat(new[] { folderObject.Name }));
                        var itemType = DataSourceUtil.FOLDER_TYPE;

                        var accessType = folderObject.CanRead ? AccessType.read  : AccessType.no_access;
                        if (folderObject.CanWrite)
                            accessType = AccessType.read_write;

                        yield return new RemoteItem(childUrl, folderObject.Name, itemType, null, 0, false, accessType);
                    }
                }
            }
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.folder_with_methods)
            {
                ImmutableList<WatersConnectAcquisitionMethodObject> acquisitionMethods;

                if (TryGetData(GetAcquisitionMethodsUrl(watersConnectUrl), out acquisitionMethods))
                {
                    foreach (var acquisitionMethod in acquisitionMethods)
                    {
                        if (Guid.TryParse(acquisitionMethod.MethodVersionId, out var versionGuid))
                        {
                            var methodUrl = new WatersConnectAcquisitionMethodUrl(watersConnectUrl.ToString())
                                .ChangeMethodName(acquisitionMethod.Name)
                                .ChangeAcquisitionMethodId(acquisitionMethod.Id)
                                .ChangeMethodVersionId(versionGuid)
                                .ChangePathParts(watersConnectUrl.GetPathParts())
                                .ChangeModifiedTime(acquisitionMethod.ModifiedDateTime);

                            yield return new RemoteItem(methodUrl, acquisitionMethod.Name,
                                DataSourceUtil.TYPE_WATERS_ACQUISITION_METHOD, null, 0);
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
 
            //  Note: The GUID ‘17C19CE93BBA488A975B1E14AAAA0B9B’ is a fixed ID for the Acquisition Method type.
            //  v1.0 uses 'methodTypeId'
            //  v2.0 uses 'methodTypeIds' which is comma delimited entries

            string id = "";
            if (string.IsNullOrEmpty(watersConnectUrl.FolderOrSampleSetId))
            {
                // Try to retrieve the folder Id from the folder data
                if (TryGetFolderByUrl(watersConnectUrl, out var folder))
                    id = folder.Id ;
            }
            else
            {
                id = watersConnectUrl.FolderOrSampleSetId;
            }

            if (string.IsNullOrEmpty(id))
                return null;

            string url = GET_METHODS_ENDPOINT + string.Format(@"&folderId={0}", id);

            return new Uri(WatersConnectAccount.ServerUrl + url);
        }

        private Uri GetAquisitionMethodUploadUrl()
        {
            return new Uri(WatersConnectAccount.ServerUrl + UPLOAD_METHOD_ENDPOINT);
        }
    }

}
