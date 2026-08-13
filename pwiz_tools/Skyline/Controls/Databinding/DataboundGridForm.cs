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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
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

        /// <summary>
        /// The report to show when this form is recreated from a saved window layout, applied once in
        /// <see cref="OnShown"/>. Without it a restored grid comes back on whatever report the global
        /// setting last held, which is usually not the one the layout was captured with.
        /// </summary>
        public ViewName? ViewToRestore { get; set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (ViewToRestore.HasValue)
            {
                // One shot: the user is free to choose another report afterwards
                var viewToRestore = ViewToRestore.Value;
                ViewToRestore = null;
                DataboundGridControl?.ChooseView(viewToRestore);
            }
        }

        /// <summary>
        /// Appends the report currently showing, for a subclass's <see cref="GetPersistentString"/>.
        /// Always appended LAST, so parts a subclass adds of its own keep their positions.
        ///
        /// <para>The DEFAULT report is left off. That is what keeps the persistent string of a grid on
        /// its default report the bare window type, which is what Skyline versions before this change
        /// require: they match the window type with an exact comparison rather than a prefix, so a
        /// layout with anything appended does not restore that window at all. Leaving the default off
        /// makes the common case of a ".sky.view" still work in those versions.</para>
        /// </summary>
        protected PersistentString AppendViewName(PersistentString persistentString)
        {
            var viewName = GetViewName();
            if (!viewName.HasValue || Equals(viewName, GetDefaultViewName()))
                return persistentString;
            return persistentString.Append(viewName.Value.ToString());
        }

        /// <summary>The report a grid shows when nothing else has been chosen: the first built-in view.</summary>
        private ViewName? GetDefaultViewName()
        {
            var defaultViewSpec = BindingListSource?.ViewContext
                ?.GetViewSpecList(ViewGroup.BUILT_IN.Id).ViewSpecs.FirstOrDefault();
            return defaultViewSpec == null ? (ViewName?) null : ViewGroup.BUILT_IN.Id.ViewName(defaultViewSpec.Name);
        }

        /// <summary>
        /// The report name <see cref="AppendViewName"/> wrote, or null for a layout saved before this
        /// was persisted (older ".sky.view" files simply end after the parts they did have).
        /// </summary>
        protected static ViewName? ParsePersistedViewName(string persistentString, int partIndex)
        {
            var parts = PersistentString.Parse(persistentString).Parts;
            return parts.Count > partIndex ? ViewName.Parse(parts[partIndex]) : null;
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



        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var skylineWindow = (BindingListSource.ViewInfo.DataSchema as SkylineDataSchema)?.SkylineWindow;
            if (skylineWindow != null)
            {
                switch (keyData)
                {
                    case Keys.Z | Keys.Control:
                        skylineWindow.Undo();
                        return true;
                    case Keys.Y | Keys.Control:
                        skylineWindow.Redo();
                        return true;
                    case Keys.D | Keys.Control:
                        DataboundGridControl.FillDown();
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public UndoState GetUndoState()
        {
            var viewName = GetViewName();
            if (!viewName.HasValue)
            {
                return null;
            }

            return new UndoState(viewName.Value, DataGridView.CurrentCellAddress);
        }

        public void RestoreUndoState(UndoState undoState)
        {
            if (!Equals(GetViewName(), undoState.ViewName))
            {
                // If name of the report in the undo state is different than the current report
                // then do nothing.
                return;
            }

            var dataGridView = DataGridView;
            var cellAddress = undoState.CurrentCellAddress;
            if (cellAddress.X < 0 || cellAddress.X >= dataGridView.ColumnCount || 
                cellAddress.Y < 0 || cellAddress.Y >= dataGridView.RowCount)
            {
                return;
            }

            DataGridView.CurrentCell = DataGridView.Rows[cellAddress.Y].Cells[cellAddress.X];
        }

        public class UndoState
        {
            public UndoState(ViewName viewName, Point currentCellAddress)
            {
                ViewName = viewName;
                CurrentCellAddress = currentCellAddress;
            }
            public ViewName ViewName { get; }
            public Point CurrentCellAddress { get; }
        }


        public static IDictionary<DataGridId, UndoState> GetUndoStates()
        {
            var dictionary = new Dictionary<DataGridId, UndoState>();
            foreach (var form in FormUtil.OpenForms.OfType<DataboundGridForm>())
            {
                var dataGridId = form.DataGridId;
                if (dataGridId != null)
                {
                    var undoState = form.GetUndoState();
                    if (undoState != null)
                    {
                        dictionary[dataGridId] = undoState;
                    }
                }
            }

            return dictionary;
        }
        public static void RestoreUndoStates(IDictionary<DataGridId, UndoState> undoStates)
        {
            foreach (var form in FormUtil.OpenForms.OfType<DataboundGridForm>())
            {
                var dataGridId = form.DataGridId;
                if (dataGridId != null && undoStates.TryGetValue(dataGridId, out var undoState))
                {
                    form.RestoreUndoState(undoState);
                }
            }
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

        public virtual bool IsComplete
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
