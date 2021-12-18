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

using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportTransitionListErrorDlg : FormEx
    {
        public ImportTransitionListErrorDlg(List<TransitionImportErrorInfo> errorList, bool isErrorAll, bool offerCancelButton)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            SimpleGridViewDriver<TransitionImportErrorInfo> compareGridViewDriver = new ImportErrorGridViewDriver(
                dataGridViewErrors,
                bindingSourceGrid, new SortableBindingList<TransitionImportErrorInfo>());
            ErrorList = errorList;
            foreach (var error in errorList)
            {
                compareGridViewDriver.Items.Add(error);
            }

            // If all of the transitions were errors, canceling and accepting are the same
            // so give a different message and disable the cancel button
            string errorListMessage;
            if (isErrorAll)
            {
                errorListMessage = errorList.Count == 1 ? Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_The_imported_transition_contains_an_error__Please_check_the_transition_list_and_the_Skyline_settings_and_try_importing_again_ :
                    string.Format(Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_All__0__transitions_contained_errors___Please_check_the_transition_list_for_errors_and_try_importing_again_, errorList.Count);
                buttonCancel.Visible = false;
                // In this case, the OK button should close the error dialog but not the column select dialog
                // Simplest way to do this is to treat it as a cancel button
                buttonOk.DialogResult = DialogResult.Cancel;
            }
            else if (offerCancelButton)
            {
                errorListMessage = errorList.Count == 1 ? Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_A_transition_contained_an_error__Skip_this_transition_and_import_the_rest_ :
                    string.Format(Resources.SkylineWindow_ImportMassList__0__transitions_contained_errors__Skip_these__0__transitions_and_import_the_rest_, errorList.Count);
            }
            else
            {
                errorListMessage = errorList.Count == 1 ? Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_A_transition_contained_an_error_ :
                    string.Format(Resources.SkylineWindow_ImportMassList__0__transitions_contained_errors_, errorList.Count);
                buttonCancel.Visible = false;
            }

            labelErrors.Text = errorListMessage;
        }

        public List<TransitionImportErrorInfo> ErrorList { get; private set; }

        private class ImportErrorGridViewDriver : SimpleGridViewDriver<TransitionImportErrorInfo>
        {
            public ImportErrorGridViewDriver(DataGridViewEx gridView,
                BindingSource bindingSource,
                SortableBindingList<TransitionImportErrorInfo> items)
                : base(gridView, bindingSource, items)
            {
            }

            protected override void DoPaste()
            {
                // No pasting.
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void cbShowText_CheckedChanged(object sender, System.EventArgs e)
        {
            LineText.Visible = cbShowText.Checked;
        }
    }
}
