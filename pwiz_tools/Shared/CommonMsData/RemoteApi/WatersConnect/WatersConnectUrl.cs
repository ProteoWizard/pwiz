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

        public bool SupportsMethodDevelopment
        {
            get => FindMatchingAccount() is WatersConnectAccount wca && wca.SupportsMethodDevelopment;
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
    }
}
