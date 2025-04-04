﻿/*
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
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public abstract class RemoteAccountType
    {
        public static readonly RemoteAccountType UNIFI = new Unifi();
        public static readonly RemoteAccountType ARDIA = new Ardia();
        public static readonly ImmutableList<RemoteAccountType> ALL = ImmutableList.ValueOf(new[] {UNIFI, ARDIA});

        public abstract string Name { get;  }
        public abstract string Label { get; }
        public abstract RemoteUrl GetEmptyUrl();
        public abstract RemoteAccount GetEmptyAccount();
        public override string ToString()
        {
            return Label;
        }

        private class Unifi : RemoteAccountType
        {
            public override RemoteUrl GetEmptyUrl()
            {
                return UnifiUrl.Empty;
            }

            public override string Label
            {
                get { return RemoteApiResources.Unifi_Label_Unifi; }
            }

            public override string Name
            {
                get { return @"unifi"; }
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return new UnifiAccount(@"https://democonnect.waters.com:48505", null, null);
            }
        }

        private class Ardia : RemoteAccountType
        {
            public override RemoteUrl GetEmptyUrl()
            {
                return ArdiaUrl.Empty;
            }

            public override string Label
            {
                get { return RemoteApiResources.Ardia_Label_Ardia; }
            }

            public override string Name
            {
                get { return @"ardia"; }
            }

            public override RemoteAccount GetEmptyAccount()
            {
                return new ArdiaAccount(string.Empty, null, null);
            }
        }
    }
}
