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
using pwiz.Skyline.Util;

// This code is associated with the DocumentGrid.

namespace pwiz.Skyline.Controls.Databinding
{

    // TODO nicksh will add a means of having this update in response to the View|Targets|By * menu

    public class DataboundGridForm : DockableFormEx
    {
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        #region Methods exposed for testing
        public BindingListSource BindingListSource { get; protected set; }
        public BoundDataGridViewEx DataGridView { get; protected set; }
        public NavBar NavBar { get; protected set; }

        public DataGridViewColumn FindColumn(PropertyPath propertyPath)
        {
            // Expression split to two lines for debugging.
            var columnPropertyDescriptors = BindingListSource.GetItemProperties(null).OfType<ColumnPropertyDescriptor>();
            var propertyDescriptor = columnPropertyDescriptors.FirstOrDefault(colPd =>
                Equals(propertyPath, colPd.PropertyPath));
            if (null == propertyDescriptor)
            {
                return null;
            }
            return DataGridView.Columns.Cast<DataGridViewColumn>().FirstOrDefault(col => col.DataPropertyName == propertyDescriptor.Name);
        }
        public bool IsComplete
        {
            get
            {
                return BindingListSource.IsComplete;
            }
        }

        public void ChooseView(string viewName)
        {
            var viewSpecs = BindingListSource.ViewContext.BuiltInViews.Concat(BindingListSource.ViewContext.CustomViews);
            var viewSpec = viewSpecs.First(view => view.Name == viewName);
            BindingListSource.SetViewSpec(viewSpec);
        }

        public int RowCount
        {
            get { return DataGridView.RowCount; }
        }

        public int ColumnCount
        {
            get { return DataGridView.ColumnCount; }
        }

        public string[] ColumnHeaderNames
        {
            get
            {
                return DataGridView.Columns.Cast<DataGridViewColumn>().Select(col => col.HeaderText).ToArray();
            }
        }

        public void ManageViews()
        {
            BindingListSource.ViewContext.ManageViews(NavBar);
        }

        #endregion

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DataboundGridForm));
            SuspendLayout();
            // 
            // DataboundGridForm
            // 
            resources.ApplyResources(this, "$this"); // Not L10N
            Name = "DataboundGridForm"; // Not L10N
            ShowInTaskbar = false;
            ResumeLayout(false);

        }
    }
}
