/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.WatersConnect;


namespace pwiz.Skyline.FileUI
{
    public class OpenFileDialogNEWatersConnectMethod : OpenFileDialogNE
    {
        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        public OpenFileDialogNEWatersConnectMethod(IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null
            // , IList<RemoteAccountType> remoteAccountTypeCreationRestriction = null   TODO ZZZ  NOT USED
            )
            : base( null /* SOURCE_TYPES */, remoteAccounts, specificDataSourceFilter )
        {
        }

        public MsDataFileUri DataSource => FileName;
        public MsDataFileUri[] DataSources => FileNames;

        // private IList<RemoteAccountType> remoteAccountTypeCreationRestriction;   TODO ZZZ  NOT USED

        protected override void CreateNewRemoteSession(RemoteAccount remoteAccount)
        {
            if (remoteAccount is WatersConnectAccount wcAccount)
            {
                RemoteSession = new WatersConnectSessionAcquisitionMethod(wcAccount);
                return;
            }

            throw new Exception("remoteAccount is NOT WatersConnectAccount");
        }

    }


}
