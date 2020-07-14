using System;
using System.Collections.Generic;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class AcceptanceCriteria
    {
        private Peptide _peptide;
        public AcceptanceCriteria(Peptide peptide)
        {
            _peptide = peptide;
        }

        public double? TargetIonRatio
        {
            get
            {
                return _peptide.DocNode.TargetIonRatio;
            }
            set
            {
                _peptide.ChangeDocNode(EditDescription.SetColumn(nameof(TargetIonRatio), value), docNode=>docNode.ChangeTargetIonRatio(value));
            }
        }

        public double? IonRatioThreshold
        {
            get
            {
                return _peptide.DocNode.IonRatioThreshold;
            }
            set
            {
                _peptide.ChangeDocNode(EditDescription.SetColumn(nameof(IonRatioThreshold), value), docNode=>docNode.ChangeIonRatioThreshold(value));
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (TargetIonRatio != null)
            {
                String str = @"Target Ion Ratio: " + TargetIonRatio;
                if (IonRatioThreshold.HasValue)
                {
                    str += @"+/-" + IonRatioThreshold.Value.ToString(@"#%");
                }
                parts.Add(str);
            }

            return TextUtil.SpaceSeparate(parts);
        }
    }
}
