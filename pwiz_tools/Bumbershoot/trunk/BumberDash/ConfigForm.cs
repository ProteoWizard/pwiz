using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BumberDash
{
    public partial class ConfigForm : Form
    {

    #region Globals
        internal string _configName = string.Empty;
        private string _saveDestinationPath;
        private string _outputDirectory;
        private string _destinationProgram = "MyriMatch";
        private List<string> _preferredSave = new List<string>();
        private int _currentTemplate = 0;
        private bool _skipDiscardCheck = false;
        private bool _cancelDialogResult = false;
        private bool _skipAutomation = false;
        private bool _breakRecursion = false;
        private bool _validMods = true;
        private bool _fileChanged = false;
        private double _factorX;
        private double _factorY;
        private bool[] _changed = new bool[52];
        private Regex _statRx = new Regex("^[\\(\\)ACDEFGHIKLMNPQRSTUVWXY]$");
        private Regex _dynRx = new Regex("^(?:\\(|\\)|(?:\\(|{\\(})?(?:(?:\\[[ACDEFGHIKLMNPQRSTUVWXY]+\\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))+!?(?:(?:\\[[ACDEFGHIKLMNPQRSTUVWXY]+\\])|(?:{[ACDEFGHIKLMNPQRSTUVWXY]+})|(?:[ACDEFGHIKLMNPQRSTUVWXY]))*(?:\\)|{\\)})?)$");
        private Template[] _formTemplate;
        private Template _fileContents = new Template();       
    #endregion

        public ConfigForm(string newFilePath, string newOutputDirectory, string configProgram)
        {
            InitializeComponent();
            _saveDestinationPath = newFilePath;
            _outputDirectory = newOutputDirectory;
            _destinationProgram = configProgram;            
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);

            _factorX = factor.Width;
            _factorY = factor.Height;
        }

        private void ConfigForm_Load(object sender, EventArgs e)
        {
            UpdateTemplateMenuItems();
            _fileContents.SetAsDefaultTemplate();
            LoadTemplate(0, e);
            tabControl1.TabPages.RemoveAt(1);

            if (!string.IsNullOrEmpty(_saveDestinationPath) && File.Exists(_saveDestinationPath))
            {
                if (Path.GetExtension(_saveDestinationPath).Equals(".cfg"))
                {
                    OpenFromFile(_saveDestinationPath);
                    SaveOverOldButton.Visible = true;
                }
                else
                {
                    _saveDestinationPath = string.Empty;
                    SaveOverOldButton.Visible = false;
                }
            }
            else
                SaveOverOldButton.Visible = false;

            switch (_destinationProgram)
            {
                case "MyriMatch":
                    myrimatchToolStripMenuItem_Click(0, new EventArgs());
                    break;
                case "TagRecon":
                    tagReconToolStripMenuItem_Click(0, new EventArgs());
                    break;
                case "DirecTag":
                    direcTagToolStripMenuItem_Click(0, new EventArgs());
                    break;
            }

            
        }

        

    #region: Form manipulation

        //Click events
        private void AdjustPrecursorMassBox_Click(object sender, EventArgs e)
        {
            if (this.AdjustPrecursorMassBox.Checked == true)
                AdjustYes();
            else
                AdjustNo();
        }

        private void ModList_SelectedValueChanged(object sender, EventArgs e)
        {
            
            try
            {
                if (ModList.SelectedItems.Count > 0)
                {
                    ResidueBox.Text = ModList.SelectedItems[0].SubItems[1].Text;
                    ModMassBox.Text = ModList.SelectedItems[0].SubItems[2].Text;
                    if (ResidueBox.Text.Length == 1)
                        ModTypeBox.SelectedIndex = 0;
                    else
                        ModTypeBox.SelectedIndex = 1;
                }
            }
            catch
            {
                MessageBox.Show("Error in reading selected item");
            }
            
        }
  
        private void AppliedModAdd_Click(object sender, EventArgs e)
        {
            object[] Values = new object[3];

            if (IsValidModification(ResidueBox.Text, ModMassBox.Text, ModTypeBox.Text, false) == 0)
            {
                Values[0] = ResidueBox.Text;
                Values[1] = double.Parse(ModMassBox.Text).ToString();
                Values[2] = ModTypeBox.Text;
                if (ModTypeBox.Text == "PreferredPTM")
                {
                    _skipAutomation = true;
                    ExplainUnknownMassShiftsAsBox.Text = "PreferredPTMs";
                    _skipAutomation = false;
                }
                ResidueBox.Clear();
                ModMassBox.Clear();
                ModTypeBox.SelectedIndex = 0;
                AppliedModDGV.Rows.Add(Values);
                AppliedModDGV.ClearSelection();
                ChangeCheck(_formTemplate[_currentTemplate]);
            }
            
        }

        private void AppliedModRemove_Click(object sender, EventArgs e)
        {
            if (AppliedModDGV.SelectedRows.Count > 0)
            {
                bool KeepExplanation = false;
                int Selection = AppliedModDGV.SelectedRows[0].Index;

                ResidueBox.Text = AppliedModDGV.Rows[Selection].Cells[0].Value.ToString();
                ModMassBox.Text = AppliedModDGV.Rows[Selection].Cells[1].Value.ToString();
                ModTypeBox.Text = AppliedModDGV.Rows[Selection].Cells[2].Value.ToString();

                AppliedModDGV.Rows.RemoveAt(Selection);

                if (ModTypeBox.Text == "PreferredPTM")
                {
                    for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                    {
                        if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                        {
                            KeepExplanation = true;
                            break;
                        }
                    }

                    if (!KeepExplanation)
                    {
                        _skipAutomation = true;
                        ExplainUnknownMassShiftsAsBox.Text = string.Empty;
                        _skipAutomation = false;
                    }
                }

                CheckMods();
                ChangeCheck(_formTemplate[_currentTemplate]);
                AppliedModDGV.ClearSelection();
            }
        }

        private void UnimodXMLBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFile = new OpenFileDialog();

            OpenFile.CheckFileExists = true;
            OpenFile.CheckPathExists = true;
            OpenFile.Filter = "XML Files (.xml)|*.xml";
            OpenFile.RestoreDirectory = true;
            OpenFile.Title = "Open XML file";

            if (OpenFile.ShowDialog().Equals(DialogResult.OK))
            {
                UnimodXMLBox.Text = OpenFile.FileName;
            }
        }

        private void BlosumBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFile = new OpenFileDialog();

            OpenFile.CheckFileExists = true;
            OpenFile.CheckPathExists = true;
            OpenFile.Filter = "FAS Files (.fas)|*.fas";
            OpenFile.RestoreDirectory = true;
            OpenFile.Title = "Open FAS file";

            if (OpenFile.ShowDialog().Equals(DialogResult.OK))
            {
                BlosumBox.Text = OpenFile.FileName;
            }

        }
   
        private void InstrumentBox_SelectedValueChanged(object sender, EventArgs e)
        {
            if (!_skipAutomation)
            {
                switch (MessageBox.Show("Load \"" + InstrumentBox.SelectedItem.ToString() + "\" default settings?", "Comfirm Template", MessageBoxButtons.YesNoCancel))
                {
                    case DialogResult.Yes:
                    LoadTemplate(InstrumentBox.SelectedIndex+1, e);
                    break;

                    case DialogResult.No:
                    _currentTemplate = InstrumentBox.SelectedIndex + 1;
                    ChangeCheck(_formTemplate[_currentTemplate]);
                    break;

                    case DialogResult.Cancel:
                    _skipAutomation = true;
                    InstrumentBox.SelectedIndex = _currentTemplate - 1;
                    _skipAutomation = false;
                    break;
                }
            }
        }

        private void Info_Click(object sender, EventArgs e)
        {
            string Anchor;

            Anchor = ((Label)sender).Name.ToString();
            Anchor = Anchor.Remove(Anchor.Length - 4);
            if (_destinationProgram == "MyriMatch")
            {
                try
                {
                    Anchor = String.Format("\"file:///{0}/MyriMatch.html#{1}\"", Environment.CurrentDirectory.ToString().Replace("\\", "/"), Anchor);
                    System.Diagnostics.Process.Start(Anchor);
                }
                catch
                {
                    MessageBox.Show("Help Page not found");
                }

            }
            else if (_destinationProgram == "DirecTag")
            {
                try
                {
                Anchor = String.Format("\"file:///{0}/DirecTag.html#{1}\"", Environment.CurrentDirectory.ToString().Replace("\\", "/"), Anchor);
                System.Diagnostics.Process.Start(Anchor);
                }
                catch
                {
                    MessageBox.Show("Help Page not found");
                }
            }
            else if (_destinationProgram == "TagRecon")
            {
                try
                {
                    Anchor = String.Format("\"file:///{0}/TagRecon.html#{1}\"", Environment.CurrentDirectory.ToString().Replace("\\", "/"), Anchor);
                    System.Diagnostics.Process.Start(Anchor);
                }
                catch
                {
                    MessageBox.Show("Help Page not found");
                }
            }
        }


        //ValueChanged Events
        private void AdvModeBox_CheckedChanged(object sender, EventArgs e)
        {
            if (AdvModeBox.Checked == true)
            {
                tabControl1.TabPages.Add(AdvTab);
                UseAvgMassOfSequencesBox.Enabled = true;
                UseChargeStateFromMSBox.Enabled = true;
                DuplicateSpectraBox.Enabled = true;
                NumChargeStatesBox.Enabled = true;
                NumMaxMissedCleavagesBox.Enabled = true;
                NumMaxMissedCleavagesAuto.Enabled = true;
                PrecursorMzToleranceBox.Enabled = true;
                FragmentMzToleranceBox.Enabled = true;
                PrecursorMzToleranceUnitsBox.Enabled = true;
                FragmentMzToleranceUnitsBox.Enabled = true;
                NTerminusMzToleranceUnitsBox.Enabled = true;
                CTerminusMzToleranceUnitsBox.Enabled = true;
                NTerminusMzToleranceBox.Enabled = true;
                CTerminusMzToleranceBox.Enabled = true;
            }
            else
            {
                tabControl1.TabPages.RemoveAt(1);
                UseAvgMassOfSequencesBox.Enabled = false;
                UseChargeStateFromMSBox.Enabled = false;
                DuplicateSpectraBox.Enabled = false;
                NumChargeStatesBox.Enabled = false;
                NumMaxMissedCleavagesBox.Enabled = false;
                NumMaxMissedCleavagesAuto.Enabled = false;
                PrecursorMzToleranceBox.Enabled = false;
                FragmentMzToleranceBox.Enabled = false;
                PrecursorMzToleranceUnitsBox.Enabled = false;
                FragmentMzToleranceUnitsBox.Enabled = false;
                NTerminusMzToleranceUnitsBox.Enabled = false;
                CTerminusMzToleranceUnitsBox.Enabled = false;
                NTerminusMzToleranceBox.Enabled = false;
                CTerminusMzToleranceBox.Enabled = false;
            }
        }

        private void ResidueBox_TextChanged(object sender, EventArgs e)
        {
            if (_dynRx.IsMatch(((TextBox)sender).Text) || ((TextBox)sender).Text.Length == 0)
                ((TextBox)sender).BackColor = Color.White;
            else
                ((TextBox)sender).BackColor = Color.LightPink;
        }

        private void EndSpectraScanNumBox_ValueChanged(object sender, EventArgs e)
        {
            if (EndSpectraScanNumBox.Value == -1)
                EndSpectraScanNumAuto.Visible = true;
            else
                EndSpectraScanNumAuto.Visible = false;
        }

        private void EndProteinIndexBox_ValueChanged(object sender, EventArgs e)
        {
            if (EndProteinIndexBox.Value == -1)
                EndProteinIndexAuto.Visible = true;
            else
                EndProteinIndexAuto.Visible = false;
        }

        private void NumMaxMissedCleavagesBox_ValueChanged(object sender, EventArgs e)
        {
            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);

            if (NumMaxMissedCleavagesBox.Value == -1)
                NumMaxMissedCleavagesAuto.Visible = true;
            else
                NumMaxMissedCleavagesAuto.Visible = false;

        }

        private void UseNETAdjustmentBox_CheckedChanged(object sender, EventArgs e)
        {
            if (NumMinTerminiCleavagesBox.SelectedIndex == 2 && UseNETAdjustmentBox.Checked == true)
            {
                MessageBox.Show("NET based adjustment is not neccessary when searching fully tryptic peptides");
                UseNETAdjustmentBox.Checked = false;
            }

            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);
        }


        //Other Events
        private void ValueBox_Leave(object sender, EventArgs e)
        {
            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);
        }

        private void NumMinTerminiCleavagesBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (NumMinTerminiCleavagesBox.SelectedIndex == 2)
                UseNETAdjustmentBox.Checked = false;

            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);
        }
        
        private void Info_MouseEnter(object sender, EventArgs e)
        {
            ((Label)sender).Font = new Font("Microsoft Sans Serif", 8, FontStyle.Bold);
        }

        private void Info_MouseLeave(object sender, EventArgs e)
        {
            ((Label)sender).Font = new Font("Microsoft Sans Serif", 7, FontStyle.Regular);
        }

        private void ModMassBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                AppliedModAdd_Click(0, e);
            }
            NumericTextBox_KeyPress(sender, e);
        }

        private void SoftMessageTimer_Tick(object sender, EventArgs e)
        {
            SoftMessageLabel.Tag = "Out 0";
            SoftMessageTimer.Enabled = false;
        }

        private void SoftMessageFadeTimer_Tick(object sender, EventArgs e)
        {
            switch (SoftMessageLabel.Tag.ToString())
            {
                case "In 0":
                    SoftMessageLabel.ForeColor = Color.PeachPuff;
                    SoftMessageLabel.Visible = true;
                    SoftMessageLabel.Tag = "In 1";
                    break;
                case "In 1":
                    SoftMessageLabel.ForeColor = Color.SandyBrown;
                    SoftMessageLabel.Tag = "In 2";
                    break;
                case "In 2":
                    SoftMessageLabel.ForeColor = Color.OrangeRed;
                    SoftMessageLabel.Tag = "In 3";
                    break;
                case "In 3":
                    SoftMessageLabel.ForeColor = Color.Red;
                    SoftMessageLabel.Tag = "Hold";
                    SoftMessageTimer.Enabled = true;
                    break;
                case "Out 0":
                    SoftMessageLabel.ForeColor = Color.Red;
                    SoftMessageLabel.Tag = "Out 1";
                    break;
                case "Out 1":
                    SoftMessageLabel.ForeColor = Color.OrangeRed;
                    SoftMessageLabel.Tag = "Out 2";
                    break;
                case "Out 2":
                    SoftMessageLabel.ForeColor = Color.SandyBrown;
                    SoftMessageLabel.Tag = "Out 3";
                    break;
                case "Out 3":
                    SoftMessageLabel.ForeColor = Color.PeachPuff;
                    SoftMessageLabel.Visible = false;
                    SoftMessageLabel.Tag = "In 0";
                    SoftMessageFadeTimer.Enabled = false;
                    break;
            }
        }

        private void AppliedModDGV_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            bool KeepExplanation = false;

            try
            {
                switch (IsValidModification(AppliedModDGV.Rows[e.RowIndex].Cells[0].Value.ToString(),
                    AppliedModDGV.Rows[e.RowIndex].Cells[1].Value.ToString(),
                    AppliedModDGV.Rows[e.RowIndex].Cells[2].Value.ToString(), true))
                {
                    case 1:
                        if (_dynRx.IsMatch(AppliedModDGV.Rows[e.RowIndex].Cells[0].Value.ToString()))
                        {
                            SoftMessageLabel.Text = "Residue motifs cannot be static.";
                            SoftMessageLabel.Location = new Point(130, 5);
                            SoftMessageFadeTimer.Enabled = true;
                            AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.White;
                            AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.LightPink;
                        }
                        else
                        {
                            SoftMessageLabel.Text = "Invalid residue character.";
                            SoftMessageLabel.Location = new Point(175, 5);
                            SoftMessageFadeTimer.Enabled = true;
                            AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.LightPink;
                            AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.White;
                        }
                        break;

                    case 2:
                        SoftMessageLabel.Text = "Invalid residue motif.";
                        SoftMessageLabel.Location = new Point(175, 5);
                        SoftMessageFadeTimer.Enabled = true;
                        AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.LightPink;
                        AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.White;
                        break;

                    case 3:
                        SoftMessageLabel.Text = "Residue Motif already present in list.";
                        SoftMessageLabel.Location = new Point(100, 5);
                        SoftMessageFadeTimer.Enabled = true;
                        AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.LightPink;
                        AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.White;
                        break;

                    case 4:
                        SoftMessageLabel.Text = "Invalid residue mass.";
                        SoftMessageLabel.Location = new Point(175, 5);
                        SoftMessageFadeTimer.Enabled = true;
                        AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.White;
                        AppliedModDGV.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.LightPink;
                        AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.White;
                        break;

                    case 0:
                        AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.White;
                        AppliedModDGV.Rows[e.RowIndex].Cells[1].Style.BackColor = Color.White;
                        AppliedModDGV.Rows[e.RowIndex].Cells[2].Style.BackColor = Color.White;
                        break;
                }
            }
            catch
            {
                SoftMessageLabel.Text = "Invalid residue character/motif.";
                SoftMessageLabel.Text = "Error reading modification.";
                AppliedModDGV.Rows[e.RowIndex].Cells[0].Value = string.Empty;
                SoftMessageLabel.Location = new Point(140, 5);
                SoftMessageFadeTimer.Enabled = true;
                AppliedModDGV.Rows[e.RowIndex].Cells[0].Style.BackColor = Color.LightPink;
            }

            if (_destinationProgram == "TagRecon")
            {
                for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                {
                    if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                    {
                        KeepExplanation = true;
                        break;
                    }
                }

                _skipAutomation = true;
                if (KeepExplanation)
                    ExplainUnknownMassShiftsAsBox.Text = "PreferredPTMs";
                else
                    ExplainUnknownMassShiftsAsBox.Text = string.Empty;
                _skipAutomation = false;
            }
            
            CheckMods();
        }

        private void CheckMods()
        {
            bool AllValid = true;

            for (int x = 0; AllValid == true && x < AppliedModDGV.Rows.Count; x++)
            {
                try
                {
                    if (IsValidModification(AppliedModDGV.Rows[x].Cells[0].Value.ToString(), AppliedModDGV.Rows[x].Cells[1].Value.ToString(), AppliedModDGV.Rows[x].Cells[2].Value.ToString(), true) > 0)
                        AllValid = false;
                }
                catch
                {
                    AllValid = false;
                }
            }

            if (AllValid)
                _validMods = true;
            else
                _validMods = false;

            //for aesthetics
            if (!AppliedModRemove.Focused == true && !AppliedModDGV.Focused == true)
                AppliedModDGV.ClearSelection();
        }


        //Used for form manipulation
        private void AdjustYes()
        {
            MinPrecursorAdjustmentBox.Enabled = true;
            MinPrecursorAdjustmentLabel.Enabled = true;
            MaxPrecursorAdjustmentBox.Enabled = true;
            MaxPrecursorAdjustmentLabel.Enabled = true;
        }

        private void AdjustNo()
        {
            MinPrecursorAdjustmentBox.Enabled = false;
            MinPrecursorAdjustmentLabel.Enabled = false;
            MaxPrecursorAdjustmentBox.Enabled = false;
            MaxPrecursorAdjustmentLabel.Enabled = false;
        }

        private void ChangeCheck(Template Temp)
        {
            if (!_breakRecursion)
            {
                _breakRecursion = true;
                ChangeCheck(_fileContents);
                _breakRecursion = false;
                if (_changed.Contains(true))
                    _fileChanged = true;
                else
                    _fileChanged = false;
            }


            //PrecursorMzTolerance
            if (PrecursorMzToleranceBox.Text ==
                Temp.PrecursorMzTolerance.ToString() &&
                PrecursorMzToleranceUnitsBox.SelectedItem.ToString() ==
                Temp.PrecursorMzToleranceUnits)
            {
                PrecursorMzToleranceLabel.ForeColor = Color.Black;
                _changed[0] = false;
            }
            else
            {
                PrecursorMzToleranceLabel.ForeColor = Color.Green;
                _changed[0] = true;
            }

            //FragmentMzTolerance
            if (FragmentMzToleranceBox.Text ==
                Temp.FragmentMzTolerance.ToString() &&
                FragmentMzToleranceUnitsBox.SelectedItem.ToString() ==
                Temp.FragmentMzToleranceUnits)
            {
                FragmentMzToleranceLabel.ForeColor = Color.Black;
                _changed[1] = false;
            }
            else
            {
                FragmentMzToleranceLabel.ForeColor = Color.Green;
                _changed[1] = true;
            }


            //PrecursorMzToleranceUnits
            if (PrecursorMzToleranceUnitsBox.SelectedItem.ToString() ==
                Temp.PrecursorMzToleranceUnits)
            {
                _changed[2] = false;
            }
            else
            {
                if (_destinationProgram == "MyriMatch")
                    _changed[2] = true;
                else
                    _changed[2] = false;
            }


            //FragmentMzToleranceUnits
            if (FragmentMzToleranceUnitsBox.SelectedItem.ToString() ==
                Temp.FragmentMzToleranceUnits)
            {
                _changed[3] = false;
            }
            else
            {
                if (_destinationProgram == "MyriMatch")
                    _changed[3] = true;
                else
                    _changed[3] = false;
            }


            //NTerminusMzTolerance
            if (NTerminusMzToleranceBox.Text ==
                Temp.NTerminusMzTolerance.ToString())
            {
                NTerminusMzToleranceLabel.ForeColor = Color.Black;
                _changed[4] = false;
            }
            else
            {
                NTerminusMzToleranceLabel.ForeColor = Color.Green;
                if (_destinationProgram != "MyriMatch")
                    _changed[4] = true;
                else
                    _changed[4] = false;
            }


            //CTerminusMzTolerance
            if (CTerminusMzToleranceBox.Text ==
                Temp.CTerminusMzTolerance.ToString())
            {
                CTerminusMzToleranceLabel.ForeColor = Color.Black;
                _changed[5] = false;
            }
            else
            {
                CTerminusMzToleranceLabel.ForeColor = Color.Green;
                if (_destinationProgram != "MyriMatch")
                    _changed[5] = true;
                else
                    _changed[5] = false;
            }


            //AdjustPrecursorMass
            if (AdjustPrecursorMassBox.Checked ==
                Temp.AdjustPrecursorMass)
            {
                AdjustPrecursorMassLabel.ForeColor = Color.Black;
                _changed[6] = false;
            }
            else
            {
                AdjustPrecursorMassLabel.ForeColor = Color.Green;
                _changed[6] = true;
            }


            //MaxPrecursorAdjustment
            if (((double)MaxPrecursorAdjustmentBox.Value * 1.008665) ==
                Temp.MaxPrecursorAdjustment)
            {
                MaxPrecursorAdjustmentLabel.ForeColor = Color.Black;
                _changed[7] = false;
            }
            else
            {
                MaxPrecursorAdjustmentLabel.ForeColor = Color.Green;
                if (_changed[6])
                    _changed[7] = true;
                else
                    _changed[7] = false;
            }


            //MinPrecursorAdjustment
            if (((double)MinPrecursorAdjustmentBox.Value * 1.008665) ==
                Temp.MinPrecursorAdjustment)
            {
                MinPrecursorAdjustmentLabel.ForeColor = Color.Black;
                _changed[8] = false;
            }
            else
            {
                MinPrecursorAdjustmentLabel.ForeColor = Color.Green;
                if (_changed[6])
                    _changed[8] = true;
                else
                    _changed[8] = false;
            }


            //DuplicateSpectra
            if (DuplicateSpectraBox.Checked ==
                Temp.DuplicateSpectra)
            {
                DuplicateSpectraLabel.ForeColor = Color.Black;
                _changed[9] = false;
            }
            else
            {
                DuplicateSpectraLabel.ForeColor = Color.Green;
                _changed[9] = true;
            }


            //UseChargeStateFromMS
            if (UseChargeStateFromMSBox.Checked ==
                Temp.UseChargeStateFromMS)
            {
                UseChargeStateFromMSLabel.ForeColor = Color.Black;
                _changed[10] = false;
            }
            else
            {
                UseChargeStateFromMSLabel.ForeColor = Color.Green;
                _changed[10] = true;
            }


            //NumChargeStates
            if (NumChargeStatesBox.Value ==
                ((decimal)Temp.NumChargeStates))
            {
                NumChargeStatesLabel.ForeColor = Color.Black;
                _changed[11] = false;
            }
            else
            {
                NumChargeStatesLabel.ForeColor = Color.Green;
                _changed[11] = true;
            }


            //TicCutoffPercentage
            if (TicCutoffPercentageBox.Value ==
                ((decimal)Temp.TicCutoffPercentage))
            {
                TicCutoffPercentageLabel.ForeColor = Color.Black;
                _changed[12] = false;
            }
            else
            {
                TicCutoffPercentageLabel.ForeColor = Color.Green;
                _changed[12] = true;
            }


            //UseSmartPlusThreeModel
            if (UseSmartPlusThreeModelBox.Checked ==
                Temp.UseSmartPlusThreeModel)
            {
                UseSmartPlusThreeModelLabel.ForeColor = Color.Black;
                _changed[13] = false;
            }
            else
            {
                UseSmartPlusThreeModelLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[13] = true;
                else
                    _changed[13] = false;
            }


            //CleavageRules
            if (CleavageRulesBox.SelectedItem.ToString() ==
                Temp.CleavageRules)
            {
                CleavageRulesLabel.ForeColor = Color.Black;
                _changed[14] = false;
            }
            else
            {
                CleavageRulesLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[14] = true;
                else
                    _changed[14] = false;
            }


            //NumMinTerminiCleavages
            if (NumMinTerminiCleavagesBox.SelectedIndex ==
                ((decimal)Temp.NumMinTerminiCleavages))
            {
                NumMinTerminiCleavagesLabel.ForeColor = Color.Black;
                _changed[15] = false;
            }
            else
            {
                NumMinTerminiCleavagesLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[15] = true;
                else
                    _changed[15] = false;
            }


            //NumMaxMissedCleavages
            if (NumMaxMissedCleavagesBox.Value ==
                ((decimal)Temp.NumMaxMissedCleavages))
            {
                NumMaxMissedCleavagesLabel.ForeColor = Color.Black;
                _changed[16] = false;
            }
            else
            {
                NumMaxMissedCleavagesLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[16] = true;
                else
                    _changed[16] = false;
            }


            //UseAvgMassOfSequences
            if (Convert.ToBoolean(UseAvgMassOfSequencesBox.SelectedIndex) ==
                Temp.UseAvgMassOfSequences)
            {
                UseAvgMassOfSequencesLabel.ForeColor = Color.Black;
                _changed[17] = false;
            }
            else
            {
                UseAvgMassOfSequencesLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[17] = true;
                else
                    _changed[17] = false;
            }


            //MinCandidateLength
            if (MinCandidateLengthBox.Value ==
                ((decimal)Temp.MinCandidateLength))
            {
                MinCandidateLengthLabel.ForeColor = Color.Black;
                _changed[18] = false;
            }
            else
            {
                MinCandidateLengthLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[18] = true;
                else
                    _changed[18] = true;
            }

            bool AllEqual = true;

            //Modifications
            if (AppliedModDGV.Rows.Count ==
                Temp.Modifications.Count)
            {
                for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                    if (!(Temp.Modifications.Contains(AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[2].Value.ToString())))
                    {
                        //MessageBox.Show(AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[2].Value.ToString());
                        AllEqual = false;
                    }
            }
            else
                AllEqual = false;

            if (AllEqual)
            {
                AppliedModLabel.ForeColor = Color.Black;
                _changed[19] = false;
            }
            else
            {
                AppliedModLabel.ForeColor = Color.Green;
                _changed[19] = true;
            }


            //MaxDynamicMods
            if (MaxDynamicModsBox.Value ==
                ((decimal)Temp.MaxDynamicMods))
            {
                MaxDynamicModsLabel.ForeColor = Color.Black;
                _changed[20] = false;
            }
            else
            {
                MaxDynamicModsLabel.ForeColor = Color.Green;
                _changed[20] = true;
            }

            AllEqual = true;

            //MaxNumPreferredDeltaMasses
            if (MaxNumPreferredDeltaMassesBox.Value ==
                ((decimal)Temp.MaxNumPreferredDeltaMasses))
            {
                MaxNumPreferredDeltaMassesLabel.ForeColor = Color.Black;
                _changed[21] = false;
            }
            else
            {
                MaxNumPreferredDeltaMassesLabel.ForeColor = Color.Green;
                _changed[21] = true;
            }


            //ExplainUnknownMassShiftsAs
            if (ExplainUnknownMassShiftsAsBox.Text ==
                Temp.ExplainUnknownMassShiftsAs)
            {
                ExplainUnknownMassShiftsAsLabel.ForeColor = Color.Black;
                _changed[22] = false;
            }
            else
            {
                ExplainUnknownMassShiftsAsLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[22] = true;
                else
                    _changed[22] = false;
            }

            //MaxModificationMassPlus
            if (MaxModificationMassPlusBox.Value ==
                ((decimal)Temp.MaxModificationMassPlus))
            {
                MaxModificationMassPlusLabel.ForeColor = Color.Black;
                _changed[23] = false;
            }
            else
            {
                MaxModificationMassPlusLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[23] = true;
                _changed[23] = false;
            }


            //MaxModificationMassMinus
            if (MaxModificationMassMinusBox.Value ==
                ((decimal)Temp.MaxModificationMassMinus))
            {
                MaxModificationMassMinusLabel.ForeColor = Color.Black;
                _changed[24] = false;
            }
            else
            {
                MaxModificationMassMinusLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[24] = true;
                else
                    _changed[24] = false;
            }


            //UnimodXML
            if (UnimodXMLBox.Text ==
                Temp.UnimodXML)
            {
                UnimodXMLLabel.ForeColor = Color.Black;
                _changed[25] = false;
            }
            else
            {
                UnimodXMLLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[25] = true;
                else
                    _changed[25] = false;
            }


            //Blosum
            if (BlosumBox.Text ==
                Temp.Blosum)
            {
                BlosumLabel.ForeColor = Color.Black;
                _changed[26] = false;
            }
            else
            {
                BlosumLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[26] = true;
                else
                    _changed[26] = false;
            }


            //BlosumThreshold
            if (BlosumThresholdBox.Value ==
                ((decimal)Temp.BlosumThreshold))
            {
                BlosumThresholdLabel.ForeColor = Color.Black;
                _changed[27] = false;
            }
            else
            {
                BlosumThresholdLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[27] = true;
                else
                    _changed[27] = false;
            }


            //MaxResults
            if (MaxResultsBox.Value ==
                ((decimal)Temp.MaxResults))
            {
                MaxResultsLabel.ForeColor = Color.Black;
                _changed[28] = false;
            }
            else
            {
                MaxResultsLabel.ForeColor = Color.Green;
                _changed[28] = true;
            }


            //ProteinSampleSize
            if (ProteinSampleSizeBox.Value ==
                ((decimal)Temp.ProteinSampleSize))
            {
                ProteinSampleSizeLabel.ForeColor = Color.Black;
                _changed[29] = false;
            }
            else
            {
                ProteinSampleSizeLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[29] = true;
                else
                    _changed[29] = false;
            }


            //NumIntensityClasses
            if (NumIntensityClassesBox.Value ==
                ((decimal)Temp.NumIntensityClasses))
            {
                NumIntensityClassesLabel.ForeColor = Color.Black;
                _changed[30] = false;
            }
            else
            {
                NumIntensityClassesLabel.ForeColor = Color.Green;
                _changed[30] = true;
            }


            //ClassSizeMultiplier
            if (ClassSizeMultiplierBox.Value ==
                ((decimal)Temp.ClassSizeMultiplier))
            {
                ClassSizeMultiplierLabel.ForeColor = Color.Black;
                _changed[31] = false;
            }
            else
            {
                ClassSizeMultiplierLabel.ForeColor = Color.Green;
                _changed[31] = true;
            }


            //DeisotopingMode
            if (DeisotopingModeBox.SelectedIndex ==
                ((decimal)Temp.DeisotopingMode))
            {
                DeisotopingModeLabel.ForeColor = Color.Black;
                _changed[32] = false;
            }
            else
            {
                DeisotopingModeLabel.ForeColor = Color.Green;
                _changed[32] = true;
            }


            //IsotopeMzTolerance
            if (IsotopeMzToleranceBox.Text ==
                Temp.IsotopeMzTolerance.ToString())
            {
                IsotopeMzToleranceLabel.ForeColor = Color.Black;
                _changed[33] = false;
            }
            else
            {
                IsotopeMzToleranceLabel.ForeColor = Color.Green;
                _changed[33] = true;
            }


            //ComplementMzTolerance
            if (ComplementMzToleranceBox.Text ==
                Temp.ComplementMzTolerance.ToString())
            {
                ComplementMzToleranceLabel.ForeColor = Color.Black;
                _changed[34] = false;
            }
            else
            {
                ComplementMzToleranceLabel.ForeColor = Color.Green;
                _changed[34] = true;
            }


            ////CPUs
            //if (CPUsBox.Value ==
            //    ((decimal)Temp.CPUs))
            //{
            //    CPUsLabel.ForeColor = Color.Black;
            //    _changed[35] = false;
            //}
            //else
            //{
            //    CPUsLabel.ForeColor = Color.Green;
            //    _changed[35] = true;
            //}


            //MinSequenceMass
            if (MinSequenceMassBox.Value ==
                ((decimal)Temp.MinSequenceMass))
            {
                MinSequenceMassLabel.ForeColor = Color.Black;
                _changed[36] = false;
            }
            else
            {
                MinSequenceMassLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[36] = true;
                else
                    _changed[36] = false;
            }


            //MaxSequenceMass
            if (MaxSequenceMassBox.Value ==
                ((decimal)Temp.MaxSequenceMass))
            {
                MaxSequenceMassLabel.ForeColor = Color.Black;
                _changed[37] = false;
            }
            else
            {
                MaxSequenceMassLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[37] = true;
                else
                    _changed[37] = false;
            }


            //StartSpectraScanNum
            if (StartSpectraScanNumBox.Value ==
                ((decimal)Temp.StartSpectraScanNum))
            {
                StartSpectraScanNumLabel.ForeColor = Color.Black;
                _changed[38] = false;
            }
            else
            {
                StartSpectraScanNumLabel.ForeColor = Color.Green;
                _changed[38] = true;
            }


            //EndSpectraScanNum
            if (EndSpectraScanNumBox.Value ==
                ((decimal)Temp.EndSpectraScanNum))
            {
                EndSpectraScanNumLabel.ForeColor = Color.Black;
                _changed[39] = false;
            }
            else
            {
                EndSpectraScanNumLabel.ForeColor = Color.Green;
                _changed[39] = true;
            }


            //StartProteinIndex
            if (StartProteinIndexBox.Value ==
                ((decimal)Temp.StartProteinIndex))
            {
                StartProteinIndexLabel.ForeColor = Color.Black;
                _changed[40] = false;
            }
            else
            {
                StartProteinIndexLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[40] = true;
                else
                    _changed[40] = false;
            }


            //EndProteinIndex
            if (EndProteinIndexBox.Value ==
                ((decimal)Temp.EndProteinIndex))
            {
                EndProteinIndexLabel.ForeColor = Color.Black;
                _changed[41] = false;
            }
            else
            {
                EndProteinIndexLabel.ForeColor = Color.Green;
                if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    _changed[41] = true;
                else
                    _changed[41] = false;
            }


            //UseNETAdjustment
            if (UseNETAdjustmentBox.Checked ==
                Temp.UseNETAdjustment)
            {
                UseNETAdjustmentLabel.ForeColor = Color.Black;
                _changed[42] = false;
            }
            else
            {
                UseNETAdjustmentLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[42] = true;
                else
                    _changed[42] = true;
            }


            //ComputeXCorr
            if (ComputeXCorrBox.Checked ==
                Temp.ComputeXCorr)
            {
                ComputeXCorrLabel.ForeColor = Color.Black;
                _changed[43] = false;
            }
            else
            {
                ComputeXCorrLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[43] = true;
                else
                    _changed[43] = false;
            }


            //MassReconMode
            if (MassReconModeBox.Checked ==
                Temp.MassReconMode)
            {
                MassReconModeLabel.ForeColor = Color.Black;
                _changed[44] = false;
            }
            else
            {
                MassReconModeLabel.ForeColor = Color.Green;
                if (_destinationProgram == "TagRecon")
                    _changed[44] = true;
                else
                    _changed[44] = false;
            }


            //MaxPeakCount
            if (MaxPeakCountBox.Value ==
                ((Decimal)Temp.MaxPeakCount))
            {
                MaxPeakCountLabel.ForeColor = Color.Black;
                _changed[45] = false;
            }
            else
            {
                MaxPeakCountLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[45] = true;
                else
                    _changed[45] = false;
            }


            //TagLength
            if (TagLengthBox.Value ==
                ((Decimal)Temp.TagLength))
            {
                TagLengthLabel.ForeColor = Color.Black;
                _changed[46] = false;
            }
            else
            {
                TagLengthLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[46] = true;
                else
                    _changed[46] = false;
            }


            //IntensityScoreWeight
            if (IntensityScoreWeightBox.Value ==
                ((Decimal)Temp.IntensityScoreWeight))
            {
                IntensityScoreWeightLabel.ForeColor = Color.Black;
                _changed[47] = false;
            }
            else
            {
                IntensityScoreWeightLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[47] = true;
                else
                    _changed[47] = false;
            }


            //MzFidelityScoreWeight
            if (MzFidelityScoreWeightBox.Value ==
                ((Decimal)Temp.MzFidelityScoreWeight))
            {
                MzFidelityScoreWeightLabel.ForeColor = Color.Black;
                _changed[48] = false;
            }
            else
            {
                MzFidelityScoreWeightLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[48] = true;
                else
                    _changed[48] = false;
            }


            //ComplementScoreWeight
            if (ComplementScoreWeightBox.Value ==
                ((Decimal)Temp.ComplementScoreWeight))
            {
                ComplementScoreWeightLabel.ForeColor = Color.Black;
                _changed[49] = false;
            }
            else
            {
                ComplementScoreWeightLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[49] = true;
                else
                    _changed[49] = true;
            }

            //MaxTagCount
            if (MaxTagCountBox.Value ==
                ((Decimal)Temp.MaxTagCount))
            {
                MaxTagCountLabel.ForeColor = Color.Black;
                _changed[50] = false;
            }
            else
            {
                MaxTagCountLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[50] = true;
                else
                    _changed[50] = true;
            }

            //MaxTagScore
            if (MaxTagScoreBox.Text ==
                Temp.MaxTagScore.ToString())
            {
                MaxTagScoreLabel.ForeColor = Color.Black;
                _changed[51] = false;
            }
            else
            {
                MaxTagScoreLabel.ForeColor = Color.Green;
                if (_destinationProgram == "DirecTag")
                    _changed[51] = true;
                else
                    _changed[51] = true;
            }

        }

        private void LoadTemplate(object sender, EventArgs e)
        {
            object[] Values;
            string[] Explode;
            int TempNum;
            
            TempNum = ((int)sender);
            _skipAutomation = true;                   
            
            #region Load from Template
                //Instrument
                InstrumentBox.SelectedIndex = TempNum - 1;
                _currentTemplate = TempNum;

                //UseChargeStateFromMS
                UseChargeStateFromMSBox.Checked = _formTemplate[TempNum].UseChargeStateFromMS;

                //AdjustPrecursorMass
                AdjustPrecursorMassBox.Checked = _formTemplate[TempNum].AdjustPrecursorMass;
                if (AdjustPrecursorMassBox.Checked == true)
                    AdjustYes();
                else
                    AdjustNo();

                //DuplicateSpectra
                DuplicateSpectraBox.Checked = _formTemplate[TempNum].DuplicateSpectra;

                //UseSmartPlusThreeModel
                UseSmartPlusThreeModelBox.Checked = _formTemplate[TempNum].UseSmartPlusThreeModel;

                //CPUs
                //CPUsBox.Value = ((int)_formTemplate[TempNum].CPUs);

                //UseAvgMassOfSequences
                UseAvgMassOfSequencesBox.SelectedIndex = Convert.ToInt32(_formTemplate[TempNum].UseAvgMassOfSequences);
                
                //DeisotopingMode
                DeisotopingModeBox.SelectedIndex = _formTemplate[TempNum].DeisotopingMode;

                //NumMinTerminiCleavages
                NumMinTerminiCleavagesBox.SelectedIndex = _formTemplate[TempNum].NumMinTerminiCleavages;

                //StartSpectraScanNum
                StartSpectraScanNumBox.Value = _formTemplate[TempNum].StartSpectraScanNum;

                //StartProteinIndex
                StartProteinIndexBox.Value = _formTemplate[TempNum].StartProteinIndex;

                //NumMaxMissedCleavages
                NumMaxMissedCleavagesBox.Value = _formTemplate[TempNum].NumMaxMissedCleavages;

                //EndSpectraScanNum
                EndSpectraScanNumBox.Value = _formTemplate[TempNum].EndSpectraScanNum;

                //EndProteinIndex
                EndProteinIndexBox.Value = _formTemplate[TempNum].EndProteinIndex;

                //ProteinSampleSize
                ProteinSampleSizeBox.Value = _formTemplate[TempNum].ProteinSampleSize;

                //Modifications
                AppliedModDGV.Rows.Clear();
                foreach (string mod in _formTemplate[TempNum].Modifications)
                {
                    Values = new object[3];
                    Explode = mod.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    Values[0] = Explode[0];
                    Values[1] = Explode[1];
                    if (tagReconToolStripMenuItem.Checked == true || Explode[2] != "PreferredPTM")
                        Values[2] = Explode[2];
                    else
                    {
                        _preferredSave.Add(Explode[0]);
                        Values[2] = "Dynamic";
                        SoftMessageLabel.Text = "Some modifications had to be converted to \"Dynamic\"";
                        SoftMessageLabel.Location = new Point(50, 5);
                        SoftMessageFadeTimer.Enabled = true;
                    }
                    AppliedModDGV.Rows.Add(Values);
                }

                //MaxNumPreferredDeltaMasses
                MaxNumPreferredDeltaMassesBox.Value = _formTemplate[TempNum].MaxNumPreferredDeltaMasses;

                //MaxDynamicMods
                MaxDynamicModsBox.Value = _formTemplate[TempNum].MaxDynamicMods;

                //NumChargeStates
                NumChargeStatesBox.Value = _formTemplate[TempNum].NumChargeStates;

                //NumIntensityClasses
                NumIntensityClassesBox.Value = _formTemplate[TempNum].NumIntensityClasses;

                //MaxResults
                MaxResultsBox.Value = _formTemplate[TempNum].MaxResults;

                //MinSequenceMass
                MinSequenceMassBox.Value = ((Decimal)_formTemplate[TempNum].MinSequenceMass);

                //IsotopeMzTolerance
                IsotopeMzToleranceBox.Text = _formTemplate[TempNum].IsotopeMzTolerance.ToString();

                //FragmentMzTolerance
                FragmentMzToleranceBox.Text = _formTemplate[TempNum].FragmentMzTolerance.ToString();

                //ComplementMzTolerance
                ComplementMzToleranceBox.Text = _formTemplate[TempNum].ComplementMzTolerance.ToString();

                //TicCutoffPercentage
                TicCutoffPercentageBox.Value = ((Decimal)_formTemplate[TempNum].TicCutoffPercentage);

                //PrecursorMzTolerance
                PrecursorMzToleranceBox.Text = _formTemplate[TempNum].PrecursorMzTolerance.ToString();

                //MaxSequenceMass
                MaxSequenceMassBox.Value = ((Decimal)_formTemplate[TempNum].MaxSequenceMass);

                //ClassSizeMultiplier
                ClassSizeMultiplierBox.Value = ((Decimal)_formTemplate[TempNum].ClassSizeMultiplier);

                //MaxPrecursorAdjustment
                MaxPrecursorAdjustmentBox.Value = Math.Round(((decimal)(_formTemplate[TempNum].MaxPrecursorAdjustment / 1.008665)));

                //MinPrecursorAdjustment
                MinPrecursorAdjustmentBox.Value = Math.Round(((decimal)(_formTemplate[TempNum].MinPrecursorAdjustment / 1.008665)));

                //CleavageRules
                CleavageRulesBox.Text = _formTemplate[TempNum].CleavageRules;

                //PrecursorMzToleranceUnits
                PrecursorMzToleranceUnitsBox.Text = _formTemplate[TempNum].PrecursorMzToleranceUnits;

                //FragmentMzToleranceUnits
                FragmentMzToleranceUnitsBox.Text = _formTemplate[TempNum].FragmentMzToleranceUnits;

                //MassReconMode
                MassReconModeBox.Checked = _formTemplate[TempNum].MassReconMode;

                //Blosum
                BlosumBox.Text = _formTemplate[TempNum].Blosum;

                //BlosumThreshold
                BlosumThresholdBox.Value = ((Decimal)_formTemplate[TempNum].BlosumThreshold);

                //UnimodXML
                UnimodXMLBox.Text = _formTemplate[TempNum].UnimodXML;

                //MaxModificationMassPlus
                MaxModificationMassPlusBox.Value = ((Decimal)_formTemplate[TempNum].MaxModificationMassPlus);

                //MaxModificationMassMinus
                MaxModificationMassMinusBox.Value = ((Decimal)_formTemplate[TempNum].MaxModificationMassMinus);

                //NTerminusMzTolerance
                NTerminusMzToleranceBox.Text = _formTemplate[TempNum].NTerminusMzTolerance.ToString();

                //CTerminusMzTolerance
                CTerminusMzToleranceBox.Text = _formTemplate[TempNum].CTerminusMzTolerance.ToString();

                //MaxPeakCount
                MaxPeakCountBox.Value = ((Decimal)_formTemplate[TempNum].MaxPeakCount);

                //TagLength
                TagLengthBox.Value = ((Decimal)_formTemplate[TempNum].TagLength);

                //IntensityScoreWeight
                TagLengthBox.Value = ((Decimal)_formTemplate[TempNum].TagLength);

                //MzFidelityScoreWeight
                MzFidelityScoreWeightBox.Value = ((Decimal)_formTemplate[TempNum].MzFidelityScoreWeight);

                //ComplementScoreWeight
                ComplementScoreWeightBox.Value = ((Decimal)_formTemplate[TempNum].ComplementScoreWeight);

                //MaxTagCount
                MaxTagCountBox.Value = ((Decimal)_formTemplate[TempNum].MaxTagCount);

                //MaxTagScore
                MaxTagScoreBox.Text = _formTemplate[TempNum].MaxTagScore.ToString();

                //ExplainUnknownMassShiftsAs
                ExplainUnknownMassShiftsAsBox.Text = _formTemplate[TempNum].ExplainUnknownMassShiftsAs;

                //UseNETAdjustment
                UseNETAdjustmentBox.Checked = _formTemplate[TempNum].UseNETAdjustment;

                //ComputeXCorr
                ComputeXCorrBox.Checked = _formTemplate[TempNum].ComputeXCorr;

                //MinCandidateLength
                MinCandidateLengthBox.Value = ((Decimal)_formTemplate[TempNum].MinCandidateLength);

                #endregion

            _skipAutomation = false;
            ChangeCheck(_formTemplate[_currentTemplate]);
                
        }

