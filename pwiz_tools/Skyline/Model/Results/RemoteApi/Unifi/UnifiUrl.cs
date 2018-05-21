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
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiUrl : RemoteUrl
    {
        public static readonly UnifiUrl Empty = new UnifiUrl(UrlPrefix);
        public static string UrlPrefix { get { return RemoteAccountType.UNIFI.Name + ":"; } } // Not L10N

        public UnifiUrl(string unifiUrl) : base(unifiUrl)
        {
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            Id = nameValueParameters.GetValue("id"); // Not L10N
        }

        public string Id { get; private set; }

        public UnifiUrl ChangeId(string id)
        {
            return ChangeProp(ImClone(this), im => im.Id = id);
        }
        
        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return true;
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.UNIFI; }
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetValue("id", Id); // Not L10N
            return result;
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel)
        {
            var account = Settings.Default.RemoteAccountList.FirstOrDefault(acct => acct.CanHandleUrl(this)) as UnifiAccount;
            if (account == null)
            {
                throw new RemoteServerException(string.Format(Resources.UnifiUrl_OpenMsDataFile_Cannot_find_account_for_username__0__and_server__1__, 
                    Username, ServerUrl));
            }
            // ReSharper disable NonLocalizedString
            string serverUrl = ServerUrl.Replace("://", "://" + account.Username + ":" + account.Password + "@");
            serverUrl += "/unifi/v1/sampleresults(" + Id + ")?";
            serverUrl += "identity=" + Uri.EscapeDataString(account.IdentityServer) + "&scope=" +
                         Uri.EscapeDataString(account.ClientScope) + "&secret=" +
                         Uri.EscapeDataString(account.ClientSecret);
            // ReSharper restore NonLocalizedString
            return new MsDataFileImpl(serverUrl, 0, LockMassParameters, simAsSpectra,
                requireVendorCentroidedMS1: CentroidMs1, requireVendorCentroidedMS2: CentroidMs2,
                ignoreZeroIntensityPoints: true, preferOnlyMsLevel: preferOnlyMsLevel);
        }
    }
}
