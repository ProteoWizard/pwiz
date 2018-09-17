/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.ElementLocators
{
    /// <summary>
    /// An ElementRef for DocNode.
    /// Can be initialized from a SrmDocument and an IdentityPath.
    /// </summary>
    public abstract class NodeRef : ElementRef
    {
        protected NodeRef(NodeRef parent)
            : base(parent)
        {
        }

        public abstract DocNode FindNode(SrmDocument document);
        public new NodeRef Parent { get { return (NodeRef)base.Parent; } }

        public abstract IdentityPath ToIdentityPath(SrmDocument document);

        public virtual NodeRef ChangeIdentityPath(SrmDocument document, IdentityPath identityPath)
        {
            if (Parent == null)
            {
                return this;
            }
            var parentNode = (DocNodeParent)document.FindNode(identityPath.Parent);
            int index = parentNode.FindNodeIndex(identityPath.Child);
            if (index < 0)
            {
                throw new IdentityNotFoundException(identityPath.Child);
            }
            var parentRef = Parent.ChangeIdentityPath(document, identityPath.Parent);
            return (NodeRef)
                ((NodeRef)ChangeParent(parentRef)).EnumerateSiblings(document).Skip(index).FirstOrDefault();
        }
    }
    public abstract class NodeRef<TDocNode> : NodeRef where TDocNode : DocNode
    {
        protected NodeRef(NodeRef parent)
            : base(parent)
        {

        }

        public override DocNode FindNode(SrmDocument document)
        {
            DocNode parent;
            if (Parent == null)
            {
                parent = document;
            }
            else
            {
                parent = Parent.FindNode(document);
            }
            return FindNode(document, parent);
        }

        public virtual TDocNode FindNode(SrmDocument document, DocNode parentNode)
        {
            var docNodeParent = parentNode as DocNodeParent;
            if (docNodeParent == null)
            {
                return null;
            }
            int count = 0;
            foreach (var child in docNodeParent.Children.OfType<TDocNode>())
            {
                if (Matches(docNodeParent, child))
                {
                    if (count == Index)
                    {
                        return child;
                    }
                    count++;
                }
            }
            return null;
        }

        public override IdentityPath ToIdentityPath(SrmDocument document)
        {
            if (Parent == null)
            {
                return IdentityPath.ROOT;
            }
            var parentPath = Parent.ToIdentityPath(document);
            if (parentPath == null)
            {
                return null;
            }
            var parentNode = document.FindNode(parentPath);
            var thisNode = FindNode(document, parentNode);
            if (thisNode == null)
            {
                return null;
            }
            return new IdentityPath(parentPath, thisNode.Id);
        }

        protected virtual bool Matches(DocNodeParent parent, TDocNode docNode)
        {
            var compare = ((NodeRef<TDocNode>)Prototype).ChangeDocNode(parent, docNode);
            return ChangeIndex(0).ChangeParent(compare.Parent).Equals(compare);
        }

        protected sealed override IEnumerable<ElementRef> EnumerateSiblings(SrmDocument document)
        {
            if (Parent == null)
            {
                yield return this;
                yield break;
            }
            var parentNode = Parent.FindNode(document) as DocNodeParent;
            if (parentNode == null)
            {
                yield break;
            }
            var counts = new Dictionary<ElementRef, int>();
            foreach (TDocNode child in parentNode.Children)
            {
                var elementRef = ChangeDocNode(parentNode, child);
                int count;
                if (counts.TryGetValue(elementRef, out count))
                {
                    yield return elementRef.ChangeIndex(count);
                    counts[elementRef] = count + 1;
                }
                else
                {
                    yield return elementRef;
                    counts.Add(elementRef, 1);
                }
            }
        }

        protected abstract NodeRef<TDocNode> ChangeDocNode(DocNodeParent parent, TDocNode docNode);
    }

    public class MoleculeGroupRef : NodeRef<PeptideGroupDocNode>
    {
        public static readonly MoleculeGroupRef PROTOTYPE
            = new MoleculeGroupRef();
        private MoleculeGroupRef()
            : base(DocumentRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"MoleculeGroup"; }
        }

        protected override NodeRef<PeptideGroupDocNode> ChangeDocNode(DocNodeParent parent, PeptideGroupDocNode docNode)
        {
            return (MoleculeGroupRef)ChangeName(docNode.Name);
        }

        public static MoleculeGroupRef GetMoleculeGroupRef(SrmDocument document, PeptideGroupDocNode peptideGroupDocNode)
        {
            return (MoleculeGroupRef)PROTOTYPE.ChangeIdentityPath(document, new IdentityPath(peptideGroupDocNode.Id));
        }
        public override AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get { return AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.protein); }
        }

    }

    public class MoleculeRef : NodeRef<PeptideDocNode>
    {
        public static readonly MoleculeRef PROTOTYPE = new MoleculeRef();

        private MoleculeRef()
            : base(MoleculeGroupRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"Molecule"; }
        }

        protected override NodeRef<PeptideDocNode> ChangeDocNode(DocNodeParent parent, PeptideDocNode docNode)
        {
            return (MoleculeRef)ChangeName(GetName(docNode));
        }

        public static string GetName(PeptideDocNode peptideDocNode)
        {
            return peptideDocNode.Peptide.IsCustomMolecule
                ? peptideDocNode.CustomMolecule.ToString()
                : peptideDocNode.ModifiedSequence;
        }

        public static MoleculeRef GetMoleculeRef(PeptideGroupDocNode peptideGroupDocNode, PeptideDocNode peptideDocNode)
        {
            var result = PROTOTYPE.ChangeName(GetName(peptideDocNode));
            var withSameName = peptideGroupDocNode.Molecules.Where(mol => GetName(mol) == result.Name).ToArray();
            if (withSameName.Length > 1)
            {
                for (int i = 0; i < withSameName.Length; i++)
                {
                    if (ReferenceEquals(withSameName[i].Id, peptideDocNode.Id))
                    {
                        result = result.ChangeIndex(i);
                        break;
                    }
                }
            }
            return (MoleculeRef)result;
        }
        public override AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get { return AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.peptide); }
        }

    }

    public class PrecursorRef : NodeRef<TransitionGroupDocNode>
    {
        public static readonly PrecursorRef PROTOTYPE = new PrecursorRef();

        private PrecursorRef()
            : base(MoleculeRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"Precursor"; }
        }

        protected override NodeRef<TransitionGroupDocNode> ChangeDocNode(DocNodeParent parent, TransitionGroupDocNode docNode)
        {
            return (PrecursorRef)ChangeName(GetName(docNode));
        }

        public static string GetName(TransitionGroupDocNode transitionGroupDocNode)
        {
            return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () =>
                transitionGroupDocNode.TransitionGroup.LabelType.Name +
                Transition.GetChargeIndicator(transitionGroupDocNode.PrecursorAdduct));
        }

        public override AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get { return AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.precursor); }
        }

    }

    public class TransitionRef : NodeRef<TransitionDocNode>
    {
        public static readonly TransitionRef PROTOTYPE = new TransitionRef();

        private TransitionRef()
            : base(PrecursorRef.PROTOTYPE)
        {
        }

        public override string ElementType
        {
            get { return @"Transition"; }
        }

        protected override NodeRef<TransitionDocNode> ChangeDocNode(DocNodeParent parent, TransitionDocNode docNode)
        {
            return (TransitionRef)ChangeName(GetName((TransitionGroupDocNode)parent, docNode));
        }

        public static string GetName(TransitionGroupDocNode parent, TransitionDocNode transition)
        {
            if (transition.Transition.IsCustom())
            {
                string name = transition.PrimaryCustomIonEquivalenceKey;
                if (string.IsNullOrEmpty(name))
                {
                    name = transition.SecondaryCustomIonEquivalenceKey;
                }
                if (string.IsNullOrEmpty(name))
                {
                    name = parent.FindNodeIndex(transition.Transition).ToString(CultureInfo.InvariantCulture);
                }
                return name;
            }
            if (transition.Transition.IsPrecursor())
            {
                return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () =>
                    transition.Transition.IonType + Transition.GetMassIndexText(transition.Transition.MassIndex) +
                    Transition.GetChargeIndicator(transition.Transition.Adduct));
            }
            return LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () =>
                transition.Transition.IonType + transition.Transition.Ordinal.ToString(CultureInfo.InvariantCulture) +
                Transition.GetChargeIndicator(transition.Transition.Adduct)
            );
        }

        public override AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get { return AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.transition); }
        }
    }

    public class DocumentRef : NodeRef<SrmDocument>
    {
        public static readonly DocumentRef PROTOTYPE =
            (DocumentRef)new DocumentRef().ChangeName(string.Empty);

        private DocumentRef()
            : base(null)
        {

        }

        public override string ElementType
        {
            get { return @"Document"; }
        }

        protected override bool Matches(DocNodeParent parent, SrmDocument docNode)
        {
            return true;
        }

        public override DocNode FindNode(SrmDocument document)
        {
            return document;
        }

        protected override NodeRef<SrmDocument> ChangeDocNode(DocNodeParent parent, SrmDocument docNode)
        {
            return this;
        }
    }
}
