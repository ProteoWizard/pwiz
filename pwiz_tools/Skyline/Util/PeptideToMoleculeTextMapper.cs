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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    public static partial class Helpers
    {
        /// <summary>
        /// Helper class to aid in translating peptide-oriented text to molecule-oriented text in a FormEx or menu
        /// </summary>
        public class PeptideToMoleculeTextMapper
        {
            private readonly List<KeyValuePair<string, string>> TRANSLATION_TABLE;
            private HashSet<char> InUseKeyboardAccelerators;  // Used when working on an entire form or menu (can be set in ctor for test purposes)
            private ToolTip ToolTip; // Used when working on an entire form
            private readonly SrmDocument.DOCUMENT_TYPE ModeUI;

            public PeptideToMoleculeTextMapper(SrmDocument.DOCUMENT_TYPE modeUI, HashSet<char> inUseKeyboardAccelerators = null)
            {
                // The basic replacements (not L10N to pick up not-yet-localized UI)
                var dict = new Dictionary<string, string>
                {
                    // ReSharper disable LocalizableElement
                    {"Peptide", "Molecule"},
                    {"Peptides", "Molecules"},
                    {"Protein", "Molecule List"},
                    {"Proteins", "Molecule Lists"},
                    {"Modified Sequence", "Molecule"},
                    {"Peptide Sequence", "Molecule"},
                    {"Peptide List", "Molecule List"},
                    // ReSharper restore LocalizableElement
                };
                // Handle lower case as well
                foreach (var kvp in dict.ToArray())
                {
                    dict.Add(kvp.Key.ToLowerInvariant(), kvp.Value.ToLowerInvariant());
                }

                ModeUI = modeUI;
                
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
                    new KeyValuePair<string, string>(Resources.PeptideToMoleculeText_Peptide_List,
                        Resources.PeptideToMoleculeText_Molecule_List)
                };
                foreach (var kvp in setL10N)
                {
                    set.Add(kvp);
                    set.Add(new KeyValuePair<string, string>(kvp.Key.ToLower(), kvp.Value.ToLower()));
                }

                // Sort so we look for longer replacements first
                var list = set.ToList();
                list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
                TRANSLATION_TABLE = new List<KeyValuePair<string, string>>(list);
                InUseKeyboardAccelerators = inUseKeyboardAccelerators;
                ToolTip = null;
                InherentlyProteomicComponents = new HashSet<Component>();
                InherentlyNonProteomicComponents = new HashSet<Component>();
                ModeUIInvariantComponents = new HashSet<Component>();
            }

            public HashSet<Component> InherentlyProteomicComponents { get; set; } // Used when working on an entire form or menu (can be set in ctor for test purposes) 
            public HashSet<Component> InherentlyNonProteomicComponents { get; set; } // Used when working on an entire form or menu (can be set in ctor for test purposes) 
            public HashSet<Component> ModeUIInvariantComponents { get; set; } // Used when working on an entire form or menu (can be set in ctor for test purposes) 

            public static string Translate(string text, bool forceSmallMolecule)
            {
                if (!forceSmallMolecule)
                {
                    return text;
                }

                var mapper = new PeptideToMoleculeTextMapper(SrmDocument.DOCUMENT_TYPE.small_molecules);
                return mapper.TranslateString(text);
            }

            // Attempt to take a string like "{0} peptides" and return one like "{0} molecules" if doctype is not purely proteomic
            public static string Translate(string text, SrmDocument.DOCUMENT_TYPE modeUI)
            {
                var mapper = new PeptideToMoleculeTextMapper(modeUI);
                return mapper.TranslateString(text);
            }

            // For all controls in a form, attempt to take a string like "{0} peptides" and return one like "{0} molecules" if doctype is not purely proteomic
            public static void Translate(Form form, SrmDocument.DOCUMENT_TYPE modeUI, HashSet<Component> inherentlyProteomicComponents = null, HashSet<Component> inherentlyNonProteomicComponents = null, HashSet<Component> modeUIInvariantComponents = null)
            {
                if (form != null)
                {
                    var mapper = new PeptideToMoleculeTextMapper(modeUI);
                    form.Text = mapper.TranslateString(form.Text); // Update the title

                    // Find a tooltip component in the form, if any
                    var tips = (from field in form.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        where typeof(Component).IsAssignableFrom(field.FieldType)
                        let component = (Component)field.GetValue(form)
                        where component is ToolTip
                        select component as ToolTip).ToArray();
                    mapper.ToolTip = tips.FirstOrDefault();

                    if (inherentlyProteomicComponents != null)
                    {
                        mapper.InherentlyProteomicComponents = inherentlyProteomicComponents;
                    }
                    if (inherentlyNonProteomicComponents != null)
                    {
                        mapper.InherentlyNonProteomicComponents = inherentlyNonProteomicComponents;
                    }
                    if (modeUIInvariantComponents != null)
                    {
                        mapper.ModeUIInvariantComponents = modeUIInvariantComponents;
                    }
                    mapper.InUseKeyboardAccelerators = new HashSet<char>();
                    mapper.FindInUseKeyboardAccelerators(form.Controls);

                    mapper.Translate(form.Controls); // Update the controls
                }
            }

            // For all items in a menu, attempt to take a string like "{0} peptides" and return one like "{0} molecules" if menu item is not purely proteomic
            // Return true if anything needed translating
            public static void Translate(ToolStripItemCollection items, SrmDocument.DOCUMENT_TYPE modeUI, HashSet<Component> inherentlyProteomic, HashSet<Component> inherentlyNonProteomic)
            {
                var mapper = new PeptideToMoleculeTextMapper(modeUI);
                if (items != null)
                {
                    mapper.InUseKeyboardAccelerators = new HashSet<char>();
                    mapper.FindInUseKeyboardAccelerators(items);
                    mapper.InherentlyProteomicComponents = inherentlyProteomic;
                    mapper.InherentlyNonProteomicComponents = inherentlyNonProteomic;
                    var activeItems = new List<ToolStripItem>();
                    for (int i = 0; i < items.Count; i++)
                    {
                        switch (modeUI)
                        {
                            case SrmDocument.DOCUMENT_TYPE.proteomic:
                                if (!mapper.InherentlyNonProteomicComponents.Contains(items[i]))
                                {
                                    activeItems.Add(items[i]);
                                }
                                break;
                            case SrmDocument.DOCUMENT_TYPE.small_molecules:
                                if (!mapper.InherentlyProteomicComponents.Contains(items[i]))
                                {
                                    activeItems.Add(items[i]);
                                }
                                break;
                            default:
                                activeItems.Add(items[i]);
                                break;
                        }
                    }
                    mapper.Translate(activeItems); // Update the menu items that aren't inherently wrong for current UI mode
                }
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

            public void Translate(IEnumerable controls)
            {
                // Prepare to disable anything tagged as being incomptible with current UI mode
                var inappropriateComponents = ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? InherentlyNonProteomicComponents
                    : ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules
                        ? InherentlyProteomicComponents
                        : null;

                foreach (var control in controls)
                {
                    var ctrl = control as Control;

                    // Disable anything tagged as being incompatible with current UI mode
                    var component = control as Component;
                    if (inappropriateComponents != null && 
                        inappropriateComponents.Contains(component))
                    {
                        ModeUIAwareFormHelper.SetComponentStateForModeUI(ctrl, false);
                    }

                    var doNotTranslate = InherentlyNonProteomicComponents.Contains(component) ||
                                         InherentlyProteomicComponents.Contains(component) ||
                                         ModeUIInvariantComponents.Contains(component);
                    if (ctrl == null)
                    {
                        // Not a normal control - is it a menu item?
                        var menuItem = control as ToolStripMenuItem;
                        if (menuItem != null && !doNotTranslate)
                        {
                            // Menu item
                            var translated = TranslateString(menuItem.Text);
                            if (!Equals(translated, menuItem.Text))
                            {
                                menuItem.Text = translated;
                            }
                        }
                        continue;
                    }

                    if (doNotTranslate)
                    {
                        continue; // Marked as not needing translation, skip this and its children
                    }

                    // Tool tips
                    if (ToolTip != null)
                    {
                        var tip = ToolTip.GetToolTip(ctrl);
                        if (!String.IsNullOrEmpty(tip))
                        {
                            ToolTip.SetToolTip(ctrl, TranslateString(tip));
                        }
                    }

                    if (!(ctrl is TextBox)) // Don't mess with the user edit area
                    {
                        ctrl.Text = TranslateString(ctrl.Text);
                    }

                    // Special controls
                    if (control is CommonDataGridView)
                    {
                        var cgv = control as CommonDataGridView;
                        // Make sure there's not already a mix of peptide and molecule language in the columns
                        var xlate = true;
                        foreach (var c in cgv.Columns)
                        {
                            var col = c as DataGridViewColumn;
                            if (col != null && TRANSLATION_TABLE.Any(kvp => col.HeaderText.Contains(kvp.Value)))
                            {
                                xlate = false;
                                break;
                            }
                        }

                        if (xlate)
                        {
                            foreach (var c in cgv.Columns)
                            {
                                var col = c as DataGridViewColumn;
                                if (col != null)
                                {
                                    col.HeaderText = TranslateString(col.HeaderText);
                                }
                            }
                        }
                    }
                    else if (control is TabControl)
                    {
                        var tabs = control as TabControl;
                        Translate(tabs.TabPages); // Handle controls inside each page
                    }
                    // N.B. for CheckedListBox the translation has to be handled upstream from here, because its a list of objects instead of simple strings
                    else
                    {
                        Translate(ctrl.Controls);
                    }
                }
            }
            private void FindInUseKeyboardAccelerators(IEnumerable controls)
            {
                var amp = '&';
                foreach (var control in controls)
                {
                    var ctrl = control as Control;

                    if (ctrl == null)
                    {
                        var menuItem = control as ToolStripItem;
                        if (menuItem != null)
                        {
                            var index = menuItem.Text.IndexOf(amp);
                            if (index >= 0)
                            {
                                InUseKeyboardAccelerators.Add(Char.ToLower(menuItem.Text[index + 1]));
                            }
                        }
                        continue;
                    }

                    if (!(ctrl is TextBox)) // Don't mess with the user edit area
                    {
                        var index = ctrl.Text.IndexOf(amp);
                        if (index >= 0)
                        {
                            InUseKeyboardAccelerators.Add(Char.ToLower(ctrl.Text[index + 1]));
                        }
                    }

                    // Special controls
                    var gb = control as GroupBox;
                    if (gb != null)
                    {
                        FindInUseKeyboardAccelerators(gb.Controls); // Handle controls inside the groupbox
                    }
                    else
                    {
                        var sc = control as SplitContainer;
                        if (sc != null)
                        {
                            FindInUseKeyboardAccelerators(sc.Controls); // Handle controls inside the splits
                        }
                        else
                        {
                            var sp = control as SplitterPanel;
                            if (sp != null)
                            {
                                FindInUseKeyboardAccelerators(sp.Controls); // Handle controls inside the splits
                            }
                        }
                    }
                }
            }
        }
    }
}
