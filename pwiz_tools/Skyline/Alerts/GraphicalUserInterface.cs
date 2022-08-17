using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Common.UserInterfaces;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public class GraphicalUserInterface : AbstractGui
    {
        public GraphicalUserInterface(Control parentControl) : base(parentControl)
        {
        }

        public override void DisplayMessage(Control owner, string message)
        {
            MessageDlg.Show(GetParentControl(owner), message);
        }

        public override void DisplayMessageWithException(Control owner, string message, Exception exception)
        {
            MessageDlg.ShowWithException(GetParentControl(owner), message, exception);
        }

        public override DialogResult DisplayMessageWithButtons(Control owner, string message, MessageBoxButtons messageBoxButtons, DialogResult defaultResult)
        {
            return new AlertDlg(message, messageBoxButtons, defaultResult).ShowAndDispose(GetParentControl(owner));
        }

        public override bool RunLongOperation(Control owner, Action<CancellationToken, IProgressMonitor> action, bool canRunOnBackground)
        {
            var longOperationRunner = new LongOperationRunner()
            {
                ExecutesJobOnBackgroundThread = canRunOnBackground,
                ParentControl = GetParentControl(owner)
            };
            bool finished = false;
            longOperationRunner.Run(longWaitBroker =>
            {
                var progressWaitBroker = new ProgressWaitBroker(progressMonitor => action(longWaitBroker.CancellationToken, progressMonitor));
                progressWaitBroker.PerformWork(longWaitBroker);
                finished = !longWaitBroker.IsCanceled;
            });
            return finished;
        }
    }
}
