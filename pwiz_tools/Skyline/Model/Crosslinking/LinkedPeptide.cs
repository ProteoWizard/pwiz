using System;
using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class LinkedPeptide : Immutable
    {
        public LinkedPeptide(Peptide peptide, int indexAa, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            IndexAa = indexAa;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public int IndexAa { get; private set; }

        public ExplicitMods ExplicitMods { get; private set; }

        public TransitionGroup GetTransitionGroup(IsotopeLabelType labelType, Adduct adduct)
        {
            return new TransitionGroup(Peptide, adduct, labelType);
        }

        public TransitionGroupDocNode GetTransitionGroupDocNode(SrmSettings settings, IsotopeLabelType labelType, Adduct adduct) {
            var transitionGroup = GetTransitionGroup(labelType, adduct);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings, ExplicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            return transitionGroupDocNode;
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var transitionGroupDocNode = GetTransitionGroupDocNode(settings, labelType, Adduct.SINGLY_PROTONATED);
            return transitionGroupDocNode.GetNeutralFormula(settings, ExplicitMods);
        }
    }
}
