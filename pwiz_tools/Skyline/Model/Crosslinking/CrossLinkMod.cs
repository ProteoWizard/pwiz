using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkMod : Immutable
    {
        public CrosslinkMod(int indexAa, CrosslinkerDef crosslinkerDef, IEnumerable<LinkedPeptide> linkedPeptides)
        {
            IndexAa = indexAa;
            CrosslinkerDef = crosslinkerDef;
            LinkedPeptides = ImmutableList.ValueOf(linkedPeptides);
        }

        public int IndexAa { get; private set; }

        public CrosslinkerDef CrosslinkerDef { get; private set; }

        public ImmutableList<LinkedPeptide> LinkedPeptides { get; private set; }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var massType = settings.TransitionSettings.Prediction.PrecursorMassType;
            MoleculeMassOffset moleculeMassOffset = CrosslinkerDef.FormulaMass.GetMoleculeMassOffset(massType);
            foreach (var linkedPeptide in LinkedPeptides)
            {
                moleculeMassOffset = moleculeMassOffset.Add(linkedPeptide.GetNeutralFormula(settings, labelType));
            }

            return moleculeMassOffset;
        }
    }
}
