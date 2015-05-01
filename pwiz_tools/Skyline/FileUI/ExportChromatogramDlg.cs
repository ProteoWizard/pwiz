/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportChromatogramDlg : FormEx
    {
        public const string EXT = ".tsv"; // Not L10N

        private SrmDocument Document { get; set; }
        private string DocumentFilePath { get; set; }

        private readonly List<ChromExtractor> _chromExtractors;
        private readonly List<ChromSource> _chromSources; 

        public ExportChromatogramDlg(SrmDocument document, string documentPath)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            Document = document;
            DocumentFilePath = documentPath;
            MakeCheckedList();
            _chromExtractors = new List<ChromExtractor>();
            _chromSources = new List<ChromSource>();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (_chromExtractors.Count == 0 && _chromSources.Count == 0)
            {
                MessageDlg.Show(this, Resources.ExportChromatogramDlg_OkDialog_At_least_one_chromatogram_type_must_be_selected);
                return;
            }
            if (checkedListVars.CheckedItems.Count == 0)
            {
                MessageDlg.Show(this, Resources.ExportChromatogramDlg_OkDialog_At_least_one_file_must_be_selected);
                return;
            }
            using (var dlg = new SaveFileDialog
                {
                    Title = Resources.ExportChromatogramDlg_OkDialog_Export_Chromatogram,
                    OverwritePrompt = true,
                    DefaultExt = EXT,
                    Filter = TextUtil.FileDialogFilterAll(Resources.ExportChromatogramDlg_OkDialog_Chromatogram_Export_Files, EXT),
                })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                    dlg.FileName = Path.GetFileNameWithoutExtension(DocumentFilePath) + EXT;
                }
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;
                WriteChromatograms(dlg.FileName);
            }

            DialogResult = DialogResult.OK;
        }

        public bool WriteChromatograms(string filePath)
        {
            var fileNames = (from object fileName in checkedListVars.CheckedItems select fileName.ToString()).ToList();

            using (var longWaitDlg = new LongWaitDlg
            {
                Text = Resources.ExportChromatogramDlg_OkDialog_Exporting_Chromatograms,
            })
            {
                try
                {
                    longWaitDlg.PerformWork(this, 1000,
                                            broker => WriteChromatograms(filePath,
                                                                         broker,
                                                                         fileNames,
                                                                         LocalizationHelper.CurrentCulture,
                                                                         _chromExtractors,
                                                                         _chromSources));
                    if (longWaitDlg.IsCanceled)
                        return false;
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.ExportChromatogramDlg_OkDialog_Failed_attempting_to_save_chromatograms_to__0__, filePath),
                                                        x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
            }
            return true;
        }

        public void  WriteChromatograms(string filePath, 
                                        IProgressMonitor progressMonitor, 
                                        IList<string> filesToExport,
                                        CultureInfo cultureInfo,
                                        IList<ChromExtractor> chromExtractors,
                                        IList<ChromSource> chromSources)
        {
            var chromExporter = new ChromatogramExporter(Document);
            using (var saver = new FileSaver(filePath))
            using (var writer = new StreamWriter(saver.SafeName))
            {
                chromExporter.Export(writer, progressMonitor, filesToExport, cultureInfo, chromExtractors, chromSources);
                writer.Close();
                saver.Commit();
            }
        }

        private void MakeCheckedList()
        {
            var measuredResults = Document.Settings.MeasuredResults;
            int i = 0;
            foreach (var filePath in measuredResults.MSDataFilePaths)
            {
                checkedListVars.Items.Insert(i, filePath.GetFileName());
                ++i;
            }
        }

        private void checkAll_clicked(object sender, EventArgs e)
        {
            UpdateCheckedAll(boxCheckAll.Checked);
        }

        public void UpdateCheckedAll(bool isAllChecked)
        {
            for (int i = 0; i < checkedListVars.Items.Count; ++ i)
            {
                checkedListVars.SetItemChecked(i, isAllChecked);
            }
        }

        private void checkBoxPrecursors_CheckedChanged(object sender, EventArgs e)
        {
            UpdateChromSources(checkBoxPrecursors.Checked, checkBoxProducts.Checked);
        }

        private void checkBoxProducts_CheckedChanged(object sender, EventArgs e)
        {
            UpdateChromSources(checkBoxPrecursors.Checked, checkBoxProducts.Checked);
        }

        private void checkBoxBasePeak_CheckedChanged(object sender, EventArgs e)
        {
            UpdateChromExtractors(checkBoxTic.Checked, checkBoxBasePeak.Checked);
        }

        private void checkBoxTic_CheckedChanged(object sender, EventArgs e)
        {
            UpdateChromExtractors(checkBoxTic.Checked, checkBoxBasePeak.Checked);
        }

        public void UpdateChromExtractors(bool ticChecked, bool basePeakChecked)
        {
            _chromExtractors.Clear();
            if (ticChecked)
                _chromExtractors.Add(ChromExtractor.summed);
            if (basePeakChecked)
                _chromExtractors.Add(ChromExtractor.base_peak);
        }

        public void UpdateChromSources(bool precursorsChecked, bool productsChecked)
        {
            _chromSources.Clear();
            if (precursorsChecked)
                _chromSources.Add(ChromSource.ms1);
            if (productsChecked)
                _chromSources.Add(ChromSource.fragment);
        }

    }
}
