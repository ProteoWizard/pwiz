/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using System.Runtime.CompilerServices;

namespace pwiz.Skyline.Model
{
    public class MoleculeSynchronizer
    {
        private ILookup<PeptideSequenceModKey, IdentityPath> _lookup;
        public MoleculeSynchronizer(ILookup<PeptideSequenceModKey, IdentityPath> lookup)
        {
            _lookup = lookup;
        }

        public static PeptideSequenceModKey GetKey(PeptideDocNode molecule)
        {
            return molecule.SequenceKey;
        }

        public static MoleculeSynchronizer MakeMoleculeSynchronizer(SrmDocument document)
        {
            return MakeMoleculeSynchronizer(document.MoleculeGroups);
        }

        public static MoleculeSynchronizer MakeMoleculeSynchronizer(IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            var lookup = peptideGroups.SelectMany(group => group.Molecules.Select(molecule
                    => Tuple.Create(GetKey(molecule), new IdentityPath(group.PeptideGroup, molecule.Peptide))))
                .ToLookup(tuple => tuple.Item1, tuple => tuple.Item2);
            return new MoleculeSynchronizer(lookup);
        }

        public IEnumerable<IdentityPath> FindMolecules(PeptideSequenceModKey key)
        {
            return _lookup[key];
        }

        public DocNodeChildren Synchronize(DocNodeChildren moleculeGroups, PeptideGroup peptideGroup, Peptide peptide)
        {
            var sourceIdentityPath = new IdentityPath(peptideGroup, peptide);
            var sourceMoleculeNode = FindPeptide(moleculeGroups, sourceIdentityPath);
            var key = GetKey(sourceMoleculeNode);
            DocNode[] newChildren = null;
            foreach (var identityPath in _lookup[key])
            {
                if (Equals(identityPath, sourceIdentityPath))
                {
                    continue;
                }

                int targetMoleculeGroupIndex = moleculeGroups.IndexOf(identityPath.GetIdentity(0));
                if (targetMoleculeGroupIndex < 0)
                {
                    continue;
                }

                var targetMoleculeGroup = (PeptideGroupDocNode)
                    ((IList<DocNode>)newChildren ?? moleculeGroups)[targetMoleculeGroupIndex];
                var targetMoleculeNode = (PeptideDocNode) targetMoleculeGroup.FindNode(identityPath.GetIdentity(1));
                if (targetMoleculeNode == null)
                {
                    continue;
                }

                var synchronizedTargetMoleculeNode = CopyPeptideIdFrom(sourceMoleculeNode, targetMoleculeNode);
                if (Equals(synchronizedTargetMoleculeNode, targetMoleculeNode))
                {
                    continue;
                }

                newChildren ??= moleculeGroups.ToArray();
                targetMoleculeGroup =
                    (PeptideGroupDocNode) targetMoleculeGroup.ReplaceChild(synchronizedTargetMoleculeNode);
                newChildren[targetMoleculeGroupIndex] = targetMoleculeGroup;
            }

            if (newChildren == null)
            {
                return moleculeGroups;
            }

            return new DocNodeChildren(newChildren, moleculeGroups);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public SrmDocument AfterReplaceChild(SrmDocument document, PeptideGroup peptideGroup)
        {
            var originalChildren = GetChildren(document);
            int peptideGroupIndex = originalChildren.IndexOf(peptideGroup);
            var newChildren = originalChildren;
            var peptideGroupDocNode = (PeptideGroupDocNode) originalChildren[peptideGroupIndex];
            foreach (PeptideDocNode peptideDocNode in peptideGroupDocNode.Children)
            {
                newChildren = Synchronize(newChildren, peptideGroup, peptideDocNode.Peptide);
            }

            if (ReferenceEquals(newChildren, originalChildren))
            {
                return document;
            }

            return (SrmDocument) document.ChangeChildren(newChildren);
        }

        private PeptideGroupDocNode FindMoleculeGroup(DocNodeChildren docNodeChildren, PeptideGroup id)
        {
            int index = docNodeChildren.IndexOf(id);
            if (index < 0)
            {
                return null;
            }

            return (PeptideGroupDocNode) docNodeChildren[index];
        }

        private PeptideDocNode FindPeptide(DocNodeChildren moleculeGroups, IdentityPath moleculeIdentityPath)
        {
            return (PeptideDocNode) FindMoleculeGroup(moleculeGroups,
                (PeptideGroup) moleculeIdentityPath.GetIdentity(0)).FindNode(moleculeIdentityPath.GetIdentity(1));
        }

        public static DocNodeChildren GetChildren(SrmDocument document)
        {
            return document.Children as DocNodeChildren ?? new DocNodeChildren(document.Children, document.Children);
        }

        private PeptideDocNode CopyPeptideIdFrom(PeptideDocNode copyIdTo, PeptideDocNode copyIdFrom)
        {
            IEnumerable<TransitionGroupDocNode> newTransitionGroups;
            if (IdsMatch(copyIdTo.TransitionGroups, copyIdFrom.TransitionGroups, tg=>RemoveProteinId(tg.TransitionGroup)))
            {
                newTransitionGroups = copyIdTo.TransitionGroups.Zip(copyIdFrom.TransitionGroups, CopyTransitionGroupIdFrom);
            }
            else
            {
                newTransitionGroups = copyIdTo.TransitionGroups.Select(t => t.ChangePeptide(copyIdFrom.Peptide));
            }

            PeptideDocNode result = copyIdTo.ChangePeptide(copyIdFrom.Peptide, newTransitionGroups);
            // Duplicate molecules are allowed to have different Standard Type values, so preserve that
            // if necessary
            if (!Equals(result.GlobalStandardType, copyIdFrom.GlobalStandardType))
            {
                result = result.ChangeStandardType(copyIdFrom.GlobalStandardType);
            }
            return result;
        }

        private TransitionGroupDocNode CopyTransitionGroupIdFrom(TransitionGroupDocNode copyIdTo, TransitionGroupDocNode copyIdFrom)
        {
            IEnumerable<TransitionDocNode> newTransitions;
            if (IdsMatch(copyIdTo.Transitions, copyIdFrom.Transitions, t=>RemoveProteinId(t.Transition)))
            {
                newTransitions =
                    copyIdTo.Transitions.Zip(copyIdFrom.Transitions, (t, f) => t.ChangeTransitionId(f.Transition));
            }
            else
            {
                newTransitions = copyIdTo.Transitions.Select(t => t.ChangeTransitionGroup(copyIdFrom.TransitionGroup));
            }

            return copyIdTo.ChangeTransitionGroupId(copyIdFrom.TransitionGroup, newTransitions);
        }

        private Peptide RemoveProteinId(Peptide peptide)
        {
            if (peptide.FastaSequence == null)
            {
                return peptide;
            }

            return new Peptide(null, peptide.Sequence, null, null, peptide.MissedCleavages, peptide.IsDecoy);
        }

        private TransitionGroup RemoveProteinId(TransitionGroup transitionGroup)
        {
            var newPeptide = RemoveProteinId(transitionGroup.Peptide);
            if (ReferenceEquals(newPeptide, transitionGroup.Peptide))
            {
                return transitionGroup;
            }

            return new TransitionGroup(newPeptide, transitionGroup.PrecursorAdduct, transitionGroup.LabelType, true,
                transitionGroup.DecoyMassShift);
        }

        private Transition RemoveProteinId(Transition transition)
        {
            var newTransitionGroup = RemoveProteinId(transition.Group);
            if (ReferenceEquals(newTransitionGroup, transition.Group))
            {
                return transition;
            }

            return transition.ChangeTransitionGroup(newTransitionGroup);
        }


        private bool IdsMatch<T>(IEnumerable<T> nodes1, IEnumerable<T> nodes2, Func<T, Identity> getIdentityFunc)
        {
            return nodes1.Select(getIdentityFunc).SequenceEqual(nodes2.Select(getIdentityFunc));
        }
    }
}
