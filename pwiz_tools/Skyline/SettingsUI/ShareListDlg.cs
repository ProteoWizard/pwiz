/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.GUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public class ShareListDlg<TItem> : FormEx where TItem : IKeyContainer<string>
    {
        private readonly CheckedListBox _checkedListBox;

        public ShareListDlg(IEnumerable<TItem> items, string title)
        {
            Text = title;
            Icon = Resources.Skyline;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 350;
            Height = 350;

            var label = new Label
            {
                Left = 12, Top = 10, AutoSize = true,
                Text = SettingsUIResources.ShareListDlg_Select_items_to_share
            };

            _checkedListBox = new CheckedListBox
            {
                Left = 12, Top = 30, Width = 310, Height = 230,
                CheckOnClick = true
            };

            foreach (var item in items)
                _checkedListBox.Items.Add(item.GetKey(), true);

            var btnOk = new Button
            {
                Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.OK), Width = 75, Top = 270, Left = 160,
                DialogResult = DialogResult.OK
            };
            var btnCancel = new Button
            {
                Text = CommonAlertDlg.GetDefaultButtonText(DialogResult.Cancel), Width = 75, Top = 270, Left = 245,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { label, _checkedListBox, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public IList<string> CheckedItemNames =>
            _checkedListBox.CheckedItems.Cast<string>().ToList();

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }
    }
}
