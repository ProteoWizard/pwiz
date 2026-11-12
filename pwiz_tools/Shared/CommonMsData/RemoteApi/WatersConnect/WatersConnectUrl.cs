/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;

namespace pwiz.CommonMsData.RemoteApi.WatersConnect
{
    public class WatersConnectUrl : RemoteUrl
    {
        public static readonly WatersConnectUrl Empty = new WatersConnectUrl(UrlPrefix);
        public static string UrlPrefix { get { return RemoteAccountType.WATERS_CONNECT.Name + @":"; } }

        public WatersConnectUrl(string watersConnectUrl) : base(watersConnectUrl)
        {
        }

        public enum ItemType
        {
            folder,
            folder_without_sample_sets,
            sample_set, // a collection of related injections
            injection,   // like a .raw file
            folder_with_methods,
            method       // a Waters acquisition method
        }

        public new enum Attr
        {
            type,
            id,
            injectionId,
            sampleSetId // Read only: lets a user name a sample set by its waters_connect id
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            InjectionId = nameValueParameters.GetValue(Attr.injectionId.ToString());
            var sampleSetId = nameValueParameters.GetValue(Attr.sampleSetId.ToString());
            FolderOrSampleSetId = nameValueParameters.GetValue(Attr.id.ToString()) ?? sampleSetId;
            Type = (ItemType?) nameValueParameters.GetLongValue(Attr.type.ToString()) ??
                   (sampleSetId != null ? ItemType.sample_set : ItemType.folder);
        }
        public string InjectionId { get; private set; }
        public string FolderOrSampleSetId { get; private set; }
        public ItemType Type { get; protected set; }
        
        public override string SourceType
        {
            get { return Type == ItemType.sample_set ? DataSourceUtil.SAMPLE_SET_TYPE : DataSourceUtil.FOLDER_TYPE; }
        }

        public WatersConnectUrl ChangeInjectionId(string id)
        {
            return ChangeProp(ImClone(this), im => im.InjectionId = id);
        }

        public WatersConnectUrl ChangeFolderOrSampleSetId(string id)
        {
            return ChangeProp(ImClone(this), im => im.FolderOrSampleSetId = id);
        }

        public WatersConnectUrl ChangeType(ItemType type)
        {
            return ChangeProp(ImClone(this), im => im.Type = type);
        }

        public override RemoteUrl ChangePathParts(IEnumerable<string> parts)
        {
            var type = Type;
            var result = (WatersConnectUrl) base.ChangePathParts(parts);
            result.FolderOrSampleSetId = null;
            if (type != ItemType.folder && type != ItemType.folder_with_methods)
                result.Type = ItemType.folder; 
            return result;
        }

        // waters_connect folder, sample set and injection names are not file names: a '.' in one does
        // not start an extension, so "QC_0.5ng" must not become "QC_0"
        public override string GetFileName()
        {
            return LastPathPart;
        }

        public override string GetFileNameWithoutExtension()
        {
            return LastPathPart;
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return false;
        }

        public bool SupportsMethodDevelopment(out string reason)
        {
            reason = null;
            if (FindMatchingAccount() is WatersConnectAccount wca)
            {
                return wca.SupportsMethodDevelopment(out reason);
            }
            return false;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.WATERS_CONNECT; }
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetValue(Attr.injectionId.ToString(), InjectionId);
            result.SetValue(Attr.id.ToString(), FolderOrSampleSetId);
            result.SetLongValue(Attr.type.ToString(), (long) Type);
            return result;
        }

        public string GetAuthenticatedUrl()
        {
            return GetMsDataUrl();
        }

        private string GetMsDataUrl()
        {
            var account = FindMatchingAccount() as WatersConnectAccount;
            if (account == null)
            {
                throw new RemoteServerException(string.Format(WatersConnectResources.WatersConnectUrl_OpenMsDataFile_Cannot_find_account_for_username__0__and_server__1__,
                    Username, ServerUrl));
            }

            // ReSharper disable LocalizableElement
            string serverUrl = ServerUrl.Replace("://", "://" + account.Username + ":" + account.Password + "@");
            serverUrl += $@"/?sampleSetId={FolderOrSampleSetId}&injectionId={InjectionId}";
            serverUrl += "&identity=" + Uri.EscapeDataString(account.IdentityServer) + "&scope=" +
                         Uri.EscapeDataString(account.ClientScope) + "&secret=" +
                         Uri.EscapeDataString(account.ClientSecret) + "&clientId=" +
                         Uri.EscapeDataString(account.ClientId);
            // ReSharper restore LocalizableElement
            return serverUrl;
        }

