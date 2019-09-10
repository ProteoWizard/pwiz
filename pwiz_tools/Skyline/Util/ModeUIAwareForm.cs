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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    public static partial class Helpers
    {
        /// <summary>
        /// Classes and interfaces for dealing with Skyline UI that adapts to proteomic vs small molecule usage
        /// </summary>

        [ProvideProperty("UIMode", typeof(Component))]   // Custom component property that appears in Designer (via IExtenderProvider)
        public class ModeUIExtender : Component, ISupportInitialize, IExtenderProvider // Behaves like a ToolTip, in that its presence in a form allows us to tag components with ModeUI info in the designer
        {
            private Dictionary<IComponent, MODE_UI_HANDLING_TYPE> _handledComponents = new Dictionary<IComponent, MODE_UI_HANDLING_TYPE>();
            private Dictionary<IComponent, string> _originalToolStripText = new Dictionary<IComponent, string>();
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

            public Dictionary<IComponent, string> GetOriginalToolStripText()
            {
                return _originalToolStripText;
            }

            public void AddHandledComponent(IComponent component, MODE_UI_HANDLING_TYPE type)
            {
                _handledComponents[component] = type;
            }

            public void BeginInit() // Required by ISupportInitialize
            {
            }

            public void EndInit() // Required by ISupportInitialize
            {
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
                get { return _modeUI ?? Program.ModeUI; }
                set { _modeUI = value; } // Useful for forms that are truly proteomic or truly nonproteomic: by setting this you override Program.ModeUI
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
                dropDown.Items.Add(NewButton(Resources.UIModeProteomic,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Proteomics_interface,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Only_show_menus_and_controls_appropriate_to_proteomics_analysis,
                    handler, SrmDocument.DOCUMENT_TYPE.proteomic));
                dropDown.Items.Add(NewButton(Resources.UIModeSmallMolecules,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Small_Molecules_interface,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Only_show_menus_and_controls_appropriate_to_small_molecule_analysis,
                    handler, SrmDocument.DOCUMENT_TYPE.small_molecules));
                dropDown.Items.Add(NewButton(Resources.UIModeMixed,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Mixed_interface,
                    Resources.ModeUIAwareFormHelper_SetModeUIToolStripButtons_Show_all_menus_and_controls,
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
                    PeptideToMoleculeTextMapper.TranslateForm(form, ModeUI, _modeUIExtender);
                }
            }

            public string Translate(string txt)
            {
                return IgnoreModeUI ? txt : PeptideToMoleculeTextMapper.Translate(txt, ModeUI);
            }

            // Like string.Format, but does UIMode translation on the format string (but not the args)
            public string Format(string format, params object[] args)
            {
                return IgnoreModeUI ? string.Format(format, args) : PeptideToMoleculeTextMapper.Format(format, ModeUI, args);
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
                            dictOriginalText[menuItem] = menuItem.Text;
                        }
                        else
                        {
                            // Restore original text so translator has a clean start
                            menuItem.Text = originalText;
                        }
                    }
                }

                // Update text, swapping "peptide" for "molecule" etc, except as specifically prohibited
                PeptideToMoleculeTextMapper.TranslateMenuItems(items, ModeUI, _modeUIExtender);


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

            public bool MenuItemHasOriginalText(string name)
            {
                foreach (var item in _modeUIExtender.GetOriginalToolStripText().Keys)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        if (Equals(name, menuItem.Text))
                        {
                            return Equals(name, _modeUIExtender.GetOriginalToolStripText()[menuItem]);
                        }
                    }
                }
                return false;
            }


            public static void SetComponentEnabledStateForModeUI(Component component, bool isDesired)
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
                            parent.TabPages.Remove(tabPage);
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
                    message = Resources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Cannot_switch_to_proteomics_interface_because_the_current_document_contains_small_molecules_data_;
                }
                else if (new_uimode == SrmDocument.DOCUMENT_TYPE.small_molecules && hasPeptides)
                {
                    message = Resources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Cannot_switch_to_molecule_interface_because_the_current_document_contains_proteomics_data_;
                }

                if (message != null)
                {
                    message = TextUtil.LineSeparate(message,@" ", Resources.ModeUIAwareFormHelper_EnableNeededButtonsForModeUI_Would_you_like_to_create_a_new_document_);
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
    }
}