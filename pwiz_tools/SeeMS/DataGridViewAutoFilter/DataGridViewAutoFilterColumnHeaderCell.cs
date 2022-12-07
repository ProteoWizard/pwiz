//---------------------------------------------------------------------
//  Copyright (C) Microsoft Corporation.  All rights reserved.
// 
//THIS CODE AND INFORMATION ARE PROVIDED AS IS WITHOUT WARRANTY OF ANY
//KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
//PARTICULAR PURPOSE.
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Collections;
using System.Reflection;

namespace DataGridViewAutoFilter
{
    /// <summary>
    /// Provides a drop-down filter list in a DataGridViewColumnHeaderCell.
    /// </summary>
    public class DataGridViewAutoFilterColumnHeaderCell : DataGridViewColumnHeaderCell
    {
        /// <summary>
        /// The ListBox used for all drop-down lists. 
        /// </summary>
        private static FilterListBox dropDownListBox = new FilterListBox();

        /// <summary>
        /// A list of filters available for the owning column stored as 
        /// formatted and unformatted string values. 
        /// </summary>
        private System.Collections.Specialized.OrderedDictionary filters =
            new System.Collections.Specialized.OrderedDictionary();

        /// <summary>
        /// The drop-down list filter value currently in effect for the owning column. 
        /// </summary>
        private String selectedFilterValue = String.Empty;

        /// <summary>
        /// The complete filter string currently in effect for the owning column. 
        /// </summary>
        private String currentColumnFilter = String.Empty;

        /// <summary>
        /// Indicates whether the DataGridView is currently filtered by the owning column.  
        /// </summary>
        private Boolean filtered;

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumnHeaderCell 
        /// class and sets its property values to the property values of the 
        /// specified DataGridViewColumnHeaderCell.
        /// </summary>
        /// <param name="oldHeaderCell">The DataGridViewColumnHeaderCell to copy property values from.</param>
        public DataGridViewAutoFilterColumnHeaderCell(DataGridViewColumnHeaderCell oldHeaderCell)
        {
            this.ContextMenuStrip = oldHeaderCell.ContextMenuStrip;
            this.ErrorText = oldHeaderCell.ErrorText;
            this.Tag = oldHeaderCell.Tag;
            this.ToolTipText = oldHeaderCell.ToolTipText;
            this.Value = oldHeaderCell.Value;
            this.ValueType = oldHeaderCell.ValueType;

            // Use HasStyle to avoid creating a new style object
            // when the Style property has not previously been set. 
            if (oldHeaderCell.HasStyle)
            {
                this.Style = oldHeaderCell.Style;
            }

            // Copy this type's properties if the old cell is an auto-filter cell. 
            // This enables the Clone method to reuse this constructor. 
            DataGridViewAutoFilterColumnHeaderCell filterCell =
                oldHeaderCell as DataGridViewAutoFilterColumnHeaderCell;
            if (filterCell != null)
            {
                this.FilteringEnabled = filterCell.FilteringEnabled;
                this.AutomaticSortingEnabled = filterCell.AutomaticSortingEnabled;
                this.DropDownListBoxMaxLines = filterCell.DropDownListBoxMaxLines;
                this.currentDropDownButtonPaddingOffset = 
                    filterCell.currentDropDownButtonPaddingOffset;
            }
        }

        /// <summary>
        /// Initializes a new instance of the DataGridViewColumnHeaderCell 
        /// class. 
        /// </summary>
        public DataGridViewAutoFilterColumnHeaderCell()
        {
        }

        /// <summary>
        /// Creates an exact copy of this cell.
        /// </summary>
        /// <returns>An object that represents the cloned DataGridViewAutoFilterColumnHeaderCell.</returns>
        public override object Clone()
        {
            return new DataGridViewAutoFilterColumnHeaderCell(this);
        }

        /// <summary>
        /// Called when the value of the DataGridView property changes
        /// in order to perform initialization that requires access to the 
        /// owning control and column. 
        /// </summary>
        protected override void OnDataGridViewChanged()
        {
            // Continue only if there is a DataGridView. 
            if (this.DataGridView == null)
            {
                return;
            }

            // Disable sorting and filtering for columns that can't make
            // effective use of them. 
            if (OwningColumn != null)
            {
                if (OwningColumn is DataGridViewImageColumn ||
                (OwningColumn is DataGridViewButtonColumn && 
                ((DataGridViewButtonColumn)OwningColumn).UseColumnTextForButtonValue) ||
                (OwningColumn is DataGridViewLinkColumn && 
                ((DataGridViewLinkColumn)OwningColumn).UseColumnTextForLinkValue))
                {
                    AutomaticSortingEnabled = false;
                    FilteringEnabled = false;
                }

                // Ensure that the column SortMode property value is not Automatic.
                // This prevents sorting when the user clicks the drop-down button.
                if (OwningColumn.SortMode == DataGridViewColumnSortMode.Automatic)
                {
                    OwningColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
                }
            }

            // Confirm that the data source meets requirements. 
            VerifyDataSource();

            // Add handlers to DataGridView events. 
            HandleDataGridViewEvents();

            // Initialize the drop-down button bounds so that any initial
            // column autosizing will accommodate the button width. 
            SetDropDownButtonBounds();

            // Call the OnDataGridViewChanged method on the base class to 
            // raise the DataGridViewChanged event.
            base.OnDataGridViewChanged();
        }

        /// <summary>
        /// Confirms that the data source, if it has been set, is a BindingSource.
        /// </summary>
        private void VerifyDataSource()
        {
            // Continue only if there is a DataGridView and its DataSource has been set.
            if (this.DataGridView == null || this.DataGridView.DataSource == null)
            {
                return;
            }

            // Throw an exception if the data source is not a BindingSource. 
            BindingSource data = this.DataGridView.DataSource as BindingSource;
            if (data == null)
            {
                throw new NotSupportedException(
                    "The DataSource property of the containing DataGridView control " +
                    "must be set to a BindingSource.");
            }
        }

        #region DataGridView events: HandleDataGridViewEvents, DataGridView event handlers, ResetDropDown, ResetFilter

