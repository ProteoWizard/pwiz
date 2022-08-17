using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.UserInterfaces
{
    public class TextUserInterface : AbstractUserInterface
    {
        public static TextUserInterface GetDefault()
        {
            return new TextUserInterface(Console.Error);
        }
        public TextUserInterface(TextWriter writer)
        {
            Writer = writer;
        }

        public TextWriter Writer { get; }

        public override void DisplayMessage(Control owner, string message)
        {
            Writer.WriteLine(message);
        }

        public override void DisplayMessageWithException(Control owner, string message, Exception exception)
        {
            Writer.WriteLine(message);
            if (exception != null)
            {
                Writer.WriteLine(exception);
            }
        }

        public override DialogResult DisplayMessageWithButtons(Control owner, string message, MessageBoxButtons messageBoxButtons, DialogResult defaultButton)
        {
            Writer.WriteLine(message);
            return defaultButton;
        }

        public override bool RunLongOperation(Control owner, Action<CancellationToken, IProgressMonitor> action, bool canRunOnBackground)
        {
            action(CancellationToken.None, new SilentProgressMonitor());
            return true;
        }
    }
}
