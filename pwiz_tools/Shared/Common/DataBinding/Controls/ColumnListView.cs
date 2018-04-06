/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Controls.Editor;

namespace pwiz.Common.DataBinding.Controls
{
    internal class ColumnListView : ListView
    {
        public ColumnListView()
        {
            HeaderStyle = ColumnHeaderStyle.None;
            View = View.Details;
            Columns.Add(new ColumnHeader());
            ShowItemToolTips = true;
            HideSelection = false;
        }

        public void ReplaceItems(IEnumerable<ListViewItem> items)
        {
            ListViewHelper.ReplaceItems(this, items.ToArray());
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            AfterResize();
        }

        public void AfterResize()
        {
            Columns[0].Width = ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 1;
        }
    }
}
