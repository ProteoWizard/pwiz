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
        public IrtStandard Selected => comboStandards.SelectedItem as IrtStandard;

        public SelectIrtStandardDlg(IEnumerable<IrtStandard> standards)
        {
            InitializeComponent();

            comboStandards.Items.AddRange(standards.Cast<object>().ToArray());
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
