/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Results.RemoteApi.WatersConnect
{
    public class WatersConnectSession : RemoteSession
    {
        public WatersConnectSession(WatersConnectAccount account) : base(account)
        {
        }

        public WatersConnectAccount WatersConnectAccount { get { return (WatersConnectAccount) Account; } }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            if (!(remoteUrl is WatersConnectUrl wcUrl))
                throw new ArgumentException();

            if (wcUrl.Type == WatersConnectUrl.ItemType.folder_without_sample_sets)
                return AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException);

            if (wcUrl.Type == WatersConnectUrl.ItemType.folder)
                return AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException) &&
                       AsyncFetch(GetSampleSetsUrl(wcUrl), GetInjections, out remoteException);

            // wcUrl.Type == WatersConnectUrl.ItemType.sample_set
            return AsyncFetch(GetInjectionsUrl(wcUrl), GetFiles, out remoteException);
        }

        private IEnumerable<WatersConnectFolderObject> EnumerateChildFolderHierarchy(JObject currentFolder, string parentId)
        {
            var folderArray = currentFolder[@"children"] as JArray;
            if (folderArray != null)
                foreach (JObject folder in folderArray)
                {
                    foreach(var childFolder in EnumerateChildFolderHierarchy(folder, currentFolder[@"id"].Value<string>()))
                        yield return childFolder;
                }

            yield return new WatersConnectFolderObject(currentFolder, parentId, false);
        }

        private ImmutableList<WatersConnectFolderObject> GetFolders(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);

            var foldersValue = jsonObject[@"children"] as JArray;
            if (foldersValue == null)
            {
                return ImmutableList<WatersConnectFolderObject>.EMPTY;
            }
            return ImmutableList.ValueOf(EnumerateChildFolderHierarchy(foldersValue.First() as JObject, null));
        }

        private ImmutableList<WatersConnectFolderObject> GetInjections(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var itemsValue = JArray.Parse(responseBody);
            if (itemsValue == null)
            {
                return ImmutableList<WatersConnectFolderObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>()
                .Where(f => WatersConnectObject.GetProperty(f, @"contentType").StartsWith(@"SampleSet"))
                .Select(f => new WatersConnectFolderObject(f, null, true)));
        }

        private ImmutableList<WatersConnectFileObject> GetFiles(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var itemsValue = JArray.Parse(responseBody);
            if (itemsValue == null)
            {
                return ImmutableList<WatersConnectFileObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>().Select(f => new WatersConnectFileObject(f)));
        }

        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {
            var watersConnectUrl = (WatersConnectUrl) parentUrl;

            if (watersConnectUrl.Type != WatersConnectUrl.ItemType.sample_set)
            { 
                ImmutableList<WatersConnectFolderObject> folders, sampleSets;
                if (TryGetData(GetRootContentsUrl(), out folders))
                {
                    foreach (var folderObject in folders)
                    {
                        if (folderObject.ParentId == watersConnectUrl.SampleSetId)
                        {
                            var childUrl = watersConnectUrl.ChangeSampleSetId(folderObject.Id)
                                .ChangeType(folderObject.HasSampleSets
                                    ? WatersConnectUrl.ItemType.folder
                                    : WatersConnectUrl.ItemType.folder_without_sample_sets)
                                .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] { folderObject.Name }));
                            yield return new RemoteItem(childUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0);
                        }
                    }
                }
                if (TryGetData(GetSampleSetsUrl(watersConnectUrl), out sampleSets))
                {
                    foreach (var sampleSet in sampleSets)
                    {
                        var childUrl = watersConnectUrl.ChangeSampleSetId(sampleSet.Id)
                            .ChangeType(WatersConnectUrl.ItemType.sample_set)
                            .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] {sampleSet.Name}));
                        yield return new RemoteItem(childUrl, sampleSet.Name, DataSourceUtil.FOLDER_TYPE, null, 0);
                    }
                }
            }
            else
            {
                ImmutableList<WatersConnectFileObject> files;
                if (TryGetData(GetInjectionsUrl(watersConnectUrl), out files))
                {
                    foreach (var fileObject in files)
                    {
                        var childUrl = watersConnectUrl.ChangeInjectionId(fileObject.Id)
                            .ChangeSampleSetId(watersConnectUrl.SampleSetId)
                            .ChangeType(WatersConnectUrl.ItemType.injection)
                            .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] {fileObject.Name}))
                            .ChangeModifiedTime(fileObject.ModifiedAt);
                        yield return new RemoteItem(childUrl, fileObject.Name, DataSourceUtil.TYPE_WATERS_RAW, fileObject.ModifiedAt, 0);
                    }
                }
            }
        }

        public override void RetryFetchContents(RemoteUrl remoteUrl)
        {
            var watersConnectUrl = (WatersConnectUrl) remoteUrl;
            RetryFetch(GetRootContentsUrl(), GetFolders);
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.folder)
                RetryFetch(GetSampleSetsUrl(watersConnectUrl), GetFolders);
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.sample_set)
                RetryFetch(GetInjectionsUrl(watersConnectUrl), GetFiles);
        }

        private Uri GetRootContentsUrl()
        {
            return new Uri(WatersConnectAccount.ServerUrl + @"/waters_connect/v1.0/folders");
        }

        private Uri GetSampleSetsUrl(WatersConnectUrl watersConnectUrl)
        {
            if (null == watersConnectUrl.SampleSetId)
                return null;

            string url = string.Format(@"/waters_connect/v2.0/sample-sets?folderId={0}", watersConnectUrl.SampleSetId);
            return new Uri(WatersConnectAccount.ServerUrl + url);
        }

        private Uri GetInjectionsUrl(WatersConnectUrl watersConnectUrl)
        {
            if (null == watersConnectUrl.SampleSetId)
                return null;

            string url = string.Format(@"/waters_connect/v2.0/sample-sets/{0}/injection-data", watersConnectUrl.SampleSetId);
            return new Uri(WatersConnectAccount.ServerUrl + url);
        }
    }
}
