using System;
using System.Drawing;
using System.Windows.Forms;
using SharedBatch.Properties;

namespace SharedBatch
{
    public partial class ShareConfigsForm : Form
    {
        private readonly IMainUiControl _uiControl;

        public ShareConfigsForm(IMainUiControl uiControl, ConfigManagerState state, Icon icon)
        {
            InitializeComponent();
            Icon = icon;
            _uiControl = uiControl;
            checkedSaveConfigs.Items.AddRange(state.ConfigNamesAsObjectArray());
        }

        public int[] IndiciesToSave { get; private set; }


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
            IndiciesToSave = new int[checkedSaveConfigs.CheckedIndices.Count];
            checkedSaveConfigs.CheckedIndices.CopyTo(IndiciesToSave, 0);
            if (IndiciesToSave.Length == 0)
            {
                _uiControl.DisplayError(Resources.ConfigManager_ExportConfigs_There_is_no_configuration_selected_ + Environment.NewLine +
                                        Resources.ConfigManager_ExportConfigs_Please_select_a_configuration_to_share_);
                return;
            }
            DialogResult = DialogResult.OK;
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
