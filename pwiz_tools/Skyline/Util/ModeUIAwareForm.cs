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
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Support for UI that adapts between proteomic and small molecule usage. Not part of
    /// <see cref="Helpers"/>, which is in SkylineCore: a partial class cannot span assemblies.
    /// </summary>
    public static class ModeUIHelpers
    {
        /// <summary>
        /// Classes and interfaces for dealing with Skyline UI that adapts to proteomic vs small molecule usage
        /// </summary>

        [ProvideProperty("UIMode", typeof(Component))]   // Custom component property that appears in Designer (via IExtenderProvider)
        public class ModeUIExtender : Component, IExtenderProvider // Behaves like a ToolTip, in that its presence in a form allows us to tag components with ModeUI info in the designer
        {
            private Dictionary<IComponent, MODE_UI_HANDLING_TYPE> _handledComponents = new Dictionary<IComponent, MODE_UI_HANDLING_TYPE>();
            private Dictionary<IComponent, MenuTextAndToolTip> _originalToolStripText = new Dictionary<IComponent, MenuTextAndToolTip>();
            public ModeUIExtender(IContainer container)
            {
                if (container != null)
                {
                    container.Add(this);
                }
            }

            public enum MODE_UI_HANDLING_TYPE
            {
                auto,        // We'll attempt the "peptide"->"molecule" translation on this component in small molecule or mixed UI modes
                proteomic,   // We'll hide this component in small molecule UI mode, and will never attempt the "peptide"->"molecule" translation
                small_mol,   // We'll hide this component in proteomics UI mode, and will never attempt the "peptide"->"molecule" translation
                small_mol_only,   // We'll only this component in small molecule UI mode, and will never attempt the "peptide"->"molecule" translation
                mixed_only,       // We'll only show this component in mixed mode, and will never attempt the "peptide"->"molecule" translation
                invariant    // We'll never hide nor attempt the "peptide"->"molecule" translation on this component
            };

            public bool CanExtend(object extendee)
            {
                return !(extendee is ModeUIExtender);
            }

            #region UIMode
            [DefaultValue(MODE_UI_HANDLING_TYPE.auto),]
            [Description("Determines display and/or 'peptide'->'molecule' translation of control under different UI modes:\n" +
                "auto           // We'll attempt the 'peptide'->'molecule' translation on this component in small molecule or mixed UI modes\n" +
                "proteomic      // We'll hide this component in small molecule UI mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "small_mol      // We'll hide this component in proteomics UI mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "small_mol_only // We'll only show this component in small molecules UI mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "mixed_only     // We'll only show this component in mixed mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "invariant      // We'll never hide nor attempt the 'peptide'->'molecule' translation on this component")]
            [Category("Behavior")]
            public MODE_UI_HANDLING_TYPE GetUIMode(IComponent control)
            {
                MODE_UI_HANDLING_TYPE value;
                return _handledComponents.TryGetValue(control, out value) ? value : MODE_UI_HANDLING_TYPE.auto;
            }

            public void SetUIMode(IComponent component, MODE_UI_HANDLING_TYPE? value)
            {
                if (value.HasValue)
                {
                    _handledComponents[component] = value.Value;
                }
            }
            #endregion

            public Dictionary<IComponent, MODE_UI_HANDLING_TYPE> GetHandledComponents()
            {
                return _handledComponents;
            }

            public struct MenuTextAndToolTip
            {
                public string Text;
                public string ToolTipText;

                public MenuTextAndToolTip(ToolStripMenuItem item) : this(item.Text, item.ToolTipText)
                {
                }

                public MenuTextAndToolTip(string text, string toolTipText)
                {
                    Text = text;
                    ToolTipText = toolTipText;
                }
            }

            public Dictionary<IComponent, MenuTextAndToolTip> GetOriginalToolStripText()
            {
                return _originalToolStripText;
            }

            public void AddHandledComponent(IComponent component, MODE_UI_HANDLING_TYPE type)
            {
                _handledComponents[component] = type;
            }

            public bool ComponentsDisabledForModeUI(IComponent component)
            {
                return FormTextMapper.GetComponentsDisabledForModeUI(_handledComponents, ProgramContext.ModeUI).Contains(component);
            }
        }

        public class ModeUIAwareFormHelper : IDisposable // Assists in adapting the UI from its traditional proteomic language to small molecule language
        {
            public static ModeUIAwareFormHelper DEFAULT = new ModeUIAwareFormHelper(null);

            private SrmDocument.DOCUMENT_TYPE? _modeUI;
            private ModeUIExtender _modeUIExtender;
            public ModeUIAwareFormHelper(ModeUIExtender modeUIExtender)
            {
                _modeUIExtender = modeUIExtender;
            }

            /// <summary>
            /// When appropriate, replace form contents such that "peptide" becomes "molecule" etc
            /// </summary>
            public SrmDocument.DOCUMENT_TYPE ModeUI
            {
                get { return _modeUI ?? ProgramContext.ModeUI; }
                set { _modeUI = value; } // Useful for forms that are truly proteomic or truly nonproteomic: by setting this you override ProgramContext.ModeUI
            }

            /// <summary>
            /// When true, no attempt at translating to mixed or small molecule UI mode will be made, but also no hiding or disabling 
            /// </summary>
            public bool IgnoreModeUI;


            /// <summary>
            /// Optional UI selector buttons we may manage on toolbar of SkylineWindow or Startup window
            /// </summary>
            private ToolStripDropDownButton _modeUIToolBarDropDownButton;

            #region Testing Support

            public SrmDocument.DOCUMENT_TYPE GetUIToolBarButtonState()
            {
                return (SrmDocument.DOCUMENT_TYPE) _modeUIToolBarDropDownButton.DropDownItems
                    .Cast<ToolStripButton>().First(b => b.Checked).Tag;
            }

            #endregion

            public void Dispose()
            {
                if (_modeUIToolBarDropDownButton != null)
                {
                    _modeUIToolBarDropDownButton.DropDown.Dispose();
                    _modeUIToolBarDropDownButton = null;
                }
            }

            public void SetModeUIToolStripButtons(ToolStripDropDownButton modeUIToolBarDropDownButton, Action<SrmDocument.DOCUMENT_TYPE> handler)
            {
                _modeUIToolBarDropDownButton = modeUIToolBarDropDownButton;
                var dropDown = new ToolStripDropDown();
                dropDown.Items.Add(NewButton(ImageResources.UIModeProteomic,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Proteomics_interface,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Only_show_menus_and_controls_appropriate_to_proteomics_analysis,
                    handler, SrmDocument.DOCUMENT_TYPE.proteomic));
                dropDown.Items.Add(NewButton(ImageResources.UIModeSmallMolecules,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Small_Molecules_interface,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Only_show_menus_and_controls_appropriate_to_small_molecule_analysis,
                    handler, SrmDocument.DOCUMENT_TYPE.small_molecules));
                dropDown.Items.Add(NewButton(ImageResources.UIModeMixed,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Mixed_interface,
                    UtilResources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Show_all_menus_and_controls,
                    handler, SrmDocument.DOCUMENT_TYPE.mixed));
                _modeUIToolBarDropDownButton.DropDown = dropDown;
            }

            public static ToolStripButton NewButton(Bitmap image, string text, string tip,
                Action<SrmDocument.DOCUMENT_TYPE> handler, SrmDocument.DOCUMENT_TYPE docType)
            {
                return new ToolStripButton(text, image, (s, e) => handler(docType))
                {
                    ToolTipText = tip,
                    ImageTransparentColor = Color.White,
                    Tag = docType
                };
            }

            public void UpdateButtonImageForModeUI()
            {
                foreach (ToolStripButton button in _modeUIToolBarDropDownButton.DropDownItems)
                {
                    button.Checked = Equals(button.Tag, ModeUI);
                    if (button.Checked)
                        _modeUIToolBarDropDownButton.Image = button.Image;
                }
            }

            // Potentially replace "peptide" with "molecule" etc in all controls on open, or possibly disable non-proteomic components etc
            public void OnLoad(Form form)
            {
                if (!IgnoreModeUI)
                {
                    FormTextMapper.TranslateForm(form, ModeUI, _modeUIExtender);
                }
            }

            public string Translate(string txt)
            {
                return IgnoreModeUI ? txt : Helpers.PeptideToMoleculeTextMapper.Translate(txt, ModeUI);
            }

            // Like string.Format, but does UIMode translation on the format string (but not the args)
            public string Format(string format, params object[] args)
            {
                return IgnoreModeUI ? string.Format(format, args) : Helpers.PeptideToMoleculeTextMapper.Format(format, ModeUI, args);
            }

            internal void AdjustMenusForModeUI(ToolStripItemCollection items)
            {
                var dictOriginalText = _modeUIExtender.GetOriginalToolStripText();

                foreach (var item in items)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        if (!dictOriginalText.TryGetValue(menuItem, out var originalText))
                        {
                            // Preserve original text in case we need to restore later
                            dictOriginalText[menuItem] = new ModeUIExtender.MenuTextAndToolTip(menuItem);
                        }
                        else
                        {
                            // Restore original text so translator has a clean start
                            menuItem.Text = originalText.Text;
                            menuItem.ToolTipText = originalText.ToolTipText;
                        }
                    }
                }

                // Update text, swapping "peptide" for "molecule" etc, except as specifically prohibited
                FormTextMapper.TranslateMenuItems(items, ModeUI, _modeUIExtender);


                var owner = items[0].Owner;
                if (owner != null)
                {
                    owner.Update();
                }

                // Recurse into sub menus
                foreach (var item in items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.DropDownItems.Count > 0)
                    {
                        AdjustMenusForModeUI(menuItem.DropDownItems);
                    }
                }
            }

            public bool MenuItemHasOriginalText(ToolStripMenuItem toolStripMenuItem)
            {
                foreach (var item in _modeUIExtender.GetOriginalToolStripText().Keys)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        if (Equals(toolStripMenuItem.Text, menuItem.Text) && Equals(toolStripMenuItem.ToolTipText, menuItem.ToolTipText))
                        {
                            var menuTextAndToolTip = _modeUIExtender.GetOriginalToolStripText()[menuItem];
                            return Equals(toolStripMenuItem.Text, menuTextAndToolTip.Text) && Equals(toolStripMenuItem.ToolTipText, menuTextAndToolTip.ToolTipText);
                        }
                    }
                }
                return false;
            }


            public static void SetComponentEnabledStateForModeUI(Component component, IList<ToolTip> toolTipControls, bool isDesired)
            {
                if (component is ToolStripMenuItem item)
                {
                    item.Visible = isDesired;
                    return;
                }

                if (component is TabPage tabPage)
                {
                    var parent = tabPage.Parent as TabControl;
                    if (parent != null)
                    {
                        if (!isDesired)
                        {
                            FormUtil.RemoveTabPage(tabPage, toolTipControls);
                        }
                        return;
                    }
                }

                if (component is Control ctrl)
                {
                    ctrl.Visible = isDesired;
                    return;
                }

                Assume.Fail();
            }

            public void NoteModeUIInvariantComponent(IComponent component)
            {
                _modeUIExtender.AddHandledComponent(component, ModeUIExtender.MODE_UI_HANDLING_TYPE.invariant);
            }

            public void AttemptChangeModeUI(SrmDocument.DOCUMENT_TYPE new_uimode)
            {
                var doc = Program.ActiveDocument;
                var hasPeptides = doc != null && doc.DocumentType != SrmDocument.DOCUMENT_TYPE.none && doc.DocumentType != SrmDocument.DOCUMENT_TYPE.small_molecules;
                var hasSmallMolecules = doc != null && doc.DocumentType != SrmDocument.DOCUMENT_TYPE.none && doc.DocumentType != SrmDocument.DOCUMENT_TYPE.proteomic;

                string message = null;
                if (new_uimode == SrmDocument.DOCUMENT_TYPE.proteomic && hasSmallMolecules)
                {
                    message = UtilResources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Cannot_switch_to_proteomics_interface_because_the_current_document_contains_small_molecules_data_;
                }
                else if (new_uimode == SrmDocument.DOCUMENT_TYPE.small_molecules && hasPeptides)
                {
                    message = UtilResources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Cannot_switch_to_molecule_interface_because_the_current_document_contains_proteomics_data_;
                }

                if (message != null)
                {
                    message = TextUtil.LineSeparate(message,@" ", UtilResources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Would_you_like_to_create_a_new_document_);
                    using (var alert = new AlertDlg(message, MessageBoxButtons.YesNo))
                    {
                        if (alert.ShowAndDispose(_modeUIToolBarDropDownButton.GetCurrentParent()) == DialogResult.Yes)
                        {
                            Program.MainWindow.NewDocument();
                        }
                    }
                    if (Program.ActiveDocument.DocumentType != SrmDocument.DOCUMENT_TYPE.none)
                        return; // User canceled out of NewDocument, no change to UI mode
                }

                ModeUI = new_uimode;

                UpdateButtonImageForModeUI();

                Settings.Default.UIMode = ModeUI.ToString();
            }
        }

        public interface IModeUIAwareForm
        {
            /// <summary>
            /// When appropriate per current UI mode, replace form contents such that "peptide" becomes "molecule" etc
            /// </summary>
            ModeUIAwareFormHelper GetModeUIHelper();

            /// <summary>
            /// When appropriate per current UI mode, replace format contents such that "peptide" becomes "molecule" etc
            /// </summary>
            string ModeUIAwareStringFormat(string format, params object[] args);
        }

        /// <summary>
        /// Applies <see cref="Helpers.PeptideToMoleculeTextMapper"/> to the controls, tool tips and menu
        /// items of a form, honoring what a <see cref="ModeUIExtender"/> marks as handled.
        /// </summary>
        public class FormTextMapper : Helpers.PeptideToMoleculeTextMapper
        {
            private List<ToolTip> ToolTips; // Used when working on an entire form

            public FormTextMapper(SrmDocument.DOCUMENT_TYPE modeUI, ModeUIExtender extender)
                : base(modeUI, extender != null)
            {
                ToolTips = null;
                HandledComponents = extender == null ? new Dictionary<IComponent, ModeUIExtender.MODE_UI_HANDLING_TYPE>() : extender.GetHandledComponents();
            }

            public Dictionary<IComponent, ModeUIExtender.MODE_UI_HANDLING_TYPE> HandledComponents { get; set; } // Used when working on an entire form or menu (can be set in ctor for test purposes) 

            // For all controls in a form, attempt to take a string like "{0} peptides" and return one like "{0} molecules" if doctype is not purely proteomic
            public static void TranslateForm(Form form, SrmDocument.DOCUMENT_TYPE modeUI, ModeUIExtender extender = null)
            {
                if (form != null)
                {
                    var mapper = new FormTextMapper(modeUI, extender);
                    form.Text = mapper.TranslateString(form.Text); // Update the title

                    // Find tooltip components in the form, if any
                    mapper.ToolTips = (from field in form.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        where typeof(Component).IsAssignableFrom(field.FieldType)
                        let component = (Component)field.GetValue(form)
                        where component is ToolTip
                        select component as ToolTip).ToList();

                    mapper.FindInUseKeyboardAccelerators(form.Controls);

                    mapper.Translate(form.Controls); // Update the controls
                }
            }

            // For all items in a menu, attempt to take a string like "{0} peptides" and return one like "{0} molecules" if menu item is not purely proteomic
            // Update keyboard accelerators as needed
            // When recurse is true, also translates submenu items (e.g. "Peptide Comparison" under Graph submenu)
            public static void TranslateMenuItems(ToolStripItemCollection items, SrmDocument.DOCUMENT_TYPE modeUI, ModeUIExtender extender, bool recurse = false)
            {
                var mapper = new FormTextMapper(modeUI, extender);
                if (items != null)
                {
                    mapper.FindInUseKeyboardAccelerators(items);
                    var activeItems = new List<ToolStripItem>();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        ModeUIExtender.MODE_UI_HANDLING_TYPE handlingType;
                        if (!mapper.HandledComponents.TryGetValue(item, out handlingType))
                        {
                            handlingType = ModeUIExtender.MODE_UI_HANDLING_TYPE.auto;
                        }

                        bool isActive;
                        switch (modeUI)
                        {
                            case SrmDocument.DOCUMENT_TYPE.proteomic:
                                isActive = handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol &&
                                           handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol_only &&
                                           handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.mixed_only;
                                break;
                            case SrmDocument.DOCUMENT_TYPE.small_molecules:
                                isActive = handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic &&
                                           handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.mixed_only;
                                break;
                            case SrmDocument.DOCUMENT_TYPE.mixed:
                                isActive = handlingType != ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol_only;
                                break;
                            default:
                                isActive = false;
                                Assume.Fail(@"unknown UI mode");
                                break;
                        }

                        if (isActive)
                        {
                            activeItems.Add(item);
                        }
                        item.Visible = isActive;
                    }
                    mapper.Translate(activeItems); // Update the menu items that aren't inherently wrong for current UI mode

                    if (recurse)
                    {
                        foreach (var menuItem in activeItems.OfType<ToolStripMenuItem>().Where(m => m.HasDropDownItems))
                        {
                            TranslateMenuItems(menuItem.DropDownItems, modeUI, extender, true);
                        }
                    }
                }
            }

            public void Translate(IEnumerable controls)
            {
                // Prepare to disable anything tagged as being incompatible with current UI mode
                var inappropriateComponents = GetComponentsDisabledForModeUI(HandledComponents, ModeUI);

                foreach (var control in controls)
                {
                    var ctrl = control as Control;

                    // Disable anything tagged as being incompatible with current UI mode
                    var component = control as Component;
                    if (inappropriateComponents != null && 
                        inappropriateComponents.Contains(component))
                    {
                        ModeUIAwareFormHelper.SetComponentEnabledStateForModeUI(ctrl, ToolTips, false);
                    }

                    ModeUIExtender.MODE_UI_HANDLING_TYPE handling;
                    var doNotTranslate = component != null && 
                                         HandledComponents.TryGetValue(component, out handling) &&
                                         (handling != ModeUIExtender.MODE_UI_HANDLING_TYPE.auto); 

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
                            if (!string.IsNullOrEmpty(menuItem.ToolTipText))
                            {
                                translated = TranslateString(menuItem.ToolTipText);
                                if (!Equals(translated, menuItem.ToolTipText))
                                {
                                    menuItem.ToolTipText = translated;
                                }
                            }
                        }
                        continue;
                    }

                    if (doNotTranslate)
                    {
                        continue; // Marked as not needing translation, skip this and its children
                    }

                    // Tool tips
                    var toolTip = ToolTips?.FirstOrDefault(t => !string.IsNullOrEmpty(t.GetToolTip(ctrl)));
                    if (toolTip != null)
                    {
                        var tipText = toolTip.GetToolTip(ctrl);
                        toolTip.SetToolTip(ctrl, TranslateString(tipText));
                    }

                    if (!(ctrl is TextBox)) // Don't mess with the user edit area
                    {
                        ctrl.Text = TranslateString(ctrl.Text);
                    }

                    // Special controls
                    if (control is CommonDataGridView cgv)
                    {
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
                    else if (control is TabControl tabs)
                    {
                        Translate(tabs.TabPages); // Handle controls inside each page
                    }
                    // N.B. for CheckedListBox the translation has to be handled upstream from here, because its a list of objects instead of simple strings
                    else
                    {
                        Translate(ctrl.Controls);
                    }
                }
            }

            public static HashSet<IComponent> GetComponentsDisabledForModeUI(Dictionary<IComponent, ModeUIExtender.MODE_UI_HANDLING_TYPE> handledComponents, SrmDocument.DOCUMENT_TYPE modeUI)
            {
                var inappropriateComponents = modeUI == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? handledComponents.Where(item => item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol) ||
                                                      item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol_only) ||
                                                      item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.mixed_only))
                        .Select(item => item.Key).ToHashSet()
                    : modeUI == SrmDocument.DOCUMENT_TYPE.small_molecules
                        ? handledComponents.Where(item => item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.proteomic) ||
                                                          item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.mixed_only))
                            .Select(item => item.Key).ToHashSet()
                        : modeUI == SrmDocument.DOCUMENT_TYPE.mixed
                            ? handledComponents
                                .Where(item => item.Value.Equals(ModeUIExtender.MODE_UI_HANDLING_TYPE.small_mol_only))
                                .Select(item => item.Key).ToHashSet()
                            : null;
                return inappropriateComponents;
            }

            private void FindInUseKeyboardAccelerators(IEnumerable controls)
            {
                const char amp = '&';
                foreach (var control in controls)
                {
                    if (!(control is Control ctrl))
                    {
                        if (control is ToolStripItem menuItem)
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
                    if (control is GroupBox gb)
                    {
                        FindInUseKeyboardAccelerators(gb.Controls); // Handle controls inside the groupbox
                    }
                    else if (control is SplitContainer sc)
                    {
                        FindInUseKeyboardAccelerators(sc.Controls); // Handle controls inside the splits
                    }
                    else if (control is SplitterPanel sp)
                    {
                        FindInUseKeyboardAccelerators(sp.Controls); // Handle controls inside the splits
                    }
                }
            }
        }
    }
}