using System;
using System.Windows.Forms;

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
            checkedSaveConfigs.Items.AddRange(_configManager.ConfigNamesAsObjectArray());
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = TextUtil.FILTER_XML, FileName = textFileName.Text };
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
            catch (ArgumentException e)
            {
                _uiControl.DisplayError(e.Message);
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
