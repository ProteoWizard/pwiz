using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class AlertLinkDlg : FormEx
    {
        public static DialogResult Show(IWin32Window parent, string message, string linkMessage, string linkUrl)
        {
            using (var dlg = new AlertLinkDlg(message, linkMessage, linkUrl))
            {
                return dlg.ShowDialog(parent);
            }
        }

        public AlertLinkDlg(string message, string linkMessage, string linkUrl)
        {
            InitializeComponent();
            pictureBox1.Image = SystemIcons.Exclamation.ToBitmap();

            // set message and link
            labelMessage.Text = Message = message;
            labelLink.Text = linkMessage;
            LinkUrl = linkUrl;

            // adjust layout of dialog depending on size of message
            labelLink.SetBounds(labelLink.Left, labelMessage.Bottom + 10, labelLink.Width, labelLink.Height);
            btnOk.SetBounds(btnOk.Left, panel1.Bottom + 15, btnOk.Width, btnOk.Height);
        }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public string Message { get; private set; }
        public string LinkUrl { get; private set; }

        private void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void labelLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShowUrl(LinkUrl);
            OkDialog();
        }

        private void ShowUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception)
            {
                MessageDlg.Show(this, string.Format("Failure attempting to show a web browser for the URL\n{0}", url));
            }
        }
    }
}
