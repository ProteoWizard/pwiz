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
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

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
                mixed,       // We'll only show this component in mixed mode, and will never attempt the "peptide"->"molecule" translation
                invariant    // We'll never hide nor attempt the "peptide"->"molecule" translation on this component
            };

            public bool CanExtend(object extendee)
            {
                return !(extendee is ModeUIExtender);
            }

            #region UIMode
            [DefaultValue(MODE_UI_HANDLING_TYPE.auto),]
            [Description("Determines display and/or 'peptide'->'molecule' translation of control under different UI modes:\n" +
                "auto      // We'll attempt the 'peptide'->'molecule' translation on this component in small molecule or mixed UI modes\n" +
                "proteomic // We'll hide this component in small molecule UI mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "small_mol // We'll hide this component in proteomics UI mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "mixed     // We'll only show this component in mixed mode, and will never attempt the 'peptide'->'molecule' translation\n" +
                "invariant // We'll never hide nor attempt the 'peptide'->'molecule' translation on this component")]
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

        public class ModeUIAwareFormHelper // Assists in adapting the UI from its traditional proteomic language to small molecule language
        {
            public static ModeUIAwareFormHelper DEFAULT = new ModeUIAwareFormHelper(null);

            private SrmDocument.DOCUMENT_TYPE? _modeUI;
            private ToolTip _modeUIExplainerToolTip;
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
            private ToolStripButton ProteomicUIToolBarButton { get; set; }
            private ToolStripButton SmallMoleculeUIToolBarButton { get; set; }

            #region Testing Support
            public void ToggleProteomicUIToolBarButton()
            {
                if (ProteomicUIToolBarButton.Enabled)
                {
                    ProteomicUIToolBarButton.Checked = !ProteomicUIToolBarButton.Checked;
                    modeUIButtonClickProteomic(null, null);
                }
            }
            public void ToggleSmallMoleculeUIToolBarButton()
            {
                if (SmallMoleculeUIToolBarButton.Enabled)
                {
                    SmallMoleculeUIToolBarButton.Checked = !SmallMoleculeUIToolBarButton.Checked;
                    modeUIButtonClickSmallMol(null, null);
                }
            }
            public SrmDocument.DOCUMENT_TYPE GetUIToolBarButtonsCheckedState()
            {
                if (ProteomicUIToolBarButton.Checked && SmallMoleculeUIToolBarButton.Checked)
                    return SrmDocument.DOCUMENT_TYPE.mixed;
                if (ProteomicUIToolBarButton.Checked)
                    return SrmDocument.DOCUMENT_TYPE.proteomic;
                if (SmallMoleculeUIToolBarButton.Checked)
                    return SrmDocument.DOCUMENT_TYPE.small_molecules;
                Assume.Fail(@"expected at least one ui mode selector button checked");
                return SrmDocument.DOCUMENT_TYPE.none; // Never gets here
            }
            public SrmDocument.DOCUMENT_TYPE GetUIToolBarButtonsEnabledState()
            {
                if (ProteomicUIToolBarButton.Enabled && SmallMoleculeUIToolBarButton.Enabled)
                    return SrmDocument.DOCUMENT_TYPE.mixed;
                if (ProteomicUIToolBarButton.Enabled)
                    return SrmDocument.DOCUMENT_TYPE.proteomic;
                if (SmallMoleculeUIToolBarButton.Enabled)
                    return SrmDocument.DOCUMENT_TYPE.small_molecules;
                return SrmDocument.DOCUMENT_TYPE.none;
            }

            public bool HasModeUIExplainerToolTip { get { return _modeUIExplainerToolTip != null && _modeUIExplainerToolTip.Active;  } }

            #endregion

            public void SetModeUIToolStripButtons(ToolStripButton proteomicUIToolBarButton, ToolStripButton smallMoleculeUIToolBarButton)
            {
                ProteomicUIToolBarButton = proteomicUIToolBarButton;
                SmallMoleculeUIToolBarButton = smallMoleculeUIToolBarButton;
            }

            // Potentially replace "peptide" with "molecule" etc in all controls on open, or possibly disable non-proteomic components etc
            public void OnLoad(Form form)
            {
                if (!IgnoreModeUI)
                {
                    PeptideToMoleculeTextMapper.TranslateForm(form, ModeUI, _modeUIExtender);
                }

                //
                // If user has never set UI mode before, draw attention to the presumably unfamiliar buttons
                //
                if (ProteomicUIToolBarButton != null && string.IsNullOrEmpty(Settings.Default.UIMode))
                {
                    // No UImode set - presumably user's first time here
                    EnableNeededButtonsForModeUI(ModeUI); // Go to default ui mode

                    // Create a tooltip explaining these new buttons
                    _modeUIExplainerToolTip = new ToolTip();
                    _modeUIExplainerToolTip.ToolTipTitle = Resources.ModeUIAwareFormHelper_OnLoad_New__Use_these_buttons_to_configure_Skyline_s_user_interface_specifically_for_proteomics_or_small_molecule_use_;
                    var toolStrip = ProteomicUIToolBarButton.Owner;
                    var container = toolStrip.Parent;
                    toolStrip.ShowItemToolTips = true;
                    _modeUIExplainerToolTip.SetToolTip(toolStrip, _modeUIExplainerToolTip.ToolTipTitle);
                    _modeUIExplainerToolTip.IsBalloon = true;
                    _modeUIExplainerToolTip.Active = true;
                    _modeUIExplainerToolTip.UseFading = true;
                    _modeUIExplainerToolTip.UseAnimation = true;
                    _modeUIExplainerToolTip.ShowAlways = true;
                    _modeUIExplainerToolTip.AutoPopDelay = Int32.MaxValue; // Show it for a long time
                    _modeUIExplainerToolTip.InitialDelay = 1;
                    _modeUIExplainerToolTip.ReshowDelay = Int32.MaxValue; // Don't show it again
                    var where = new Point(toolStrip.Width, -toolStrip.Height);
                    _modeUIExplainerToolTip.Show(_modeUIExplainerToolTip.ToolTipTitle, toolStrip, where);
                    // Position cursor on the new control
                    var target = new Point(toolStrip.Right - toolStrip.Height, toolStrip.Top + toolStrip.Height / 2);
                    Point screen_coords = container.PointToScreen(target);
                    Cursor.Position = screen_coords;

                }
            }

            public string Translate(string txt)
            {
                return IgnoreModeUI ? txt : PeptideToMoleculeTextMapper.Translate(txt, ModeUI);
            }

            internal void AdjustMenusForModeUI(ToolStripItemCollection items)
            {
                var dictOriginalText = _modeUIExtender.GetOriginalToolStripText();

                foreach (var item in items)
                {
                    var menuItem = item as ToolStripMenuItem;
                    if (menuItem != null)
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
                    var menuItem = item as ToolStripMenuItem;
                    if (menuItem != null && menuItem.DropDownItems.Count > 0)
                    {
                        AdjustMenusForModeUI(menuItem.DropDownItems);
                    }
                }

            }


            public static void SetComponentEnabledStateForModeUI(Component component, bool isDesired)
            {
                ToolStripMenuItem item = component as ToolStripMenuItem;
                if (item != null)
                {
                    item.Visible = isDesired;
                    return;
                }

                var tabPage = component as TabPage;
                if (tabPage != null)
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

                var ctrl = component as Control;
                if (ctrl != null)
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

            /// <summary>
            /// Handler for the buttons that allow user to switch between proteomic, small mol, or mixed UI display.
            /// Between the two buttons there are three states - we enforce that at least one is always checked.
            /// </summary>
            public void modeUIButtonClickProteomic(object sender, EventArgs e)
            {
                HandleModeUIButtonClick(SrmDocument.DOCUMENT_TYPE.proteomic);
            }

            /// <summary>
            /// Handler for the buttons that allow user to switch between proteomic, small mol, or mixed UI display.
            /// Between the two buttons there are three states - we enforce that at least one is always checked.
            /// </summary>
            public void modeUIButtonClickSmallMol(object sender, EventArgs e)
            {
                HandleModeUIButtonClick(SrmDocument.DOCUMENT_TYPE.small_molecules);
            }

            private void HandleModeUIButtonClick(SrmDocument.DOCUMENT_TYPE clickedWhat)
            {
                var newModeUI = EnforceValidButtonStateOnClick(clickedWhat == SrmDocument.DOCUMENT_TYPE.proteomic);
                EnableNeededButtonsForModeUI(newModeUI);
            }

            public SrmDocument.DOCUMENT_TYPE EnforceValidButtonStateOnClick(bool clickedProteomic)
            {
                if (!ProteomicUIToolBarButton.Checked && !SmallMoleculeUIToolBarButton.Checked) // No current valid button state
                {
                    if (clickedProteomic)  // User clicked proteomic turning it off, so turn on small mol
                    {
                        SmallMoleculeUIToolBarButton.Checked = true;
                    }
                    else // User clicked on small mol turning it off, so turn on proteomic
                    {
                        ProteomicUIToolBarButton.Checked = true;
                    }
                }

                if (ProteomicUIToolBarButton.Checked)
                {
                    return SmallMoleculeUIToolBarButton.Checked
                        ? SrmDocument.DOCUMENT_TYPE.mixed
                        : SrmDocument.DOCUMENT_TYPE.proteomic;
                }

                if (_modeUIExplainerToolTip != null)
                {
                    // If we're here, user has clicked the buttons and no longer needs that balloon tooltip
                    _modeUIExplainerToolTip.Active = false;
                }

                return SrmDocument.DOCUMENT_TYPE.small_molecules;
            }

            public void EnableNeededButtonsForModeUI(SrmDocument.DOCUMENT_TYPE modeUI)
            {
                var current_ui_mode = ModeUI; // Begin with current selection - for an empty doc this is from Settings
                var doc = Program.ActiveDocument;
                var hasPeptides = doc != null && doc.Peptides.Any();
                var requireProteomic = hasPeptides; // If doc has any peptides, require the proteomic button 
                var hasSmallMolecules = doc != null && doc.CustomMolecules.Any();
                var requireSmallMolecule = hasSmallMolecules; // If doc has any smallmol, require the smallmol button

                ProteomicUIToolBarButton.Enabled = !hasPeptides; // Make button inoperable if document contains peptides
                SmallMoleculeUIToolBarButton.Enabled = !hasSmallMolecules; // Make button inoperable if document contains small molecules


                if (requireProteomic)
                {
                    ProteomicUIToolBarButton.Checked = true;
                }
                if (requireSmallMolecule)
                {
                    SmallMoleculeUIToolBarButton.Checked = true;
                }

                if (modeUI == SrmDocument.DOCUMENT_TYPE.mixed) 
                {
                    ProteomicUIToolBarButton.Checked = true;
                    SmallMoleculeUIToolBarButton.Checked = true;
                }
                else if (modeUI == SrmDocument.DOCUMENT_TYPE.proteomic) 
                {
                    ProteomicUIToolBarButton.Checked = true;
                }
                else  
                {
                    SmallMoleculeUIToolBarButton.Checked = true;
                }

                // Set tooltips to explain button checked/enabled states
                ProteomicUIToolBarButton.ToolTipText = hasPeptides ?
                    Resources.SkylineWindow_EnableNeededModeUIButtons_Proteomics_controls_cannot_be_hidden_when_document_contains_proteomic_targets :
                    ProteomicUIToolBarButton.Checked ?
                        Resources.SkylineWindow_EnableNeededModeUIButtons_Click_to_hide_proteomics_specific_controls_and_menu_items :
                        Resources.SkylineWindow_EnableNeededModeUIButtons_Click_to_show_proteomics_specific_controls_and_menu_items;
                SmallMoleculeUIToolBarButton.ToolTipText = hasSmallMolecules ?
                    Resources.SkylineWindow_EnableNeededModeUIButtons_Small_molecule_controls_cannot_be_hidden_when_document_contains_non_proteomic_targets :
                    SmallMoleculeUIToolBarButton.Checked ?
                        Resources.SkylineWindow_EnableNeededModeUIButtons_Click_to_hide_non_proteomics_controls_and_menu_items :
                        Resources.SkylineWindow_EnableNeededModeUIButtons_Click_to_show_non_proteomics_controls_and_menu_items;

                SrmDocument.DOCUMENT_TYPE new_uimode;
                if (!ProteomicUIToolBarButton.Checked)
                {
                    new_uimode = SrmDocument.DOCUMENT_TYPE.small_molecules;
                }
                else if (!SmallMoleculeUIToolBarButton.Checked)
                {
                    new_uimode = SrmDocument.DOCUMENT_TYPE.proteomic;
                }
                else
                {
                    new_uimode = SrmDocument.DOCUMENT_TYPE.mixed;
                }

                if (current_ui_mode != new_uimode)
                {
                    ModeUI = new_uimode;
                }

                Settings.Default.UIMode = ModeUI.ToString();

            }

            internal void SetButtonsCheckedForModeUI(SrmDocument.DOCUMENT_TYPE modeUI)
            {
                switch (modeUI)
                {
                    case SrmDocument.DOCUMENT_TYPE.proteomic:
                        ProteomicUIToolBarButton.Checked = true;
                        SmallMoleculeUIToolBarButton.Checked = false;
                        break;
                    case SrmDocument.DOCUMENT_TYPE.small_molecules:
                        ProteomicUIToolBarButton.Checked = false;
                        SmallMoleculeUIToolBarButton.Checked = true;
                        break;
                    case SrmDocument.DOCUMENT_TYPE.mixed:
                        ProteomicUIToolBarButton.Checked = true;
                        SmallMoleculeUIToolBarButton.Checked = true;
                        break;
                }
            }
        }

    }

    public static partial class Helpers
    {
        public interface IModeUIAwareForm
        {
            /// <summary>
            /// When appropriate per current UI mode, replace form contents such that "peptide" becomes "molecule" etc
            /// </summary>
            ModeUIAwareFormHelper GetModeUIHelper();
        }
    }
}