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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Grpc.Core;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model.Koina.Communication;
using pwiz.Skyline.Model.Koina.Config;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.AlphaPeptDeep;
using pwiz.Skyline.Model.Lib.Carafe;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Server = pwiz.Skyline.Util.Server;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolOptionsUI : FormEx, IMultipleViewProvider
    {
        private readonly SettingsListBoxDriver<Server> _driverServers;
        private readonly SettingsListBoxDriver<RemoteAccount> _driverRemoteAccounts;
        private readonly SettingsListComboDriver<ColorScheme> _driverColorSchemes;

        // For Koina pinging
        private readonly SrmSettings _settingsNoMod;
        private readonly KoinaIntensityModel.PeptidePrecursorNCE _pingInput;

        public ToolOptionsUI(SrmSettings settings)
        {
            InitializeComponent();
            EmbedAlphaPeptDeepUserSettings();
            EmbedCarafeDataSettings();
            EmbedCarafeModelSettings();
            EmbedCarafeLibrarySettings();
            checkBoxShowWizard.Checked = Settings.Default.ShowStartupForm;
            powerOfTenCheckBox.Checked = Settings.Default.UsePowerOfTen;
            Icon = Resources.Skyline;

            _driverServers = new SettingsListBoxDriver<Server>(listboxServers, Settings.Default.ServerList);
            _driverServers.LoadList();
            _driverRemoteAccounts =
                new SettingsListBoxDriver<RemoteAccount>(listBoxRemoteAccounts, Settings.Default.RemoteAccountList);
            _driverRemoteAccounts.LoadList();
            _driverColorSchemes =
                new SettingsListComboDriver<ColorScheme>(comboColorScheme, Settings.Default.ColorSchemes, true);
            _driverColorSchemes.LoadList(Settings.Default.CurrentColorScheme);

            var pingPep = new Peptide(@"PING");
            var peptide = new PeptideDocNode(pingPep);
            var precursor = new TransitionGroupDocNode(
                new TransitionGroup(pingPep, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                new TransitionDocNode[0]);
            _pingInput = new KoinaIntensityModel.PeptidePrecursorNCE(peptide, precursor, IsotopeLabelType.light, 32);
            _settingsNoMod = settings.ChangePeptideModifications(
                pm => new PeptideModifications(new StaticMod[0], new TypedModifications[0]));

            // Hide ability to turn off live reports
            //tabControl.TabPages.Remove(tabMisc);

            // Populate the languages list with the languages that Skyline has been localized to
            string defaultDisplayName = string.Format(ToolsUIResources.ToolOptionsUI_ToolOptionsUI_Default___0__,
                CultureUtil.GetDisplayLanguage(CultureInfo.InstalledUICulture).DisplayName);
            listBoxLanguages.Items.Add(new DisplayLanguageItem(string.Empty, defaultDisplayName));
            foreach (var culture in CultureUtil.AvailableDisplayLanguages())
            {
                listBoxLanguages.Items.Add(new DisplayLanguageItem(culture.Name, culture.DisplayName));
            }

            for (int i = 0; i < listBoxLanguages.Items.Count; i++)
            {
                var displayLanguageItem = (DisplayLanguageItem)listBoxLanguages.Items[i];
                if (Equals(displayLanguageItem.Key, Settings.Default.DisplayLanguage))
                {
                    listBoxLanguages.SelectedIndex = i;
                }
            }

            comboCompactFormatOption.Items.AddRange(CompactFormatOption.ALL_VALUES.ToArray());
            comboCompactFormatOption.SelectedItem = CompactFormatOption.FromSettings();

            var iModels = KoinaIntensityModel.Models.ToList();
            iModels.Insert(0, string.Empty);
            var rtModels = KoinaRetentionTimeModel.Models.ToList();
            rtModels.Insert(0, string.Empty);

            tbxKoinaServer.Text = KoinaConfig.GetKoinaConfig().Server;
            intensityModelCombo.Items.AddRange(iModels.ToArray());
            ComboHelper.AutoSizeDropDown(intensityModelCombo);
            iRTModelCombo.Items.AddRange(rtModels.ToArray());
            ComboHelper.AutoSizeDropDown(iRTModelCombo);

            koinaServerStatusLabel.Text = string.Empty;
            if (iModels.Contains(Settings.Default.KoinaIntensityModel))
                intensityModelCombo.SelectedItem = Settings.Default.KoinaIntensityModel;
            if (rtModels.Contains(Settings.Default.KoinaRetentionTimeModel))
                iRTModelCombo.SelectedItem = Settings.Default.KoinaRetentionTimeModel;

            ceCombo.Items.AddRange(
                Enumerable.Range(KoinaConstants.MIN_NCE, KoinaConstants.MAX_NCE - KoinaConstants.MIN_NCE + 1)
                    .Select(c => (object)c)
                    .ToArray());
            ceCombo.SelectedItem = Settings.Default.KoinaNCE;
            tbxSettingsFilePath.Text = System.Configuration.ConfigurationManager
                .OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
        }

        private class KoinaPingRequest : KoinaHelpers.KoinaRequest
        {
            private static KoinaConfig _koinaConfig;
            private static Channel _channel;

            static KoinaPingRequest()
            {
                _koinaConfig = KoinaConfig.GetKoinaConfig();
                _channel = _koinaConfig.CreateChannel();
            }

            public KoinaPingRequest(string ms2Model, string rtModel, SrmSettings settings,
                PeptideDocNode peptide, TransitionGroupDocNode precursor, int nce, Action updateCallback) : base(null,
                null, null, settings, peptide, precursor, null, nce, updateCallback)
            {
                Client = KoinaPredictionClient.CreateClient(_channel, _koinaConfig.Server);
                IntensityModel = KoinaIntensityModel.GetInstance(ms2Model);
                RTModel = KoinaRetentionTimeModel.GetInstance(rtModel);

                if (IntensityModel == null && RTModel == null)
                    throw new KoinaNotConfiguredException();
            }

            public override KoinaHelpers.KoinaRequest Predict()
            {
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        var labelType = Precursor.LabelType;
                        var ms = IntensityModel.PredictSingle(Client, Settings,
                            new KoinaIntensityModel.PeptidePrecursorNCE(Peptide, Precursor, labelType, NCE),
                            _tokenSource.Token);

                        var iRTMap = RTModel.PredictSingle(Client,
                            Settings,
                            Peptide, _tokenSource.Token);

                        var spectrumInfo = new SpectrumInfoKoina(ms, Precursor, labelType, NCE);
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

        private KoinaPingRequest _pingRequest;


        public enum ServerStatus
        {
            UNAVAILABLE,
            QUERYING,
            AVAILABLE,
            SELECT_SERVER,
            SELECT_MODEL
        }

        public ServerStatus KoinaServerStatus { get; private set; }

        private void SetServerStatus(ServerStatus status)
        {
            switch (status)
            {
                case ServerStatus.UNAVAILABLE:
                    koinaServerStatusLabel.Text =
                        KoinaResources.ToolOptionsUI_UpdateServerStatus_Server_unavailable;
                    koinaServerStatusLabel.ForeColor = Color.Red;
                    break;
                case ServerStatus.QUERYING:
                    koinaServerStatusLabel.Text =
                        KoinaResources.ToolOptionsUI_UpdateServerStatus_Querying_server___;
                    koinaServerStatusLabel.ForeColor = Color.Black;
                    break;
                case ServerStatus.AVAILABLE:
                    koinaServerStatusLabel.Text = KoinaResources.ToolOptionsUI_ToolOptionsUI_Server_online;
                    koinaServerStatusLabel.ForeColor = Color.Green;
                    break;
                case ServerStatus.SELECT_SERVER:
                    koinaServerStatusLabel.Text = KoinaResources.ToolOptionsUI_SetServerStatus_Select_a_server;
                    koinaServerStatusLabel.ForeColor = Color.Red;
                    break;
                case ServerStatus.SELECT_MODEL:
                    koinaServerStatusLabel.Text = KoinaResources.ToolOptionsUI_SetServerStatus_Select_both_models;
                    koinaServerStatusLabel.ForeColor = Color.Red;
                    break;
            }

            KoinaServerStatus = status;
        }

        private void UpdateServerStatus()
        {
            if (!IsHandleCreated)
                return;

            try
            {
                if (string.IsNullOrEmpty(KoinaIntensityModelCombo) ||
                    string.IsNullOrEmpty(KoinaRetentionTimeModelCombo))
                {
                    _pingRequest?.Cancel();
                    SetServerStatus(ServerStatus.SELECT_MODEL);
                    return;
                }

                var nodePep = new PeptideDocNode(new Peptide(_pingInput.Sequence), _pingInput.ExplicitMods);
                var nodeGroup = new TransitionGroupDocNode(new TransitionGroup(nodePep.Peptide,
                        Adduct.FromChargeProtonated(_pingInput.PrecursorCharge), _pingInput.LabelType),
                    Array.Empty<TransitionDocNode>());
                var pr = new KoinaPingRequest(KoinaIntensityModelCombo,
                    KoinaRetentionTimeModelCombo,
                    _settingsNoMod, nodePep, nodeGroup, _pingInput.NCE.Value,
                    () => { CommonActionUtil.SafeBeginInvoke(this, UpdateServerStatus); });
                if (_pingRequest == null || !_pingRequest.Equals(pr))
                {
                    _pingRequest?.Cancel();
                    _pingRequest = (KoinaPingRequest)pr.Predict();
                    koinaServerStatusLabel.Text = KoinaResources.ToolOptionsUI_UpdateServerStatus_Querying_server___;
                    koinaServerStatusLabel.ForeColor = Color.Black;

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

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel)
                _pingRequest?.Cancel();
        }

        protected override void OnClosed(EventArgs e)
        {
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

                Settings.Default.CurrentColorScheme = (string)comboColorScheme.SelectedItem;

                bool koinaSettingsValidBefore = KoinaHelpers.KoinaSettingsValid;
                Settings.Default.KoinaIntensityModel = (string)intensityModelCombo.SelectedItem;
                Settings.Default.KoinaRetentionTimeModel = (string)iRTModelCombo.SelectedItem;
                Settings.Default.KoinaNCE = (int)ceCombo.SelectedItem;
                if (koinaSettingsValidBefore != KoinaHelpers.KoinaSettingsValid)
                    Program.MainWindow?.UpdateGraphSpectrumEnabled();
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
        public enum TABS
        {
           // AlphaPeptDeep,
           // Carafe,
            Display,
            Koina,
            Language,
            Miscellaneous,
            Panorama,
            Remote
        }
        // ReSharper restore InconsistentNaming

        public class PanoramaTab : IFormView
        {
        }

        public class RemoteTab : IFormView
        {
        }

        public class AlphaPeptDeepTab : IFormView
        {
        }

        public class CarafeTab : IFormView
        {
        }

        public class LanguageTab : IFormView
        {
        }

        public class MiscellaneousTab : IFormView
        {
        }

        public class DisplayTab : IFormView
        {
        }

        public class KoinaTab : IFormView
        {
        }

        private static readonly IFormView[] TAB_PAGES = // N.B. order must agree with TABS enum above
        {
            new AlphaPeptDeepTab(), new CarafeTab(), new DisplayTab(), new KoinaTab(),
            new LanguageTab(), new MiscellaneousTab(), new PanoramaTab(), new RemoteTab()
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

        public string KoinaIntensityModelCombo
        {
            get { return (string)intensityModelCombo.SelectedItem; }
            set { intensityModelCombo.SelectedItem = value; }
        }

        public string KoinaRetentionTimeModelCombo
        {
            get { return (string)iRTModelCombo.SelectedItem; }
            set { iRTModelCombo.SelectedItem = value; }
        }

        public int CECombo
        {
            get { return (int)ceCombo.SelectedItem; }
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
                        ToolsUIResources
                            .ToolOptionsUI_btnResetSettings_Click_Are_you_sure_you_want_to_clear_all_saved_settings__This_will_immediately_return__0__to_its_original_configuration_and_cannot_be_undone_,
                        Program.Name), MultiButtonMsgDlg.BUTTON_OK) == DialogResult.OK)
            {
                Settings.Default.Reset();
                Settings.Default.SettingsUpgradeRequired = false; // do not restore settings from older versions
                Settings.Default.Save();
            }
        }

        private void koinaDescrLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(@"https://koina.wilhelmlab.org/");
        }

        private void intensityModelCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty((string)intensityModelCombo.SelectedItem) &&
                !string.IsNullOrEmpty((string)iRTModelCombo.SelectedItem))
            {
                iRTModelCombo.SelectedItem = string.Empty;
            }
            else if (!string.IsNullOrEmpty((string)intensityModelCombo.SelectedItem) &&
                     string.IsNullOrEmpty((string)iRTModelCombo.SelectedItem))
            {
                iRTModelCombo.SelectedIndex = 1; // First non-empty iRT model
            }

            UpdateServerStatus();
        }

        private void iRTModelCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty((string)intensityModelCombo.SelectedItem) &&
                string.IsNullOrEmpty((string)iRTModelCombo.SelectedItem))
            {
                intensityModelCombo.SelectedItem = string.Empty;
            }
            else if (string.IsNullOrEmpty((string)intensityModelCombo.SelectedItem) &&
                     !string.IsNullOrEmpty((string)iRTModelCombo.SelectedItem))
            {
                intensityModelCombo.SelectedIndex = 1; // First non-empty intensity model
            }

            UpdateServerStatus();
        }

        private void ToolOptionsUI_Shown(object sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == (int)TABS.Koina)
                UpdateServerStatus();
            else
            {
                tabControl.SelectedIndexChanged += tabControl_SelectedIndexChanged;
            }
        }

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == (int)TABS.Koina)
                UpdateServerStatus();
        }

        private void btnCarafeTrainingDataGenerationSettings_Click(object sender, EventArgs e)
        {
            this.ActiveControl = null;

            if (CarafeLibraryBuilder.DataParameters == null)
                CarafeLibraryBuilder.CarafeDefaultSettings();
            if (KeyValueGridDlg.Show(this, btnCarafeTrainingDataGenerationSettings.Text,
                    CarafeLibraryBuilder.DataParameters,
                    setting => setting.Value.ToString(),
                    (value, setting) => setting.Value = value,
                    (value, setting) => setting.Validate(value),
                    setting => setting.ValidValues,
                    setting => setting.Description) != null)
                this.DialogResult = DialogResult.None;
        }

        private void btnCarafeModelTrainingSettings_Click(object sender, EventArgs e)
        {
            this.ActiveControl = null;

            if (CarafeLibraryBuilder.ModelParameters == null)
                CarafeLibraryBuilder.CarafeDefaultSettings();
            if (KeyValueGridDlg.Show(this, btnCarafeModelTrainingSettings.Text,
                    CarafeLibraryBuilder.ModelParameters,
                    setting => setting.Value.ToString(),
                    (value, setting) => setting.Value = value,
                    (value, setting) => setting.Validate(value),
                    setting => setting.ValidValues,
                    setting => setting.Description) != null)
                this.DialogResult = DialogResult.None;
        }

        private void btnCarafeLibrarySettings_Click(object sender, EventArgs e)
        {
            if (CarafeLibraryBuilder.LibraryParameters == null)
                CarafeLibraryBuilder.CarafeDefaultSettings();
            KeyValueGridDlg.Show(btnCarafeLibraryGenerationSettings.Text,
                    CarafeLibraryBuilder.LibraryParameters,
                    setting => setting.Value.ToString(),
                    (value, setting) => setting.Value = value,
                    (value, setting) => setting.Validate(value),
                    setting => setting.ValidValues,
                    setting => setting.Description);
        }

        private void ToolsOptionsUI_CarafeHelpButtonClicked(object sender, EventArgs e)
        {
            const string proteinAssociationWikiPath =
                @"/wiki/home/software/Skyline/page.view?name=Carafe%20Options%20Help"; // CONSIDER: get version programatically
            if (sender == lnkCarafeTrainingDataHelp)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#training-data-generation");
            else if (sender == lnkCarafeModelTrainingHelp)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#model-training");
            else if (sender == lnkCarafeLibraryHelp)
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath + @"#library-generation");
            else
                WebHelpers.OpenSkylineLink(this, proteinAssociationWikiPath);
        }

        private void lnkCarafeHelp_Clicked(object sender, EventArgs e)
        {
            ToolsOptionsUI_CarafeHelpButtonClicked(sender, e);
        }


        private void btnAlphaPeptDeepUserSettings_Click(object sender, EventArgs e)
        {
            if (AlphapeptdeepLibraryBuilder.UserParameters == null)
                AlphapeptdeepLibraryBuilder.AlphaPeptDeepDefaultSettings();
            KeyValueGridDlg.Show(btnAlphaPeptDeepUserSettings.Text,
                AlphapeptdeepLibraryBuilder.UserParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description);
        }

        private void EmbedCarafeDataSettings()
        {
            btnCarafeTrainingDataGenerationSettings.Visible = false;
            if (CarafeLibraryBuilder.DataParameters == null)
                CarafeLibraryBuilder.CarafeDefaultDataSettings();

            _carafeDataParams = KeyValueGridDlg.Show(null, btnCarafeTrainingDataGenerationSettings.Text,
                CarafeLibraryBuilder.DataParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeDataTabControl);
        }

        private void EmbedCarafeModelSettings()
        {
            btnCarafeModelTrainingSettings.Visible = false;
            if (CarafeLibraryBuilder.ModelParameters == null)
                CarafeLibraryBuilder.CarafeDefaultModelSettings();

            _carafeModelParams = KeyValueGridDlg.Show(null, btnCarafeModelTrainingSettings.Text,
                CarafeLibraryBuilder.ModelParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeModelTabControl);
        }

        private void EmbedCarafeLibrarySettings()
        {
            btnCarafeLibraryGenerationSettings.Visible = false;
            if (CarafeLibraryBuilder.LibraryParameters == null)
                CarafeLibraryBuilder.CarafeDefaultLibrarySettings();

            _carafeLibraryParams = KeyValueGridDlg.Show(null, btnCarafeLibraryGenerationSettings.Text,
                CarafeLibraryBuilder.LibraryParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, CarafeLibraryTabControl);
        }
        private void EmbedAlphaPeptDeepUserSettings()
        {
            btnAlphaPeptDeepUserSettings.Visible = false;
            if (AlphapeptdeepLibraryBuilder.UserParameters == null)
                AlphapeptdeepLibraryBuilder.AlphaPeptDeepDefaultSettings();

            _alphaPeptDeepParams = KeyValueGridDlg.Show(null, btnAlphaPeptDeepUserSettings.Text,
                AlphapeptdeepLibraryBuilder.UserParameters,
                setting => setting.Value.ToString(),
                (value, setting) => setting.Value = value,
                (value, setting) => setting.Validate(value),
                setting => setting.ValidValues,
                setting => setting.Description, AlphaPeptDeepTabControl);
        }

        private Dictionary<string, Control> _alphaPeptDeepParams;
        private Dictionary<string, Control> _carafeDataParams;
        private Dictionary<string, Control> _carafeModelParams;
        private Dictionary<string, Control> _carafeLibraryParams;
        public Control.ControlCollection AlphaPeptDeepTabControl
        {
            get => tabAlphaPeptDeep.Controls;
        }
        public Control.ControlCollection CarafeDataTabControl
        {
            get => tabCarafeData.Controls;
        }
        public Control.ControlCollection CarafeModelTabControl
        {
            get => tabCarafeModel.Controls;
        }
        public Control.ControlCollection CarafeLibraryTabControl
        {
            get => tabCarafeLibrary.Controls;
        }
        private void btnOK_Click(object sender, EventArgs e)
        {
            if (this.ActiveControl is ComboBox)
            {
                this.DialogResult = DialogResult.None;
            }
            this.ActiveControl = null;
            SetParameters(AlphapeptdeepLibraryBuilder.UserParameters, _alphaPeptDeepParams);
            SetParameters(CarafeLibraryBuilder.DataParameters, _carafeDataParams);
            SetParameters(CarafeLibraryBuilder.ModelParameters, _carafeModelParams);
            SetParameters(CarafeLibraryBuilder.LibraryParameters, _carafeLibraryParams);
        }
        private void SetParameters(IDictionary<string, AbstractDdaSearchEngine.Setting> gridValues, Dictionary<string, Control> keyValueParams)
        {
            this.ActiveControl = null;
            foreach (var kvp in keyValueParams)
            {
                if (!gridValues[kvp.Key].IsValid)
                    this.DialogResult = DialogResult.None;

                if (kvp.Value is TextBox tb)
                    gridValues[kvp.Key].Value = tb.Text;
                else if (kvp.Value is CheckBox cb)
                    gridValues[kvp.Key].Value = cb.Checked.ToString();
                else if (kvp.Value is ComboBox cmb)
                {
                    if (cmb.SelectedItem != null)
                    {
                        gridValues[kvp.Key].Value = cmb.SelectedItem.ToString();
                    }
                }
                else
                    throw new InvalidOperationException();

            }
        }
        private void tabAlphaPeptDeep_Click(object sender, EventArgs e)
        {

        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void lnkCarafeTrainingDataHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {

        }
    }
}
