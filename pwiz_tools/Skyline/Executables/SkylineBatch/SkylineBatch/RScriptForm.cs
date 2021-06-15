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
    public partial class RScriptForm : Form
    {
        private static string _lastChosenVersion = null;


        public RScriptForm(string currentPath, string currentVersion)
        {
            InitializeComponent();
            Icon = Program.Icon();

            var sortedRVersions = Settings.Default.RVersions.Keys.ToList();
            sortedRVersions.Sort();
            foreach (var version in sortedRVersions)
                comboRVersions.Items.Add(version);
        }
    }
}
