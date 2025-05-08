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
using System.Linq;
using System.Runtime.InteropServices;
using IdentityModel.Client;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.FileUI;
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

            if (wcUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_only)
                return AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException);

            if (wcUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_sample_sets)
            {
                var gotFolders = AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException);
                if (wcUrl.EncodedPath == null) // assume root folder cannot have sample sets
                    return gotFolders;
                var sampleSetsUrl = GetSampleSetsUrl(wcUrl);
                if (sampleSetsUrl != null)
                    return gotFolders && AsyncFetch(sampleSetsUrl, GetInjections, out remoteException);
                // if sampleSetsUrl is null, last path segment may be a sample_set misclassified as a folder
            }

            // wcUrl.Type == WatersConnectUrl.ItemType.sample_set
            var injectionsUrl = GetInjectionsUrl(wcUrl);
            if (injectionsUrl == null)
            {
                remoteException = null;
                return false; // fetch not complete
            }
            return AsyncFetch(injectionsUrl, GetFiles, out remoteException);
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

        /// <summary>
        /// Checks if an object (folder, data file, method etc.) exists on the remote server. If the path begins with a slash, 
        /// it is assumed to be an absolute path. otherwise it is checked relative to the current directory.
        /// </summary>
        /// <param name="path">remote path to check.</param>
        /// <param name="fullPath">absolute path built from the path and current directory</param>
        /// <param name="currentDirectory">current directory on the remote server</param>
        /// <returns>true if the folder exists.</returns>
        public virtual bool PathExists(string path, out WatersConnectUrl fullPath, RemoteUrl currentDirectory)
        {
            if (!(currentDirectory is WatersConnectUrl watersCurrentDirectory))
            {
                fullPath = null;
                return false;
            }
            if (!UrlPath.IsRooted(path))
                path = UrlPath.Combine(currentDirectory.EncodedPath, path);

            var foldersUrl = GetRootContentsUrl();
            fullPath = null;
            ImmutableList<WatersConnectFolderObject> folders;
            if (TryGetData(foldersUrl, out folders))
            {
                var folder = folders.FirstOrDefault(folder => folder.Path.Equals(path));
                if (folder != null)
                {
                    fullPath = folder.ToUrl(watersCurrentDirectory);
                    return true;
                }
            }
            var sampleSetsUrl = GetSampleSetsUrl(watersCurrentDirectory);
            return false;
        }

        protected void EnsureSuccess(System.Net.HttpStatusCode status,  string responseBody)
        {
            if (status >= System.Net.HttpStatusCode.BadRequest)
            {
                throw new RemoteServerException(string.Format("Waters Connect server returns an error: {0}", responseBody));
            }
        }
        protected ImmutableList<WatersConnectFolderObject> GetFolders(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            EnsureSuccess(response.StatusCode, responseBody);
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
            string responseBody = response.Content.ReadAsStringAsync().Result;
            EnsureSuccess(response.StatusCode, responseBody);
            var itemsValue = JArray.Parse(responseBody);
            if (itemsValue == null)
            {
                return ImmutableList<WatersConnectFolderObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>()
                .Where(f => WatersConnectObject.GetProperty(f, @"contentType").StartsWith(@"SampleSet"))
                .Select(f => new WatersConnectFolderObject(f, null, true)));
        }
        
        protected ImmutableList<WatersConnectFileObject> GetFiles(Uri requestUri)
        {
            var httpClient = WatersConnectAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            EnsureSuccess(response.StatusCode, responseBody);
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
                            .ChangeType(folderObject.HasSampleSets
                                ? WatersConnectUrl.ItemType.folder_child_folders_sample_sets
                                : WatersConnectUrl.ItemType.folder_child_folders_only)
                            .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] { folderObject.Name }));
                        yield return new RemoteItem(childUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0, folderObject);
                    }
                }
            }
             
            var sampleSetsUrl = GetSampleSetsUrl(watersConnectUrl);
            if (watersConnectUrl.Type != WatersConnectUrl.ItemType.sample_set && sampleSetsUrl != null)
            { 
                ImmutableList<WatersConnectFolderObject> sampleSets;
                if (TryGetData(sampleSetsUrl, out sampleSets))
                {
                    watersConnectUrl = GetWatersConnectUrlWithFolderOrSampleSetId(watersConnectUrl, false);
                    if (watersConnectUrl != null)
                        foreach (var sampleSet in sampleSets)
                        {
                            var childUrl = watersConnectUrl.ChangeFolderOrSampleSetId(sampleSet.Id)
                                .ChangeType(WatersConnectUrl.ItemType.sample_set)
                                .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] {sampleSet.Name}));
                            yield return new RemoteItem(childUrl, sampleSet.Name, DataSourceUtil.FOLDER_TYPE, null, 0, sampleSet);
                        }
                }
            }
            else
            {
                ImmutableList<WatersConnectFileObject> files;
                if (TryGetData(GetInjectionsUrl(watersConnectUrl), out files))
                {
                    watersConnectUrl = GetWatersConnectUrlWithFolderOrSampleSetId(watersConnectUrl, true);
                    if (watersConnectUrl != null)
                        foreach (var fileObject in files)
                        {
                            var childUrl = watersConnectUrl.ChangeInjectionId(fileObject.Id)
                                .ChangeFolderOrSampleSetId(watersConnectUrl.FolderOrSampleSetId)
                                .ChangeType(WatersConnectUrl.ItemType.injection)
                                .ChangePathParts(watersConnectUrl.GetPathParts().Concat(new[] {fileObject.Name}))
                                .ChangeModifiedTime(fileObject.ModifiedAt);
                            yield return new RemoteItem(childUrl, fileObject.Name, DataSourceUtil.TYPE_WATERS_RAW, fileObject.ModifiedAt, 0, fileObject);
                        }
                }
            }
        }

        public override void RetryFetchContents(RemoteUrl remoteUrl)
        {
            var watersConnectUrl = (WatersConnectUrl) remoteUrl;
            RetryFetch(GetRootContentsUrl(), GetFolders);
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.folder_child_folders_sample_sets)
                RetryFetch(GetSampleSetsUrl(watersConnectUrl), GetFolders);
            if (watersConnectUrl.Type == WatersConnectUrl.ItemType.sample_set)
                RetryFetch(GetInjectionsUrl(watersConnectUrl), GetFiles);
        }

        protected Uri GetRootContentsUrl()
        {
            return new Uri(WatersConnectAccount.ServerUrl + @"/waters_connect/v1.0/folders");
        }

        protected WatersConnectUrl GetWatersConnectUrlWithFolderOrSampleSetId(WatersConnectUrl watersConnectUrl, bool fetchSamples)
        {
            if (watersConnectUrl.FolderOrSampleSetId != null || watersConnectUrl.EncodedPath == null)
                return watersConnectUrl;

            // if url folder id is not set, try to get folder list and find it
            ImmutableList<WatersConnectFolderObject> folders;
            if (TryGetData(GetRootContentsUrl(), out folders))
            {
                var pathParts = watersConnectUrl.GetPathParts().ToArray();
                foreach (var folder in folders)
                {
                    if (folder.Path == watersConnectUrl.GetFilePath())
                        return watersConnectUrl.ChangeFolderOrSampleSetId(folder.Id);
                }

                // handle case where last path segment is a sample set name instead of a folder name
                var folderPathIfLastSegmentIsSampleSet = string.Join(@"/", pathParts.Take(pathParts.Length - 1));
                foreach (var folder in folders)
                {
                    if (folder.Path != folderPathIfLastSegmentIsSampleSet)
                        continue;

                    // found the folder

                    // if fetchSamples is false, return null to indicate to calling code that we're waiting for fetch
                    if (!fetchSamples)
                        return null;

                    // else get sample sets for it
                    var sampleSetsUrl = new Uri(WatersConnectAccount.ServerUrl + string.Format(@"/waters_connect/v2.0/sample-sets?folderId={0}", folder.Id));
                    ImmutableList<WatersConnectFolderObject> sampleSets;
                    if (!TryGetData(sampleSetsUrl, out sampleSets))
                    {
                        AsyncFetch(sampleSetsUrl, GetInjections, out _);
                        return null;
                    }
                    foreach (var sampleSet in sampleSets)
                    {
                        if (sampleSet.Name == watersConnectUrl.LastPathPart)
                            return watersConnectUrl.ChangeFolderOrSampleSetId(sampleSet.Id);
                    }
                }
            }

            return null; // could not find folder id or sample set id
        }

        private Uri GetSampleSetsUrl(WatersConnectUrl watersConnectUrl)
        {
            watersConnectUrl = GetWatersConnectUrlWithFolderOrSampleSetId(watersConnectUrl, false);

            if (null == watersConnectUrl)
                return null;

            string url = string.Format(@"/waters_connect/v2.0/sample-sets?folderId={0}", watersConnectUrl.FolderOrSampleSetId);
            return new Uri(WatersConnectAccount.ServerUrl + url);
        }

        private Uri GetInjectionsUrl(WatersConnectUrl watersConnectUrl)
        {
            watersConnectUrl = GetWatersConnectUrlWithFolderOrSampleSetId(watersConnectUrl, true);

            if (null == watersConnectUrl)
                return null;

            string url = string.Format(@"/waters_connect/v2.0/sample-sets/{0}/injection-data", watersConnectUrl.FolderOrSampleSetId);
            return new Uri(WatersConnectAccount.ServerUrl + url);
        }
    }
}
