using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.ui.Forms
{
    public partial class UpgradeWorkspaceForm : Form
    {
        public UpgradeWorkspaceForm(WorkspaceUpgrader workspaceUpgrader)
        {
            InitializeComponent();
        }
    }
}
