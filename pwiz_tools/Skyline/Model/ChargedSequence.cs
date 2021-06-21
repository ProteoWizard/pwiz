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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Represents a proteomic sequence and a charge.
    /// The sequence can either be a modified peptide sequence, or a crosslinked peptide sequence.
    /// </summary>
    public class ChargedSequence : Immutable
    {
        public ChargedSequence(string sequence, Adduct adduct)
        {
            Sequence = sequence;
            Adduct = adduct;
        }

        public string Sequence { get; private set; }

        public Adduct Adduct { get; private set; }

        public ChargedSequence ChangeAdduct(Adduct adduct)
        {
            return ChangeProp(ImClone(this), im => im.Adduct = adduct);
        }

        /// <summary>
        /// Does some validation on a peptide sequence. The sequence can either be an ordinary modified sequence,
        /// or a crosslinked peptide sequence.
        /// </summary>
        public static ChargedSequence ParsePeptideSequence(string peptideSequence, out string errorMessage)
        {
            errorMessage = null;
            CrosslinkLibraryKey crosslinkLibraryKey =
                CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(peptideSequence, 0);
            if (crosslinkLibraryKey != null)
            {
                if (!crosslinkLibraryKey.IsSupportedBySkyline())
                {
                    errorMessage = Resources
                        .PasteDlg_ListPeptideSequences_The_structure_of_this_crosslinked_peptide_is_not_supported_by_Skyline;
                    return null;
                }
            }
            else
            {
                if (!FastaSequence.IsExSequence(peptideSequence))
                {
                    errorMessage = Resources
                        .PasteDlg_ListPeptideSequences_The_structure_of_this_crosslinked_peptide_is_not_supported_by_Skyline;
                    return null;
                }
                peptideSequence = FastaSequence.NormalizeNTerminalMod(peptideSequence);
            }

            return new ChargedSequence(peptideSequence, Adduct.EMPTY);
        }

        /// <summary>
        /// Parses a peptides sequence which may optionally be followed by a charge indicator.
        /// The peptide sequence will have N-terminal mods normalized so that they appear after the first amino acid.
        ///
        /// If the peptide sequence is invalid, then <paramref name="errorMessage "/> will be set.
        /// </summary>
        public static ChargedSequence ParsePeptideAndCharge(string peptideSequence, out string errorMessage)
        {
            errorMessage = null;
            peptideSequence = (peptideSequence ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(peptideSequence))
            {
                return null;
            }
            var adduct = Transition.GetChargeFromIndicator(peptideSequence, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
            peptideSequence = Transition.StripChargeIndicators(peptideSequence, TransitionGroup.MIN_PRECURSOR_CHARGE, TransitionGroup.MAX_PRECURSOR_CHARGE);
            var chargedSequence = ParsePeptideSequence(peptideSequence, out errorMessage);
            if (chargedSequence == null)
            {
                return null;
            }

            return chargedSequence.ChangeAdduct(adduct);
        }
    }
}
