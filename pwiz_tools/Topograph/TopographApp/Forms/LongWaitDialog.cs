using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public partial class LongWaitDialog : Form, ILongOperationUi
    {
        private bool _closed;
        public LongWaitDialog(IWin32Window parentWindow, String title)
        {
            InitializeComponent();
            ParentWindow = parentWindow;
            Text = title;
        }

        public LongOperationBroker LongOperationBroker
        {
            get; private set;
        }

        public IWin32Window ParentWindow { get; set; }
        
        public void DisplayLongOperationUi(LongOperationBroker broker)
        {
            LongOperationBroker = broker;
            timer1_Tick(timer1, new EventArgs());
            ShowDialog(ParentWindow);
        }

        public void UpdateLongOperationUi()
        {
        }

        public void LongOperationEnded()
        {
            try
            {
                if (IsHandleCreated && !_closed)
                {
                    BeginInvoke(new Action(Close));
                }
            }
            catch
            {
                // ignore
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!LongOperationBroker.Cancel())
            {
                e.Cancel = true;
                return;
            }
            _closed = true;
            LongOperationBroker.WaitUntilFinished();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (LongOperationBroker != null)
            {
                tbxMessage.Text = LongOperationBroker.StatusMessage;
                btnCancel.Enabled = LongOperationBroker.IsCancellable && !LongOperationBroker.WasCancelled;
            }
        }
    }
}
