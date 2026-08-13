/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2024 Vanderbilt University - Nashville, TN 37232
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
using System.Windows.Forms;
using pwiz.CommonFileDialogs;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Skyline-specific adapter for BaseFileDialogNE that provides Skyline's
    /// custom dialog implementations, settings, and main window references.
    /// </summary>
    public class SkylineFileDialogNE : BaseFileDialogNE
    {
        public SkylineFileDialogNE(string[] sourceTypes, IList<RemoteAccount> remoteAccounts,
            IList<string> specificDataSourceFilter = null, bool isRemote = false)
            : base(sourceTypes, remoteAccounts, specificDataSourceFilter, isRemote)
        {
        }

        protected override IWin32Window GetFallbackDialogOwner()
        {
            return Program.MainWindow;
        }

        protected override void ShowErrorMessage(IWin32Window owner, string message)
        {
            MessageDlg.ShowError(owner, message);
        }

        protected override void ShowErrorWithException(IWin32Window owner, string message, Exception exception)
        {
            MessageDlg.ShowWithException(owner, message, exception);
        }

        protected override DialogResult ShowRetryDialog(string message, string retryButtonText)
        {
            return MultiButtonMsgDlg.Show(this, message, retryButtonText);
        }

        protected override IList<RemoteAccount> OnEditAccounts(IWin32Window owner)
        {
            var list = Settings.Default.RemoteAccountList;
            var listNew = list.EditList((Control)owner, null);
            if (listNew != null)
            {
                list.Clear();
                list.AddRange(listNew);
                return Settings.Default.RemoteAccountList;
            }
            return null;
        }

        protected override RemoteAccount OnCreateNewAccount(IWin32Window owner)
        {
            var newAccount = Settings.Default.RemoteAccountList.NewItem((Control)owner, Settings.Default.RemoteAccountList, null);
            if (null != newAccount)
            {
                Settings.Default.RemoteAccountList.Add(newAccount);
            }
            return newAccount;
        }
    }
}
