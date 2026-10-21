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
                // Path-form or id-form URL naming its server
                dataPathParts = pathParts;
                if (FindMatchingAccount() is WatersConnectAccount matchingAccount)
                    return matchingAccount;
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
                var rootUrl = (WatersConnectUrl) account.GetRootUrl();
                FetchContents(session, rootUrl);    // Loads the folder tree
                if (session.TryGetFolderById(sampleSet.ParentId, out var folder))
                {
                    var sampleSetUrl = FetchContents(session, folder.ToUrl(rootUrl))
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
