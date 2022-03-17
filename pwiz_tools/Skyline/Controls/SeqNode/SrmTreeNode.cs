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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Imaging;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.SeqNode
{
    /// <summary>
    /// Dummy <see cref="TreeNode"/> used to cause the <see cref="SequenceTree"/>
    /// to show the expand indicator without actually creating all the
    /// child nodes.
    /// </summary>
    public class DummyNode : TreeNodeMS
    {        
    }

    public class EmptyNode : TreeNodeMS
    {
        public const string TEXT_EMPTY = "                 ";

        public EmptyNode(): base(TEXT_EMPTY)
        {
        }
    }

    /// <summary>
    /// Base class for all tree node to document node mapping in the <see cref="SequenceTree"/>.
    /// </summary>
    public abstract class SrmTreeNode : TreeNodeMS, IClipboardDataProvider, ITipProvider
    {
        private const int ANNOTATION_WIDTH = 5;

        /// <summary>
        /// This must be present to support node updating before the
        /// node is inserted into the tree view, and the TreeView property
        /// is set.
        /// </summary>
        private readonly SequenceTree _sequenceTree;

        protected SrmTreeNode(SequenceTree tree, DocNode model)
        {
            _sequenceTree = tree;

            Assume.IsNotNull(model);

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
        /// Returns the start node, or an ancestor, if it is of a given type.
        /// </summary>
        /// <typeparam name="TNode">The type to look for</typeparam>
        /// <returns>The selected node or ancestor of the desired type</returns>
        public TNode GetNodeOfType<TNode>()
            where TNode : TreeNode
        {
            for (TreeNode node = this; node != null; node = node.Parent)
            {
                if (node is TNode)
                    return (TNode)node;
            }
            return null;
        }

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

        public override void DrawNodeCustom(Graphics g, int rightEdge)
        {
            base.DrawNodeCustom(g, rightEdge);

            // Draw color rectangle for a peptide if multiple peptides are selected.
            if (Settings.Default.AllowMultiplePeptideSelection && Document.Settings.HasResults &&
                (IsInSelection || (Parent != null && ((TreeNodeMS)Parent).IsInSelection)))
            {
                var color = ChromColor;
                if (color.HasValue)
                {
                    // No color if this is the only selected peptide.
                    bool multiSelect = false;
                    foreach (var node in TreeViewMS.SelectedNodes)
                    {
                        if ((node is PeptideTreeNode && !ReferenceEquals(node, this)) ||
                            node is PeptideGroupTreeNode)
                        {
                            multiSelect = true;
                            break;
                        }
                    }
                    if (multiSelect)
                    {
                        // Draw the peptide color rectangle: white background to erase text underneath,
                        // black border, and the peptide color in the interior.
                        const int imgWidth = TreeViewMS.IMG_WIDTH, imgHeight = TreeViewMS.IMG_WIDTH;
                        const int colorRectWidth = imgWidth;
                        const int colorRectHeight = imgHeight;
                        rightEdge -= imgWidth + 3;
                        int top = BoundsMS.Top + (BoundsMS.Height - imgHeight) / 2;
                        var backgroundRect = new Rectangle(rightEdge - 2, top, colorRectWidth + 7, colorRectHeight);
                        g.FillRectangle(Brushes.White, backgroundRect);
                        var colorRect = new Rectangle(rightEdge, top, colorRectWidth - 2, colorRectHeight - 3);
                        g.FillRectangle(new SolidBrush(color.Value), colorRect);

                        // Draw red outline around selected peptide if the tree isn't focused.
                        if (IsSelected && !TreeView.Focused)
                        {
                            var bounds = BoundsMS;
                            bounds.Width--;
                            bounds.Height--;
                            g.DrawRectangle(new Pen(Color.Red), bounds);
                        }
                    }
                }
            }
        }

        public virtual Color? ChromColor
        {
            get { return null; }
        }

        protected override void DrawTextMS(Graphics g)
        {
            base.DrawTextMS(g);
            
            DrawAnnotationIndicator(g);
        }

        protected void DrawAnnotationIndicator(Graphics g)
        {
            if (!Model.Annotations.IsEmpty)
            {
                var bounds = BoundsMS;
                int zoomFactor = 0;
                if (Equals(Settings.Default.TextZoom, TreeViewMS.LRG_TEXT_FACTOR))
                    zoomFactor = 1;
                if (Equals(Settings.Default.TextZoom, TreeViewMS.XLRG_TEXT_FACTOR))
                    zoomFactor = 2;
                g.FillPolygon(Model.Annotations.ColorBrush, new[]
                    {
                        new Point(bounds.Right, bounds.Top),
                        new Point(bounds.Right - (ANNOTATION_WIDTH + zoomFactor), bounds.Top),
                        new Point(bounds.Right, bounds.Top + ANNOTATION_WIDTH + zoomFactor)
                    });
            }
        }

        public bool ShowAnnotationTipOnly { get; set; }

        public virtual bool HasTip
        {
            get { return !Model.Annotations.IsEmpty; }
        }

        protected static string FormatAdductTip(Adduct adduct)
        {
            return adduct.IsProteomic
                ? string.Format(adduct.AdductCharge.ToString(LocalizationHelper.CurrentCulture))
                : string.Format(@"{0} ({1})", adduct.AdductCharge.ToString(LocalizationHelper.CurrentCulture), adduct.AdductFormula);
        }

        public virtual Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (RenderTools rt = new RenderTools())
            {
                bool lastMultiLine = false;
                var annotations = Model.Annotations;
                if (!String.IsNullOrEmpty(annotations.Note))
                {
                    lastMultiLine = table.AddDetailRowLineWrap(g, Resources.SrmTreeNode_RenderTip_Note, annotations.Note, rt); 
                    // L10N: I'm not completely sure if the user will see this, but in AddDetailRowLineWrap it seems like
                        // the function displays the values.
                }
                foreach (var annotation in annotations.ListAnnotations())
                {
                    string annotationName = annotation.Key;
                    var annotationValue = annotation.Value;
                    var def = Document.Settings.DataSettings.AnnotationDefs.FirstOrDefault(annotationDef => 
                        Equals(annotationDef.Name, annotationName));
                    if (def == null)
                    {
                        // This should not be possible, but it has happened.  Rather than cause an unhandled
                        // exception, just guess that anything where the annotation name and value are equal
                        // is a True/False type set to True, and display everything else as usual.
                        if (Equals(annotationName, annotationValue))
                            annotationValue = Resources.SrmTreeNode_RenderTip_True;
                        // L10N: I'm not completely sure if the user will see this, but in AddDetailRowLineWrap it seems like
                        // the function displays the values.
                    }
                    else if (def.Type == AnnotationDef.AnnotationType.true_false)
                    {
                        annotationValue = annotationValue != null
                                              ? Resources.SrmTreeNode_RenderTip_True
                                              : Resources.SrmTreeNode_RenderTip_False;
                        // L10N: I'm not completely sure if the user will see this, but in AddDetailRowLineWrap it seems like
                        // the function displays the values.
                    }
                    // If the last row was multi-line, add a spacer line.
                    if (lastMultiLine)
                        table.AddDetailRow(@" ", @" ", rt);
                    lastMultiLine = table.AddDetailRowLineWrap(g, annotationName, annotationValue, rt);
                }
                SizeF size = table.CalcDimensions(g);
                // Add an extra pixel, because Chinese touches the border otherwise
                if (size.Height > 0)
                {
                    if (draw)
                    {
                        // Extra pixel above for Chinese characters
                        g.TranslateTransform(0, 1);
                        table.Draw(g);
                    }
                    // If not showing annotations only, separate any annotations from
                    // the rest of the tip by some space.
                    if (!ShowAnnotationTipOnly)
                    {
                        size.Height += TableDesc.TABLE_SPACING;
                    }
                }
                // Extra pixel above for Chinese characters
                size.Height++;

                var width = Math.Min(sizeMax.Width, size.Width);
                var height = Math.Min(sizeMax.Height, size.Height);
                // Add 2 pixels extra padding in the annotations-only case.
                if (ShowAnnotationTipOnly)
                {
                    width += 2;
                    height += 2;
                }
                return new Size((int)Math.Round(width), (int)Math.Round(height));
            }
        }

        public DataObject ProvideData()
        {
            return GetNodeData();
        }

        protected virtual DataObject GetNodeData()
        {
            DataObject data = new DataObject();
            data.SetData(DataFormats.Text, Text);
            data.SetData(DataFormats.Html, HtmlFragment.ClipBoardText(Text));

            return data;
        }
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

        public void CollapseAndClear()
        {
            if (IsExpanded)
                Collapse();
            if (Nodes.Count > 0 && !(Nodes[0] is DummyNode))
            {
                Nodes.Clear();
                Nodes.Add(new DummyNode());
            }
        }

        /// <summary>
        /// Called when children are updated during model changes.
        /// </summary>
        /// <param name="expandDefault">True if this type of node is expanded by default</param>
        protected void OnUpdateChildren(bool expandDefault)
        {
            int countChildNodes = Nodes.Count;
            // Only update children tree nodes if expanded or expanding by default and there are zero children now
            UpdateChildren(IsExpanded || (expandDefault && countChildNodes == 0));
            if (countChildNodes == 0 && Nodes.Count > 0 && expandDefault)
                Expand();
        }

        protected abstract void UpdateChildren(bool materialize);

        #region IChildPicker Members

        public virtual bool CanShow { get { return true; } }

        public void ShowPickList(Point location, bool okOnDeactivate)
        {
            Exception exception = null;
            try
            {
                PopupPickList popup = new PopupPickList(this, ChildHeading, okOnDeactivate) { Location = location };
                popup.Show(FormEx.GetParentForm(TreeView));
                popup.Focus();
            }
            // Deal with apparent race condition with this timer-driven call and deletion of the associated node
            catch (ObjectDisposedException)
            {
                return; // Ignore
            }
            // Catch exceptions that may be caused trying to read results information from SKYD file
            catch (IOException x)
            {
                exception = x;
            }
            catch (UnauthorizedAccessException x)
            {
                exception = x;
            }

            if (exception != null)
            {
                MessageDlg.Show(Program.MainWindow, TextUtil.LineSeparate(Resources.SrmTreeNodeParent_ShowPickList_An_error_occurred_creating_options_, exception.Message));                
            }
        }

        public abstract bool Filtered { get; set; }

        public virtual string GetPickLabel(DocNode child)
        {
            return child.ToString();
        }

        public virtual bool DrawPickLabel(DocNode child, Graphics g, Rectangle bounds, ModFontHolder fonts, Color foreColor, Color backColor)
        {
            // Do nothing by default, and let the pick list render the label text
            return false;
        }

        public abstract Image GetPickTypeImage(DocNode child);

        public abstract Image GetPickPeakImage(DocNode child);

        public abstract ITipProvider GetPickTip(DocNode child);

        public abstract IEnumerable<DocNode> GetChoices(bool useFilter);

        public virtual IEnumerable<DocNode> Chosen
        {
            get { return ChildDocNodes; }
        }

        protected void MergeChosen(IList<DocNode> choices, bool useFilter)
        {
            MergeChosen(choices, useFilter, node => node.Id);
        }

        protected void MergeChosen<TKey>(IList<DocNode> choices, bool useFilter,
                                         Func<DocNode, TKey> keySelector)
        {
            // Store currently chosen items in a list
            var listChosen = Chosen.ToList();
            // Build a dictionary of them by node specific key type, with the
            // locations where they are stored in the list of choices.  This has
            // to be done carefully, because while auto-generated nodes will have
            // unique keys, manually created nodes may not.
            var dictChosen = new Dictionary<TKey, InsertNodeLocation>();
            // And global indices of the chosen to avoid duplicating such manually created nodes
            var dictChosenGlobalIndices = new Dictionary<int, InsertNodeLocation>();
            foreach (var node in listChosen)
            {
                var nodeChosen = new InsertNodeLocation(node);
                dictChosenGlobalIndices.Add(node.Id.GlobalIndex, nodeChosen);
                var key = keySelector(node);
                if (!dictChosen.ContainsKey(key))
                    dictChosen.Add(key, nodeChosen);
            }
            
            // Replace nodes in the choices list with matching nodes in the
            // chosen list.
            int insertedNodeCount = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var key = keySelector(choice);
                InsertNodeLocation nodeChosen;
                if (dictChosenGlobalIndices.TryGetValue(choice.Id.GlobalIndex, out nodeChosen) ||
                    dictChosen.TryGetValue(key, out nodeChosen))
                {
                    choices[i] = nodeChosen.InsertNode;
                    nodeChosen.Location = i;
                    insertedNodeCount++;
                }
            }

            // If not all nodes were added, and this is not supposed to be a
            // filtered set, add the remainig nodes to the list.
            if (insertedNodeCount < listChosen.Count && !useFilter)
            {
                int iLast = listChosen.Count;
                int iLastChoice = choices.Count;
                for (int i = iLast - 1; i >= 0; i--)
                {
                    // If this item was not added to the list of choices,
                    // continue until we find the next item that as.
                    var nodeChosen = dictChosen[keySelector(listChosen[i])];
                    if (nodeChosen.Location == -1)
                        continue;

                    // If the last item that was inserted into the list of choices
                    // was not the item immediately preceding the current item,
                    // loop until everything between the current item and the
                    // last item gets inserted now.
                    int iFirstChoice = nodeChosen.Location + 1;
                    while (iLast - i > 1)
                    {
                        iLast--;
                        var nodeInsert = listChosen[iLast];
                        if (iFirstChoice < iLastChoice)
                            iLastChoice = GetPickInsertIndex(nodeInsert, choices, iFirstChoice, iLastChoice);
                        choices.Insert(iLastChoice, nodeInsert);
                    }

                    iLast = i;
                    iLastChoice = nodeChosen.Location;
                }
                // Handle any remaining nodes at the head of the chosen list
                while (iLast > 0)
                {
                    iLast--;
                    var nodeInsert = listChosen[iLast];
                    if (0 < iLastChoice)
                        iLastChoice = GetPickInsertIndex(nodeInsert, choices, 0, iLastChoice);
                    choices.Insert(iLastChoice, nodeInsert);
                }
            }
        }

        protected virtual int GetPickInsertIndex(DocNode node, IList<DocNode> choices, int iFirst, int iLast)
        {
            // By default insert into the first available location
            return iFirst;
        }

        private sealed class InsertNodeLocation
        {
            public InsertNodeLocation(DocNode insertNode)
            {
                InsertNode = insertNode;
                Location = -1;
            }

            public DocNode InsertNode { get; private set; }
            public int Location { get; set; }
        }

        public abstract bool ShowAutoManageChildren { get; }

        public bool AutoManageChildren
        {
            get { return ((DocNodeParent) Model).AutoManageChildren; }
        }

        public virtual string SynchSiblingsLabel
        {
            get { return null; }
        }

        public virtual bool IsSynchSiblings
        {
            get { return false; }
            set { /* Ignore */  }
        }

        public void Pick(IEnumerable<DocNode> chosen, bool autoManageChildren, bool synchSiblings)
        {
            // Quick check to see if anything changed
            var chosenNodes = chosen.ToArray();
            if (Helpers.Equals(chosenNodes, Chosen) && AutoManageChildren == autoManageChildren && IsSynchSiblings == synchSiblings)
                return;

            SequenceTree.FirePickedChildren(this, new ChildPickedList(chosenNodes, autoManageChildren), synchSiblings);

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
        public delegate TNode CreateTreeNode<out TNode>(SequenceTree tree, DocNode nodeDoc)
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
        /// <param name="changeAll"></param>
        public static void UpdateNodes<TNode>(SequenceTree tree, TreeNodeCollection treeNodes,
                IList<DocNode> docNodes, bool materialize, CreateTreeNode<TNode> create, bool changeAll = false)
            where TNode : SrmTreeNode
        {
            // This code is highly optimized to make as few modifications to the
            // tree as possible, as they can have negative impact on the selection.

            // First short-cut all the complexity, if the end result will be an
            // empty list.  This is way faster at removing all the proteins in the
            // File/New case.
            if (docNodes.Count == 0 || changeAll)
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
                // Avoid updating a lot of nodes just because a parent tree node was opened
                if (docNodes.Count > 1000)
                    treeNodes.Clear();
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
                    if (!ReferenceEquals(nodeTree.Model, nodeDoc))
                    {
                        if (ReferenceEquals(nodeTree.Model.Id, nodeDoc.Id) ||
                            ReferenceEquals(nodeTree.Model.Id, nodeDoc.ReplacedId)) // Node was replaced by a different Id, but should not change selection
                        {
                            nodeTree.Model = nodeDoc;
                            selChanged = ReferenceEquals(nodeTree, nodeSel);
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
                                selChanged = ReferenceEquals(nodeTree, nodeSel);
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


            int firstInsertPosition = i;
            List<TreeNode> nodesToInsert = new List<TreeNode>(docNodes.Count - firstInsertPosition);
            // Enumerate remaining DocNodes adding to the tree either corresponding
            // TreeNodes from the map, or creating new TreeNodes as necessary.
            for (; i < docNodes.Count; i++)
            {
                nodeDoc = docNodes[i];
                TNode nodeTree;
                if (!remaining.TryGetValue(nodeDoc.Id.GlobalIndex, out nodeTree))
                {
                    nodeTree = create(tree, nodeDoc);
                    nodesToInsert.Add(nodeTree);
                }
                else
                {
                    nodesToInsert.Add(nodeTree);
                }
                if (tree.ShowReplicate == ReplicateDisplay.best)
                    nodeTree.Model = nodeDoc;
            }

            if (firstInsertPosition == treeNodes.Count)
            {
                treeNodes.AddRange(nodesToInsert.ToArray());
            }
            else
            {
                for (int insertNodeIndex = 0; insertNodeIndex < nodesToInsert.Count; insertNodeIndex++)
                {
                    treeNodes.Insert(insertNodeIndex + firstInsertPosition, nodesToInsert[insertNodeIndex]);
                }
            }

            // If necessary, update the model for these new tree nodes. This needs to be done after 
            // the nodes have been added to the tree, because otherwise the icons may not update correctly.
            for (int insertNodeIndex = 0; insertNodeIndex < nodesToInsert.Count; insertNodeIndex++)
            {
                var nodeTree = (TNode) treeNodes[insertNodeIndex + firstInsertPosition];
                var docNode = docNodes[insertNodeIndex + firstInsertPosition];
                // Best replicate display, requires that the node have correct
                // parenting, before the text and icons can be set correctly.
                // So, force a model change to update those values.
                // Also, TransitionGroups and Transitions need to know their Peptide in order
                // to display ratios, so they must be updated when their parent changes
                if (tree.ShowReplicate == ReplicateDisplay.best 
                    || docNode is TransitionGroupDocNode
                    || docNode is TransitionDocNode
                    || !ReferenceEquals(docNode, nodeTree.Model))
                {
                    nodeTree.Model = docNode;
                }
            }

            if (selChanged)
                tree.FireSelectedNodeChanged();
            else
                tree.SelectedNode = nodeSel;
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
        /// <param name="okOnDeactivate">Causes OnOk when the form is deactivated when true</param>
        void ShowPickList(Point location, bool okOnDeactivate);

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
        /// Return the complete list of possible children for this node.
        /// 
        /// Any type of object may be returned, as long as it is sufficiently informative
        /// to be used in updating the child list when passed to the <see cref="Pick"/>
        /// function when the user okays the <see cref="PopupPickList"/>.
        /// </summary>
        /// <returns>All possible children for the implementing node.</returns>
        IEnumerable<DocNode> GetChoices(bool useFilter);

        /// <summary>
        /// A list of currently chose children expressed with objects that support
        /// content equality with the list returned from <see cref="GetChoices"/>,
        /// since this is how the <see cref="PopupPickList"/> sets its checkboxes.
        /// </summary>
        IEnumerable<DocNode> Chosen { get; }

        /// <summary>
        /// Given an object used to queue cration of a new child node, return
        /// a label to show in the <see cref="PopupPickList"/> check list.
        /// </summary>
        /// <param name="child">One of the objects returned from <see cref="GetChoices"/></param>
        /// <returns>A string label to display to the user</returns>
        string GetPickLabel(DocNode child);

        /// <summary>
        /// Allows the child pricker to draw the labels for the children in the
        /// popup pick list.
        /// </summary>
        /// <param name="child">Item to draw</param>
        /// <param name="g"><see cref="Graphics"/> object to draw in</param>
        /// <param name="bounds">Rectangle to draw in</param>
        /// <param name="fonts">Base font to use for text</param>
        /// <param name="foreColor">Text color</param>
        /// <param name="backColor">Background color</param>
        /// <returns>true if custom drawing was performed, false otherise</returns>
        bool DrawPickLabel(DocNode child, Graphics g, Rectangle bounds, ModFontHolder fonts, Color foreColor, Color backColor);

        /// <summary>
        /// Gets the type image that will be displayed in the sequence tree, if
        /// a given node is picked.
        /// </summary>
        /// <param name="child">Item for which image is requested</param>
        /// <returns>An image to display beside the pick label</returns>
        Image GetPickTypeImage(DocNode child);

        /// <summary>
        /// Gets the peak image that will be displayed in the sequence tree, if
        /// a given node is picked.
        /// </summary>
        /// <param name="child">Item for which image is requested</param>
        /// <returns>An image to display beside the pick label</returns>
        Image GetPickPeakImage(DocNode child);

        /// <summary>
        /// Gets a <see cref="ITipProvider"/> for a specific child.
        /// </summary>
        /// <param name="child">The child for which a tip is requested</param>
        /// <returns>A tip provider that can be used to show a tip about the child</returns>
        ITipProvider GetPickTip(DocNode child);        

        /// <summary>
        /// The implementing node is required to update the child list of its underlying
        /// <see cref="DocNode"/>, which will in turn cause the tree to update.
        /// </summary>
        /// <param name="chosen">List of objects supplied by <see cref="GetChoices"/> that the user left checked</param>
        /// <param name="autoManageChildren">True if the auto-manage bit should also be set</param>
        /// <param name="synchSiblings">True if siblings of this node should be synchronized with it</param>
        void Pick(IEnumerable<DocNode> chosen, bool autoManageChildren, bool synchSiblings);

        /// <summary>
        /// Whether to show the checkbox that controls whether children will be automatically added
        /// or removed if Skyline settings are changed in the future.
        /// </summary>
        bool ShowAutoManageChildren { get; }

        /// <summary>
        /// True if the parent object has auot-manage set.
        /// </summary>
        bool AutoManageChildren { get; }

        /// <summary>
        /// The label to show the user for the synchronize checkbox, or null if the checkbox
        /// should not be shown.
        /// </summary>
        string SynchSiblingsLabel { get; }

        /// <summary>
        /// True if the synchronize checkbox should be on when the pick list is shown.
        /// </summary>
        bool IsSynchSiblings { get; set; }
    }

    /// <summary>
    /// Event arguments when new child set picked.
    /// </summary>
    public class PickedChildrenEventArgs : EventArgs
    {
        public PickedChildrenEventArgs(SrmTreeNodeParent node, IPickedList pickedList, bool synchSiblings)
        {
            Node = node;
            PickedList = pickedList;
            IsSynchSiblings = synchSiblings;
        }

        public SrmTreeNodeParent Node { get; private set; }
        public IPickedList PickedList { get; private set; }
        public bool IsSynchSiblings { get; private set; }
    }

    /// <summary>
    /// .
    /// </summary>
    internal class ChildPickedList : IPickedList
    {
        public ChildPickedList(IEnumerable<DocNode> picked, bool autoManageChildren)
        {
            Chosen = picked;
            AutoManageChildren = autoManageChildren;
        }

        public IEnumerable<DocNode> Chosen { get; private set; }
        public bool AutoManageChildren { get; private set; }
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

    /// <summary>
    /// Implement to enable a control to display tool tips.
    /// </summary>
    public interface ITipDisplayer
    {
        /// <summary>
        /// Gets the bounds of the control in which the tip is displayed.
        /// </summary>
        Rectangle ScreenRect { get; }

        /// <summary>
        /// Indicates if the tip should be displayed.
        /// </summary>
        bool AllowDisplayTip { get; }

        /// <summary>
        /// Gets the screen coordinates of the given rectangle, 
        /// which in this case is the node whose tip we are displaying.
        /// </summary>
        Rectangle RectToScreen(Rectangle r);
    }

    public class NodeTip : CustomTip
    {
        public static string FontFace { get { return @"Arial"; } }
        public static float FontSize { get { return 8f; } }

        public static int TipDelayMs { get { return 500; } }

        private ITipProvider _tipProvider;
        private readonly ITipDisplayer _tipDisplayer;
        private Rectangle _rectItem;
        private Timer _timer;
        private readonly MoveThreshold _moveThreshold = new MoveThreshold(5, 5);

        private const int NODE_SPACE_Y = 5;

        public NodeTip(ITipDisplayer tipDisplayer)
        {
            _timer = new Timer { Interval = TipDelayMs };
            _timer.Tick += Timer_Tick;
            _tipDisplayer = tipDisplayer;
        }

        protected override void Dispose(bool disposing)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            base.Dispose(disposing);
        }

        public void HideTip()
        {
            SetTipProvider(null, new Rectangle(), new Point());
        }

        public void SetTipProvider(ITipProvider tipProvider, Rectangle rectItem, Point cursorPos)
        {
            if (!_moveThreshold.Moved(cursorPos)) 
                return;
            _timer.Stop();
            if (Visible)
            {
                AnimateMode animate = (Y < _rectItem.Y ?
                AnimateMode.SlideTopToBottom : AnimateMode.SlideBottomToTop);
                HideAnimate(animate);
            }
            _tipProvider = tipProvider;
            _rectItem = _tipDisplayer.RectToScreen(rectItem);
            _moveThreshold.Location = cursorPos;
            if (tipProvider != null)
                _timer.Start();
        }

        public override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_tipProvider != null)
            {
                // Render in unrestricted size, since current algorithms may
                // not render completely, if given exactly the ClientSize.
                _tipProvider.RenderTip(e.Graphics, ClientSize, true);
            }
        }

        private void Timer_Tick(Object sender, EventArgs e)
        {
            _timer.Stop();
            if (_tipDisplayer == null || !_tipDisplayer.AllowDisplayTip)
                return;

            Rectangle rectScreen = _tipDisplayer.ScreenRect;
            AnimateMode animate = AnimateMode.SlideTopToBottom;

            using (Bitmap bitmap1 = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap1))
                {
                    Size size = _tipProvider.RenderTip(g, rectScreen.Size, false);
                    int yPos = _rectItem.Y + _rectItem.Height + NODE_SPACE_Y;
                    if (yPos + size.Height > rectScreen.Bottom)
                    {
                        if (rectScreen.Bottom - yPos > _rectItem.Top - NODE_SPACE_Y - rectScreen.Top)
                        {
                            size.Height = rectScreen.Bottom - yPos;

                            // Recalc size based to fit into restricted area.
                            size = _tipProvider.RenderTip(g, size, false);
                        }
                        else
                        {
                            yPos = _rectItem.Top - NODE_SPACE_Y;
                            if (yPos - size.Height < rectScreen.Top)
                            {
                                size.Height = yPos - rectScreen.Top;

                                // Recalc size based to fit into restricted area.
                                size = _tipProvider.RenderTip(g, size, false);
                            }
                            yPos -= size.Height;
                            animate = AnimateMode.SlideBottomToTop;
                        }
                    }
                    Location = new Point(_rectItem.X, yPos);
                    ClientSize = size;
                }
            }

            ShowAnimate(X, Y, animate); // Not really animated anymore, because of GDI handle leak on Windows 10
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

        private const string X80 =
        @"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

        /// <summary>
        /// Adds a text column a with potential line wrap.
        /// </summary>
        /// <param name="g">The graphics object in which the text will be rendered</param>
        /// <param name="name">Field name</param>
        /// <param name="value">Field value text</param>
        /// <param name="rt">Rendering tools used to render the text</param>
        /// <returns>True if the text was multi-line</returns>
        public bool AddDetailRowLineWrap(Graphics g, string name, string value, RenderTools rt)
        {
            SizeF sizeX80 = g.MeasureString(X80, rt.FontNormal);
            float widthLine = sizeX80.Width;
            var words = value.Split(TextUtil.SEPARATOR_SPACE);
            string line = string.Empty;
            bool firstRow = true;
            // This is a little bit strange, but it works.  Because the split call
            // splits only on spaces, newlines are preserved, and MeasureString will
            // account for them.  So, only when a line gets too long will it be wrapped
            // by creating a new row.  This does mean, however, that firstRow is not
            // a valid indicator on its own of whether the text is multi-line.
            foreach (string word in words)
            {
                if (g.MeasureString(TextUtil.SpaceSeparate(line + word,string.Empty), rt.FontNormal).Width > widthLine)
                {
                    AddDetailRow(firstRow ? name : string.Empty, line, rt);
                    line = string.Empty;
                    firstRow = false;
                }
                line += word + TextUtil.SEPARATOR_SPACE;
            }
            AddDetailRow(firstRow ? name : string.Empty, line, rt);
            // The text is multi-line if either it required wrapping to multiple rows,
            // or it contains new-line characters.
            return !firstRow || value.Contains('\n');
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
                    colWidths[i] += i < row.Count - 1 ? row.ColumnSpacing : 1;
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

    public class DisplaySettings
    {
        internal readonly bool _showBestReplicate;
        internal readonly int _resultsIndex;

        public DisplaySettings(NormalizedValueCalculator normalizedValueCalculator, PeptideDocNode nodePep, bool showBestReplicate, int resultsIndex, NormalizeOption normalizeOption)
        {
            NormalizedValueCalculator = normalizedValueCalculator;
            _showBestReplicate = showBestReplicate;
            _resultsIndex = resultsIndex;
            NormalizeOption = normalizeOption;
            NodePep = nodePep;
        }
         
        public DisplaySettings(DisplaySettings settings, PeptideDocNode nodePep) 
        {
            _showBestReplicate = settings._showBestReplicate;
            _resultsIndex = settings._resultsIndex;
            NormalizeOption = settings.NormalizeOption;
            NodePep = nodePep;
        }

        public PeptideDocNode NodePep { get; private set; }

        public int ResultsIndex
        {
            get
            {
                return _showBestReplicate && NodePep != null && NodePep.BestResult != -1 ? NodePep.BestResult : _resultsIndex;
            }
        }

        public NormalizeOption NormalizeOption { get; private set; }

        public NormalizationMethod NormalizationMethod
        {
            get
            {
                return NormalizedValueCalculator.NormalizationMethodForMolecule(NodePep, NormalizeOption);
            }
        }

        public NormalizedValueCalculator NormalizedValueCalculator { get; private set; }
    }
}
