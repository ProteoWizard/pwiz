/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.RemoteApi.Ardia;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.FileUI
{
    public class PublishDocumentDlgArdia : PublishDocumentDlgBase
    {
        private IList<ArdiaAccount> _ardiaAccounts;

        public PublishDocumentDlgArdia(
            IDocumentUIContainer docContainer,
            IList<ArdiaAccount> accounts,
            string fileName,
            DocumentFormat? fileFormatOnDisk) : base(docContainer, fileName, fileFormatOnDisk)
        {
            _ardiaAccounts = accounts;
        }

        internal override void HandleDialogLoad()
        {
            cbAnonymousServers.Visible = false;
            createRemoteFolder.Visible = true;
            createRemoteFolder.Click += CreateRemoteFolder_Click;
        }

        private void CreateRemoteFolder_Click(object sender, EventArgs eventArgs)
        {
        }
    }
}
