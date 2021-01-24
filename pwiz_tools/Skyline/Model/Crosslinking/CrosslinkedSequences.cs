using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class CrosslinkedSequence
    {
        public CrosslinkedSequence(IEnumerable<ModifiedSequence> peptides,
            IEnumerable<CrosslinkModification> crosslinks)
        {
            Peptides = ImmutableList.ValueOf(peptides);
            Crosslinks = ImmutableList.ValueOf(crosslinks);
        }

        public static CrosslinkedSequence GetCrosslinkedSequence(SrmSettings settings,
            PeptideStructure peptideStructure, IsotopeLabelType labelType)
        {
            var modifiedSequences = new List<ModifiedSequence>();
            for (int i = 0; i < peptideStructure.Peptides.Count; i++)
            {
                modifiedSequences.Add(ModifiedSequence.GetModifiedSequence(settings, peptideStructure.Peptides[i].Sequence, peptideStructure.ExplicitModList[i], labelType));
            }
            return new CrosslinkedSequence(modifiedSequences, peptideStructure.Crosslinks);
        }

        public ImmutableList<ModifiedSequence> Peptides { get; private set; }
        public ImmutableList<CrosslinkModification> Crosslinks { get; private set; }

        public override string ToString()
        {
            return Format(sequence => sequence.ToString(), ModifiedSequence.FormatFullName);
        }

        public string FullNames
        {
            get
            {
                return Format(sequence => sequence.FullNames, ModifiedSequence.FormatFullName);
            }
        }

        public string MonoisotopicMasses
        {
            get
            {
                return Format(sequence => sequence.MonoisotopicMasses,
                    FormatMassModificationFunc(MassType.Monoisotopic, true));
            }
        }
        public string AverageMasses
        {
            get
            {
                return Format(sequence => sequence.MonoisotopicMasses,
                    FormatMassModificationFunc(MassType.Average, true));
            }
        }

        private static Func<ModifiedSequence.Modification, string> FormatMassModificationFunc(MassType massType, bool fullPrecision)
        {
            return mod =>
            {
                string str = ModifiedSequence.FormatMassModification(new[] {mod}, massType, fullPrecision);
                if (string.IsNullOrEmpty(str))
                {
                    return string.Empty;
                }

                return str.Substring(1, str.Length - 2);
            };
        }

        private string Format(Func<ModifiedSequence, string> sequenceFormatter,
            Func<ModifiedSequence.Modification, string> modificationFormatter)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strHyphen = string.Empty;
            foreach (var modifiedSequence in Peptides)
            {
                stringBuilder.Append(strHyphen);
                strHyphen = @"-";
                stringBuilder.Append(sequenceFormatter(modifiedSequence));
            }
            stringBuilder.Append(@"-");
            foreach (var crosslink in Crosslinks)
            {
                stringBuilder.Append(@"[");
                var staticMod = crosslink.Crosslinker;
                var modification = new ModifiedSequence.Modification(new ExplicitMod(-1, staticMod),
                    staticMod.MonoisotopicMass ?? 0, staticMod.AverageMass ?? 0);
                stringBuilder.Append(modificationFormatter(modification));
                stringBuilder.Append(@"@");
                stringBuilder.Append(crosslink.Sites.ToString());
                stringBuilder.Append(@"]");
            }

            return stringBuilder.ToString();
        }
    }
}