        /// <summary>
        /// Add handlers to various DataGridView events, primarily to invalidate 
        /// the drop-down button bounds, hide the drop-down list, and reset 
        /// cached filter values when changes in the DataGridView require it.
        /// </summary>
        private void HandleDataGridViewEvents()
        {
            this.DataGridView.Scroll += new ScrollEventHandler(DataGridView_Scroll);
            this.DataGridView.ColumnDisplayIndexChanged += new DataGridViewColumnEventHandler(DataGridView_ColumnDisplayIndexChanged);
            this.DataGridView.ColumnWidthChanged += new DataGridViewColumnEventHandler(DataGridView_ColumnWidthChanged);
            this.DataGridView.ColumnHeadersHeightChanged += new EventHandler(DataGridView_ColumnHeadersHeightChanged);
            this.DataGridView.SizeChanged += new EventHandler(DataGridView_SizeChanged);
            this.DataGridView.DataSourceChanged += new EventHandler(DataGridView_DataSourceChanged);
            this.DataGridView.DataBindingComplete += new DataGridViewBindingCompleteEventHandler(DataGridView_DataBindingComplete);

            // Add a handler for the ColumnSortModeChanged event to prevent the
            // column SortMode property from being inadvertently set to Automatic.
            this.DataGridView.ColumnSortModeChanged += new DataGridViewColumnEventHandler(DataGridView_ColumnSortModeChanged);
        }

