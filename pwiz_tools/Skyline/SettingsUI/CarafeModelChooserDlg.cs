/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Lists the fine-tuned Carafe models in a folder, and those recently used, in a grid of what each
    /// was trained on (instrument, gradient, precursor m/z, NCE, fragmentation, held-out scores), so a
    /// user can sort and compare them to pick the one that matches the library's instrument and method.
    /// </summary>
    public partial class CarafeModelChooserDlg : FormEx
    {
        private readonly string _initialPath;

        public CarafeModelChooserDlg(string currentPath)
        {
            InitializeComponent();
            Icon = Resources.Skyline;

            _initialPath = currentPath;
            FolderPath = GetInitialFolder(currentPath);
        }

        /// <summary>
        /// The remembered model folder, else the folder of the current or most recent model, else the
        /// library folder.
        /// </summary>
        private static string GetInitialFolder(string currentPath)
        {
            var candidates = new[] { Settings.Default.CarafeModelDirectory, GetDirectoryOrNull(currentPath) }
                .Concat(Settings.Default.CarafeModelList.Select(GetDirectoryOrNull))
                .Append(Settings.Default.LibraryDirectory);
            return candidates.FirstOrDefault(folder => !string.IsNullOrEmpty(folder) && Directory.Exists(folder)) ?? string.Empty;
        }

        private static string GetDirectoryOrNull(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The folder whose models the grid lists, along with the recently used ones.</summary>
        public string FolderPath
        {
            get { return textFolder.Text; }
            set
            {
                textFolder.Text = value;
                UpdateModels();
            }
        }

        /// <summary>The models the grid lists, in the order it shows them.</summary>
        public IList<CarafeModelChoice> Models
        {
            get { return gridModels.Rows.Cast<DataGridViewRow>().Select(row => (CarafeModelChoice)row.Tag).ToList(); }
        }

        public CarafeModelChoice SelectedModel
        {
            get { return gridModels.CurrentRow is { Selected: true } row ? (CarafeModelChoice)row.Tag : null; }
        }

        /// <summary>The path of the chosen model. Setting it selects that model's row, if the grid lists it.</summary>
        public string SelectedModelPath
        {
            get { return SelectedModel?.Path; }
            set
            {
                var row = gridModels.Rows.Cast<DataGridViewRow>()
                    .FirstOrDefault(r => string.Equals(((CarafeModelChoice)r.Tag).Path, value, StringComparison.OrdinalIgnoreCase));
                if (row != null)
                {
                    gridModels.CurrentCell = row.Cells[0];
                    // Already the current cell after a ClearSelection, which setting it does not undo
                    row.Selected = true;
                }
                else
                {
                    gridModels.ClearSelection();
                }
                UpdateOk();
            }
        }

        private void UpdateModels()
        {
            string selectedPath = SelectedModelPath ?? _initialPath;
            var paths = new List<string>();
            string folder = FolderPath;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                try
                {
                    paths.AddRange(Directory.EnumerateFiles(folder, @"*" + CarafeModelFile.EXTENSION).OrderBy(path => path));
                }
                catch (Exception x) when (x is IOException || x is UnauthorizedAccessException)
                {
                    MessageDlg.ShowWithException(this, x.Message, x);
                }
            }
            paths.AddRange(Settings.Default.CarafeModelList);

            gridModels.Rows.Clear();
            foreach (var model in paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(CarafeModelChoice.Read).Where(model => model != null))
            {
                int rowIndex = gridModels.Rows.Add(model.FileName, model.Instrument, model.GradientMinutes, model.PrecursorMzRange,
                    model.Nce, model.Fragmentation, model.TrainingRunCount, model.Ms2Cosine, model.RtR2, model.Saved);
                var row = gridModels.Rows[rowIndex];
                row.Tag = model;
                row.Cells[colFileName.Index].ToolTipText = model.Path;
            }
            SelectedModelPath = selectedPath;
        }

        private void btnFolder_Click(object sender, EventArgs e)
        {
            ChooseFolder();
        }

        /// <summary>
        /// Shows a folder browser for the folder of models to list, and lists it.
        /// </summary>
        public void ChooseFolder()
        {
            using var dlg = FormUtil.CreateFolderBrowserDialog();
            dlg.Description = SettingsUIResources.CarafeModelChooserDlg_ChooseFolder_Choose_a_folder_of_fine_tuned_Carafe_models;
            dlg.ShowNewFolderButton = false;
            dlg.SelectedPath = FolderPath;
            if (dlg.ShowDialog(this) == DialogResult.OK)
                FolderPath = dlg.SelectedPath;
        }

        private void gridModels_SelectionChanged(object sender, EventArgs e)
        {
            UpdateOk();
        }

        private void UpdateOk()
        {
            btnOk.Enabled = SelectedModel != null;
        }

        private void gridModels_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                OkDialog();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (SelectedModel == null)
                return;
            Settings.Default.CarafeModelDirectory = FolderPath;
            DialogResult = DialogResult.OK;
        }
    }
}
