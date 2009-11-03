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
        public LongWaitDialog(IWin32Window parentWindow)
        {
            InitializeComponent();
            ParentWindow = parentWindow;
        }

        public LongOperationBroker LongOperationBroker
        {
            get; private set;
        }

        public IWin32Window ParentWindow { get; set; }
        
        public void DisplayLongOperationUi(LongOperationBroker broker)
        {
            LongOperationBroker = broker;
            UpdateUi();
            ShowDialog(ParentWindow);
        }

        public void UpdateLongOperationUi()
        {
            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(UpdateUi));
                }
            }
            catch(Exception e)
            {
                Console.Out.WriteLine(e);
            }
        }

        private void UpdateUi()
        {
            tbxMessage.Text = LongOperationBroker.StatusMessage;
            btnCancel.Enabled = LongOperationBroker.IsCancellable && !LongOperationBroker.WasCancelled;
        }

        public void LongOperationEnded()
        {
            try
            {
                if (!IsDisposed)
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
            if (!LongOperationBroker.Cancel())
            {
                e.Cancel = true;
                return;
            }
            LongOperationBroker.WaitUntilFinished();            
        }
    }
}
