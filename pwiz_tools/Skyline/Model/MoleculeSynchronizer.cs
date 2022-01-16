using System;
using System.Collections.Generic;
using System.Linq;

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

        public static MoleculeSynchronizer MakeMoleculeIndex(IEnumerable<PeptideGroupDocNode> peptideGroups)
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
            var sourceDocNode = FindPeptide(moleculeGroups, sourceIdentityPath);
            var key = GetKey(sourceDocNode);
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

                newChildren = newChildren ?? moleculeGroups.ToArray();
                var targetMoleculeGroup = (PeptideGroupDocNode) newChildren[targetMoleculeGroupIndex];
                int childIndex = targetMoleculeGroup.FindNodeIndex(identityPath.GetIdentity(1));
                if (childIndex < 0)
                {
                    continue;
                }

                targetMoleculeGroup =
                    (PeptideGroupDocNode) targetMoleculeGroup.ReplaceChild(
                        sourceDocNode.ChangePeptide((Peptide) identityPath.GetIdentity(1)));
                newChildren[childIndex] = targetMoleculeGroup;
            }

            if (newChildren == null)
            {
                return moleculeGroups;
            }

            return new DocNodeChildren(newChildren, moleculeGroups);
        }

        public SrmDocument AfterReplaceChild(SrmDocument document, PeptideGroup peptideGroup)
        {
            var originalChildren = GetChildren(document);
            var newChildren = originalChildren;
            var peptideGroupDocNode = document.FindPeptideGroup(peptideGroup);
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
    }
}
