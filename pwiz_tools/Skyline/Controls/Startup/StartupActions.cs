/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
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

using System.Windows.Forms;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Controls.Startup
{
    public delegate bool StartupAction(SkylineWindow skylineWindow);

    public class ActionImport
    {
        public enum DataType
        {
            peptide_search_dda,
            peptide_search_prm,
            peptide_search_dia,
            fasta,
            transition_list,
            proteins,
            peptides
        }

        public DataType ImportType { get; set; }
        public string FilePath { get; set; }

        public ActionImport(DataType action)
        {
            ImportType = action;
        }


        public bool DoStartupAction(SkylineWindow skylineWindow)
        {
            if (skylineWindow.Visible)
            {
                OpenSkylineStartupSettingsUI(skylineWindow);
            }
            else
            {
                skylineWindow.Shown += (sender, eventArgs) => OpenSkylineStartupSettingsUI(skylineWindow);
            }
            return true;
        }

        private void OpenSkylineStartupSettingsUI(SkylineWindow skylineWindow)
        {
            ImportPeptideSearchDlg.Workflow? importPeptideSearchType = null;
            switch (ImportType)
            {
                case DataType.peptide_search_dda:
                    importPeptideSearchType = ImportPeptideSearchDlg.Workflow.dda;
                    break;
                case DataType.peptide_search_prm:
                    importPeptideSearchType = ImportPeptideSearchDlg.Workflow.prm;
                    break;
                case DataType.peptide_search_dia:
                    importPeptideSearchType = ImportPeptideSearchDlg.Workflow.dia;
                    break;
            }
            if (importPeptideSearchType.HasValue)
            {
                if (FilePath != null)
                    skylineWindow.LoadFile(FilePath);
                skylineWindow.ResetDefaultSettings();
                skylineWindow.ShowImportPeptideSearchDlg(importPeptideSearchType.Value);
                return;
            }

            using (var settingsUI = new StartPageSettingsUI(skylineWindow))
            {
                if (settingsUI.ShowDialog(skylineWindow) == DialogResult.OK)
                {
                    skylineWindow.SetIntegrateAll(settingsUI.IsIntegrateAll);

                    switch (ImportType)
                    {
                        case DataType.fasta:
                            skylineWindow.OpenPasteFileDlg(PasteFormat.fasta);
                            break;
                        case DataType.peptides:
                            skylineWindow.OpenPasteFileDlg(PasteFormat.peptide_list);
                            break;
                        case DataType.proteins:
                            skylineWindow.OpenPasteFileDlg(PasteFormat.protein_list);
                            break;
                        case DataType.transition_list:
                            skylineWindow.OpenPasteFileDlg(PasteFormat.transition_list);
                            break;
                    }
                }
            }
        }
    }
}