/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls
{
	/// <summary>
    /// A MultiSelect TreeView.
    /// <para>
    /// Inspired by the example at http://www.codeproject.com/KB/tree/treeviewms.aspx for details.</para>
	/// </summary>
    public abstract class TreeViewMS : TreeView
    {
        // Length of the horizontal dashed lines representing each branch of the tree
        protected internal const int HORZ_DASH_LENGTH = 11;
        // Text padding
        protected internal const int PADDING = 3;
        // Width of images associated with the tree
        protected internal const int IMG_WIDTH = 16;

        private TreeNodeMS _anchorNode;
	    private bool _inRightClick;

	    private const int DEFAULT_ITEM_HEIGHT = 16;
	    private const float DEFAULT_FONT_SIZE = (float) 8.25;

        public const double DEFAULT_TEXT_FACTOR = 1;
	    public const double LRG_TEXT_FACTOR = 1.25;
	    public const double XLRG_TEXT_FACTOR = 1.5;

        protected TreeViewMS()
        {
            UseKeysOverride = false;
            _inRightClick = false;

            SelectedNodes = new TreeNodeSelectionMS();

            DashBrush = new TextureBrush(Resources.Dash) { WrapMode = WrapMode.Tile };
            SetStyle(ControlStyles.UserPaint, true);
            ItemHeight = DEFAULT_ITEM_HEIGHT;

            TreeStateRestorer = new TreeViewStateRestorer(this);
            AutoExpandSingleNodes = true;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IDocumentUIContainer DocumentContainer { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TextureBrush DashBrush { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ICollection<TreeNodeMS> SelectedNodes { get; private set; }

        // If true, disjoint select is enabled.
	    private bool _allowDisjoint;

        /// <summary>
        /// For functional testing of multiple selection code.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Keys KeysOverride { get; set; }

        public bool UseKeysOverride { get; set; }

        private Keys ModifierKeysOverriden
        {
            // If the control key is overriden, we can assume disjoint select was intended. 
            get
            {
                if (KeysOverride == Keys.Control)
                    _allowDisjoint = true;
                return UseKeysOverride ? KeysOverride : ModifierKeys;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // It is necessary to make sure that just the CTRL key is pressed and no modifiers -
            // else, CTRL+C, CTRL+V, CTRL+Z .. etc can all cause incorrect selections.
            // The combination below represents just the CTRL key command key.
            // Modifiers to CTRL are sent as a seperate, following command key, which will disable disjoint select.
            if (!UseKeysOverride)
                _allowDisjoint = keyData == (Keys.Control | Keys.LButton | Keys.ShiftKey);
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected bool IsDisjointSelect
        {
            // Disjoint select only occurs when the control key is held, so check that first then check
            // allow disjoint to check for modifiers to the control key.
            get { return ModifierKeysOverriden == Keys.Control && _allowDisjoint; }
        }

        protected bool IsRangeSelect { get { return ModifierKeysOverriden == Keys.Shift; } }

        public void SelectNode(TreeNodeMS node, bool select)
        {
            if (!select)
                SelectedNodes.Remove(node);
            else if (!node.IsInSelection)
            {
                SelectedNodes.Add(node);
                // Make sure all ancestors of this node are expanded
                for (var parent = node.Parent; parent != null && !parent.IsExpanded; parent = parent.Parent)
                {
                    parent.Expand();
                }
            }
            node.IsInSelection = select;
        }

        public bool IsNodeSelected(TreeNode node)
        {
            return node is TreeNodeMS && ((TreeNodeMS) node).IsInSelection;
        }

        protected void UpdateSelection()
        {
            // Remove any nodes from the selection that may have been
            // removed from the tree.
            var selectedNodes = SelectedNodes.ToArray();
            foreach (var node in selectedNodes)
            {
                if (node.TreeView == null)
                    SelectedNodes.Remove(node);
            }

            // If any nodes were removed from the selection, reset the
            // anchor node to the selected node.
            if (selectedNodes.Length != SelectedNodes.Count)
                _anchorNode = (TreeNodeMS) SelectedNode;
        }

        [Browsable(true)]
        public bool AutoExpandSingleNodes { get; set; }

        protected void TreeViewMS_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (!IsInUpdate)
            {
                TreeNode nodeTree = e.Node;
                if (IsParentNode(nodeTree))
                {
                    // Save and restore top node to keep from scrolling
                    TreeNode nodeTop = TopNode;

                    int children = EnsureChildren(nodeTree);

                    // Do the Windows explorer thing of expanding single node children.
                    if (AutoExpandSingleNodes && children == 1)
                        nodeTree.Nodes[0].Expand();

                    TopNode = nodeTop;
                }
            }
        }

	    protected abstract bool IsParentNode(TreeNode node);

	    protected abstract int EnsureChildren(TreeNode node);

        public bool RestoredFromPersistentString { get; set; }
        private TreeViewStateRestorer TreeStateRestorer { get; set; }

        public string GetPersistentString()
        {
            return TreeStateRestorer.GetPersistentString();
        }

        public void RestoreExpansionAndSelection(string persistentString)
        {
            TreeStateRestorer.RestoreExpansionAndSelection(persistentString);
        }

        public void UpdateTopNode()
        {
            TreeStateRestorer.UpdateTopNode();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        public void ScrollLeft()
        {
            SetScrollPos(Handle, 0, 0, true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _inRightClick = e.Button == MouseButtons.Right;
            if (_inRightClick)
            {
                TreeNodeMS node = (TreeNodeMS)GetNodeAt(0, e.Y);
                if (node != null && node.BoundsMS.Contains(e.Location))
                    SelectedNode = node;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_inRightClick)
            {
                TreeNodeMS node = (TreeNodeMS)GetNodeAt(0, e.Y);
                if (node != null && node.BoundsMS.Contains(e.Location))
                {
                    // If we are within the bounds of a node and that node is not the selected node,
                    // make it the selected node. Changing the font of the TreeView at runtime
                    // apparently does not update node bounds, so we need to do this check in case the
                    // user clicked outside of the original node bounds.
                    if (!ReferenceEquals(node, SelectedNode))
                        SelectedNode = node;
                    // Handle cases where clicking on the selected node should change
                    // the selection.
                    else
                    {
                        // Disjoint selection or the SelectedNode is not in the selection
                        if (IsDisjointSelect || !IsNodeSelected(node))
                            SelectedNode = null;
                        // More than a single node currently selected, and not performing
                        // range selection on an existing range selection.
                        else if (SelectedNodes.Count > 1 &&
                                 (!IsRangeSelect || ReferenceEquals(_anchorNode, SelectedNode)))
                            SelectedNode = null;
                    }
                }
            }
            base.OnMouseUp(e);
            _inRightClick = false;
        }

        protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
        {
            base.OnBeforeSelect(e);

            // New selection is always the anchor for the next shift selection
            if (_anchorNode == null || !IsRangeSelect)
                _anchorNode = (TreeNodeMS) e.Node;
        }

        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            // Save old selection for invalidating
            var selectedNodesOld = SelectedNodes.ToArray();

            TreeNodeMS node = (TreeNodeMS)e.Node;

            // Don't change the selection if this is a right click and the node is in the
            // selection.
            if (node != null && !(_inRightClick && node.IsInSelection))
            {
                if (IsDisjointSelect)
                {
                    // Toggle selection on the node
                    SelectNode(node, !IsNodeSelected(e.Node));
                }
                else if (IsRangeSelect && !ReferenceEquals(_anchorNode, node))
                {
                    // Figure out top and bottom of the range to be selected
                    TreeNodeMS upperNode = _anchorNode;
                    TreeNodeMS bottomNode = node;
                    if (upperNode.BoundsMS.Top > bottomNode.BoundsMS.Top)
                        Helpers.Swap(ref upperNode, ref bottomNode);

                    // Set new selection to contain all visible nodes between top and bottom
                    SelectedNodes.Clear();
                    while (upperNode != null && !ReferenceEquals(upperNode, bottomNode))
                    {
                        SelectNode(upperNode, true);
                        upperNode = (TreeNodeMS)upperNode.NextVisibleNode;
                    }
                    SelectNode(bottomNode, true);
                }
                else
                {
                    // Make this a single selection of the selected node.
                    SelectedNodes.Clear();
                    SelectNode(node, true);
                }

                // Invalidate the changed nodes
                var unchangedNodes = new HashSet<TreeNodeMS>(selectedNodesOld.Intersect(SelectedNodes));
                InvalidateChangedNodes(selectedNodesOld, unchangedNodes);
                InvalidateChangedNodes(SelectedNodes, unchangedNodes);
            }

            Invalidate();

            // Make sure selection is updated before after select event is fired
            base.OnAfterSelect(e);
        }

        public virtual void OnTextZoomChanged()
        {
            ItemHeight = (int)(DEFAULT_ITEM_HEIGHT * Settings.Default.TextZoom);
            Font = new Font(Font.FontFamily, (float)(DEFAULT_FONT_SIZE * Settings.Default.TextZoom));
        }

        private void InvalidateChangedNodes(IEnumerable<TreeNodeMS> nodes, ICollection<TreeNodeMS> unchangedNodes)
        {
            if (IsInUpdate)
                return;

            foreach (var node in nodes)
            {
                if (!unchangedNodes.Contains(node))
                    InvalidateNode(node);
            }
        }

        protected void InvalidateNode(TreeNodeMS node)
        {
            Invalidate(new Rectangle(0, node.BoundsMS.Top, ClientRectangle.Width, node.BoundsMS.Height));
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {

        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // If we have nodes, then we have to draw them - and that means everything
            // about the node.
            using (var backColorBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(backColorBrush, ClientRectangle);
            }

            // No painting beyond the background while updating, since it can cause
            // unexpected exceptions.  This used to happen during a node removal that
            // caused removal of the control's scrollbar.
            if (IsInUpdate)
                return;

            // Draw all nodes exposed in the paint clipping rectangle.
            var drawRect = e.Graphics.ClipBounds;
            drawRect.Intersect(e.ClipRectangle);
            int bottom = (int)drawRect.Bottom;
            for (var node = TopNode;
                node != null && node.Bounds.Top <= bottom;
                node = node.NextVisibleNode)
            {
                ((TreeNodeMS)node).DrawNodeCustom(e.Graphics, ClientRectangle.Right);
            }
        }

	    private int _updateLockCount;

        public bool IsInUpdate { get { return _updateLockCount > 0; } }

        public void BeginUpdateMS()
        {
            BeginUpdate();
            _updateLockCount++;
        }

        public void EndUpdateMS()
        {
            if (_updateLockCount == 0)
                return;
            if (--_updateLockCount == 0)
                UpdateSelection();
            EndUpdate();
        }

        private class TreeNodeSelectionMS : ICollection<TreeNodeMS>
        {
            private readonly List<TreeNodeMS> _nodes = new List<TreeNodeMS>();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<TreeNodeMS> GetEnumerator()
            {
                return _nodes.GetEnumerator();
            }

            public void Add(TreeNodeMS item)
            {
                if (item == null)
                {
                    throw new ArgumentNullException();
                }
                item.IsInSelection = true;
                _nodes.Add(item);                
            }

            public void Clear()
            {
                _nodes.ForEach(node => node.IsInSelection = false);
                _nodes.Clear();
            }

            public bool Contains(TreeNodeMS item)
            {
                return _nodes.Contains(item);
            }

            public void CopyTo(TreeNodeMS[] array, int arrayIndex)
            {
                _nodes.CopyTo(array, arrayIndex);
            }

            public bool Remove(TreeNodeMS item)
            {
                if (item == null)
                {
                    return false;
                }
                if (_nodes.Remove(item))
                {
                    item.IsInSelection = false;
                    return true;
                }
                return false;
            }

            public int Count
            {
                get { return _nodes.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }
        }
    }

    public class TreeNodeMS : TreeNode
    {
        private const TextFormatFlags FORMAT_TEXT = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        public TreeNodeMS()
        {
        }

        public TreeNodeMS(string text) : base(text)
        {
        }

        /// <summary>
        /// Returns a typed reference to the owning <see cref="SequenceTree"/>.
        /// </summary>
        public TreeViewMS TreeViewMS { get { return (TreeViewMS)TreeView; } }

        public bool IsInSelection { get; protected internal set; }

        public Color ForeColorMS
        {
            get
            {
                if (!(IsSelected && IsInSelection) || !TreeViewMS.Focused)
                    return ForeColor;
                return SystemColors.HighlightText;
            }
        }

        public Color BackColorMS
        {
            get
            {
                if (!IsInSelection)
                    return BackColor;
                if (!TreeViewMS.Focused)
                    return Color.LightGray;
                if (IsSelected)
                    return SystemColors.Highlight;
                return Lighten(SystemColors.Highlight, 255);
            }
        }

        private static Color Lighten(Color color, byte brightness)
        {
            var hsbColor = new HSBColor(color) {B = brightness, S = 30};
            return hsbColor.ToRGB();
        }

        public Brush SelectionBrush
        {
            get
            {
                if (!IsInSelection)
                    return null;
                if (!TreeViewMS.Focused)
                    return Brushes.LightGray;
                if (IsSelected)
                    return SystemBrushes.Highlight;
                return new SolidBrush(BackColorMS);
            }
        }

        protected double _textZoomFactor;
        protected string _widthText;
        protected int _widthCustom;
        protected IList<Color> _groupColors;

        protected virtual int WidthCustom
        {
            get { return _widthCustom > 0 ? _widthCustom : Bounds.Width; }
        }

        protected virtual void EnsureWidthCustom(Graphics g)
        {
            // Measured only once, because the default Bounds width appears to be too
            // large when the tree view is not allowed to draw itself.
            if (_widthCustom == 0 || !ReferenceEquals(_widthText, Text) || _textZoomFactor != Settings.Default.TextZoom)
            {
                _widthCustom = TextRenderer.MeasureText(g, Text, TreeView.Font, Bounds.Size, FORMAT_TEXT).Width;
                _widthText = Text;
            }
        }

        /// <summary>
        /// Because these nodes allow override of text drawing, this virtual
        /// is required to get the true bounds of the text that is drawn.
        /// </summary>
        public Rectangle BoundsMS
        {
            get
            {
                var bounds = Bounds;
                bounds.Width = WidthCustom;
                return bounds;
            }
        }

        public int XIndent
        {
            // Finds the X coordinate of the indent for this node, accounting for horizontal scrolling.
            get
            {
                int treeIndent = TreeViewMS.HORZ_DASH_LENGTH + TreeViewMS.PADDING;
                // Always indent for the node image, whether it has one or not
                treeIndent += TreeViewMS.IMG_WIDTH;
                // Only indent for the state image, if it has one
                if (StateImageIndex != -1)
                    treeIndent += TreeViewMS.IMG_WIDTH;
                return BoundsMS.X - treeIndent;
            }
        }

        public int HorizScrollDiff
        {
            get
            {
                return XIndent - (Level*TreeView.Indent + 11);
            }
        }

        public virtual void DrawNodeCustom(Graphics g, int rightEdge)
        {
            EnsureWidthCustom(g);

            Rectangle bounds = BoundsMS;

            // Draw dashed lines
            var treeView = TreeViewMS;
            var dashBrush = treeView.DashBrush;
            // Horizontal line.
            dashBrush.TranslateTransform(Level % 2 + HorizScrollDiff, 0);
            g.FillRectangle(dashBrush, XIndent, bounds.Top + bounds.Height / 2,
                TreeViewMS.HORZ_DASH_LENGTH, 1);
            // Vertical lines corresponding to the horizontal level of this node.
            dashBrush.TranslateTransform(-Level % 2 - HorizScrollDiff, 0);
            // Check if this is the Root.
            if (ReferenceEquals(this, treeView.Nodes[0]))
            {
                if (treeView.Nodes.Count > 1)
                {
                    g.FillRectangle(dashBrush, XIndent, bounds.Top + bounds.Height/2,
                        1, bounds.Height/2);
                }
            }
            // Move up the levels of the tree, drawing the corresponding vertical lines.
            else
            {
                try
                {
                    TreeNodeMS curNode = this;
                    while (curNode != null)
                    {
                        dashBrush.TranslateTransform(0, curNode.Level % 2);
                        if (curNode.NextNode != null)
                            g.FillRectangle(dashBrush, curNode.XIndent, bounds.Top, 1, bounds.Height);
                        else if (curNode == this)
                            g.FillRectangle(dashBrush, curNode.XIndent, bounds.Top, 1, bounds.Height / 2);
                        dashBrush.TranslateTransform(0, -curNode.Level % 2);
                        curNode = curNode.Parent as TreeNodeMS;
                    }
                }
                catch (NullReferenceException)
                {
                    // Ignore a NullReferenceException in this code.  The case
                    // that once caused this has been fixed, but this safeguard is
                    // kept to avoid showing an unhandled exception to the user.

                    // If the node being painted is in the process of being removed
                    // from the tree, then curNode.NextNode will throw a NRE.
                }
            }

            // Draw Collapse/Expand bmps and the image associated with the node.
            if (Nodes.Count > 0)
            {
                Image expandCollapse = IsExpanded ? Resources.Collapse : Resources.Expand;
                g.DrawImage(expandCollapse, XIndent - expandCollapse.Width/2,
                    bounds.Top + (BoundsMS.Height - expandCollapse.Height + 1) / 2);
            }

            // Draw images associated with the node.
            int imgLocX = XIndent + TreeViewMS.HORZ_DASH_LENGTH;
            const int imgWidth = TreeViewMS.IMG_WIDTH, imgHeight = TreeViewMS.IMG_WIDTH;
            if (StateImageIndex != -1)
            {
                Image stateImg = TreeView.StateImageList.Images[StateImageIndex];
                g.DrawImageUnscaled(stateImg, imgLocX, bounds.Top + (bounds.Height - imgHeight) / 2, imgWidth, imgHeight);
                imgLocX += imgWidth;
            }
            if (ImageIndex != -1)
            {
                Image nodeImg = TreeView.ImageList.Images[ImageIndex];
                g.DrawImageUnscaled(nodeImg, imgLocX, bounds.Top + (bounds.Height - imgHeight) / 2, imgWidth, imgHeight);
            }

            DrawTextMS(g);
        }

        protected virtual void DrawTextMS(Graphics g)
        {
            DrawTextBackground(g);

            TextRenderer.DrawText(g, Text, TreeView.Font, BoundsMS, ForeColorMS, BackColorMS, FORMAT_TEXT);

            DrawFocus(g);
        }

        protected void DrawTextBackground(Graphics g)
        {
            if (IsInSelection)
                g.FillRectangle(SelectionBrush, BoundsMS);
        }

        protected void DrawFocus(Graphics g)
        {
            if (IsSelected && TreeView.Focused)
                ControlPaint.DrawFocusRectangle(g, BoundsMS, ForeColorMS, BackColorMS);
        }
    }
}
