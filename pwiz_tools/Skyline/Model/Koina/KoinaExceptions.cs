/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Koina
{
    /// <summary>
    /// Simple class to distinguish Koina exceptions.
    /// </summary>
    public class KoinaException : Exception
    {
        public KoinaException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    public class KoinaPeptideTooLongException : KoinaException
    {
        public KoinaPeptideTooLongException(Target target)
            : base(string.Format(
                KoinaResources.KoinaPeptideTooLongException_KoinaPeptideTooLongException_Peptide_Sequence___0_____1___is_longer_than_the_maximum_supported_length_by_Koina___2__,
                target.Sequence, FastaSequence.StripModifications(target.Sequence).Length, KoinaConstants.PEPTIDE_SEQ_LEN))
        {
        }
    }

    public class KoinaUnsupportedAminoAcidException : KoinaException
    {
        public KoinaUnsupportedAminoAcidException(Target target, int index)
            : base(string.Format(
                KoinaResources.KoinaPeptideTooLongException_KoinaUnsupportedAminoAcidException_Amino_acid___0___in___1___is_not_supported_by_Koina,
                target.Sequence[index], target))
        {
        }
    }

    public class KoinaUnsupportedModificationException : KoinaException
    {
        public KoinaUnsupportedModificationException(Target target, StaticMod mod, int index)
            : base(string.Format(KoinaResources.KoinaUnsupportedModificationException_KoinaUnsupportedModificationException_Modification___0___at_index___1___in___2___is_not_supported_by_Koina, mod.Name,
                index, target.Sequence))
        {
        }
    }

    public class KoinaSmallMoleculeException : KoinaException
    {
        public KoinaSmallMoleculeException(Target target)
            : base(string.Format(KoinaResources.KoinaSmallMoleculeException_KoinaSmallMoleculeException_Koina_only_supports_peptides____0___is_a_small_molecule_, target.Molecule.Name))
        {
        }
    }

    public class KoinaNotConfiguredException : KoinaException
    {
        public KoinaNotConfiguredException() : this(KoinaResources
            .GraphSpectrum_UpdateUI_Some_Koina_settings_are_not_set_)
        {
        }

        public KoinaNotConfiguredException(string message) : base(message)
        {
        }
    }

    public class KoinaPredictingException : KoinaException
    {
        public KoinaPredictingException() : base(KoinaResources.KoinaPredictingException_KoinaPredictingException_Making_predictions___)
        {
        }
    }
}
