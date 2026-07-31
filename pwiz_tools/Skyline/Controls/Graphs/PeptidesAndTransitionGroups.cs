using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Utility class to hold lists of selected peptides and transition groups.
    /// </summary>
    public class PeptidesAndTransitionGroups
    {
        private readonly HashSet<int> _setGlobalIndices = new HashSet<int>();
        public readonly List<PeptideDocNode> NodePeps = new List<PeptideDocNode>();
        public readonly List<TransitionGroupDocNode> NodeGroups = new List<TransitionGroupDocNode>();
        public readonly List<IdentityPath> GroupPaths = new List<IdentityPath>();
        public bool ProteinSelected;
        public bool ShowPeptideTotals { get { return ProteinSelected || _peptideCount > 1; } }
        private int _peptideCount;

        public void Add(IdentityPath pathPep, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            if (!_setGlobalIndices.Contains(nodeGroup.TransitionGroup.GlobalIndex))
            {
                _setGlobalIndices.Add(nodeGroup.TransitionGroup.GlobalIndex);
                NodeGroups.Add(nodeGroup);
                if (!_setGlobalIndices.Contains(nodePep.Peptide.GlobalIndex))
                {
                    _setGlobalIndices.Add(nodePep.Peptide.GlobalIndex);
                    _peptideCount++;
                }
                NodePeps.Add(nodePep);
                GroupPaths.Add(new IdentityPath(pathPep, nodeGroup.Id));
            }
        }

        public void Add(IdentityPath pathPep, PeptideDocNode nodePep)
        {
            foreach (var nodeGroups in nodePep.TransitionGroups)
                Add(pathPep, nodePep, nodeGroups);
        }

        public void Complete(int maxPeaks, int chromIndex)
        {
            if (chromIndex < 0)
                return;

            if (NodeGroups.Count <= maxPeaks)
                return;

            // Ranked by peak area rather than by peak height, which is what this used to use. Height
            // is not one of the values the columnar results keep, and reading it would mean reading
            // a chromatogram for every precursor, which is the work this method exists to avoid. The
            // two order a set of peaks almost the same way, and this only decides what a crowded
            // graph leaves out.
            var statHeights = new Statistics(NodeGroups.Select(nodeGroup => nodeGroup.GetArea(chromIndex))
                .Where(area => area.HasValue).Select(area => area.Value));
            if (statHeights.Length <= maxPeaks)
                return;

            double minHeight = statHeights.QNthItem(statHeights.Length - maxPeaks);

            var nodePeps = new List<PeptideDocNode>();
            var nodeGroups = new List<TransitionGroupDocNode>();
            var groupPaths = new List<IdentityPath>();
            if (minHeight > 0)
            {
                for (int i = 0; i < NodeGroups.Count; i++)
                {
                    var nodeGroup = NodeGroups[i];
                    if ((nodeGroup.GetArea(chromIndex) ?? 0) < minHeight)
                        continue;
                    nodePeps.Add(NodePeps[i]);
                    nodeGroups.Add(nodeGroup);
                    groupPaths.Add(GroupPaths[i]);
                }
                NodePeps.Clear();
                NodePeps.AddRange(nodePeps);
                NodeGroups.Clear();
                NodeGroups.AddRange(nodeGroups);
                GroupPaths.Clear();
                GroupPaths.AddRange(groupPaths);
            }
        }


        /// <summary>
        /// Returns the peptides that are explicitly or implictly selected in the tree view.
        /// Peptides are implicitly selected when the protein containing them is selected.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization)] // disable optimizations in hopes of tracking down NullReferenceException
        public static PeptidesAndTransitionGroups Get(IList<TreeNodeMS> selectedNodes, int resultsIndex, int maxDisplayPeptides)
        {
            var peptidesAndTransitionGroups = new PeptidesAndTransitionGroups();
            if (!Settings.Default.AllowMultiplePeptideSelection)
                return peptidesAndTransitionGroups;

            foreach (var selectedNode in selectedNodes)
            {
                // Add all peptides from a selected protein.
                var proteinNode = selectedNode as PeptideGroupTreeNode;
                if (proteinNode != null)
                {
                    peptidesAndTransitionGroups.ProteinSelected = true;
                    foreach (var nodePep in proteinNode.DocNode.Molecules)
                    {
                        peptidesAndTransitionGroups.Add(new IdentityPath(proteinNode.Path, nodePep.Id), nodePep);
                    }
                }
                else
                {
                    // Walk up the sequence tree until we find a peptide.
                    var node = (TreeNode)selectedNode;
                    while (node != null && !(node is PeptideTreeNode))
                        node = node.Parent;
                    var nodePepTree = node as PeptideTreeNode;
                    if (nodePepTree != null)
                        peptidesAndTransitionGroups.Add(nodePepTree.Path, nodePepTree.DocNode);
                }
            }

            if (peptidesAndTransitionGroups.NodePeps.Count == 0)
            {
                foreach (var selectedNode in selectedNodes)
                {
                    // Add transition groups directly.
                    var node = (TreeNode)selectedNode;
                    while (node != null && !(node is TransitionGroupTreeNode))
                        node = node.Parent;
                    if (node != null)
                    {
                        var groupTreeNode = (TransitionGroupTreeNode)node;
                        var pepTreeNode = (PeptideTreeNode)groupTreeNode.Parent;
                        if (pepTreeNode != null)
                        {
                            peptidesAndTransitionGroups.Add(pepTreeNode.Path,
                                pepTreeNode.DocNode,
                                groupTreeNode.DocNode);
                        }
                    }
                }
            }

            peptidesAndTransitionGroups.Complete(maxDisplayPeptides, resultsIndex);

            return peptidesAndTransitionGroups;

        }

        public IEnumerable<IdentityPath> GetUniquePeptidePaths()
        {
            return GroupPaths.Where(path => path.Length > (int) SrmDocument.Level.Molecules)
                .Select(path => path.GetPathTo((int) SrmDocument.Level.Molecules))
                .Distinct();
        }
    }
}