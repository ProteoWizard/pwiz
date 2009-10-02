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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Imaging;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.SeqNode
{
    /// <summary>
    /// Dummy <see cref="TreeNode"/> used to cause the <see cref="SequenceTree"/>
    /// to show the expand indicator without actually creating all the
    /// child nodes.
    /// </summary>
    public class DummyNode : TreeNode
    {        
    }

    public class EmptyNode : TreeNode
    {
        public const string TEXT_EMPTY = "                 ";

        public EmptyNode(): base(TEXT_EMPTY)
        {
        }
    }

    /// <summary>
    /// Base class for all tree node to document node mapping in the <see cref="SequenceTree"/>.
    /// </summary>
    public abstract class SrmTreeNode : TreeNode
    {
        private readonly SequenceTree _sequenceTree;

        protected SrmTreeNode(SequenceTree tree, DocNode model)
        {
            _sequenceTree = tree;

            Debug.Assert(model != null);

            Model = model;
        }

        public abstract string Heading { get; }

        public DocNode Model
        {
            get { return (DocNode) Tag; }

            set
            {
                Tag = value;
                OnModelChanged();
            }
        }

        public void UpdateState()
        {
            OnModelChanged();            
        }

        /// <summary>
        /// Returns a typed reference to the owning <see cref="SequenceTree"/>.
        /// </summary>
        public SequenceTree SequenceTree { get { return _sequenceTree; } }

        /// <summary>
        /// Shortcut read-only property to the underlying <see cref="SrmDocument"/>.  This
        /// is the actual document, and may be more recent what the user is seeing
        /// in the tree at the time this property is accessed.
        /// 
        /// All modification must be applied to the current document, or produce
        /// an error, if it has been modified to the point where the information in
        /// the UI is insufficient to complete the action.
        /// </summary>
        public SrmDocument Document { get { return SequenceTree.Document; } }

        /// <summary>
        /// Shortcut read-only property to the document settings.  Because the underlying
        /// document may be replaced as the current document between calls, this
        /// property should only be used when the document itself is not required.
        /// 
        /// Otherwise, the document should be requested once, and the settings taken
        /// from that reference to the document.
        /// </summary>
        public SrmSettings DocSettings { get { return Document.Settings; } }

        /// <summary>
        /// Returns a typed reference to the parent <see cref="SrmTreeNodeParent"/> of this node.
        /// </summary>
        public SrmTreeNodeParent SrmParent { get { return (SrmTreeNodeParent) Parent; } }

        /// <summary>
        /// Returns an <see cref="IdentityPath"/> to this node, suitable for use
        /// in modifying the node through a reference to its <see cref="SrmDocument"/>,
        /// or for saving as an in memory reference to a tree selection.
        /// </summary>
        /// <returns></returns>
        public IdentityPath Path
        {
            get { return new IdentityPath(GetPath(new Stack<Identity>())); }
        }

        public static IdentityPath GetSafePath(SrmTreeNode node)
        {
            return (node == null ? IdentityPath.ROOT : node.Path);
        }

        /// <summary>
        /// Override to handle changes to the underlying document node, performing
        /// tasks such as updating the node icon, label text, and children.
        /// </summary>
        protected virtual void OnModelChanged()
        {            
        }

        /// <summary>
        /// Recursive call used to support the public method <see cref="GetPath"/>.
        /// Pushes the identity of the current node onto the path, and calls parent.
        /// </summary>
        /// <param name="path">A stack of children seen so far</param>
        /// <returns>The final path to the originating node</returns>
        protected virtual Stack<Identity> GetPath(Stack<Identity> path)
        {
            // Add this node to the end of the path
            path.Push(Model.Id);
            // If no parents, then the path is complete
            if (Parent == null)
                return path;
            // Add parent nodes
            return SrmParent.GetPath(path);
        }

        #region object overrides

        /// <summary>
        /// Node equality determined as content equality between the
        /// <see cref="Model"/> property of two tree nodes.
        /// </summary>
        /// <param name="obj">Other tree node to compare against</param>
        /// <returns>Tree if the <see cref="Model"/> properties are equal</returns>
        public bool Equals(SrmTreeNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Model, Model);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (SrmTreeNode)) return false;
            return Equals((SrmTreeNode) obj);
        }

        public override int GetHashCode()
        {
            return Tag.GetHashCode();
        }

        #endregion // object overrides
    }

    /// <summary>
    /// Base class for all tree node to parent document nodes mapping in
    /// the <see cref="SequenceTree"/>.
    /// </summary>
    public abstract class SrmTreeNodeParent : SrmTreeNode, IChildPicker
    {
        protected SrmTreeNodeParent(SequenceTree tree, DocNodeParent model)
            : base(tree, model)
        {
        }

        public abstract string ChildHeading { get; }

        public abstract string ChildUndoHeading { get; }

        public IList<DocNode> ChildDocNodes { get { return ((DocNodeParent) Model).Children; } }

        public void EnsureChildren()
        {
            if (ChildDocNodes.Count == 0)
                Nodes.Clear();
            else if (Nodes.Count == 0 || Nodes[0] is DummyNode)
            {
                try
                {
                    SequenceTree.BeginUpdate();
                    UpdateChildren(true);
                }
                finally
                {
                    SequenceTree.EndUpdate();
                }
            }
        }

        /// <summary>
        /// Called when children are updated during model changes.
        /// </summary>
        /// <param name="expandDefault">True if this type of node is expanded by default</param>
        protected void OnUpdateChildren(bool expandDefault)
        {
            int countChildNodes = Nodes.Count;
            UpdateChildren(IsExpanded || expandDefault);
            if (countChildNodes == 0 && Nodes.Count > 0 && expandDefault)
                Expand();
        }

        protected abstract void UpdateChildren(bool materialize);

        #region IChildPicker Members

        public virtual bool CanShow { get { return true; } }

        public void ShowPickList(Point location)
        {
            PopupPickList popup = new PopupPickList(this, ChildHeading) { Location = location };
            popup.Show();
            popup.Focus();
        }

        public abstract bool Filtered { get; set; }

        public abstract IPickedList CreatePickedList(IEnumerable<object> chosen, bool autoManageChildren);

        public abstract IEnumerable<object> GetChoices(bool useFilter);

        public virtual bool Equivalent(object choice1, object choice2)
        {
            return Equals(choice1, choice2);
        }

        public virtual string GetPickLabel(object child)
        {
            return child.ToString();
        }

        public virtual IEnumerable<object> Chosen
        {
            get
            {
                foreach (DocNode child in ChildDocNodes)
                    yield return child.Id;
            }
        }

        public abstract bool ShowAutoManageChildren { get; }

        public bool AutoManageChildren { get
            {
                return ((DocNodeParent) Model).AutoManageChildren;
            }
        }

        public void Pick(IEnumerable<object> chosen, bool autoManageChildren)
        {
            // Quick check to see if anything changed.
            if (Helpers.Equals(chosen, Chosen) && AutoManageChildren == autoManageChildren)
                return;

            SequenceTree.FirePickedChildren(this, CreatePickedList(chosen, autoManageChildren));

            // Make sure this node is open to show changes
            Expand();
        }

        #endregion

        /// <summary>
        /// Creates a corresponding <see cref="TreeNode"/> for a new <see cref="DocNode"/>.
        /// The function returns the tree node with any necessary child nodes.  Typically,
        /// this happens as a result of a recursive call to <see cref="SrmTreeNodeParent.UpdateNodes{TNode}"/>
        /// from an override of <see cref="SrmTreeNode.OnModelChanged"/>.
        /// </summary>
        /// <typeparam name="TNode">Type of tree node to create</typeparam>
        /// <param name="tree">The <see cref="SequenceTree"/> instance which will contain the node</param>
        /// <param name="nodeDoc">The <see cref="DocNode"/> for which a tree node is required</param>
        /// <returns>A new tree node</returns>
        public delegate TNode CreateTreeNode<TNode>(SequenceTree tree, DocNode nodeDoc)
            where TNode : SrmTreeNode;

        /// <summary>
        /// Performs the bulk of the real work for synchronizing the <see cref="SequenceTree"/>
        /// tree node structure with <see cref="SrmDocument"/> model.
        /// </summary>
        /// <typeparam name="TNode">Type of tree node in the supplied list</typeparam>
        /// <param name="tree">The <see cref="SequenceTree"/> instance that contains the nodes</param>
        /// <param name="treeNodes">A raw tree node collection from <see cref="TreeView"/></param>
        /// <param name="docNodes">List of <see cref="DocNode"/> objects the node collection should be updated to match</param>
        /// <param name="materialize">True forces doc nodes to materialize into tree node,
        ///     false allows a dummy node to be used, if appropriate</param>
        /// <param name="create">Node creation function used to supply tree nodes for new doc nodes</param>
        public static void UpdateNodes<TNode>(SequenceTree tree, TreeNodeCollection treeNodes,
                IList<DocNode> docNodes, bool materialize, CreateTreeNode<TNode> create)
            where TNode : SrmTreeNode
        {
            // This code is highly optimized to make as few modifications to the
            // tree as possible, as they can have negative impact on the selection.

            // First short-cut all the complexity, if the end result will be an
            // empty list.  This is way faster at removing all the proteins in the
            // File/New case.
            if (docNodes.Count == 0)
            {
                for (int iNode = treeNodes.Count - 1; iNode >= 0; iNode--)
                {
                    TreeNode nodeTree = treeNodes[iNode];
                    if (nodeTree is SrmTreeNode || nodeTree is DummyNode)
                        treeNodes.RemoveAt(iNode);
                }
            }
            else if (!materialize)
            {
                if (treeNodes.Count == 0)
                    treeNodes.Add(new DummyNode());
                if (treeNodes[0] is DummyNode)
                    return;
            }
            else if (treeNodes.Count > 0 && treeNodes[0] is DummyNode)
            {
                treeNodes.RemoveAt(0);
            }

            DocNode nodeDoc = null;

            // Keep remaining tree nodes into a map by the identity global index.
            Dictionary<int, TNode> remaining = new Dictionary<int, TNode>();

            // Enumerate as many tree nodes as possible that have either an
            // exact reference match with its corresponding DocNode in the list, or an
            // identity match with its corresponding DocNode.
            int i = 0;

            // Keep track of whether selected node changes
            bool selChanged = false;
            TreeNode nodeSel = tree.SelectedNode;

            do
            {
                // Match as many document nodes to existing tree nodes as possible.
                int count = Math.Min(docNodes.Count, treeNodes.Count);
                while (i < count)
                {
                    nodeDoc = docNodes[i];
                    TNode nodeTree = treeNodes[i] as TNode;
                    if (nodeTree == null)
                        break;
                    else if (!ReferenceEquals(nodeTree.Model, nodeDoc))
                    {
                        if (ReferenceEquals(nodeTree.Model.Id, nodeDoc.Id))
                        {
                            nodeTree.Model = nodeDoc;
                            selChanged = (nodeTree == nodeSel);
                        }
                        else
                        {
                            // If no usable equality, and not in the map of nodes already
                            // removed, then this loop cannot continue.
                            if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                                break;

                            // Found node with the same ID, so replace its doc node, if not
                            // reference equal to the one looked up.
                            if (!ReferenceEquals(nodeTree.Model, nodeDoc))
                            {
                                nodeTree.Model = nodeDoc;
                                selChanged = (nodeTree == nodeSel);
                            }
                            treeNodes.Insert(i, nodeTree);
                        }
                    }
                    i++;
                }

                // Add unmatched nodes to a map by GlobalIndex, until the next
                // document node is encountered, or all remaining nodes have been
                // added.
                Dictionary<int, TNode> remove = new Dictionary<int, TNode>();
                for (int iRemove = i; iRemove < treeNodes.Count; iRemove++)
                {
                    TNode nodeTree = treeNodes[iRemove] as TNode;
                    if (nodeTree == null)
                        break;
                    // Stop removing, if the next node in the document is encountered.
                    if (nodeDoc != null && ReferenceEquals(nodeTree.Model.Id, nodeDoc.Id))
                        break;

                    remove.Add(nodeTree.Model.Id.GlobalIndex, nodeTree);
                    remaining.Add(nodeTree.Model.Id.GlobalIndex, nodeTree);
                }

                // Remove the newly mapped children from the tree itself for now.
                foreach (TNode node in remove.Values)
                    node.Remove();
            }
            // Loop, if not all tree nodes have been removed or matched.
            while (i < treeNodes.Count && treeNodes[i] is TNode);


            // Enumerate remaining DocNodes adding to the tree either corresponding
            // TreeNodes from the map, or creating new TreeNodes as necessary.
            for (; i < docNodes.Count; i++)
            {
                nodeDoc = docNodes[i];
                TNode nodeTree;
                if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                    nodeTree = create(tree, nodeDoc);
                else if (!ReferenceEquals(nodeTree.Model, nodeDoc))
                    nodeTree.Model = nodeDoc;
                treeNodes.Insert(i, nodeTree);
            }

            if (selChanged)
                tree.FireSelectedNodeChanged();
        }
    }

    /// <summary>
    /// Implement to cause a <see cref="SequenceTree"/> to enable the mouse over
    /// and click user interface for the <see cref="PopupPickList"/>.
    /// </summary>
    public interface IShowPicker
    {
        /// <summary>
        /// Return false to disable child picking on the implementing node
        /// depending on application state.
        /// </summary>
        bool CanShow { get; }

        /// <summary>
        /// Initialize and show a <see cref="PopupPickList"/> at the specified location,
        /// containing appropriate choices for the implementing node.
        /// </summary>
        /// <param name="location">Location to show the popup</param>
        void ShowPickList(Point location);

        /// <summary>
        /// Property to determine whether the picker is showing a filtered list
        /// or not.  This determines the state of a button in the picker, and
        /// will get set/unset when the user clicks on the button.
        /// </summary>
        bool Filtered { get; set; }
    }

    /// <summary>
    /// Implement to support the <see cref="PopupPickList"/> user interface for
    /// picking children of a <see cref="SrmTreeNode"/>.
    /// </summary>
    public interface IChildPicker : IShowPicker
    {
        /// <summary>
        /// Given an object used to queue cration of a new child node, return
        /// a label to show in the <see cref="PopupPickList"/> check list.
        /// </summary>
        /// <param name="child">One of the objects returned from <see cref="GetChoices"/></param>
        /// <returns>A string label to display to the user</returns>
        string GetPickLabel(object child);

        /// <summary>
        /// Return the complete list of possible children for this node.
        /// 
        /// Any type of object may be returned, as long as it is sufficiently informative
        /// to be used in updating the child list when passed to the <see cref="Pick"/>
        /// function when the user okays the <see cref="PopupPickList"/>.
        /// </summary>
        /// <returns>All possible children for the implementing node.</returns>
        IEnumerable<object> GetChoices(bool useFilter);

        /// <summary>
        /// Determines whether two choice values are equivalent, though their exact
        /// properties may differ.
        /// </summary>
        /// <param name="choice1">First choice to compare</param>
        /// <param name="choice2">Second choice to compare</param>
        /// <returns>True if the choices represent the same element</returns>
        bool Equivalent(object choice1, object choice2);

        /// <summary>
        /// A list of currently chose children expressed with objects that support
        /// content equality with the list returned from <see cref="GetChoices"/>,
        /// since this is how the <see cref="PopupPickList"/> sets its checkboxes.
        /// </summary>
        IEnumerable<object> Chosen { get; }

        /// <summary>
        /// The implementing node is required to update the child list of its underlying
        /// <see cref="DocNode"/>, which will in turn cause the tree to update.
        /// </summary>
        /// <param name="chosen">List of objects supplied by <see cref="GetChoices"/> that the user left checked</param>
        /// <param name="autoManageChildren">True if the auto-manage bit should also be set</param>
        void Pick(IEnumerable<object> chosen, bool autoManageChildren);

        /// <summary>
        /// Whether to show the checkbox that controls whether children will be automatically added
        /// or removed if Skyline settings are changed in the future.
        /// </summary>
        bool ShowAutoManageChildren { get; }

        /// <summary>
        /// True if the parent object has auot-manage set.
        /// </summary>
        bool AutoManageChildren { get; }
    }

    /// <summary>
    /// Event arguments when new child set picked.
    /// </summary>
    public class PickedChildrenEventArgs : EventArgs
    {
        public PickedChildrenEventArgs(SrmTreeNodeParent node, IPickedList pickedList)
        {
            Node = node;
            PickedList = pickedList;
        }

        public SrmTreeNodeParent Node { get; private set; }
        public IPickedList PickedList { get; private set; }
    }

    /// <summary>
    /// Base class for child node picks, when the pick tag is simply the
    /// <see cref="Identity"/> objects of the desired nodes.
    /// </summary>
    internal abstract class AbstractPickedList : IPickedList
    {
        protected AbstractPickedList(SrmSettings settings, IEnumerable<object> picked, bool autoManageChildren)
        {
            Settings = settings;
            Picked = picked;
            AutoManageChildren = autoManageChildren;
        }

        public SrmSettings Settings { get; private set; }
        public IEnumerable<object> Picked { get; private set; }
        public bool AutoManageChildren { get; private set; }

        public IEnumerable<Identity> Chosen
        {
            get
            {
                foreach (object pick in Picked)
                    yield return GetId(pick);
            }
        }

        public abstract DocNode CreateChildNode(Identity childId);

        public abstract Identity GetId(object pick);
    }

    /// <summary>
    /// Implement to provide custom tool tips for a <see cref="SrmTreeNode"/>.
    /// </summary>
    public interface ITipProvider
    {
        /// <summary>
        /// Return false to disable tips on the implementing node depending
        /// on application state.
        /// </summary>
        bool HasTip { get; }

        /// <summary>
        /// In the process of showing a custom tip, this function is called
        /// multiple times. First, it is called with <see cref="draw"/> set to false,
        /// and a maximum size allowable for the tip client area. The implementing code
        /// is expected to return a desired size for the tip client area.  The caller
        /// may call as many times as necessary with <see cref="draw"/> set to false
        /// in order to negotiate a tip size.  The implementation must not actually
        /// draw on the <see cref="Graphics"/> supplied in these cases.
        /// 
        /// Finally, the method will be called once with <see cref="draw"/> set to true
        /// and a maximum size.  The implementation must then use the <see cref="Graphics"/>
        /// supplied to draw its tip with origin (0,0) and within the maximum size.
        /// </summary>
        /// <param name="g">Graphics to use for measuring or drawing the tip</param>
        /// <param name="sizeMax">Maximum size within which the tip must fit</param>
        /// <param name="draw">True if the implementation should paint, or false if it should measure</param>
        /// <returns>The best size for the tip that fits within the maximum specified</returns>
        Size RenderTip(Graphics g, Size sizeMax, bool draw);
    }

    public class NodeTip : CustomTip
    {
        public static string FontFace { get { return "Arial"; } }
        public static float FontSize { get { return 8f; } }

        private SrmTreeNode _nodeTree;
        private readonly Timer _timer;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        private const int NODE_SPACE_Y = 5;

        public NodeTip()
        {
            _timer = new Timer { Interval = 500 };
            _timer.Tick += Timer_Tick;
        }

        public SequenceTree SequenceTree
        {
            get { return _nodeTree == null ? null : _nodeTree.SequenceTree; }
        }

        public TreeNode Node
        {
            get { return _nodeTree; }
        }

        public void SetNode(TreeNode node, Point cursorPos)
        {
            // First make sure it supports the required types.

            if (!(node is SrmTreeNode && node is ITipProvider))
                node = null;

            if (_nodeTree != node)
            {
                _timer.Stop();
                if (Visible)
                {
                    AnimateMode animate = (Y < _nodeTree.Bounds.Y ?
                        AnimateMode.SlideTopToBottom : AnimateMode.SlideBottomToTop);
                    HideAnimate(animate);
                }
                _nodeTree = (SrmTreeNode)node;
                _moveThreshold.Location = cursorPos;
                if (node != null)
                    _timer.Start();
            }
            else if (_timer.Enabled && _moveThreshold.Moved(cursorPos))
            {
                _timer.Stop();
                _moveThreshold.Location = cursorPos;
                if (node != null)
                    _timer.Start();
            }
        }

        public override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            ITipProvider tipProvider = Node as ITipProvider;
            if (tipProvider != null)
            {
                // Render in unrestricted size, since current algorithms may
                // not render completely, if given exactly the ClientSize.
                tipProvider.RenderTip(e.Graphics, ClientSize, true);
            }
        }

        private void Timer_Tick(Object sender, EventArgs e)
        {
            _timer.Stop();
            if (SequenceTree == null || !SequenceTree.Focused)
                return;

            ITipProvider tipProvider = Node as ITipProvider;
            Debug.Assert(tipProvider != null);

            Point loc = SequenceTree.PointToScreen(Node.Bounds.Location);
            Rectangle rectScreen = Screen.GetBounds(SequenceTree);
            AnimateMode animate = AnimateMode.SlideTopToBottom;

            using (Bitmap bitmap1 = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap1))
                {
                    Size size = tipProvider.RenderTip(g, rectScreen.Size, false);
                    Rectangle rectNode = SequenceTree.RectangleToScreen(Node.Bounds);
                    int yPos = loc.Y + rectNode.Height + NODE_SPACE_Y;
                    if (yPos + size.Height > rectScreen.Bottom)
                    {
                        if (rectScreen.Bottom - yPos > rectNode.Top - NODE_SPACE_Y - rectScreen.Top)
                        {
                            size.Height = rectScreen.Bottom - yPos;

                            // Recalc size based to fit into restricted area.
                            size = tipProvider.RenderTip(g, size, false);
                        }
                        else
                        {
                            yPos = rectNode.Top - NODE_SPACE_Y;
                            if (yPos - size.Height < rectScreen.Top)
                            {
                                size.Height = yPos - rectScreen.Top;

                                // Recalc size based to fit into restricted area.
                                size = tipProvider.RenderTip(g, size, false);
                            }
                            yPos -= size.Height;
                            animate = AnimateMode.SlideBottomToTop;
                        }
                    }
                    Location = new Point(loc.X, yPos);
                    ClientSize = size;
                }
            }

            ShowAnimate(X, Y, animate);
        }
    }

    internal class RenderTools : IDisposable
    {
        bool _disposed;

        public RenderTools()
        {
            FontNormal = new Font(NodeTip.FontFace, NodeTip.FontSize);
            FontBold = new Font(NodeTip.FontFace, NodeTip.FontSize, FontStyle.Bold);
            BrushNormal = Brushes.Black;
            BrushChoice = BrushNormal;
            BrushChosen = Brushes.Blue;
            BrushSelected = Brushes.Red;
        }

        public Font FontNormal { get; private set; }
        public Font FontBold { get; private set; }
        public Brush BrushNormal { get; private set; }
        public Brush BrushChoice { get; private set; }
        public Brush BrushChosen { get; private set; }
        public Brush BrushSelected { get; private set; }

        #region IDisposable Members

        public void Dispose()
        {
            if (!_disposed)
            {
                if (FontNormal != null)
                    FontNormal.Dispose();
                if (FontBold != null)
                    FontBold.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }

    internal class TableDesc : List<RowDesc>
    {
        public const int COL_SPACING = 2;
        public const int TABLE_SPACING = 6;

        public void AddDetailRow(string name, string value, RenderTools rt)
        {
            var row = new RowDesc
                {
                    new CellDesc(name, rt) { Font = rt.FontBold },
                    new CellDesc(value, rt)
                };
            row.ColumnSpacing = COL_SPACING;
            Add(row);
        }

        public SizeF CalcDimensions(Graphics g)
        {
            SizeF size = new SizeF(0, 0);
            List<float> colWidths = new List<float>();

            foreach (RowDesc row in this)
            {
                float heightMax = 0f;

                row.CalcDimensions(g);
                for (int i = 0; i < row.Count; i++)
                {
                    if (i == colWidths.Count)
                        colWidths.Add(0f);
                    SizeF sizeCell = row[i].SizeF;
                    colWidths[i] = Math.Max(colWidths[i], sizeCell.Width);
                    // Add spacing, if this is not the last column
                    if (i < row.Count - 1)
                        colWidths[i] += row.ColumnSpacing;
                    heightMax = Math.Max(heightMax, sizeCell.Height);
                }

                // Reset the heights all to the same value
                foreach (CellDesc cell in row)
                    cell.Height = heightMax;

                size.Height += heightMax;
            }

            foreach (RowDesc row in this)
            {
                // Reset widths for each column to the same value
                for (int i = 0; i < row.Count; i++)
                    row[i].Width = colWidths[i];
            }

            // Total the widths used.
            foreach (float width in colWidths)
                size.Width += width;

            return size;
        }

        public void Draw(Graphics g)
        {
            StringFormat sf = new StringFormat();
            float y = 0f;
            foreach (RowDesc row in this)
            {
                float x = 0f;
                foreach (CellDesc cell in row)
                {
                    sf.Alignment = cell.Align;
                    sf.LineAlignment = StringAlignment.Near;
                    RectangleF rect = new RectangleF(x, y, cell.Width, cell.Height);
                    Font font = cell.Font;
                    Brush brush = cell.Brush;
                    g.DrawString(cell.Text, font, brush, rect, sf);
                    x += cell.Width;
                }
                y += row[0].Height;
            }
        }
    }

    internal class RowDesc : List<CellDesc>
    {
        public int ColumnSpacing { get; set; }

        public void CalcDimensions(Graphics g)
        {
            foreach (CellDesc cell in this)
                cell.SizeF = g.MeasureString(cell.Text, cell.Font);
        }        
    }

    internal class CellDesc
    {
        private SizeF _sizeF;

        public CellDesc(string text, RenderTools rt)
        {
            Text = text;
            Align = StringAlignment.Near;
            Font = rt.FontNormal;
            Brush = rt.BrushNormal;
        }

        public string Text { get; set; }
        public Font Font { get; set; }
        public Brush Brush { get; set; }
        public StringAlignment Align { get; set; }
        public SizeF SizeF
        {
            get { return _sizeF; }
            set { _sizeF = value; }
        }
        public float Width
        {
            get { return _sizeF.Width; }
            set { _sizeF.Width = value; }
        }
        public float Height
        {
            get { return _sizeF.Height; }
            set { _sizeF.Height = value; }
        }
    }
}
