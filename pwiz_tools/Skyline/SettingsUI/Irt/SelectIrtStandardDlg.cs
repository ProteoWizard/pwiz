using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public partial class SelectIrtStandardDlg : FormEx
    {
        public IrtStandard Selected
        {
            get => comboStandards.SelectedItem as IrtStandard;
            set => comboStandards.SelectedItem = value;
        }

        public IEnumerable<IrtStandard> Standards => comboStandards.Items.Cast<IrtStandard>();

        public SelectIrtStandardDlg(IEnumerable<IrtStandard> standards)
        {
            InitializeComponent();

            comboStandards.Items.AddRange(standards.Cast<object>().ToArray());
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }
    }
}
