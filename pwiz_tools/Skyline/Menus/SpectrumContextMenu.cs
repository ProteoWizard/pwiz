/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Menus
{
    public partial class SpectrumContextMenu : ContextMenuControl
    {
        public SpectrumContextMenu(SkylineWindow skylineWindow) : base(skylineWindow)
        {
            InitializeComponent();
        }

        public void BuildSpectrumMenu(bool isProteomic, ZedGraphControl zedGraphControl, ContextMenuStrip menuStrip)
        {
            // Insert skyline specific menus
            var set = Settings.Default;
            var control = FormUtil.FindParentOfType<IMzScalePlot>(menuStrip.SourceControl);
            int iInsert = 0;
            if (control?.IsAnnotated ?? false)
            {
                if (isProteomic)
                {
                    menuStrip.Items.Insert(iInsert++, ionTypesContextMenuItem);
                    specialionsContextMenuItem.Checked = set.ShowSpecialIons;
                    menuStrip.Items.Insert(iInsert++, specialionsContextMenuItem);
                }
                else
                {
                    fragmentionsContextMenuItem.Checked = set.ShowFragmentIons;
                    menuStrip.Items.Insert(iInsert++, fragmentionsContextMenuItem);
                }

                precursorIonContextMenuItem.Checked = set.ShowPrecursorIon;
                menuStrip.Items.Insert(iInsert++, precursorIonContextMenuItem);
                menuStrip.Items.Insert(iInsert++, chargesContextMenuItem);

                menuStrip.Items.Insert(iInsert++, toolStripSeparator11);

                ranksContextMenuItem.Checked = set.ShowRanks;
                menuStrip.Items.Insert(iInsert++, ranksContextMenuItem);

                ionMzValuesContextMenuItem.Checked = set.ShowIonMz;
                menuStrip.Items.Insert(iInsert++, ionMzValuesContextMenuItem);
                observedMzValuesContextMenuItem.Checked = set.ShowObservedMz;
                menuStrip.Items.Insert(iInsert++, observedMzValuesContextMenuItem);
                menuStrip.Items.Insert(iInsert++, massErrorToolStripMenuItem);
                massErrorToolStripMenuItem.Checked = set.ShowFullScanMassError;
                duplicatesContextMenuItem.Checked = set.ShowDuplicateIons;
                menuStrip.Items.Insert(iInsert++, duplicatesContextMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator13);
            }
            else
            {
                menuStrip.Items.Insert(iInsert++, massErrorToolStripMenuItem);
                massErrorToolStripMenuItem.Checked = set.ShowFullScanMassError;
            }
            lockYaxisContextMenuItem.Checked = set.LockYAxis;
            menuStrip.Items.Insert(iInsert++, lockYaxisContextMenuItem);
            menuStrip.Items.Insert(iInsert++, toolStripSeparator14);

            // Need to test small mol
            if (isProteomic && control?.ControlType == SpectrumControlType.LibraryMatch)
            {
                koinaLibMatchItem.Checked = Settings.Default.Koina;
                menuStrip.Items.Insert(iInsert++, koinaLibMatchItem);
                mirrorMenuItem.Checked = Settings.Default.LibMatchMirror;
                menuStrip.Items.Insert(iInsert++, mirrorMenuItem);
                menuStrip.Items.Insert(iInsert++, toolStripSeparator61);
            }

            if (control != null)
            {
                menuStrip.Items.Insert(iInsert++, spectrumGraphPropsContextMenuItem);
                if (control.ControlType == SpectrumControlType.LibraryMatch)
                {
                    showLibSpectrumPropertiesContextMenuItem.Checked = control.ShowPropertiesSheet;
                    menuStrip.Items.Insert(iInsert++, showLibSpectrumPropertiesContextMenuItem);
                }
                else if (control.ControlType == SpectrumControlType.FullScanViewer)
                {
                    showFullScanSpectrumPropertiesContextMenuItem.Checked = control.ShowPropertiesSheet;
                    menuStrip.Items.Insert(iInsert++, showFullScanSpectrumPropertiesContextMenuItem);
                }
            }


            if (control is { HasChromatogramData: true }) // Don't offer to show chromatograms when there are none
            {
                showLibraryChromatogramsSpectrumContextMenuItem.Checked = set.ShowLibraryChromatograms;
                menuStrip.Items.Insert(iInsert++, showLibraryChromatogramsSpectrumContextMenuItem);
            }
            /*
            if(ListMzScaleCopyables().Count() >=2)
            {
                menuStrip.Items.Insert(iInsert++, synchMzScaleToolStripMenuItem);
                synchMzScaleToolStripMenuItem.Checked = Settings.Default.SyncMZScale;
            }
            */
            //menuStrip.Items.Insert(iInsert, toolStripSeparator15);

            UpdateIonTypeMenu();
            UpdateChargesMenu();
        }

        public void UpdateChargesMenu()
        {
            if (chargesContextMenuItem.DropDownItems.Count > 0 && chargesContextMenuItem.DropDownItems[0] is MenuControl<ChargeSelectionPanel> chargeSelector)
            {
                chargeSelector.Update(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
            }
            else
            {
                chargesContextMenuItem.DropDownItems.Clear();
                var selectorControl = new MenuControl<ChargeSelectionPanel>(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
                chargesContextMenuItem.DropDownItems.Add(selectorControl);
                selectorControl.HostedControl.OnChargeChanged += SkylineWindow.IonChargeSelector_ionChargeChanged;
            }
        }

        public void UpdateIonTypeMenu()
        {
            if (ionTypesContextMenuItem.DropDownItems.Count > 0 &&
                ionTypesContextMenuItem.DropDownItems[0] is MenuControl<IonTypeSelectionPanel> ionSelector)
            {
                ionSelector.Update(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
            }
            else
            {
                ionTypesContextMenuItem.DropDownItems.Clear();
                var ionTypeSelector = new MenuControl<IonTypeSelectionPanel>(SkylineWindow.GraphSpectrumSettings, SkylineWindow.DocumentUI.Settings.PeptideSettings);
                ionTypesContextMenuItem.DropDownItems.Add(ionTypeSelector);
                ionTypeSelector.HostedControl.IonTypeChanged += SkylineWindow.IonTypeSelector_IonTypeChanges;
                ionTypeSelector.HostedControl.LossChanged += SkylineWindow.IonTypeSelector_LossChanged;
            }
        }

        #region Event Handlers

        private void chargesMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateChargesMenu();
        }

        private void ionTypeMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            UpdateIonTypeMenu();
        }

        private void fragmentsMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowFragmentIons(!SkylineWindow.GraphSpectrumSettings.ShowFragmentIons);
        }

        private void precursorIonMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowPrecursorIon(!SkylineWindow.GraphSpectrumSettings.ShowPrecursorIon);
        }

        private void specialionsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSpecialIons(!SkylineWindow.GraphSpectrumSettings.ShowSpecialIons);
        }

        private void ranksMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowRanks = !Settings.Default.ShowRanks;
            SkylineWindow.UpdateSpectrumGraph(false);
        }

        private void scoresContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowLibraryScores = !Settings.Default.ShowLibraryScores;
            SkylineWindow.UpdateSpectrumGraph(false);
        }

        private void ionMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowIonMz = !Settings.Default.ShowIonMz;
            SkylineWindow.UpdateSpectrumGraph(false);
        }

        private void massErrorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowFullScanMassError = !Settings.Default.ShowFullScanMassError;
            SkylineWindow.UpdateSpectrumGraph(false);
        }

        private void observedMzValuesContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ToggleObservedMzValues();
        }

        private void duplicatesContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowDuplicateIons = duplicatesContextMenuItem.Checked;
            SkylineWindow.UpdateSpectrumGraph(false);
        }

        private void lockYaxisContextMenuItem_Click(object sender, EventArgs e)
        {
            // Avoid updating the rest of the graph just to change the y-axis lock state
            Settings.Default.LockYAxis = lockYaxisContextMenuItem.Checked;
            SkylineWindow.GraphSpectrum?.LockYAxis(lockYaxisContextMenuItem.Checked);
            SkylineWindow.GraphFullScan?.LockYAxis(lockYaxisContextMenuItem.Checked);
        }

        private void koinaLibMatchItem_Click(object sender, EventArgs e)
        {
            koinaLibMatchItem.Checked = !koinaLibMatchItem.Checked;

            if (koinaLibMatchItem.Checked)
                KoinaUIHelpers.CheckKoinaSettings(SkylineWindow, SkylineWindow);

            SkylineWindow.GraphSpectrumSettings.Koina = koinaLibMatchItem.Checked;
        }

        private void mirrorMenuItem_Click(object sender, EventArgs e)
        {
            mirrorMenuItem.Checked = !mirrorMenuItem.Checked;
            SkylineWindow.GraphSpectrumSettings.Mirror = mirrorMenuItem.Checked;
        }

        private void spectrumGraphPropsContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.ShowSpectrumProperties();
        }

        private void showLibSpectrumPropertiesContextMenuItem_Click(object sender, EventArgs e)
        {
            var graphSpectrum = SkylineWindow.GraphSpectrum;
            if (graphSpectrum != null && graphSpectrum.Visible)
                graphSpectrum.ShowPropertiesSheet = !showLibSpectrumPropertiesContextMenuItem.Checked;
        }

        private void showFullScanSpectrumPropertiesContextMenuItem_Click(object sender, EventArgs e)
        {
            var graphFullScan = SkylineWindow.GraphFullScan;
            if (graphFullScan != null && graphFullScan.Visible)
                graphFullScan.ShowPropertiesSheet = !showFullScanSpectrumPropertiesContextMenuItem.Checked;
        }

        private void zoomSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            SkylineWindow.GraphSpectrum?.ZoomSpectrumToSettings();
        }

        private void showChromatogramsSpectrumContextMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.ShowLibraryChromatograms = !Settings.Default.ShowLibraryChromatograms;
            SkylineWindow.UpdateGraphPanes();
        }

        private void synchMzScaleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var source = (synchMzScaleToolStripMenuItem.Owner as ContextMenuStrip)?.SourceControl?.FindForm() as IMzScalePlot;
            SkylineWindow.SynchMzScaleToolStripMenuItemClick(synchMzScaleToolStripMenuItem.Checked, source);
        }

        #endregion
    }
}
