using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{
    public partial class UpgradeDlg : Form
    {
        public UpgradeDlg()
        {
            InitializeComponent();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://brendanx-uw1.gs.washington.edu/labkey/wiki/home/software/Skyline/page.view?name=LicenseAgreement");
        }
    }
}
