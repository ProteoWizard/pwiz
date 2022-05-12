/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Util;

// This code is associated with the DocumentGrid.

namespace pwiz.Skyline.Controls.Databinding
{
    public partial class DataboundGridForm : DockableFormEx, IDataboundGridForm
    {
        public DataboundGridForm()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        public virtual DataGridId DataGridId
        {
            get
            {
                return null;
            }
        }

        public ViewName? GetViewName()
        {
            return DataboundGridControl?.GetViewName();
        }

        DataboundGridControl IDataboundGridForm.GetDataboundGridControl()
        {
            return DataboundGridControl;
        }

        protected static bool ColumnsEqual(ViewInfo viewInfo1, ViewInfo viewInfo2)
        {
            if (!Equals(viewInfo1.ViewSpec, viewInfo2.ViewSpec))
            {
                return false;
            }

            if (viewInfo1.DisplayColumns.Count != viewInfo2.DisplayColumns.Count)
            {
                return false;
            }

            for (int icol = 0; icol < viewInfo1.DisplayColumns.Count; icol++)
            {
                if (!viewInfo1.DisplayColumns[icol].ColumnDescriptor.GetAttributes()
                        .SequenceEqual(viewInfo2.DisplayColumns[icol].ColumnDescriptor.GetAttributes()))
                {
                    return false;
                }
            }

            return true;
        }



        #region Methods exposed for testing
        public BindingListSource BindingListSource { get { return databoundGridControl.BindingListSource; } }
        public BoundDataGridViewEx DataGridView { get { return databoundGridControl.DataGridView; } }
        public NavBar NavBar { get { return databoundGridControl.NavBar; } }

        public DataGridViewColumn FindColumn(string propertyPathText)
        {
            return FindColumn(PropertyPath.Parse(propertyPathText));
        }

        public DataGridViewColumn FindColumn(PropertyPath propertyPath)
        {
            return databoundGridControl.FindColumn(propertyPath);
        }

        public bool IsComplete
        {
            get
            {
                return databoundGridControl.IsComplete;
            }
        }

        public void ChooseView(string viewName)
        {
            databoundGridControl.ChooseView(viewName);
        }

        public int RowCount
        {
            get { return databoundGridControl.RowCount; }
        }

        public int ColumnCount
        {
            get { return databoundGridControl.ColumnCount; }
        }

        public string[] ColumnHeaderNames
        {
            get { return databoundGridControl.ColumnHeaderNames; }
        }

        public void ManageViews()
        {
            databoundGridControl.ManageViews();
        }

        public void QuickFilter(DataGridViewColumn column)
        {
            databoundGridControl.QuickFilter(column);
        }

        public DataboundGridControl DataboundGridControl { get { return databoundGridControl; } }

        #endregion
    }
}
