using pwiz.Common.GUI;
using pwiz.Skyline.Util;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{
    public class AlertDlg : CommonAlertDlg
    {
        private Helpers.ModeUIAwareFormHelper _modeUIHelper;
        public Helpers.ModeUIExtender ModeUIExtender; // Allows UI mode management in Designer
        private Container _components; // For IExtender use
        public Helpers.ModeUIAwareFormHelper GetModeUIHelper() // Method instead of property so it doesn't show up in Designer
        {
            return _modeUIHelper;
        }

        public AlertDlg()
        {
            InitializeComponent(); // Required for Windows Form Designer support
        }

        public AlertDlg(string message) : base(message)
        {
            InitializeComponent();
        }
        public AlertDlg(string message, MessageBoxButtons messageBoxButtons) : base(message, messageBoxButtons)
        {
            InitializeComponent();
        }

        public AlertDlg(string message, MessageBoxButtons messageBoxButtons, DialogResult defaultButton) : base(message, messageBoxButtons, defaultButton)
        {
            InitializeComponent();
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            _components = new Container();
            ModeUIExtender = new Helpers.ModeUIExtender(_components);
            _modeUIHelper = new Helpers.ModeUIAwareFormHelper(ModeUIExtender);
        }
        #endregion
        public DialogResult ShowAndDispose(IWin32Window parent)
        {
            using (this)
            {
                return ShowWithTimeout(parent, GetTitleAndMessageDetail());
            }
        }

        public DialogResult ShowWithTimeout(IWin32Window parent, string message)
        {
            Assume.IsNotNull(parent);   // Problems if the parent is null

            if (Program.FunctionalTest && Program.PauseSeconds == 0 && !Debugger.IsAttached)
            {
                bool timeout = false;
                var timeoutTimer = new Timer { Interval = TIMEOUT_SECONDS * 1000 };
                timeoutTimer.Tick += (sender, args) =>
                {
                    timeoutTimer.Stop();
                    if (!timeout)
                    {
                        timeout = true;
                        Close();
                    }
                };
                timeoutTimer.Start();

                var result = ShowDialog(parent);
                timeoutTimer.Stop();
                if (timeout)
                    throw new TimeoutException(
                        string.Format(@"{0} not closed for {1} seconds. Message = {2}",
                            GetType(),
                            TIMEOUT_SECONDS,
                            message));
                return result;
            }

            return ShowDialog(parent);
        }
        private const int TIMEOUT_SECONDS = 10;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            GetModeUIHelper().OnLoad(this);
        }
    }
}
