/*
 * Original author: Shannon Joyner <saj9191 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Grpc.Core;
using pwiz.Common.Controls;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Config;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Server = pwiz.Skyline.Util.Server;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolOptionsUI : FormEx
    {
        private readonly SettingsListBoxDriver<Server> _driverServers;
        private readonly SettingsListBoxDriver<RemoteAccount> _driverRemoteAccounts;
        private readonly SettingsListComboDriver<ColorScheme> _driverColorSchemes;

        // For Prosit pinging
        private readonly SrmSettings _settingsNoMod;
        private readonly PrositIntensityModel.PeptidePrecursorNCE _pingInput;
        public ToolOptionsUI(SrmSettings settings)
        {
            InitializeComponent();
            checkBoxShowWizard.Checked = Settings.Default.ShowStartupForm;
            powerOfTenCheckBox.Checked = Settings.Default.UsePowerOfTen;
            Icon = Resources.Skyline;

            _driverServers = new SettingsListBoxDriver<Server>(listboxServers, Settings.Default.ServerList);
            _driverServers.LoadList();
            _driverRemoteAccounts = new SettingsListBoxDriver<RemoteAccount>(listBoxRemoteAccounts, Settings.Default.RemoteAccountList);
            _driverRemoteAccounts.LoadList();
            _driverColorSchemes = new SettingsListComboDriver<ColorScheme>(comboColorScheme, Settings.Default.ColorSchemes, true);
            _driverColorSchemes.LoadList(Settings.Default.CurrentColorScheme);

            var pingPep = new Peptide(@"PING");
            var peptide = new PeptideDocNode(pingPep);
            var precursor = new TransitionGroupDocNode(new TransitionGroup(pingPep, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                new TransitionDocNode[0]);
            _pingInput = new PrositIntensityModel.PeptidePrecursorNCE(peptide, precursor, IsotopeLabelType.light, 32);
            _settingsNoMod = settings.ChangePeptideModifications(
                pm => new PeptideModifications(new StaticMod[0], new TypedModifications[0]));

            // Hide ability to turn off live reports
            //tabControl.TabPages.Remove(tabMisc);

            // Populate the languages list with the languages that Skyline has been localized to
            string defaultDisplayName = string.Format(Resources.ToolOptionsUI_ToolOptionsUI_Default___0__,
                CultureUtil.GetDisplayLanguage(CultureInfo.InstalledUICulture).DisplayName);
            listBoxLanguages.Items.Add(new DisplayLanguageItem(string.Empty, defaultDisplayName));
            foreach (var culture in CultureUtil.AvailableDisplayLanguages())
            {
                listBoxLanguages.Items.Add(new DisplayLanguageItem(culture.Name, culture.DisplayName));
            }
            for (int i = 0; i < listBoxLanguages.Items.Count; i++)
            {
                var displayLanguageItem = (DisplayLanguageItem) listBoxLanguages.Items[i];
                if (Equals(displayLanguageItem.Key, Settings.Default.DisplayLanguage))
                {
                    listBoxLanguages.SelectedIndex = i;
                }
            }
            comboCompactFormatOption.Items.AddRange(CompactFormatOption.ALL_VALUES.ToArray());
            comboCompactFormatOption.SelectedItem = CompactFormatOption.FromSettings();

            var iModels = PrositIntensityModel.Models.ToArray();
            var rtModels = PrositRetentionTimeModel.Models.ToArray();

            tbxPrositServer.Text = PrositConfig.GetPrositConfig().Server;
            intensityModelCombo.Items.AddRange(iModels);
            iRTModelCombo.Items.AddRange(rtModels);
            
            prositServerStatusLabel.Text = string.Empty;
            if (iModels.Contains(Settings.Default.PrositIntensityModel))
                intensityModelCombo.SelectedItem = Settings.Default.PrositIntensityModel;
            if (rtModels.Contains(Settings.Default.PrositRetentionTimeModel))
                iRTModelCombo.SelectedItem = Settings.Default.PrositRetentionTimeModel;

            ceCombo.Items.AddRange(
                Enumerable.Range(PrositConstants.MIN_NCE, PrositConstants.MAX_NCE - PrositConstants.MIN_NCE + 1).Select(c => (object) c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.PrositNCE;
        }

        private class PrositPingRequest : PrositHelpers.PrositRequest
        {
            public PrositPingRequest(string ms2Model, string rtModel, SrmSettings settings,
                PeptideDocNode peptide, TransitionGroupDocNode precursor, int nce, Action updateCallback) : base(null,
                null, null, settings, peptide, precursor, nce, updateCallback)
            {
                Client = PrositPredictionClient.CreateClient(PrositConfig.GetPrositConfig());
                IntensityModel = PrositIntensityModel.GetInstance(ms2Model);
                RTModel = PrositRetentionTimeModel.GetInstance(rtModel);

                if (IntensityModel == null && RTModel == null)
                    throw new PrositNotConfiguredException();
            }

            public override PrositHelpers.PrositRequest Predict()
            {
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        var ms = IntensityModel.PredictSingle(Client, Settings,
                            new PrositIntensityModel.PeptidePrecursorNCE(Peptide, Precursor, Precursor.LabelType, NCE), _tokenSource.Token);

                        var iRTMap = RTModel.PredictSingle(Client,
                            Settings,
                            Peptide, _tokenSource.Token);

                        var spectrumInfo = new SpectrumInfoProsit(ms, Precursor, NCE);
                        var irt = iRTMap[Peptide];
                        Spectrum = new SpectrumDisplayInfo(
                            spectrumInfo, Precursor, irt);
                    }
                    catch (Exception ex)
                    {
                        Exception = ex;

                        // Ignore, UpdateUI is already working on a new request,
                        // so don't even update UI
                        if (ex.InnerException is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Cancelled)
                            return;
                    }
                    
                    // Bad timing could cause the ping to finish right when we cancel as the form closes
                    // causing a UI update to be called after the form was destroyed
                    if (!_tokenSource.IsCancellationRequested)
                        _updateCallback.Invoke();
                });

                return this;
            }
        }

        private PrositPingRequest _pingRequest;


        public enum ServerStatus
        {
            UNAVAILABLE, QUERYING, AVAILABLE, SELECT_SERVER, SELECT_MODEL
        }

        public ServerStatus PrositServerStatus { get; private set; }

        private void SetServerStatus(ServerStatus status)
        {
            switch (status)
            {
                case ServerStatus.UNAVAILABLE:
                    prositServerStatusLabel.Text =
                        PrositResources.ToolOptionsUI_UpdateServerStatus_Server_unavailable;
                    prositServerStatusLabel.ForeColor = Color.Red;
                    break;
                case ServerStatus.QUERYING:
                    prositServerStatusLabel.Text =
                        PrositResources.ToolOptionsUI_UpdateServerStatus_Querying_server___;
                    prositServerStatusLabel.ForeColor = Color.Black;
                    break;
                case ServerStatus.AVAILABLE:
                    prositServerStatusLabel.Text = PrositResources.ToolOptionsUI_ToolOptionsUI_Server_online;
                    prositServerStatusLabel.ForeColor = Color.Green;
                    break;
                case ServerStatus.SELECT_SERVER:
                    prositServerStatusLabel.Text = PrositResources.ToolOptionsUI_SetServerStatus_Select_a_server;
                    prositServerStatusLabel.ForeColor = Color.Red;
                    break;
                case ServerStatus.SELECT_MODEL:
                    prositServerStatusLabel.Text = PrositResources.ToolOptionsUI_SetServerStatus_Select_both_models;
                    prositServerStatusLabel.ForeColor = Color.Red;
                    break;
            }

            PrositServerStatus = status;
        }

        private void UpdateServerStatus()
        {
            if (!IsHandleCreated)
                return;

            try
            {
                if (PrositIntensityModelCombo == null || PrositRetentionTimeModelCombo == null)
                {
                    _pingRequest?.Cancel();
                    SetServerStatus(ServerStatus.SELECT_MODEL);
                    return;
                }

                var pr = new PrositPingRequest(PrositIntensityModelCombo,
                    PrositRetentionTimeModelCombo,
                    _settingsNoMod, _pingInput.NodePep, _pingInput.NodeGroup, _pingInput.NCE.Value,
                    () => { Invoke((Action) UpdateServerStatus); });
                if (_pingRequest == null || !_pingRequest.Equals(pr))
                {
                    _pingRequest?.Cancel();
                    _pingRequest = (PrositPingRequest) pr.Predict();
                    prositServerStatusLabel.Text = PrositResources.ToolOptionsUI_UpdateServerStatus_Querying_server___;
                    prositServerStatusLabel.ForeColor = Color.Black;

                }
                else if (_pingRequest.Spectrum == null)
                {
                    SetServerStatus(_pingRequest.Exception == null ? ServerStatus.QUERYING : ServerStatus.UNAVAILABLE);
                }
                else
                {
                    SetServerStatus(ServerStatus.AVAILABLE);
                }
            }
            catch
            {
                SetServerStatus(ServerStatus.UNAVAILABLE);
            }
        }

        private void btnEditServers_Click(object sender, EventArgs e)
        {
            EditServers();
        }

        public void EditServers()
        {
            _driverServers.EditList();
        }

        private void btnEditRemoteAccountList_Click(object sender, EventArgs e)
        {
            EditRemoteAccounts();
        }

        public void EditRemoteAccounts()
        {
            _driverRemoteAccounts.EditList();
        }

        protected override void OnClosed(EventArgs e)
        {
            _pingRequest?.Cancel();

            if (DialogResult == DialogResult.OK)
            {
                var displayLanguageItem = listBoxLanguages.SelectedItem as DisplayLanguageItem;
                if (null != displayLanguageItem)
                {
                    Settings.Default.ShowStartupForm = checkBoxShowWizard.Checked;
                    Settings.Default.DisplayLanguage = displayLanguageItem.Key;
                    Settings.Default.UsePowerOfTen = powerOfTenCheckBox.Checked;
                    Program.MainWindow?.UpdateGraphPanes();
                }
                CompactFormatOption compactFormatOption = comboCompactFormatOption.SelectedItem as CompactFormatOption;
                if (null != compactFormatOption)
                {
                    Settings.Default.CompactFormatOption = compactFormatOption.Name;
                }
                Settings.Default.CurrentColorScheme = (string) comboColorScheme.SelectedItem;

                Settings.Default.PrositIntensityModel = (string) intensityModelCombo.SelectedItem;
                Settings.Default.PrositRetentionTimeModel = (string)iRTModelCombo.SelectedItem;
                Settings.Default.PrositNCE = (int) ceCombo.SelectedItem;
            }
            base.OnClosed(e);
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private class DisplayLanguageItem
        {
            public DisplayLanguageItem(string key, string displayName)
            {
                Key = key;
                DisplayName = displayName;
            }
            public string DisplayName { get; private set; }
            public string Key { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
        }

        // ReSharper disable InconsistentNaming
        public enum TABS { Panorama, Remote, Prosit, Language, Miscellaneous, Display }
        // ReSharper restore InconsistentNaming

        public class PanoramaTab : IFormView { }
        public class RemoteTab : IFormView { }
        public class LanguageTab : IFormView { }
        public class MiscellaneousTab : IFormView { }
        public class DisplayTab : IFormView { }
        public class PrositTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new PanoramaTab(), new RemoteTab(), new LanguageTab(), new MiscellaneousTab(), new DisplayTab(), new PrositTab()
        };

        public void NavigateToTab(TABS tab)
        {
            SelectedTab = tab;
        }

        #region Functional testing support

        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControl.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        public TABS SelectedTab
        {
            get { return (TABS)tabControl.SelectedIndex; }
            set { tabControl.SelectedIndex = (int)value; }
        }

        public bool PowerOfTenCheckBox
        {
            get { return powerOfTenCheckBox.Checked; }
            set { powerOfTenCheckBox.Checked = value; }
        }

        public string PrositIntensityModelCombo
        {
            get { return (string)intensityModelCombo.SelectedItem; }
            set { intensityModelCombo.SelectedItem = value; }
        }

        public string PrositRetentionTimeModelCombo
        {
            get { return (string) iRTModelCombo.SelectedItem; }
            set { iRTModelCombo.SelectedItem = value; }
        }

        public int CECombo
        {
            get { return (int) ceCombo.SelectedItem; }
            set { ceCombo.SelectedItem = value; }
        }

        #endregion

        private void comboColorScheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            ColorScheme newColorScheme = _driverColorSchemes.SelectedItem;
            if (newColorScheme != null)
            {
                Settings.Default.CurrentColorScheme = newColorScheme.Name;
                Program.MainWindow?.ChangeColorScheme();
            }
            _driverColorSchemes.SelectedIndexChangedEvent(sender, e);
        }

        public ComboBox getColorCB()
        {
            return comboColorScheme;
        }

        public SettingsListComboDriver<ColorScheme> getColorDrive()
        {
            return _driverColorSchemes;
        }

        private void btnResetSettings_Click(object sender, EventArgs e)
        {
            ResetAllSettings();
        }

        public void ResetAllSettings()
        {
            if (MultiButtonMsgDlg.Show(this,
                    string.Format(
                        Resources
                            .ToolOptionsUI_btnResetSettings_Click_Are_you_sure_you_want_to_clear_all_saved_settings__This_will_immediately_return__0__to_its_original_configuration_and_cannot_be_undone_,
                        Program.Name), MultiButtonMsgDlg.BUTTON_OK) == DialogResult.OK)
            {
                Settings.Default.Reset();
            }
        }

        private void prositDescrLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(@"https://www.proteomicsdb.org/prosit/");
        }

        private void intensityModelCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateServerStatus();
        }

        private void iRTModelCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateServerStatus();
        }

        private void ToolOptionsUI_Shown(object sender, EventArgs e)
        {
            UpdateServerStatus();
        }
    }
}