        public override MsDataFileImpl OpenMsDataFile(OpenMsDataFileParams openParams)
        {
            Assume.IsNotNull(FolderOrSampleSetId);
            Assume.IsNotNull(InjectionId);
            return new MsDataFileImpl(GetMsDataUrl(), 0, LockMassParameters, openParams.SimAsSpectra,
                requireVendorCentroidedMS1: openParams.CentroidMs1, requireVendorCentroidedMS2: openParams.CentroidMs2,
                ignoreZeroIntensityPoints: openParams.IgnoreZeroIntensityPoints, preferOnlyMsLevel: openParams.PreferOnlyMs1 ? 1 : 0);
        }

        /// <summary>
        /// True if the value is a friendly waters_connect path of the form
        /// "waters_connect:&lt;account alias&gt;/Path/To/Injection". The friendly form has no '=' after the
        /// prefix, which distinguishes it from the serialized query-string form (path=...&amp;server=...).
        /// </summary>
        public static bool IsFriendlyUrl(string value)
        {
            return value != null && value.StartsWith(UrlPrefix) &&
                   value.Length > UrlPrefix.Length && value.IndexOf('=', UrlPrefix.Length) < 0;
        }

        /// <summary>
        /// Parses a friendly waters_connect path into a path-form URL whose path parts are the account
        /// alias followed by the folder/sample-set/injection names. The result is unresolved (it has no
        /// server-assigned ids); call <see cref="ResolveInjection"/> to navigate the server and fill them in.
        /// </summary>
        public static WatersConnectUrl ParseFriendly(string value)
        {
            var pathParts = value.Substring(UrlPrefix.Length)
                .Split('/').Where(part => !string.IsNullOrEmpty(part));
            return (WatersConnectUrl) Empty.ChangePathParts(pathParts);
        }

        /// <summary>
        /// Resolves a waters_connect URL to a concrete injection. Accepts a friendly path, a path-form URL
        /// (with a server), or an id form ("waters_connect:sampleSetId=...&amp;injectionId=...");
        /// see <see cref="FindAccount"/> for how each picks its account. A path names the injection by its
        /// folder, sample set and injection names. An id form needs the sample set id as well as the
        /// injection id, because the server cannot look an injection up by its id alone. Returns this URL
        /// unchanged if it is already resolved (has a server, a path and both ids). Throws
        /// <see cref="RemoteServerException"/> if the account or injection cannot be found or the server
        /// does not respond.
        /// </summary>
        public WatersConnectUrl ResolveInjection()
        {
            if (InjectionId != null && FolderOrSampleSetId != null && ServerUrl != null && EncodedPath != null)
                return this;

            // An injection id needs its sample set id, and a URL with no injection id needs a path
            if (InjectionId != null ? FolderOrSampleSetId == null : EncodedPath == null)
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveInjection_Name_a_waters_connect_injection_by_its_path_or_its_ids_);

