/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class MProphetFeaturesDlg : FormEx
    {
        public const string EXT = ".csv";

        private readonly IPeakFeatureCalculator[] _calculators;

        private SrmDocument Document { get; set; }
        private string DocumentFilePath { get; set; }

        public MProphetFeaturesDlg(SrmDocument document, string documentPath)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            Document = document;
            DocumentFilePath = documentPath;

            _calculators = PeakFeatureCalculator.Calculators.ToArray();
            foreach (var calculator in _calculators)
                comboMainVar.Items.Add(calculator.Name);
            comboMainVar.SelectedIndex = 0;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private IPeakFeatureCalculator GetCalcFromName(string name)
        {
            return _calculators.First(c => string.Equals(c.Name, name));
        }

        public void OkDialog()
        {
            using (var dlg = new SaveFileDialog
            {
                Title = Resources.MProphetFeaturesDlg_OkDialog_Export_mProphet_Features,
                OverwritePrompt = true,
                DefaultExt = EXT,
                Filter = TextUtil.FileDialogFilterAll("mProphet Feature Files", EXT),
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                    dlg.FileName = Path.GetFileNameWithoutExtension(DocumentFilePath) + EXT;
                }
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                string mainVarName = comboMainVar.SelectedItem.ToString();
                var listCalculators = new List<IPeakFeatureCalculator> { GetCalcFromName(mainVarName) };
                foreach (var itemCalc in checkedListVars.CheckedItems)
                    listCalculators.Add(GetCalcFromName(itemCalc.ToString()));

                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.SkylineWindow_OpenSharedFile_Extracting_Files,
                })
                {
                    try
                    {
                        longWaitDlg.PerformWork(this, 1000,
                                                b => WriteFeatures(dlg.FileName,
                                                                   listCalculators,
                                                                   Document.GetPeakFeatures(listCalculators, b),
                                                                   CultureInfo.CurrentCulture));
                        if (longWaitDlg.IsCanceled)
                            return;
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format("Failed attempting to save mProphet features to {0}.", dlg.FileName),
                                                                          x.Message);
                        MessageDlg.Show(this, message);
                    }
                }
            }

            DialogResult = DialogResult.OK;
        }

        public static void WriteFeatures(string filePath, IEnumerable<IPeakFeatureCalculator> calcs,
            IEnumerable<PeakTransitionGroupFeatures> features, CultureInfo cultureInfo)
        {
            using (var fs = new FileSaver(filePath))
            using (var writer = new StreamWriter(fs.SafeName))
            {
                WriteFeatures(writer, calcs, features, cultureInfo);

                writer.Close();
                fs.Commit();
            }
        }

        public static void WriteFeatures(TextWriter writer, IEnumerable<IPeakFeatureCalculator> calcs,
            IEnumerable<PeakTransitionGroupFeatures> features, CultureInfo cultureInfo)
        {
            WriteHeaderRow(writer, calcs, cultureInfo);
            foreach (var peakTransitionGroupFeatures in features)
                WriteRow(writer, peakTransitionGroupFeatures, cultureInfo);
        }

        private static void WriteHeaderRow(TextWriter writer, IEnumerable<IPeakFeatureCalculator> calcs,
            CultureInfo cultureInfo)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);
            writer.Write("transition_group_id");
            writer.Write(separator);
            writer.Write("run_id");
            writer.Write(separator);
            writer.Write("filename");
            writer.Write(separator);
            writer.Write("RT");
            writer.Write(separator);
            writer.Write("Sequence");
            writer.Write(separator);
            writer.Write("FullPeptideName");
            writer.Write(separator);
            writer.Write("ProteinName");
            writer.Write(separator);
            writer.Write("decoy");
            bool first = true;
            foreach (var peakFeatureCalculator in calcs)
            {
                writer.Write(separator);
                writer.Write(first ? "main_var_{0}" : "var_{0}", peakFeatureCalculator.Name.Replace(" ", "_"));
                first = false;
            }
            writer.WriteLine();
        }

        private static void WriteRow(TextWriter writer,
                                     PeakTransitionGroupFeatures features,
                                     CultureInfo cultureInfo)
        {
            char separator = TextUtil.GetCsvSeparator(cultureInfo);

            foreach (var peakGroupFeatures in features.PeakGroupFeatures)
            {
                writer.Write(features.Id);
                writer.Write(separator);
                writer.Write(features.Id.Run);
                writer.Write(separator);
                writer.Write(features.Id.FilePath);
                writer.Write(separator);
                writer.Write(peakGroupFeatures.RetentionTime);
                writer.Write(separator);
                writer.Write(features.Id.NodePep.Peptide.Sequence);
                writer.Write(separator);
                writer.Write(features.Id.NodePep.ModifiedSequence);
                writer.Write(separator);
                writer.Write(features.Id.NodePepGroup.Name);
                writer.Write(separator);
                writer.Write(features.Id.NodePep.IsDecoy ? 1 : 0);

                foreach (float featureColumn in peakGroupFeatures.Features)
                {
                    writer.Write(separator);
                    if (float.IsNaN(featureColumn))
                        writer.Write(TextUtil.EXCEL_NA);
                    else
                        writer.Write(featureColumn);
                }
                writer.WriteLine();
            }
        }

        private void comboMainVar_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVarsList();
        }

        private void UpdateVarsList()
        {
            int item = 0;
            for (int i = 0; i < _calculators.Length; i++)
            {
                string name = _calculators[i].Name;
                if (i == comboMainVar.SelectedIndex)
                {
                    if (item < checkedListVars.Items.Count && string.Equals(name, checkedListVars.Items[item].ToString()))                    
                        checkedListVars.Items.RemoveAt(i);
                    continue;
                }
                if (item < checkedListVars.Items.Count)
                {
                    if (!string.Equals(name, checkedListVars.Items[item].ToString()))
                    {
                        checkedListVars.Items.Insert(item, name);
                        checkedListVars.SetItemChecked(item, true);
                    }
                }
                else
                {
                    checkedListVars.Items.Add(name);
                    checkedListVars.SetItemChecked(item, true);
                }
                item++;
            }
        }
    }
}
