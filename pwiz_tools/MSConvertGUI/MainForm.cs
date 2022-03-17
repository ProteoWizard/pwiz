//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using CustomDataSourceDialog;
using pwiz.CLI.msdata;

namespace MSConvertGUI
{
    public partial class MainForm : Form
    {
        IList<string> cmdline_args;
        string SetDefaultsDataType = ""; // watch last-added filetype, offer to set defaults for that type
        string AboutButtonHelpText = "Version: " + Application.ProductVersion + "\r\n\r\n" +
            "Each control has a \"tooltip\": let your cursor rest atop a control for a moment to see a help message for that control.\r\n\r\n" +
            "For more in depth help, visit http://proteowizard.sourceforge.net/tools.shtml .  " +
            "The documentation there describing the command line version of msconvert will be useful for understanding this " +
            "program, especially filters.\r\n\r\nActually, you will generally find the command line version to be more current, complete and " +
            "flexible than this GUI version and may wish to treat this program as a learning tool for using the command line " +
            "version.\r\n\r\nTo repeat: IF YOU DO NOT SEE A NEEDED OPTION OR FILTER HERE, TRY USING THE COMMAND LINE VERSION (\"MSCONVERT.EXE\") INSTEAD.\r\n\r\nHere are the various tooltips in a more long lasting form:";
        System.Collections.SortedList sortedToolTips = new System.Collections.SortedList();

        private IList<KeyValuePair<string, string>> thresholdTypes = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("Count", "count"),
            new KeyValuePair<string, string>("Count after ties", "count-after-ties"),
            new KeyValuePair<string, string>("Absolute intensity", "absolute"),
            new KeyValuePair<string, string>("Relative to BPI", "bpi-relative"),
            new KeyValuePair<string, string>("Relative to TIC", "tic-relative"),
            new KeyValuePair<string, string>("Fraction of TIC", "tic-cutoff")
        };

        private string PresetFolder { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSConvertGUI"; } }

        private void LoadPresets()
        {
            if (!Directory.Exists(PresetFolder))
                Directory.CreateDirectory(PresetFolder);

            presetComboBox.Items.Clear();
            foreach (var filename in Directory.GetFiles(PresetFolder, "*.cmdline"))
            {
                presetComboBox.Items.Add(Path.GetFileNameWithoutExtension(filename));
            }
        }

        private void SelectPreset(string name)
        {
            int i = presetComboBox.Items.IndexOf(name);
            if (i >= 0)
                presetComboBox.SelectedIndex = i;
            else
                throw new FileNotFoundException("invalid preset name");
        }

        private void SetDefaultPreset()
        {
            string presetName = SetDefaultsDataType;
            if (presetName == String.Empty)
                presetName = "Generic";
            presetName += " Defaults";

            try
            {
                SelectPreset(presetName);
            }
            catch (FileNotFoundException)
            {
                try
                {
                    SelectPreset("Generic Defaults");
                }
                catch
                {
                    // generic defaults not found, so create it
                    setCfgFromGUI(MakePresetFilename("Generic Defaults"));
                    SelectPreset("Generic Defaults");
                }
            }
        }

        public MainForm(IList<string> args)
        {
            cmdline_args = args;
            InitializeComponent();

            Text = "MSConvertGUI" + (Environment.Is64BitProcess ? " (64-bit)" : "");
            LoadPresets();
            EnableDragAndDropRows(FilterDGV);
        }

        private void setFileBoxText(object item)
        {
            string text = item.ToString();

            if ("" != text)
                lastFileboxText = text; // for use in setting browse directory

            if (text.Count(o => "?*".Contains(o)) > 0)
            {
                string directory = Path.GetDirectoryName(text);
                if (String.IsNullOrEmpty(directory))
                    directory = ".";
                foreach (string filepath in Directory.GetFiles(directory, Path.GetFileName(text)))
                {
                    FileBox.Tag = filepath;
                    FileBox.Text = filepath;
                    AddFileButton_Click(this, EventArgs.Empty);
                }
            }
            else if (IsNetworkSource(text))
            {
                var credentialMatch = Regex.Match(text, "(http[s]?://)([^:]+):([^@]+)@(.*)");
                if (credentialMatch.Success)
                {
                    string url = credentialMatch.Groups[1].Value + credentialMatch.Groups[4].Value;
                    string[] urlParts = new Uri(url).GetLeftPart(UriPartial.Authority).Split(':');
                    LastUsedUnifiCredentials = new UnifiBrowserForm.Credentials
                    {
                        Username = credentialMatch.Groups[2].Value,
                        Password = credentialMatch.Groups[3].Value,
                        IdentityServer = urlParts[0] + ":" + urlParts[1] + ":50333",
                        ClientScope = "unifi",
                        ClientSecret = "secret"
                    };
                    UnifiCredentialsByUrl[url] = LastUsedUnifiCredentials;

                    // NB: set Tag first because setting Text triggers FileBox_TextChanged
                    FileBox.Tag = url;
                    FileBox.Text = url;
                }
                else
                {
                    FileBox.Tag = text;
                    FileBox.Text = text;
                }
            }
            else
            {
                FileBox.Tag = item;
                FileBox.Text = text;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            assignTooltips();

            foreach (DataGridViewColumn column in FilterDGV.Columns)
                column.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;

            foreach (int value in Enum.GetValues(typeof(PeakPickingMethod)))
            {
                var type = typeof(PeakPickingMethod);
                string description = (Attribute.GetCustomAttribute(type.GetField(Enum.GetName(type, (PeakPickingMethod)value)), typeof(DescriptionAttribute)) as DescriptionAttribute).Description;
                PeakPickingAlgorithmComboBox.Items.Add(new ListViewItem(description) { Tag = value });
            }
            PeakPickingAlgorithmComboBox.SelectedIndex = 0;

            foreach (Control control in FilterGB.Controls)
            {
                if (control == FilterBox)
                    control.Location = new Point(FilterGB.Width / 2 - control.Width / 2, control.Location.Y);
                else
                    control.Location = new Point(FilterGB.Width / 2 - control.Width / 2, FilterGB.Height / 2 - control.Height / 2 + FilterBox.Height);
            }

            DemuxMassErrorTypeBox.SelectedIndex = 0;
            DemuxTypeBox.SelectedIndex = 0;

            for (int i = 0; i < cmdline_args.Count; i++)
            {
                // mimic user adding file via filebox
                setFileBoxText(cmdline_args[i]);
                AddFileButton_Click(sender, e);
                if (FileBox.Text.Length > 0)
                {
                    // if it didn't get added, field will still contain file or dir name
                    MessageBox.Show("Don't know how to read \"" + FileBox.Text + "\", ignored", this.Text);
                    setFileBoxText("");
                }
            }

            SetDefaultPreset();

            FilterBox.Text = "Subset";
            ActivationTypeBox.Text = "Any";
            AnalyzerTypeBox.Text = "Any";
            PolarityBox.Text = "Any";

            if (Properties.Settings.Default.LastUsedUnifiUrl.Length > 0)
            {
                try
                {
                    var unifiSettings = UnifiBrowserForm.Credentials.ParseUrlWithAuthentication(Properties.Settings.Default.LastUsedUnifiUrl);
                    LastUsedUnifiHost = unifiSettings.Item1;
                    LastUsedUnifiCredentials = unifiSettings.Item2;
                }
                catch (Exception ex)
                {
                    Program.HandleException(ex);
                }
            }

            FilesToConvertInParallelUpDown.Value = Properties.Settings.Default.NumFilesToConvertInParallel;

            thresholdTypeComboBox.Items.AddRange(thresholdTypes.Select(o => o.Key).ToArray());
            thresholdTypeComboBox.SelectedIndex = 0;
            thresholdOrientationComboBox.SelectedIndex = 0;
            thresholdValueTextBox.Text = "100";

            ValidateNumpress(); // make sure numpress settings are reasonable

            networkResourceComboBox.DisplayMember = "DisplayName";
            networkResourceComboBox.Items.Insert(0, placeholder);
            networkResourceComboBox.SelectedItem = placeholder;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // don't let running jobs prevent the process from exiting
            Process.GetCurrentProcess().Kill();
        }

        class DummyComboBoxItem
        {
            public string DisplayName
            {
                get
                {
                    return "Browse network resource...";
                }
            }
        }
        private DummyComboBoxItem placeholder = new DummyComboBoxItem();

        private Map<string, UnifiBrowserForm.Credentials> UnifiCredentialsByUrl = new Map<string, UnifiBrowserForm.Credentials>();
        private string LastUsedUnifiHost;
        private UnifiBrowserForm.Credentials LastUsedUnifiCredentials;

        private void networkResourceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (networkResourceComboBox.SelectedItem == null) return;
            if (networkResourceComboBox.SelectedItem == placeholder) return;

            if (networkResourceComboBox.SelectedItem.ToString() == "UNIFI")
            {
                var browser = new UnifiBrowserForm(LastUsedUnifiHost, LastUsedUnifiCredentials);
                if (browser.ShowDialog() == DialogResult.OK)
                {
                    var selectedDataSources = browser.SelectedSampleResults.ToList();
                    if (selectedDataSources.Count == 1)
                    {
                        setFileBoxText(selectedDataSources[0]);
                        UnifiCredentialsByUrl[selectedDataSources[0].Url] = browser.SelectedCredentials;
                    }
                    else if (selectedDataSources.Count > 1)
                    {
                        foreach (var dataSource in selectedDataSources)
                        {
                            UnifiCredentialsByUrl[dataSource.Url] = browser.SelectedCredentials;
                            FileListBox.Items.Add(dataSource);
                        }

                        /*if (String.IsNullOrEmpty(OutputBox.Text) ||
                            !Directory.Exists(OutputBox.Text))
                            OutputBox.Text = Path.GetDirectoryName(selectedDataSources[0]);*/

                        RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
                    }
                }

                LastUsedUnifiHost = browser.SelectedHost;
                if (browser.SelectedCredentials != null)
                {
                    LastUsedUnifiCredentials = browser.SelectedCredentials;
                    Properties.Settings.Default.LastUsedUnifiUrl = LastUsedUnifiCredentials.GetUrlWithAuthentication(LastUsedUnifiHost);
                }
                else
                    Properties.Settings.Default.LastUsedUnifiUrl = LastUsedUnifiHost;
                Properties.Settings.Default.Save();
            }

            networkResourceComboBox.Items.Add(placeholder);
            networkResourceComboBox.SelectedItem = placeholder;
        }

        private void networkResourceComboBox_DropDown(object sender, EventArgs e)
        {
            networkResourceComboBox.Items.Remove(placeholder);
        }

        private void networkResourceComboBox_Leave(object sender, EventArgs e)
        {
            //this covers user aborting the selection (by clicking away or choosing the system null drop down option)
            //The control may not immedietly change, but if the user clicks anywhere else it will reset
            if (networkResourceComboBox.SelectedItem != placeholder)
            {
                if (!networkResourceComboBox.Items.Contains(placeholder)) networkResourceComboBox.Items.Add(placeholder);
                networkResourceComboBox.SelectedItem = placeholder;
            }
        }

        private void FilterBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            PeakPickingPanel.Visible = false;
            DemultiplexPanel.Visible = false;
            ZeroSamplesPanel.Visible = false;
            ETDFilterPanel.Visible = false;
            ThresholdFilterPanel.Visible = false;
            ChargeStatePredictorPanel.Visible = false;
            SubsetPanel.Visible = false;
            LockmassRefinerPanel.Visible = false;
            ScanSummingPanel.Visible = false;
            DiaUmpirePanel.Visible = false;

            switch (FilterBox.Text)
            {
                case "Peak Picking":
                    PeakPickingPanel.Visible = true;
                    break;
                case "Demultiplex":
                    DemultiplexPanel.Visible = true;
                    break;
                case "Zero Samples":
                    ZeroSamplesPanel.Visible = true;
                    break;
                case "ETD Peak Filter":
                    ETDFilterPanel.Visible = true;
                    break;
                case "Threshold Peak Filter":
                    ThresholdFilterPanel.Visible = true;
                    break;
                case "Charge State Predictor":
                    ChargeStatePredictorPanel.Visible = true;
                    break;
                case "Subset":
                    SubsetPanel.Visible = true;
                    break;
                case "Lockmass Refiner":
                    LockmassRefinerPanel.Visible = true;
                    break;
                case "Scan Summing":
                    ScanSummingPanel.Visible = true;
                    break;
                case "DIA-Umpire":
                    DiaUmpirePanel.Visible = true;
                    break;
            }
        }

