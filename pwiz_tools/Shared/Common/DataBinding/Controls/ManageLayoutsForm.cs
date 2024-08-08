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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class ManageLayoutsForm : CommonFormEx
    {
        private ViewLayoutList _viewLayoutList;
        private bool _inUpdateUi;

        private enum LayoutType
        {
            plain,
            filter,
            pivot,
            filterAndPivot,
        }

        public ManageLayoutsForm()
        {
            InitializeComponent();
            imageList1.Images.Add(Resources.PlainLayout);
            imageList1.Images.Add(Resources.Filter);
            imageList1.Images.Add(Resources.Pivot);
            imageList1.Images.Add(Resources.PivotAndFilter);
        }

        public ViewLayoutList ViewLayoutList
        {
            get { return _viewLayoutList; }
            set
            {
                if (Equals(ViewLayoutList, value))
                {
                    return;
                }
                _viewLayoutList = value;
                UpdateUi();
            }
        }

        public void UpdateUi()
        {
            if (_inUpdateUi)
            {
                return;
            }
            try
            {
                _inUpdateUi = true;
                ListViewHelper.ReplaceItems(listViewLayouts, ViewLayoutList.Layouts.Select(MakeListViewItem).ToArray());
                Text = string.Format(Resources.ManageLayoutsForm_UpdateUi_Manage_layouts_for__0_, ViewLayoutList.ViewName);
                UpdateButtons();
            }
            finally
            {
                _inUpdateUi = false;
            }
        }

        private ListViewItem MakeListViewItem(ViewLayout viewLayout)
        {
            var listViewItem = new ListViewItem(viewLayout.Name);
            bool hasPivot = viewLayout.RowTransforms.OfType<PivotSpec>().Any();
            bool hasFilter = viewLayout.RowTransforms.OfType<RowFilter>().Any();
            if (hasPivot)
            {
                if (hasFilter)
                {
                    listViewItem.ImageIndex = (int) LayoutType.filterAndPivot;
                }
                else
                {
                    listViewItem.ImageIndex = (int) LayoutType.pivot;
                }
            }
            else
            {
                if (hasFilter)
                {
                    listViewItem.ImageIndex = (int) LayoutType.filter;
                }
                else
                {
                    listViewItem.ImageIndex = (int) LayoutType.plain;
                }
            }
            if (IsDefault(viewLayout))
            {
                listViewItem.Font = new Font(listViewItem.Font, FontStyle.Bold);
            }
            return listViewItem;
        }

        private void listViewLayouts_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (_inUpdateUi)
            {
                return;
            }
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            btnDelete.Enabled = listViewLayouts.SelectedIndices.Count != 0;
            if (listViewLayouts.SelectedIndices.Count == 1)
            {
                btnMakeDefault.Enabled = true;
                if (IsDefault(_viewLayoutList.Layouts[listViewLayouts.SelectedIndices[0]]))
                {
                    btnMakeDefault.Text = Resources.ManageLayoutsForm_UpdateButtons_Clear_Default;
                }
                else
                {
                    btnMakeDefault.Text = Resources.ManageLayoutsForm_UpdateButtons_Set_Default;
                }
            }
            else
            {
                btnMakeDefault.Enabled = false;
            }
        }

        private bool IsDefault(ViewLayout viewLayout)
        {
            return viewLayout.Name == ViewLayoutList.DefaultLayoutName;
        }

        private void btnMakeDefault_Click(object sender, System.EventArgs e)
        {
            ToggleDefault();
        }

        public void ToggleDefault()
        {
            if (listViewLayouts.SelectedIndices.Count != 1)
            {
                return;
            }
            var viewLayout = ViewLayoutList.Layouts[listViewLayouts.SelectedIndices[0]];
            ViewLayoutList = ViewLayoutList.ChangeDefaultLayoutName(IsDefault(viewLayout) ? null : viewLayout.Name);
        }

        private void btnDelete_Click(object sender, System.EventArgs e)
        {
            var oldLayouts = ViewLayoutList.Layouts;
            var newLayouts = Enumerable.Range(0, oldLayouts.Count)
                .Except(listViewLayouts.SelectedIndices.Cast<int>())
                .Select(index => oldLayouts[index]);
            ViewLayoutList = ViewLayoutList.ChangeLayouts(newLayouts);
        }

        public ListView ListView { get { return listViewLayouts; } }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }
    }
}
