//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is a post on MSDN: https://social.msdn.microsoft.com/Forums/windowsdesktop/en-US/25d6b3a6-1533-4fae-85a7-6cc553c8c9a7/datagridview-extended-to-implement-ui-automation?forum=windowsaccessibilityandautomation&prof=required
//
// The Initial Developer of the Original Code is Mike Watkins.
//
// Copyright 2015 Mike Watkins
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Drawing;
using System.Security.Permissions;
using System.ComponentModel;

namespace IDPicker.Controls
{
    public class AutomationDataGridView : PreviewDataGridView, IRawElementProviderFragmentRoot, IGridProvider, ISelectionProvider, ITableProvider
    {
        public const int WM_GETOBJECT = 0x3D;       // Windows Message GetObject

        /// <summary>
        /// Handles WM_GETOBJECT message; others are passed to base handler.
        /// </summary>
        /// <param name="m">Windows message.</param>
        /// <remarks>This method provides the link with UI Automation.</remarks>
        [PermissionSetAttribute(SecurityAction.Demand, Unrestricted = true)]
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if ((m.Msg == WM_GETOBJECT) && (m.LParam.ToInt64() == AutomationInteropProvider.RootObjectId))
            {
                m.Result = AutomationInteropProvider.ReturnRawElementProvider(
                    Handle, m.WParam, m.LParam, (IRawElementProviderSimple)this);

                return;
            }
            base.WndProc(ref m);
        }

        #region IRawElementProviderSimple Members

