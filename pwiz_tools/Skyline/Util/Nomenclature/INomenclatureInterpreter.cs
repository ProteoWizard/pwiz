/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util.Nomenclature
{
    /// <summary>
    /// Derives a molecular formula from a richer nomenclature than a plain chemical formula
    /// (for example an InChI string, a lipid or glycan shorthand, or an oligonucleotide sequence).
    /// One implementation exists per supported mode; implementations are registered in
    /// <see cref="NomenclatureRegistry"/> and invoked as a fallback when a user's text does not
    /// parse as an ordinary chemical formula.
    /// </summary>
    public interface INomenclatureInterpreter
    {
        /// <summary>
        /// Stable, lower-case, non-localized identifier for this mode (for example "inchi").
        /// Used as the recognized value in the transition-list "Formula Source" column and as the
        /// selection key for the interactive "Interpret as" control.
        /// </summary>
        string ModeKey { get; }

        /// <summary>
        /// Cheap shape or prefix test used in "Auto" mode to decide whether this interpreter should
        /// attempt <see cref="Derive"/> on the given text. Must not throw.
        /// </summary>
        bool CanHandle(string text);

        /// <summary>
        /// Derives a formula from the given text, also returning any molecule identifiers recognized
        /// along the way (for example the InChI itself). Throws a parser exception whose message is
        /// the offending text when the text cannot be interpreted; callers compose the localized,
        /// user-facing message from that value.
        /// </summary>
        ParsedMolecule Derive(string text, out MoleculeAccessionNumbers idsFound);
    }
}
