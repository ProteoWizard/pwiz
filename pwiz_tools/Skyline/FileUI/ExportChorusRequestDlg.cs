/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportChorusRequestDlg : FormEx
    {
        private readonly string _documentFilePath;
        public ExportChorusRequestDlg(SrmDocument srmDocument, string documentFilePath)
        {
            InitializeComponent();
            _documentFilePath = documentFilePath;
            Icon = Properties.Resources.Skyline;
            Document = srmDocument;
            comboBoxReferenceFile.Items.Add(string.Empty);
            comboBoxReferenceFile.SelectedIndex = 0;
            var libraries = srmDocument.Settings.PeptideSettings.Libraries;
            if (libraries.HasLibraries)
            {
                HashSet<string> fileNameSet = new HashSet<string>();
                foreach (var library in libraries.Libraries)
                {
                    foreach (var retentionTimeSource in library.ListRetentionTimeSources())
                    {
                        fileNameSet.Add(retentionTimeSource.Name);
                    }
                }
                var fileNames = fileNameSet.ToArray<object>();
                Array.Sort(fileNames, StringComparer.CurrentCultureIgnoreCase);
                comboBoxReferenceFile.Items.AddRange(fileNames);
            }
        }

        public SrmDocument Document { get; private set; }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string strReferenceFile = (string) comboBoxReferenceFile.SelectedItem;
            string strSaveFileName = string.Empty;
            if (!string.IsNullOrEmpty(_documentFilePath))
            {
                strSaveFileName = Path.GetFileNameWithoutExtension(_documentFilePath);
            }
            if (!string.IsNullOrEmpty(strReferenceFile))
            {
                strSaveFileName += Path.GetFileNameWithoutExtension(strReferenceFile);
            }
            strSaveFileName += ".ChorusRequest.xml"; // Not L10N
            strSaveFileName = strSaveFileName.Replace(' ', '_');
            using (var saveFileDialog = new SaveFileDialog { FileName = strSaveFileName})
            {
                if (saveFileDialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }
                SpectrumFilter spectrumFilterData = new SpectrumFilter(Document, MsDataFileUri.Parse(strReferenceFile), null);
                using (var saver = new FileSaver(saveFileDialog.FileName))
                {
                    if (!saver.CanSave(this))
                    {
                        return;
                    }
                    using (var stream = new StreamWriter(saver.SafeName))
                    {
                        var xmlSerializer = new XmlSerializer(typeof(Model.Results.RemoteApi.GeneratedCode.ChromatogramRequestDocument));
                        xmlSerializer.Serialize(stream, spectrumFilterData.ToChromatogramRequestDocument());
                    }
                    saver.Commit();
                }
            }
            Close();
        }
    }
}
