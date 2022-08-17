using System;
using System.Threading;
using System.Windows.Forms;
using JetBrains.Annotations;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.UserInterfaces
{
    public interface IUserInterface
    {
        void DisplayMessage(Control owner, string message);
        void DisplayMessageWithException(Control owner, string message, Exception exception);
        DialogResult DisplayMessageWithButtons(Control owner, string message, MessageBoxButtons messageBoxButtons, DialogResult defaultButton);

        /// <summary>
        /// Executes an action while displaying progress to the user and allowing the user to cancel.
        /// </summary>
        /// <param name="owner">Parent control for displaying message</param>
        /// <param name="action">Action to be performed</param>
        /// <param name="canRunOnBackground">If false, then the action must be invoked</param>
        /// <returns>false if the user canceled the operation before it completed. Throws an exception if the action threw an exception.</returns>
        bool RunLongOperation([CanBeNull] Control owner, Action<CancellationToken, IProgressMonitor> action, bool canRunOnBackground);
    }
}
