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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.EditUI
{
    public partial class PermuteIsotopeModificationsDlg : FormEx
    {
        private readonly SettingsListComboDriver<StaticMod> _driverIsotopeModification;
        public PermuteIsotopeModificationsDlg(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            _driverIsotopeModification = new SettingsListComboDriver<StaticMod>(comboIsotopeModification, Settings.Default.HeavyModList);
            _driverIsotopeModification.LoadList(null);
        }

        public SkylineWindow SkylineWindow { get; private set; }

        public bool SimplePermutation
        {
            get
            {
                return radioButtonSimplePermutation.Checked;
            }
            set
            {
                if (value)
                {
                    radioButtonSimplePermutation.Checked = true;
                }
                else
                {
                    radioButtonComplexPermutation.Checked = true;
                }
            }
        }

        public StaticMod IsotopeModification
        {
            get
            {
                return _driverIsotopeModification.SelectedItem;
            }
        }

        public void AddIsotopeModification()
        {
            _driverIsotopeModification.AddItem();
        }

        public AuditLogEntry GetAuditLogEntry(SrmDocumentPair docPair, StaticMod isotopeModification, bool simple)
        {
            return AuditLogEntry.CreateSimpleEntry(
                simple ? MessageType.permuted_isotope_label_simple : MessageType.permuted_isotope_label_complete, 
                docPair.NewDocumentType, isotopeModification.Name);
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);
            var isotopeModification = _driverIsotopeModification.SelectedItem;
            if (isotopeModification == null)
            {
                helper.ShowTextBoxError(comboIsotopeModification, Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty);
                return;
            }
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDocument = SkylineWindow.Document;
                var deferSettingsChangeDoc = originalDocument.BeginDeferSettingsChanges();
                var globalStaticMods = Settings.Default.StaticModList.ToList();
                var globalIsotopeMods = Settings.Default.HeavyModList.ToList();
                var newDocument = deferSettingsChangeDoc;
                var simplePermutation = SimplePermutation;
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.Text = EditUIResources.PermuteIsotopeModificationsDlg_OkDialog_Permuting_Isotope_Modifications;
                    longWaitDlg.PerformWork(this, 1000, progressMonitor =>
                    {
                        var isotopePermuter = new IsotopeModificationPermuter(isotopeModification,
                            simplePermutation, IsotopeLabelType.heavy, globalStaticMods, globalIsotopeMods);
                        newDocument = isotopePermuter.PermuteIsotopeModifications(progressMonitor, newDocument);
                        if (!ReferenceEquals(newDocument, deferSettingsChangeDoc))
                        {
                            var settingsChangeMonitor = new SrmSettingsChangeMonitor(progressMonitor, EditUIResources.PermuteIsotopeModificationsDlg_OkDialog_Updating_settings);
                            newDocument = newDocument.EndDeferSettingsChanges(originalDocument, settingsChangeMonitor);
                        }
                    });
                    if (longWaitDlg.IsCanceled)
                    {
                        return;
                    }

                }
                if (!ReferenceEquals(newDocument, deferSettingsChangeDoc))
                {
                    SkylineWindow.ModifyDocument(EditUIResources.PermuteIsotopeModificationsDlg_OkDialog_Permute_isotope_modifications, doc =>
                    {
                        // This will be true because we have acquired the lock on SkylineWindow.DocumentChangeLock()
                        Assume.IsTrue(ReferenceEquals(doc, originalDocument));
                        return newDocument;
                    }, docPair=>GetAuditLogEntry(docPair, isotopeModification, simplePermutation));
                }
                DialogResult = DialogResult.OK;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void comboIsotopeModification_SelectedIndexChanged(object sender, EventArgs e)
        {
            _driverIsotopeModification.SelectedIndexChangedEvent(sender, e);
        }
    }
}
