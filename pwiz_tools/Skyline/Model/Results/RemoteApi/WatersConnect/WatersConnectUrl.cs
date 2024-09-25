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
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.WatersConnect
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
            injection   // like a .raw file
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            InjectionId = nameValueParameters.GetValue(@"injectionId");
            FolderOrSampleSetId = nameValueParameters.GetValue(@"sampleSetId");
            Type = (ItemType?) nameValueParameters.GetLongValue(@"type") ?? ItemType.folder;
        }
        public string InjectionId { get; private set; }
        public string FolderOrSampleSetId { get; private set; }
        public ItemType Type { get; private set; }

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
        
        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return false;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.WATERS_CONNECT; }
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetValue(@"injectionId", InjectionId);
            result.SetValue(@"sampleSetId", FolderOrSampleSetId);
            result.SetLongValue(@"type", (long) Type);
            return result;
        }

        public override MsDataFileImpl OpenMsDataFile(OpenMsDataFileParams openParams)
        {
            var account = FindMatchingAccount(Settings.Default.RemoteAccountList) as WatersConnectAccount;
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
                         Uri.EscapeDataString(account.ClientSecret);
            // ReSharper restore LocalizableElement
            return new MsDataFileImpl(serverUrl, 0, LockMassParameters, openParams.SimAsSpectra,
                requireVendorCentroidedMS1: openParams.CentroidMs1, requireVendorCentroidedMS2: openParams.CentroidMs2,
                ignoreZeroIntensityPoints: openParams.IgnoreZeroIntensityPoints, preferOnlyMsLevel: openParams.PreferOnlyMs1 ? 1 : 0);
        }
    }
}
