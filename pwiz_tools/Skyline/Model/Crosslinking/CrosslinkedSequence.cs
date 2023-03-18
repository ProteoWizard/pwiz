/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// A list of <see cref="ModifiedSequence"/> joined together with crosslinkers.
    /// </summary>
    public class CrosslinkedSequence : ProteomicSequence
    {
        public CrosslinkedSequence(IEnumerable<ProteomicSequence> peptides,
            IEnumerable<Crosslink> crosslinks)
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

        public ImmutableList<ProteomicSequence> Peptides { get; private set; }
        public ImmutableList<Crosslink> Crosslinks { get; private set; }

        public override string ToString()
        {
            return FormatDefault();
        }

        public override string FullNames
        {
            get
            {
                return Format(sequence => sequence.FullNames, ModifiedSequence.FormatFullName);
            }
        }

        public override string MonoisotopicMasses
        {
            get
            {
                return Format(sequence => sequence.MonoisotopicMasses,
                    FormatMassModificationFunc(MassType.Monoisotopic, true));
            }
        }
        public override string AverageMasses
        {
            get
            {
                return Format(sequence => sequence.MonoisotopicMasses,
                    FormatMassModificationFunc(MassType.Average, true));
            }
        }

        public override string ThreeLetterCodes
        {
            get { return Format(sequence => sequence.ThreeLetterCodes, ModifiedSequence.FormatThreeLetterCode); }
        }

        public override string UnimodIds
        {
            get
            {
                // Consider(nicksh): Should the crosslinker be formatted with the unimod id as well?
                return Format(sequence => sequence.UnimodIds, ModifiedSequence.FormatFullName);
            }
        }

        public override string FormatDefault()
        {
            return Format(sequence => sequence.FormatDefault(), ModifiedSequence.FormatFullName);
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

        private string Format(Func<ProteomicSequence, string> sequenceFormatter,
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
