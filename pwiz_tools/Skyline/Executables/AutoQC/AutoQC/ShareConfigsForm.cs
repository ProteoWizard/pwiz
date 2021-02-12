using System;
using System.Windows.Forms;
using SharedBatch;

namespace AutoQC
{
    public partial class ShareConfigsForm : Form
    {
        private readonly AutoQcConfigManager _configManager;
        private readonly IMainUiControl _uiControl;


        public ShareConfigsForm(IMainUiControl uiControl, AutoQcConfigManager configManager)
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
            var indiciesToSave = new int[checkedSaveConfigs.CheckedIndices.Count];
            checkedSaveConfigs.CheckedIndices.CopyTo(indiciesToSave, 0);

            try
            {
                _configManager.ExportConfigs(textFileName.Text, indiciesToSave);
            }
            catch (ArgumentException)
            {
                _uiControl.DisplayError($"This is not a valid file path: \"{textFileName.Text}\"");
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