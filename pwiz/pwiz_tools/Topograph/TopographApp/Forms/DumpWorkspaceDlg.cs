using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DumpWorkspaceDlg : Form
    {
        public DumpWorkspaceDlg()
        {
            InitializeComponent();
            comboDatabaseType.SelectedIndex = 0;
        }
        public DatabaseTypeEnum DatabaseTypeEnum
        {
            get
            {
                return (DatabaseTypeEnum) Enum.Parse(typeof (DatabaseTypeEnum), (string) comboDatabaseType.SelectedItem);
            }
        }
    }
}
