/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    [XmlRoot("chorus_account")]
    public sealed class ChorusAccount : RemoteAccount
    {
        public const string DEFAULT_SERVER = "https://chorusproject.org"; // Not L10N
        public static readonly ChorusAccount BLANK = (ChorusAccount)new ChorusAccount()
            .ChangeServerUrl(DEFAULT_SERVER); // Not L10N
        public ChorusAccount(string serverUrl, string username, string password)
        {
            ServerUrl = serverUrl;
            Username = username;
            Password = password;
        }

        private ChorusAccount(ChorusAccount chorusAccount)
        {
            ServerUrl = chorusAccount.ServerUrl;
            Username = chorusAccount.Username;
            Password = chorusAccount.Password;
        }

        public ChorusUrl GetChorusUrl()
        {
            return (ChorusUrl) ChorusUrl.Empty.ChangeServerUrl(ServerUrl).ChangeUsername(Username);
        }

        public ChorusAccount Clone()
        {
            return new ChorusAccount(this);
        }


        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private ChorusAccount()
        {
        }

        public static ChorusAccount Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChorusAccount());
        }
        #endregion

        public override RemoteSession CreateSession()
        {
            return new ChorusSession(this);
        }

        public override RemoteUrl GetRootUrl()
        {
            return GetChorusUrl();
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.CHORUS; }
        }
    }
}
