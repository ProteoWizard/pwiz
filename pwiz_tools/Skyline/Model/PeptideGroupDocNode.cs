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
using System.Diagnostics;
using System.IO;
using System.Linq;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideGroupDocNode : DocNodeParent
    {
        private string _name;
        private string _description;

        public PeptideGroupDocNode(PeptideGroup id, string name, string description, PeptideDocNode[] children)
            : this(id, Annotations.EMPTY, name, description, children)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, string name, string description,
                                   PeptideDocNode[] children)
            : this(id, annotations, name, description, children, true)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, String name, String description,
                                   PeptideDocNode[] children, bool autoManageChildren)
            : base (id, annotations, children, autoManageChildren)
        {
            _name = name;
            _description = description;
        }

        public PeptideGroup PeptideGroup { get { return (PeptideGroup)Id; } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget
        {
            get { return AnnotationDef.AnnotationTarget.protein; }
        }

        public bool IsPeptideList { get { return !(PeptideGroup is FastaSequence); } }
        public bool IsDecoy { get { return PeptideGroup.IsDecoy; } }

        public string Name { get { return PeptideGroup.Name ?? _name; } }
        public string Description { get { return PeptideGroup.Description ?? _description; } }

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { Peptides, TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int PeptideCount { get { return GetCount((int)Level.Peptides); } }
        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public IEnumerable<PeptideDocNode> Peptides { get { return Children.Cast<PeptideDocNode>(); } }

        public PeptideGroupDocNode ChangeName(string name)
        {
            // Only allow set, if the id object has no name
            Debug.Assert(PeptideGroup.Name == null);
            return ChangeProp(ImClone(this), (im, v) => im._name = v, name);
        }

        public PeptideGroupDocNode ChangeDescription(string desc)
        {
            // Only allow set, if the id object has no name
            Debug.Assert(PeptideGroup.Description == null);
            return ChangeProp(ImClone(this), (im, v) => im._description = v, desc);
        }

        public PeptideGroupDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff)
        {
            if (diff.DiffPeptides && settingsNew.PeptideSettings.Filter.AutoSelect && AutoManageChildren)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                int countPeptides = 0;
                int countIons = 0;

                Dictionary<int, DocNode> mapIndexToChild = CreateGlobalIndexToChildMap();
                Dictionary<PeptideModKey, DocNode> mapIdToChild = CreatePeptideModToChildMap();

                foreach(PeptideDocNode nodePep in GetPeptideNodes(settingsNew, true))
                {
                    PeptideDocNode nodePepResult = nodePep;
                    SrmSettingsDiff diffNode = SrmSettingsDiff.ALL;

                    DocNode existing;
                    // Add values that existed before the change. First check for exact match by
                    // global index, which will happen when explicit modifications are added,
                    // and then by content identity.
                    if (mapIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out existing) ||
                        mapIdToChild.TryGetValue(nodePep.Key, out existing))
                    {
                        nodePepResult = (PeptideDocNode) existing;
                        diffNode = diff;
                    }

                    if (nodePepResult != null)
                    {
                        // Materialize children of the peptide.
                        nodePepResult = nodePepResult.ChangeSettings(settingsNew, diffNode);

                        childrenNew.Add(nodePepResult);

                        // Make sure a single peptide group does not exceed document limits.
                        countPeptides++;
                        countIons += nodePepResult.TransitionCount;
                        if (countIons > SrmDocument.MAX_TRANSITION_COUNT ||
                            countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                            throw new InvalidDataException("Document size limit exceeded.");
                    }
                }

                if (PeptideGroup.Sequence != null)
                    childrenNew = PeptideGroup.RankPeptides(childrenNew, settingsNew, true);

                return (PeptideGroupDocNode) ChangeChildrenChecked(childrenNew);
            }
            else
            {
                var nodeResult = this;

                if (diff.DiffPeptides && diff.SettingsOld != null)
                {
                    // If variable modifications changed, remove all peptides with variable
                    // modifications which are no longer possible.
                    var modsNew = settingsNew.PeptideSettings.Modifications;
                    var modsVarNew = modsNew.VariableModifications.ToArray();
                    var modsOld = diff.SettingsOld.PeptideSettings.Modifications;
                    var modsVarOld = modsOld.VariableModifications.ToArray();
                    if (modsNew.MaxVariableMods < modsOld.MaxVariableMods ||
                        !ArrayUtil.EqualsDeep(modsVarNew, modsVarOld))
                    {
                        IList<DocNode> childrenNew = new List<DocNode>();
                        foreach (PeptideDocNode nodePeptide in nodeResult.Children)
                        {
                            if (nodePeptide.AreVariableModsPossible(modsNew.MaxVariableMods, modsVarNew))
                                childrenNew.Add(nodePeptide);
                        }

                        nodeResult = (PeptideGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                    }
                }

                // Check for changes affecting children
                if (diff.DiffPeptideProps || diff.DiffExplicit ||
                    diff.DiffTransitionGroups || diff.DiffTransitionGroupProps ||
                    diff.DiffTransitions || diff.DiffTransitionProps ||
                    diff.DiffResults)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();

                    // Enumerate the nodes making necessary changes.
                    foreach (PeptideDocNode nodePeptide in nodeResult.Children)
                        childrenNew.Add(nodePeptide.ChangeSettings(settingsNew, diff));

                    if (ArrayUtil.ReferencesEqual(childrenNew, Children))
                        childrenNew = Children;

                    nodeResult = (PeptideGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }
                return nodeResult;
            }
        }

        public PeptideGroupDocNode Merge(PeptideGroupDocNode nodePepGroup)
        {
            var childrenNew = new List<PeptideDocNode>(Children.Cast<PeptideDocNode>());
            // Remember where all the existing children are
            var dictPepIndex = new Dictionary<PeptideModKey, int>();
            for (int i = 0; i < childrenNew.Count; i++)
            {
                var key = childrenNew[i].Key;
                if (!dictPepIndex.ContainsKey(key))
                    dictPepIndex[key] = i;
            }
            // Add the new children to the end, or merge when the peptide is already present
            foreach (PeptideDocNode nodePep in nodePepGroup.Children)
            {
                int i;
                if (dictPepIndex.TryGetValue(nodePep.Key, out i))
                    childrenNew[i] = childrenNew[i].Merge(nodePep);
                else
                    childrenNew.Add(nodePep);
                    
            }
            // If it is a FASTA sequence, make sure new peptides are sorted into place
            if (PeptideGroup is FastaSequence && childrenNew.Count > Children.Count)
                childrenNew.Sort(FastaSequence.ComparePeptides);
            return (PeptideGroupDocNode) ChangeChildrenChecked(childrenNew.Cast<DocNode>().ToArray());
        }

        public PeptideGroupDocNode EnsureChildren(SrmSettings settings, bool peptideList)
        {
            var result = this;
            // Check if children will change as a result of ChangeSettings.
            var changed = result.ChangeSettings(settings, SrmSettingsDiff.ALL);
            if (result.AutoManageChildren && !AreEquivalentChildren(result.Children, changed.Children))
            {
                changed = result = (PeptideGroupDocNode) result.ChangeAutoManageChildren(false);
                changed = changed.ChangeSettings(settings, SrmSettingsDiff.ALL);
            }
            // Match children resulting from ChangeSettings to current children.
            var dictIndexToChild = Children.ToDictionary(child => child.Id.GlobalIndex);
            var listChildren = new List<DocNode>();
            foreach (PeptideDocNode nodePep in changed.Children)
            {
                DocNode child;
                if (dictIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out child))
                    listChildren.Add(((PeptideDocNode) child).EnsureChildren(settings, peptideList));
            }
            return (PeptideGroupDocNode) result.ChangeChildrenChecked(listChildren);
        }

        public static bool AreEquivalentChildren(IList<DocNode> children1, IList<DocNode> children2)
        {
            if (children1.Count != children2.Count)
                return false;
            for (int i = 0; i < children1.Count; i++)
            {
                if(!Equals(((PeptideDocNode) children1[i]).Key, ((PeptideDocNode) children2[i]).Key))
                    return false;
            }
            return true;
        }
    

        public IEnumerable<PeptideDocNode> GetPeptideNodes(SrmSettings settings, bool useFilter)
        {
            // FASTA sequences can generate a comprehensive list of available peptides.
            FastaSequence fastaSeq = Id as FastaSequence;
            if (fastaSeq != null)
            {
                foreach (PeptideDocNode nodePep in fastaSeq.CreatePeptideDocNodes(settings, useFilter))
                    yield return nodePep;
            }
                // Peptide lists without variable modifications just return their existing children.
            else if (!settings.PeptideSettings.Modifications.HasVariableModifications)
            {
                foreach (PeptideDocNode nodePep in Children)
                {
                    if (!nodePep.HasVariableMods)
                        yield return nodePep;                    
                }
            }
                // If there are variable modifications, fill out the available list.
            else
            {
                var setNonExplicit = new HashSet<Peptide>();
                IPeptideFilter filter = (useFilter ? settings : PeptideFilter.UNFILTERED);
                foreach (PeptideDocNode nodePep in Children)
                {
                    if (nodePep.HasExplicitMods && !nodePep.HasVariableMods)
                        yield return nodePep;
                    else if (!setNonExplicit.Contains(nodePep.Peptide))
                    {
                        bool returnedResult = false;
                        var peptide = nodePep.Peptide;
                        // The peptide will be returned as the Id of the unmodified instance of this
                        // peptide.  If the peptide DocNode is explicitly modified this will cause
                        // two nodes in the tree to have the same Id.  So, use a copy instead.
                        if (nodePep.HasExplicitMods)
                            peptide = (Peptide) peptide.Copy();
                        foreach (PeptideDocNode nodePepResult in peptide.CreateDocNodes(settings, filter))
                        {
                            yield return nodePepResult;
                            returnedResult = true;
                        }
                        // Make sure the peptide is not removed due to filtering
                        if (!returnedResult)
                            yield return nodePep;
                        setNonExplicit.Add(nodePep.Peptide);
                    }
                }                
            }
        }

        private Dictionary<PeptideModKey, DocNode> CreatePeptideModToChildMap()
        {
            var map = new Dictionary<PeptideModKey, DocNode>();
            foreach (PeptideDocNode child in Children)
            {
                var key = child.Key;
                // Skip repeats.  These can only be created by the user, and should be
                // matched by the global index dictionary.
                if (!map.ContainsKey(key))
                    map.Add(key, child);
            }
            return map;
        }

        private Dictionary<int, DocNode> CreateGlobalIndexToChildMap()
        {
            return Children.ToDictionary(child => child.Id.GlobalIndex);
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            return PeptideGroupTreeNode.DisplayText(this, settings);
        }

        public static int CompareNames(PeptideGroupDocNode p1, PeptideGroupDocNode p2)
        {
            return string.Compare(p1.Name, p2.Name, StringComparison.CurrentCulture);
        }

        #region object overrides

        public bool Equals(PeptideGroupDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && Equals(obj._name, _name) && Equals(obj._description, _description);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideGroupDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (_name != null ? _name.GetHashCode() : 0);
                result = (result*397) ^ (_description != null ? _description.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}