using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiSession : RemoteSession
    {
        public UnifiSession(UnifiAccount account) : base(account)
        {
        }

        public UnifiAccount UnifiAccount { get { return (UnifiAccount) Account; } }

        public override bool AsyncFetchContents(RemoteUrl remoteUrl, out RemoteServerException remoteException)
        {
            bool result = AsyncFetch(GetRootContentsUrl(), GetFolders, out remoteException);
            if (null == remoteException)
            {
                result = result && AsyncFetch(GetFileContentsUrl((UnifiUrl) remoteUrl), GetFiles, out remoteException);
            }
            return result;
        }

        private ImmutableList<UnifiFolderObject> GetFolders(Uri requestUri)
        {
            var httpClient = UnifiAccount.GetAuthenticatedHttpClient();
            string responseBody = RemoteHttpInterface.Instance.Get(httpClient, requestUri);
            var jsonObject = JObject.Parse(responseBody);

            var foldersValue = jsonObject["value"] as JArray;
            if (foldersValue == null)
            {
                return ImmutableList<UnifiFolderObject>.EMPTY;
            }
            return ImmutableList.ValueOf(foldersValue.OfType<JObject>().Select(f => new UnifiFolderObject(f)));
        }

        private ImmutableList<UnifiFileObject> GetFiles(Uri requestUri)
        {
            var httpClient = UnifiAccount.GetAuthenticatedHttpClient();
            var response = httpClient.GetAsync(requestUri).Result;
            string responseBody = response.Content.ReadAsStringAsync().Result;
            var jsonObject = JObject.Parse(responseBody);
            var itemsValue = jsonObject["value"] as JArray;
            if (itemsValue == null)
            {
                return ImmutableList<UnifiFileObject>.EMPTY;
            }
            return ImmutableList.ValueOf(itemsValue.OfType<JObject>().Select(f => new UnifiFileObject(f)));
        }

        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {
            var unifiUrl = (UnifiUrl) parentUrl;
            ImmutableList<UnifiFolderObject> folders;
            if (TryGetData(GetRootContentsUrl(), out folders))
            {
                foreach (var folderObject in folders)
                {
                    if (folderObject.ParentId == unifiUrl.Id)
                    {
                        var childUrl = unifiUrl.ChangeId(folderObject.Id)
                            .ChangePathParts(unifiUrl.GetPathParts().Concat(new[] {folderObject.Name}));
                        yield return new RemoteItem(childUrl, folderObject.Name, DataSourceUtil.FOLDER_TYPE, null, 0);
                    }
                }
            }
            ImmutableList<UnifiFileObject> files;
            if (TryGetData(GetFileContentsUrl(unifiUrl), out files))
            {
                foreach (var fileObject in files)
                {
                    var childUrl = unifiUrl.ChangeId(fileObject.Id)
                        .ChangePathParts(unifiUrl.GetPathParts().Concat(new[] {fileObject.Name}))
                        .ChangeModifiedTime(fileObject.ModifiedAt);
                    yield return new RemoteItem(childUrl, fileObject.Name, DataSourceUtil.TYPE_WATERS_RAW, fileObject.ModifiedAt, 0);
                }
            }
        }

        public override void RetryFetchContents(RemoteUrl chorusUrl)
        {
            var unifiUrl = (UnifiUrl) chorusUrl;
            RetryFetch(GetRootContentsUrl(), GetFolders);
            RetryFetch(GetFileContentsUrl(unifiUrl), GetFiles);
        }

        private Uri GetRootContentsUrl()
        {
            return new Uri(UnifiAccount.ServerUrl + "/unifi/v1/folders");
        }

        private Uri GetFileContentsUrl(UnifiUrl unifiUrl)
        {
            if (null == unifiUrl.Id)
            {
                return null;
            }
            string url = string.Format("/unifi/v1/folders({0})/items", unifiUrl.Id);
            return new Uri(UnifiAccount.ServerUrl + url);
        }
    }
}
