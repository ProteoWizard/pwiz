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
using System.IO;
using System.Web.Configuration;
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
            folder_child_folders_sample_sets, // Child Folders and Sample Sets in the given folder
            folder_child_folders_only,  // Child Folders only
            sample_set, // a collection of related injections
            injection,   // like a .raw file
            folder_child_folders_acquisition_methods,  // Child Folders and Acquisition Methods in the given folder
            acquisition_method // Single Acquisition Method
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            InjectionId = nameValueParameters.GetValue(@"injectionId");
            FolderOrSampleSetId = nameValueParameters.GetValue(@"sampleSetId");
            Type = (ItemType?) nameValueParameters.GetLongValue(@"type") ?? ItemType.folder_child_folders_sample_sets;
        }
        public string InjectionId { get; private set; }
        public string FolderOrSampleSetId { get; private set; }
        public ItemType Type { get; protected set; }

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

            //  TODO 'serverUrl' currently excludes 'AcquisitionMethodId'

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

    public class WatersConnectAcquisitionMethodUrl : WatersConnectUrl
    {
        public static readonly WatersConnectAcquisitionMethodUrl Empty =
            new WatersConnectAcquisitionMethodUrl(UrlPrefix);

        public new static string UrlPrefix
        {
            get { return RemoteAccountType.WATERS_CONNECT.Name + @":acquisition_method:"; }
        }

        public Guid MethodVersionId { get; private set; }
        public string AcquisitionMethodId { get; private set; }

        public WatersConnectAcquisitionMethodUrl(string watersConnectUrl) : base(watersConnectUrl)
        {
        }

        public WatersConnectAcquisitionMethodUrl ChangeMethodVersionId(Guid id)
        {
            return ChangeProp(ImClone(this), im => im.MethodVersionId = id);
        }
        public WatersConnectAcquisitionMethodUrl ChangeAcquisitionMethodId(string id)
        {
            return ChangeProp(ImClone(this), im => im.AcquisitionMethodId = id);
        }


        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            if (MethodVersionId != Guid.Empty)
                result.SetValue(@"methodVersionId", MethodVersionId.ToString());
            if (AcquisitionMethodId != null)
                result.SetValue(@"acquisitionMethodId", AcquisitionMethodId);
            return result;
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            var methodVersionId = nameValueParameters.GetValue(@"methodVersionId");
            if (!string.IsNullOrEmpty(methodVersionId))
            {
                Guid id;
                if (Guid.TryParse(methodVersionId, out id))
                {
                    MethodVersionId = id;
                }
                else
                {
                    throw new InvalidDataException(string.Format("Invalid method version Id", methodVersionId));
                }
            }
            AcquisitionMethodId = nameValueParameters.GetValue(@"acquisitionMethodId");
            Type = ItemType.acquisition_method;
        }
    }
}
