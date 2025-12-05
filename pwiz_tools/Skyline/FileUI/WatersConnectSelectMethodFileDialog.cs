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

using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.CommonMsData.RemoteApi;
using pwiz.CommonMsData.RemoteApi.WatersConnect;
using pwiz.Skyline.Alerts;


namespace pwiz.Skyline.FileUI
{

    public sealed class WatersConnectSelectMethodFileDialog : WatersConnectMethodFileDialog
    {
        /// <summary>
        /// File picker for WatersConnect method files
        /// </summary>
        /// <param name="remoteAccounts">Waters Connect accounts</param>
        public WatersConnectSelectMethodFileDialog(IList<RemoteAccount> remoteAccounts)
            : base( remoteAccounts )
        {
            Text = string.Format(FileUIResources.OpenFileDialogNEWatersConnectMethod_Select_Template, InstrumentType);
            actionButton.Text = FileUIResources.WatersConnectSelectMethodFileDialog_SelectButtonText;
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
            MessageDlg.Show(this, string.Format(FileUIResources.WatersConnectSelectMethodFileDialog_ItemSelected_Method__0__does_not_exist, methodName));
            return false;
        }
    }


}
