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
using System.Linq;
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

        public const string EXT_TUTORIAL_FILES = ".zip";

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
            PositionButtonsModeUI();

            // Setup to manage and interact with mode selector buttons in UI
            SetModeUIToolStripButtons(toolStripButtonModeUI, true);
        }

        /// <summary>
        /// Handler for the toolbar button dropdown that allow user to switch between proteomic, small mol, or mixed UI display.
        /// </summary>
        public override void SetUIMode(SrmDocument.DOCUMENT_TYPE mode)
        {
            base.SetUIMode(mode);

            PopulateWizardPanel(); // Update wizards for new UI mode
            PopulateTutorialPanel(); // Update tutorial order for new UI mode

            GetModeUIHelper().OnLoad(this); // Reprocess any needed translations

            // Update the menu structure for this mode
            if (Program.MainWindow != null)
            {
                Program.MainWindow.SetUIMode(ModeUI);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            UpgradeManager.CheckForUpdateAsync(this);

            base.OnHandleCreated(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            EnsureUIModeSet();
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
                modeUIHandler.SetUIMode(recentFileControl, Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.invariant);
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
                    EventAction = () => DoAction(skylineWindow =>
                    {
                        skylineWindow.NewDocument(true);
                        return true;
                    }),
                    BackColor = _darkHoverColor,
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_DDA_Peptide_Search,
                    IsProteomicOnly = true, // Don't show in small molecule mode
                    Icon = Resources.WizardPeptideSearchDDA,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_dda),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_DDA_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_PopulateWizardPanel_Import_DIA_Peptide_Search,
                    IsProteomicOnly = true, // Don't show in small molecule mode
                    Icon = Resources.WizardPeptideSearchDIA,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_dia),
                    Description =
                        Resources.StartPage_PopulateWizardPanel_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_DIA_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.StartPage_PopulateWizardPanel_Import_PRM_Peptide_Search,
                    IsProteomicOnly = true, // Don't show in small molecule mode
                    Icon = Resources.WizardPeptideSearchPRM,
                    EventAction = () => Import(ActionImport.DataType.peptide_search_prm),
                    Description =
                        Resources.StartPage_PopulateWizardPanel_Use_the_Skyline_Import_Peptide_Search_wizard_to_build_a_spectral_library_from_peptide_search_results_on_PRM_data__and_then_import_the_raw_data_to_quantify_peptides_using_Skyline_MS1_Filtering_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_FASTA,
                    IsProteomicOnly = true, // Don't show in small molecule mode
                    Icon = Resources.WizardFasta,
                    EventAction = () => Import(ActionImport.DataType.fasta),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_with_target_proteins_specified_in_FASTA_format_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_Protein_List,
                    IsProteomicOnly = true, // Don't show in small molecule mode
                    Icon = Resources.WizardImportProteins,
                    EventAction = () => Import(ActionImport.DataType.proteins),
                    Description =
                        Resources.SkylineStartup_SkylineStartup_Start_a_new_Skyline_document_with_target_proteins_specified_in_a_tabular_list_you_can_paste_into_a_grid_
                },
                new ActionBoxControl
                {
                    Caption = Resources.SkylineStartup_SkylineStartup_Import_Peptide_List,
                    IsProteomicOnly = true, // Don't show in small molecule mode
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
            flowLayoutPanelWizard.Controls.Clear();
            foreach (var box in wizardBoxPanels)
            {
                if (ModeUI != SrmDocument.DOCUMENT_TYPE.small_molecules || !box.IsProteomicOnly)
                {
                    flowLayoutPanelWizard.Controls.Add(box);
                    if (box.IsProteomicOnly)
                    {
                        GetModeUIHelper().NoteModeUIInvariantComponent(box); // Call it invariant rather than proteomic so it still shows in small mol mode
                    }
                }
            }
        }

        private void PopulateTutorialPanel()
        {
            var labelFont = new Font(@"Arial", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            var labelAnchor = AnchorStyles.Left | AnchorStyles.Right;
            var labelWidth = flowLayoutPanelTutorials.ClientRectangle.Width - SystemInformation.VerticalScrollBarWidth;
            var tutorialProteomicBoxPanels = new Control[]
            {
                new Label // Section heading
                {
                    Text = TutorialTextResources.Section_Introductory,
                    Font = labelFont,
                    Anchor = labelAnchor,
                    Width = labelWidth
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.MethodEdit_Caption,
                    Icon = TutorialImageResources.MethodEdit_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.MethodEdit_zip,
                        TutorialLinkResources.MethodEdit_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.MethodEdit_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.MethodRefine_Caption,
                    Icon = TutorialImageResources.MethodRefine_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.MethodRefine_zip,
                        TutorialLinkResources.MethodRefine_pdf,
                        TutorialLinkResources.MethodRefine_sky
                    ),
                    Description = TutorialTextResources.MethodRefine_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.GroupedStudy_Caption,
                    Icon = TutorialImageResources.GroupedStudies_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.GroupedStudy_zip,
                        TutorialLinkResources.GroupedStudy_pdf,
                        TutorialLinkResources.GroupedStudy_sky
                    ),
                    Description = TutorialTextResources.GroupedStudy_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.ExistingQuant_Caption,
                    Icon = TutorialImageResources.ExistingQuant_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.ExistingQuant_zip,
                        TutorialLinkResources.ExistingQuant_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.ExistingQuant_Description,
                },
                new Label // Section heading
                {
                    Text = TutorialTextResources.Section_Full_Scan_Acquisition_Data,
                    Font = labelFont,
                    Anchor = labelAnchor,
                    Width = labelWidth
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.MS1Filtering_Caption,
                    Icon = TutorialImageResources.MS1Filtering_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.MS1Filtering_zip,
                        TutorialLinkResources.MS1Filtering_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.MS1Filtering_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.DDASearch_Caption,
                    Icon = TutorialImageResources.DDASearch_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.DDASearch_zip,
                        TutorialLinkResources.DDSearch_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.MS1Filtering_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.TargetedMSMS_Caption,
                    Icon = TutorialImageResources.TargetedMSMS_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.TargetedMSMS_zip,
                        TutorialLinkResources.TargetedMSMS_pdf,
                        TutorialLinkResources.TargetedMSMS_sky
                    ),
                    Description = TutorialTextResources.TargetedMSMS_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.DIA_Caption,
                    Icon = TutorialImageResources.DIA_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.DIA_zip,
                        TutorialLinkResources.DIA_pdf,
                        TutorialLinkResources.DIA_sky
                    ),
                    Description = TutorialTextResources.DIA_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.DIA_SWATH_Caption,
                    Icon = TutorialImageResources.DIA_SWATH_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.DIA_TTOF_zip,
                        TutorialLinkResources.DIA_TTOF_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.DIA_SWATH_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.DIA_PASEF_Caption,
                    Icon = TutorialImageResources.DIA_PASEF_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.DIA_PASEF_zip,
                        TutorialLinkResources.DIA_PASEF_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.DIA_PASEF_Description
                }
            };
            var tutorialSmallMoleculeBoxPanels = new Control[]
            {
                new Label // Section heading
                {
                    Text = TutorialTextResources.Section_Small_Molecules,
                    Font = labelFont,
                    Anchor = labelAnchor,
                    Width = labelWidth
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.SmallMolecule_Caption,
                    Icon = TutorialImageResources.SmallMolecule_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.SmallMolecule_zip,
                        TutorialLinkResources.SmallMolecule_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.SmallMolecule_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.SmallMoleculeMethodDevCEOpt_Caption,
                    Icon = TutorialImageResources.SmallMoleculeMethodDevCEOpt_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.SmallMoleculeMethodDevCEOpt_zip,
                        TutorialLinkResources.SmallMoleculeMethodDevCEOpt_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.SmallMoleculeMethodDevCEOpt_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.SmallMoleculeQuantification_Caption,
                    Icon = TutorialImageResources.SmallMoleculeQuantification_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.SmallMoleculeQuantification_zip,
                        TutorialLinkResources.SmallMoleculeQuantification_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.SmallMoleculeQuantification_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.HiResMetabolomics_Caption,
                    Icon = TutorialImageResources.HiResMetabolomics_start,
                    EventAction = () => Tutorial(
                        TutorialLinkResources.HiResMetabolomics_zip,
                        TutorialLinkResources.HiResMetabolomics_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.HiResMetabolomics_Description
                },
                new TutorialActionBoxControl
                {
                Caption = TutorialTextResources.SmallMolLibraries_Caption,
                Icon = TutorialImageResources.SmallMolLibraries_start,
                EventAction = () => Tutorial(
                    TutorialLinkResources.SmallMolLibraries_zip,
                    TutorialLinkResources.SmallMolLibraries_pdf,
                    string.Empty
                ),
                Description = TutorialTextResources.SmallMolLibraries_Description
                }
            };
            var tutorialAdvancedBoxPanels = new Control[]
            {
                new Label
                {
                    Text = TutorialTextResources.Section_Advanced_Topics,
                    Font = labelFont,
                    Anchor = labelAnchor,
                    Width = labelWidth
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.AbsoluteQuant_Caption, 
                    Icon = TutorialImageResources.AbsoluteQuant_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.AbsoluteQuant_zip,
                        TutorialLinkResources.AbsoluteQuant_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.AbsoluteQuant_Description,
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.CustomReports_Caption, 
                    Icon = TutorialImageResources.CustomReports_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.CustomReports_zip,
                        TutorialLinkResources.CustomReports_pdf,
                        TutorialLinkResources.CustomReports_sky
                    ),
                    Description = TutorialTextResources.CustomReports_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.PeakPicking_Caption, 
                    Icon = TutorialImageResources.PeakPicking_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.PeakPicking_zip,
                        TutorialLinkResources.PeakPicking_pdf,
                        TutorialLinkResources.PeakPicking_sky
                    ),
                    Description = TutorialTextResources.PeakPicking_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.iRT_Caption, 
                    Icon = TutorialImageResources.iRT_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.iRT_zip,
                        TutorialLinkResources.iRT_pdf,
                        TutorialLinkResources.iRT_sky
                    ),
                    Description = TutorialTextResources.iRT_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.OptimizeCE_Caption, 
                    Icon = TutorialImageResources.OptimizeCE_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.OptimizeCE_zip,
                        TutorialLinkResources.OptimizeCE_pdf,
                        TutorialLinkResources.OptimizeCE_sky
                    ),
                    Description = TutorialTextResources.OptimizeCE_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.IMSFiltering_Caption,
                    Icon = TutorialImageResources.IMSFiltering_start,
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.IMSFiltering_zip,
                        TutorialLinkResources.IMSFiltering_pdf,
                        TutorialLinkResources.IMSFiltering_sky
                    ),
                    Description = TutorialTextResources.IMSFiltering_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.LibraryExplorer_Caption, 
                    Icon = TutorialImageResources.LibraryExplorer_start, 
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.LibraryExplorer_zip,
                        TutorialLinkResources.LibraryExplorer_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.LibraryExplorer_Description
                },
                new TutorialActionBoxControl
                {
                    Caption = TutorialTextResources.AuditLog_Caption,
                    Icon = TutorialImageResources.AuditLog_start,
                    EventAction = ()=>Tutorial(
                        TutorialLinkResources.AuditLog_zip,
                        TutorialLinkResources.AuditLog_pdf,
                        string.Empty
                    ),
                    Description = TutorialTextResources.AuditLog_Description
                },
            };

            Control previousBox = null;
            // For small molecule mode, lead with small molecule tutorials
            var tutorialBoxPanels = ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules
                ? tutorialSmallMoleculeBoxPanels.ToList()
                : tutorialProteomicBoxPanels.ToList();
            tutorialBoxPanels.AddRange(ModeUI != SrmDocument.DOCUMENT_TYPE.small_molecules
                ? tutorialSmallMoleculeBoxPanels
                : tutorialProteomicBoxPanels);
            tutorialBoxPanels.AddRange(tutorialAdvancedBoxPanels);
            flowLayoutPanelTutorials.Controls.Clear();
            foreach (var box in tutorialBoxPanels)
            {
                if (box is Label && previousBox != null)
                {
                    flowLayoutPanelTutorials.SetFlowBreak(previousBox, true); // Start a new section
                }
                flowLayoutPanelTutorials.Controls.Add(box);
                previousBox = box;
                GetModeUIHelper().NoteModeUIInvariantComponent(box); // Tutorials don't need any UI mode treatment
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
        
        private void Tutorial(string skyFileLocation, string pdfFileLocation, string zipSkyFileLocation)
        {
            Assume.IsNotNull(skyFileLocation);

            var pathChooserDlg = new PathChooserDlg(Resources.StartPage_Tutorial__Folder_for_tutorial_files_, skyFileLocation);
            if (pathChooserDlg.ShowDialog(this) != DialogResult.OK)
                return;

            DialogResult = DialogResult.OK;

            string paths = pathChooserDlg.ExtractionPath;
            DoAction(new ActionTutorial( paths, skyFileLocation, pdfFileLocation, zipSkyFileLocation)
                .DoStartupAction);
        }

        private string OpenFileDlg()
        {
            using (var openNewFileDlg = new OpenFileDialog
            {
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC_AND_SKY_ZIP, SrmDocumentSharing.FILTER_SHARING, SkypFile.FILTER_SKYP),
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

        private void PositionButtonsModeUI()
        {
            tooStripModeUI.GripStyle = ToolStripGripStyle.Hidden;
            tooStripModeUI.Location = new Point(Width - (toolStripButtonModeUI.Width + 2*(Margin.Left+2*Margin.Right)), tabControlMain.Top);
            tooStripModeUI.BringToFront();
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
            // Resize section tutorial dividers
            foreach (Control control in flowLayoutPanelTutorials.Controls)
            {
                if (control is Label)
                    control.Width = flowLayoutPanelTutorials.ClientRectangle.Width - control.Left - SystemInformation.VerticalScrollBarWidth;
            }
            // ModeUI controls
            PositionButtonsModeUI(); 
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
                Settings.Default.SaveWithoutExceptions();
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
                if (Equals(control.AccessibleName, @"ActionBoxControl"))
                {
                    controls.Add(control);
                }
            }
            foreach (Control control in flowLayoutPanelTutorials.Controls)
            {
                if (Equals(control.AccessibleName, @"ActionBoxControl"))
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

        public void UpdateTaskbarProgress(TaskbarProgress.TaskbarStates state, int? percentComplete)
        {
            _taskbarProgress.SetState(Handle, state);
            if (percentComplete.HasValue)
            {
                _taskbarProgress.SetValue(Handle, percentComplete.Value, 100);
            }
        }

        public void TestImportAction(ActionImport.DataType type, string filePath = null)
        {
            var action = new ActionImport(type) {FilePath = filePath};
            DoAction(action.DoStartupAction);
        }

        public void TestTutorialAction()
        {
            Tutorial(
                TutorialLinkResources.LibraryExplorer_zip,
                TutorialLinkResources.LibraryExplorer_pdf,
                string.Empty
                );
        }

        public void ClickWizardAction(string actionName)
        {
            flowLayoutPanelWizard.Controls.Cast<ActionBoxControl>().First(c => Equals(c.Caption, actionName))
                .EventAction();
        }
    }
}
