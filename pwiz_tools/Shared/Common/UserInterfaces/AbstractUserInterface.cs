using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.UserInterfaces
{
    public abstract class AbstractUserInterface : IUserInterface
    {
        public abstract void DisplayMessage(Control owner, string message);
        public abstract void DisplayMessageWithException(Control owner, string message, Exception exception);

        public abstract DialogResult DisplayMessageWithButtons(Control owner, string message, MessageBoxButtons messageBoxButtons,
            DialogResult defaultButton);

        public virtual bool RunLongOperation(Control owner, Action<CancellationToken, IProgressMonitor> action,
            bool canRunOnBackground)
        {
            action(CancellationToken.None, new SilentProgressMonitor());
            return true;
        }
    }
}