#endregion
        

    #region: Validation

        private void NumUpDownBox_Leave(object sender, EventArgs e)
        {
            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private int IsValidModification(string Residue, string Mass, string Type, bool Passive)
        {
            /*******************************************
             *******************************************
             *****                                 *****
             ***** Returns Error code for result:  *****
             *****                                 *****
             ***** 0 - All Valid                   *****
             ***** 1 - Invalid Mod Character       *****
             ***** 2 - Invalid Mod Motif           *****
             ***** 3 - Mod Already Present         *****
             ***** 4 - Invalid Mod Mass            *****
             *****                                 *****
             *******************************************
             *******************************************/
            String Mod = Residue;
            bool SkipOne = Passive;
            double x;

            if (Type == "Static" && !_statRx.IsMatch(Residue))
            {
                if (!Passive)
                    MessageBox.Show("Invalid Residue Character");
                return 1;
            }

            else if (Type != "Static" && !_dynRx.IsMatch(Residue))
            {
                if (!Passive)
                    MessageBox.Show("Invalid Residue Motif");
                return 2;
            }

            for (int foo = 0; foo < AppliedModDGV.Rows.Count; foo++)
            {
                try
                {
                    if (AppliedModDGV.Rows[foo].Cells[0].Value.ToString() == Mod)
                    {
                        if (SkipOne)
                            SkipOne = false;
                        else
                        {
                            if (!Passive)
                                MessageBox.Show("Residue Motif already present in list.");
                            return 3;
                        }
                    }
                }
                catch
                {//null or otherwise unreadable value already caught
                }
            }

            if (double.TryParse(Mass, out x))
                return 0;
            else
                return 4;
        }

        private void StartSpectraScanNumBox_Leave(object sender, EventArgs e)
        {
            if (EndSpectraScanNumBox.Value != -1 && StartSpectraScanNumBox.Value > EndSpectraScanNumBox.Value)
            {
                MessageBox.Show("Must be less than End Spectra Scan Number if End Spectra Scan Number is not \"Auto\"");
                StartSpectraScanNumBox.Value = EndSpectraScanNumBox.Value;
            }
            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);

            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private void EndSpectraScanNumBox_Leave(object sender, EventArgs e)
        {
            if (EndSpectraScanNumBox.Value != -1 && StartSpectraScanNumBox.Value > EndSpectraScanNumBox.Value)
            {
                MessageBox.Show("Must be either \"Auto\" or greater than Start Spectra Scan Number");
                EndSpectraScanNumBox.Value = StartSpectraScanNumBox.Value;
            }

            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);

            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private void StartProteinIndexBox_Leave(object sender, EventArgs e)
        {
            if (EndProteinIndexBox.Value != -1 && StartProteinIndexBox.Value > EndProteinIndexBox.Value)
            {
                MessageBox.Show("Must be less than End Protein Index if End Protein Index is not \"Auto\"");
                StartProteinIndexBox.Value = EndProteinIndexBox.Value;
            }

            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);

            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private void EndProteinIndexBox_Leave(object sender, EventArgs e)
        {
            if (EndProteinIndexBox.Value != -1 && StartProteinIndexBox.Value > EndProteinIndexBox.Value)
            {
                MessageBox.Show("Must be either \"Auto\" or greater than Start Protein Index");
                EndProteinIndexBox.Value = StartProteinIndexBox.Value;
            }

            if (!_skipAutomation)
                ChangeCheck(_formTemplate[_currentTemplate]);

            ((NumericUpDown)sender).Value = Math.Round((((NumericUpDown)sender).Value));
        }

        private void MinSequenceMassBox_Leave(object sender, EventArgs e)
        {
            if (MinSequenceMassBox.Value > MaxSequenceMassBox.Value)
            {
                MessageBox.Show("Min Sequence Mass cannot be larger than Max Sequence Mass");
                MinSequenceMassBox.Value = MaxSequenceMassBox.Value;
            }
        }

        private void MaxSequenceMassBox_Leave(object sender, EventArgs e)
        {
            if (MaxSequenceMassBox.Value < MinSequenceMassBox.Value)
            {
                MessageBox.Show("Max Sequence Mass cannot be smaller than Min Sequence Mass");
                MaxSequenceMassBox.Value = MinSequenceMassBox.Value;
            }
        }

        private void ConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_cancelDialogResult)
            {
                e.Cancel = true;
                _cancelDialogResult = false;
            }
            else if (!_skipDiscardCheck && !ConfirmDiscard())
                e.Cancel = true;
        }

        private void NumericTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
   
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                if (!(e.KeyChar.Equals('-') && ((TextBox)sender).SelectionStart == 0 && !((TextBox)sender).Text.Contains('-')))
                {
                    if (!(e.KeyChar.Equals('.') && !((TextBox)sender).Text.Contains('.')))
                    {
                            e.Handled = true;
                    }
                }
            }

            if (((TextBox)sender).SelectionStart == 0 && ((TextBox)sender).SelectionLength == 0 && ((TextBox)sender).Text.Contains('-'))
                e.Handled = true;
        }

        private void NumericTextBox_Leave(object sender, EventArgs e)
        {
            try
            {
                ((TextBox)sender).Text = double.Parse(((TextBox)sender).Text).ToString();
            }
            catch
            {
                ((TextBox)sender).Text = string.Empty;
            }
        }

        private void ExplainUnknownMassShiftsAsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_skipAutomation)
            {
                bool KeepExplanation = false;

                if (_destinationProgram == "TagRecon" && ExplainUnknownMassShiftsAsBox.Text == "PreferredPTMs")
                {
                    for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                    {
                        if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                        {
                            KeepExplanation = true;
                            break;
                        }
                    }

                    if (!KeepExplanation)
                    {
                        _skipAutomation = true;
                        MessageBox.Show("Please select a list of preferred ptms in the general tab before setting to PreferredPTMs mode");
                        ExplainUnknownMassShiftsAsBox.Text = string.Empty;
                        _skipAutomation = false;
                    }
                }
            }
        }

        private bool ConfirmDiscard()
        {
            if (_fileChanged)
            {
                string MsgBoxString = "Do you want to save changes";
                if (string.IsNullOrEmpty(_configName))
                    MsgBoxString += "?";
                else
                    MsgBoxString += " to \"" + _configName.Substring(_configName.LastIndexOf('\\') + 1) + "\"?";

                switch (MessageBox.Show(MsgBoxString, "Save file?", MessageBoxButtons.YesNoCancel))
                {
                    case DialogResult.Yes:
                        EventArgs z = new EventArgs();
                        saveToolStripMenuItem_Click(0, z);
                        return true;

                    case DialogResult.No:
                        return true;

                    case DialogResult.Cancel:
                        return false;

                    default:
                        return false;
                }

            }
            else
                return true;
        }

        #endregion


    #region: Menu Items

        //Functions called by menu clicks

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //force validation and check for validity
            AdvModeBox.Select();
            if (!_validMods)
            {
                MessageBox.Show("Invalid config parameters, please correct all highlighted items");
                return;
            }

            if (string.IsNullOrEmpty(_configName))
                saveAsToolStripMenuItem_Click("Save", e);
            else
            {
                SaveToFile(_configName);
            }

        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //force validation and check for validity
            AdvModeBox.Select();
            if (!_validMods)
            {
                MessageBox.Show("Invalid config parameters, please correct all highlighted items");
                return;
            }

            SaveFileDialog Saveas = new SaveFileDialog();

            Saveas.Title = sender.ToString();
            Saveas.RestoreDirectory = true;
            Saveas.Filter = "Config Files (.cfg)|*.cfg";
            if (Saveas.ShowDialog().Equals(DialogResult.OK))
            {
                SaveToFile(Saveas.FileName.ToString());
                _configName = Saveas.FileName.ToString();
                _fileChanged = false;
            }

        }
 
        private void direcTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //force validation
            AdvModeBox.Select();

            _formTemplate[0].SetDirecTag();
            _destinationProgram = "DirecTag";
            if (string.IsNullOrEmpty(_configName))
                Text = "DirecTag Config Editor";
            else
                Text = "DirecTag Config Editor | " + _configName.Substring(_configName.LastIndexOf('\\') + 1);

            if (String.IsNullOrEmpty(_configName))
            {
                TicCutoffPercentageBox.Value = ((decimal)1);
                DeisotopingModeBox.SelectedIndex = 1;
                MaxResultsBox.Value = 30;
                _fileContents.DeisotopingMode = 1;
                _fileContents.TicCutoffPercentage = ((double)1);
                _fileContents.MaxResults = 30;
            }

            //main visibility settings
            TagReconGB.Visible = false;
            DirecTagGB.Visible = true;
            TagReconTolerancePanel.Visible = true;
            DTScorePanel.Visible = true;
            TRModOptionsGB.Visible = false;
            MaxNumPreferredDeltaMassesPannel.Visible = false;

            //other visibility settings
            StartProteinIndexBox.Visible = false;
            StartProteinIndexInfo.Visible = false;
            StartProteinIndexLabel.Visible = false;
            EndProteinIndexBox.Visible = false;
            EndProteinIndexInfo.Visible = false;
            EndProteinIndexLabel.Visible = false;
            SequenceGB.Visible = false;
            UseAvgMassOfSequencesBox.Visible = false;
            UseAvgMassOfSequencesInfo.Visible = false;
            UseAvgMassOfSequencesLabel.Visible = false;
            DigestionGB.Visible = false;
            MinCandidateLengthBox.Visible = false;
            MinCandidateLengthLabel.Visible = false;
            UseSmartPlusThreeModelBox.Visible = false;
            UseSmartPlusThreeModelInfo.Visible = false;
            UseSmartPlusThreeModelLabel.Visible = false;
            ProteinSampleSizeBox.Visible = false;
            ProteinSampleSizeInfo.Visible = false;
            ProteinSampleSizeLabel.Visible = false;

            if (PrecursorMzToleranceUnitsBox.Items.Count == 2)
            {
                PrecursorMzToleranceUnitsBox.Items.RemoveAt(1);
                FragmentMzToleranceUnitsBox.Items.RemoveAt(1);
                PrecursorMzToleranceUnitsBox.SelectedIndex = 0;
                FragmentMzToleranceUnitsBox.SelectedIndex = 0;
            }

            if (ModTypeBox.Items.Count == 3)
                ModTypeBox.Items.RemoveAt(2);
            ModTypeBox.SelectedIndex = 0;
            DataGridViewComboBoxColumn dgvcbc = AppliedModDGV.Columns[2] as DataGridViewComboBoxColumn;
            if (dgvcbc.Items.Count == 3)
                dgvcbc.Items.RemoveAt(2);

            for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
            {
                if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                {
                    _preferredSave.Add(AppliedModDGV.Rows[x].Cells[0].Value.ToString());
                    AppliedModDGV.Rows[x].Cells[2].Value = "Dynamic";
                    SoftMessageLabel.Text = "Some modifications had to be converted to \"Dynamic\"";
                    SoftMessageLabel.Location = new Point(50, 5);
                    SoftMessageFadeTimer.Enabled = true;
                }
            }

            //box size and position adjustments
            NTerminusMzToleranceUnitsBox.SelectedIndex = 0;
            CTerminusMzToleranceUnitsBox.SelectedIndex = 0;
            
            AdjustPrecursorMassLabel.Text = "Adjust for C13 errors:";
            AdjustPrecursorMassLabel.Location = new Point((int)(21 * _factorX), (int)(6 * _factorY));
            DTNewOptionsPanel.Parent = DirecTagGB;
            DTNewOptionsPanel.Location = new Point((int)(11 * _factorX), (int)(77 * _factorY));


            ChargeGB.Location = new Point((int)(282 * _factorX), (int)(6 * _factorY));
            InstrumentGB.Height = (int)(76*_factorY);
            ScoringGB.Location = new Point((int)(282 * _factorX), (int)(155 * _factorY));
            ScoringGB.Width = (int)(242*_factorX);
            ScoringGB.Height = (int)(151*_factorY);
            SubsetGB.Location = new Point((int)(282 * _factorX), (int)(86 * _factorY));
            SubsetGB.Width = (int)(242*_factorX);
            SubsetGB.Height = (int)(67*_factorY);
            MaxResultsPanel.Location = new Point((int)(4 * _factorX), (int)(7 * _factorY));
            MiscGB.Location = new Point((int)(282 * _factorX), (int)(307 * _factorY));
            MiscGB.Width = (int)(241*_factorX);
            MiscGB.Height = (int)(40*_factorY);
            ToleranceGB.Height = (int)(106*_factorY);
            ToleranceGB.Location = new Point((int)(7 * _factorX), (int)(6 * _factorY));
            PrecursorPannel.Location = new Point((int)(7 * _factorX), (int)(45 * _factorY));
            FragmentPannel.Location = new Point((int)(262 * _factorX), (int)(45 * _factorY));
            TagReconTolerancePanel.Location = new Point((int)(3 * _factorX), (int)(72 * _factorY));
            ModGB.Location = new Point((int)(7 * _factorX), (int)(120 * _factorY));
            tabControl1.Height = (int)(460*_factorY);
            this.Height = (int)(530*_factorY);

            InstrumentPannel.Parent = ToleranceGB;
            InstrumentPannel.Location = new Point((int)(167 * _factorX), (int)(10 * _factorY));
            InstrumentGB.Visible = false;
            ToleranceGB.Text = string.Empty;

            myrimatchToolStripMenuItem.Checked = false;
            tagReconToolStripMenuItem.Checked = false;
            direcTagToolStripMenuItem.Checked = true;

            ChangeCheck(_formTemplate[0]);
        }

        private void myrimatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //force validation
            AdvModeBox.Select();
            
            _formTemplate[0].SetMyricon();
            _destinationProgram = "MyriMatch";
            if (string.IsNullOrEmpty(_configName))
                Text = "MyriMatch Config Editor";
            else
                Text = "MyriMatch Config Editor | " + _configName.Substring(_configName.LastIndexOf('\\') + 1);

            if (String.IsNullOrEmpty(_configName))
            {
                TicCutoffPercentageBox.Value = ((decimal)0.98);
                DeisotopingModeBox.SelectedIndex = 0;
                MaxResultsBox.Value = 5;
                _fileContents.DeisotopingMode = 0;
                _fileContents.TicCutoffPercentage = ((double)0.98);
                _fileContents.MaxResults = 5;
            }

            TagReconGB.Visible = false;
            DirecTagGB.Visible = false;
            TagReconTolerancePanel.Visible = false;
            DTScorePanel.Visible = false;
            TRModOptionsGB.Visible = false;
            MaxNumPreferredDeltaMassesPannel.Visible = false;

            StartProteinIndexBox.Visible = true;
            StartProteinIndexInfo.Visible = true;
            StartProteinIndexLabel.Visible = true;
            EndProteinIndexBox.Visible = true;
            EndProteinIndexInfo.Visible = true;
            EndProteinIndexLabel.Visible = true;
            SequenceGB.Visible = true;
            UseAvgMassOfSequencesBox.Visible = true;
            UseAvgMassOfSequencesInfo.Visible = true;
            UseAvgMassOfSequencesLabel.Visible = true;
            DigestionGB.Visible = true;
            MinCandidateLengthBox.Visible = true;
            MinCandidateLengthLabel.Visible = true;
            UseSmartPlusThreeModelBox.Visible = true;
            UseSmartPlusThreeModelInfo.Visible = true;
            UseSmartPlusThreeModelLabel.Visible = true;
            ProteinSampleSizeBox.Visible = true;
            ProteinSampleSizeInfo.Visible = true;
            ProteinSampleSizeLabel.Visible = true;

            if (PrecursorMzToleranceUnitsBox.Items.Count == 1)
            {
                PrecursorMzToleranceUnitsBox.Items.Add("ppm");
                FragmentMzToleranceUnitsBox.Items.Add("ppm");
            }

            if (ModTypeBox.Items.Count == 3)
                ModTypeBox.Items.RemoveAt(2);
            ModTypeBox.SelectedIndex = 0;
            DataGridViewComboBoxColumn dgvcbc = AppliedModDGV.Columns[2] as DataGridViewComboBoxColumn;
            if (dgvcbc.Items.Count == 3)
                dgvcbc.Items.RemoveAt(2);

            for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
            {
                if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                {
                    _preferredSave.Add(AppliedModDGV.Rows[x].Cells[0].Value.ToString());
                    AppliedModDGV.Rows[x].Cells[2].Value = "Dynamic";
                    SoftMessageLabel.Text = "Some modifications had to be converted to \"Dynamic\"";
                    SoftMessageLabel.Location = new Point((int)(50 * _factorX), (int)(5 * _factorY));
                    SoftMessageFadeTimer.Enabled = true;
                }
            }

            //box size and position adjustments

            AdjustPrecursorMassLabel.Text = "Adjust Precursor Mass:";
            AdjustPrecursorMassLabel.Location = new Point((int)(11 * _factorX), (int)(6 * _factorY));
            DTNewOptionsPanel.Parent = MiscGB;
            DTNewOptionsPanel.Location = new Point((int)(5 * _factorX), (int)(63 * _factorY));

            ChargeGB.Location = new Point((int)(282 * _factorX), (int)(6 * _factorY));
            SequenceGB.Location = new Point((int)(282 * _factorX), (int)(86 * _factorY));
            ScoringGB.Location = new Point((int)(5 * _factorX), (int)(81 * _factorY));
            ScoringGB.Width = (int)(267*_factorX) ;
            ScoringGB.Height = (int)(68*_factorY);
            InstrumentGB.Height = (int)(117*_factorY);
            SubsetGB.Location = new Point((int)(5 * _factorX), (int)(154 * _factorY));
            SubsetGB.Width = (int)(267* _factorX);
            SubsetGB.Height = (int)(127*_factorY);
            MiscGB.Location= new Point((int)(282 * _factorX), (int)(180 * _factorY));
            MiscGB.Width = (int)(242*_factorX);
            MiscGB.Height = (int)(160*_factorY);
            ToleranceGB.Width = (int)(514*_factorX);
            ToleranceGB.Height = (int)(46*_factorY);
            ToleranceGB.Location = new Point((int)(7 * _factorX), (int)(129 * _factorY));
            PrecursorPannel.Location = new Point((int)(7 * _factorX), (int)(10 * _factorY));
            FragmentPannel.Location = new Point((int)(262 * _factorX), (int)(10 * _factorY));
            ModGB.Location = new Point((int)(7 * _factorX), (int)(181 * _factorY));
            tabControl1.Height = (int)(525*_factorY);
            this.Height = (int)(595*_factorY);

            InstrumentPannel.Parent = InstrumentGB;
            InstrumentPannel.Location = new Point((int)(39 * _factorX), (int)(30 * _factorY));
            InstrumentGB.Visible = true;
            ToleranceGB.Text = "Tolerance";

            myrimatchToolStripMenuItem.Checked = true;
            tagReconToolStripMenuItem.Checked = false;
            direcTagToolStripMenuItem.Checked = false;

            ChangeCheck(_formTemplate[0]);
        }

        private void tagReconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //force validation
            AdvModeBox.Select();

            _formTemplate[0].SetMyricon();
            _destinationProgram = "TagRecon";
            if (string.IsNullOrEmpty(_configName))
                Text = "TagRecon Config Editor";
            else
                Text = "TagRecon Config Editor | " + _configName.Substring(_configName.LastIndexOf('\\') + 1);

            if (String.IsNullOrEmpty(_configName))
            {
                
                TicCutoffPercentageBox.Value = ((decimal)0.98);
                DeisotopingModeBox.SelectedIndex = 0;
                MaxResultsBox.Value = 5;
                _fileContents.DeisotopingMode = 0;
                _fileContents.TicCutoffPercentage = ((double)0.98);
                _fileContents.MaxResults = 5;
            }

            TagReconGB.Visible = true;
            DirecTagGB.Visible = false;
            TagReconTolerancePanel.Visible = true;
            DTScorePanel.Visible = false;
            TRModOptionsGB.Visible = true;
            MaxNumPreferredDeltaMassesPannel.Visible = true;

            StartProteinIndexBox.Visible = true;
            StartProteinIndexInfo.Visible = true;
            StartProteinIndexLabel.Visible = true;
            EndProteinIndexBox.Visible = true;
            EndProteinIndexInfo.Visible = true;
            EndProteinIndexLabel.Visible = true;
            SequenceGB.Visible = true;
            UseAvgMassOfSequencesBox.Visible = true;
            UseAvgMassOfSequencesInfo.Visible = true;
            UseAvgMassOfSequencesLabel.Visible = true;
            DigestionGB.Visible = true;
            MinCandidateLengthBox.Visible = true;
            MinCandidateLengthLabel.Visible = true;
            UseSmartPlusThreeModelBox.Visible = true;
            UseSmartPlusThreeModelInfo.Visible = true;
            UseSmartPlusThreeModelLabel.Visible = true;
            ProteinSampleSizeBox.Visible = true;
            ProteinSampleSizeInfo.Visible = true;
            ProteinSampleSizeLabel.Visible = true;

            if (PrecursorMzToleranceUnitsBox.Items.Count == 2)
            {
                PrecursorMzToleranceUnitsBox.Items.RemoveAt(1);
                FragmentMzToleranceUnitsBox.Items.RemoveAt(1);
                PrecursorMzToleranceUnitsBox.SelectedIndex = 0;
                FragmentMzToleranceUnitsBox.SelectedIndex = 0;
            }

            if (ModTypeBox.Items.Count == 2)
                ModTypeBox.Items.Add("PreferredPTM");
            ModTypeBox.SelectedIndex = 0;

            DataGridViewComboBoxColumn dgvcbc = AppliedModDGV.Columns[2] as DataGridViewComboBoxColumn;
            if (dgvcbc.Items.Count == 2)
                dgvcbc.Items.Add("PreferredPTM");

            for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
            {
                if (_preferredSave.Contains(AppliedModDGV.Rows[x].Cells[0].Value.ToString()))
                {
                    AppliedModDGV.Rows[x].Cells[2].Value = "PreferredPTM";
                    SoftMessageLabel.Text = "Saved modifications have been restored to \"PreferredPTM\"";
                    SoftMessageLabel.Location = new Point((int)(25 * _factorX), (int)(5 * _factorY));
                    SoftMessageFadeTimer.Enabled = true;
                    ExplainUnknownMassShiftsAsBox.Text = "PreferredPTMs";
                }
            }
            _preferredSave.Clear();

            //box size and position adjustments
            NTerminusMzToleranceUnitsBox.SelectedIndex = 0;
            CTerminusMzToleranceUnitsBox.SelectedIndex = 0;

            AdjustPrecursorMassLabel.Text = "Adjust for C13 errors:";
            AdjustPrecursorMassLabel.Location = new Point(21, 6);
            DTNewOptionsPanel.Parent = MiscGB;
            DTNewOptionsPanel.Location = new Point((int)(5 * _factorX), (int)(63 * _factorY));

            ChargeGB.Location = new Point((int)(282 * _factorX), (int)(6 * _factorY));
            SubsetGB.Location = new Point((int)(282 * _factorX), (int)(86 * _factorY));
            SubsetGB.Width = (int)(242*_factorX);
            SubsetGB.Height = (int)(127*_factorY);
            ScoringGB.Location = new Point((int)(5 * _factorX), (int)(81 * _factorY));
            ScoringGB.Width = (int)(267*_factorX);
            ScoringGB.Height = (int)(70*_factorY);
            InstrumentGB.Height = (int)(117*_factorY);
            SequenceGB.Location = new Point((int)(281 * _factorX), (int)(216 * _factorY));
            MiscGB.Location = new Point((int)(281 * _factorX), (int)(310 * _factorY));
            MiscGB.Height = (int)(160*_factorY);
            MiscGB.Width = (int)(241*_factorX);
            ToleranceGB.Height = (int)(76*_factorY);
            ToleranceGB.Width = (int)(514*_factorX);
            ToleranceGB.Location = new Point((int)(8 * _factorX), (int)(130 * _factorY));
            PrecursorPannel.Location = new Point((int)(7 * _factorX), (int)(11 * _factorY));
            FragmentPannel.Location = new Point((int)(262 * _factorX), (int)(10 * _factorY));
            TagReconTolerancePanel.Location = new Point((int)(3 * _factorX), (int)(37 * _factorY));
            ModGB.Location = new Point((int)(8 * _factorX), (int)(212 * _factorY));
            tabControl1.Height = (int)(565*_factorY);
            this.Height = (int)(635*_factorY);

            InstrumentPannel.Parent = InstrumentGB;
            InstrumentPannel.Location = new Point((int)(39 * _factorX), (int)(30 * _factorY));
            InstrumentGB.Visible = true;
            ToleranceGB.Text = "Tolerance";

            myrimatchToolStripMenuItem.Checked = false;
            tagReconToolStripMenuItem.Checked = true;
            direcTagToolStripMenuItem.Checked = false;

            ChangeCheck(_formTemplate[0]);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        //Functions used by menu items
        private void UpdateTemplateMenuItems()
        {   
            string[] EntireFile;
            string[] EntireTemplate;
            string[] EntireInstruction;
            string[] Delimiter = new string[1];
            StreamReader cin;

            #region Open template file
            try
            {
                cin = new StreamReader("Templates.cfg");
            }
            catch
            {
                if (MessageBox.Show("Template file not found, would you like to create one?", "Template not found", MessageBoxButtons.YesNo).Equals(DialogResult.Yes))
                {
                    try
                    {
                        StreamWriter cout = new StreamWriter("Templates.cfg");
                        cout.Close();
                        cout.Dispose();
                        cin = new StreamReader("Templates.cfg");
                    }
                    catch
                    {
                        MessageBox.Show("Couldn't create file, check program folder for Templates.cfg");
                        Close();
                        return;
                    }
                }
                else
                {
                    Close();
                    return;
                }
            }
            #endregion

            //clear old menu
            InstrumentBox.Items.Clear();

            //find number of custom templates
            EntireFile = cin.ReadToEnd().Split(System.Environment.NewLine.ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
            cin.Close();
            _formTemplate = new Template[EntireFile.Length + 1];
            _formTemplate[0].SetAsDefaultTemplate();
            if (_destinationProgram == "DirecTag")
                _formTemplate[0].SetDirecTag();
            for (int TempNum = 1; TempNum <= EntireFile.Length; TempNum++)
            {
                Delimiter[0] = "||";
                EntireTemplate = EntireFile[TempNum-1].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                Delimiter[0] = "..";

                #region Set up template 
                try
                {
                    //name
                    _formTemplate[TempNum].Name = EntireTemplate[0];

                    //UseChargeStateFromMS
                    EntireInstruction = EntireTemplate[1].Split(Delimiter,StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].UseChargeStateFromMS = bool.Parse(EntireInstruction[1]);

                    //AdjustPrecursorMass
                    EntireInstruction = EntireTemplate[2].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].AdjustPrecursorMass = bool.Parse(EntireInstruction[1]);

                    //DuplicateSpectra
                    EntireInstruction = EntireTemplate[3].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].DuplicateSpectra = bool.Parse(EntireInstruction[1]);

                    //UseSmartPlusThreeModel
                    EntireInstruction = EntireTemplate[4].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].UseSmartPlusThreeModel = bool.Parse(EntireInstruction[1]);

                    //CPUs
                    EntireInstruction = EntireTemplate[5].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].CPUs = int.Parse(EntireInstruction[1]);

                    //UseAvgMassOfSequences
                    EntireInstruction = EntireTemplate[6].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].UseAvgMassOfSequences = bool.Parse(EntireInstruction[1]);

                    //DeisotopingMode
                    EntireInstruction = EntireTemplate[7].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].DeisotopingMode = int.Parse(EntireInstruction[1]);

                    //NumMinTerminiCleavages
                    EntireInstruction = EntireTemplate[8].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].NumMinTerminiCleavages = int.Parse(EntireInstruction[1]);

                    //StartSpectraScanNum
                    EntireInstruction = EntireTemplate[9].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].StartSpectraScanNum = int.Parse(EntireInstruction[1]);

                    //StartProteinIndex
                    EntireInstruction = EntireTemplate[10].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].StartProteinIndex = int.Parse(EntireInstruction[1]);

                    //NumMaxMissedCleavages
                    EntireInstruction = EntireTemplate[11].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].NumMaxMissedCleavages = int.Parse(EntireInstruction[1]);

                    //EndSpectraScanNum
                    EntireInstruction = EntireTemplate[12].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].EndSpectraScanNum = int.Parse(EntireInstruction[1]);

                    //EndProteinIndex
                    EntireInstruction = EntireTemplate[13].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].EndProteinIndex = int.Parse(EntireInstruction[1]);

                    //ProteinSampleSize
                    EntireInstruction = EntireTemplate[14].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].ProteinSampleSize = int.Parse(EntireInstruction[1]);

                    //MaxNumPreferredDeltaMasses
                    EntireInstruction = EntireTemplate[15].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxNumPreferredDeltaMasses = int.Parse(EntireInstruction[1]);

                    //Modifications
                    EntireInstruction = EntireTemplate[16].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].Modifications = new List<string>();
                    for (int x = 1; x < EntireInstruction.Length; x++)
                    {
                        _formTemplate[TempNum].Modifications.Add(EntireInstruction[x]);
                    }

                    //MaxDynamicMods
                    EntireInstruction = EntireTemplate[17].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxDynamicMods = int.Parse(EntireInstruction[1]);

                    //NumChargeStates
                    EntireInstruction = EntireTemplate[18].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].NumChargeStates = int.Parse(EntireInstruction[1]);

                    //NumIntensityClasses
                    EntireInstruction = EntireTemplate[19].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].NumIntensityClasses = int.Parse(EntireInstruction[1]);

                    //MaxResults
                    EntireInstruction = EntireTemplate[20].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxResults = int.Parse(EntireInstruction[1]);

                    //MinSequenceMass
                    EntireInstruction = EntireTemplate[21].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MinSequenceMass = double.Parse(EntireInstruction[1]);

                    //IsotopeMzTolerance
                    EntireInstruction = EntireTemplate[22].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].IsotopeMzTolerance = double.Parse(EntireInstruction[1]);

                    //FragmentMzTolerance
                    EntireInstruction = EntireTemplate[23].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].FragmentMzTolerance = double.Parse(EntireInstruction[1]);

                    //ComplementMzTolerance
                    EntireInstruction = EntireTemplate[24].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].ComplementMzTolerance = double.Parse(EntireInstruction[1]);

                    //TicCutoffPercentage
                    EntireInstruction = EntireTemplate[25].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].TicCutoffPercentage = double.Parse(EntireInstruction[1]);

                    //PrecursorMzTolerance
                    EntireInstruction = EntireTemplate[26].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].PrecursorMzTolerance = double.Parse(EntireInstruction[1]);

                    //MaxSequenceMass
                    EntireInstruction = EntireTemplate[27].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxSequenceMass = double.Parse(EntireInstruction[1]);

                    //ClassSizeMultiplier
                    EntireInstruction = EntireTemplate[28].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].ClassSizeMultiplier = double.Parse(EntireInstruction[1]);

                    //MaxPrecursorAdjustment
                    EntireInstruction = EntireTemplate[29].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxPrecursorAdjustment = double.Parse(EntireInstruction[1]);

                    //MinPrecursorAdjustment
                    EntireInstruction = EntireTemplate[30].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MinPrecursorAdjustment = double.Parse(EntireInstruction[1]);

                    //CleavageRules
                    EntireInstruction = EntireTemplate[31].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].CleavageRules = EntireInstruction[1];

                    //PrecursorMzToleranceUnits
                    EntireInstruction = EntireTemplate[32].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].PrecursorMzToleranceUnits = EntireInstruction[1];

                    //FragmentMzToleranceUnits
                    EntireInstruction = EntireTemplate[33].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].FragmentMzToleranceUnits = EntireInstruction[1];
                                     
                    //MassReconMode
                    EntireInstruction = EntireTemplate[34].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MassReconMode = bool.Parse(EntireInstruction[1]);

                    //Blosum
                    EntireInstruction = EntireTemplate[35].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    if (EntireInstruction.Length == 1)
                        _formTemplate[TempNum].Blosum = "Default";
                    else
                        _formTemplate[TempNum].Blosum = EntireInstruction[1];

                    //BlosumThreshold
                    EntireInstruction = EntireTemplate[36].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].BlosumThreshold = double.Parse(EntireInstruction[1]);

                    //UnimodXML
                    EntireInstruction = EntireTemplate[37].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    if (EntireInstruction.Length == 1)
                        _formTemplate[TempNum].UnimodXML = "Default";
                    else
                        _formTemplate[TempNum].UnimodXML = EntireInstruction[1];

                    //MaxModificationMassPlus
                    EntireInstruction = EntireTemplate[38].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxModificationMassPlus = double.Parse(EntireInstruction[1]);

                    //MaxModificationMassMinus
                    EntireInstruction = EntireTemplate[39].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxModificationMassMinus = double.Parse(EntireInstruction[1]);

                    //NTerminusMzTolerance
                    EntireInstruction = EntireTemplate[40].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].NTerminusMzTolerance = double.Parse(EntireInstruction[1]);

                    //CTerminusMzTolerance
                    EntireInstruction = EntireTemplate[41].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].CTerminusMzTolerance = double.Parse(EntireInstruction[1]);

                    //MaxPeakCount
                    EntireInstruction = EntireTemplate[42].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxPeakCount = int.Parse(EntireInstruction[1]);

                    //TagLength
                    EntireInstruction = EntireTemplate[43].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].TagLength = int.Parse(EntireInstruction[1]);

                    //IntensityScoreWeight
                    EntireInstruction = EntireTemplate[44].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].IntensityScoreWeight = double.Parse(EntireInstruction[1]);

                    //MzFidelityScoreWeight
                    EntireInstruction = EntireTemplate[45].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MzFidelityScoreWeight = double.Parse(EntireInstruction[1]);

                    //ComplementScoreWeight
                    EntireInstruction = EntireTemplate[46].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].ComplementScoreWeight = double.Parse(EntireInstruction[1]);
                   
                    //ExplainUnknownMassShiftsAs
                    EntireInstruction = EntireTemplate[47].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    if (EntireInstruction.Length == 1)
                        _formTemplate[TempNum].ExplainUnknownMassShiftsAs = string.Empty;
                    else
                        _formTemplate[TempNum].ExplainUnknownMassShiftsAs = EntireInstruction[1];

                    //UseNETAdjustment
                    EntireInstruction = EntireTemplate[48].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].UseNETAdjustment = bool.Parse(EntireInstruction[1]);

                    //ComputeXCorr
                    EntireInstruction = EntireTemplate[49].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].ComputeXCorr = bool.Parse(EntireInstruction[1]);

                    //MinCandidateLength
                    EntireInstruction = EntireTemplate[50].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MinCandidateLength = int.Parse(EntireInstruction[1]);

                    //MaxTagCount
                    EntireInstruction = EntireTemplate[51].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxTagCount = int.Parse(EntireInstruction[1]);

                    //MaxTagScore
                    EntireInstruction = EntireTemplate[52].Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
                    _formTemplate[TempNum].MaxTagScore = double.Parse(EntireInstruction[1]);

                }
                catch
                {
                    MessageBox.Show("Invalid template entry at line " + TempNum);
                    System.Environment.Exit(0);
                }
                #endregion

            }

            //populate menu and droplist items
            for (int x = (_formTemplate.Length-1); x > 0; x--)
                InstrumentBox.Items.Insert(0, _formTemplate[x].Name);



        }

        private string PepXMLtoEntireFileString(string CutFile)
        {
            #region Lists of valid paramaters
            string[] BoolList =
                {
                    "UseChargeStateFromMS",
                    "AdjustPrecursorMass",
                    "DuplicateSpectra",
                    "UseSmartPlusThreeModel",
                    "MassReconMode",
                    "UseNETAdjustment",
                    "ComputeXCorr",
                    "UseAvgMassOfSequences"
                };
            string[] NumberList =
            {
                "DeisotopingMode",
                "NumMinTerminiCleavages",
                "CPUs",
                "StartSpectraScanNum",
                "StartProteinIndex",
                "NumMaxMissedCleavages",
                "EndSpectraScanNum",
                "EndProteinIndex",
                "ProteinSampleSize",
                "MaxDynamicMods",
                "MaxNumPreferredDeltaMasses",
                "NumChargeStates",
                "NumIntensityClasses",
                "MaxResults",
                "MaxPeakCount",
                "TagLength",
                "MinCandidateLength",
                "MinSequenceMass",
                "IsotopeMzTolerance",
                "FragmentMzTolerance",
                "ComplementMzTolerance",
                "TicCutoffPercentage",
                "PrecursorMzTolerance",
                "MaxSequenceMass",
                "ClassSizeMultiplier",
                "MaxPrecursorAdjustment",
                "MinPrecursorAdjustment",
                "BlosumThreshold",
                "MaxModificationMassPlus",
                "MaxModificationMassMinus",
                "NTerminusMzTolerance",
                "CTerminusMzTolerance",
                "IntensityScoreWeight",
                "MzFidelityScoreWeight",
                "ComplementScoreWeight",
                "MaxTagCount",
                "MaxTagScore"
            };
            string[] StringList =
            {
                "CleavageRules",
                "PrecursorMzToleranceUnits",
                "FragmentMzToleranceUnits",
                "Blosum",
                "UnimodXML",
                "ExplainUnknownMassShiftsAs",
                "OutputSuffix",
                "StaticMods",
                "DynamicMods",
                "PreferredDeltaMasses"
            };
            #endregion
            string FormattedFile = string.Empty;
            string[] EntireLine = CutFile.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string[] Explode;

            for (int x = 0; x < EntireLine.Length; x++)
            {
                //get the two meaningful values
                EntireLine[x] = EntireLine[x].Replace("<parameter name=\"Config:", " ");
                EntireLine[x] = EntireLine[x].Replace("\" value=", " ");
                EntireLine[x] = EntireLine[x].Replace(" />", " ");
                Explode = EntireLine[x].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                Explode[0] = Explode[0].Trim();
                Explode[1] = Explode[1].Trim();

                //check if value is actually meaningful to the editor
                if (BoolList.Contains(Explode[0]))
                {
                    Explode[1] = Explode[1].Trim('\"');
                    Explode[1] = Convert.ToBoolean(int.Parse(Explode[1])).ToString().ToLower();
                    FormattedFile += Explode[0] + " = " + Explode[1] + System.Environment.NewLine;
                }
                if (NumberList.Contains(Explode[0]))
                {
                    Explode[1] = Explode[1].Trim('\"');
                    FormattedFile += Explode[0] + " = " + Explode[1] + System.Environment.NewLine;
                }
                if (StringList.Contains(Explode[0]))
                {
                    if (Explode.Length > 2)
                    {
                        for (int foo = 2; foo < Explode.Length; foo++)
                            Explode[1] += " " + Explode[foo];
                    }
                    FormattedFile += Explode[0] + " = " + Explode[1] + System.Environment.NewLine;
                }

            }


            return FormattedFile;
        }

        private string TagsFileToEntireFileString(string CutFile)
        {
            #region Lists of valid paramaters
            string[] BoolList =
                {
                    "UseChargeStateFromMS",
                    "AdjustPrecursorMass",
                    "DuplicateSpectra"
                };
            string[] NumberList =
            {
                "CPUs",
                "StartSpectraScanNum",
                "EndSpectraScanNum",
                "MaxDynamicMods",
                "NumChargeStates",
                "NumIntensityClasses",
                "MaxResults",
                "MaxPeakCount",
                "TagLength",
                "IsotopeMzTolerance",
                "FragmentMzTolerance",
                "TicCutoffPercentage",
                "PrecursorMzTolerance",
                "ClassSizeMultiplier",
                "MaxPrecursorAdjustment",
                "MinPrecursorAdjustment",
                "IntensityScoreWeight",
                "MzFidelityScoreWeight",
                "ComplementScoreWeight"
            };
            string[] StringList =
            {
                "StaticMods",
                "DynamicMods"
            };
            #endregion

            string FormattedFile = string.Empty;
            string[] EntireLine = CutFile.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string[] Explode;

            for (int x = 0; x < EntireLine.Length; x++)
            {
                //get the two meaningful values
                Explode = EntireLine[x].Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                Explode[0] = Explode[0].Trim();
                Explode[1] = Explode[1].Trim();

                //check if value is actually meaningful to the editor
                if (BoolList.Contains(Explode[0]))
                {
                    Explode[1] = Explode[1].Trim('\"');
                    Explode[1] = Convert.ToBoolean(int.Parse(Explode[1])).ToString().ToLower();
                    FormattedFile += Explode[0] + " = " + bool.Parse(Explode[1]).ToString().ToLower() + System.Environment.NewLine;
                }
                if (NumberList.Contains(Explode[0]))
                {
                    Explode[1] = Explode[1].Trim('\"');
                    FormattedFile += Explode[0] + " = " + Explode[1] + System.Environment.NewLine;
                }
                if (StringList.Contains(Explode[0]))
                {
                    if (Explode.Length > 2)
                    {
                        for (int foo = 2; foo < Explode.Length; foo++)
                            Explode[1] += " " + Explode[foo];
                    }
                    FormattedFile += Explode[0] + " = " + Explode[1] + System.Environment.NewLine;
                }

            }

            FormattedFile = FormattedFile.Trim();

            return FormattedFile;
        }

        private void SaveToFile(string filepath)
        {
            StreamWriter cout;
            string CommandGroup = string.Empty;
            string EntireFile = string.Empty;

            try
            {
                cout = new StreamWriter(filepath);
                Text = filepath.Substring(filepath.LastIndexOf('\\') + 1);
                if (!string.IsNullOrEmpty(_destinationProgram))
                    Text = _destinationProgram + " Config Editor | " + Text;
            }
            catch
            {
                MessageBox.Show("Cannot write to file, make sure it is not open");
                throw;
            }
            
            ChangeCheck(_formTemplate[0]);

            _fileContents.SetAsDefaultTemplate();
            if (_destinationProgram == "DirecTag")
                _fileContents.SetDirecTag();


            #region Search through form and find items that need to be written
            
                #region 1. Tolerance group

                //    if (Changed[0])
                //    {
                        CommandGroup += "PrecursorMzTolerance = " +
                            PrecursorMzToleranceBox.Text +
                            System.Environment.NewLine;
                        _fileContents.PrecursorMzTolerance = double.Parse(PrecursorMzToleranceBox.Text);
                //    }

                //    if (Changed[1])
                //    {
                        CommandGroup += "FragmentMzTolerance = " +
                            FragmentMzToleranceBox.Text +
                            System.Environment.NewLine;
                        _fileContents.FragmentMzTolerance = double.Parse(FragmentMzToleranceBox.Text);
                //    }

                    if (_destinationProgram == "MyriMatch")
                    {
                //        if (Changed[2])
                    //    {
                            CommandGroup += "PrecursorMzToleranceUnits = \"" +
                                PrecursorMzToleranceUnitsBox.Text.ToString() +
                                "\"" + System.Environment.NewLine;
                            _fileContents.PrecursorMzToleranceUnits = PrecursorMzToleranceUnitsBox.Text;
                    //    }

                //        if (Changed[3])
                    //    {
                            CommandGroup += "FragmentMzToleranceUnits = \"" +
                                FragmentMzToleranceUnitsBox.Text.ToString() +
                                "\"" + System.Environment.NewLine;
                            _fileContents.FragmentMzToleranceUnits = FragmentMzToleranceUnitsBox.Text;
                    //    }
                    }

                    if (_destinationProgram == "DirecTag")
                    {
                        //        if (Changed[4])
                        //    {
                        CommandGroup += "NTerminusMassTolerance = " +
                            NTerminusMzToleranceBox.Text +
                            System.Environment.NewLine;
                        _fileContents.NTerminusMzTolerance = double.Parse(NTerminusMzToleranceBox.Text);
                        //    }

                        //        if (Changed[5])
                        //    {
                        CommandGroup += "CTerminusMassTolerance = " +
                            CTerminusMzToleranceBox.Text +
                            System.Environment.NewLine;
                        _fileContents.CTerminusMzTolerance = double.Parse(CTerminusMzToleranceBox.Text);
                        //    }
                    }

                    if (_destinationProgram == "TagRecon")
                    {
                //        if (Changed[4])
                    //    {
                            CommandGroup += "NTerminusMzTolerance = " +
                                NTerminusMzToleranceBox.Text +
                                System.Environment.NewLine;
                            _fileContents.NTerminusMzTolerance = double.Parse(NTerminusMzToleranceBox.Text);
                    //    }

                //        if (Changed[5])
                    //    {
                            CommandGroup += "CTerminusMzTolerance = " +
                                CTerminusMzToleranceBox.Text +
                                System.Environment.NewLine;
                            _fileContents.CTerminusMzTolerance = double.Parse(CTerminusMzToleranceBox.Text);
                    //    }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 2. Precursor group
                    CommandGroup = string.Empty;

                    if (_changed[6])
                    {
                        CommandGroup += "AdjustPrecursorMass = true" +
                            System.Environment.NewLine;
                        _fileContents.AdjustPrecursorMass = AdjustPrecursorMassBox.Checked;

                        CommandGroup += "MaxPrecursorAdjustment = " +
                            ((double)MaxPrecursorAdjustmentBox.Value * 1.008665).ToString() +
                            System.Environment.NewLine;
                        _fileContents.MaxPrecursorAdjustment = ((double)MaxPrecursorAdjustmentBox.Value * 1.008665);

                        CommandGroup += "MinPrecursorAdjustment = " +
                            ((double)MinPrecursorAdjustmentBox.Value * 1.008665).ToString() +
                            System.Environment.NewLine;
                        _fileContents.MinPrecursorAdjustment = ((double)MinPrecursorAdjustmentBox.Value * 1.008665);

                        CommandGroup += "NumSearchBestAdjustments = " +
                            (Math.Round(((double)MaxPrecursorAdjustmentBox.Value) - ((double)MinPrecursorAdjustmentBox.Value)) +1).ToString() +
                            System.Environment.NewLine;
                        _fileContents.MinPrecursorAdjustment = ((double)MinPrecursorAdjustmentBox.Value * 1.008665);
                    }
            
                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 3. Instrument group
                    CommandGroup = string.Empty;

                    if (_changed[9])
                    {
                        CommandGroup += "DuplicateSpectra = " +
                            DuplicateSpectraBox.Checked.ToString().ToLower() +
                            System.Environment.NewLine;
                        _fileContents.DuplicateSpectra = DuplicateSpectraBox.Checked;
                    }

                    //if (Changed[10])
                    //{
                        CommandGroup += "UseChargeStateFromMS = " +
                            UseChargeStateFromMSBox.Checked.ToString().ToLower() +
                            System.Environment.NewLine;
                        _fileContents.UseChargeStateFromMS = UseChargeStateFromMSBox.Checked;
                    //}

                    //if (Changed[11])
                    //{
                        CommandGroup += "NumChargeStates = " +
                            NumChargeStatesBox.Value.ToString() +
                            System.Environment.NewLine;
                        _fileContents.NumChargeStates = ((int)NumChargeStatesBox.Value);
                    //}

                    //if (Changed[12])
                    //{
                        CommandGroup += "TicCutoffPercentage = " +
                            TicCutoffPercentageBox.Value.ToString() +
                            System.Environment.NewLine;
                        _fileContents.TicCutoffPercentage = ((double)TicCutoffPercentageBox.Value);
                    //}

                    if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    {
                        if (_changed[13])
                        {
                            CommandGroup += "UseSmartPlusThreeModel = " +
                                UseSmartPlusThreeModelBox.Checked.ToString().ToLower() +
                                System.Environment.NewLine;
                            _fileContents.UseSmartPlusThreeModel = UseSmartPlusThreeModelBox.Checked;
                        }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 4. Digestion group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    {
                    //    if (Changed[14])
                    //    {
                            CommandGroup += "CleavageRules = \"" +
                                    CleavageRulesBox.SelectedItem.ToString() + "\"" +
                                    System.Environment.NewLine;
                            _fileContents.CleavageRules = CleavageRulesBox.Text;
                    //    }

                    //    if (Changed[15])
                    //    {
                            CommandGroup += "NumMinTerminiCleavages = " +
                                    NumMinTerminiCleavagesBox.SelectedIndex.ToString() +
                                    System.Environment.NewLine;
                            _fileContents.NumMinTerminiCleavages = NumMinTerminiCleavagesBox.SelectedIndex;
                    //    }

                    //    if (Changed[16])
                    //    {
                            CommandGroup += "NumMaxMissedCleavages = " +
                                    NumMaxMissedCleavagesBox.Value.ToString() +
                                    System.Environment.NewLine;
                            _fileContents.NumMaxMissedCleavages = ((int)NumMaxMissedCleavagesBox.Value);
                    //    }

                        if (_changed[17])
                        {
                            CommandGroup += "UseAvgMassOfSequences = " +
                                    Convert.ToBoolean(UseAvgMassOfSequencesBox.SelectedIndex).ToString().ToLower() +
                                    System.Environment.NewLine;
                            _fileContents.UseAvgMassOfSequences = Convert.ToBoolean(UseAvgMassOfSequencesBox.SelectedIndex);
                        }

                        if (_changed[18])
                        {
                            CommandGroup += "MinCandidateLength = " +
                                    MinCandidateLengthBox.Value.ToString() +
                                    System.Environment.NewLine;
                            _fileContents.MinCandidateLength = ((int)MinCandidateLengthBox.Value);
                        }

                        if (!string.IsNullOrEmpty(CommandGroup))
                            EntireFile += CommandGroup + System.Environment.NewLine;

                    }

                #endregion

                #region 5. Modifications group
                    CommandGroup = string.Empty;
                    string TempString = string.Empty;

                 //   if (Changed[19] && DynamicModsList.Items.Count > 0)
                 //   {
                        CommandGroup += "DynamicMods = \"";
                        for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                        {
                            if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "Dynamic")
                            {
                                TempString += " " + AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " * " + AppliedModDGV.Rows[x].Cells[1].Value.ToString();
                                _fileContents.Modifications.Add(AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString() + " Dynamic");
                            }
                        }
                        CommandGroup += TempString.Trim() + "\"" + System.Environment.NewLine;
                        
                 //   }

                 //   if (Changed[20])
                 //   {
                        CommandGroup += "MaxDynamicMods = " +
                                    MaxDynamicModsBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.MaxDynamicMods = ((int)MaxDynamicModsBox.Value);
                 //   }

                    TempString = string.Empty;

                 //   if (Changed[21] && StaticModsList.Items.Count > 0)
                 //   {
                        CommandGroup += "StaticMods = \"";
                        for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                        {
                            if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "Static")
                            {
                                TempString += " " + AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString();
                                _fileContents.Modifications.Add(AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString() + " Static");
                            }
                        }
                        CommandGroup += TempString.Trim() + "\"" + System.Environment.NewLine;

                     TempString = string.Empty;

                 //   }

                        if (_destinationProgram == "TagRecon" && ExplainUnknownMassShiftsAsBox.Text == "PreferredPTMs")
                        {
                            CommandGroup += "PreferredDeltaMasses = \"";
                            for (int x = 0; x < AppliedModDGV.Rows.Count; x++)
                            {
                                if (AppliedModDGV.Rows[x].Cells[2].Value.ToString() == "PreferredPTM")
                                {
                                    TempString += String.Format(" {0} {1}", AppliedModDGV.Rows[x].Cells[0].Value, AppliedModDGV.Rows[x].Cells[1].Value);
                                    _fileContents.Modifications.Add(AppliedModDGV.Rows[x].Cells[0].Value.ToString() + " " + AppliedModDGV.Rows[x].Cells[1].Value.ToString() + " PreferredPTM");
                                }
                            }
                            CommandGroup += TempString.Trim() + "\"" + System.Environment.NewLine;

                            CommandGroup += "MaxNumPreferredDeltaMasses = " +
                                    MaxNumPreferredDeltaMassesBox.Value.ToString() +
                                    System.Environment.NewLine;
                            _fileContents.MaxDynamicMods = ((int)MaxNumPreferredDeltaMassesBox.Value);
                        }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;


                #endregion

                #region 6. Main TagRecon group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "TagRecon")
                    {
                        if (_changed[22])
                        {
                            CommandGroup += "ExplainUnknownMassShiftsAs = \"" +
                                        ExplainUnknownMassShiftsAsBox.Text.ToString().ToLower() +
                                         "\"" + System.Environment.NewLine;
                            _fileContents.ExplainUnknownMassShiftsAs = ExplainUnknownMassShiftsAsBox.Text;

                            if (ExplainUnknownMassShiftsAsBox.Text.ToString().ToLower() == "blindptms")
                            {
                                _changed[23] = true;
                                _changed[24] = true;

                            }
                            else if (ExplainUnknownMassShiftsAsBox.Text.ToString().ToLower() == "mutations")
                            {
                                _changed[27] = true;

                            }
                        }

                        if (_changed[23])
                        {
                            CommandGroup += "MaxModificationMassPlus = " +
                                        MaxModificationMassPlusBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MaxModificationMassPlus = ((Double)MaxModificationMassPlusBox.Value);
                        }

                        if (_changed[24])
                        {
                            CommandGroup += "MaxModificationMassMinus = " +
                                        MaxModificationMassMinusBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MaxModificationMassMinus = ((Double)MaxModificationMassMinusBox.Value);
                        }

                        //if (Changed[25])
                        //{
                        if (UnimodXMLBox.Text == "Default" || UnimodXMLBox.BackColor == Color.LightCoral)
                            UnimodXMLBox.Text = Application.StartupPath + @"\tagrecon\unimod.xml";
                        CommandGroup += "UnimodXML = \"" +
                            UnimodXMLBox.Text.ToString() +
                            "\"" + System.Environment.NewLine;
                        _fileContents.UnimodXML = UnimodXMLBox.Text;
                        //}
                        // Application.StartupPath + @"\tagrecon\blosum62.fas"
                        //if (Changed[26])
                        //{
                        if (BlosumBox.Text == "Default" || BlosumBox.BackColor == Color.LightCoral)
                            BlosumBox.Text = Application.StartupPath + @"\tagrecon\blosum62.fas";
                            CommandGroup += "Blosum = \"" +
                                        BlosumBox.Text.ToString() +
                                        "\"" + System.Environment.NewLine;
                            _fileContents.Blosum = BlosumBox.Text;
                        //}

                        if (_changed[27])
                        {
                            CommandGroup += "BlosumThreshold = " +
                                        BlosumThresholdBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.BlosumThreshold = ((Double)BlosumThresholdBox.Value);
                        }

                        if (!string.IsNullOrEmpty(CommandGroup))
                            EntireFile += CommandGroup + System.Environment.NewLine;

                    }

                #endregion

                #region 7. MaxResults group
                    CommandGroup = string.Empty;

                    //if (Changed[28])
                    //{
                        CommandGroup += "MaxResults = " +
                                    MaxResultsBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.MaxResults = ((int)MaxResultsBox.Value);
                        if (!string.IsNullOrEmpty(CommandGroup))
                            EntireFile += CommandGroup + System.Environment.NewLine;
                    //}
                #endregion

                #region 8. Misc group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    {
                        if (_changed[29])
                        {
                            CommandGroup += "ProteinSampleSize = " +
                                        ProteinSampleSizeBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.ProteinSampleSize = ((int)ProteinSampleSizeBox.Value);
                        }
                    }


                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 9. Classes group
                    CommandGroup = string.Empty;

                    if (_changed[30])
                    {
                        CommandGroup += "NumIntensityClasses = " +
                                    NumIntensityClassesBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.NumIntensityClasses = ((int)NumIntensityClassesBox.Value);
                    }

                    if (_changed[31])
                    {
                        CommandGroup += "ClassSizeMultiplier = " +
                                    ClassSizeMultiplierBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.ClassSizeMultiplier = ((int)ClassSizeMultiplierBox.Value);
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 10. Deisotoping group
                    CommandGroup = string.Empty;

                    if (_changed[32])
                    {
                        CommandGroup += "DeisotopingMode = " +
                                    DeisotopingModeBox.SelectedIndex.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.DeisotopingMode = DeisotopingModeBox.SelectedIndex;
                    }

                    if (_changed[33])
                    {
                        CommandGroup += "IsotopeMzTolerance = " +
                                    IsotopeMzToleranceBox.Text +
                                    System.Environment.NewLine;
                        _fileContents.IsotopeMzTolerance = double.Parse(IsotopeMzToleranceBox.Text);
                    }

                    if (_changed[34])
                    {
                        CommandGroup += "ComplementMzTolerance = " +
                                    ComplementMzToleranceBox.Text +
                                    System.Environment.NewLine;
                        _fileContents.ComplementMzTolerance = double.Parse(ComplementMzToleranceBox.Text);
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 11. System group
                    //CommandGroup = string.Empty;

                    //if (_changed[35])
                    //{
                    //    CommandGroup += "CPUs = " +
                    //                CPUsBox.Value.ToString() +
                    //                System.Environment.NewLine;
                    //    _fileContents.CPUs = ((int)CPUsBox.Value);
                    //}

                    //if (!string.IsNullOrEmpty(CommandGroup))
                    //    EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 12. Sequence Mass group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    {
                        if (_changed[36])
                        {
                            CommandGroup += "MinSequenceMass = " +
                                        MinSequenceMassBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MinSequenceMass = ((double)MinSequenceMassBox.Value);
                        }

                        if (_changed[37])
                        {
                            CommandGroup += "MaxSequenceMass = " +
                                        MaxSequenceMassBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MaxSequenceMass = ((double)MaxSequenceMassBox.Value);
                        }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 13. Subsetting group
                    CommandGroup = string.Empty;

                    if (_changed[38])
                    {
                        CommandGroup += "StartSpectraScanNum = " +
                                    StartSpectraScanNumBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.StartSpectraScanNum = ((int)StartSpectraScanNumBox.Value);
                    }

                    if (_changed[39])
                    {
                        CommandGroup += "EndSpectraScanNum = " +
                                    EndSpectraScanNumBox.Value.ToString() +
                                    System.Environment.NewLine;
                        _fileContents.EndSpectraScanNum = ((int)EndSpectraScanNumBox.Value);
                    }

                    if (_destinationProgram == "MyriMatch" || _destinationProgram == "TagRecon")
                    {
                        if (_changed[40])
                        {
                            CommandGroup += "StartProteinIndex = " +
                                        StartProteinIndexBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.StartProteinIndex = ((int)StartProteinIndexBox.Value);
                        }

                        if (_changed[41])
                        {
                            CommandGroup += "EndProteinIndex = " +
                                        EndProteinIndexBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.EndProteinIndex = ((int)EndProteinIndexBox.Value);
                        }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 14. Secondary TagRecon group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "TagRecon")
                    {
                        if (_changed[42])
                        {
                            CommandGroup += "UseNETAdjustment = " +
                                        UseNETAdjustmentBox.Checked.ToString().ToLower() +
                                        System.Environment.NewLine;
                            _fileContents.UseNETAdjustment = UseNETAdjustmentBox.Checked;
                        }

                        if (_changed[43])
                        {
                            CommandGroup += "ComputeXCorr = " +
                                        ComputeXCorrBox.Checked.ToString().ToLower() +
                                        System.Environment.NewLine;
                            _fileContents.ComputeXCorr = ComputeXCorrBox.Checked;
                        }

                        if (_changed[44])
                        {
                            CommandGroup += "MassReconMode = " +
                                        MassReconModeBox.Checked.ToString().ToLower() +
                                        System.Environment.NewLine;
                            _fileContents.MassReconMode = MassReconModeBox.Checked;
                        }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

                #region 15. DirecTag group
                    CommandGroup = string.Empty;

                    if (_destinationProgram == "DirecTag")
                    {
                        if (_changed[45])
                        {
                            CommandGroup += "MaxPeakCount = " +
                                        MaxPeakCountBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MaxPeakCount = ((int)MaxPeakCountBox.Value);
                        }

                        if (_changed[46])
                        {
                            CommandGroup += "TagLength = " +
                                        TagLengthBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.TagLength = ((int)TagLengthBox.Value);
                        }

                        if (_changed[47])
                        {
                            CommandGroup += "IntensityScoreWeight = " +
                                        IntensityScoreWeightBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.IntensityScoreWeight = ((double)IntensityScoreWeightBox.Value);
                        }

                        if (_changed[48])
                        {
                            CommandGroup += "MzFidelityScoreWeight = " +
                                        MzFidelityScoreWeightBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MzFidelityScoreWeight = ((double)MzFidelityScoreWeightBox.Value);
                        }

                        if (_changed[49])
                        {
                            CommandGroup += "ComplementScoreWeight = " +
                                        ComplementScoreWeightBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.ComplementScoreWeight = ((double)ComplementScoreWeightBox.Value);
                        }

                        if (_changed[50])
                        {
                            CommandGroup += "MaxTagCount = " +
                                        MaxTagCountBox.Value.ToString() +
                                        System.Environment.NewLine;
                            _fileContents.MaxTagCount = ((int)MaxTagCountBox.Value);
                        }

                        if (_changed[51])
                        {
                            CommandGroup += "MaxTagScore = " +
                                MaxTagScoreBox.Text +
                                System.Environment.NewLine;
                            _fileContents.MaxTagScore = double.Parse(MaxTagScoreBox.Text);
                        }
                    }

                    if (!string.IsNullOrEmpty(CommandGroup))
                        EntireFile += CommandGroup + System.Environment.NewLine;

                #endregion

            #endregion


            EntireFile = EntireFile.Trim();

            cout.Write(EntireFile);
            cout.Close();
            cout.Dispose();

            ChangeCheck(_formTemplate[_currentTemplate]);

        }

        private void OpenFromFile(string filePath)
        {
            string EntireFile = string.Empty;
            
            StreamReader cin;
            string TempString;

            
            try
            {
                cin = new StreamReader(filePath);
                
                if (!string.IsNullOrEmpty(_destinationProgram))
                    Text = _destinationProgram + " Config Editor | " + Text;

                if (Path.GetExtension(filePath).ToLower() == ".cfg")
                {
                    _configName = Path.GetFileName(filePath);
                    Text = _configName.Substring(_configName.LastIndexOf('\\') + 1);
                    EntireFile = cin.ReadToEnd();
                }
                else if (System.IO.Path.GetExtension(filePath).ToLower() == ".tags")
                {
                    while (!cin.EndOfStream)
                    {
                        TempString = cin.ReadLine();
                        if (TempString.Contains("TagsParameters"))
                        {
                            TempString = cin.ReadLine();
                            while (!string.IsNullOrEmpty(TempString))
                            {
                                TempString = TempString.Remove(0, 2);
                                EntireFile += TempString + ",";
                                TempString = cin.ReadLine();
                            }
                            EntireFile = EntireFile.Trim();
                            break;
                        }
                    }

                    EntireFile = TagsFileToEntireFileString(EntireFile);
                }
                else if (Path.GetExtension(filePath).ToLower() == ".pepxml")
                {
                    while (!cin.EndOfStream)
                    {
                        TempString = cin.ReadLine();
                        if (TempString.Contains("<parameter name=\"Config:"))
                            EntireFile += TempString + System.Environment.NewLine;
                        else if (EntireFile.Length > 0)
                            break;
                    }
                    EntireFile = PepXMLtoEntireFileString(EntireFile);
                    _configName = string.Empty;
                }
                else
                    return;
                
            }
            catch
            {
                MessageBox.Show("Could not read config file.");
                return;
            }

            cin.Close();
            cin.Dispose();
            
            _fileContents.SetAsDefaultTemplate();
            if (_destinationProgram == "DirecTag")
                _fileContents.SetDirecTag();

            _currentTemplate = 0;
            UpdateTemplateMenuItems();
            LoadTemplate(0, new EventArgs());

            

            _skipAutomation = true;
            OpenFromFileContents(EntireFile);
            _skipAutomation = false;

            ChangeCheck(_formTemplate[_currentTemplate]);
        }

        private void OpenFromFileContents(string EntireFile)
        {
            string[] EntireLine = EntireFile.Split(System.Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            double CheckForFloatSymbol;
            string TempString;
            string[] Explode;
            object[] Values;

            for (int foo = 0; foo< EntireLine.Length; foo++)
            {
                if (!string.IsNullOrEmpty(EntireLine[foo]))
                {

                    Explode = EntireLine[foo].Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    //Check for float char
                    TempString = Explode[1].Replace('f', ' ');
                    TempString.Trim();
                    if (double.TryParse(TempString, out CheckForFloatSymbol))
                        Explode[1] = TempString;

                    Explode[1] = Explode[1].Trim();
                    Explode[1] = Explode[1].Trim("\"".ToCharArray());

                    switch (Explode[0].Trim())
                    {
                        case "PrecursorMzTolerance":
                            PrecursorMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.PrecursorMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "FragmentMzTolerance":
                            FragmentMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.FragmentMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "PrecursorMzToleranceUnits":
                            PrecursorMzToleranceUnitsBox.Text = Explode[1];
                            _fileContents.PrecursorMzToleranceUnits = Explode[1];
                            break;

                        case "FragmentMzToleranceUnits":
                            FragmentMzToleranceUnitsBox.Text = Explode[1];
                            _fileContents.FragmentMzToleranceUnits = Explode[1];
                            break;

                        case "NTerminusMzTolerance":
                            NTerminusMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.NTerminusMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "CTerminusMzTolerance":
                            CTerminusMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.CTerminusMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "NTerminusMassTolerance":
                            NTerminusMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.NTerminusMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "CTerminusMassTolerance":
                            CTerminusMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.CTerminusMzTolerance = Double.Parse(Explode[1]);
                            break;

                        case "AdjustPrecursorMass":
                            AdjustPrecursorMassBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.AdjustPrecursorMass = bool.Parse(Explode[1]);
                            if (AdjustPrecursorMassBox.Checked == true)
                                AdjustYes();
                            else
                                AdjustNo();
                            break;

                        case "MaxPrecursorAdjustment":
                            MaxPrecursorAdjustmentBox.Value = (int)(Double.Parse(Explode[1]) / 1.008665);
                            _fileContents.MaxPrecursorAdjustment = Double.Parse(Explode[1]);
                            break;

                        case "MinPrecursorAdjustment":
                            MinPrecursorAdjustmentBox.Value = (int)(Double.Parse(Explode[1]) / 1.008665);
                            _fileContents.MinPrecursorAdjustment = Double.Parse(Explode[1]);
                            break;

                        case "DuplicateSpectra":
                            DuplicateSpectraBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.DuplicateSpectra = bool.Parse(Explode[1]);
                            break;

                        case "UseChargeStateFromMS":
                            UseChargeStateFromMSBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.UseChargeStateFromMS = bool.Parse(Explode[1]);
                            break;

                        case "NumChargeStates":
                            NumChargeStatesBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.NumChargeStates = int.Parse(Explode[1]);
                            break;

                        case "TicCutoffPercentage":
                            TicCutoffPercentageBox.Value = Math.Round(Decimal.Parse(Explode[1]),2);
                            _fileContents.TicCutoffPercentage = Math.Round(Double.Parse(Explode[1]),2);
                            break;

                        case "UseSmartPlusThreeModel":
                            UseSmartPlusThreeModelBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.UseSmartPlusThreeModel = bool.Parse(Explode[1]);
                            break;

                        case "CleavageRules":
                            CleavageRulesBox.Text = Explode[1];
                            _fileContents.CleavageRules = Explode[1];
                            break;

                        case "NumMinTerminiCleavages":
                            NumMinTerminiCleavagesBox.SelectedIndex = int.Parse(Explode[1]);
                            _fileContents.NumMinTerminiCleavages = int.Parse(Explode[1]);
                            break;

                        case "NumMaxMissedCleavages":
                            NumMaxMissedCleavagesBox.Value = int.Parse(Explode[1]);
                            _fileContents.NumMaxMissedCleavages = int.Parse(Explode[1]);
                            break;

                        case "UseAvgMassOfSequences":
                            UseAvgMassOfSequencesBox.SelectedIndex = Convert.ToInt32(bool.Parse(Explode[1]));
                            _fileContents.UseAvgMassOfSequences = bool.Parse(Explode[1]);
                            break;

                        case "MinCandidateLength":
                            MinCandidateLengthBox.Value = int.Parse(Explode[1]);
                            _fileContents.MinCandidateLength = int.Parse(Explode[1]);
                            break;

                        case "DynamicMods":
                            Explode = Explode[1].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            for (int x = 0; x + 2 < Explode.Length; x = x + 3)
                            {
                                Values = new object[3];
                                Values[0] = Explode[x];
                                Values[1] = Explode[x + 2];
                                Values[2] = "Dynamic";
                                AppliedModDGV.Rows.Add(Values);
                                _fileContents.Modifications.Add(Explode[x] + " " + Explode[x + 2] + " Dynamic");
                            }
                            break;

                        case "MaxDynamicMods":
                            MaxDynamicModsBox.Value = int.Parse(Explode[1]);
                            _fileContents.MaxDynamicMods = int.Parse(Explode[1]);
                            break;

                        case "MaxNumPreferredDeltaMasses":
                            MaxNumPreferredDeltaMassesBox.Value = int.Parse(Explode[1]);
                            _fileContents.MaxNumPreferredDeltaMasses = int.Parse(Explode[1]);
                            break;

                        case "StaticMods":
                            Explode = Explode[1].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            for (int x = 0; x + 1 < Explode.Length; x = x + 2)
                            {
                                Values = new object[3];
                                Values[0] = Explode[x];
                                Values[1] = Explode[x + 1];
                                Values[2] = "Static";
                                AppliedModDGV.Rows.Add(Values);
                                _fileContents.Modifications.Add(Explode[x] + " " + Explode[x+1] + " Static");
                            }
                            break;

                        case "PreferredDeltaMasses":
                            Explode = Explode[1].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            for (int x = 0; x + 1 < Explode.Length; x = x + 2)
                            {
                                Values = new object[3];
                                Values[0] = Explode[x];
                                Values[1] = Explode[x + 1];
                                if (_destinationProgram == "TagRecon")
                                    Values[2] = "PreferredPTM";
                                else
                                {
                                    _preferredSave.Add(Explode[x]);
                                    Values[2] = "Dynamic";
                                    SoftMessageLabel.Text = "Some modifications had to be converted to \"Dynamic\"";
                                    SoftMessageLabel.Location = new Point(50, 5);
                                    SoftMessageFadeTimer.Enabled = true;
                                }
                                AppliedModDGV.Rows.Add(Values);
                                _fileContents.Modifications.Add(Explode[x] + " " + Explode[x+1] + " PreferredPTM");
                            }
                            break;

                        case "ExplainUnknownMassShiftsAs":
                            switch (Explode[1])
                            {
                                case "blindptms":
                                    ExplainUnknownMassShiftsAsBox.Text = "BlindPTMs";
                                    break;
                                case "preferredptms":
                                    ExplainUnknownMassShiftsAsBox.Text = "PreferredPTMs";
                                    break;
                                case "mutations":
                                    ExplainUnknownMassShiftsAsBox.Text = "Mutations";
                                    break;
                            }
                            _fileContents.ExplainUnknownMassShiftsAs = ExplainUnknownMassShiftsAsBox.Text;
                            break;

                        case "MaxModificationMassPlus":
                            MaxModificationMassPlusBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.MaxModificationMassPlus = double.Parse(Explode[1]);
                            break;

                        case "MaxModificationMassMinus":
                            MaxModificationMassMinusBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.MaxModificationMassMinus = double.Parse(Explode[1]);
                            break;

                        case "UnimodXML":
                            if (Explode[1] == Application.StartupPath + @"\tagrecon\unimod.xml" || !File.Exists(Explode[1]) || !(new FileInfo(Explode[1])).Extension.Equals(".xml"))
                                Explode[1] = "Default";
                            UnimodXMLBox.Text = Explode[1];
                            _fileContents.UnimodXML = Explode[1];
                            break;

                        case "Blosum":
                            if (Explode[1] == Application.StartupPath + @"\tagrecon\blosum62.fas" || !File.Exists(Explode[1]) || !(new FileInfo(Explode[1])).Extension.Equals(".fas"))
                                Explode[1] = "Default";
                            BlosumBox.Text = Explode[1];
                            _fileContents.Blosum = Explode[1];
                            break;

                        case "BlosumThreshold":
                            BlosumThresholdBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.BlosumThreshold = Double.Parse(Explode[1]);
                            break;

                        case "MaxResults":
                            MaxResultsBox.Value = int.Parse(Explode[1]);
                            _fileContents.MaxResults = int.Parse(Explode[1]);
                            break;

                        case "ProteinSampleSize":
                            ProteinSampleSizeBox.Value = int.Parse(Explode[1]);
                            _fileContents.ProteinSampleSize = int.Parse(Explode[1]);
                            break;

                        case "NumIntensityClasses":
                            NumIntensityClassesBox.Value = int.Parse(Explode[1]);
                            _fileContents.NumIntensityClasses = int.Parse(Explode[1]);
                            break;

                        case "ClassSizeMultiplier":
                            ClassSizeMultiplierBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.ClassSizeMultiplier = Double.Parse(Explode[1]);
                            break;

                        case "DeisotopingMode":
                            DeisotopingModeBox.SelectedIndex = int.Parse(Explode[1]);
                            _fileContents.DeisotopingMode = int.Parse(Explode[1]);
                            break;

                        case "IsotopeMzTolerance":
                            IsotopeMzToleranceBox.Text = double.Parse(Explode[1]).ToString();
                            _fileContents.IsotopeMzTolerance = double.Parse(Explode[1]);
                            break;

                        case "ComplementMzTolerance":
                            ComplementMzToleranceBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.ComplementMzTolerance = Double.Parse(Explode[1]);
                            break;

                        //case "CPUs":
                        //    CPUsBox.Value = int.Parse(Explode[1]);
                        //    _fileContents.CPUs = int.Parse(Explode[1]);
                        //    break;

                        case "MinSequenceMass":
                            MinSequenceMassBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.MinSequenceMass = Double.Parse(Explode[1]);
                            break;

                        case "MaxSequenceMass":
                            MaxSequenceMassBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.MaxSequenceMass = Double.Parse(Explode[1]);
                            break;

                        case "StartSpectraScanNum":
                            StartSpectraScanNumBox.Value = int.Parse(Explode[1]);
                            _fileContents.StartSpectraScanNum = int.Parse(Explode[1]);
                            break;

                        case "EndSpectraScanNum":
                            EndSpectraScanNumBox.Value = int.Parse(Explode[1]);
                            _fileContents.EndSpectraScanNum = int.Parse(Explode[1]);
                            break;

                        case "StartProteinIndex":
                            StartProteinIndexBox.Value = int.Parse(Explode[1]);
                            _fileContents.StartProteinIndex = int.Parse(Explode[1]);
                            break;

                        case "EndProteinIndex":
                            EndProteinIndexBox.Value = int.Parse(Explode[1]);
                            _fileContents.EndProteinIndex = int.Parse(Explode[1]);
                            break;

                        case "UseNETAdjustment":
                            UseNETAdjustmentBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.UseNETAdjustment = bool.Parse(Explode[1]);
                            break;

                        case "ComputeXCorr":
                            ComputeXCorrBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.ComputeXCorr = bool.Parse(Explode[1]);
                            break;

                        case "MassReconMode":
                            MassReconModeBox.Checked = bool.Parse(Explode[1]);
                            _fileContents.MassReconMode = bool.Parse(Explode[1]);
                            break;

                        case "MaxPeakCount":
                            MaxPeakCountBox.Value = int.Parse(Explode[1]);
                            _fileContents.MaxPeakCount = int.Parse(Explode[1]);
                            break;

                        case "TagLength":
                            TagLengthBox.Value = int.Parse(Explode[1]);
                            _fileContents.TagLength = int.Parse(Explode[1]);
                            break;

                        case "IntensityScoreWeight":
                            IntensityScoreWeightBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.IntensityScoreWeight = Double.Parse(Explode[1]);
                            break;

                        case "MzFidelityScoreWeight":
                            MzFidelityScoreWeightBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.MzFidelityScoreWeight = Double.Parse(Explode[1]);
                            break;

                        case "ComplementScoreWeight":
                            ComplementScoreWeightBox.Value = Decimal.Parse(Explode[1]);
                            _fileContents.ComplementScoreWeight = Double.Parse(Explode[1]);
                            break;

                        case "MaxTagCount":
                            MaxTagCountBox.Value = int.Parse(Explode[1]);
                            _fileContents.MaxTagCount = int.Parse(Explode[1]);
                            break;

                        case "MaxTagScore":
                            MaxTagScoreBox.Text = Double.Parse(Explode[1]).ToString();
                            _fileContents.MaxTagScore = Double.Parse(Explode[1]);
                            break;
                    }
                }
            }
        }

        #endregion


    #region: DialogResult Buttons

        private void SaveOverOldButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveToFile(_saveDestinationPath);
                _configName = _saveDestinationPath;
                _skipDiscardCheck = true;
            }
            catch
            {
                _cancelDialogResult= true;
            }
        }

        private void SaveAsNewButton_Click(object sender, EventArgs e)
        {
            string newCfgFilePath;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.RestoreDirectory = true;
            sfd.InitialDirectory = _outputDirectory;
            sfd.CheckPathExists = true;
            sfd.DefaultExt = ".cfg";
            sfd.Filter = "Config File(.cfg)|*.cfg";
            sfd.AddExtension = true;
            sfd.FileName = String.Format("{0}.cfg", _destinationProgram);


            try
            {
                if (sfd.ShowDialog().Equals(DialogResult.OK))
                {
                    newCfgFilePath = sfd.FileName;
                    SaveToFile(newCfgFilePath);
                    _configName = newCfgFilePath;
                    _skipDiscardCheck = true;
                }
                else
                {
                    _cancelDialogResult = true;
                }
            }
            catch
            {
                _cancelDialogResult = true;
            }
        }

    #endregion

        private void UnimodXMLBox_Leave(object sender, EventArgs e)
        {
            if (UnimodXMLBox.Text != "Default" &&
                (string.IsNullOrEmpty(UnimodXMLBox.Text) ||
                !File.Exists(UnimodXMLBox.Text) ||
                !(new FileInfo(UnimodXMLBox.Text)).Extension.Equals(".xml")))
                UnimodXMLBox.BackColor = Color.LightCoral;
            else
                UnimodXMLBox.BackColor = Color.White;
        }

        private void BlosumBox_Leave(object sender, EventArgs e)
        {
            if (BlosumBox.Text != "Default" &&
                (string.IsNullOrEmpty(BlosumBox.Text) ||
                !File.Exists(BlosumBox.Text) ||
                !(new FileInfo(BlosumBox.Text)).Extension.Equals(".fas")))
                BlosumBox.BackColor = Color.LightCoral;
            else
                BlosumBox.BackColor = Color.White;
        }



    }

    public struct Template
    {
        public bool UseChargeStateFromMS,
            AdjustPrecursorMass,
            DuplicateSpectra,
            UseSmartPlusThreeModel,
            MassReconMode,
            UseNETAdjustment,
            ComputeXCorr,
            UseAvgMassOfSequences;

        public int DeisotopingMode,
            NumMinTerminiCleavages,
            CPUs,
            StartSpectraScanNum,
            StartProteinIndex,
            NumMaxMissedCleavages,
            EndSpectraScanNum,
            EndProteinIndex,
            ProteinSampleSize,
            MaxDynamicMods,
            MaxNumPreferredDeltaMasses,
            NumChargeStates,
            NumIntensityClasses,
            MaxResults,
            MaxPeakCount,
            TagLength,
            MinCandidateLength,
            MaxTagCount;

        public double MinSequenceMass,
            IsotopeMzTolerance,
            FragmentMzTolerance,
            ComplementMzTolerance,
            TicCutoffPercentage,
            PrecursorMzTolerance,
            MaxSequenceMass,
            ClassSizeMultiplier,
            MaxPrecursorAdjustment,
            MinPrecursorAdjustment,
            BlosumThreshold,
            MaxModificationMassPlus,
            MaxModificationMassMinus,
            NTerminusMzTolerance,
            CTerminusMzTolerance,
            IntensityScoreWeight,
            MzFidelityScoreWeight,
            ComplementScoreWeight,
            MaxTagScore;

        public string Name,
            CleavageRules,
            PrecursorMzToleranceUnits,
            FragmentMzToleranceUnits,
            Blosum,
            UnimodXML,
            ExplainUnknownMassShiftsAs,
            OutputSuffix;

        public List<string> Modifications;

        public void SetAsDefaultTemplate()
        {
            UseChargeStateFromMS = false;
            AdjustPrecursorMass = false;
            DuplicateSpectra = true;
            UseSmartPlusThreeModel = true;
            CPUs = 0;
            UseAvgMassOfSequences = true;
            DeisotopingMode = 0;
            NumMinTerminiCleavages = 2;
            StartSpectraScanNum = 0;
            StartProteinIndex = 0;
            NumMaxMissedCleavages = -1;
            EndSpectraScanNum = -1;
            EndProteinIndex = -1;
            ProteinSampleSize = 100;
            MaxDynamicMods = 2;
            MaxNumPreferredDeltaMasses = 1;
            NumChargeStates = 3;
            NumIntensityClasses = 3;
            MaxResults = 5;
            MinSequenceMass = 0;
            IsotopeMzTolerance = 0.25;
            FragmentMzTolerance = 0.5;
            ComplementMzTolerance = 0.5;
            TicCutoffPercentage = 0.98;
            PrecursorMzTolerance = 1.25;
            MaxSequenceMass = 10000;
            ClassSizeMultiplier = 2;
            MaxPrecursorAdjustment = 2.5;
            MinPrecursorAdjustment = -2.5;
            Name = "System Default";
            CleavageRules = "Trypsin/P";
            PrecursorMzToleranceUnits = "daltons";
            FragmentMzToleranceUnits = "daltons";
            OutputSuffix = String.Empty;
            Modifications = new List<string>();
            MassReconMode = false;
            Blosum = Application.StartupPath + @"\tagrecon\blosum62.fas";
            UnimodXML = Application.StartupPath + @"\tagrecon\unimod.xml";
            BlosumThreshold = 0;
            MaxModificationMassPlus = 300;
            MaxModificationMassMinus = 150;
            NTerminusMzTolerance = 0.75;
            CTerminusMzTolerance = 0.5;
            MaxPeakCount = 100;
            TagLength = 3;
            IntensityScoreWeight = 1;
            MzFidelityScoreWeight = 1;
            ComplementScoreWeight = 1;
            ExplainUnknownMassShiftsAs = "";
            UseNETAdjustment = false;
            ComputeXCorr = false;
            MinCandidateLength = 5;
            MaxTagScore = 20;
            MaxTagCount = 50;
        }

        public void SetMyricon()
        {
            TicCutoffPercentage = 0.98;
            DeisotopingMode = 0;
            MaxResults = 5;
        }

        public void SetDirecTag()
        {
            TicCutoffPercentage = 1;
            DeisotopingMode = 1;
            MaxResults = 30;
        }
    }

}