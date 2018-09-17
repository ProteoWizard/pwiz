/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.SettingsUI
{
    public partial class FilterMidasLibraryDlg : FormEx
    {
        public FilterMidasLibraryDlg(string docPath, MidasLibSpec libSpec, IEnumerable<LibrarySpec> libSpecs)
        {
            InitializeComponent();

            _libSpecs = libSpecs.ToArray();

            if (libSpec != null)
            {
                LibraryName = Helpers.GetUniqueName(
                    libSpec.Name.Insert(libSpec.Name.StartsWith(MidasLibSpec.PREFIX) ? MidasLibSpec.PREFIX.Length : 0, @"Filter_"),
                    _libSpecs.Select(lib => lib.Name).ToArray());
                if (!string.IsNullOrEmpty(docPath))
                    // ReSharper disable once AssignNullToNotNullAttribute
                    FileName = Path.Combine(Path.GetDirectoryName(docPath), Path.ChangeExtension(Path.GetFileName(libSpec.FilePath), @".midas.blib"));
            }
        }

        private readonly LibrarySpec[] _libSpecs;

        public string LibraryName
        {
            get { return txtName.Text; }
            private set { txtName.Text = value; }
        }
        public string FileName
        {
            get { return txtPath.Text; }
            private set { txtPath.Text = value; }
        }

        private void btnOk_Click(object sender, System.EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (string.IsNullOrEmpty(LibraryName))
            {
                MessageDlg.Show(this, Resources.FilterMidasLibraryDlg_OkDialog_You_must_enter_a_name_for_the_filtered_library_);
                return;
            }
            else if (_libSpecs.Any(libSpec => txtName.Text.Equals(libSpec.Name)))
            {
                MessageDlg.Show(this, Resources.FilterMidasLibraryDlg_OkDialog_A_library_with_this_name_already_exists_);
                return;
            }
            else if (string.IsNullOrEmpty(FileName))
            {
                MessageDlg.Show(this, Resources.FilterMidasLibraryDlg_OkDialog_You_must_enter_a_path_for_the_filtered_library_);
            }

            DialogResult = DialogResult.OK;
        }

        private void btnBrowse_Click(object sender, System.EventArgs e)
        {
            using (var saveDlg = new SaveFileDialog
            {
                Title = Resources.FilterMidasLibraryDlg_btnBrowse_Click_Export_Filtered_MIDAS_Library,
                OverwritePrompt = true,
                DefaultExt = BiblioSpecLiteSpec.EXT,
                Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB)
            })
            {
                if (saveDlg.ShowDialog(this) == DialogResult.OK)
                {
                    FileName = saveDlg.FileName;
                }
            }
        }
    }
}