        public static string IdentifySource(object dataSource)
        {
            if (dataSource is INetworkSource networkSource)
                return ReaderList.FullReaderList.identify(networkSource.Url);
            else
                return ReaderList.FullReaderList.identify(dataSource.ToString());
        }

        public static bool IsNetworkSource(object dataSource)
        {
            return dataSource is INetworkSource ||
                   dataSource.ToString().StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) ||
                   dataSource.ToString().StartsWith("https://", StringComparison.InvariantCultureIgnoreCase);
        }

        public bool IsValidSource(object dataSource)
        {
            string sourcePath = dataSource.ToString();
            return (IsNetworkSource(dataSource) || File.Exists(sourcePath) || Directory.Exists(sourcePath)) &&
                   !FileListBox.Items.Contains(dataSource) &&
                   !String.IsNullOrEmpty(IdentifySource(dataSource));
        }

        private void FileBox_TextChanged(object sender, EventArgs e)
        {
            string fileBoxText = FileBox.Text.Trim();
            if (FileBox.Tag == null || FileBox.Tag is string) FileBox.Tag = fileBoxText;
            AddFileButton.Enabled = IsValidSource(FileBox.Tag);
        }

        private void AddFileButton_Click(object sender, EventArgs e)
        {
            if (IsValidSource(FileBox.Tag))
            {
                if (String.IsNullOrEmpty(OutputBox.Text))
                {
                    if (!IsNetworkSource(FileBox.Tag))
                        OutputBox.Text = Path.GetDirectoryName(FileBox.Text);
                    else
                        OutputBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                }

                // update the set-defaults button
                SetDefaultsDataType = IdentifySource(FileBox.Tag);
                presetSetDefaultButton.Text = "Save as defaults for " + SetDefaultsDataType + " data";
                //setToolTip(presetSetDefaultButton, "Saves the current settings and uses them as the defaults next time you open " + SetDefaultsDataType + " data with MSConvertGUI.");
                // and add to the list
                FileListBox.Items.Add(FileBox.Tag);
                FileBox.Clear();
                RemoveFileButton.Enabled = true;
            }
        }

        private void RemoveFileButton_Click(object sender, EventArgs e)
        {
            if (FileListBox.SelectedItems.Count == 1)
            {
                setFileBoxText(FileListBox.Items[FileListBox.SelectedIndex]);
                FileListBox.Items.RemoveAt(FileListBox.SelectedIndex);
            }
            else if (FileListBox.SelectedItems.Count > 1)
            {
                setFileBoxText(FileListBox.SelectedItems[0]);
                var itemsToDelete = FileListBox.SelectedItems.Cast<object>().ToList();
                foreach (var item in itemsToDelete)
                    FileListBox.Items.Remove(item);
            }
            else
                return;

            RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
            AddFileButton.Enabled = IsValidSource(FileBox.Tag);
        }

        private void BrowseFileButton_Click(object sender, EventArgs e)
        {
            var fileList = new List<string>();
            foreach (var typeExtsPair in ReaderList.FullReaderList.getFileExtensionsByType())
                if (typeExtsPair.Value.Count > 0) // e.g. exclude UNIFI
                    fileList.Add(typeExtsPair.Key);
            fileList.Sort();
            fileList.Insert(0, "Any spectra format");

            OpenDataSourceDialog browseToFileDialog;
            browseToFileDialog = String.IsNullOrEmpty(FileBox.Text)
                                     ? new OpenDataSourceDialog(fileList, lastFileboxText)
                                     : new OpenDataSourceDialog(fileList, FileBox.Text);

            #region Set up Delegates
            browseToFileDialog.FolderType = x =>
                                                {
                                                    try
                                                    {
                                                        string type = ReaderList.FullReaderList.identify(x);
                                                        if (type == String.Empty)
                                                            return "File Folder";
                                                        return type;
                                                    }
                                                    catch { return String.Empty; }
                                                };
            browseToFileDialog.FileType = x =>
                                              {
                                                  try
                                                  {
                                                      return ReaderList.FullReaderList.identify(x);
                                                  }
                                                  catch { return String.Empty; }
                                              };
            #endregion

            if (browseToFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (browseToFileDialog.DataSources.Count == 1)
                    setFileBoxText(browseToFileDialog.DataSources[0]);
                else if (browseToFileDialog.DataSources.Count > 1)
                {
                    foreach (string dataSource in browseToFileDialog.DataSources)
                        FileListBox.Items.Add(dataSource);

                    if (String.IsNullOrEmpty(OutputBox.Text) ||
                        !Directory.Exists(OutputBox.Text))
                        OutputBox.Text = Path.GetDirectoryName(browseToFileDialog.DataSources[0]);

                    RemoveFileButton.Enabled = FileListBox.Items.Count > 0;
                }
            }
        }

        private void FileListBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (FileListBox.SelectedIndices.Count > 0)
                {
                    foreach (int selectedIndex in FileListBox.SelectedIndices.Cast<int>().Reverse())
                        FileListBox.Items.RemoveAt(selectedIndex);
                }
                e.Handled = true;
            }
        }

        private void ZeroSamples_ModeChanged(object sender, EventArgs e)
        {
            ZeroSamplesAddMissingFlankCountBox.Enabled = ZeroSamplesAddMissing.Checked;
        }

        private void NumTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private enum PeakPickingMethod
        {
            [Description("Vendor (does not work for UNIFI, and it MUST be the first filter!)")]
            Vendor,

            [Description("CWT (continuous wavelet transform; works for any profile data)")]
            Cwt
        }

        private void AddFilterButton_Click(object sender, EventArgs e)
        {
            switch (FilterBox.Text)
            {
                case "Peak Picking":
                    if (!String.IsNullOrEmpty(PeakMSLevelLow.Text) ||
                        !String.IsNullOrEmpty(PeakMSLevelHigh.Text))
                    {
                        PeakPickingMethod method = (PeakPickingMethod)(PeakPickingAlgorithmComboBox.SelectedItem as ListViewItem).Tag;
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "peakPicking",
                                                   String.Format("{0} {1}msLevel={2}-{3}",
                                                                 Enum.GetName(typeof(PeakPickingMethod), method).ToLowerInvariant(),
                                                                 method == PeakPickingMethod.Cwt ? String.Format("snr={0} peakSpace={1} ", PeakMinSnr.Text, PeakMinSpacing.Text) : String.Empty,
                                                                 PeakMSLevelLow.Text, PeakMSLevelHigh.Text)
                                               });
                    }
                    break;
                case "Demultiplex":
                    String demuxArgs = String.Empty;
                    if (!String.IsNullOrEmpty(DemuxTypeBox.Text))
                    {
                        String optimizationArgs = DemuxTypeBox.Text == "Overlap Only" ? " optimization=overlap_only" : String.Empty;
                        demuxArgs += optimizationArgs;
                    }

                    if (!String.IsNullOrEmpty(DemuxMassErrorValue.Text) &&
                        !String.IsNullOrEmpty(DemuxMassErrorTypeBox.Text))
                    {
                        demuxArgs += String.Format(" massError={0}{1}",
                            DemuxMassErrorValue.Text,
                            DemuxMassErrorTypeBox.Text);
                    }

                    FilterDGV.Rows.Add(new[]
                        {
                            "demultiplex",
                            demuxArgs
                        });
                    break;
                case "Zero Samples":
                    String args = ZeroSamplesAddMissing.Checked ? "addMissing" : "removeExtra";
                    if (ZeroSamplesAddMissing.Checked && (!String.IsNullOrEmpty(ZeroSamplesAddMissingFlankCountBox.Text)))
                        args += String.Format("={0}", ZeroSamplesAddMissingFlankCountBox.Text);
                    if (!String.IsNullOrEmpty(ZeroSamplesMSLevelLow.Text) ||
                        !String.IsNullOrEmpty(ZeroSamplesMSLevelHigh.Text))
                        args += String.Format(" {0}-{1}", ZeroSamplesMSLevelLow.Text, ZeroSamplesMSLevelHigh.Text);
                    else // no mslevels specified means all mslevels
                        args += " 1-";
                    FilterDGV.Rows.Add(new[]
                                           {
                                               "zeroSamples",
                                                args
                                           });
                    break;

                case "DIA-Umpire":
                    FilterDGV.Rows.Add(new[]
                    {
                        "diaUmpire",
                        String.Format("params={0}", DiaUmpireParamsFileTextBox.Text)
                    });
                    break;

                case "ETD Peak Filter":
                    var tempObject = new[] { "ETDFilter", String.Empty };
                    if (!ETDRemovePrecursorBox.Checked || !ETDRemoveChargeReducedBox.Checked ||
                        !ETDRemoveNeutralLossBox.Checked || !ETDBlanketRemovalBox.Checked)
                        tempObject[1] = String.Format("{0} {1} {2} {3}",
                                                      ETDRemovePrecursorBox.Checked.ToString().ToLowerInvariant(),
                                                      ETDRemoveChargeReducedBox.Checked.ToString().ToLowerInvariant(),
                                                      ETDRemoveNeutralLossBox.Checked.ToString().ToLowerInvariant(),
                                                      ETDBlanketRemovalBox.Checked.ToString().ToLowerInvariant());
                    FilterDGV.Rows.Add(tempObject);
                    break;
                case "Charge State Predictor":
                    if (!String.IsNullOrEmpty(ChaMCMinBox.Text) &&
                        !String.IsNullOrEmpty(ChaMCMaxBox.Text))
                        FilterDGV.Rows.Add(new[]
                                               {
                                                   "chargeStatePredictor",
                                                   String.Format("overrideExistingCharge={0} maxMultipleCharge={1} minMultipleCharge={2} singleChargeFractionTIC={3}",
                                                                 ChaOverwriteCharge.Checked.ToString().ToLowerInvariant(),
                                                                 ChaMCMaxBox.Text, ChaMCMinBox.Text, ChaSingleBox.Value)
                                               });
                    break;
                case "Subset":
                    string scanTimeLow = null, scanTimeHigh = null;
                    if (!String.IsNullOrEmpty(ScanTimeLow.Text) || !String.IsNullOrEmpty(ScanTimeHigh.Text))
                    {
                        scanTimeLow = String.IsNullOrEmpty(ScanTimeLow.Text) ? "0" : ScanTimeLow.Text;
                        scanTimeHigh = String.IsNullOrEmpty(ScanTimeHigh.Text) ? "1e8" : ScanTimeHigh.Text;
                    }

                    if (!String.IsNullOrEmpty(MSLevelLow.Text) || !String.IsNullOrEmpty(MSLevelHigh.Text))
                        FilterDGV.Rows.Add(new[] { "msLevel", String.Format("{0}-{1}", MSLevelLow.Text, MSLevelHigh.Text) });
                    if (!String.IsNullOrEmpty(ScanNumberLow.Text) || !String.IsNullOrEmpty(ScanNumberHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanNumber", String.Format("{0}-{1}", ScanNumberLow.Text, ScanNumberHigh.Text) });
                    if (!String.IsNullOrEmpty(scanTimeLow) || !String.IsNullOrEmpty(scanTimeHigh))
                        FilterDGV.Rows.Add(new[] { "scanTime", String.Format("[{0},{1}]", scanTimeLow, scanTimeHigh) });
                    if (!String.IsNullOrEmpty(ScanEventLow.Text) || !String.IsNullOrEmpty(ScanEventHigh.Text))
                        FilterDGV.Rows.Add(new[] { "scanEvent", String.Format("{0}-{1}", ScanEventLow.Text, ScanEventHigh.Text) });
                    if (!String.IsNullOrEmpty(ChargeStatesLow.Text) || !String.IsNullOrEmpty(ChargeStatesHigh.Text))
                        FilterDGV.Rows.Add(new[] { "chargeState", String.Format("{0}-{1}", ChargeStatesLow.Text, ChargeStatesHigh.Text) });
                    if (!String.IsNullOrEmpty(DefaultArrayLengthLow.Text) || !String.IsNullOrEmpty(DefaultArrayLengthHigh.Text))
                        FilterDGV.Rows.Add(new[] { "defaultArrayLength", String.Format("{0}-{1}", DefaultArrayLengthLow.Text, DefaultArrayLengthHigh.Text) });
                    if (!String.IsNullOrEmpty(CollisionEnergyLow.Text) && !String.IsNullOrEmpty(CollisionEnergyHigh.Text))
                        FilterDGV.Rows.Add(new[] { "collisionEnergy", String.Format("low={0} high={1} acceptNonCID={2} acceptMissingCE={3}", CollisionEnergyLow.Text, CollisionEnergyHigh.Text,
                                                                                    CollisionEnergyAcceptNonCIDMSnSpectra.Checked, CollisionEnergyAcceptCIDSpectraWithMissingCE.Checked) });
                    if (ActivationTypeBox.Text != "Any")
                        FilterDGV.Rows.Add(new[] { "activation", ActivationTypeBox.Text });
                    if (AnalyzerTypeBox.Text != "Any")
                        FilterDGV.Rows.Add(new[] { "analyzer", AnalyzerTypeBox.Text });
                    if (PolarityBox.Text != "Any")
                        FilterDGV.Rows.Add(new[] { "polarity", PolarityBox.Text.ToLowerInvariant() });
                    //if (!String.IsNullOrEmpty(mzWinLow.Text) || !String.IsNullOrEmpty(mzWinHigh.Text))
                    //    FilterDGV.Rows.Add(new[] { "mzWindow", String.Format("[{0},{1}]", mzWinLow.Text, mzWinHigh.Text) });
                    break;
                case "Threshold Peak Filter":
                    int thresholdTypeIndex = thresholdTypes.Select(o => o.Key).ToList().IndexOf(thresholdTypeComboBox.SelectedItem.ToString());
                    string thresholdType = thresholdTypes[thresholdTypeIndex].Value; // Count after ties -> count-after-ties
                    string thresholdOrientation = thresholdOrientationComboBox.SelectedItem.ToString().ToLowerInvariant().Replace(' ', '-'); // Most intense -> most-intense
                    FilterDGV.Rows.Add(new[] { "threshold", String.Format("{0} {1} {2}", thresholdType, thresholdValueTextBox.Text, thresholdOrientation) });
                    break;
                case "Lockmass Refiner":
                    FilterDGV.Rows.Add(new[] { "lockmassRefiner", $"mz={LockmassMz.Text} tol={LockmassTolerance.Text}" });
                    break;
                case "Scan Summing":
                    FilterDGV.Rows.Add(new[] { "scanSumming", $"precursorTol={ScanSummingPrecursorToleranceTextBox.Text} scanTimeTol={ScanSummingScanTimeToleranceTextBox.Text} ionMobilityTol={ScanSummingIonMobilityToleranceTextBox.Text} sumMs1={(ScanSummingSumMs1Checkbox.Checked ? 1 : 0)}" });
                    break;
            }
        }

        private void RemoveFilterButton_Click(object sender, EventArgs e)
        {
            if (FilterDGV.SelectedRows.Count > 0)
                FilterDGV.Rows.Remove(FilterDGV.SelectedRows[0]);
        }

        private void ValidateNumpress()
        {
            bool numpressEnable = ("mzML" == OutputFormatBox.Text); // meaningless outside of mzML
            NumpressLinearBox.Enabled = numpressEnable;
            NumpressPicBox.Enabled = numpressEnable;
            NumpressSlofBox.Enabled = numpressEnable;
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressPicBox.Checked = false; // mutually exclusive
        }

        private void OutputFormatBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ValidateNumpress();
        }

        private void OutputBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new FolderBrowserDialog();
            if (!String.IsNullOrEmpty(OutputBox.Text) && Directory.Exists(OutputBox.Text))
                ofd.SelectedPath = OutputBox.Text;
            if (ofd.ShowDialog() == DialogResult.OK)
                OutputBox.Text = ofd.SelectedPath;
        }

        private void FileListRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (FileListRadio.Checked)
            {
                AddFileButton.Visible = true;
                RemoveFileButton.Visible = true;
                FileListBox.Enabled = true;
            }
            else
            {
                AddFileButton.Visible = false;
                RemoveFileButton.Visible = false;
                FileListBox.Enabled = false;
            }
        }

        private String ConstructCommandline()
        // if you update this, you probably need to update SetControlsFromCommandline too
        {
            var commandLine = new StringBuilder();
            //Get config settings

            if (OutputFormatBox.Text != OutputExtensionBox.Text)
                commandLine.AppendFormat("--ext|{0}|", OutputExtensionBox.Text);
            ValidateNumpress(); // make sure numpress settings are reasonable
            switch (OutputFormatBox.Text)
            {
                case "mzXML":
                    commandLine.Append("--mzXML|");
                    break;
                case "mz5":
                    commandLine.Append("--mz5|");
                    break;
                case "mgf":
                    commandLine.Append("--mgf|");
                    break;
                case "text":
                    commandLine.Append("--text|");
                    break;
                case "ms1":
                    commandLine.Append("--ms1|");
                    break;
                case "cms1":
                    commandLine.Append("--cms1|");
                    break;
                case "ms2":
                    commandLine.Append("--ms2|");
                    break;
                case "cms2":
                    commandLine.Append("--cms2|");
                    break;
            }

            if (Precision32.Checked)
                commandLine.Append("--32|");

            if (!WriteIndexBox.Checked)
                commandLine.Append("--noindex|");

            if (UseZlibBox.Checked)
                commandLine.Append("--zlib|");

            if (GzipBox.Checked)
                commandLine.Append("--gzip|");

            if (NumpressLinearBox.Checked)
                commandLine.AppendFormat("--numpressLinear|");

            if (NumpressSlofBox.Checked)
                commandLine.AppendFormat("--numpressSlof|");

            if (NumpressPicBox.Checked)
                commandLine.Append("--numpressPic|");

            if (CombineIonMobilitySpectraBox.Checked)
                commandLine.Append("--combineIonMobilitySpectra|");

            if (SimSpectraBox.Checked)
                commandLine.Append("--simAsSpectra|");

            if (SrmSpectraBox.Checked)
                commandLine.Append("--srmAsSpectra|");

            var msLevelsTotal = String.Empty;
            var scanNumberTotal = String.Empty;
            foreach (DataGridViewRow row in FilterDGV.Rows)
            {
                switch ((string)row.Cells[0].Value)
                {
                    case "msLevel":
                        msLevelsTotal += (string)row.Cells[1].Value + " ";
                        break;
                    case "scanNumber":
                        scanNumberTotal += (string)row.Cells[1].Value + " ";
                        break;
                    default:
                        commandLine.AppendFormat("--filter|{0} {1}|", row.Cells[0].Value, row.Cells[1].Value);
                        break;
                }
            }

            if (!String.IsNullOrEmpty(msLevelsTotal))
                commandLine.AppendFormat("--filter|msLevel {0}|", msLevelsTotal.Trim());
            if (!String.IsNullOrEmpty(scanNumberTotal))
                commandLine.AppendFormat("--filter|scanNumber {0}|", scanNumberTotal.Trim());

            if (MakeTPPCompatibleOutputButton.Checked)
            {
                String tppline = "--filter|titleMaker <RunId>.<ScanNumber>.<ScanNumber>.<ChargeState> File:\"<SourcePath>\", NativeID:\"<Id>\"|";
                if (!commandLine.ToString().Contains("--filter|titleMaker"))
                    commandLine.Append(tppline);
            }

            return commandLine.ToString();
        }

        private void SetControlsFromCommandline(string commandLine)
        // if you update this, you probably need to update ConstructCommandLine too
        {
            // Get config settings
            Precision32.Checked = (commandLine.IndexOf("--32") >= 0);
            Precision64.Checked = !Precision32.Checked;
            WriteIndexBox.Checked = !(commandLine.IndexOf("--noindex") >= 0);
            UseZlibBox.Checked = (commandLine.IndexOf("--zlib") >= 0);
            GzipBox.Checked = (commandLine.IndexOf("--gzip") >= 0);
            NumpressLinearBox.Checked = (commandLine.IndexOf("--numpressLinear") >= 0);
            NumpressSlofBox.Checked = (commandLine.IndexOf("--numpressSlof") >= 0);
            NumpressPicBox.Checked = (commandLine.IndexOf("--numpressPic") >= 0);
            CombineIonMobilitySpectraBox.Checked = (commandLine.IndexOf("--combineIonMobilitySpectra") >= 0);
            SimSpectraBox.Checked = (commandLine.IndexOf("--simAsSpectra") >= 0);
            SrmSpectraBox.Checked = (commandLine.IndexOf("--srmAsSpectra") >= 0);
            string OutputExtension = "";

            OutputFormatBox.Text = "mzML";
            FilterDGV.Rows.Clear();

            string[] words = commandLine.Split('|');
            for (int i = 0; i < words.Length; i++)
            {
                switch (words[i])
                {
                    case "--ext":
                        OutputExtension = words[++i];
                        break;
                    case "--mzXML":
                    case "--mz5":
                    case "--mgf":
                    case "--text":
                    case "--ms1":
                    case "--cms1":
                    case "--ms2":
                    case "--cms2":
                        OutputFormatBox.Text = words[i].Substring(2);
                        break;
                    case "--filter":
                        var space = words[++i].IndexOf(' ');
                        FilterDGV.Rows.Add(new[] { words[i].Substring(0,space),
                            words[i].Substring(space+1) });
                        break;
                    case "--32":
                    case "--noindex":
                    case "--zlib":
                    case "--gzip":
                    case "--numpressLinear":
                    case "--numpressSlof":
                    case "--numpressPic":
                    case "--combineIonMobilitySpectra":
                    case "--simAsSpectra":
                    case "--srmAsSpectra":
                        break; // already handled these booleans above
                    case "":
                        break; // just that trailing "|"
                    default:
                        MessageBox.Show("skipping unknown config item \"" + words[i] + "\"");
                        for (int j = i + 1; j < words.Length; j++)  // skip any args
                        {
                            if (words[j].StartsWith("--"))
                            {
                                i = j - 1;
                                break;
                            }
                            i++;
                        }
                        break;
                }
                if (OutputExtension != "")
                {
                    OutputExtensionBox.Text = OutputExtension;
                }
                ValidateNumpress(); // make sure numpress settings are reasonable
            }
        }

        /// <summary>
        /// Returns preset filename for the given preset name. If no preset name is given, then:
        /// 1. If a file is queued, returns a preset filename representing the default settings for that file type
        /// 2. Else returns a generic defaults preset
        /// </summary>
        private string MakePresetFilename(string presetName = null)
        {
            if (presetName == null)
            {
                if (SetDefaultsDataType != "") // any current input type?
                    presetName = String.Format($"{SetDefaultsDataType} Defaults");
                else
                    presetName = "Generic Defaults";
            }
            // note not calling these files "*.cfg" in order to distinguish them from the boost program_options files
            return Path.Combine(PresetFolder, presetName + ".cmdline");
        }

        private void presetSaveButton_Click(object sender, EventArgs e)
        {
            string presetName = presetComboBox.SelectedItem as string;
            setCfgFromGUI(MakePresetFilename(presetName));
            SelectPreset(presetName);
        }

        private void presetSaveAsButton_Click(object sender, EventArgs e)
        {
            using (var textInputPrompt = new TextInputPrompt("Preset Name", false, "")
            {
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                ControlBox = false,
                ShowIcon = false,
                ShowInTaskbar = false
            })
            {
                if (textInputPrompt.ShowDialog(this) == DialogResult.Cancel)
                    return;

                var cfgFileName = MakePresetFilename(textInputPrompt.GetText());
                if (File.Exists(cfgFileName) &&
                    (MessageBox.Show("Preset \"" + textInputPrompt.GetText() + "\" already exists. Do you want to replace it?",
                                        "MSConvertGUI",
                                        MessageBoxButtons.YesNo,
                                        MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No))
                {
                    return;
                }

                setCfgFromGUI(cfgFileName);
                SelectPreset(textInputPrompt.GetText());
            }
        }

        private void setCfgFromGUI(string cfgFileName) // write a config file for current GUI state
        {
            string cmdline = ConstructCommandline();
            File.WriteAllText(cfgFileName, cmdline);
            LoadPresets();
        }

        private void SetGUIfromCfg(string cfgFileName) // populate buttons etc from config file
        {
            if (!File.Exists(cfgFileName))
            {
                MessageBox.Show("Can't find config file \"" + cfgFileName + "\"");
                return;
            }
            string cmdline = File.ReadAllText(cfgFileName);
            SetControlsFromCommandline(cmdline);
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            var filesToProcess = new List<object>();
            var commandLine = ConstructCommandline();

            //Get files or filelist text
            if (FileListRadio.Checked)
            {
                if (FileListBox.Items.Count == 0)
                {
                    MessageBox.Show("No files to process");
                    return;
                }

                filesToProcess.AddRange(from object item in FileListBox.Items select item);
            }
            else if (String.IsNullOrEmpty(FileBox.Text) || !File.Exists(FileBox.Text))
            {
                MessageBox.Show("No files to process");
                return;
            }
            else
            {
                filesToProcess.Add(String.Format("--filelist|\"{0}\"", FileBox.Text));
            }

            string outputFolder = String.IsNullOrEmpty(OutputBox.Text) ? Application.StartupPath
                                                                       : OutputBox.Text;
            if (!Directory.Exists(outputFolder))
            {
                if (MessageBox.Show("The directory \"" + outputFolder + "\" does not exist. Do you want to create it?",
                                    "Create Directory?",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.No)
                    return;
                Directory.CreateDirectory(outputFolder);
            }


            var pf = new ProgressForm(filesToProcess, outputFolder, commandLine, UnifiCredentialsByUrl);
            pf.Text = "Conversion Progress";
            pf.ShowDialog();

        }

        void setToolTip(Control ctl, string text, string filtername = "")
        {
            ctlToolTip.UseFading = true;
            ctlToolTip.UseAnimation = true;
            ctlToolTip.IsBalloon = true;

            ctlToolTip.ShowAlways = true;

            ctlToolTip.AutoPopDelay = 5000;
            ctlToolTip.InitialDelay = 1000;
            ctlToolTip.ReshowDelay = 500;

            ctlToolTip.SetToolTip(ctl, text);

            // construct the About box help
            // sometime we use the same tooltip on a label and a control
            // just retain the first use
            for (int i = 0; i < sortedToolTips.Count; i++)
            {
                if (text == sortedToolTips.GetByIndex(i).ToString())
                {
                    return;
                }
            }
            String key = "";
            if (filtername.Length > 0)
            {
                if (!filtername.Contains("Filter"))
                    key = "Filters: ";
                key += filtername + ": ";
            }
            else if (ctl.Parent.Text.Length > 0)
                key += ctl.Parent.Text + ": ";
            if ((!(ctl is TextBox)) && (ctl.Text.Length > 0))
                key += ctl.Text;
            else if (!ctl.Name.Contains("Panel"))
                key += ctl.Name;
            if (key.Length > 0)
                sortedToolTips.Add(key, text);
        }

        void assignTooltips()
        {
            setToolTip(this.PresetSaveButton, "Saves the current settings in the currently selected preset.");
            setToolTip(this.WriteIndexBox, "Include an index in mzML and mzXML output files.");
            setToolTip(this.GzipBox, "This compresses the entire output file using gzip, and adds \".gz\" to the end of the filename.");
            setToolTip(this.UseZlibBox, "Using zlib to compress peak lists results in much smaller mzML and mzXML output files.");
            setToolTip(this.OptionsGB, "Useful options for controlling output format and file size.");
            setToolTip(this.FileListRadio, "Click this for normal operation.");
            setToolTip(this.TextFileRadio, "Click this if your input file actually contains a list of files to be converted.");
            setToolTip(this.AddFilterButton, "Add the filter specifed above to the list below.", "Filters");
            setToolTip(this.RemoveFilterButton, "Select a filter in the list below then click here to remove it.", "Filters");
            setToolTip(this.LockmassRefinerPanel, "Corrects mass accuracy in Waters data.", "Lockmass Refiner");
            setToolTip(this.LockmassMz, "True m/z of the reference analyte.", "Lockmass Refiner");
            setToolTip(this.LockmassTolerance, "The refinement will look for an observed m/z within this tolerance of the true m/z.", "Lockmass Refiner");
            setToolTip(this.ZeroSamplesMSLevelLow, "Lowest MS level for scans to be treated with this filter.", "Zero Samples");
            setToolTip(this.ZeroSamplesMSLevelHigh, "Highest MS level for scans to be treated with this filter (may be left blank).", "Zero Samples");
            setToolTip(this.ZeroSamplesMSLevelLabel, "Perform this filter only on scans with these MS Levels.", "Zero Samples");
            setToolTip(this.ZeroSamplesRemove, "Reduces output file sizes by removing zero values which are not adjacent to nonzero values.", "Zero Samples");
            setToolTip(this.ZeroSamplesPanel, "These filters help with missing or unwanted zero value samples.", "Zero Samples");
            setToolTip(this.PeakMSLevelLow, "Lowest MS level on which to perform peak picking.", "Peak Picking");
            setToolTip(this.PeakMSLevelHigh, "Highest MS level on which to perform peak picking (may be left blank).", "Peak Picking");
            setToolTip(this.PeakMSLevelLabel, "Selects the MS levels for scans on which to perform peak picking.", "Peak Picking");
            setToolTip(this.MSLevelsLabel, "Use this filter to include only scans within a range of MS levels.", "Subset");
            setToolTip(this.MSLevelLow, "Lowest MS level for scans to be included in the conversion.", "Subset");
            setToolTip(this.MSLevelHigh, "Highest MS level to be included in the conversion (may be left blank).", "Subset");
            setToolTip(this.ChargeStatesLabel, "Use this filter to include only MSn precursors within a range of charge states or possible charge states.", "Subset");
            setToolTip(this.ChargeStatesLow, "Lowest charge state (or possible charge state) for scans to be included in the conversion.", "Subset");
            setToolTip(this.ChargeStatesHigh, "Highest charge state (or possible charge state) for scans to be included in the conversion (may be left blank).", "Subset");
            setToolTip(this.DefaultArrayLengthLabel, "Use this filter to exclude scans with too few or too many data points (or peaks for centroid scans).", "Subset");
            setToolTip(this.DefaultArrayLengthLow, "Lowest number of data points (or peaks for centroid scans) for scans to be included in the conversion.", "Subset");
            setToolTip(this.DefaultArrayLengthHigh, "Highest number of data points (or peaks for centroid scans) for scans to be included in the conversion (may be left blank).", "Subset");
            setToolTip(this.ScanNumberLabel, "Use this filter to include only scans with a limited range of scan numbers.", "Subset");
            setToolTip(this.ScanNumberHigh, "Highest scan number to include in the conversion (may be left blank).", "Subset");
            setToolTip(this.ScanNumberLow, "Lowest scan number to include in the conversion.", "Subset");
            setToolTip(this.ScanTimeLabel, "Use this filter to include only scans with a limited range of scan times.", "Subset");
            setToolTip(this.ScanTimeHigh, "Highest scan time to include in the conversion.", "Subset");
            setToolTip(this.ScanTimeLow, "Lowest scan time to include in the conversion.", "Subset");
            setToolTip(this.ScanEventLabel, "Use this filter to include only scans generated by a range of scan events.", "Subset");
            setToolTip(this.ScanEventHigh, "Highest scan event to include in the conversion (may be left blank).", "Subset");
            setToolTip(this.ScanEventLow, "Lowest scan event to include in the conversion.", "Subset");
            setToolTip(this.CollisionEnergyLabel, "Use this filter to include only scans activated within the specified collision energy range.", "Subset");
            setToolTip(this.CollisionEnergyHigh, "Highest collision energy to include in the conversion.", "Subset");
            setToolTip(this.CollisionEnergyLow, "Lowest collision energy to include in the conversion.", "Subset");
            setToolTip(this.mzWinLabel, "Use this filter to include only scans with a limited range of m/z values.", "Subset");
            setToolTip(this.mzWinHigh, "Highest m/z value to include in the conversion.", "Subset");
            setToolTip(this.mzWinLow, "Lowest m/z value to include in the conversion.", "Subset");
            setToolTip(this.SubsetPanel, "Set values for one or more subset filters, then click Add.", "Subset");
            setToolTip(this.FilterBox, "This chooses the type of filter that you want to add next.");
            setToolTip(this.AboutButton, "Provides information on using MSConvertGUI.");
            setToolTip(this.FilterGB, "Use these controls to add to the conversion filter list.  The order of the filter list is significant.  In particular, vendor-supplied peakPicking must be first since it only works on raw, untransformed data.");
            setToolTip(this.StartButton, "Click here to begin the conversion process.");
            setToolTip(this.RemoveFileButton, "Select a file to be removed from the conversion list, then click here.");
            setToolTip(this.FileListBox, "Add files to this conversion list by using the Browse button to select a file, then clicking the Add button.");
            setToolTip(this.FileBox, "Use the Browse button or type a filename here, then click Add to add it to the list of files to be converted.");
            setToolTip(this.AddFileButton, "Adds the current file to the conversion list. You can drag the rows to reorder them.");
            setToolTip(this.FilterDGV, "Use the controls above to add conversion filters. The order can be significant. You can drag the rows to reorder them.");
            setToolTip(this.MakeTPPCompatibleOutputButton, "Check this to use TPP-compatible output settings, e.g. an MGF TITLE format like <basename>.<scan>.<scan>.<charge>.");
            MSDataFile.WriteConfig mwc = new MSDataFile.WriteConfig(); // for obtaining default numpress tolerance
            setToolTip(this.NumpressLinearBox, String.Format("Check this to use numpress linear prediction lossy compression for binary mz and rt data in mzML output (relative accuracy loss will not exceed {0}).  Note that not all mzML readers recognize this format.", mwc.numpressLinearErrorTolerance));
            setToolTip(this.NumpressSlofBox, String.Format("Check this to use numpress short logged float lossy compression for binary intensities in mzML output (relative accuracy loss will not exceed  {0}).  Note that not all mzML readers recognize this format.", mwc.numpressSlofErrorTolerance));
            setToolTip(this.NumpressPicBox, "Check this to use numpress positive integer lossy compression for binary intensities in mzML output (absolute accuracy loss will not exceed 0.5).  Note that not all mzML readers recognize this format.");
            setToolTip(this.CombineIonMobilitySpectraBox, "Check this to collapse the ion mobility dimension by combining mobility spectra together. When combining Bruker TDF data in the mzML format, the mobility of each scan is preserved in a new mobility binary data array. For PASEF data, the MS2s are combined on a per-precursor basis rather than per-frame.");
            setToolTip(this.SimSpectraBox, "Check this to request that SIM mode data be represented as spectra instead of chromatograms. Not all vendor formats support this mode.");
            setToolTip(this.SrmSpectraBox, "Check this to request that SRM mode data be represented as spectra instead of chromatograms. Not all vendor formats support this mode.");
            setToolTip(this.ETDFilterPanel, "Use these filter options to remove unreacted and charge-reduced precursor peaks in ETD spectra.", "ETD Peak");
            setToolTip(this.ETDRemovePrecursorBox, "Check this to remove unreacted precursor peaks from ETD spectra.", "ETD Peak");
            setToolTip(this.ETDRemoveChargeReducedBox, "Check this to remove charge-reduced precursor peaks from ETD spectra.", "ETD Peak");
            setToolTip(this.ETDRemoveNeutralLossBox, "Check this to remove prominent neutral losses of the +1 charge-reduced precursor from ETD spectra.", "ETD Peak");
            setToolTip(this.ETDBlanketRemovalBox, "Check this for an alternative way of neutral loss filtering using a charge-scaled 60 Da exclusion window below the charge-reduced precursors.", "ETD Peak");

            setToolTip(this.ThresholdFilterPanel, "Use this filter to remove small noise peaks or undesirable big peaks. Several different thresholding methods are available.", "Threshold");

            string thresholdOrientation = "Controls whether the threshold filter keeps the most intense or the least intense peaks.";
            setToolTip(this.thresholdOrientationLabel, thresholdOrientation, "Threshold");
            setToolTip(this.thresholdOrientationComboBox, thresholdOrientation, "Threshold");

            string thresholdTypeHelp = "The filter can use different thresholding schemes:\r\n" +
                                       "Count types: keeps the most/least intense peaks.\r\n" +
                                       "Absolute: keeps peaks with intensities greater than the threshold.\r\n" +
                                       "Relative types: keeps a peak if its the fraction of BPI/TIC is greater than the threshold.\r\n" +
                                       "Fraction of TIC: keeps as many peaks as needed until the threshold of TIC is accounted for.";
            setToolTip(this.thresholdTypeLabel, thresholdTypeHelp, "Threshold");
            setToolTip(this.thresholdTypeComboBox, thresholdTypeHelp, "Threshold");

            string thresholdValueHelp = "The meaning of this threshold value depends on the threshold type:\r\n" +
                                        "Count types: keeps the <value> most/least intense peaks.\r\n" +
                                        "Absolute: keeps peaks with intensities greater/less than <value>.\r\n" +
                                        "Relative types: keeps a peak if its the fraction of BPI/TIC is greater/less than <value>.\r\n" +
                                        "Fraction of TIC: keeps as many peaks as needed until the fraction <value> of the TIC is accounted for.";
            setToolTip(this.thresholdValueLabel, thresholdValueHelp, "Threshold");
            setToolTip(this.thresholdValueTextBox, thresholdValueHelp, "Threshold");

            setToolTip(this.ChargeStatePredictorPanel, "Use this filter to add missing (and optionally overwrite existing) charge state information to MSn spectra.\r\n" +
                                                       "For CID spectra, the charge state is single/multiple based on %TIC below the precursor m/z.\r\n" +
                                                       "For ETD spectra, the charge state is predicted using the published ETDz SVM prediction model.", "Charge State Predictor");
            setToolTip(this.ChaOverwriteCharge, "Check this to overwrite spectra's existing charge state(s) with the predicted ones.", "Charge State Predictor");

            string chaSingleHelp = "When the %TIC below the precursor m/z is less than this value, the spectrum is predicted as singly charged.";
            setToolTip(this.ChaSingleLabel, chaSingleHelp, "Charge State Predictor");
            setToolTip(this.ChaSingleBox, chaSingleHelp, "Charge State Predictor");

            string maxChargeHelp = "Maximum multiple charge state to be predicted.";
            setToolTip(this.ChaMCMaxLabel, maxChargeHelp, "Charge State Predictor");
            setToolTip(this.ChaMCMaxBox, maxChargeHelp, "Charge State Predictor");

            string minChargeHelp = "Minimum multiple charge state to be predicted.";
            setToolTip(this.ChaMCMinLabel, minChargeHelp, "Charge State Predictor");
            setToolTip(this.ChaMCMinBox, minChargeHelp, "Charge State Predictor");

            string OutputExtensionHelp = "Sets the filename extension for the output file(s)";
            setToolTip(this.OutputExtensionLabel, OutputExtensionHelp);
            setToolTip(this.OutputExtensionBox, OutputExtensionHelp);

            string precisionHelp = "Sets output precision for writing binary m/z and intensity information. High resolution instruments should use 64-bit m/z values, but otherwise it just creates unnecessarily large output files.";
            setToolTip(this.PrecisionLabel, precisionHelp);
            setToolTip(this.Precision64, precisionHelp);
            setToolTip(this.Precision32, precisionHelp);

            string activationTypeHelp = "Include only scans with this precursor activation type.";
            setToolTip(this.ActivationTypeLabel, activationTypeHelp, "Subset");
            setToolTip(this.ActivationTypeBox, activationTypeHelp, "Subset");

            string analyzerTypeHelp = "Include only scans from the specified analyzer type.";
            setToolTip(this.AnalyzerTypeBox, analyzerTypeHelp, "Subset");
            setToolTip(this.AnalyzerTypeLabel, analyzerTypeHelp, "Subset");

            string scanPolarityHelp = "Include only scans with the specified scan polarity.";
            setToolTip(this.PolarityBox, scanPolarityHelp, "Subset");
            setToolTip(this.PolarityLabel, scanPolarityHelp, "Subset");

            string preferVendorHelp = "Choose which algorithm to use for peak picking. Normally the vendor method works better, but not all input formats support vendor peakpicking. For those formats, CWT is better.";
            setToolTip(this.PeakPickingAlgorithmComboBox, preferVendorHelp, "Peak Picking");
            setToolTip(this.PeakPickingPanel, "Use this filter to perform peak picking (centroiding) on the input data.", "Peak Picking");

            setToolTip(this.DemultiplexPanel, "Use this filter to demultiplex overlapping or MSX data.", "Demultiplex");
            string massErrorHelp = "Specify the relative (ppm) or absolute (Da) mass error of the MS2 spectra.";
            setToolTip(this.DemuxMassErrorValue, massErrorHelp);
            setToolTip(this.DemuxMassErrorTypeBox, massErrorHelp);
            setToolTip(this.DemuxMassErrorLabel, massErrorHelp);
            setToolTip(this.DemuxTypeBox, "Specify the type of multiplexing that was used to acquire the data.");

            string outputHelp = "Choose the directory for writing the converted file(s).";
            setToolTip(this.OutputLabel, outputHelp);
            setToolTip(this.OutputBox, outputHelp);

            string outputFormatHelp = "Selects the output format for the conversion";
            setToolTip(this.FormatLabel, outputFormatHelp);
            setToolTip(this.OutputFormatBox, outputFormatHelp);

            string addZerosHelp = "Adds flanking zero values next to nonzero values where needed, to help with things like smoothing.";
            setToolTip(this.ZeroSamplesAddMissing, addZerosHelp, "Zero Samples");
            setToolTip(this.ZeroSamplesAddMissingFlankCountBox, addZerosHelp, "Zero Samples");

            setToolTip(this.ScanSummingPanel, "Use this filter to combine MS2 spectra with similar precursor m/z, scan time, and ion mobility.", "Scan Summing");
            setToolTip(this.ScanSummingPrecursorToleranceTextBox, "Specify how similar two MS2 spectra's precursors must be to be considered for summing.");
            setToolTip(this.ScanSummingScanTimeToleranceTextBox, "Specify how similar two MS2 spectra's scan times must be to be considered for summing. A value of 0 specifies that a similar scan time is not required for summing.");
            setToolTip(this.ScanSummingIonMobilityToleranceTextBox, "Specify how similar two MS2 spectra's ion mobilities must be to be considered for summing. A value of 0 specifies that a similar ion mobility is not required for summing.");
            setToolTip(this.ScanSummingSumMs1Checkbox, "Specify whether to sum MS1 scans as well as MS2s. Currently the tolerances are ignored: points are only summed within MS1 spectra, not between them.");
        }

        private void AboutButtonClick(object sender, EventArgs e)
        {
            // Create a scrollable form.
            Form aboutBox = new Form();
            int w = 700, h = 400;
            aboutBox.Size = new System.Drawing.Size(w, h);
            w = aboutBox.ClientRectangle.Width;
            h = aboutBox.ClientRectangle.Height;
            // Create the accept button.
            Button buttonOK = new Button();
            // Create the scrollable help text area.
            TextBox helptext = new TextBox();
            // Set the Multiline property to true.
            helptext.Multiline = true;
            // Add vertical scroll bars to the TextBox control.
            helptext.ScrollBars = ScrollBars.Vertical;
            // Allow the Return key to be entered in the TextBox control.
            helptext.AcceptsReturn = true;
            // Allow the TAB key to be entered in the TextBox control.
            helptext.AcceptsTab = true;
            // Set WordWrap to true to allow text to wrap to the next line.
            helptext.WordWrap = true;

            helptext.Size = new System.Drawing.Size(w, h - (20 + buttonOK.Height));
            helptext.Text = this.AboutButtonHelpText + "\r\n";
            for (int i = 0; i < this.sortedToolTips.Count; i++)
            {
                helptext.Text += ("\r\n" + sortedToolTips.GetKey(i) + ":\r\n" + sortedToolTips.GetByIndex(i) + "\r\n");
            }
            buttonOK.Text = "OK";
            // Set the position of the button on the form.
            buttonOK.Location = new Point((w - buttonOK.Width) / 2, h - (10 + buttonOK.Height));
            // Make button1's dialog result OK.
            buttonOK.DialogResult = DialogResult.OK;

            // Add OK button to the form.
            aboutBox.Controls.Add(buttonOK);
            // Add helptext to the form.
            aboutBox.Controls.Add(helptext);


            // Set the caption bar text of the form.   
            aboutBox.Text = this.AboutButton.Text;

            // Define the border style of the form to a dialog box.
            aboutBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            // Set the accept button of the form to button1.
            aboutBox.AcceptButton = buttonOK;
            // Set the start position of the form to the center of the screen.
            aboutBox.StartPosition = FormStartPosition.CenterScreen;


            // Display the form as a modal dialog box.
            aboutBox.ShowDialog();

            aboutBox.Dispose();
        }

        private void NumpressPicBox_CheckedChanged(object sender, EventArgs e)
        {  // numpress Pic and numpress Slof are mutually exclusive
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressSlofBox.Checked = false;
        }

        private void NumpressSlofBox_CheckedChanged(object sender, EventArgs e)
        {  // numpress Pic and numpress Slof are mutually exclusive
            if (NumpressPicBox.Checked && NumpressSlofBox.Checked)
                NumpressPicBox.Checked = false;
        }

        private void PeakPickingAlgorithmComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            PeakMinSnr.Enabled = PeakMinSnrLabel.Enabled = PeakMinSpacing.Enabled = PeakMinSpacingLabel.Enabled = ((PeakPickingMethod)PeakPickingAlgorithmComboBox.SelectedIndex == PeakPickingMethod.Cwt);
        }

        private void presetComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetGUIfromCfg(Path.Combine(PresetFolder, (presetComboBox.SelectedItem as string) + ".cmdline"));
        }

        private void presetSetDefaultButton_Click(object sender, EventArgs e)
        {
            setCfgFromGUI(MakePresetFilename());
            SelectPreset(SetDefaultsDataType == String.Empty ? "Generic Defaults" : SetDefaultsDataType + " Defaults");
        }

        private void FilesToConvertInParallelUpDown_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.NumFilesToConvertInParallel = FilesToConvertInParallelUpDown.Value;
            Properties.Settings.Default.Save();
        }

        #region Drag 'n Drop reordering of rows
        // http://www.inforbiro.com/blog-eng/c-sharp-datagridview-drag-and-drop-rows-reorder/
        private Rectangle dragBoxFromMouseDown;
        private int rowIndexFromMouseDown;
        private int rowIndexOfItemUnderMouseToDrop;

        private void EnableDragAndDropRows(DataGridView dgv)
        {
            dgv.AllowDrop = true;

            dgv.MouseMove += (sender, e) =>
            {
                if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
                {
                    // If the mouse moves outside the rectangle, start the drag.
                    if (dragBoxFromMouseDown != Rectangle.Empty &&
                    !dragBoxFromMouseDown.Contains(e.X, e.Y))
                    {
                        // Proceed with the drag and drop, passing in the list item.                    
                        DragDropEffects dropEffect = dgv.DoDragDrop(
                              dgv.Rows[rowIndexFromMouseDown],
                              DragDropEffects.Move);
                    }
                }
            };

            dgv.MouseDown += (sender, e) =>
            {
                // Get the index of the item the mouse is below.
                rowIndexFromMouseDown = dgv.HitTest(e.X, e.Y).RowIndex;

                if (rowIndexFromMouseDown != -1)
                {
                    // Remember the point where the mouse down occurred. 
                    // The DragSize indicates the size that the mouse can move 
                    // before a drag event should be started.                
                    Size dragSize = SystemInformation.DragSize;

                    // Create a rectangle using the DragSize, with the mouse position being
                    // at the center of the rectangle.
                    dragBoxFromMouseDown = new Rectangle(
                                  new Point(
                                    e.X - (dragSize.Width / 2),
                                    e.Y - (dragSize.Height / 2)),
                              dragSize);
                }
                else
                    // Reset the rectangle if the mouse is not over an item in the ListBox.
                    dragBoxFromMouseDown = Rectangle.Empty;
            };

            dgv.DragOver += (sender, e) =>
            {
                e.Effect = DragDropEffects.Move;
            };

            dgv.DragDrop += (sender, e) =>
            {
                // The mouse locations are relative to the screen, so they must be 
                // converted to client coordinates.
                Point clientPoint = dgv.PointToClient(new Point(e.X, e.Y));

                // Get the row index of the item the mouse is below. 
                rowIndexOfItemUnderMouseToDrop = Math.Max(0, dgv.HitTest(clientPoint.X, clientPoint.Y).RowIndex);

                // If the drag operation was a move then remove and insert the row.
                if (e.Effect == DragDropEffects.Move)
                {
                    DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
                    dgv.Rows.RemoveAt(rowIndexFromMouseDown);
                    dgv.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);

                }
            };
        }
        #endregion

        private void DiaUmpireParamsFileBrowseButton_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog() {Title = "Pick DIA-Umpire Params File", CheckFileExists = true})
            {
                if (ofd.ShowDialog(this) == DialogResult.Cancel)
                    return;

                DiaUmpireParamsFileTextBox.Text = ofd.FileName;
            }
        }
    }
}
