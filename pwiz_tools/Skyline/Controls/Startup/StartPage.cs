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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Startup
{
    public partial class StartPage : FormEx, IMultipleViewProvider
    {
        // ReSharper disable InconsistentNaming
        public enum TABS { Wizard, Tutorial }
        // ReSharper restore InconsistentNaming

        public class WizardTab : IFormView { }
        public class TutorialTab : IFormView { }

        private static readonly IFormView[] TAB_PAGES =
        {
            new WizardTab(), new TutorialTab()
        };

        public const string EXT_TUTORIAL_FILES = ".zip";    // Not L10N

        public static readonly Color _darkBackground = Color.FromArgb(67, 122, 197); // Background of left userControl & items included.
        public static readonly Color _darkHoverColor = Color.FromArgb(144, 176, 220); // Hover color for Blank Doc action box item.
        public static readonly Color _darkestHoverColor = Color.FromArgb(25, 85, 157); // Hover color for items on left userControl.

        private readonly TaskbarProgress _taskbarProgress = new TaskbarProgress();

        // Double buffer to reduce fo rm-resize flicker.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }

        public StartPage()
        {
            Point location = Settings.Default.StartPageLocation;
            Size size = Settings.Default.StartPageSize;
            bool maximize = Settings.Default.StartPageMaximized;

            InitializeComponent();

            checkBoxShowStartup.Checked = Settings.Default.ShowStartupForm;
            // Get placement values before changing anything.

            // Restore window placement.
            if (!size.IsEmpty)
                Size = size;
            if (!location.IsEmpty)
            {
                StartPosition = FormStartPosition.Manual;

                Location = location;
                ForceOnScreen();
            }
            else if (Owner == null)
            {
                CenterToScreen();
            }
            else
            {
                CenterToParent();
            }
            if (maximize)
                WindowState = FormWindowState.Maximized;
            
            PopulateLeftPanel();
            PopulateWizardPanel();
            PopulateTutorialPanel();
        }

        public StartupAction Action { get; private set; }
       
        private void PopulateLeftPanel()
        {
            List<string> mruList = Settings.Default.MruList; // New MRU Length is 20, for file menu is only 4.
            var distanceFromTop = 0;
            foreach (var mru in mruList)
            {
                string filePath = mru;
                var recentFileControl = new RecentFileControl
                {
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Width = recentFilesPanel.Width - 20,
                    Top = distanceFromTop,
                    EventAction = () => DoAction(skylineWindow=>skylineWindow.LoadFile(filePath, this))
                };
                toolTip.SetToolTip(recentFileControl, filePath);
                foreach (Control control in recentFileControl.Controls)
                {
                    toolTip.SetToolTip(control, filePath);
                }
                recentFilesPanel.Controls.Add(recentFileControl);
                distanceFromTop += recentFileControl.Height + 3; // 3 is padding between each RecentFileControl.
            }
        }

        private void PopulateWizardPanel()
        {
            // Will display in same order as boxPanels list.
            var wizardBoxPanels = new[]
            {
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Blank_Document,
                    Icon = Resources.WizardBlankDocument,
                    EventAction = () => DoAction(skylineWindow => true),
                    BackColor = _darkHoverColor,
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_DDA_Peptide_Search,
                    Icon = Resources.WizardPeptideSearchDDA,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_dda),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_DDA_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_PopulateWizardPanel_Import_DIA_DDA_Peptide_Search,
                    Icon = Resources.WizardPeptideSearchDIA,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_dia),
                    Description =
                        Resources.StartPage_PopulateWizardPanel_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_DIA_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_PopulateWizardPanel_Import_PRM_Peptide_Search,
                    Icon = Resources.WizardPeptideSearchPRM,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_prm),
                    Description =
                        Resources.StartPage_PopulateWizardPanel_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_PRM_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_FASTA,
                    Icon = Resources.WizardFasta,
                    EventAction = () => Import(ActionImport.DataType.fasta),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_with_target_proteins_specified_in_FASTA_format_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_Protein_List,
                    Icon = Resources.WizardImportProteins,
                    EventAction = () => Import(ActionImport.DataType.proteins),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_with_target_proteins_specified_in_a_tabular_list_you_can_paste_into_a_grid_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_Peptide_List,
                    Icon = Resources.WizardImportPeptide,
                    EventAction = () => Import(ActionImport.DataType.peptides),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_with_targets_specified_as_a_list_of_peptide_sequences_in_a_tabular_list_you_can_paste_into_a_grid_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_Transition_List,
                    Icon = Resources.WizardImportTransition,
                    EventAction = () => Import(ActionImport.DataType.transition_list),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_from_a_complete_transition_list_with_peptide_sequences__precursor_m_z_values__and_product_m_z_values__which_you_can_paste_into_a_grid_
                }
            };
            foreach (var box in wizardBoxPanels)
            {
                flowLayoutPanelWizard.Controls.Add(box);
            }
        }

        private void PopulateTutorialPanel()
        {
            var tutorialBoxPanels = new[]
            {
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Targeted_Method_Editing,
                    Icon = Resources.MethodEdit_thumb,
                    EventAction = () => Tutorial(
                        ActionTutorial.TutorialType.targeted_method_editing,
                        TutorialLinkResources.MethodEdit_zip,
                        TutorialLinkResources.MethodEdit_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_getBoxPanels_methodedit
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Targeted_Method_Refinement, 
                    Icon = Resources.MethodRefine_thumb,
                    EventAction = () => Tutorial(
                        ActionTutorial.TutorialType.targeted_method_refinement, 
                        TutorialLinkResources.MethodRefine_zip,
                        TutorialLinkResources.MethodRefine_pdf,
                        TutorialLinkResources.MethodRefine_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_
                },
                new ActionBoxControl
                {
                    Caption = TutorialLinkResources.StartPage_getBoxPanels_Grouped_Study_Data_Processing, 
                    Icon = Resources.GroupedStudies_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.grouped_study_data_processing, 
                        TutorialLinkResources.GroupedStudy_zip,
                        TutorialLinkResources.GroupedStudy_pdf,
                        TutorialLinkResources.GroupedStudy_sky
                        ),
                    Description = Resources.StartPage_GroupedStudy_Description
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Existing___Quantitative_Experiments, 
                    Icon = Resources.ExistingQuant_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.existing_and_quantitative_experiments, 
                        TutorialLinkResources.ExistingQuant_zip,
                        TutorialLinkResources.ExistingQuant_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_getBoxPanels_Get_hands_on_experience_working_with_quantitative_experiments_and_isotope_labeled_reference_peptides__by_starting_with_experiments_with_published_transition_lists_and_SRM_mass_spectrometer_data__Learn_effective_ways_of_analyzing_your_data_in_Skyline_using_several_of_the_available_peak_area_and_retention_time_summary_charts_,
                },
                new ActionBoxControl
                {
                    Caption = TutorialLinkResources.StartPage_getBoxPanels_Small_Molecule_Targets, 
                    Icon = Resources.SmallMolecule_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.small_molecule_targets, 
                        TutorialLinkResources.SmallMolecule_zip,
                        TutorialLinkResources.SmallMolecule_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_SmallMolecule_Description
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_MS1_Full_Scan_Filtering, 
                    Icon = Resources.MS1Filtering_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.ms1_fullscan_filtering, 
                        TutorialLinkResources.MS1Filtering_zip,
                        TutorialLinkResources.MS1Filtering_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_getBoxPanels_ms1filtering
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Targeted_MS_MS__PRM_, 
                    Icon = Resources.TargetedMSMS_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.targeted_ms_ms, 
                        TutorialLinkResources.TargetedMSMS_zip,
                        TutorialLinkResources.TargetedMSMS_pdf,
                        TutorialLinkResources.TargetedMSMS_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_targetedmsms
                },
                new ActionBoxControl
                {
                    Caption = TutorialLinkResources.StartPage_getBoxPanels_Data_Independent_Acquisition, 
                    Icon = Resources.DIA_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.data_independent_acquisition, 
                        TutorialLinkResources.DIA_zip,
                        TutorialLinkResources.DIA_pdf,
                        TutorialLinkResources.DIA_sky
                        ),
                    Description = Resources.StartPage_DIA_Description
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Absolute_Quantification, 
                    Icon = Resources.AbsoluteQuant_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.absolute_quantifiaction, 
                        TutorialLinkResources.AbsoluteQuant_zip,
                        TutorialLinkResources.AbsoluteQuant_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_getBoxPanels_Get_hands_on_experience_using_Skyline_with_Excel_to_estimate_the_absolute_molecular_quantities_of_peptides_in_your_experiments__,
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Custom_Reports___Results_Grid, 
                    Icon = Resources.CustomReports_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.custom_reports_results_grid, 
                        TutorialLinkResources.CustomReports_zip,
                        TutorialLinkResources.CustomReports_pdf,
                        TutorialLinkResources.CustomReports_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_customreports
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Advanced_Peak_Picking_Models, 
                    Icon = Resources.PeakPicking_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.advanced_peak_picking_models, 
                        TutorialLinkResources.PeakPicking_zip,
                        TutorialLinkResources.PeakPicking_pdf,
                        TutorialLinkResources.PeakPicking_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_peakpicking
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_iRT_Retention_Time_Prediction, 
                    Icon = Resources.iRT_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.irt_retention_time_prediction, 
                        TutorialLinkResources.iRT_zip,
                        TutorialLinkResources.iRT_pdf,
                        TutorialLinkResources.iRT_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_irt
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Collision_Energy_Optimization, 
                    Icon = Resources.OptimizeCE_thumb, 
                    EventAction = ()=>Tutorial(ActionTutorial.TutorialType.collision_energy_optimization, 
                        TutorialLinkResources.OptimizeCE_zip,
                        TutorialLinkResources.OptimizeCE_pdf,
                        TutorialLinkResources.OptimizeCE_sky
                        ),
                    Description = Resources.StartPage_getBoxPanels_Get_hands_on_experience_using_Skyline_to_work_with_empirically_measured_optimal_collision_energy__CE__values__In_this_tutorial__you_will_create_scheduled_CE_optimization_transitions_lists_for_a_document_with_30_peptide_precursors__Using_supplied_RAW_files_from_a_Thermo_TSQ_Vantage__you_will_recalculate_the_linear_equation_used_to_calculate_CE_for_that_instrument__You_will_also_export_a_transition_list_with_CE_values_optimized_separately_for_each_transition_
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_getBoxPanels_Spectral_Library_Explorer, 
                    Icon = Resources.LibraryExplorer_thumb, 
                    EventAction = ()=>Tutorial(
                        ActionTutorial.TutorialType.spectral_library_explorer, 
                        TutorialLinkResources.LibraryExplorer_zip,
                        TutorialLinkResources.LibraryExplorer_pdf,
                        string.Empty
                        ),
                    Description = Resources.StartPage_getBoxPanels_Get_hands_on_experience_working_with_the_Skyline_Spectral_Library_Explorer__new_in_v0_7__Learn_more_about_working_with_isotope_labels_and_product_ion_neutral_losses_using_MS_MS_spectral_libraries_containing_15N_labeled_and_phosphorylated_peptides__Use_the_Library_Explorer_to_accelerate_the_transition_between_shotgun_discovery_experiments_and_targeted_investigation_
                }
            };
            foreach (var box in tutorialBoxPanels)
            {
                flowLayoutPanelTutorials.Controls.Add(box);
            }
        }

        // MouseHover and MouseLeave Actions
        private void wizardTab_MouseHover(object sender, EventArgs e)
        {
            flowLayoutPanelWizard.Focus();
        }

        private void tutorialTab_MouseHover(object sender, EventArgs e)
        {
            flowLayoutPanelTutorials.Focus();
        }

        private void openFile_MouseHover(object sender, EventArgs e)
        {
            Cursor = Cursors.Hand;
            openFilePanel.BackColor = _darkestHoverColor;
        }

        private void openFile_MouseLeave(object sender, EventArgs e)
        {
            Cursor = Cursors.Default;
            openFilePanel.BackColor = Color.Transparent;
        }

        // Click Actions
        public void OpenRecentFile(String path)
        {
            DoAction(skylineWindow => skylineWindow.LoadFile(path, this));
        }
        private void openFile_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            OpenFile(null);
        }

        public void OpenFile(string path)
        {
            var fileToOpen = path;
            if (fileToOpen == null)
            {
                fileToOpen = OpenFileDlg();
                if (fileToOpen == null)
                    return;
            }

            DialogResult = DialogResult.OK;

            DoAction(skylineWindow => skylineWindow.LoadFile(fileToOpen, this));
        }

        private void Import(ActionImport.DataType type)
        {
            DoAction(new ActionImport(type).DoStartupAction);
        }
        
        private void Tutorial(ActionTutorial.TutorialType type, string skyFileLocation, string pdfFileLocation, string zipSkyFileLocation)
        {
            Assume.IsNotNull(skyFileLocation);

            var pathChooserDlg = new PathChooserDlg(Resources.StartPage_Tutorial__Folder_for_tutorial_files_, skyFileLocation);
            if (pathChooserDlg.ShowDialog(this) != DialogResult.OK)
                return;

            DialogResult = DialogResult.OK;

            string paths = pathChooserDlg.ExtractionPath;
            DoAction(new ActionTutorial(type, paths, skyFileLocation, pdfFileLocation, zipSkyFileLocation)
                .DoStartupAction);
        }

        private string OpenFileDlg()
        {
            using (var openNewFileDlg = new OpenFileDialog
            {
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC_AND_SKY_ZIP, SrmDocumentSharing.FILTER_SHARING),
                FilterIndex = 1
            })
            {
                if (openNewFileDlg.ShowDialog() != DialogResult.OK)
                    return null;

                var fileToOpen = openNewFileDlg.InitialDirectory + openNewFileDlg.FileName;
                DialogResult = DialogResult.OK;
                return fileToOpen;
            }
        }

        private void StartPage_Resize(object sender, EventArgs e)
        {
            // Left Panel Controls
            leftPanel.Height = Height;
            flowLayoutPanelWizard.Height = Height;
            recentFilesPanel.Height = Height - recentFilesPanel.Top - leftBottomPanel.Height*2;
            leftBottomPanel.Location = new Point(18,recentFilesPanel.Height + recentFilesPanel.Top);
            // Tab Panel Controls
            tabControlMain.Width = Width - leftPanel.Width;
            tabControlMain.Height = Height;
            flowLayoutPanelWizard.Height = Height;
            flowLayoutPanelWizard.Width = tabControlMain.Width - 40;
            // Start Page Window Settings to avoid saving minimized or maximized sizes
            if (WindowState == FormWindowState.Normal)
                Settings.Default.StartPageSize = Size;
            Settings.Default.StartPageMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        private void StartPage_Move(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Normal)
                Settings.Default.StartPageLocation = Location;
            Settings.Default.StartPageMaximized =
                (WindowState == FormWindowState.Maximized);
        }

        // Updates settings on form close.
        private void StartPage_FormClosing(object sender, FormClosedEventArgs e)
        {
            Settings.Default.ShowStartupForm = checkBoxShowStartup.Checked;
            if (DialogResult.Cancel == DialogResult)
            {
                Settings.Default.Save();
            }
        }

        // Test Methods
        public IFormView ShowingFormView
        {
            get
            {
                int selectedIndex = 0;
                Invoke(new Action(() => selectedIndex = tabControlMain.SelectedIndex));
                return TAB_PAGES[selectedIndex];
            }
        }

        public TABS SelectedTab
        {
            get { return (TABS)tabControlMain.SelectedIndex; }
            set { tabControlMain.SelectedIndex = (int)value; }
        }

        public List<RecentFileControl> GetRecentFileControls()
        {
            List<RecentFileControl> controls = new List<RecentFileControl>();
            foreach (RecentFileControl control in recentFilesPanel.Controls)
            {
                controls.Add(control);
            }
            return controls;
        }

        public List<Control> GetVisibleBoxPanels()
        {
            List<Control> controls = new List<Control>();
            foreach (Control control in flowLayoutPanelWizard.Controls)
            {
                if (control.AccessibleName.Equals("ActionBoxControl")) // Not L10N
                {
                    controls.Add(control);
                }
            }
            foreach (Control control in flowLayoutPanelTutorials.Controls)
            {
                if (control.AccessibleName.Equals("ActionBoxControl")) // Not L10N
                {
                    controls.Add(control);
                }
            }
            return controls;
        }

        public void DoAction(StartupAction action)
        {
            Action = action;

            var window = Owner as SkylineWindow;
            if (window == null)
            {
                MainWindow = new SkylineWindow();
                if (!action(MainWindow))
                {
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        public SkylineWindow MainWindow { get; private set; }

        public void UpdateTaskbarProgress(int? percentComplete)
        {
            if (!percentComplete.HasValue)
                _taskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.NoProgress);
            else
            {
                _taskbarProgress.SetState(Handle, TaskbarProgress.TaskbarStates.Normal);
                _taskbarProgress.SetValue(Handle, percentComplete.Value, 100);
            }
        }

        public void TestImportAction(ActionImport.DataType type, string filePath = null)
        {
            var action = new ActionImport(type) {FilePath = filePath};
            DoAction(action.DoStartupAction);
        }

        public void TestTutorialAction(ActionTutorial.TutorialType type)
        {
            Tutorial(
                ActionTutorial.TutorialType.spectral_library_explorer,
                TutorialLinkResources.LibraryExplorer_zip,
                TutorialLinkResources.LibraryExplorer_pdf,
                string.Empty
                );
        }  
    }
}
