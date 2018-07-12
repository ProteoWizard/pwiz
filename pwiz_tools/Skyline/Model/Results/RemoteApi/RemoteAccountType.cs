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
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteAccountType
    {
        public static readonly RemoteAccountType CHORUS = new Chorus();
        public static readonly RemoteAccountType UNIFI = new Unifi();
        public static readonly ImmutableList<RemoteAccountType> ALL = ImmutableList.ValueOf(new[] {UNIFI, CHORUS});

        public abstract string Name { get;  }
        public abstract string Label { get; }
        public abstract RemoteUrl GetEmptyUrl();
        public abstract RemoteAccount GetEmptyAccount();
        public override string ToString()
        {
            return Label;
        }

        private class Chorus : RemoteAccountType
        {
            public override string Label
            {
                get { return Resources.Chorus_Label_Chorus; }
            }

            public override string Name
            {
                get { return "chorus"; } // Not L10N
            }

            public override RemoteUrl GetEmptyUrl()
            {
                return ChorusUrl.Empty;
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return ChorusAccount.BLANK;
            }
        }

        private class Unifi : RemoteAccountType
        {
            public override RemoteUrl GetEmptyUrl()
            {
                return UnifiUrl.Empty;
            }

            public override string Label
            {
                get { return Resources.Unifi_Label_Unifi; }
            }

            public override string Name
            {
                get { return "unifi"; } // Not L10N
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return new UnifiAccount("https://unifiapi.waters.com:50034", null, null); // Not L10N
            }
        }
    }
}
