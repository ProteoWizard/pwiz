using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TFExportTool.Properties;

namespace TFExportTool
{
    // ReSharper disable once InconsistentNaming
    public partial class TFExportDlg : Form
    {
        public TFExportDlg(List<String> filePaths)
        {
            InitializeComponent();

            Icon = Resources.icon;

            comboBoxStandard.SelectedIndex = 0;
            BindingSource bs = new BindingSource();
            bs.DataSource = filePaths;
            comboBoxRTFiles.DataSource = bs; // select rt from a file
            comboBoxRTFiles.DropDownWidth = DropDownWidth(comboBoxRTFiles);
            comboBoxDataSetIntensitySort.DataSource = bs; // sort by intensity from file
            comboBoxDataSetIntensitySort.DropDownWidth = DropDownWidth(comboBoxRTFiles);
        }

        // http://stackoverflow.com/questions/4842160/auto-width-of-comboboxs-content
        int DropDownWidth(ComboBox myCombo)
        {
            int maxWidth = 0;
            Label label = new Label();

            foreach (var obj in myCombo.Items)
            {
                label.Text = obj.ToString();
                int temp = label.PreferredWidth;
                if (temp > maxWidth)
                    maxWidth = temp;
            }
            label.Dispose();
            return maxWidth;
        }

        private void radioButtonAll_CheckedChanged(object sender, EventArgs e)
        {
           EnableDisable();
        }

        private void radioButtonOne_CheckedChanged(object sender, EventArgs e)
        {
            EnableDisable();
        }

        public bool IsAutoFill()
        {
            return radioButtonOne.Checked || radioButtonAll.Checked;
        }

        public bool IsAutoFillAll()
        {
            return radioButtonAll.Checked;
        }

        public int GetRtWindow()
        {
            return Decimal.ToInt16(numericUpDown1.Value);
        }
        public bool IsAutoFillConfirming()
        {
            return radioButtonConfirming.Checked;
        }

        public int GetStandardType()
        {
            return comboBoxStandard.SelectedIndex;
        }

        private void EnableDisable()
        {
            if (radioButtonAll.Checked)
            {
                radioButtonConfirming.Enabled = false;
                radioButtonFragment.Enabled = false;
                radioButtonFragment.Checked = false;
                radioButtonConfirming.Checked = false;
            }
            else
            {
                radioButtonConfirming.Enabled = true;
                radioButtonFragment.Enabled = true;
                radioButtonConfirming.Checked = true;
            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxRTFiles.Enabled = radioButtonUseDataSet.Checked;
        }

        public string GetSelectedRTFile()
        {
            if (radioButtonRTAvg.Checked)
                return null;
            return comboBoxRTFiles.Text;
        }
        public string GetSelectedIntensityFile()
        {
            if (radioButtonIntensityAvg.Checked)
                return null;
            return comboBoxDataSetIntensitySort.Text;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxDataSetIntensitySort.Enabled = radioButtonIntensityFromFile.Checked;
        }
    }
}
