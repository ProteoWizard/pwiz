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

        public int GetRowIndexForRowIndexHierarchy (IList<int> rowIndexHierarchy)
        {
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
                            throw new ArgumentOutOfRangeException("rowIndexHierarchy", "Row hierarchy not found.");
                        if (compare == 0)
                            break;
                    }
                    ++flatRowIndex;
                }
                if (compare != 0)
                    throw new ArgumentOutOfRangeException("rowIndexHierarchy", "Row hierarchy not found.");
            }
            return flatRowIndex;
        }

        public int GetRowIndexForRowIndexHierarchy (params int[] rowIndexHierarchy)
        {
            return this.GetRowIndexForRowIndexHierarchy(rowIndexHierarchy as IList<int>);
        }

        protected static IList<int> HeaderRowIndexHierarchy = new int[] { -1 };
        public IList<int> GetRowHierarchyForRowIndex (int rowIndex)
        {
            if (rowIndex < 0)
                return HeaderRowIndexHierarchy;
            return expandedRowList[rowIndex].RowIndexHierarchy;
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

            public int ChildRowCount { get; set; }
            public bool HasChildRows { get { return ChildRowCount > 0; } }
            public List<ExpandedRowList> ExpandedChildRows { get; set; }
            public IList<int> RowIndexHierarchy { get; set; }

            public static List<ExpandedRowList> EmptyChildRows = new List<ExpandedRowList>();

            public override string ToString ()
            {
                return String.Format("{0} ({1})", String.Join(",", RowIndexHierarchy.Select(o => o.ToString()).ToArray()), ExpandedChildRows.Count);
            }
        }

        protected List<ExpandedRowList> expandedRowList;

        protected override void OnCellValueNeeded (DataGridViewCellValueEventArgs e)
        {
            if (CellValueNeeded == null)
                throw new InvalidOperationException("TreeDataGridView requires at least one handler for CellValueNeeded");

            // DataGridView may ask for cells after RowCount is set to 0 in order to commit changes
            if (expandedRowList == null || e.RowIndex >= expandedRowList.Count)
                return;

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

        protected override void OnKeyDown (KeyEventArgs e)
        {
            if (e.KeyData == Keys.Right)
            {
                int totalRowsAdded = 0;
                foreach (var row in SelectedCells.Cast<DataGridViewCell>().Select(o => o.OwningRow).Distinct().OrderByDescending(o => o.Index))
                    totalRowsAdded += expand(row.Index, expandedRowList[row.Index].RowIndexHierarchy.Count);
                base.RowCount += totalRowsAdded;
            }
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

            if (hitTestInfo.ColumnIndex != 0 || hitTestInfo.RowIndex < 0)
            {
                base.OnMouseClick(e);
                return;
            }

            var rowInfo = expandedRowList[hitTestInfo.RowIndex];
            if (!rowInfo.HasChildRows)
            {
                base.OnMouseClick(e);
                return;
            }

            var cellBounds = GetCellDisplayRectangle(hitTestInfo.ColumnIndex, hitTestInfo.RowIndex, false);
            Point symbolPoint = GetSymbolPoint(cellBounds, rowInfo.RowIndexHierarchy.Count - 1);

            if (e.X < symbolPoint.X || e.X > symbolPoint.X + SymbolWidth)
            {
                base.OnMouseClick(e);
                return;
            }

            if (rowInfo.ExpandedChildRows.Count > 0)
                Collapse(hitTestInfo.RowIndex);
            else
                Expand(hitTestInfo.RowIndex);
        }

        protected Point GetSymbolPoint (Rectangle cellBounds, int nodeDepth)
        {
            Point symbolPoint = cellBounds.Location;
            symbolPoint.X += SymbolWidth / 2 + nodeDepth * (SymbolWidth + 2);
            symbolPoint.Y += (cellBounds.Height - SymbolHeight) / 3;
            return symbolPoint;
        }

        protected override void OnCellPainting (DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0 || e.ColumnIndex != 0)
            {
                base.OnCellPainting(e);
                return;
            }
            e.Handled = true;

            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
            e.PaintBackground(e.CellBounds, isSelected);

            var rowInfo = expandedRowList[e.RowIndex];
            int nodeDepth = rowInfo.RowIndexHierarchy.Count - 1;
            int symbolHeight = SymbolHeight;
            int symbolWidth = SymbolWidth;

            Point symbolPoint = GetSymbolPoint(e.CellBounds, nodeDepth);

            if (rowInfo.HasChildRows)
            {
                var symbol = rowInfo.ExpandedChildRows.Count > 0 ? ExpandedSymbol : CollapsedSymbol;

                var smoothingMode = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

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

            var indentPadding = new Padding(symbolPoint.X - e.CellBounds.X + SymbolWidth + 2, 0, 0, 0);
            e.CellStyle.Padding = indentPadding;
            e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground);
        }

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
                    var childRowInfo = new ExpandedRowList(rowInfo.RowIndexHierarchy, j);
                    var cellValueEventArgs = new TreeDataGridViewCellValueEventArgs(0, childRowInfo.RowIndexHierarchy);
                    CellValueNeeded(this, cellValueEventArgs);
                    childRowInfo.ChildRowCount = cellValueEventArgs.ChildRowCount;
                    childRowList.Add(childRowInfo);
                }
                rowInfo.ExpandedChildRows = childRowList;
                expandedRowList.InsertRange(index + 1, childRowList);
                rowsAdded = childRowList.Count;
            }

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

    public delegate void TreeDataGridViewCellValueEventHandler (object sender, TreeDataGridViewCellValueEventArgs e);
    public delegate void TreeDataGridViewCellFormattingEventHandler (object sender, TreeDataGridViewCellFormattingEventArgs e);
    public delegate void TreeDataGridViewCellEventHandler (object sender, TreeDataGridViewCellEventArgs e);
}