using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Common.UserInterfaces;
using pwiz.Topograph.ui.Forms;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui
{
    public class TopographUserInterface : AbstractGraphicalUserInterface
    {
        public TopographUserInterface(Control parent) : base(parent)
        {

        }


        public override void DisplayMessageWithException(Control owner, string message, Exception exception)
        {
            DisplayMessage(owner, message);
        }

        public override DialogResult DisplayMessageWithButtons(Control owner, string message, MessageBoxButtons messageBoxButtons,
            DialogResult defaultButton)
        {
            return MessageBox.Show(owner.TopLevelControl, message, null, messageBoxButtons);
        }

        public override bool RunLongOperation(Control owner, Action<CancellationToken, IProgressMonitor> job, bool canRunOnBackground)
        {
            if (!canRunOnBackground)
            {
                return base.RunLongOperation(owner, job, false);
            }
            using (var longWaitDialog = new LongWaitDialog(owner?.TopLevelControl, Program.AppName))
            {
                var longOperationBroker =
                    new LongOperationBroker(
                        broker =>
                            job.Invoke(CancellationToken.None, ProgressMonitorImpl.NewProgressMonitorImpl(new ProgressStatus("Working"),
                                iProgress =>
                                {
                                    try
                                    {
                                        broker.
                                            UpdateStatusMessage(
                                                iProgress + "% complete");
                                        return true;
                                    }
                                    catch (JobCancelledException)
                                    {
                                        return false;
                                    }
                                })), longWaitDialog);
                longOperationBroker.LaunchJob();
                return !longOperationBroker.WasCancelled;
            }
        }
    }
}
