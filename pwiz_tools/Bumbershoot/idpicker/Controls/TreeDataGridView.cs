//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    public class TreeDataGridView : PreviewDataGridView
    {
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool VirtualMode { get { return true; } }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new int RowCount { get { return base.RowCount; } }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool AllowUserToAddRows { get { return false; } }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool AllowUserToDeleteRows { get { return false; } }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RootRowCount
        {
            get { return rootRowCount; }
            set
            {
                if (CellValueNeeded == null)
                    throw new InvalidOperationException("TreeDataGridView requires at least one handler for CellValueNeeded (before setting RootRowCount)");

                //Discard old list and repopulate with new values
                expandedRowList = null;
                if (value > 0)
                {
                    expandedRowList = new List<ExpandedRowList>(value);

                    for (int i = 0; i < value; ++i)
                    {
                        var rowInfo = new ExpandedRowList(i);
                        expandedRowList.Add(rowInfo);

                        var cellValueEventArgs = new TreeDataGridViewCellValueEventArgs(0, rowInfo.RowIndexHierarchy);
                        CellValueNeeded(this, cellValueEventArgs);
                        rowInfo.ChildRowCount = cellValueEventArgs.ChildRowCount;
                    }
                }
                base.RowCount = rootRowCount = value;
            }
        }
        private int rootRowCount;

        /// <summary>
        /// Class for storing a collection of lines to be drawn, 
        /// capable of retrieving overall information about dimensions 
        /// of combined lines.
        /// </summary>
        public class Symbol
        {
            public struct Line
            {
                public Line (int x1, int y1, int x2, int y2) : this()
                {
                    Start = new Point(x1, y1);
                    End = new Point(x2, y2);
                }

                public Point Start { get; set; }
                public Point End { get; set; }
            }

            public Symbol ()
            {
                var lines = new BindingList<Line>() {RaiseListChangedEvents = true};
                lines.ListChanged += Lines_ListChanged;
                Lines = lines;
            }

            void Lines_ListChanged (object sender, ListChangedEventArgs e)
            {
                Left = Lines.Min(o => Math.Min(o.Start.X, o.End.X));
                Top = Lines.Min(o => Math.Max(o.Start.Y, o.End.Y));
                Right = Lines.Max(o => Math.Max(o.Start.X, o.End.X));
                Bottom = Lines.Max(o => Math.Max(o.Start.Y, o.End.Y));

                Width = Right - Left;
                Height = Bottom - Top;
            }

            public IList<Line> Lines { get; private set; }

            public int Left { get; private set; }
            public int Top { get; private set; }
            public int Right { get; private set; }
            public int Bottom { get; private set; }

            public int Width { get; private set; }
            public int Height { get; private set; }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Symbol ExpandedSymbol { get; set; }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Symbol CollapsedSymbol { get; set; }

        public int SymbolWidth { get { return Math.Max(ExpandedSymbol.Width, CollapsedSymbol.Width); } }
        public int SymbolHeight { get { return Math.Max(ExpandedSymbol.Height, CollapsedSymbol.Height); } }

        public TreeDataGridView ()
        {
            base.VirtualMode = true;
            base.AllowUserToAddRows = false;
            base.AllowUserToDeleteRows = false;

            // default expanded symbol is triangle pointing down: v
            ExpandedSymbol = new Symbol();
            ExpandedSymbol.Lines.Add(new Symbol.Line(0,0, 8,0));
            ExpandedSymbol.Lines.Add(new Symbol.Line(0,0, 4,4));
            ExpandedSymbol.Lines.Add(new Symbol.Line(4,4, 8,0));

            // default collapsed symbol is a triangle pointing right: >
            CollapsedSymbol = new Symbol();
            CollapsedSymbol.Lines.Add(new Symbol.Line(0, 0, 0, 8));
            CollapsedSymbol.Lines.Add(new Symbol.Line(0, 0, 4, 4));
            CollapsedSymbol.Lines.Add(new Symbol.Line(4, 4, 0, 8));

            expandedRowList = new List<ExpandedRowList>();
        }

        public new event TreeDataGridViewCellValueEventHandler CellValueNeeded;
        public new event TreeDataGridViewCellFormattingEventHandler CellFormatting;
        public new event TreeDataGridViewCellEventHandler CellClick;
        public new event TreeDataGridViewCellEventHandler CellContentClick;
        public new event TreeDataGridViewCellEventHandler CellDoubleClick;
        public new event TreeDataGridViewCellEventHandler CellContentDoubleClick;
        public new event TreeDataGridViewCellMouseEventHandler CellMouseClick;

        /// <summary>
        /// Sets row image. To use set e.Value to the desired image.
        /// </summary>
        public event TreeDataGridViewCellValueEventHandler CellIconNeeded;

        /// <summary>
        /// Get the actual row index of the indicated row
        /// </summary>
        /// <param name="rowIndexHierarchy">Row hierarchical location information</param>
        /// <returns></returns>
        public int GetRowIndexForRowIndexHierarchy (IList<int> rowIndexHierarchy)
        {
            //start at the top row of the top tier, 
            int flatRowIndex = rowIndexHierarchy[0];
            if (flatRowIndex < 0)
                return -1;
            for (int i=0; i < rowIndexHierarchy.Count; ++i)
            {
                // skip rows until flatRow.RowIndexHierarchy[i] == rowIndexHierarchy[i]
                int compare = 1;
                while (flatRowIndex < expandedRowList.Count)
                {
                    var flatRow = expandedRowList[flatRowIndex];
                    if (rowIndexHierarchy.Count == flatRow.RowIndexHierarchy.Count)
                    {
                        compare = flatRow.RowIndexHierarchy[i].CompareTo(rowIndexHierarchy[i]);
                        if (compare > 0)
                            throw new ArgumentOutOfRangeException("rowIndexHierarchy", String.Format("Row hierarchy {{{0}}} not found.", String.Join(",", rowIndexHierarchy.Select(o => o.ToString()).ToArray())));
                        if (compare == 0)
                            break;
                    }
                    ++flatRowIndex;
                }
                if (compare != 0)
                    throw new ArgumentOutOfRangeException("rowIndexHierarchy", String.Format("Row hierarchy {{{0}}} not found.", String.Join(",", rowIndexHierarchy.Select(o => o.ToString()).ToArray())));
            }
            return flatRowIndex;
        }

        public int GetRowIndexForRowIndexHierarchy (params int[] rowIndexHierarchy)
        {
            return this.GetRowIndexForRowIndexHierarchy(rowIndexHierarchy as IList<int>);
        }

        protected static ReadOnlyCollection<int> HeaderRowIndexHierarchy = new ReadOnlyCollection<int>(new int[] { -1 });

        /// <summary>
        /// If Row index is known get the hierarchical identification of the row
        /// </summary>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        public ReadOnlyCollection<int> GetRowHierarchyForRowIndex (int rowIndex)
        {
            if (rowIndex < 0)
                return HeaderRowIndexHierarchy;
            return new ReadOnlyCollection<int>(expandedRowList[rowIndex].RowIndexHierarchy);
        }

        public DataGridViewCell this[int columnIndex, params int[] rowIndexHierarchy]
        {
            get { return base[columnIndex, GetRowIndexForRowIndexHierarchy(rowIndexHierarchy)]; }
            set { base[columnIndex, GetRowIndexForRowIndexHierarchy(rowIndexHierarchy)] = value; }
        }

        public DataGridViewCell this[int columnIndex, IList<int> rowIndexHierarchy]
        {
            get { return base[columnIndex, GetRowIndexForRowIndexHierarchy(rowIndexHierarchy)]; }
            set { base[columnIndex, GetRowIndexForRowIndexHierarchy(rowIndexHierarchy)] = value; }
        }

        //Disallow editing just by column and row index, as the index of any given row changes
        private new DataGridViewCell this[int columnIndex, int rowIndex] { set { } }

        #region Expand/Collapse methods

        public void Expand (int index)
        {
            base.RowCount += expand(index, expandedRowList[index].RowIndexHierarchy.Count);
            Refresh();
        }

        public void Expand (int index, int maxDepth)
        {
            base.RowCount += expand(index, maxDepth);
            Refresh();
        }

        public void ExpandAll (int maxDepth)
        {
            // for each root row
            int totalRowsAdded = 0;
            for (int i = 0; i < expandedRowList.Count; ++i)
            {
                int rowsAdded = expand(i, maxDepth);
                totalRowsAdded += rowsAdded;
                i += rowsAdded;
            }
            base.RowCount += totalRowsAdded;
            Refresh();
        }

        public void ExpandAll () { ExpandAll(int.MaxValue); }

        public void Collapse (int index)
        {
            base.RowCount -= collapse(index);
            Refresh();
        }

        public void CollapseAll ()
        {
            if (expandedRowList == null)
                return;

            // for each root row
            int totalRowsRemoved = 0;
            for (int i = 0; i < expandedRowList.Count; ++i)
                if (expandedRowList[i].RowIndexHierarchy.Count == 1 &&
                    expandedRowList[i].ExpandedChildRows.Count > 0)
                {
                    int rowsRemoved = collapse(i);
                    totalRowsRemoved += rowsRemoved;
                }
            base.RowCount -= totalRowsRemoved;
            Refresh();
        }
        #endregion

        /// <summary>
        /// Holds information on a specific row, including how to navigate to it through the information tiers
        /// </summary>
        protected class ExpandedRowList
        {
            public ExpandedRowList (int rowIndex) : this(null, rowIndex) { }
            public ExpandedRowList (IList<int> parentRowIndexHierarchy, int childRowIndex)
            {
                ChildRowCount = 0;
                ExpandedChildRows = EmptyChildRows;

                int rowIndexHierarchySize = parentRowIndexHierarchy != null ? parentRowIndexHierarchy.Count+1 : 1;
                var rowIndexHierarchy = new List<int>(rowIndexHierarchySize);
                if (parentRowIndexHierarchy != null)
                    rowIndexHierarchy.AddRange(parentRowIndexHierarchy);
                rowIndexHierarchy.Add(childRowIndex);
                RowIndexHierarchy = rowIndexHierarchy;
            }

            /// <summary>how many child rows are currently expanded</summary>
            public int ChildRowCount { get; set; } 

            /// <summary>quick test if row is expanded</summary>
            public bool HasChildRows { get { return ChildRowCount > 0; } }

            /// <summary>info on sub-rows</summary>
            public List<ExpandedRowList> ExpandedChildRows { get; set; }

            /// <summary>the exact position of the row in the tree hierarchy</summary>
            public IList<int> RowIndexHierarchy { get; set; }

            //Examples- Value of {0} indicates that it is the first row in the top tier
            //          Value of {3} indicates that it is the fourth row in the top tier
            //          Value of {1,3} indicates that it is the fourth sub-row of the second row in the top tier
            //          Value of {0,4,2,3} would be accesssed by expanding the first row,
            //                             then the fifth subrow, then the third sub row of that,
            //                             and finally look to the third sub item of that expansion

            /// <summary>rows that are present even when there are no sub-rows</summary>
            public static List<ExpandedRowList> EmptyChildRows = new List<ExpandedRowList>();

            public override string ToString ()
            {
                return String.Format("{0} ({1})", String.Join(",", RowIndexHierarchy.Select(o => o.ToString()).ToArray()), ExpandedChildRows.Count);
            }
        }

        protected List<ExpandedRowList> expandedRowList; //keeps track of all nodes shown in table

        #region Overridden events

        protected override void OnCellValueNeeded (DataGridViewCellValueEventArgs e)
        {
            if (CellValueNeeded == null)
                throw new InvalidOperationException("TreeDataGridView requires at least one handler for CellValueNeeded");

            // DataGridView may ask for cells after RowCount is set to 0 in order to commit changes
            if (expandedRowList == null || e.RowIndex >= expandedRowList.Count)
                return;

            //converts row number into hierarchy list, understandable by user-specified value-retrieval function
            var rowInfo = expandedRowList[e.RowIndex];
            var cellValueEventArgs = new TreeDataGridViewCellValueEventArgs(e.ColumnIndex, rowInfo.RowIndexHierarchy);
            CellValueNeeded(this, cellValueEventArgs);
            e.Value = cellValueEventArgs.Value;
            if (e.ColumnIndex == 0)
                rowInfo.ChildRowCount = cellValueEventArgs.ChildRowCount;
        }

        protected override void OnCellFormatting (DataGridViewCellFormattingEventArgs e)
        {
            // DataGridView may ask for cells after RowCount is set to 0 in order to commit changes
            if (expandedRowList == null || e.RowIndex >= expandedRowList.Count)
                return;

            if (CellFormatting != null)
                CellFormatting(this, new TreeDataGridViewCellFormattingEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex), e.Value, e.DesiredType, e.CellStyle));
            else
                base.OnCellFormatting(e);
        }

        protected override void OnCellClick (DataGridViewCellEventArgs e)
        {
            base.OnCellClick(e);
            if (CellClick != null)
                CellClick(this, new TreeDataGridViewCellEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex)));
        }

        protected override void OnCellContentClick (DataGridViewCellEventArgs e)
        {
            base.OnCellContentClick(e);
            if (CellContentClick != null)
                CellContentClick(this, new TreeDataGridViewCellEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex)));
        }

        protected override void OnCellDoubleClick(DataGridViewCellEventArgs e)
        {
            base.OnCellDoubleClick(e);
            if (CellDoubleClick != null)
                CellDoubleClick(this, new TreeDataGridViewCellEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex)));
        }

        protected override void OnCellContentDoubleClick(DataGridViewCellEventArgs e)
        {
            base.OnCellContentDoubleClick(e);
            if (CellContentDoubleClick != null)
                CellContentDoubleClick(this, new TreeDataGridViewCellEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex)));
        }

        protected override void OnCellMouseClick (DataGridViewCellMouseEventArgs e)
        {
            base.OnCellMouseClick(e);
            if (CellMouseClick != null)
                CellMouseClick(this, new TreeDataGridViewCellMouseEventArgs(e.ColumnIndex, GetRowHierarchyForRowIndex(e.RowIndex), e.X, e.Y, e));
        }

        protected override void OnKeyDown (KeyEventArgs e)
        {
            //Expands row if right arrow is pressed
            if (e.KeyData == Keys.Right)
            {
                int totalRowsAdded = 0;
                foreach (var row in SelectedCells.Cast<DataGridViewCell>().Select(o => o.OwningRow).Distinct().OrderByDescending(o => o.Index))
                    totalRowsAdded += expand(row.Index, expandedRowList[row.Index].RowIndexHierarchy.Count);
                base.RowCount += totalRowsAdded;
            }
            //Collapses row if left arrow is pressed
            else if (e.KeyData == Keys.Left)
            {
                int totalRowsRemoved = 0;
                foreach (var row in SelectedCells.Cast<DataGridViewCell>().Select(o => o.OwningRow).Distinct().OrderByDescending(o => o.Index))
                    totalRowsRemoved += collapse(row.Index);
                base.RowCount -= totalRowsRemoved;
            }
            else
            {
                base.OnKeyDown(e);
                return;
            }
            Refresh();
        }

        protected override void OnMouseClick (MouseEventArgs e)
        {
            var hitTestInfo = HitTest(e.X, e.Y);

            //Dont do anything special if click isn't in the first cell of the row
            if (hitTestInfo.ColumnIndex != 0 || hitTestInfo.RowIndex < 0)
            {
                base.OnMouseClick(e);
                return;
            }

            //Dont do anything special if row has no children
            var rowInfo = expandedRowList[hitTestInfo.RowIndex];
            if (!rowInfo.HasChildRows)
            {
                base.OnMouseClick(e);
                return;
            }

            //Find symbol location
            var cellBounds = GetCellDisplayRectangle(hitTestInfo.ColumnIndex, hitTestInfo.RowIndex, false);
            Point symbolPoint = GetSymbolPoint(cellBounds, rowInfo.RowIndexHierarchy.Count - 1);

            //Dont do anything special if symbol isn't clicked
            if (e.X < symbolPoint.X || e.X > symbolPoint.X + SymbolWidth)
            {
                base.OnMouseClick(e);
                return;
            }

            //Expand or collapse depending on what is needed
            if (rowInfo.ExpandedChildRows.Count > 0)
                Collapse(hitTestInfo.RowIndex);
            else
                Expand(hitTestInfo.RowIndex);
        }

        /// <summary>
        /// Create symbols and/or images
        /// </summary>
        /// <param name="e"></param>
        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            //Only paint in the first column of each info-containing row
            if (e.RowIndex < 0 || e.ColumnIndex != 0)
            {
                base.OnCellPainting(e);
                return;
            }
            e.Handled = true;

            //Still show selection if row is selected
            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
            e.PaintBackground(e.CellBounds, isSelected);

            var rowInfo = expandedRowList[e.RowIndex];
            int nodeDepth = rowInfo.RowIndexHierarchy.Count - 1;
            int iconWidth = 0;

            Point symbolPoint = GetSymbolPoint(e.CellBounds, nodeDepth);

            //Draw symbol if row is not in bottom node
            if (rowInfo.HasChildRows)
            {
                var symbol = rowInfo.ExpandedChildRows.Count > 0 ? ExpandedSymbol : CollapsedSymbol;

                var smoothingMode = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                //Go through and actually draw the stored symbol
                var brush = new SolidBrush(isSelected ? e.CellStyle.SelectionForeColor : e.CellStyle.ForeColor);
                var pen = new Pen(brush);
                foreach (Symbol.Line line in symbol.Lines)
                {
                    Point lineStart = line.Start, lineEnd = line.End;
                    lineStart.Offset(symbolPoint.X, symbolPoint.Y);
                    lineEnd.Offset(symbolPoint.X, symbolPoint.Y);
                    e.Graphics.DrawLine(pen, lineStart, lineEnd);
                }
                e.Graphics.SmoothingMode = smoothingMode;
            }

            //Draw icon if one is selected
            if (CellIconNeeded != null)
            {
                var cellValueEventArgs = new TreeDataGridViewCellValueEventArgs(0, rowInfo.RowIndexHierarchy);
                CellIconNeeded(this, cellValueEventArgs);
                if (cellValueEventArgs.Value != null)
                {
                    var icon = (Image)cellValueEventArgs.Value;
                    e.Graphics.DrawImage(icon, symbolPoint.X + SymbolWidth + 5, symbolPoint.Y - 3, 15, 15);
                    iconWidth = 20; //Indicates icon was drawn
                }
            }

            //Paint cell info, taking previous images into account
            var indentPadding = new Padding(symbolPoint.X - e.CellBounds.X + SymbolWidth + iconWidth + 2, 0, 0, 0);
            e.CellStyle.Padding = indentPadding;
            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground);
        }

        #endregion

        /// <summary>
        /// Find location of symbol in relation to form
        /// </summary>
        /// <param name="cellBounds"></param>
        /// <param name="nodeDepth"></param>
        /// <returns></returns>
        protected Point GetSymbolPoint (Rectangle cellBounds, int nodeDepth)
        {
            Point symbolPoint = cellBounds.Location;
            symbolPoint.X += SymbolWidth / 2 + nodeDepth * (SymbolWidth + 2);
            symbolPoint.Y += (cellBounds.Height - SymbolHeight) / 3;
            return symbolPoint;
        }

        /// <summary>
        /// Show child rows
        /// </summary>
        /// <param name="index">Which row to expand</param>
        /// <param name="maxDepth">How far down to expand</param>
        /// <returns></returns>
        protected int expand (int index, int maxDepth)
        {
            var rowInfo = expandedRowList[index];

            if (!rowInfo.HasChildRows || rowInfo.ExpandedChildRows.Count > 0 && maxDepth == 0)
                return 0; // row already expanded, nothing to do

            int rowsAdded = 0;
            if (rowInfo.ExpandedChildRows.Count == 0)
            {
                var childRowList = new List<ExpandedRowList>(rowInfo.ChildRowCount);
                for (int j = 0; j < rowInfo.ChildRowCount; ++j)
                {
                    //get the content of the child row and add it to list to be inserted
                    var childRowInfo = new ExpandedRowList(rowInfo.RowIndexHierarchy, j);
                    var cellValueEventArgs = new TreeDataGridViewCellValueEventArgs(0, childRowInfo.RowIndexHierarchy);
                    CellValueNeeded(this, cellValueEventArgs);
                    childRowInfo.ChildRowCount = cellValueEventArgs.ChildRowCount;
                    childRowList.Add(childRowInfo);
                }

                //insert new rows into table
                rowInfo.ExpandedChildRows = childRowList;
                expandedRowList.InsertRange(index + 1, childRowList);
                rowsAdded = childRowList.Count;
            }

            //If more expansion is needed recurse back to the begining
            if (maxDepth > rowInfo.RowIndexHierarchy.Count)
                for (int j = 0; j < rowInfo.ChildRowCount; ++j)
                    rowsAdded += expand(index + rowsAdded + 1 - rowInfo.ChildRowCount + j, maxDepth);

            return rowsAdded;
        }

        protected int collapse (int index)
        {
            var rowInfo = expandedRowList[index];
            if (!rowInfo.HasChildRows || rowInfo.ExpandedChildRows.Count == 0)
                return 0; // row already collapsed, nothing to do

            int rowsRemoved = 0;
            if (rowInfo.ExpandedChildRows.Count > 0)
            {
                // find the next row with the same depth or less as the collapsed row
                int j = index + rowInfo.ExpandedChildRows.Count + 1;
                for (; j < expandedRowList.Count; ++j)
                    if (expandedRowList[j].RowIndexHierarchy.Count <= rowInfo.RowIndexHierarchy.Count)
                        break;
                rowsRemoved = j - index - 1;

                //remove approprite rows and show that current row has no shown children
                expandedRowList.RemoveRange(index + 1, rowsRemoved);
                rowInfo.ExpandedChildRows = ExpandedRowList.EmptyChildRows;
            }
            return rowsRemoved;
        }
    }

    public class TreeDataGridViewCellValueEventArgs : EventArgs
    {
        public TreeDataGridViewCellValueEventArgs (int columnIndex, params int[] rowIndexHierarchy) : this(columnIndex, rowIndexHierarchy as IList<int>) { }
        public TreeDataGridViewCellValueEventArgs (int columnIndex, IList<int> rowIndexHierarchy)
        {
            ColumnIndex = columnIndex;
            RowIndexHierarchy = rowIndexHierarchy;
        }

        public int ColumnIndex { get; protected set; }
        public IList<int> RowIndexHierarchy { get; protected set; }
        public object Value { get; set; }
        public int ChildRowCount { get; set; }
    }

    public class TreeDataGridViewCellFormattingEventArgs : DataGridViewCellFormattingEventArgs
    {
        public TreeDataGridViewCellFormattingEventArgs (int columnIndex, IList<int> rowIndexHierarchy, object value, Type desiredType, DataGridViewCellStyle cellStyle)
            : base(columnIndex, 0, value, desiredType, cellStyle)
        {
            RowIndexHierarchy = rowIndexHierarchy;
        }

        // not accessible to clients
        private new int RowIndex { get { return base.RowIndex; } }

        public IList<int> RowIndexHierarchy { get; protected set; }
    }

    public class TreeDataGridViewCellEventArgs : EventArgs
    {
        public TreeDataGridViewCellEventArgs (int columnIndex, params int[] rowIndexHierarchy) : this(columnIndex, rowIndexHierarchy as IList<int>) { }
        public TreeDataGridViewCellEventArgs (int columnIndex, IList<int> rowIndexHierarchy)
        {
            ColumnIndex = columnIndex;
            RowIndexHierarchy = rowIndexHierarchy;
        }

        public int ColumnIndex { get; protected set; }
        public IList<int> RowIndexHierarchy { get; protected set; }
    }

    public class TreeDataGridViewCellMouseEventArgs : DataGridViewCellMouseEventArgs
    {
        public TreeDataGridViewCellMouseEventArgs (int columnIndex, IList<int> rowIndexHierarchy,
                                                   int localX, int localY, MouseEventArgs e)
            : base(columnIndex, 0, localX, localY, e)
        {
            RowIndexHierarchy = rowIndexHierarchy;
        }

        // not accessible to clients
        private new int RowIndex { get { return base.RowIndex; } }

        public IList<int> RowIndexHierarchy { get; protected set; }
    }

    public delegate void TreeDataGridViewCellValueEventHandler (object sender, TreeDataGridViewCellValueEventArgs e);
    public delegate void TreeDataGridViewCellFormattingEventHandler (object sender, TreeDataGridViewCellFormattingEventArgs e);
    public delegate void TreeDataGridViewCellEventHandler (object sender, TreeDataGridViewCellEventArgs e);
    public delegate void TreeDataGridViewCellMouseEventHandler (object sender, TreeDataGridViewCellMouseEventArgs e);
}