            var account = FindAccount(out var dataPathParts);
            using (var session = (WatersConnectSession) RemoteSession.CreateSession(account))
            {
                if (InjectionId != null)
                {
                    var sampleSetUrl = FindSampleSetById(session, account, FolderOrSampleSetId);
                    var injectionUrl = FetchContents(session, sampleSetUrl)
                        .Select(item => (WatersConnectUrl) item.MsDataFileUri)
                        .FirstOrDefault(url => WatersConnectObject.IdEquals(url.InjectionId, InjectionId));
                    if (injectionUrl == null)
                    {
                        throw new RemoteServerException(string.Format(
                            WatersConnectResources.WatersConnectUrl_ResolveInjection_Could_not_find_the_waters_connect_injection_with_the_id___0__,
                            InjectionId));
                    }
                    return injectionUrl;
                }

                var injectionPathUrl = FindItemByPath(session, account, dataPathParts);
                if (injectionPathUrl?.Type != ItemType.injection)
                {
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_ResolveInjection_Could_not_find_the_injection___0___under_the_waters_connect_path___1__,
                        dataPathParts.LastOrDefault() ?? string.Empty, string.Join(@"/", GetPathParts())));
                }
                return injectionPathUrl;
            }
        }

        /// <summary>
        /// Lists the injections in the sample set this URL names, as concrete injection URLs. Accepts a
        /// friendly path or path-form URL to a sample set, or an id form ("waters_connect:sampleSetId=...");
        /// see <see cref="FindAccount"/> for how each picks its account. Throws
        /// <see cref="RemoteServerException"/> if the account or sample set cannot be found, the path names
        /// something other than a sample set, or the server does not respond.
        /// </summary>
        public IList<WatersConnectUrl> ListSampleSetInjections()
        {
            var account = FindAccount(out var dataPathParts);
            using (var session = (WatersConnectSession) RemoteSession.CreateSession(account))
            {
                WatersConnectUrl sampleSetUrl;
                if (dataPathParts.Count == 0 && FolderOrSampleSetId != null)
                {
                    sampleSetUrl = FindSampleSetById(session, account, FolderOrSampleSetId);
                }
                else
                {
                    sampleSetUrl = FindItemByPath(session, account, dataPathParts);
                    if (sampleSetUrl?.Type != ItemType.sample_set)
                    {
                        throw new RemoteServerException(string.Format(
                            WatersConnectResources.WatersConnectUrl_ListSampleSetInjections_The_waters_connect_path___0___is_not_a_sample_set_,
                            string.Join(@"/", GetPathParts())));
                    }
                }
                return FetchContents(session, sampleSetUrl).Select(item => (WatersConnectUrl) item.MsDataFileUri).ToList();
            }
        }

        /// <summary>
        /// Resolves an acquisition method template given on the command line to the method's current
        /// published version, as choosing it in the Export Method dialog would. Accepts a friendly path
        /// ("waters_connect:&lt;account alias&gt;/Folder/MethodName"), an id form
        /// ("waters_connect:methodId=..."), or a method URL saved by Skyline, which is returned unchanged.
        /// The method id stays the same across a method's versions; only the server's search can find a
        /// method by it without its folder. Throws <see cref="RemoteServerException"/> if the account or a
        /// published version of the method cannot be found or the server does not respond.
        /// </summary>
        public static WatersConnectAcquisitionMethodUrl ResolveAcquisitionMethod(string value)
        {
            if (value == null || !value.StartsWith(UrlPrefix))
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveAcquisitionMethod_Name_a_waters_connect_acquisition_method_by_its_path_or_its_id_);

            string methodId = null;
            WatersConnectUrl url;
            if (IsFriendlyUrl(value))
            {
                url = ParseFriendly(value);
            }
            else
            {
                var methodUrl = new WatersConnectAcquisitionMethodUrl(value);
                if (methodUrl.MethodVersionId != Guid.Empty && methodUrl.ServerUrl != null)
                    return methodUrl;
                url = methodUrl;
                methodId = GetQueryValue(value, PARAM_METHOD_ID);
            }
            if (methodId == null && url.EncodedPath == null)
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveAcquisitionMethod_Name_a_waters_connect_acquisition_method_by_its_path_or_its_id_);

            var account = url.FindAccount(out var dataPathParts);
            using (var session = new WatersConnectSessionAcquisitionMethod(account))
            {
                var folders = session.GetFolderTree();
                WatersConnectFolderObject folder;
                Func<WatersConnectAcquisitionMethodObject, bool> isMethod;
                if (methodId != null)
                {
                    var folderId = session.FindMethodFolderId(methodId);
                    folder = folders.FirstOrDefault(f => WatersConnectObject.IdEquals(f.Id, folderId));
                    isMethod = m => WatersConnectObject.IdEquals(m.MethodId, methodId);
                }
                else
                {
                    var methodName = dataPathParts.LastOrDefault();
                    folder = FindFolderByPath(folders, dataPathParts.Take(dataPathParts.Count - 1));
                    isMethod = m => Equals(m.Name, methodName);
                }
                var method = folder == null ? null : session.GetPublishedMethods(folder.Id).FirstOrDefault(isMethod);
                if (method == null || !Guid.TryParse(method.MethodVersionId, out var methodVersionId))
                {
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_ResolveAcquisitionMethod_Could_not_find_a_published_waters_connect_acquisition_method___0__,
                        methodId ?? string.Join(@"/", url.GetPathParts())));
                }
                return new WatersConnectAcquisitionMethodUrl(GetFolderUrl(account, folder).ToString())
                    .ChangeMethodName(method.Name)
                    .ChangeAcquisitionMethodId(method.Id)
                    .ChangeMethodVersionId(methodVersionId);
            }
        }

        /// <summary>
        /// Resolves where a method exported on the command line goes, returning the URL of its folder and
        /// the method's name. Accepts a friendly path ("waters_connect:&lt;account alias&gt;/Folder/MethodName"),
        /// a path-form URL with a server, or an id form ("waters_connect:folderId=...&amp;methodName=...").
        /// Throws <see cref="RemoteServerException"/> if the account or folder cannot be found, the folder is
        /// not on the template's account or cannot be written to, it already has a method with that name, the
        /// account does not support method development, or the server does not respond.
        /// </summary>
        public static WatersConnectUrl ResolveMethodDestination(string value, WatersConnectAcquisitionMethodUrl templateUrl,
            out string methodName)
        {
            if (value == null || !value.StartsWith(UrlPrefix))
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_Name_the_waters_connect_method_by_its_folder_path_and_name__or_by_its_folder_id_and_name_);

            var url = IsFriendlyUrl(value) ? ParseFriendly(value) : new WatersConnectUrl(value);
            var account = url.FindAccount(out var dataPathParts);
            // A path names the folder and then the method; the id form names them separately
            var folderId = IsFriendlyUrl(value) ? null : GetQueryValue(value, PARAM_FOLDER_ID);
            var folderPathParts = dataPathParts.Take(Math.Max(0, dataPathParts.Count - 1)).ToList();
            methodName = (folderId != null ? GetQueryValue(value, PARAM_METHOD_NAME) : dataPathParts.LastOrDefault())?.Trim();
            if (string.IsNullOrEmpty(methodName) || folderId == null && folderPathParts.Count == 0)
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_Name_the_waters_connect_method_by_its_folder_path_and_name__or_by_its_folder_id_and_name_);
            // The method is created by the folder's account from the template, so both need the same account
            if (!templateUrl.BelongsToAccount(account))
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_The_template_and_the_exported_method_must_be_on_the_same_waters_connect_account_);

            WatersConnectFolderObject folder;
            IList<WatersConnectAcquisitionMethodObject> folderMethods = null;
            using (var session = new WatersConnectSessionAcquisitionMethod(account))
            {
                var folders = session.GetFolderTree();
                folder = folderId != null
                    ? folders.FirstOrDefault(f => WatersConnectObject.IdEquals(f.Id, folderId))
                    : FindFolderByPath(folders, folderPathParts);
                if (folder != null && folder.CanWrite)
                    folderMethods = session.GetPublishedMethods(folder.Id);
            }
            if (folder == null)
            {
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_Could_not_find_the_waters_connect_folder___0__,
                    folderId ?? string.Join(@"/", folderPathParts)));
            }
            if (!folder.CanWrite)
            {
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_You_do_not_have_permission_to_write_to_the_waters_connect_folder___0__,
                    folder.Path));
            }
            // Like the Export Method dialog, never replace an existing method
            var existingName = methodName;
            if (folderMethods.Any(m => Equals(m.Name, existingName)))
            {
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_A_waters_connect_method_named___0___already_exists_in_the_folder___1___,
                    methodName, folder.Path));
            }
            if (!account.SupportsMethodDevelopment(out var reason))
            {
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_ResolveMethodDestination_The_waters_connect_account_for__0__does_not_support_method_development___1_,
                    account.ServerUrl, reason ?? string.Empty));
            }
            return GetFolderUrl(account, folder);
        }

        /// <summary>
        /// Finds the saved account for a URL being resolved, and the path parts below the account. A URL
        /// with a server matches by server and username. Without a server, a friendly path names the
        /// account by its alias (falling back to its server URL) in the first path part, and an id form
        /// uses the only saved waters_connect account (with the URL's username, if it gives one).
        /// </summary>
        private WatersConnectAccount FindAccount(out IList<string> dataPathParts)
        {
            var pathParts = GetPathParts().ToList();
            if (ServerUrl != null)
            {
                // Path-form or id-form URL naming its server, and the username if more than one account
                // on that server has been saved
                dataPathParts = pathParts;
                var serverAccounts = GetWatersConnectAccounts()
                    .Where(account => account.CanHandleUrl(this) && (Username == null || Equals(account.Username, Username)))
                    .ToArray();
                if (serverAccounts.Length > 1)
                    throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_FindOnlyAccount_More_than_one_waters_connect_account_has_been_saved__Add_a_server_or_username_to_choose_one_);
                if (serverAccounts.Length == 1)
                    return serverAccounts[0];
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_OpenMsDataFile_Cannot_find_account_for_username__0__and_server__1__,
                    Username, ServerUrl));
            }

            if (pathParts.Count == 0)
            {
                dataPathParts = pathParts;
                return FindOnlyAccount(Username);
            }

            // Friendly form: the first path part is the account alias
            var alias = pathParts[0];
            dataPathParts = pathParts.Skip(1).ToList();
            var aliasAccount = FindAccountByAlias(alias);
            if (aliasAccount == null)
            {
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_ResolveInjection_No_waters_connect_account_was_found_with_the_alias_or_server___0__, alias));
            }
            return aliasAccount;
        }

        /// <summary>
        /// Returns the only saved waters_connect account, or the only one with the given username if
        /// there is one.
        /// </summary>
        private static WatersConnectAccount FindOnlyAccount(string username)
        {
            var accounts = GetWatersConnectAccounts()
                .Where(account => username == null || Equals(account.Username, username)).ToArray();
            if (accounts.Length == 0)
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_FindOnlyAccount_No_waters_connect_account_has_been_saved_);
            if (accounts.Length > 1)
                throw new RemoteServerException(WatersConnectResources.WatersConnectUrl_FindOnlyAccount_More_than_one_waters_connect_account_has_been_saved__Add_a_server_or_username_to_choose_one_);
            return accounts[0];
        }

        // Read only: query-string parameters a user can give on the command line to name a method
        // template or the folder and name of an exported method
        private const string PARAM_METHOD_ID = "methodId";
        private const string PARAM_FOLDER_ID = "folderId";
        private const string PARAM_METHOD_NAME = "methodName";

        private static string GetQueryValue(string value, string name)
        {
            return NameValueParameters.Parse(value.Substring(UrlPrefix.Length)).GetValue(name);
        }

        private static WatersConnectFolderObject FindFolderByPath(IEnumerable<WatersConnectFolderObject> folders, IEnumerable<string> pathParts)
        {
            var path = string.Join(@"/", pathParts);
            return folders.FirstOrDefault(f => Equals(f.Path, path));
        }

        private static WatersConnectUrl GetFolderUrl(WatersConnectAccount account, WatersConnectFolderObject folder)
        {
            return ((WatersConnectUrl) account.GetRootUrl().ChangePathPartsOnly(folder.Path.Split('/')))
                .ChangeFolderOrSampleSetId(folder.Id)
                .ChangeType(ItemType.folder_with_methods);
        }

        private static WatersConnectAccount[] GetWatersConnectAccounts()
        {
            return (RemoteAccountStorage?.GetRemoteAccounts() ?? Array.Empty<RemoteAccount>())
                .OfType<WatersConnectAccount>().ToArray();
        }

        /// <summary>
        /// Lists the parent of the item the path parts name and returns the child with the last name,
        /// or null if there is none. The returned URL carries the ids the server assigned.
        /// </summary>
        private static WatersConnectUrl FindItemByPath(RemoteSession session, WatersConnectAccount account, IList<string> dataPathParts)
        {
            var itemName = dataPathParts.LastOrDefault();
            if (itemName == null)
                return null;
            // The root URL has no path at all; an empty path would not be recognized as the root
            var parentUrl = dataPathParts.Count == 1
                ? account.GetRootUrl()
                : account.GetRootUrl().ChangePathParts(dataPathParts.Take(dataPathParts.Count - 1));
            var item = FetchContents(session, parentUrl).FirstOrDefault(i => Equals(i.Label, itemName));
            return (WatersConnectUrl) item?.MsDataFileUri;
        }

        /// <summary>
        /// Returns the URL of the sample set with the given id, with its full folder path, as listing its
        /// folder would produce it. The sample set's properties name its folder, and the folder tree gives
        /// that folder's path.
        /// </summary>
        private static WatersConnectUrl FindSampleSetById(WatersConnectSession session, WatersConnectAccount account, string sampleSetId)
        {
            var sampleSet = session.GetSampleSet(sampleSetId);
            if (sampleSet != null)
            {
                var folder = session.GetFolderTree().FirstOrDefault(f => WatersConnectObject.IdEquals(f.Id, sampleSet.ParentId));
                if (folder != null)
                {
                    var sampleSetUrl = FetchContents(session, folder.ToUrl((WatersConnectUrl) account.GetRootUrl()))
                        .Select(item => (WatersConnectUrl) item.MsDataFileUri)
                        .FirstOrDefault(url => url.Type == ItemType.sample_set &&
                                               WatersConnectObject.IdEquals(url.FolderOrSampleSetId, sampleSetId));
                    if (sampleSetUrl != null)
                        return sampleSetUrl;
                }
            }
            throw new RemoteServerException(string.Format(
                WatersConnectResources.WatersConnectUrl_FindSampleSetById_Could_not_find_the_waters_connect_sample_set_with_the_id___0__,
                sampleSetId));
        }

        private static WatersConnectAccount FindAccountByAlias(string alias)
        {
            var accounts = GetWatersConnectAccounts();
            var matches = accounts.Where(account => Equals(account.AccountAlias, alias)).ToArray();
            if (matches.Length == 0)
                matches = accounts.Where(account => Equals(account.ServerUrl, alias)).ToArray();
            if (matches.Length > 1)
                throw new RemoteServerException(string.Format(
                    WatersConnectResources.WatersConnectUrl_FindAccountByAlias_Multiple_waters_connect_accounts_match_the_alias_or_server___0___Use_a_unique_account_alias_, alias));
            return matches.FirstOrDefault();
        }

        private const int REMOTE_FETCH_MAX_ATTEMPTS = 60;
        private const int REMOTE_FETCH_WAIT_MS = 1000;

        /// <summary>
        /// Synchronously drives the asynchronous fetch for a remote URL until its contents are available,
        /// then returns the listed child items. Throws if the server reports an error or does not finish
        /// responding within the timeout.
        /// </summary>
        private static IList<RemoteItem> FetchContents(RemoteSession session, RemoteUrl remoteUrl)
        {
            var signal = new object();
            void OnContentsAvailable()
            {
                lock (signal) Monitor.Pulse(signal);
            }

            session.ContentsAvailable += OnContentsAvailable;
            try
            {
                RemoteServerException exception = null;
                bool completed = false;
                lock (signal)
                {
                    // AsyncFetchContents returns true once the contents are available, or false while a
                    // background fetch is still in progress; each completed stage pulses ContentsAvailable.
                    for (int i = 0; i < REMOTE_FETCH_MAX_ATTEMPTS; i++)
                    {
                        if (session.AsyncFetchContents(remoteUrl, out exception))
                        {
                            completed = true;
                            break;
                        }
                        if (exception != null)
                            break;
                        Monitor.Wait(signal, REMOTE_FETCH_WAIT_MS);
                    }
                }

                if (exception != null)
                    throw exception;
                if (!completed)
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_FetchContents_Timed_out_waiting_for_the_remote_server_to_list___0__,
                        remoteUrl.GetFilePath()));

                return session.ListContents(remoteUrl).ToList();
            }
            finally
            {
                session.ContentsAvailable -= OnContentsAvailable;
            }
        }
    }
}