        /// <summary>
        /// Retrieves an object that provides support for a control pattern on a UI Automation element.
        /// </summary>
        /// <param name="patternId">Identifier of the pattern.</param>
        /// <returns>
        /// Object that implements the pattern interface, or null if the pattern is not supported.
        /// </returns>
        object IRawElementProviderSimple.GetPatternProvider(int patternId)
        {
            if (patternId.Equals(GridPatternIdentifiers.Pattern.Id) ||
                patternId.Equals(TablePatternIdentifiers.Pattern.Id) ||
                patternId.Equals(SelectionPatternIdentifiers.Pattern.Id))
            {
                return this;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the value of a property supported by the UI Automation provider.
        /// </summary>
        /// <param name="propertyId">The property identifier.</param>
        /// <returns>
        /// The property value, or a null if the property is not supported by this provider, or <see cref="F:System.Windows.Automation.AutomationElementIdentifiers.NotSupported"/> if it is not supported at all.
        /// </returns>
        object IRawElementProviderSimple.GetPropertyValue(int propertyId)
        {
            if (propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id)
            {
                return ControlType.Table.Id;
                //return ControlType.DataGrid.Id;
            }
            else if (propertyId == AutomationElementIdentifiers.AutomationIdProperty.Id ||
                     propertyId == AutomationElementIdentifiers.NameProperty.Id)
            {
                return this.Name;
            }
            else if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
            {
                bool canFocus = false;
                this.Invoke(new MethodInvoker(() => { canFocus = this.CanFocus; }));
                return canFocus;
            }
            else if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
            {
                return this.GetType().ToString();
            }
            else if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
            {
                return this.Enabled;
            }

            return null;
        }

        /// <summary>
        /// Gets a base provider for this element.
        /// </summary>
        /// <value></value>
        /// <returns>The base provider, or null.</returns>
        IRawElementProviderSimple IRawElementProviderSimple.HostRawElementProvider
        {
            get
            {
                IntPtr hwnd = IntPtr.Zero;
                Invoke(new MethodInvoker(() => { hwnd = this.Handle; }));
                return AutomationInteropProvider.HostProviderFromHandle(hwnd);
            }
        }

        /// <summary>
        /// Gets a value that specifies characteristics of the UI Automation provider; for example, whether it is a client-side or server-side provider.
        /// </summary>
        /// <value></value>
        /// <returns>Either <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ClientSideProvider"/> or <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ServerSideProvider"/>.</returns>
        ProviderOptions IRawElementProviderSimple.ProviderOptions
        {
            get
            {
                return ProviderOptions.ServerSideProvider;
            }
        }

        #endregion

        #region IRawElementProviderFragment Members

        /// <summary>
        /// Gets the bounding rectangle of this element.
        /// </summary>
        /// <value></value>
        /// <returns>The bounding rectangle, in screen coordinates.</returns>
        System.Windows.Rect IRawElementProviderFragment.BoundingRectangle
        {
            get
            {
                Rectangle bounds = Rectangle.Empty;
                Invoke(new MethodInvoker(() => bounds = RectangleToScreen(Bounds)));
                return new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        /// Retrieves the root node of the fragment.
        /// </summary>
        /// <value></value>
        /// <returns>The root node. </returns>
        IRawElementProviderFragmentRoot IRawElementProviderFragment.FragmentRoot
        {
            get
            {
                return this;
            }
        }

        /// <summary>
        /// Retrieves an array of fragment roots that are embedded in the UI Automation element tree rooted at the current element.
        /// </summary>
        /// <returns>An array of root fragments, or null.</returns>
        IRawElementProviderSimple[] IRawElementProviderFragment.GetEmbeddedFragmentRoots()
        {
            List<IRawElementProviderSimple> embeddedFragmentRoots = new List<IRawElementProviderSimple>();
            foreach (DataGridViewRow row in this.Rows)
            {
                embeddedFragmentRoots.Add(new AutomationDataGridViewRow(this, row));
            }

            return embeddedFragmentRoots.ToArray();
        }

        /// <summary>
        /// Retrieves the runtime identifier of an element.
        /// </summary>
        /// <returns>
        /// The unique run-time identifier of the element.
        /// </returns>
        int[] IRawElementProviderFragment.GetRuntimeId()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the UI Automation element in a specified direction within the tree.
        /// </summary>
        /// <param name="direction">The direction in which to navigate.</param>
        /// <returns>
        /// The element in the specified direction, or null if there is no element in that direction
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragment.Navigate(NavigateDirection direction)
        {
            if (this.Rows.Count > 0)
            {
                switch (direction)
                {
                    case NavigateDirection.FirstChild:
                        return new AutomationDataGridViewHeaderRow(this);
                    case NavigateDirection.LastChild:
                        return new AutomationDataGridViewRow(this, Rows[Rows.Count - 1]);
                }
            }

            return null;
        }

        /// <summary>
        /// Responds to a client request to set the focus to this control.
        /// </summary>
        /// <remarks>Setting focus to the control is handled by the parent window.</remarks>
        void IRawElementProviderFragment.SetFocus()
        {
            this.Invoke(new MethodInvoker(() =>
            {
                if (this.CanFocus)
                {
                    this.Focus();
                }
            }));
        }

        #endregion

        #region IRawElementProviderFragmentRoot Members

        /// <summary>
        /// Retrieves the element in this fragment that is at the specified point.
        /// </summary>
        /// <param name="x">The X coordinate,.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>
        /// The provider for the child element at the specified point, if one exists, or the root provider if the point is on this element but not on any child element. Otherwise returns null.
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.ElementProviderFromPoint(double x, double y)
        {
            AutomationDataGridViewRow returnRow = null;

            this.Invoke(new MethodInvoker(delegate()
            {
                System.Drawing.Point clientPoint = new System.Drawing.Point((int)x, (int)y);
                clientPoint = this.PointToClient(clientPoint);

                foreach (DataGridViewRow row in this.Rows)
                {
                    if (this.GetRowDisplayRectangle(row.Index, false).Contains(clientPoint))
                    {
                        returnRow = new AutomationDataGridViewRow(this, row);
                        break;
                    }
                }
            }));

            return returnRow;
        }

        /// <summary>
        /// Retrieves the element in this fragment that has the input focus.
        /// </summary>
        /// <returns>
        /// The provider for the element in this fragment that has the input focus, if any; otherwise, a null reference (Nothing in Visual Basic).
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.GetFocus()
        {
            return new AutomationDataGridViewRow(this, this.CurrentRow);
        }

        #endregion

        #region IGridProvider Members

        /// <summary>
        /// Gets the total number of columns in a grid.
        /// </summary>
        /// <value></value>
        /// <returns>The total number of columns in a grid.</returns>
        int IGridProvider.ColumnCount
        {
            get
            {
                return this.Columns.OfType<DataGridViewColumn>().Where(o => o.Visible).Count();
            }
        }

        /// <summary>
        /// Retrieves the UI Automation provider for the specified cell.
        /// </summary>
        /// <param name="row">The ordinal number of the row of interest.</param>
        /// <param name="column">The ordinal number of the column of interest.</param>
        /// <returns>
        /// The UI Automation provider for the specified cell.
        /// </returns>
        IRawElementProviderSimple IGridProvider.GetItem(int row, int column)
        {
            if (this.Rows.Count <= row)
            {
                throw new ArgumentException("Invalid row index specified.");
            }

            if (ColumnCount <= column)
            {
                throw new ArgumentException("Invalid column index spceified.");
            }

            return new AutomationDataGridViewCell(this, new AutomationDataGridViewRow(this, Rows[row]), Rows[row].Cells[column]);
        }

        /// <summary>
        /// Gets the number of rows excluding those that are hidden because the
        /// parent row is collapsed.
        /// </summary>
        /// <value></value>
        int IGridProvider.RowCount
        {
            get
            {
                return this.RowCount;
            }
        }

        #endregion

        #region ISelectionProvider Members

        /// <summary>
        /// Gets a value that specifies whether the UI Automation provider allows more than one child element to be selected concurrently.
        /// </summary>
        /// <value></value>
        /// <returns>true if multiple selection is allowed; otherwise false.</returns>
        bool ISelectionProvider.CanSelectMultiple
        {
            get
            {
                return this.MultiSelect;
            }
        }

        /// <summary>
        /// Retrieves a UI Automation provider for each child element that is selected.
        /// </summary>
        /// <returns>A collection of UI Automation providers.</returns>
        IRawElementProviderSimple[] ISelectionProvider.GetSelection()
        {
            List<IRawElementProviderSimple> selectedItems = new List<IRawElementProviderSimple>();

            foreach (DataGridViewRow selectedItem in this.SelectedRows)
            {
                selectedItems.Add(new AutomationDataGridViewRow(this, selectedItem));
            }

            return selectedItems.ToArray();
        }

        /// <summary>
        /// Gets a value that specifies whether the UI Automation provider requires at least one child element to be selected.
        /// </summary>
        /// <value></value>
        /// <returns>true if selection is required; otherwise false.</returns>
        bool ISelectionProvider.IsSelectionRequired
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ITableProvider Members

        public IRawElementProviderSimple[] GetColumnHeaders()
        {
            return Columns.OfType<DataGridViewColumn>().Where(o => o.Visible).OrderBy(o => o.DisplayIndex).Select(o => new AutomationDataGridViewHeaderCell(this, this, o.HeaderCell)).ToArray();
        }

        public IRawElementProviderSimple[] GetRowHeaders()
        {
            if (RowHeadersVisible)
                return Rows.OfType<DataGridViewRow>().Where(o => o.Visible).Select(o => new AutomationDataGridViewHeaderCell(this, this, o.HeaderCell)).ToArray();
            return null;
        }

        public RowOrColumnMajor RowOrColumnMajor
        {
            get { return RowOrColumnMajor.RowMajor; }
        }

        #endregion

        internal void EmulateMouseClick(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            base.OnClick(e);
            base.OnMouseClick(e);
            base.OnMouseUp(e);
        }
    }

    #region Nested Classes

    /// <summary>
    /// Common data grid view row class used for exposing each row to UI Automation.
    /// </summary>
    internal class AutomationDataGridViewRow :
        IRawElementProviderFragmentRoot,
        ISelectionItemProvider
    {
        AutomationDataGridView _grid;
        public DataGridViewRow _row { get; private set; }

        /// <summary>
        /// Class Constructor.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="row"></param>
        public AutomationDataGridViewRow(AutomationDataGridView grid, DataGridViewRow row)
        {
            if (grid == null)
            {
                throw new ArgumentNullException("grid");
            }

            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            _grid = grid;
            _row = row;

        }

        #region IRawElementProviderFragmentRoot Members

        /// <summary>
        /// Retrieves the element in this fragment that is at the specified point.
        /// </summary>
        /// <param name="x">The X coordinate,.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>
        /// The provider for the child element at the specified point, if one exists, or the root provider if the point is on this element but not on any child element. Otherwise returns null.
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.ElementProviderFromPoint(double x, double y)
        {
            // return cell from point value here
            System.Drawing.Point clientPoint = new System.Drawing.Point((int)x, (int)y);

            _grid.Invoke(new MethodInvoker(delegate() { clientPoint = _grid.PointToClient(clientPoint); }));

            foreach (DataGridViewCell cell in _row.Cells)
            {
                Rectangle cellBounds = new Rectangle();
                _grid.Invoke(new MethodInvoker(delegate() { cellBounds = _grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false); }));

                if (cellBounds.Contains(clientPoint))
                {
                    return new AutomationDataGridViewCell(_grid, this, cell);
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the element in this fragment that has the input focus.
        /// </summary>
        /// <returns>
        /// The provider for the element in this fragment that has the input focus, if any; otherwise, a null reference (Nothing in Visual Basic).
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.GetFocus()
        {
            // Return the cell that has focus here
            return new AutomationDataGridViewCell(_grid, this, _grid.CurrentCell);
        }

        #endregion

        #region IRawElementProviderFragment Members

        /// <summary>
        /// Gets the bounding rectangle of this element.
        /// </summary>
        /// <value></value>
        /// <returns>The bounding rectangle, in screen coordinates.</returns>
        System.Windows.Rect IRawElementProviderFragment.BoundingRectangle
        {
            get
            {
                Rectangle bounds = Rectangle.Empty;
                _grid.Invoke(new MethodInvoker(() => bounds = _grid.RectangleToScreen(_grid.GetRowDisplayRectangle(_row.Index, true))));
                return new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        /// Retrieves the root node of the fragment.
        /// </summary>
        /// <value></value>
        /// <returns>The root node. </returns>
        IRawElementProviderFragmentRoot IRawElementProviderFragment.FragmentRoot
        {
            get
            {
                return _grid;
            }
        }

        /// <summary>
        /// Retrieves an array of fragment roots that are embedded in the UI Automation element tree rooted at the current element.
        /// </summary>
        /// <returns>An array of root fragments, or null.</returns>
        IRawElementProviderSimple[] IRawElementProviderFragment.GetEmbeddedFragmentRoots()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the runtime identifier of an element.
        /// </summary>
        /// <returns>
        /// The unique run-time identifier of the element.
        /// </returns>
        int[] IRawElementProviderFragment.GetRuntimeId()
        {
            return new int[] { AutomationInteropProvider.AppendRuntimeId, _row.Index };
        }

        /// <summary>
        /// Retrieves the UI Automation element in a specified direction within the tree.
        /// </summary>
        /// <param name="direction">The direction in which to navigate.</param>
        /// <returns>
        /// The element in the specified direction, or null if there is no element in that direction
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragment.Navigate(NavigateDirection direction)
        {
            switch (direction)
            {
                case NavigateDirection.FirstChild:
                    if (_row.HeaderCell.Visible)
                        return new AutomationDataGridViewHeaderCell(_grid, this, _row.HeaderCell);
                    else
                        return new AutomationDataGridViewCell(_grid, this, _row.Cells.OfType<DataGridViewCell>().Where(o => o.Visible).OrderBy(o => o.OwningColumn.DisplayIndex).First());

                case NavigateDirection.LastChild:
                    return new AutomationDataGridViewCell(_grid, this, _row.Cells.OfType<DataGridViewCell>().Where(o => o.Visible).OrderByDescending(o => o.OwningColumn.DisplayIndex).First());

                case NavigateDirection.NextSibling:
                    if (_row.Index < _grid.Rows.Count - 1)
                    {
                        return new AutomationDataGridViewRow(_grid, _grid.Rows[_row.Index + 1]);
                    }
                    break;

                case NavigateDirection.PreviousSibling:
                    if (_row.Index > 0)
                    {
                        return new AutomationDataGridViewRow(_grid, _grid.Rows[_row.Index - 1]);
                    }
                    break;

                case NavigateDirection.Parent:
                    return _grid;
            }

            return null;
        }

        /// <summary>
        /// Sets the focus to this element.
        /// </summary>
        void IRawElementProviderFragment.SetFocus()
        {
            _grid.Invoke(new MethodInvoker(delegate() { _row.Selected = true; }));
        }

        #endregion

        #region IRawElementProviderSimple Members

        /// <summary>
        /// Retrieves an object that provides support for a control pattern on a UI Automation element.
        /// </summary>
        /// <param name="patternId">Identifier of the pattern.</param>
        /// <returns>
        /// Object that implements the pattern interface, or null if the pattern is not supported.
        /// </returns>
        object IRawElementProviderSimple.GetPatternProvider(int patternId)
        {
            if (patternId.Equals(SelectionItemPatternIdentifiers.Pattern.Id))
            {
                return this;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the value of a property supported by the UI Automation provider.
        /// </summary>
        /// <param name="propertyId">The property identifier.</param>
        /// <returns>
        /// The property value, or a null if the property is not supported by this provider, or <see cref="F:System.Windows.Automation.AutomationElementIdentifiers.NotSupported"/> if it is not supported at all.
        /// </returns>
        object IRawElementProviderSimple.GetPropertyValue(int propertyId)
        {
            if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
            {
                return true;
            }
            else if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
            {
                return this.GetType().ToString();
            }
            else if (propertyId == AutomationElementIdentifiers.LocalizedControlTypeProperty.Id ||
                     propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id)
            {
                return ControlType.DataItem.Id;
            }
            else if (propertyId == AutomationElementIdentifiers.NameProperty.Id)
            {
                var cellValues = _row.Cells.OfType<DataGridViewCell>().Where(o => o.Visible).OrderBy(o => o.OwningColumn.DisplayIndex).Select(o => o.FormattedValue);
                if (_row.HeaderCell.Visible)
                    cellValues = new string[] { _row.HeaderCell.Value.ToString() }.Concat(cellValues);
                return String.Join(";", cellValues);
            }
            else if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
            {
                IRawElementProviderSimple provider = _grid as IRawElementProviderSimple;
                return ((bool)provider.GetPropertyValue(AutomationElementIdentifiers.IsEnabledProperty.Id));
            }

            return null;
        }

        /// <summary>
        /// Gets a base provider for this element.
        /// </summary>
        /// <value></value>
        /// <returns>The base provider, or null.</returns>
        IRawElementProviderSimple IRawElementProviderSimple.HostRawElementProvider
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a value that specifies characteristics of the UI Automation provider; for example, whether it is a client-side or server-side provider.
        /// </summary>
        /// <value></value>
        /// <returns>Either <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ClientSideProvider"/> or <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ServerSideProvider"/>.</returns>
        ProviderOptions IRawElementProviderSimple.ProviderOptions
        {
            get
            {
                return ProviderOptions.ServerSideProvider;
            }
        }

        #endregion

        #region ISelectionItemProvider Members

        /// <summary>
        /// Adds the current element to the collection of selected items.
        /// </summary>
        void ISelectionItemProvider.AddToSelection()
        {
            ((ISelectionItemProvider)this).Select();
        }

        /// <summary>
        /// Gets a value that indicates whether an item is selected.
        /// </summary>
        /// <value></value>
        /// <returns>true if the element is selected; otherwise false.</returns>
        bool ISelectionItemProvider.IsSelected
        {
            get
            {
                return _row.Selected;
            }
        }

        /// <summary>
        /// Removes the current element from the collection of selected items.
        /// </summary>
        void ISelectionItemProvider.RemoveFromSelection()
        {
            _grid.Invoke(new MethodInvoker(() =>
            {
                if (_row.Selected)
                {
                    _row.Selected = false;
                }
            }));
        }

        /// <summary>
        /// Deselects any selected items and then selects the current element.
        /// </summary>
        void ISelectionItemProvider.Select()
        {
            IRawElementProviderSimple provider = _grid as IRawElementProviderSimple;
            if (!(bool)provider.GetPropertyValue(AutomationElementIdentifiers.IsEnabledProperty.Id))
            {
                throw new ElementNotEnabledException();
            }

            if (!((ISelectionItemProvider)this).IsSelected)
            {
                _grid.Invoke(new MethodInvoker(() =>
                {
                    _row.Selected = true;
                }));
            }
        }

        /// <summary>
        /// Gets the UI Automation provider that implements <see cref="T:System.Windows.Automation.Provider.ISelectionProvider"/> and acts as the container for the calling object.
        /// </summary>
        /// <value></value>
        /// <returns>The provider that supports <see cref="T:System.Windows.Automation.Provider.ISelectionProvider"/>. </returns>
        IRawElementProviderSimple ISelectionItemProvider.SelectionContainer
        {
            get
            {
                return _grid;
            }
        }

        #endregion
    }

    /// <summary>
    /// Class that represents a GridCell for the purpose of Automation.
    /// </summary>
    internal class AutomationDataGridViewCell :
        IRawElementProviderFragment,
        IGridItemProvider,
        IValueProvider,
        ISelectionItemProvider,
        IInvokeProvider
    {
        private AutomationDataGridView _grid;
        private AutomationDataGridViewRow _row;
        private DataGridViewCell _cell;

        public AutomationDataGridViewCell(AutomationDataGridView grid, AutomationDataGridViewRow row, DataGridViewCell cell)
        {
            if (grid == null)
            {
                throw new ArgumentNullException("grid");
            }

            if (row == null)
            {
                throw new ArgumentNullException("row");
            }

            if (cell == null)
            {
                throw new ArgumentNullException("cell");
            }

            _grid = grid;
            _row = row;
            _cell = cell;
        }

        #region IRawElementProviderSimple Members

        /// <summary>
        /// Retrieves an object that provides support for a control pattern on a UI Automation element.
        /// </summary>
        /// <param name="patternId">Identifier of the pattern.</param>
        /// <returns>
        /// Object that implements the pattern interface, or null if the pattern is not supported.
        /// </returns>
        object IRawElementProviderSimple.GetPatternProvider(int patternId)
        {
            if (patternId.Equals(GridItemPatternIdentifiers.Pattern.Id) ||
                patternId.Equals(ValuePatternIdentifiers.Pattern.Id) ||
                patternId.Equals(SelectionItemPatternIdentifiers.Pattern.Id) ||
                patternId.Equals(InvokePatternIdentifiers.Pattern.Id))
            {
                return this;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieves the value of a property supported by the UI Automation provider.
        /// </summary>
        /// <param name="propertyId">The property identifier.</param>
        /// <returns>
        /// The property value, or a null if the property is not supported by this provider, or <see cref="F:System.Windows.Automation.AutomationElementIdentifiers.NotSupported"/> if it is not supported at all.
        /// </returns>
        object IRawElementProviderSimple.GetPropertyValue(int propertyId)
        {
            if (propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id ||
                propertyId == AutomationElementIdentifiers.LocalizedControlTypeProperty.Id)
            {
                return ControlType.DataItem.Id;
            }
            else if (propertyId == AutomationElementIdentifiers.AutomationIdProperty.Id)
            {
                return _grid.Columns[_cell.ColumnIndex].HeaderText;
            }
            else if (propertyId == AutomationElementIdentifiers.NameProperty.Id)
            {
                return _cell.Value;
            }
            else if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
            {
                return this.GetType().ToString();
            }
            else if (propertyId == AutomationElementIdentifiers.HasKeyboardFocusProperty.Id)
            {
                return _cell.IsInEditMode;
            }
            else if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
            {
                return _grid.Enabled;
            }
            else if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
            {
                return _grid.Enabled && _grid.Visible;
            }

            return null;
        }

        /// <summary>
        /// Gets a base provider for this element.
        /// </summary>
        /// <value></value>
        /// <returns>The base provider, or null.</returns>
        IRawElementProviderSimple IRawElementProviderSimple.HostRawElementProvider
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a value that specifies characteristics of the UI Automation provider; for example, whether it is a client-side or server-side provider.
        /// </summary>
        /// <value></value>
        /// <returns>Either <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ClientSideProvider"/> or <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ServerSideProvider"/>.</returns>
        ProviderOptions IRawElementProviderSimple.ProviderOptions
        {
            get
            {
                return ProviderOptions.ServerSideProvider;
            }
        }

        #endregion

        #region IRawElementProviderFragment Members

        /// <summary>
        /// Gets the bounding rectangle of this element.
        /// </summary>
        /// <value></value>
        /// <returns>The bounding rectangle, in screen coordinates.</returns>
        System.Windows.Rect IRawElementProviderFragment.BoundingRectangle
        {
            get
            {
                Rectangle bounds = Rectangle.Empty;
                _grid.Invoke(new MethodInvoker(() => bounds = _grid.RectangleToScreen(_grid.GetCellDisplayRectangle(_cell.ColumnIndex, _cell.RowIndex, true))));
                return new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        /// Retrieves the root node of the fragment.
        /// </summary>
        /// <value></value>
        /// <returns>The root node. </returns>
        IRawElementProviderFragmentRoot IRawElementProviderFragment.FragmentRoot
        {
            get
            {
                return _grid;
            }
        }

        /// <summary>
        /// Retrieves an array of fragment roots that are embedded in the UI Automation element tree rooted at the current element.
        /// </summary>
        /// <returns>An array of root fragments, or null.</returns>
        IRawElementProviderSimple[] IRawElementProviderFragment.GetEmbeddedFragmentRoots()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the runtime identifier of an element.
        /// </summary>
        /// <returns>
        /// The unique run-time identifier of the element.
        /// </returns>
        int[] IRawElementProviderFragment.GetRuntimeId()
        {
            return new int[] { AutomationInteropProvider.AppendRuntimeId, _cell.RowIndex, _cell.ColumnIndex };
        }

        private IRawElementProviderFragment nextCell()
        {
            var nextColumnInDisplayOrder = _grid.Columns.OfType<DataGridViewColumn>()
                                                        .Where(o => o.Visible)
                                                        .OrderBy(o => o.DisplayIndex)
                                                        .SkipWhile(o => o.Index != _cell.ColumnIndex)
                                                        .Skip(1)
                                                        .FirstOrDefault();
            if (nextColumnInDisplayOrder != null)
                return new AutomationDataGridViewCell(_grid, _row, _cell.OwningRow.Cells[nextColumnInDisplayOrder.Index]);
            return null;
        }

        private IRawElementProviderFragment previousCell()
        {
            var previousColumnInDisplayOrder = _grid.Columns.OfType<DataGridViewColumn>()
                                                            .Where(o => o.Visible)
                                                            .OrderByDescending(o => o.DisplayIndex)
                                                            .SkipWhile(o => o.Index != _cell.ColumnIndex)
                                                            .Skip(1)
                                                            .FirstOrDefault();
            if (previousColumnInDisplayOrder != null)
                return new AutomationDataGridViewCell(_grid, _row, _cell.OwningRow.Cells[previousColumnInDisplayOrder.Index]);
            else if (_grid.RowHeadersVisible)
                return new AutomationDataGridViewHeaderCell(_grid, _row, _cell.OwningRow.HeaderCell);
            return null;
        }

        /// <summary>
        /// Retrieves the UI Automation element in a specified direction within the tree.
        /// </summary>
        /// <param name="direction">The direction in which to navigate.</param>
        /// <returns>
        /// The element in the specified direction, or null if there is no element in that direction
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragment.Navigate(NavigateDirection direction)
        {
            switch (direction)
            {
                case NavigateDirection.NextSibling:
                    return nextCell();
                case NavigateDirection.PreviousSibling:
                    return previousCell();
                case NavigateDirection.Parent:
                    return _row;
            }

            return null;
        }

        /// <summary>
        /// Sets the focus to this element.
        /// </summary>
        void IRawElementProviderFragment.SetFocus()
        {
            _grid.Invoke(new MethodInvoker(delegate()
            {
                _grid.CurrentCell = _cell;
            }));
        }

        #endregion

        #region IGridItemProvider Members

        /// <summary>
        /// Gets the ordinal number of the column that contains the cell or item.
        /// </summary>
        /// <value></value>
        /// <returns>A zero-based ordinal number that identifies the column containing the cell or item.</returns>
        int IGridItemProvider.Column
        {
            get
            {
                return _cell.ColumnIndex;
            }
        }

        /// <summary>
        /// Gets the number of columns spanned by a cell or item.
        /// </summary>
        /// <value></value>
        /// <returns>The number of columns spanned. </returns>
        int IGridItemProvider.ColumnSpan
        {
            get
            {
                return 1;
            }
        }

        /// <summary>
        /// Gets a UI Automation provider that implements <see cref="T:System.Windows.Automation.Provider.IGridProvider"/> and represents the container of the cell or item.
        /// </summary>
        /// <value></value>
        /// <returns>A UI Automation provider that implements the <see cref="T:System.Windows.Automation.GridPattern"/> and represents the cell or item container. </returns>
        IRawElementProviderSimple IGridItemProvider.ContainingGrid
        {
            get
            {
                return _grid;
            }
        }

        /// <summary>
        /// Gets the ordinal number of the row that contains the cell or item.
        /// </summary>
        /// <value></value>
        /// <returns>A zero-based ordinal number that identifies the row containing the cell or item. </returns>
        int IGridItemProvider.Row
        {
            get
            {
                return _cell.RowIndex;
            }
        }

        /// <summary>
        /// Gets the number of rows spanned by a cell or item.
        /// </summary>
        /// <value></value>
        /// <returns>The number of rows spanned. </returns>
        int IGridItemProvider.RowSpan
        {
            get
            {
                return 1;
            }
        }

        #endregion

        #region IValueProvider Members

        /// <summary>
        /// Gets a value that specifies whether the value of a control is read-only.
        /// </summary>
        /// <value></value>
        /// <returns>true if the value is read-only; false if it can be modified. </returns>
        bool IValueProvider.IsReadOnly
        {
            get
            {
                return _cell.State == DataGridViewElementStates.ReadOnly;
            }
        }

        /// <summary>
        /// Sets the value of a control.
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="T:System.InvalidOperationException">If locale-specific information is passed to a control in an incorrect format such as an incorrectly formatted date. </exception>
        /// <exception cref="T:System.ArgumentException">If a new value cannot be converted from a string to a format the control recognizes.</exception>
        /// <exception cref="T:System.Windows.Automation.ElementNotEnabledException">When an attempt is made to manipulate a control that is not enabled.</exception>
        void IValueProvider.SetValue(string value)
        {
            // Check if we are enabled
            if (!(bool)((IRawElementProviderSimple)this).GetPropertyValue(AutomationElementIdentifiers.IsEnabledProperty.Id))
            {
                throw new ElementNotEnabledException();
            }

            // Check if we are read only
            if (((IValueProvider)this).IsReadOnly)
            {
                throw new InvalidOperationException("Cannot set the value on a ReadOnly field!");
            }

            // Set Value
            _grid.Invoke(new MethodInvoker(delegate()
            {
                _grid.BeginEdit(false);
                _cell.Value = value;
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                _grid.EndEdit();
            }));
        }

        /// <summary>
        /// Gets the value of the control.
        /// </summary>
        /// <value></value>
        /// <returns>The value of the control as a string. </returns>
        string IValueProvider.Value
        {
            get
            {
                return _cell.Value == null ? null : _cell.Value.ToString();
            }
        }

        #endregion

        #region ISelectionItemProvider Members

        /// <summary>
        /// Adds the current element to the collection of selected items.
        /// </summary>
        void ISelectionItemProvider.AddToSelection()
        {
            _grid.Invoke(new MethodInvoker(() =>
            {
                _cell.Selected = true;
            }));
        }

        /// <summary>
        /// Gets a value that indicates whether an item is selected.
        /// </summary>
        /// <value></value>
        /// <returns>true if the element is selected; otherwise false.</returns>
        bool ISelectionItemProvider.IsSelected
        {
            get
            {
                bool selected = false;
                _grid.Invoke(new MethodInvoker(() => { selected = _grid.CurrentCell.Equals(_cell); }));
                return selected;
            }
        }

        /// <summary>
        /// Removes the current element from the collection of selected items.
        /// </summary>
        void ISelectionItemProvider.RemoveFromSelection()
        {
            _grid.Invoke(new MethodInvoker(() =>
            {
                _cell.Selected = false;
            }));
        }

        /// <summary>
        /// Deselects any selected items and then selects the current element.
        /// </summary>
        void ISelectionItemProvider.Select()
        {
            _grid.Invoke(new MethodInvoker(() =>
            {
                foreach (DataGridViewCell cell in _grid.SelectedCells)
                {
                    cell.Selected = false;
                }
                _cell.Selected = true;
            }));
        }

        /// <summary>
        /// Gets the UI Automation provider that implements <see cref="T:System.Windows.Automation.Provider.ISelectionProvider"/> and acts as the container for the calling object.
        /// </summary>
        /// <value></value>
        /// <returns>The provider that supports <see cref="T:System.Windows.Automation.Provider.ISelectionProvider"/>. </returns>
        IRawElementProviderSimple ISelectionItemProvider.SelectionContainer
        {
            get
            {
                return _grid;
            }
        }

        #endregion

        #region IInvokeProvider Members

        public void Invoke()
        {
            _grid.Focus();
            _cell.Selected = true;
        }

        #endregion
    }


    internal class AutomationDataGridViewHeaderRow :
        IRawElementProviderFragmentRoot
    {
        AutomationDataGridView _grid;

        /// <summary>
        /// Class Constructor.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="row"></param>
        public AutomationDataGridViewHeaderRow(AutomationDataGridView grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException("grid");
            }

            _grid = grid;
        }

        #region IRawElementProviderFragmentRoot Members

        /// <summary>
        /// Retrieves the element in this fragment that is at the specified point.
        /// </summary>
        /// <param name="x">The X coordinate,.</param>
        /// <param name="y">The Y coordinate.</param>
        /// <returns>
        /// The provider for the child element at the specified point, if one exists, or the root provider if the point is on this element but not on any child element. Otherwise returns null.
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.ElementProviderFromPoint(double x, double y)
        {
            // return cell from point value here
            System.Drawing.Point clientPoint = new System.Drawing.Point((int)x, (int)y);
            AutomationDataGridViewHeaderCell headerCell = null;

            _grid.Invoke(new MethodInvoker(delegate()
            {
                clientPoint = _grid.PointToClient(clientPoint);

                foreach (DataGridViewColumn column in _grid.Columns)
                {
                    if (!column.Visible)
                        continue;

                    Rectangle cellBounds = _grid.GetCellDisplayRectangle(column.Index, -1, false);

                    if (cellBounds.Contains(clientPoint))
                    {
                        headerCell = new AutomationDataGridViewHeaderCell(_grid, this, column.HeaderCell);
                        break;
                    }
                }
            }));

            return headerCell;
        }

        /// <summary>
        /// Retrieves the element in this fragment that has the input focus.
        /// </summary>
        /// <returns>
        /// The provider for the element in this fragment that has the input focus, if any; otherwise, a null reference (Nothing in Visual Basic).
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragmentRoot.GetFocus()
        {
            // Return the cell that has focus here
            var currentHeaderCell = _grid.CurrentCell as DataGridViewHeaderCell;
            if (currentHeaderCell != null)
                return new AutomationDataGridViewHeaderCell(_grid, this, currentHeaderCell);
            return null;
        }

        #endregion

        #region IRawElementProviderFragment Members

        /// <summary>
        /// Gets the bounding rectangle of this element.
        /// </summary>
        /// <value></value>
        /// <returns>The bounding rectangle, in screen coordinates.</returns>
        System.Windows.Rect IRawElementProviderFragment.BoundingRectangle
        {
            get
            {
                Rectangle bounds = Rectangle.Empty;
                _grid.Invoke(new MethodInvoker(() => { bounds = _grid.RectangleToScreen(_grid.Bounds); bounds.Height = _grid.ColumnHeadersHeight; }));
                return new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        /// <summary>
        /// Retrieves the root node of the fragment.
        /// </summary>
        /// <value></value>
        /// <returns>The root node. </returns>
        IRawElementProviderFragmentRoot IRawElementProviderFragment.FragmentRoot
        {
            get
            {
                return _grid;
            }
        }

        /// <summary>
        /// Retrieves an array of fragment roots that are embedded in the UI Automation element tree rooted at the current element.
        /// </summary>
        /// <returns>An array of root fragments, or null.</returns>
        IRawElementProviderSimple[] IRawElementProviderFragment.GetEmbeddedFragmentRoots()
        {
            return null;
        }

        /// <summary>
        /// Retrieves the runtime identifier of an element.
        /// </summary>
        /// <returns>
        /// The unique run-time identifier of the element.
        /// </returns>
        int[] IRawElementProviderFragment.GetRuntimeId()
        {
            return new int[] { AutomationInteropProvider.AppendRuntimeId, -1 };
        }

        /// <summary>
        /// Retrieves the UI Automation element in a specified direction within the tree.
        /// </summary>
        /// <param name="direction">The direction in which to navigate.</param>
        /// <returns>
        /// The element in the specified direction, or null if there is no element in that direction
        /// </returns>
        IRawElementProviderFragment IRawElementProviderFragment.Navigate(NavigateDirection direction)
        {
            switch (direction)
            {
                case NavigateDirection.FirstChild:
                    return new AutomationDataGridViewHeaderCell(_grid, this, _grid.Columns.OfType<DataGridViewColumn>().Where(o => o.Visible).OrderBy(o => o.DisplayIndex).First().HeaderCell);

                case NavigateDirection.LastChild:
                    return new AutomationDataGridViewHeaderCell(_grid, this, _grid.Columns.OfType<DataGridViewColumn>().Where(o => o.Visible).OrderByDescending(o => o.DisplayIndex).First().HeaderCell);

                case NavigateDirection.NextSibling:
                    if (_grid.RowCount > 0)
                        return new AutomationDataGridViewRow(_grid, _grid.Rows[0]);
                    break;

                case NavigateDirection.Parent:
                    return _grid;
            }

            return null;
        }

        /// <summary>
        /// Sets the focus to this element.
        /// </summary>
        void IRawElementProviderFragment.SetFocus()
        {
            _grid.Focus();
        }

        #endregion

        #region IRawElementProviderSimple Members

        /// <summary>
        /// Retrieves an object that provides support for a control pattern on a UI Automation element.
        /// </summary>
        /// <param name="patternId">Identifier of the pattern.</param>
        /// <returns>
        /// Object that implements the pattern interface, or null if the pattern is not supported.
        /// </returns>
        object IRawElementProviderSimple.GetPatternProvider(int patternId)
        {
            return null;
        }

        /// <summary>
        /// Retrieves the value of a property supported by the UI Automation provider.
        /// </summary>
        /// <param name="propertyId">The property identifier.</param>
        /// <returns>
        /// The property value, or a null if the property is not supported by this provider, or <see cref="F:System.Windows.Automation.AutomationElementIdentifiers.NotSupported"/> if it is not supported at all.
        /// </returns>
        object IRawElementProviderSimple.GetPropertyValue(int propertyId)
        {
            if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
            {
                return true;
            }
            else if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
            {
                return this.GetType().ToString();
            }
            else if (propertyId == AutomationElementIdentifiers.LocalizedControlTypeProperty.Id ||
                     propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id)
            {
                return ControlType.Header.Id;
            }
            else if (propertyId == AutomationElementIdentifiers.NameProperty.Id)
            {
                return String.Join(";", _grid.Columns.OfType<DataGridViewColumn>().Where(o => o.Visible).OrderBy(o => o.DisplayIndex).Select(o => o.Name));
            }
            else if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
            {
                IRawElementProviderSimple provider = _grid as IRawElementProviderSimple;
                return ((bool)provider.GetPropertyValue(AutomationElementIdentifiers.IsEnabledProperty.Id));
            }

            return null;
        }

        /// <summary>
        /// Gets a base provider for this element.
        /// </summary>
        /// <value></value>
        /// <returns>The base provider, or null.</returns>
        IRawElementProviderSimple IRawElementProviderSimple.HostRawElementProvider
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a value that specifies characteristics of the UI Automation provider; for example, whether it is a client-side or server-side provider.
        /// </summary>
        /// <value></value>
        /// <returns>Either <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ClientSideProvider"/> or <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ServerSideProvider"/>.</returns>
        ProviderOptions IRawElementProviderSimple.ProviderOptions
        {
            get
            {
                return ProviderOptions.ServerSideProvider;
            }
        }

        #endregion
    }

    /// <summary>
    /// Class that represents ColumnHeaders for the purpose of Automation.
    /// </summary>
    internal class AutomationDataGridViewHeaderCell : IRawElementProviderFragment, IInvokeProvider
    {
        private AutomationDataGridView _grid;
        private DataGridViewHeaderCell _headerCell;
        private IRawElementProviderFragmentRoot _parent;

        public AutomationDataGridViewHeaderCell(AutomationDataGridView grid, IRawElementProviderFragmentRoot parent, DataGridViewHeaderCell headerCell)
        {
            if (grid == null)
                throw new ArgumentNullException("grid");

            if (headerCell == null)
                throw new ArgumentNullException("headerCell");

            if (parent == null)
                throw new ArgumentNullException("parent");

            _grid = grid;
            _headerCell = headerCell;
            _parent = parent;
        }

        #region IRawElementProviderSimple Members

        /// <summary>
        /// Retrieves an object that provides support for a control pattern on a UI Automation element.
        /// </summary>
        /// <param name="patternId">Identifier of the pattern.</param>
        /// <returns>
        /// Object that implements the pattern interface, or null if the pattern is not supported.
        /// </returns>
        object IRawElementProviderSimple.GetPatternProvider(int patternId)
        {
            if (patternId == InvokePatternIdentifiers.Pattern.Id)
                return this;
            return null;
        }

        /// <summary>
        /// Retrieves the value of a property supported by the UI Automation provider.
        /// </summary>
        /// <param name="propertyId">The property identifier.</param>
        /// <returns>
        /// The property value, or a null if the property is not supported by this provider, or <see cref="F:System.Windows.Automation.AutomationElementIdentifiers.NotSupported"/> if it is not supported at all.
        /// </returns>
        object IRawElementProviderSimple.GetPropertyValue(int propertyId)
        {
            if (propertyId == AutomationElementIdentifiers.ControlTypeProperty.Id ||
                propertyId == AutomationElementIdentifiers.LocalizedControlTypeProperty.Id)
            {
                return ControlType.HeaderItem.Id;
            }
            else if (propertyId == AutomationElementIdentifiers.AutomationIdProperty.Id)
            {
                return _headerCell.Value;
            }
            else if (propertyId == AutomationElementIdentifiers.NameProperty.Id)
            {
                return _headerCell.Value;
            }
            else if (propertyId == AutomationElementIdentifiers.ClassNameProperty.Id)
            {
                return this.GetType().ToString();
            }
            else if (propertyId == AutomationElementIdentifiers.IsEnabledProperty.Id)
            {
                return _headerCell.Visible;
            }
            else if (propertyId == AutomationElementIdentifiers.IsKeyboardFocusableProperty.Id)
            {
                return false;
            }

            return null;
        }

        /// <summary>
        /// Gets a base provider for this element.
        /// </summary>
        /// <value></value>
        /// <returns>The base provider, or null.</returns>
        IRawElementProviderSimple IRawElementProviderSimple.HostRawElementProvider
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a value that specifies characteristics of the UI Automation provider; for example, whether it is a client-side or server-side provider.
        /// </summary>
        /// <value></value>
        /// <returns>Either <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ClientSideProvider"/> or <see cref="F:System.Windows.Automation.Provider.ProviderOptions.ServerSideProvider"/>.</returns>
        ProviderOptions IRawElementProviderSimple.ProviderOptions
        {
            get
            {
                return ProviderOptions.ServerSideProvider;
            }
        }

        #endregion

        public System.Windows.Rect BoundingRectangle
        {
            get
            {
                Rectangle bounds = Rectangle.Empty;
                _grid.Invoke(new MethodInvoker(() =>
                {
                    if (_headerCell is DataGridViewColumnHeaderCell)
                    {
                        bounds = _grid.RectangleToScreen(_grid.GetColumnDisplayRectangle(_headerCell.ColumnIndex, true));
                        bounds.Height = _headerCell.Size.Height;
                    }
                    else
                    {
                        bounds = _grid.RectangleToScreen(_grid.GetRowDisplayRectangle(_headerCell.RowIndex, true));
                        bounds.Width = _headerCell.Size.Width;
                    }
                }));
                return new System.Windows.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        public IRawElementProviderFragmentRoot FragmentRoot
        {
            get { return _grid; }
        }

        public IRawElementProviderSimple[] GetEmbeddedFragmentRoots()
        {
            return null;
        }

        public int[] GetRuntimeId()
        {
            return new int[] { AutomationInteropProvider.AppendRuntimeId, _headerCell.GetHashCode() };
        }

        private IRawElementProviderFragment nextHeaderCell()
        {
            var columnHeaderCell = _headerCell as DataGridViewColumnHeaderCell;
            if (columnHeaderCell != null)
            {
                var nextColumnInDisplayOrder = _grid.Columns.OfType<DataGridViewColumn>()
                                                            .Where(o => o.Visible)
                                                            .OrderBy(o => o.DisplayIndex)
                                                            .SkipWhile(o => o.Index != columnHeaderCell.ColumnIndex)
                                                            .Skip(1)
                                                            .FirstOrDefault();
                if (nextColumnInDisplayOrder != null)
                    return new AutomationDataGridViewHeaderCell(_grid, _parent, nextColumnInDisplayOrder.HeaderCell);
            }
            else
            {
                var rowHeaderCell = _headerCell as DataGridViewRowHeaderCell;
                if (rowHeaderCell != null)
                {
                    for (int i = 0; i < _grid.ColumnCount; ++i)
                        if (_grid.Columns[i].Visible)
                            return new AutomationDataGridViewCell(_grid, _parent as AutomationDataGridViewRow, rowHeaderCell.OwningRow.Cells[i]);
                }
            }
            return null;
        }

        private IRawElementProviderFragment previousHeaderCell()
        {
            var columnHeaderCell = _headerCell as DataGridViewColumnHeaderCell;
            if (columnHeaderCell != null)
            {
                var previousColumnInDisplayOrder = _grid.Columns.OfType<DataGridViewColumn>()
                                                                .Where(o => o.Visible)
                                                                .OrderByDescending(o => o.DisplayIndex)
                                                                .SkipWhile(o => o.Index != columnHeaderCell.ColumnIndex)
                                                                .Skip(1)
                                                                .FirstOrDefault();
                if (previousColumnInDisplayOrder != null)
                    return new AutomationDataGridViewHeaderCell(_grid, _parent, previousColumnInDisplayOrder.HeaderCell);
            }
            // row header is the first cell, so return null
            return null;
        }

        public IRawElementProviderFragment Navigate(NavigateDirection direction)
        {
            switch (direction)
            {
                case NavigateDirection.NextSibling:
                    return nextHeaderCell();

                case NavigateDirection.PreviousSibling:
                    return previousHeaderCell();

                case NavigateDirection.Parent:
                    return _parent;
            }

            return null;
        }

        public void SetFocus()
        {
            _grid.Invoke(new MethodInvoker(() => _grid.Focus()));
        }

        #region IInvokeProvider Members

        public void Invoke()
        {
            _grid.Invoke(new MethodInvoker(() =>
            {
                _grid.Focus();

                Rectangle bounds;
                if (_headerCell is DataGridViewColumnHeaderCell)
                {
                    bounds = _grid.GetColumnDisplayRectangle(_headerCell.ColumnIndex, true);
                    bounds.Height = _headerCell.Size.Height;
                }
                else
                {
                    bounds = _grid.GetRowDisplayRectangle(_headerCell.RowIndex, true);
                    bounds.Width = _headerCell.Size.Width;
                }
                var centerPoint = bounds.GetCenterPoint();
                var e = new MouseEventArgs(MouseButtons.Left, 1, (int) centerPoint.X, (int) centerPoint.Y, 0);
                _grid.EmulateMouseClick(e);
            }));
        }

        #endregion
    }

    #endregion
}