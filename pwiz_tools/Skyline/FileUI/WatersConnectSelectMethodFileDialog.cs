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
using System.Linq;
using System.Windows.Forms;
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Alerts;


namespace pwiz.Skyline.FileUI
{

    public sealed class WatersConnectSelectMethodFileDialog : WatersConnectMethodFileDialog
    {
        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        public WatersConnectSelectMethodFileDialog(IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null)
            : base( remoteAccounts, specificDataSourceFilter )
        {
            Text = string.Format(FileUIResources.OpenFileDialogNEWatersConnectMethod_Select_Template, InstrumentType);
        }

        public WatersConnectAcquisitionMethodUrl MethodUrl { get; private set; }

        protected override bool ItemSelected(ListViewItem item)
        {
            if (item.Tag is SourceInfo sourceInfo)
            {
                MethodUrl = sourceInfo.MsDataFileUri as WatersConnectAcquisitionMethodUrl;
                if (MethodUrl != null)
                {
                    return true;
                }
            }
            return base.ItemSelected(item);     // Should not happen, but just in case
        }

        protected override bool ItemSelected(string methodName)
        {
            MessageDlg.Show(this, string.Format("Method {0} does not exist. Please, select an existing method.", methodName));
            return false;
        }
    }


}
