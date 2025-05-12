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
using pwiz.Skyline.Alerts;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NHibernate.Util;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Results.RemoteApi.WatersConnect;
using pwiz.Skyline.Util;


namespace pwiz.Skyline.FileUI
{

    // TODO: [RC] Make sure the path combo dropdown is populated with the correct path
    public class OpenFileDialogNEWatersConnectMethod : OpenFileDialogNE
    {
        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        public OpenFileDialogNEWatersConnectMethod(IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null, RemoteUrl templateUrl = null)
            : base( null /* SOURCE_TYPES */, remoteAccounts, specificDataSourceFilter )
        {
            listView.MultiSelect = false;
            Text = "Select Template";

            if (templateUrl is WatersConnectAcquisitionMethodUrl mehodUrl)
            {   // if the template is already set, use its path as the initial directory
                InitialDirectory = mehodUrl.ChangeType(WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods)
                    .ChangePathParts(UrlPath.GetFilePathParts(templateUrl.EncodedPath)); ;
            }
            else
            {
                var acctList = remoteAccounts.OfType<WatersConnectAccount>().ToList();
                if (acctList.Count() == 1)  // if there is only one account, set the initial directory to its root path
                    InitialDirectory = (acctList.First().GetRootUrl() as WatersConnectUrl)?.ChangeType(WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods);
                else
                    InitialDirectory = RemoteUrl.EMPTY;
            }
        }

        public WatersConnectAcquisitionMethodUrl MethodUrl { get; private set; }

        protected override void CreateNewRemoteSession(RemoteAccount remoteAccount)
        {
            if (remoteAccount is WatersConnectAccount wcAccount)
            {
                RemoteSession = new WatersConnectSessionAcquisitionMethod(wcAccount);
                return;
            }

            throw new Exception("remoteAccount is NOT WatersConnectAccount");
        }

        protected override void DoMainAction()
        {
            Open();
        }

        protected override void SelectItem()
        {
            Open();
        }

        protected override RemoteUrl GetRootUrl()
        {   // We need to make sure the root URL has the correct type for method retrieval
            return (base.GetRootUrl() as WatersConnectUrl)?.ChangeType(WatersConnectUrl.ItemType.folder_child_folders_acquisition_methods);
        }

        public void Open()
        {
            if (listView.SelectedItems.Count == 0)
                return;

            var item = listView.SelectedItems[0];

            if (DataSourceUtil.IsFolderType(item.SubItems[1].Text))
            {
                OpenFolderItem(item);
                return;
            }

            if (DataSourceUtil.TYPE_WATERS_ACQUISITION_METHOD.Equals(item.SubItems[1].Text))
            {
                if (listView.SelectedItems[0].Tag is SourceInfo sourceInfo)
                {
                    MethodUrl = sourceInfo.MsDataFileUri as WatersConnectAcquisitionMethodUrl;
                }
                DialogResult = DialogResult.OK;
                return;
            }

            try
            {
                // perhaps the user has typed an entire filename into the text box - or just garbage
                OpenFolderFromTextBox();
                // TODO: Make sure it can open the file from the text box remotely
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { } // guard against user typed-in-garbage
        }
    }


}
