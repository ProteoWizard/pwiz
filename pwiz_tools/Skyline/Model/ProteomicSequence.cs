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
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Base class of <see cref="ModifiedSequence"/> and <see cref="CrosslinkedSequence"/>.
    /// All of the public properties on this class are visible in the Document Grid.
    /// </summary>
    public abstract class ProteomicSequence
    {
        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, Peptide peptide,
            ExplicitMods explicitMods, IsotopeLabelType labelType)
        {
            if (peptide.IsCustomMolecule)
            {
                return null;
            }
            if (explicitMods == null || !explicitMods.HasCrosslinks)
            {
                return ModifiedSequence.GetModifiedSequence(settings, peptide.Sequence, explicitMods, labelType);
            }

            return CrosslinkedSequence.GetCrosslinkedSequence(settings, explicitMods.GetPeptideStructure(), labelType);
        }

        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, PeptideDocNode peptideDocNode, IsotopeLabelType labelType)
        {
            return GetProteomicSequence(settings, peptideDocNode.Peptide, peptideDocNode.ExplicitMods, labelType);
        }

        public abstract string MonoisotopicMasses { get; }
        public abstract string AverageMasses { get; }
        public abstract string ThreeLetterCodes { get; }
        public abstract string FullNames { get; }
        public abstract string UnimodIds { get; }
        public abstract string FormatDefault();
    }
}