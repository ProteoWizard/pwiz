/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ExportAnnotationsDlg : FormEx
    {
        private bool _inUpdate;
        public ExportAnnotationsDlg(SkylineDataSchema dataSchema)
        {
            InitializeComponent();
            DataSchema = dataSchema;
            Handlers = ImmutableList.ValueOf(ElementHandler.GetElementHandlers(dataSchema));
            _inUpdate = true;
            foreach (var handler in Handlers)
            {
                listBoxElementTypes.Items.Add(handler.Name);
            }
            SelectAll(listBoxElementTypes);
            _inUpdate = false;
            UpdateUi();
            SelectAll(listBoxAnnotations);
            SelectAll(listBoxProperties);
        }
        public SkylineDataSchema DataSchema { get; private set; }
        public ImmutableList<ElementHandler> Handlers { get; private set; }
        public SrmDocument Document { get { return DataSchema.Document; } }
        public IEnumerable<ElementHandler> SelectedHandlers
        {
            get
            {
                return listBoxElementTypes.SelectedIndices.OfType<int>().Select(i => Handlers[i]);
            }
            set
            {
                var selectedHandlers = new HashSet<string>(value.Select(handler=>handler.Name));
                for (int i = 0; i < listBoxElementTypes.Items.Count; i++)
                {
                    listBoxElementTypes.SetSelected(i, selectedHandlers.Contains(listBoxElementTypes.Items[i]));
                }
            }
        }

        public IEnumerable<string> SelectedAnnotationNames
        {
            get { return listBoxAnnotations.SelectedItems.OfType<string>(); }
            set
            {
                var selectedNames = new HashSet<string>(value);
                for (int i = 0; i < listBoxAnnotations.Items.Count; i++)
                {
                    listBoxAnnotations.SetSelected(i, selectedNames.Contains(listBoxAnnotations.Items[i]));
                }
            }
        }

        public IEnumerable<string> SelectedProperties
        {
            get { return listBoxProperties.SelectedItems.OfType<string>(); }
            set
            {
                var selectedProperties = new HashSet<string>(value);
                for (int i = 0; i < listBoxProperties.Items.Count; i++)
                {
                    listBoxProperties.SetSelected(i, selectedProperties.Contains(listBoxProperties.Items[i]));
                }
            }
        }

        public bool RemoveBlankRows
        {
            get { return cbxRemoveBlankRows.Checked; }
            set { cbxRemoveBlankRows.Checked = value;}
        }

        public ExportAnnotationSettings GetExportAnnotationSettings()
        {
            return ExportAnnotationSettings.EMPTY
                .ChangeElementTypes(SelectedHandlers.Select(handler=>handler.Name))
                .ChangeAnnotationNames(SelectedAnnotationNames)
                .ChangePropertyNames(SelectedProperties)
                .ChangeRemoveBlankRows(cbxRemoveBlankRows.Checked);
        }

        public void UpdateUi()
        {
            if (_inUpdate)
            {
                return;
            }
            try
            {
                _inUpdate = true;
                UpdateAnnotations();
                UpdateProperties();
                UpdateButtons();
            }
            finally
            {
                _inUpdate = false;
            }
        }

        private void UpdateAnnotations()
        {
            var selectedAnnotations = new HashSet<string>(SelectedAnnotationNames);
            var annotationTargets = SelectedHandlers.Aggregate(AnnotationDef.AnnotationTargetSet.EMPTY,
                (value, handler) => value.Union(handler.AnnotationTargets));
            var newAnnotations = Document.Settings.DataSettings.AnnotationDefs.Where(
                    annotationDef =>
                        annotationDef.AnnotationTargets.Intersect(annotationTargets).Any())
                .Select(annotationDef => annotationDef.Name).OrderBy(name => name).ToArray();
            if (newAnnotations.SequenceEqual(listBoxAnnotations.Items.OfType<string>()))
            {
                return;
            }
            listBoxAnnotations.Items.Clear();
            listBoxAnnotations.Items.AddRange(newAnnotations);
            for (int i = 0; i < listBoxAnnotations.Items.Count; i++)
            {
                if (selectedAnnotations.Contains(newAnnotations[i]))
                {
                    listBoxAnnotations.SelectedIndices.Add(i);
                }
                else
                {
                    listBoxAnnotations.SelectedIndices.Remove(i);
                }
            }
        }

        private void UpdateProperties()
        {
            var selectedProperties = ImmutableList.ValueOf(SelectedProperties);
            var newProperties = SelectedHandlers
                .SelectMany(handler => handler.Properties.Select(pd => pd.PropertyDescriptor.Name)).Distinct()
                .OrderBy(name => name).ToArray();
            if (newProperties.SequenceEqual(listBoxProperties.Items.OfType<string>()))
            {
                return;
            }
            listBoxProperties.Items.Clear();
            listBoxProperties.Items.AddRange(newProperties);
            for (int i = 0; i < listBoxProperties.Items.Count; i++)
            {
                if (selectedProperties.Contains(newProperties[i]))
                {
                    listBoxProperties.SelectedIndices.Add(i);
                }
                else
                {
                    listBoxProperties.SelectedIndices.Remove(i);
                }
            }
        }

        private void UpdateButtons()
        {
            btnExport.Enabled = listBoxProperties.SelectedIndices.OfType<int>().Any() ||
                                listBoxAnnotations.SelectedIndices.OfType<int>().Any();
        }

        private void checkedListBoxElementTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUi();
        }

        private void ListBoxSelectionChanged(object sender, EventArgs e)
        {
            if (_inUpdate)
            {
                return;
            }

            UpdateButtons();
        }

        private void SelectAll(ListBox checkedListBox)
        {
            for (int i = 0; i < checkedListBox.Items.Count; i++)
            {
                checkedListBox.SelectedIndices.Add(i);
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string strSaveFileName = string.Empty;
            string documentFilePath = null;
            if (null != DataSchema.SkylineWindow)
            {
                documentFilePath = DataSchema.SkylineWindow.DocumentFilePath;
            }
            if (!string.IsNullOrEmpty(documentFilePath))
            {
                strSaveFileName = Path.GetFileNameWithoutExtension(documentFilePath);
            }
            strSaveFileName += @"Annotations.csv";
            bool success;
            using (var dlg = new SaveFileDialog
            {
                FileName = strSaveFileName,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_CSV),
                InitialDirectory = Settings.Default.ExportDirectory,
                OverwritePrompt = true,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                success = ExportAnnotations(dlg.FileName);
            }
            if (success)
            {
                DialogResult = DialogResult.OK;
            }
        }

        public bool ExportAnnotations(string filename)
        {
            var settings = GetExportAnnotationSettings();
            bool success = false;
            try
            {
                var documentAnnotations = new DocumentAnnotations(Document);
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.PerformWork(this, 1000, broker =>
                    {
                        using (var fileSaver = new FileSaver(filename))
                        {
                            documentAnnotations.WriteAnnotationsToFile(broker.CancellationToken, settings, fileSaver.SafeName);
                            fileSaver.Commit();
                        }
                        success = true;
                    });
                }
            }
            catch (Exception e)
            {
                MessageDlg.ShowException(this, e);
                return false;
            }
            return success;
        }

    }
}