        /// <summary>
        /// Invalidates the drop-down button bounds when the user scrolls horizontally.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A ScrollEventArgs that contains the event data.</param>
        private void DataGridView_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                ResetDropDown();
            }
        }

        /// <summary>
        /// Invalidates the drop-down button bounds when the column display index changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridView_ColumnDisplayIndexChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResetDropDown();
        }

        /// <summary>
        /// Invalidates the drop-down button bounds when a column width changes
        /// in the DataGridView control. A width change in any column of the 
        /// control has the potential to affect the drop-down button location, 
        /// depending on the current horizontal scrolling position and whether
        /// the changed column is to the left or right of the current column. 
        /// It is easier to invalidate the button in all cases. 
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A DataGridViewColumnEventArgs that contains the event data.</param>
        private void DataGridView_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResetDropDown();
        }

        /// <summary>
        /// Invalidates the drop-down button bounds when the height of the column headers changes.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">An EventArgs that contains the event data.</param>
        private void DataGridView_ColumnHeadersHeightChanged(object sender, EventArgs e)
        {
            ResetDropDown();
        }

        /// <summary>
        /// Invalidates the drop-down button bounds when the size of the DataGridView changes.
        /// This prevents a painting issue that occurs when the right edge of the control moves 
        /// to the right and the control contents have previously been scrolled to the right.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">An EventArgs that contains the event data.</param>
        private void DataGridView_SizeChanged(object sender, EventArgs e)
        {
            ResetDropDown();
        }

        /// <summary>
        /// Invalidates the drop-down button bounds, hides the drop-down 
        /// filter list, if it is showing, and resets the cached filter values
        /// if the filter has been removed. 
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A DataGridViewBindingCompleteEventArgs that contains the event data.</param>
        private void DataGridView_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.Reset)
            {
                ResetDropDown();
                ResetFilter();
            }
        }

        /// <summary>
        /// Verifies that the data source meets requirements, invalidates the 
        /// drop-down button bounds, hides the drop-down filter list if it is 
        /// showing, and resets the cached filter values if the filter has been removed. 
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">An EventArgs that contains the event data.</param>
        private void DataGridView_DataSourceChanged(object sender, EventArgs e)
        {
            VerifyDataSource();
            ResetDropDown();
            ResetFilter();
        }

        /// <summary>
        /// Invalidates the drop-down button bounds and hides the filter
        /// list if it is showing.
        /// </summary>
        private void ResetDropDown()
        {
            InvalidateDropDownButtonBounds();
            if (dropDownListBoxShowing)
            {
                HideDropDownList();
            }
        }

        /// <summary>
        /// Resets the cached filter values if the filter has been removed.
        /// </summary>
        private void ResetFilter()
        {
            if (this.DataGridView == null) return;
            BindingSource source = this.DataGridView.DataSource as BindingSource;
            if (source == null || String.IsNullOrEmpty(source.Filter))
            {
                filtered = false;
                selectedFilterValue = "(All)";
                currentColumnFilter = String.Empty;
            }
        }

        /// <summary>
        /// Throws an exception when the column sort mode is changed to Automatic.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A DataGridViewColumnEventArgs that contains the event data.</param>
        private void DataGridView_ColumnSortModeChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column == OwningColumn &&
                e.Column.SortMode == DataGridViewColumnSortMode.Automatic)
            {
                throw new InvalidOperationException(
                    "A SortMode value of Automatic is incompatible with " +
                    "the DataGridViewAutoFilterColumnHeaderCell type. " +
                    "Use the AutomaticSortingEnabled property instead.");
            }
        }

        #endregion DataGridView events

        /// <summary>
        /// Paints the column header cell, including the drop-down button. 
        /// </summary>
        /// <param name="graphics">The Graphics used to paint the DataGridViewCell.</param>
        /// <param name="clipBounds">A Rectangle that represents the area of the DataGridView that needs to be repainted.</param>
        /// <param name="cellBounds">A Rectangle that contains the bounds of the DataGridViewCell that is being painted.</param>
        /// <param name="rowIndex">The row index of the cell that is being painted.</param>
        /// <param name="cellState">A bitwise combination of DataGridViewElementStates values that specifies the state of the cell.</param>
        /// <param name="value">The data of the DataGridViewCell that is being painted.</param>
        /// <param name="formattedValue">The formatted data of the DataGridViewCell that is being painted.</param>
        /// <param name="errorText">An error message that is associated with the cell.</param>
        /// <param name="cellStyle">A DataGridViewCellStyle that contains formatting and style information about the cell.</param>
        /// <param name="advancedBorderStyle">A DataGridViewAdvancedBorderStyle that contains border styles for the cell that is being painted.</param>
        /// <param name="paintParts">A bitwise combination of the DataGridViewPaintParts values that specifies which parts of the cell need to be painted.</param>
        protected override void Paint(
            Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, 
            int rowIndex, DataGridViewElementStates cellState, 
            object value, object formattedValue, string errorText, 
            DataGridViewCellStyle cellStyle, 
            DataGridViewAdvancedBorderStyle advancedBorderStyle, 
            DataGridViewPaintParts paintParts)
        {
            // Use the base method to paint the default appearance. 
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, 
                cellState, value, formattedValue, 
                errorText, cellStyle, advancedBorderStyle, paintParts);

            // Continue only if filtering is enabled and ContentBackground is 
            // part of the paint request. 
            if (!FilteringEnabled || 
                (paintParts & DataGridViewPaintParts.ContentBackground) == 0)
            {
                return;
            }

            // Retrieve the current button bounds. 
            Rectangle buttonBounds = DropDownButtonBounds;

            // Continue only if the buttonBounds is big enough to draw.
            if (buttonBounds.Width < 1 || buttonBounds.Height < 1) return;

            // Paint the button manually or using visual styles if visual styles 
            // are enabled, using the correct state depending on whether the 
            // filter list is showing and whether there is a filter in effect 
            // for the current column. 
            if (Application.RenderWithVisualStyles)
            {
                ComboBoxState state = ComboBoxState.Normal;

                if (dropDownListBoxShowing)
                {
                    state = ComboBoxState.Pressed;
                }
                else if (filtered)
                {
                    state = ComboBoxState.Hot;
                }
                ComboBoxRenderer.DrawDropDownButton(
                    graphics, buttonBounds, state);
            }
            else
            {
                // Determine the pressed state in order to paint the button 
                // correctly and to offset the down arrow. 
                Int32 pressedOffset = 0;
                PushButtonState state = PushButtonState.Normal;
                if (dropDownListBoxShowing)
                {
                    state = PushButtonState.Pressed;
                    pressedOffset = 1;
                }
                ButtonRenderer.DrawButton(graphics, buttonBounds, state);

                // If there is a filter in effect for the column, paint the 
                // down arrow as an unfilled triangle. If there is no filter 
                // in effect, paint the down arrow as a filled triangle.
                if (filtered)
                {
                    graphics.DrawPolygon(SystemPens.ControlText, new Point[] {
                        new Point(
                            buttonBounds.Width / 2 + 
                                buttonBounds.Left - 1 + pressedOffset, 
                            buttonBounds.Height * 3 / 4 + 
                                buttonBounds.Top - 1 + pressedOffset),
                        new Point(
                            buttonBounds.Width / 4 + 
                                buttonBounds.Left + pressedOffset,
                            buttonBounds.Height / 2 + 
                                buttonBounds.Top - 1 + pressedOffset),
                        new Point(
                            buttonBounds.Width * 3 / 4 + 
                                buttonBounds.Left - 1 + pressedOffset,
                            buttonBounds.Height / 2 + 
                                buttonBounds.Top - 1 + pressedOffset)
                    });
                }
                else
                {
                    graphics.FillPolygon(SystemBrushes.ControlText, new Point[] {
                        new Point(
                            buttonBounds.Width / 2 + 
                                buttonBounds.Left - 1 + pressedOffset, 
                            buttonBounds.Height * 3 / 4 + 
                                buttonBounds.Top - 1 + pressedOffset),
                        new Point(
                            buttonBounds.Width / 4 + 
                                buttonBounds.Left + pressedOffset,
                            buttonBounds.Height / 2 + 
                                buttonBounds.Top - 1 + pressedOffset),
                        new Point(
                            buttonBounds.Width * 3 / 4 + 
                                buttonBounds.Left - 1 + pressedOffset,
                            buttonBounds.Height / 2 + 
                                buttonBounds.Top - 1 + pressedOffset)
                    });
                }
            }

        }

        /// <summary>
        /// Handles mouse clicks to the header cell, displaying the 
        /// drop-down list or sorting the owning column as appropriate. 
        /// </summary>
        /// <param name="e">A DataGridViewCellMouseEventArgs that contains the event data.</param>
        protected override void OnMouseDown(DataGridViewCellMouseEventArgs e)
        {
            Debug.Assert(this.DataGridView != null, "DataGridView is null");

            // Continue only if the user did not click the drop-down button 
            // while the drop-down list was displayed. This prevents the 
            // drop-down list from being redisplayed after being hidden in 
            // the LostFocus event handler. 
            if (lostFocusOnDropDownButtonClick)
            {
                lostFocusOnDropDownButtonClick = false;
                return;
            }

            // Retrieve the current size and location of the header cell,
            // excluding any portion that is scrolled off screen. 
            Rectangle cellBounds = this.DataGridView
                .GetCellDisplayRectangle(e.ColumnIndex, -1, false);

            // Continue only if the column is not manually resizable or the
            // mouse coordinates are not within the column resize zone. 
            if (this.OwningColumn.Resizable == DataGridViewTriState.True &&
                ((this.DataGridView.RightToLeft == RightToLeft.No &&
                cellBounds.Width - e.X < 6) || e.X < 6))
            {
                return;
            }

            // Unless RightToLeft is enabled, store the width of the portion
            // that is scrolled off screen. 
            Int32 scrollingOffset = 0;
            if (this.DataGridView.RightToLeft == RightToLeft.No &&
                this.DataGridView.FirstDisplayedScrollingColumnIndex ==
                this.ColumnIndex)
            {
                scrollingOffset =
                    this.DataGridView.FirstDisplayedScrollingColumnHiddenWidth;
            }

            // Show the drop-down list if filtering is enabled and the mouse click occurred
            // within the drop-down button bounds. Otherwise, if sorting is enabled and the
            // click occurred outside the drop-down button bounds, sort by the owning column. 
            // The mouse coordinates are relative to the cell bounds, so the cell location
            // and the scrolling offset are needed to determine the client coordinates.
            if (FilteringEnabled &&
                DropDownButtonBounds.Contains(
                e.X + cellBounds.Left - scrollingOffset, e.Y + cellBounds.Top))
            {
                // If the current cell is in edit mode, commit the edit. 
                if (this.DataGridView.IsCurrentCellInEditMode)
                {
                    // Commit and end the cell edit.  
                    this.DataGridView.EndEdit();

                    // Commit any change to the underlying data source. 
                    BindingSource source =
                        this.DataGridView.DataSource as BindingSource;
                    if (source != null)
                    {
                        source.EndEdit();
                    }
                }
                ShowDropDownList();
            }
            else if (AutomaticSortingEnabled &&
                this.DataGridView.SelectionMode != 
                DataGridViewSelectionMode.ColumnHeaderSelect)
            {
                SortByColumn();
            }

            base.OnMouseDown(e);
        }

        /// <summary>
        /// Sorts the DataGridView by the current column if AutomaticSortingEnabled is true.
        /// </summary>
        private void SortByColumn()
        {
            Debug.Assert(this.DataGridView != null && OwningColumn != null, "DataGridView or OwningColumn is null");

            // Continue only if the data source supports sorting. 
            IBindingList sortList = this.DataGridView.DataSource as IBindingList;
            if (sortList == null ||
                !sortList.SupportsSorting ||
                !AutomaticSortingEnabled)
            {
                return;
            }

            // Determine the sort direction and sort by the owning column. 
            ListSortDirection direction = ListSortDirection.Ascending;
            if (this.DataGridView.SortedColumn == OwningColumn && 
                this.DataGridView.SortOrder == SortOrder.Ascending)
            {
                direction = ListSortDirection.Descending;
            }
            this.DataGridView.Sort(OwningColumn, direction);
        }

        #region drop-down list: Show/HideDropDownListBox, SetDropDownListBoxBounds, DropDownListBoxMaxHeightInternal

        /// <summary>
        /// Indicates whether dropDownListBox is currently displayed 
        /// for this header cell. 
        /// </summary>
        private bool dropDownListBoxShowing;

        /// <summary>
        /// Displays the drop-down filter list. 
        /// </summary>
        public void ShowDropDownList()
        {
            Debug.Assert(this.DataGridView != null, "DataGridView is null");

            // Ensure that the current row is not the row for new records.
            // This prevents the new row from affecting the filter list and also
            // prevents the new row from being added when the filter changes.
            if (this.DataGridView.CurrentRow != null &&
                this.DataGridView.CurrentRow.IsNewRow)
            {
                this.DataGridView.CurrentCell = null;
            }

            // Populate the filters dictionary, then copy the filter values 
            // from the filters.Keys collection into the ListBox.Items collection, 
            // selecting the current filter if there is one in effect. 
            PopulateFilters();

            String[] filterArray = new String[filters.Count];
            filters.Keys.CopyTo(filterArray, 0);
            dropDownListBox.Items.Clear();
            dropDownListBox.Items.AddRange(filterArray);
            dropDownListBox.SelectedItem = selectedFilterValue;

            // Add handlers to dropDownListBox events. 
            HandleDropDownListBoxEvents();

            // Set the size and location of dropDownListBox, then display it. 
            SetDropDownListBoxBounds();
            dropDownListBox.Visible = true;
            dropDownListBoxShowing = true;

            Debug.Assert(dropDownListBox.Parent == null, 
                "ShowDropDownListBox has been called multiple times before HideDropDownListBox");

            // Add dropDownListBox to the DataGridView. 
            this.DataGridView.Controls.Add(dropDownListBox);

            // Set the input focus to dropDownListBox. 
            dropDownListBox.Focus();
         
            // Invalidate the cell so that the drop-down button will repaint
            // in the pressed state. 
            this.DataGridView.InvalidateCell(this);
        }

        /// <summary>
        /// Hides the drop-down filter list. 
        /// </summary>
        public void HideDropDownList()
        {
            Debug.Assert(this.DataGridView != null, "DataGridView is null");

            // Hide dropDownListBox, remove handlers from its events, and remove 
            // it from the DataGridView control. 
            dropDownListBoxShowing = false;
            dropDownListBox.Visible = false;
            UnhandleDropDownListBoxEvents();
            this.DataGridView.Controls.Remove(dropDownListBox);

            // Invalidate the cell so that the drop-down button will repaint
            // in the unpressed state. 
            this.DataGridView.InvalidateCell(this);
        }

        /// <summary>
        /// Sets the dropDownListBox size and position based on the formatted 
        /// values in the filters dictionary and the position of the drop-down 
        /// button. Called only by ShowDropDownListBox.  
        /// </summary>
        private void SetDropDownListBoxBounds()
        {
            Debug.Assert(filters.Count > 0, "filters.Count <= 0");

            // Declare variables that will be used in the calculation, 
            // initializing dropDownListBoxHeight to account for the 
            // ListBox borders.
            Int32 dropDownListBoxHeight = 2;
            Int32 currentWidth = 0;
            Int32 dropDownListBoxWidth = 0;
            Int32 dropDownListBoxLeft = 0;

            // For each formatted value in the filters dictionary Keys collection,
            // add its height to dropDownListBoxHeight and, if it is wider than 
            // all previous values, set dropDownListBoxWidth to its width.
            using (Graphics graphics = dropDownListBox.CreateGraphics())
            {
                foreach (String filter in filters.Keys)
                {
                    SizeF stringSizeF = graphics.MeasureString(
                        filter, dropDownListBox.Font);
                    dropDownListBoxHeight += (Int32)stringSizeF.Height;
                    currentWidth = (Int32)stringSizeF.Width;
                    if (dropDownListBoxWidth < currentWidth)
                    {
                        dropDownListBoxWidth = currentWidth;
                    }
                }
            }

            // Increase the width to allow for horizontal margins and borders. 
            dropDownListBoxWidth += 6;

            // Constrain the dropDownListBox height to the 
            // DropDownListBoxMaxHeightInternal value, which is based on 
            // the DropDownListBoxMaxLines property value but constrained by
            // the maximum height available in the DataGridView control.
            if (dropDownListBoxHeight > DropDownListBoxMaxHeightInternal)
            {
                dropDownListBoxHeight = DropDownListBoxMaxHeightInternal;

                // If the preferred height is greater than the available height,
                // adjust the width to accommodate the vertical scroll bar. 
                dropDownListBoxWidth += SystemInformation.VerticalScrollBarWidth;
            }

            // Calculate the ideal location of the left edge of dropDownListBox 
            // based on the location of the drop-down button and taking the 
            // RightToLeft property value into consideration. 
            if (this.DataGridView.RightToLeft == RightToLeft.No)
            {
                dropDownListBoxLeft = DropDownButtonBounds.Right - 
                    dropDownListBoxWidth + 1;
            }
            else
            {
                dropDownListBoxLeft = DropDownButtonBounds.Left - 1;
            }

            // Determine the left and right edges of the available horizontal
            // width of the DataGridView control. 
            Int32 clientLeft = 1;
            Int32 clientRight = this.DataGridView.ClientRectangle.Right;
            if (this.DataGridView.DisplayedRowCount(false) < 
                this.DataGridView.RowCount)
            {
                if (this.DataGridView.RightToLeft == RightToLeft.Yes)
                {
                    clientLeft += SystemInformation.VerticalScrollBarWidth;
                }
                else
                {
                    clientRight -= SystemInformation.VerticalScrollBarWidth;
                }
            }

            // Adjust the dropDownListBox location and/or width if it would
            // otherwise overlap the left or right edge of the DataGridView.
            if (dropDownListBoxLeft < clientLeft)
            {
                dropDownListBoxLeft = clientLeft;
            }
            Int32 dropDownListBoxRight = 
                dropDownListBoxLeft + dropDownListBoxWidth + 1;
            if (dropDownListBoxRight > clientRight)
            {
                if (dropDownListBoxLeft == clientLeft)
                {
                    dropDownListBoxWidth -=
                        dropDownListBoxRight - clientRight;
                }
                else
                {
                    dropDownListBoxLeft -=
                        dropDownListBoxRight - clientRight;
                    if (dropDownListBoxLeft < clientLeft)
                    {
                        dropDownListBoxWidth -= clientLeft - dropDownListBoxLeft;
                        dropDownListBoxLeft = clientLeft;
                    }
                }
            }

            // Set the ListBox.Bounds property using the calculated values. 
            dropDownListBox.Bounds = new Rectangle(dropDownListBoxLeft,
                DropDownButtonBounds.Bottom, // top of drop-down list box
                dropDownListBoxWidth, dropDownListBoxHeight);
        }

        /// <summary>
        /// Gets the actual maximum height of the drop-down list, in pixels.
        /// The maximum height is calculated from the DropDownListBoxMaxLines 
        /// property value, but is limited to the available height of the 
        /// DataGridView control. 
        /// </summary>
        protected Int32 DropDownListBoxMaxHeightInternal
        {
            get
            {
                // Calculate the height of the available client area
                // in the DataGridView control, taking the horizontal
                // scroll bar into consideration and leaving room
                // for the ListBox bottom border. 
                Int32 dataGridViewMaxHeight = this.DataGridView.Height -
                    this.DataGridView.ColumnHeadersHeight - 1;
                if (this.DataGridView.DisplayedColumnCount(false) <
                    this.DataGridView.ColumnCount)
                {
                    dataGridViewMaxHeight -=
                        SystemInformation.HorizontalScrollBarHeight;
                }

                // Calculate the height of the list box, using the combined 
                // height of all items plus 2 for the top and bottom border. 
                Int32 listMaxHeight = dropDownListBoxMaxLinesValue * dropDownListBox.ItemHeight + 2;

                // Return the smaller of the two values. 
                if (listMaxHeight < dataGridViewMaxHeight)
                {
                    return listMaxHeight;
                }
                else
                {
                    return dataGridViewMaxHeight;
                }
            }
        }

        #endregion drop-down list

        #region ListBox events: HandleDropDownListBoxEvents, UnhandleDropDownListBoxEvents, ListBox event handlers

        /// <summary>
        /// Adds handlers to ListBox events for handling mouse
        /// and keyboard input.
        /// </summary>
        private void HandleDropDownListBoxEvents()
        {
            dropDownListBox.MouseClick += new MouseEventHandler(DropDownListBox_MouseClick);
            dropDownListBox.LostFocus += new EventHandler(DropDownListBox_LostFocus);
            dropDownListBox.KeyDown += new KeyEventHandler(DropDownListBox_KeyDown);
        }

        /// <summary>
        /// Removes the ListBox event handlers. 
        /// </summary>
        private void UnhandleDropDownListBoxEvents()
        {
            dropDownListBox.MouseClick -= new MouseEventHandler(DropDownListBox_MouseClick);
            dropDownListBox.LostFocus -= new EventHandler(DropDownListBox_LostFocus);
            dropDownListBox.KeyDown -= new KeyEventHandler(DropDownListBox_KeyDown);
        }

        /// <summary>
        /// Adjusts the filter in response to a user selection from the drop-down list. 
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        private void DropDownListBox_MouseClick(object sender, MouseEventArgs e)
        {
            Debug.Assert(this.DataGridView != null, "DataGridView is null");

            // Continue only if the mouse click was in the content area
            // and not on the scroll bar. 
            if (!dropDownListBox.DisplayRectangle.Contains(e.X, e.Y))
            {
                return;
            }

            UpdateFilter();
            HideDropDownList();
        }

        /// <summary>
        /// Indicates whether the drop-down list lost focus because the
        /// user clicked the drop-down button. 
        /// </summary>
        private Boolean lostFocusOnDropDownButtonClick;

        /// <summary>
        /// Hides the drop-down list when it loses focus. 
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">An EventArgs that contains the event data.</param>
        private void DropDownListBox_LostFocus(object sender, EventArgs e)
        {
            // If the focus was lost because the user clicked the drop-down
            // button, store a value that prevents the subsequent OnMouseDown
            // call from displaying the drop-down list again. 
            if (DropDownButtonBounds.Contains(
                this.DataGridView.PointToClient(new Point(
                Control.MousePosition.X, Control.MousePosition.Y))))
            {
                lostFocusOnDropDownButtonClick = true;
            }
            HideDropDownList();
        }

        /// <summary>
        /// Handles the ENTER and ESC keys.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">A KeyEventArgs that contains the event data.</param>
        void DropDownListBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    UpdateFilter();
                    HideDropDownList();
                    break;
                case Keys.Escape:
                    HideDropDownList();
                    break;
            }
        }

        #endregion ListBox events

        #region filtering: PopulateFilters, FilterWithoutCurrentColumn, UpdateFilter, RemoveFilter, AvoidNewRowWhenFiltering, GetFilterStatus

        /// <summary>
        /// Populates the filters dictionary with formatted and unformatted string
        /// representations of each unique value in the column, accounting for all 
        /// filters except the current column's. Also adds special filter options. 
        /// </summary>
        private void PopulateFilters()
        {
            // Continue only if there is a DataGridView.
            if (this.DataGridView == null)
            {
                return;
            }

            // Cast the data source to a BindingSource. 
            BindingSource data = this.DataGridView.DataSource as BindingSource;

            Debug.Assert(data != null && data.SupportsFiltering && OwningColumn != null,
                "DataSource is not a BindingSource, or does not support filtering, or OwningColumn is null");

            // Prevent the data source from notifying the DataGridView of changes. 
            data.RaiseListChangedEvents = false;

            // Cache the current BindingSource.Filter value and then change 
            // the Filter property to temporarily remove any filter for the 
            // current column. 
            String oldFilter = data.Filter;
            data.Filter = FilterWithoutCurrentColumn(oldFilter);

            // Reset the filters dictionary and initialize some flags
            // to track whether special filter options are needed. 
            filters.Clear();
            Boolean containsBlanks = false;
            Boolean containsNonBlanks = false;

            // Initialize an ArrayList to store the values in their original
            // types. This enables the values to be sorted appropriately.  
            ArrayList list = new ArrayList(data.Count);

            // Retrieve each value and add it to the ArrayList if it isn't
            // already present. 
            foreach (Object item in data)
            {
                Object value = null;

                // Use the ICustomTypeDescriptor interface to retrieve properties
                // if it is available; otherwise, use reflection. The 
                // ICustomTypeDescriptor interface is useful to customize 
                // which values are exposed as properties. For example, the 
                // DataRowView class implements ICustomTypeDescriptor to expose 
                // cell values as property values.                
                // 
                // Iterate through the property names to find a case-insensitive
                // match with the DataGridViewColumn.DataPropertyName value.
                // This is necessary because DataPropertyName is case-
                // insensitive, but the GetProperties and GetProperty methods
                // used below are case-sensitive.
                ICustomTypeDescriptor ictd = item as ICustomTypeDescriptor;
                if (ictd != null)
                {
                    PropertyDescriptorCollection properties = ictd.GetProperties();
                    foreach (PropertyDescriptor property in properties)
                    {
                        if (String.Compare(this.OwningColumn.DataPropertyName,
                            property.Name, true /*case insensitive*/,
                            System.Globalization.CultureInfo.InvariantCulture) == 0)
                        {
                            value = property.GetValue(item);
                            break;
                        }
                    }
                }
                else
                {
                    PropertyInfo[] properties = item.GetType().GetProperties(
                        BindingFlags.Public | BindingFlags.Instance);
                    foreach (PropertyInfo property in properties)
                    {
                        if (String.Compare(this.OwningColumn.DataPropertyName,
                            property.Name, true /*case insensitive*/,
                            System.Globalization.CultureInfo.InvariantCulture) == 0)
                        {
                            value = property.GetValue(item, null /*property index*/);
                            break;
                        }
                    }
                }

                // Skip empty values, but note that they are present. 
                if (value == null || value == DBNull.Value)
                {
                    containsBlanks = true;
                    continue;
                }

                // Add values to the ArrayList if they are not already there.
                if (!list.Contains(value))
                {
                    list.Add(value);
                }
            }

            // Sort the ArrayList. The default Sort method uses the IComparable 
            // implementation of the stored values so that string, numeric, and 
            // date values will all be sorted correctly. 
            list.Sort();

            // Convert each value in the ArrayList to its formatted representation
            // and store both the formatted and unformatted string representations
            // in the filters dictionary. 
            foreach (Object value in list)
            {
                // Use the cell's GetFormattedValue method with the column's
                // InheritedStyle property so that the dropDownListBox format
                // will match the display format used for the column's cells. 
                String formattedValue = null;
                DataGridViewCellStyle style = OwningColumn.InheritedStyle;
                formattedValue = (String)GetFormattedValue(value, -1, ref style, 
                    null, null, DataGridViewDataErrorContexts.Formatting);

                if (String.IsNullOrEmpty(formattedValue))
                {
                    // Skip empty values, but note that they are present.
                    containsBlanks = true;
                }
                else if (!filters.Contains(formattedValue))
                {
                    // Note whether non-empty values are present. 
                    containsNonBlanks = true;

                    // For all non-empty values, add the formatted and 
                    // unformatted string representations to the filters 
                    // dictionary.
                    filters.Add(formattedValue, value.ToString());
                }
            }

            // Restore the filter to the cached filter string and 
            // re-enable data source change notifications. 
            if (oldFilter != null) data.Filter = oldFilter;
            data.RaiseListChangedEvents = true;

            // Add special filter options to the filters dictionary
            // along with null values, since unformatted representations
            // are not needed. 
            filters.Insert(0, "(All)", null);
            if (containsBlanks && containsNonBlanks)
            {
                filters.Add("(Blanks)", null);
                filters.Add("(NonBlanks)", null);
            }
        }

        /// <summary>
        /// Returns a copy of the specified filter string after removing the part that filters the current column, if present. 
        /// </summary>
        /// <param name="filter">The filter string to parse.</param>
        /// <returns>A copy of the specified filter string without the current column's filter.</returns>
        private String FilterWithoutCurrentColumn(String filter)
        {
            // If there is no filter in effect, return String.Empty. 
            if (String.IsNullOrEmpty(filter))
            {
                return String.Empty;
            }

            // If the column is not filtered, return the filter string unchanged. 
            if (!filtered)
            {
                return filter;
            }

            if (filter.IndexOf(currentColumnFilter) > 0)
            {
                // If the current column filter is not the first filter, return
                // the specified filter value without the current column filter 
                // and without the preceding " AND ". 
                return filter.Replace(
                    " AND " + currentColumnFilter, String.Empty);
            }
            else
            {
                if (filter.Length > currentColumnFilter.Length)
                {
                    // If the current column filter is the first of multiple 
                    // filters, return the specified filter value without the 
                    // current column filter and without the subsequent " AND ". 
                    return filter.Replace(
                        currentColumnFilter + " AND ", String.Empty);
                }
                else
                {
                    // If the current column filter is the only filter, 
                    // return the empty string.
                    return String.Empty;
                }
            }
        }

        /// <summary>
        /// Updates the BindingSource.Filter value based on a user selection
        /// from the drop-down filter list. 
        /// </summary>
        private void UpdateFilter()
        {
            // Continue only if the selection has changed.
            if (dropDownListBox.SelectedItem.ToString().Equals(selectedFilterValue))
            {
                return;
            }

            // Store the new selection value. 
            selectedFilterValue = dropDownListBox.SelectedItem.ToString();

            // Cast the data source to an IBindingListView.
            IBindingListView data = 
                this.DataGridView.DataSource as IBindingListView;

            Debug.Assert(data != null && data.SupportsFiltering,
                "DataSource is not an IBindingListView or does not support filtering");

            // If the user selection is (All), remove any filter currently 
            // in effect for the column. 
            if (selectedFilterValue.Equals("(All)"))
            {
                data.Filter = FilterWithoutCurrentColumn(data.Filter);
                filtered = false;
                currentColumnFilter = String.Empty;
                return;
            }

            // Declare a variable to store the filter string for this column.
            String newColumnFilter = null;

            // Store the column name in a form acceptable to the Filter property, 
            // using a backslash to escape any closing square brackets. 
            String columnProperty = 
                OwningColumn.DataPropertyName.Replace("]", @"\]");

            // Determine the column filter string based on the user selection.
            // For (Blanks) and (NonBlanks), the filter string determines whether
            // the column value is null or an empty string. Otherwise, the filter
            // string determines whether the column value is the selected value. 
            switch (selectedFilterValue)
            {
                case "(Blanks)":
                    newColumnFilter = String.Format(
                        "LEN(ISNULL(CONVERT([{0}],'System.String'),''))=0",
                        columnProperty);
                    break;
                case "(NonBlanks)":
                    newColumnFilter = String.Format(
                        "LEN(ISNULL(CONVERT([{0}],'System.String'),''))>0",
                        columnProperty);
                    break;
                default:
                    newColumnFilter = String.Format("[{0}]='{1}'",
                        columnProperty,
                        ((String)filters[selectedFilterValue])
                        .Replace("'", "''"));  
                    break;
            }

            // Determine the new filter string by removing the previous column 
            // filter string from the BindingSource.Filter value, then appending 
            // the new column filter string, using " AND " as appropriate. 
            String newFilter = FilterWithoutCurrentColumn(data.Filter);
            if (String.IsNullOrEmpty(newFilter))
            {
                newFilter += newColumnFilter;
            }
            else
            {
                newFilter += " AND " + newColumnFilter;
            }


            // Set the filter to the new value.
            try
            {
                data.Filter = newFilter;
            }
            catch (InvalidExpressionException ex)
            {
                throw new NotSupportedException(
                    "Invalid expression: " + newFilter, ex);
            }

            // Indicate that the column is currently filtered
            // and store the new column filter for use by subsequent
            // calls to the FilterWithoutCurrentColumn method. 
            filtered = true;
            currentColumnFilter = newColumnFilter;
        }

        /// <summary>
        /// Removes the filter from the BindingSource bound to the specified DataGridView. 
        /// </summary>
        /// <param name="dataGridView">The DataGridView bound to the BindingSource to unfilter.</param>
        public static void RemoveFilter(DataGridView dataGridView)
        {
            if (dataGridView == null)
            {
                throw new ArgumentNullException("dataGridView");
            }

            // Cast the data source to a BindingSource.
            BindingSource data = dataGridView.DataSource as BindingSource;

            // Confirm that the data source is a BindingSource that 
            // supports filtering.
            if (data == null || 
                data.DataSource == null || 
                !data.SupportsFiltering)
            {
                throw new ArgumentException("The DataSource property of the " +
                    "specified DataGridView is not set to a BindingSource " +
                    "with a SupportsFiltering property value of true.");
            }

            // Ensure that the current row is not the row for new records.
            // This prevents the new row from being added when the filter changes.
            if (dataGridView.CurrentRow != null && dataGridView.CurrentRow.IsNewRow)
            {
                dataGridView.CurrentCell = null;
            }

            // Remove the filter. 
            data.Filter = null;
        }

        /// <summary>
        /// Gets a status string for the specified DataGridView indicating the 
        /// number of visible rows in the bound, filtered BindingSource, or 
        /// String.Empty if all rows are currently visible. 
        /// </summary>
        /// <param name="dataGridView">The DataGridView bound to the 
        /// BindingSource to return the filter status for.</param>
        /// <returns>A string in the format "x of y records found" where x is 
        /// the number of rows currently displayed and y is the number of rows 
        /// available, or String.Empty if all rows are currently displayed.</returns>
        public static String GetFilterStatus(DataGridView dataGridView)
        {
            // Continue only if the specified value is valid. 
            if (dataGridView == null)
            {
                throw new ArgumentNullException("dataGridView");
            }

            // Cast the data source to a BindingSource.
            BindingSource data = dataGridView.DataSource as BindingSource;

            // Return String.Empty if there is no appropriate data source or
            // there is no filter in effect. 
            if (String.IsNullOrEmpty(data.Filter) ||
                data == null || 
                data.DataSource == null || 
                !data.SupportsFiltering)
            {
                return String.Empty;
            }

            // Retrieve the filtered row count. 
            Int32 currentRowCount = data.Count;

            // Retrieve the unfiltered row count by 
            // temporarily unfiltering the data.
            data.RaiseListChangedEvents = false;
            String oldFilter = data.Filter;
            data.Filter = null;
            Int32 unfilteredRowCount = data.Count;
            data.Filter = oldFilter;
            data.RaiseListChangedEvents = true;

            Debug.Assert(currentRowCount <= unfilteredRowCount, 
                "current count is greater than unfiltered count");

            // Return String.Empty if the filtered and unfiltered counts
            // are the same, otherwise, return the status string. 
            if (currentRowCount == unfilteredRowCount)
            {
                return String.Empty;
            }
            return String.Format("{0} of {1} records found", 
                currentRowCount, unfilteredRowCount);
        }

        #endregion filtering

        #region button bounds: DropDownButtonBounds, InvalidateDropDownButtonBounds, SetDropDownButtonBounds, AdjustPadding

        /// <summary>
        /// The bounds of the drop-down button, or Rectangle.Empty if filtering 
        /// is disabled or the button bounds need to be recalculated. 
        /// </summary>
        private Rectangle dropDownButtonBoundsValue = Rectangle.Empty;

        /// <summary>
        /// The bounds of the drop-down button, or Rectangle.Empty if filtering
        /// is disabled. Recalculates the button bounds if filtering is enabled
        /// and the bounds are empty.
        /// </summary>
        protected Rectangle DropDownButtonBounds
        {
            get
            {
                if (!FilteringEnabled)
                {
                    return Rectangle.Empty;
                }
                if (dropDownButtonBoundsValue == Rectangle.Empty)
                {
                    SetDropDownButtonBounds();
                }
                return dropDownButtonBoundsValue;
            }
        }

        /// <summary>
        /// Sets dropDownButtonBoundsValue to Rectangle.Empty if it isn't already empty. 
        /// This indicates that the button bounds should be recalculated. 
        /// </summary>
        private void InvalidateDropDownButtonBounds()
        {
            if (!dropDownButtonBoundsValue.IsEmpty)
            {
                dropDownButtonBoundsValue = Rectangle.Empty;
            }
        }

        /// <summary>
        /// Sets the position and size of dropDownButtonBoundsValue based on the current 
        /// cell bounds and the preferred cell height for a single line of header text. 
        /// </summary>
        private void SetDropDownButtonBounds()
        {
            // Retrieve the cell display rectangle, which is used to 
            // set the position of the drop-down button. 
            Rectangle cellBounds = 
                this.DataGridView.GetCellDisplayRectangle(
                this.ColumnIndex, -1, false);

            // Initialize a variable to store the button edge length,
            // setting its initial value based on the font height. 
            Int32 buttonEdgeLength = this.InheritedStyle.Font.Height + 5;

            // Calculate the height of the cell borders and padding.
            Rectangle borderRect = BorderWidths(
                this.DataGridView.AdjustColumnHeaderBorderStyle(
                this.DataGridView.AdvancedColumnHeadersBorderStyle,
                new DataGridViewAdvancedBorderStyle(), false, false));
            Int32 borderAndPaddingHeight = 2 +
                borderRect.Top + borderRect.Height +
                this.InheritedStyle.Padding.Vertical;
            Boolean visualStylesEnabled =
                Application.RenderWithVisualStyles &&
                this.DataGridView.EnableHeadersVisualStyles;
            if (visualStylesEnabled) 
            {
                borderAndPaddingHeight += 3;
            }

            // Constrain the button edge length to the height of the 
            // column headers minus the border and padding height. 
            if (buttonEdgeLength >
                this.DataGridView.ColumnHeadersHeight -
                borderAndPaddingHeight)
            {
                buttonEdgeLength =
                    this.DataGridView.ColumnHeadersHeight -
                    borderAndPaddingHeight;
            }

            // Constrain the button edge length to the
            // width of the cell minus three.
            if (buttonEdgeLength > cellBounds.Width - 3)
            {
                buttonEdgeLength = cellBounds.Width - 3;
            }

            // Calculate the location of the drop-down button, with adjustments
            // based on whether visual styles are enabled. 
            Int32 topOffset = visualStylesEnabled ? 4 : 1;
            Int32 top = cellBounds.Bottom - buttonEdgeLength - topOffset;
            Int32 leftOffset = visualStylesEnabled ? 3 : 1;
            Int32 left = 0;
            if (this.DataGridView.RightToLeft == RightToLeft.No)
            {
                left = cellBounds.Right - buttonEdgeLength - leftOffset;
            }
            else
            {
                left = cellBounds.Left + leftOffset;
            }

            // Set the dropDownButtonBoundsValue value using the calculated 
            // values, and adjust the cell padding accordingly.  
            dropDownButtonBoundsValue = new Rectangle(left, top, 
                buttonEdgeLength, buttonEdgeLength);
            AdjustPadding(buttonEdgeLength + leftOffset);
        }

        /// <summary>
        /// Adjusts the cell padding to widen the header by the drop-down button width.
        /// </summary>
        /// <param name="newDropDownButtonPaddingOffset">The new drop-down button width.</param>
        private void AdjustPadding(Int32 newDropDownButtonPaddingOffset)
        {
            // Determine the difference between the new and current 
            // padding adjustment.
            Int32 widthChange = newDropDownButtonPaddingOffset - 
                currentDropDownButtonPaddingOffset;

            // If the padding needs to change, store the new value and 
            // make the change.
            if (widthChange != 0)
            {
                // Store the offset for the drop-down button separately from 
                // the padding in case the client needs additional padding.
                currentDropDownButtonPaddingOffset = 
                    newDropDownButtonPaddingOffset;
                
                // Create a new Padding using the adjustment amount, then add it
                // to the cell's existing Style.Padding property value. 
                Padding dropDownPadding = new Padding(0, 0, widthChange, 0);
                this.Style.Padding = Padding.Add(
                    this.InheritedStyle.Padding, dropDownPadding);
            }
        }

        /// <summary>
        /// The current width of the drop-down button. This field is used to adjust the cell padding.  
        /// </summary>
        private Int32 currentDropDownButtonPaddingOffset;

        #endregion button bounds

        #region public properties: FilteringEnabled, AutomaticSortingEnabled, DropDownListBoxMaxLines

        /// <summary>
        /// Indicates whether filtering is enabled for the owning column. 
        /// </summary>
        private Boolean filteringEnabledValue = true;

        /// <summary>
        /// Gets or sets a value indicating whether filtering is enabled.
        /// </summary>
        [DefaultValue(true)]
        public Boolean FilteringEnabled
        {
            get 
            { 
                // Return filteringEnabledValue if (there is no DataGridView
                // or if (its DataSource property has not been set. 
                if (this.DataGridView == null ||
                    this.DataGridView.DataSource == null)
                {
                    return filteringEnabledValue;
                }

                // if (the DataSource property has been set, return a value that combines 
                // the filteringEnabledValue and BindingSource.SupportsFiltering values.
                BindingSource data = this.DataGridView.DataSource as BindingSource;
                Debug.Assert(data != null);
                return filteringEnabledValue && data.SupportsFiltering;
            }
            set 
            {
                // If filtering is disabled, remove the padding adjustment
                // and invalidate the button bounds. 
                if (!value)
                {
                    AdjustPadding(0);
                    InvalidateDropDownButtonBounds();
                }
                
                filteringEnabledValue = value; 
            }
        }

        /// <summary>
        /// Indicates whether automatic sorting is enabled. 
        /// </summary>
        private Boolean automaticSortingEnabledValue = true;

        /// <summary>
        /// Gets or sets a value indicating whether automatic sorting is enabled for the owning column. 
        /// </summary>
        [DefaultValue(true)]
        public Boolean AutomaticSortingEnabled
        {
            get 
            { 
                return automaticSortingEnabledValue; 
            }
            set 
            { 
                automaticSortingEnabledValue = value;
                if (OwningColumn != null)
                {
                    if (value)
                    {
                        OwningColumn.SortMode = DataGridViewColumnSortMode.Programmatic;
                    }
                    else
                    {
                        OwningColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
                    }
                }
            }
        }

        /// <summary>
        /// The maximum number of lines in the drop-down list. 
        /// </summary>
        private Int32 dropDownListBoxMaxLinesValue = 20; 

        /// <summary>
        /// Gets or sets the maximum number of lines to display in the drop-down filter list. 
        /// The actual height of the drop-down list is constrained by the DataGridView height. 
        /// </summary>
        [DefaultValue(20)]
        public Int32 DropDownListBoxMaxLines
        {
            get { return dropDownListBoxMaxLinesValue; }
            set { dropDownListBoxMaxLinesValue = value; }
        }

        #endregion public properties

        /// <summary>
        /// Represents a ListBox control used as a drop-down filter list
        /// in a DataGridView control.
        /// </summary>
        private class FilterListBox : ListBox
        {
            /// <summary>
            /// Initializes a new instance of the FilterListBox class.
            /// </summary>
            public FilterListBox()
            {
                Visible = false;
                IntegralHeight = true;
                BorderStyle = BorderStyle.FixedSingle;
                TabStop = false;
            }

            /// <summary>
            /// Indicates that the FilterListBox will handle (or ignore) all 
            /// keystrokes that are not handled by the operating system. 
            /// </summary>
            /// <param name="keyData">A Keys value that represents the keyboard input.</param>
            /// <returns>true in all cases.</returns>
            protected override bool IsInputKey(Keys keyData)
            {
                return true;
            }

            /// <summary>
            /// Processes a keyboard message directly, preventing it from being
            /// intercepted by the parent DataGridView control.
            /// </summary>
            /// <param name="m">A Message, passed by reference, that 
            /// represents the window message to process.</param>
            /// <returns>true if the message was processed by the control;
            /// otherwise, false.</returns>
            protected override bool ProcessKeyMessage(ref Message m)
            {
                return ProcessKeyEventArgs(ref m);
            }

        }

    }

}

