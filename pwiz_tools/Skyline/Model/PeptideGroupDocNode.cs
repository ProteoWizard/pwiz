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
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideGroupDocNode : DocNodeParent
    {
        private ProteinMetadata _proteinMetadata;  // name, description, accession, gene, etc

        public PeptideGroupDocNode(PeptideGroup id, string name, string description, PeptideDocNode[] children)
            : this(id, Annotations.EMPTY, name, description, children)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, ProteinMetadata proteinMetadata, PeptideDocNode[] children)
            : this(id, Annotations.EMPTY, proteinMetadata, children, true)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, string name, string description,
                                   PeptideDocNode[] children)
            : this(id, annotations, name, description, children, true)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, string name, string description,
            PeptideDocNode[] children, bool autoManageChildren)
            : this(id, annotations, new ProteinMetadata(name, description), children, autoManageChildren)
        {
        }

        public PeptideGroupDocNode(PeptideGroup id, Annotations annotations, ProteinMetadata proteinMetadata,
            PeptideDocNode[] children, bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            if (Equals(id.Name, proteinMetadata.Name))
                proteinMetadata = proteinMetadata.ChangeName(null);  // Make it clear that the name hasn't been altered
            if (Equals(id.Description, proteinMetadata.Description))
                proteinMetadata = proteinMetadata.ChangeDescription(null);  // Make it clear that the description hasn't been altered
            _proteinMetadata = proteinMetadata;
        }

        public PeptideGroup PeptideGroup { get { return (PeptideGroup)Id; } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget
        {
            get { return AnnotationDef.AnnotationTarget.protein; }
        }

        public bool IsProtein { get { return PeptideGroup is FastaSequence; } }
        public bool IsPeptideList { get { return !(PeptideGroup is FastaSequence); } }
        public bool IsDecoy { get { return PeptideGroup.IsDecoy; } }

        public string Name { get { return _proteinMetadata.Name ?? PeptideGroup.Name ?? string.Empty; } } // prefer ours over peptidegroup, if set
        public string OriginalName { get { return PeptideGroup.Name; } }
        public string Description { get { return _proteinMetadata.Description ?? PeptideGroup.Description; } } // prefer ours over peptidegroup, if set
        public string OriginalDescription { get { return PeptideGroup.Description; } } 
        public ProteinMetadata ProteinMetadata { get { return _proteinMetadata.Merge(new ProteinMetadata(PeptideGroup.Name, PeptideGroup.Description)); } } // prefer our name and description over peptidegroup

        /// <summary>
        /// returns our actual metadata, not merged with that of the ID object
        /// </summary>
        public ProteinMetadata ProteinMetadataOverrides { get { return _proteinMetadata; } } 

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { Molecules, TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int MoleculeCount { get { return GetCount((int)Level.Molecules); } }
        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        public int PeptideCount { get { return IsProteomic ? MoleculeCount : 0; } }

        public bool IsEmpty { get { return MoleculeCount == 0; }}  // If empty, it's neither proteomic nor non-proteomic

        public bool IsProteomic
        {
            // Default assumption for an empty PeptideGroupDocNode is that it's proteomic (probably undergoing population from a protein)
            get { return IsEmpty || !((PeptideDocNode)Children[0]).Peptide.IsCustomMolecule; }
        }

        public bool IsNonProteomic
        {
            get { return !IsProteomic; }
        }

        public IEnumerable<PeptideDocNode> Molecules { get { return Children.Cast<PeptideDocNode>(); } }
        public IEnumerable<PeptideDocNode> SmallMolecules { get { return Molecules.Where(p => p.Peptide.IsCustomMolecule); } }
        public IEnumerable<PeptideDocNode> Peptides { get { return Molecules.Where(p => !p.Peptide.IsCustomMolecule); } }

        public PeptideGroupDocNode ChangeName(string name)
        {
            var newMetadata = _proteinMetadata.ChangeName(name);
            return ChangeProteinMetadata(newMetadata);
        }

        public PeptideGroupDocNode ChangeDescription(string desc)
        {
            var newMetadata = _proteinMetadata.ChangeDescription(desc);
            return ChangeProteinMetadata(newMetadata);
        }

        public PeptideGroupDocNode ChangeProteinMetadata(ProteinMetadata proteinMetadata)
        {
            var newMetadata = proteinMetadata;
            if (Equals(PeptideGroup.Name, newMetadata.Name))
                newMetadata = newMetadata.ChangeName(null); // no actual override
            if (Equals(PeptideGroup.Description, newMetadata.Description))
                newMetadata = newMetadata.ChangeDescription(null); // no actual override
            return ChangeProp(ImClone(this), im => im._proteinMetadata = newMetadata);
        }

        public PeptideGroupDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff,
            DocumentSettingsContext context = null)
        {
            if (diff.Monitor != null)
                diff.Monitor.ProcessGroup(this);

            if (diff.DiffPeptides && settingsNew.PeptideSettings.Filter.AutoSelect && AutoManageChildren)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                int countPeptides = 0;
                int countIons = 0;

                Dictionary<int, DocNode> mapIndexToChild = CreateGlobalIndexToChildMap();
                Dictionary<PeptideModKey, DocNode> mapIdToChild = CreatePeptideModToChildMap();

                IEnumerable<PeptideDocNode> peptideDocNodes;
                if (!IsProtein ||
                    settingsNew.PeptideSettings.Filter.PeptideUniqueness == PeptideFilter.PeptideUniquenessConstraint.none ||
                                        settingsNew.PeptideSettings.NeedsBackgroundProteomeUniquenessCheckProcessing)
                {
                    peptideDocNodes = GetPeptideNodes(settingsNew, true, diff.Monitor).ToList();
                }
                else
                {
                    // Checking peptide uniqueness against the background proteome can be expensive.
                    // Do all the regular processing, then filter those results at the end when we
                    // can do it in aggregate for best speed.
                    IEnumerable<PeptideDocNode> peptideDocNodesUnique;
                    var peptideDocNodesPrecalculatedForUniquenessCheck = context == null ? null : context.PeptideDocNodesPrecalculatedForUniquenessCheck;
                    var uniquenessDict = context == null ? null : context.UniquenessDict;
                    if (peptideDocNodesPrecalculatedForUniquenessCheck != null)
                    {
                        // Already processed, and a global list of peptides provided
                        Assume.IsNotNull(uniquenessDict);
                        peptideDocNodesUnique = peptideDocNodesPrecalculatedForUniquenessCheck;
                    }
                    else
                    {
                        // We'll have to do the processing for this node, and work with
                        // just the peptides on this node.  With luck the background proteome
                        // will already have those cached for uniqueness checks.
                        var settingsNoUniquenessFilter =
                            settingsNew.ChangePeptideFilter(f => f.ChangePeptideUniqueness(PeptideFilter.PeptideUniquenessConstraint.none));
                        var nodes = GetPeptideNodes(settingsNoUniquenessFilter, true, diff.Monitor).ToList();
                        var sequences = new List<Target>(from p in nodes select p.Peptide.Target);
                        peptideDocNodesUnique = nodes;  // Avoid ReSharper multiple enumeration warning
                        uniquenessDict = settingsNew.PeptideSettings.Filter.CheckPeptideUniqueness(settingsNew, sequences, diff.Monitor);
                    }
                    // ReSharper disable once PossibleNullReferenceException
                    peptideDocNodes = peptideDocNodesUnique.Where(p =>
                    {
                        // It's possible during document load for uniqueness dict to get out of synch, so be 
                        // cautious with lookup and just return false of not found. Final document change will clean that up.
                        bool isUnique;
                        return IsNonProteomic || (uniquenessDict.TryGetValue(p.Peptide.Target, out isUnique) && isUnique);
                    });
                }
                
                foreach(var nodePep in peptideDocNodes)
                {
                    if (diff.Monitor != null && diff.Monitor.IsCanceled())
                        throw new OperationCanceledException();

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
                        if (settingsNew.TransitionSettings.Libraries.MinIonCount > 0 && nodePepResult.TransitionGroupCount == 0)
                            continue;

                        childrenNew.Add(nodePepResult);

                        // Make sure a single peptide group does not exceed document limits.
                        countPeptides++;
                        countIons += nodePepResult.TransitionCount;
                        if (countIons > SrmDocument.MaxTransitionCount)
                            throw new InvalidDataException(String.Format(
                                Resources.PeptideGroupDocNode_ChangeSettings_The_current_document_settings_would_cause_the_number_of_targeted_transitions_to_exceed__0_n0___The_document_settings_must_be_more_restrictive_or_add_fewer_proteins_,
                                SrmDocument.MaxTransitionCount));
                        if (countPeptides > SrmDocument.MAX_PEPTIDE_COUNT)
                            throw new InvalidDataException(String.Format(
                                Resources.PeptideGroupDocNode_ChangeSettings_The_current_document_settings_would_cause_the_number_of_peptides_to_exceed__0_n0___The_document_settings_must_be_more_restrictive_or_add_fewer_proteins_,
                                SrmDocument.MAX_PEPTIDE_COUNT));
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

                    childrenNew = RankChildren(settingsNew, childrenNew);

                    nodeResult = (PeptideGroupDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }
                return nodeResult;
            }
        }

        public IList<DocNode> RankChildren(SrmSettings settingsNew, IList<DocNode> childrenNew)
        {
            if (PeptideGroup.Sequence != null)
                childrenNew = PeptideGroup.RankPeptides(childrenNew, settingsNew, false);
            return childrenNew;
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


        public IEnumerable<PeptideDocNode> GetPeptideNodes(SrmSettings settings, bool useFilter, SrmSettingsChangeMonitor monitor = null)
        {
            // FASTA sequences can generate a comprehensive list of available peptides.
            FastaSequence fastaSeq = Id as FastaSequence;
            if (fastaSeq != null)
            {
                foreach (PeptideDocNode nodePep in fastaSeq.CreatePeptideDocNodes(settings, useFilter, null))
                {
                    if (monitor != null && monitor.IsCanceled())
                        throw new OperationCanceledException();
                    yield return nodePep;
                }
            }
            // Peptide lists without variable modifications just return their existing children.
            else if (!settings.PeptideSettings.Modifications.HasVariableModifications)
            {
                foreach (PeptideDocNode nodePep in Children)
                {
                    if (monitor != null && monitor.IsCanceled())
                        throw new OperationCanceledException();
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
                    if (monitor != null && monitor.IsCanceled())
                        throw new OperationCanceledException();
                    if (nodePep.Peptide.IsCustomMolecule) // Modifications mean nothing to custom ions // TODO(bspratt) but static isotope labels do?
                        yield return nodePep;
                    else if (nodePep.HasExplicitMods && !nodePep.HasVariableMods)
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
            return ProteinMetadataManager.ProteinModalDisplayText(this);
        }

        public static int CompareNames(PeptideGroupDocNode p1, PeptideGroupDocNode p2)
        {
            return string.Compare(p1.Name, p2.Name, StringComparison.CurrentCulture);
        }

        public static int ComparePreferredNames(PeptideGroupDocNode p1, PeptideGroupDocNode p2)
        {
            return string.Compare(p1.ProteinMetadata.PreferredName ?? String.Empty, p2.ProteinMetadata.PreferredName ?? String.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        public static int CompareAccessions(PeptideGroupDocNode p1, PeptideGroupDocNode p2)
        {
            return string.Compare(p1.ProteinMetadata.Accession ?? String.Empty, p2.ProteinMetadata.Accession ?? String.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        public static int CompareGenes(PeptideGroupDocNode p1, PeptideGroupDocNode p2)
        {
            return string.Compare(p1.ProteinMetadata.Gene ?? String.Empty, p2.ProteinMetadata.Gene ?? String.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        #region object overrides

        public bool Equals(PeptideGroupDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && Equals(obj._proteinMetadata, _proteinMetadata); 
        }

        protected override IList<DocNode> OnChangingChildren(DocNodeParent clone, int indexReplaced)
        {
            var childrenNew = clone.Children;
            if (IsColorComplete(childrenNew, indexReplaced))
                return childrenNew;
            return GenerateColors(childrenNew);
        }

        private bool IsColorComplete(IList<DocNode> children, int indexReplaced)
        {
            // If only 1 node was replaced, check to see if it has a color
            if (indexReplaced != -1)
            {
                return ((PeptideDocNode) children[indexReplaced]).Color.A != 0;
            }

            // Because using LINQ shows up in a profiler
            foreach (PeptideDocNode peptideDocNode in children)
            {
                if (peptideDocNode.Color.A == 0)
                    return false;
            }
            return true;
        }

        private IList<DocNode> GenerateColors(IList<DocNode> children)
        {
            var newChildren = new List<DocNode>(children.Count);
            var colorList = new List<Color>(children.Count);

            // To avoid color collisions, create a list of all colors
            // assigned to peptides so far.
            foreach (PeptideDocNode peptideDocNode in children)
            {
                if (peptideDocNode.Color.A != 0)
                    colorList.Add(peptideDocNode.Color);
            }

            // Generate colors for peptides without colors, avoiding
            // collisions with already assigned colors.
            foreach (PeptideDocNode peptideDocNode in children)
            {
                if (peptideDocNode.Color.A != 0)
                    newChildren.Add(peptideDocNode);
                else
                {
                    var color = ColorGenerator.GetColor(peptideDocNode.ModifiedTarget.ToString(), colorList);
                    newChildren.Add(peptideDocNode.ChangeColor(color));
                    colorList.Add(color);
                }
            }
            return newChildren;
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
                result = (result * 397) ^ _proteinMetadata.GetHashCode();
                return result;
            }
        }

        #endregion
    }
}