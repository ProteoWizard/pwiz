/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
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
using System.Linq;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Ardia
{
    public class ArdiaSession : RemoteSession
    {
        public ArdiaSession(ArdiaAccount account) : base(account)
        {
        }

        public ArdiaAccount ArdiaAccount { get { return (ArdiaAccount) Account; } }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            if (!(remoteUrl is ArdiaUrl ardiaUrl))
                throw new ArgumentException();

            if (ardiaUrl.SequenceKey != null)
                return AsyncFetch(GetFolderContentsUrl(ardiaUrl), GetRawFiles, out remoteException);
            else
                return AsyncFetch(GetFolderContentsUrl(ardiaUrl), GetFoldersAndSequences, out remoteException);
        }

        private ImmutableList<ArdiaFolderObject> GetFoldersAndSequences(Uri requestUri)
        {
            using var httpClient = ArdiaAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var itemsValue = jsonObject[@"children"] as JArray;
            if (itemsValue == null)
                return ImmutableList<ArdiaFolderObject>.EMPTY;

            Func<JObject, bool> isArdiaFolderOrSequence = f => f[@"type"].Value<string>().Contains(@"folder") ||
                                                               f[@"type"].Value<string>().Contains(@"sequence");
            string parentPath = (Account as ArdiaAccount)?.GetPathFromFolderContentsUrl(requestUri.AbsoluteUri);
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>().Where(isArdiaFolderOrSequence).Select(f => new ArdiaFolderObject(f, parentPath)));
        }

        private ImmutableList<ArdiaFileObject> GetRawFiles(Uri requestUri)
        {
            using var httpClient = ArdiaAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var itemsValue = jsonObject[@"injections"] as JArray;
            if (itemsValue == null)
                return ImmutableList<ArdiaFileObject>.EMPTY;

            return ImmutableList.ValueOf(itemsValue.OfType<JObject>().Select(f => new ArdiaFileObject(f)));
        }

        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {
            var ardiaUrl = (ArdiaUrl) parentUrl;
            ImmutableList<ArdiaFolderObject> folders;
            if (TryGetData(GetFolderContentsUrl(ardiaUrl), out folders) && folders != null)
            {
                foreach (var folderObject in folders.OfType<ArdiaFolderObject>())
                {
                    if (folderObject.ParentId == "" || folderObject.ParentId.TrimStart('/') == ardiaUrl.EncodedPath)
                    {
                        var childUrl = ardiaUrl.ChangeId(folderObject.Id);
                        if (folderObject.SequenceKey != null)
                            childUrl = childUrl.ChangeSequenceKey(folderObject.SequenceKey);
                        var baseChildUrl = childUrl.ChangePathParts(ardiaUrl.GetPathParts().Concat(new[] { folderObject.Name }));
                        yield return new RemoteItem(baseChildUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0);
                    }
                }
            }

            ImmutableList<ArdiaFileObject> files;
            if (TryGetData(GetFolderContentsUrl(ardiaUrl), out files) && files != null)
            {
                foreach (var fileObject in files.OfType<ArdiaFileObject>())
                {
                    string rawName = fileObject.Name.ToLowerInvariant().EndsWith(DataSourceUtil.EXT_THERMO_RAW)
                        ? fileObject.Name
                        : fileObject.Name + DataSourceUtil.EXT_THERMO_RAW;

                    var childUrl = ardiaUrl.ChangeId(fileObject.Id)
                        .ChangeRawName(rawName)
                        .ChangeRawSize(fileObject.Size)
                        .ChangeStorageId(fileObject.StorageId)
                        .ChangePathParts(ardiaUrl.GetPathParts().Concat(new[] { fileObject.Name }))
                        .ChangeModifiedTime(fileObject.ModifiedAt);
                    yield return new RemoteItem(childUrl, fileObject.Name, DataSourceUtil.TYPE_THERMO_RAW, fileObject.ModifiedAt, fileObject.Size ?? 0);
                }
            }
        }

        public override void RetryFetchContents(RemoteUrl remoteUrl)
        {
            var ardiaUrl = (ArdiaUrl) remoteUrl;
            RetryFetch(GetFolderContentsUrl(ardiaUrl), GetFoldersAndSequences);
            RetryFetch(GetFolderContentsUrl(ardiaUrl), GetRawFiles);
        }

        private Uri GetFolderContentsUrl(ArdiaUrl ardiaUrl)
        {
            return new Uri(ArdiaAccount.GetFolderContentsUrl(ardiaUrl));
        }

        private Uri GetFolderContentsUrl(string folder = "")
        {
            return new Uri(ArdiaAccount.GetFolderContentsUrl(folder));
        }

        private Uri GetFileContentsUrl(ArdiaUrl ardiaUrl)
        {
            return GetFolderContentsUrl(ArdiaAccount.GetPathFromFolderContentsUrl(ardiaUrl.ServerUrl));
        }
    }
}
