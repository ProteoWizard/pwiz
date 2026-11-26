/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    public static partial class Helpers
    {
        /// <summary>
        /// Translates peptide-oriented text to molecule-oriented text. Applying it to the controls and
        /// menus of a form is FormTextMapper, in the Skyline UI.
        /// </summary>
        public class PeptideToMoleculeTextMapper
        {
            public static readonly PeptideToMoleculeTextMapper SMALL_MOLECULE_MAPPER = new PeptideToMoleculeTextMapper(SrmDocument.DOCUMENT_TYPE.small_molecules, false);

            protected readonly List<KeyValuePair<string, string>> TRANSLATION_TABLE;
            protected readonly SrmDocument.DOCUMENT_TYPE ModeUI;

            /// <param name="modeUI">The UI mode to translate for</param>
            /// <param name="currentCultureOnly">Add the localized terms of the current UI culture only,
            /// rather than of every culture: false for a long-lived mapper, since the culture can change
            /// during its lifetime in the test system</param>
            public PeptideToMoleculeTextMapper(SrmDocument.DOCUMENT_TYPE modeUI, bool currentCultureOnly)
            {
                // Japanese has a set of characters that are easily swapped between Hiragana and Katakana
                // One orginally appeared in about 2% of the translations of the word "peptide"
//                const char jaHeHiragana = 'へ';
//                const char jaHeKatakana = 'ヘ';
//                const char jaBeHiragana = 'べ';
//                const char jaBeKatakana = 'ベ';
                const char jaPeHiragana = 'ぺ';  // Used as a typo in "peptide"
                const char jaPeKatakana = 'ペ';  // Used in "peptide"

                // The basic replacements (not L10N to pick up not-yet-localized UI - maintain the list below in concert with this one)
                var dict = new Dictionary<string, string>
                {
                    // ReSharper disable LocalizableElement
                    {"Peptide", "Molecule"},
                    {"Peptides", "Molecules"},
                    {"Protein", "Molecule List"},
                    {"Proteins", "Molecule Lists"},
                    {"Modified Sequence", "Molecule"},
                    {"Peptide Sequence", "Molecule"},
                    {"Modified Peptide Sequence", "Molecule"},
                    {"Ion Charges", "Ion adducts" }
                    // ReSharper restore LocalizableElement
                };
                // Handle lower case as well
                foreach (var kvp in dict.ToArray())
                {
                    dict.Add(kvp.Key.ToLowerInvariant(), kvp.Value.ToLowerInvariant());
                    // Also handle combinations of upper/lower case (eg "Modified peptide sequence")
                    var space = kvp.Key.IndexOf(' ');
                    if (space > 0)
                    {
                        dict.Add(kvp.Key.Substring(0, space) + kvp.Key.Substring(space).ToLowerInvariant(), kvp.Value);
                    }
                }

                ModeUI = modeUI == SrmDocument.DOCUMENT_TYPE.none ? SrmDocument.DOCUMENT_TYPE.proteomic : modeUI; // SrmDocument.DOCUMENT_TYPE.none would be unusual, but would still mean no translation

                // Handle keyboard accelerators where possible: P&eptide => Mol&ecule
                var set = new HashSet<KeyValuePair<string, string>>();
                foreach (var kvp in dict)
                {
                    var pep = kvp.Key.ToLowerInvariant();
                    var mol = kvp.Value.ToLowerInvariant();
                    foreach (var c in pep.Distinct().Where(c => Char.IsLetterOrDigit(c) && mol.Contains(c)))
                    {
                        var positionP = pep.IndexOf(c);
                        var positionM = mol.IndexOf(c);
                        // Prefer to map "Peptide &List" to "Molecule &List" rather than "Mo&lecule List"
                        if (Char.IsUpper(kvp.Key[positionP]))
                        {
                            var positionU = kvp.Value.IndexOf(kvp.Key[positionP]);
                            if (positionU >= 0)
                            {
                                positionM = positionU;
                            }
                        }

                        var amp = @"&";
                        set.Add(new KeyValuePair<string, string>(kvp.Key.Insert(positionP, amp), kvp.Value.Insert(positionM, amp)));
                    }
                    set.Add(kvp);
                }

                // Add localized versions, if any
                // NB this assumes that localized versions of Skyline are non-western, and don't attempt to embed keyboard accelerators in control texts
                var currentUICulture = Thread.CurrentThread.CurrentUICulture;
                var cultureNames = currentCultureOnly
                    ? new[] {currentUICulture.Name}
                    : new[] {@"zh-Hans", @"ja" };  // Culture can change in lifetime of a static object in our test system, so include all
                foreach (var cultureName in cultureNames)
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);
                    var setL10N = new HashSet<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Peptide,
                            Resources.PeptideToMoleculeText_Molecule),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Peptides,
                            Resources.PeptideToMoleculeText_Molecules),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Protein,
                            Resources.PeptideToMoleculeText_Molecule_List),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Proteins,
                            Resources.PeptideToMoleculeText_Molecule_Lists),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Peptide_Sequence,
                            Resources.PeptideToMoleculeText_Molecule),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Modified_Sequence,
                            Resources.PeptideToMoleculeText_Molecule),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Modified_Peptide_Sequence,
                            Resources.PeptideToMoleculeText_Molecule),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Peptide_List,
                            Resources.PeptideToMoleculeText_Molecule_List),
                        new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Ion_charges,
                            Resources.PeptideToMoleculeText_Ion_adducts)
                    };
                    foreach (var kvp in setL10N.Where(kp => !dict.ContainsKey(kp.Key))) // Avoids adding not-yet-translated keys
                    {
                        set.Add(kvp);
                        set.Add(new KeyValuePair<string, string>(kvp.Key.ToLower(), kvp.Value.ToLower()));
                        // As protection, just in case the Hiragana "Pe" ever shows up in our translations again
                        if (cultureName.Equals(@"ja") && kvp.Key.Contains(jaPeKatakana))
                            set.Add(new KeyValuePair<string, string>(kvp.Key.Replace(jaPeKatakana, jaPeHiragana), kvp.Value));
                    }
                }
                Thread.CurrentThread.CurrentUICulture = currentUICulture;

                // Sort so we look for longer replacements first
                var list = set.ToList();
                list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
                TRANSLATION_TABLE = new List<KeyValuePair<string, string>>(list);
                InUseKeyboardAccelerators = new HashSet<char>();
            }

            public HashSet<char> InUseKeyboardAccelerators { get; set; }  // Used when working on an entire form or menu (be set explicitly for test purposes)

            public static string Translate(string text, bool forceSmallMolecule)
            {
                if (!forceSmallMolecule)
                {
                    return text;
                }

                return SMALL_MOLECULE_MAPPER.TranslateString(text);
            }

            // Attempt to take a string like "{0} peptides" and return one like "{0} molecules" if doctype is not purely proteomic
            public static string Translate(string text, SrmDocument.DOCUMENT_TYPE modeUI)
            {
                if (modeUI != SrmDocument.DOCUMENT_TYPE.mixed && modeUI != SrmDocument.DOCUMENT_TYPE.small_molecules)
                    return text;
                return SMALL_MOLECULE_MAPPER.TranslateString(text);
            }

            public static string Format(string format, SrmDocument.DOCUMENT_TYPE modeUI, params object[] args)
            {
                if (modeUI != SrmDocument.DOCUMENT_TYPE.mixed && modeUI != SrmDocument.DOCUMENT_TYPE.small_molecules)
                    return string.Format(format, args);
                return SMALL_MOLECULE_MAPPER.FormatTranslateString(format, args);
            }

            public string FormatTranslateString(string format, params object[] args)
            {
                return string.Format(TranslateString(format), args);
            }

            public string TranslateString(string text) // Public for test purposes
            {
                if (ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic || string.IsNullOrEmpty(text))
                {
                    return text;
                }

                var noAmp = text.Replace(@"&", String.Empty);
                if (TRANSLATION_TABLE.Any(kvp => noAmp.Contains(kvp.Value))) // Avoid "p&eptides are a kind of molecule" => "mol&ecules are a kind of molecule" 
                {
                    return text;
                }
                foreach (var kvp in TRANSLATION_TABLE)
                {
                    // Replace each occurrence, but be careful not to change &Peptide to &Molecule
                    for (var i = 0; ; )
                    {
                        i = text.IndexOf(kvp.Key, i, StringComparison.Ordinal);
                        if (i >= 0) // Found something to replace
                        {
                            if (!(i > 0 && text[i - 1] == '&')) // Watch for & before match - if we wanted to match it we already would have since table is sorted long to short
                            {
                                text = text.Substring(0, i) + kvp.Value + text.Substring(i + kvp.Key.Length);
                                i += kvp.Value.Length;
                            }
                            else
                            {
                                i += kvp.Key.Length;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // Did we get tripped up by & keyboard accelerators?
                noAmp = text.Replace(@"&", String.Empty);
                if (InUseKeyboardAccelerators != null && TRANSLATION_TABLE.Any(kvp => noAmp.Contains(kvp.Key)))
                {
                    // See if the proposed replacement has any letters that aren't in use as accelerators elsewhere
                    noAmp = TranslateString(noAmp);
                    var accel = noAmp.FirstOrDefault(c => Char.IsLetterOrDigit(c) && !InUseKeyboardAccelerators.Contains(Char.ToLower(c)));
                    var index = noAmp.IndexOf(accel);
                    if (index >= 0)
                    {
                        var indexU = noAmp.IndexOf(Char.ToUpper(accel)); // Prefer upper case 
                        text = noAmp.Insert((indexU >= 0) ? indexU : index, @"&");
                        InUseKeyboardAccelerators.Add(Char.ToLower(accel));
                    }
                }
                // If no keyboard accelerator available, proceed without one
                if (TRANSLATION_TABLE.Any(kvp => text.Contains(kvp.Key)))
                {
                    return noAmp;
                }

                return text;
            }
        }
    }
}
