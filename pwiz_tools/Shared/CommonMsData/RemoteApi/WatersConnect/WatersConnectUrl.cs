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
            injectionId
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            InjectionId = nameValueParameters.GetValue(Attr.injectionId.ToString());
            FolderOrSampleSetId = nameValueParameters.GetValue(Attr.id.ToString());
            Type = (ItemType?) nameValueParameters.GetLongValue(Attr.type.ToString()) ?? ItemType.folder;
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
        /// Resolves a friendly or path-form waters_connect URL (one without an injection id) to a concrete
        /// injection by matching the account and navigating the server. A friendly URL (no server) matches
        /// the account by its alias (the first path part), falling back to the server URL; a path-form URL
        /// (with a server) matches by server and username. Returns this URL unchanged if it is already
        /// resolved (has an injection id). Throws <see cref="RemoteServerException"/> if the account or
        /// injection cannot be found or the server does not respond.
        /// </summary>
        public WatersConnectUrl ResolveInjection()
        {
            if (InjectionId != null)
                return this;

            var pathParts = GetPathParts().ToList();
            WatersConnectAccount account;
            IList<string> dataPathParts;
            if (ServerUrl == null)
            {
                // Friendly form: the first path part is the account alias
                var alias = pathParts.FirstOrDefault();
                account = FindAccountByAlias(alias);
                if (account == null)
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_ResolveInjection_No_waters_connect_account_was_found_with_the_alias_or_server___0__, alias));
                dataPathParts = pathParts.Skip(1).ToList();
            }
            else
            {
                account = FindMatchingAccount() as WatersConnectAccount;
                if (account == null)
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_OpenMsDataFile_Cannot_find_account_for_username__0__and_server__1__,
                        Username, ServerUrl));
                dataPathParts = pathParts;
            }

            var injectionName = dataPathParts.LastOrDefault();
            var sampleSetUrl = (WatersConnectUrl) account.GetRootUrl()
                .ChangePathParts(dataPathParts.Take(Math.Max(0, dataPathParts.Count - 1)));
            using (var session = RemoteSession.CreateSession(account))
            {
                var injection = injectionName == null
                    ? null
                    : FetchContents(session, sampleSetUrl).FirstOrDefault(item => Equals(item.Label, injectionName));
                if (injection == null)
                    throw new RemoteServerException(string.Format(
                        WatersConnectResources.WatersConnectUrl_ResolveInjection_Could_not_find_the_injection___0___under_the_waters_connect_path___1__,
                        injectionName ?? string.Empty, string.Join(@"/", pathParts)));
                return (WatersConnectUrl) injection.MsDataFileUri;
            }
        }

        private static WatersConnectAccount FindAccountByAlias(string alias)
        {
            var accounts = (RemoteAccountStorage?.GetRemoteAccounts() ?? Array.Empty<RemoteAccount>())
                .OfType<WatersConnectAccount>().ToArray();
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
