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
using System;
using System.IO;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util.Nomenclature
{
    /// <summary>
    /// The ordered set of <see cref="INomenclatureInterpreter"/>s and the single fallback entry
    /// point <see cref="TryDeriveFormula"/>, invoked when a user's text does not parse as an
    /// ordinary chemical formula. Each additional nomenclature mode (lipid, oligonucleotide,
    /// glycan, and so on) is added simply by registering another interpreter here.
    /// </summary>
    public static class NomenclatureRegistry
    {
        private static readonly INomenclatureInterpreter[] INTERPRETERS =
        {
            new InChiFormulaInterpreter()
        };

        /// <summary>
        /// Attempts to derive a formula from <paramref name="text"/>. When <paramref name="modeKey"/>
        /// is null or empty the mode is "Auto": each interpreter whose
        /// <see cref="INomenclatureInterpreter.CanHandle"/> accepts the text is tried in order.
        /// Otherwise only the interpreter with the matching <see cref="INomenclatureInterpreter.ModeKey"/>
        /// is used.
        /// Returns false with a null <paramref name="error"/> when nothing recognizes the text, and
        /// false with a non-null <paramref name="error"/> (the offending text) when an interpreter
        /// claimed the text but could not derive a formula. Callers compose the localized,
        /// user-facing message from that value.
        /// </summary>
        public static bool TryDeriveFormula(string text, string modeKey,
            out ParsedMolecule formula, out MoleculeAccessionNumbers idsFound, out string error)
        {
            formula = ParsedMolecule.EMPTY;
            idsFound = MoleculeAccessionNumbers.EMPTY;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var explicitMode = !string.IsNullOrEmpty(modeKey);
            foreach (var interpreter in INTERPRETERS)
            {
                if (explicitMode)
                {
                    if (!string.Equals(interpreter.ModeKey, modeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                else if (!interpreter.CanHandle(text))
                {
                    continue;
                }

                try
                {
                    formula = interpreter.Derive(text, out idsFound);
                    return true;
                }
                catch (Exception e) when (IsParserException(e))
                {
                    // The interpreter claimed the text but could not derive a formula. Surface the
                    // offending value so the caller can build a localized message, and stop: a
                    // positive CanHandle (or an explicit mode selection) is authoritative, so we do
                    // not silently fall through to a different interpreter.
                    error = e.Message;
                    return false;
                }
            }

            return false;
        }

        private static bool IsParserException(Exception e)
        {
            return e is InvalidDataException ||
                   e is InvalidOperationException ||
                   e is ArgumentException ||
                   e is FormatException;
        }
    }
}
