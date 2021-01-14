using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class ShareConfigsForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly IMainUiControl _uiControl;


        public ShareConfigsForm(IMainUiControl uiControl, ConfigManager configManager)
        {
            InitializeComponent();
            _uiControl = uiControl;
            _configManager = configManager;
            checkedSaveConfigs.Items.AddRange(_configManager.GetConfigNames());
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog { Title = Resources.ConfigManager_Save_configurations, Filter = Resources.ConfigManager_XML_file_extension, FileName = textFileName.Text };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            textFileName.Text = dialog.FileName;
            Export();
        }

        private void checkedSaveConfigs_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Unchecked)
            {
                checkBoxSelectAll.Checked = false;
                return;
            }
            var finalLogChecked = checkedSaveConfigs.CheckedItems.Count == checkedSaveConfigs.Items.Count - 1;
            checkBoxSelectAll.Checked = finalLogChecked;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Export();
        }

        private void Export()
        {
            var indiciesToSave = new int[checkedSaveConfigs.CheckedIndices.Count];
            checkedSaveConfigs.CheckedIndices.CopyTo(indiciesToSave, 0);

            try
            {
                _configManager.ExportConfigs(textFileName.Text, indiciesToSave);
            }
            catch (ArgumentException)
            {
                _uiControl.DisplayError("Save Configuration Error", $"This is not a valid file path: \"{textFileName.Text}\"");
                return;
            }

            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void checkBoxSelectAll_Click(object sender, EventArgs e)
        {
            checkedSaveConfigs.ItemCheck -= checkedSaveConfigs_ItemCheck;
            for (int i = 0; i < checkedSaveConfigs.Items.Count; i++)
            {
                checkedSaveConfigs.SetItemChecked(i, checkBoxSelectAll.Checked);
            }
            checkedSaveConfigs.ItemCheck += checkedSaveConfigs_ItemCheck;
        }
    }
}
