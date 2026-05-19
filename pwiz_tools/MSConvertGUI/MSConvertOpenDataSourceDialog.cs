/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 Matt Chambers
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
using System.Linq;
using System.Windows.Forms;
using pwiz.CommonFileDialogs;
using pwiz.CommonMsData;
using pwiz.CommonMsData.RemoteApi;

namespace MSConvertGUI
{
    /// <summary>
    /// MSConvertGUI's file open dialog, using the shared BaseFileDialogNE infrastructure.
    /// Supports both local files and remote accounts (UNIFI, Waters Connect, Ardia).
    /// </summary>
    public class MSConvertOpenDataSourceDialog : OpenFileDialogNE
    {
        private static string[] GetSourceTypes()
        {
            var types = new List<string> { CommonFileDialogResources.OpenDataSourceDialog_OpenDataSourceDialog_Any_spectra_format };
            foreach (var typeExtsPair in pwiz.CLI.msdata.ReaderList.FullReaderList.getFileExtensionsByType())
            {
                if (typeExtsPair.Value.Count > 0) // exclude types with no file extensions (e.g. UNIFI)
                    types.Add(typeExtsPair.Key);
            }
            types.Sort(1, types.Count - 1, null); // sort all except "Any spectra format"
            return types.ToArray();
        }

        public MSConvertOpenDataSourceDialog()
            : base(GetSourceTypes(), MSConvertRemoteAccountServices.INSTANCE.GetRemoteAccountList())
        {
        }

        protected override IList<RemoteAccount> OnEditAccounts(IWin32Window owner)
        {
            using (var dlg = new RemoteAccountEditForm(MSConvertRemoteAccountServices.INSTANCE.GetRemoteAccountList()))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK)
                {
                    MSConvertRemoteAccountServices.INSTANCE.SetAccounts(dlg.Accounts);
                    return dlg.Accounts;
                }
            }
            return null;
        }

        protected override RemoteAccount OnCreateNewAccount(IWin32Window owner)
        {
            using (var dlg = new RemoteAccountDetailForm(null))
            {
                if (dlg.ShowDialog(owner) == DialogResult.OK && dlg.Account != null)
                {
                    MSConvertRemoteAccountServices.INSTANCE.AddAccount(dlg.Account);
                    return dlg.Account;
                }
            }
            return null;
        }
    }
}
