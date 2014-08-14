/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.DataBinding
{
    public partial class LiveReportForm : DockableFormEx
    {
        private IEnumerable _rowSource;
        private SkylineDataSchema _dataSchema;
        public LiveReportForm(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _dataSchema = new SkylineDataSchema(documentContainer);
            var parentColumn = new ColumnDescriptor(_dataSchema, typeof (Protein));
            var viewContext = new SkylineViewContext(parentColumn);
            bindingListSource.SetViewContext(viewContext);
            bindingListSource.RowSource = new Proteins(_dataSchema);
            navBar.BindingNavigator.Items.Insert(0, toolStripDropDownRowSource);
        }

        private void ProteinsToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            if (RowSource is Proteins)
            {
                return;
            }
            RowSource = new Proteins(_dataSchema);
        }

        private void PeptidesToolStripMenuItemOnClick(object sender, EventArgs e)
        {
            if (RowSource is Peptides)
            {
                return;
            }
            RowSource = new Peptides(_dataSchema, new[]{IdentityPath.ROOT});
        }

        private void precursorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RowSource is Precursors)
            {
                return;
            }
            RowSource = new Precursors(_dataSchema, new[]{IdentityPath.ROOT});
        }

        private void transitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RowSource is Transitions)
            {
                return;
            }
            RowSource = new Transitions(_dataSchema, new[]{IdentityPath.ROOT});
        }

        public IEnumerable RowSource
        {
            get { return _rowSource; }
            set
            {
                if (ReferenceEquals(_rowSource, value))
                {
                    return;
                }
                _rowSource = value;
                proteinsToolStripMenuItem.Checked = RowSource is Proteins;
                peptidesToolStripMenuItem.Checked = RowSource is Peptides;
                precursorsToolStripMenuItem.Checked = RowSource is Precursors;
                transitionsToolStripMenuItem.Checked = RowSource is Transitions;
                var listItemType = GetListItemType(RowSource.GetType());
                if (bindingListSource.ViewContext == null ||
                    bindingListSource.ViewContext.ParentColumn.PropertyType != listItemType)
                {
                    boundDataGridView.Columns.Clear();
                    var viewContext = new SkylineViewContext(new ColumnDescriptor(_dataSchema, listItemType));
                    bindingListSource.RowSource = RowSource;
                    bindingListSource.SetViewContext(viewContext, new ViewInfo(viewContext.ParentColumn, viewContext.BuiltInViewSpecs.First()));
                }
            }
        }

        private static Type GetListItemType(Type listType)
        {
            var listInterface =
                listType.FindInterfaces((type, args) => type.IsGenericType && typeof (IList<>) == type.GetGenericTypeDefinition(), null).FirstOrDefault();
            if (null == listInterface)
            {
                return null;
            }
            return listInterface.GetGenericArguments()[0];
        }
    }
}
