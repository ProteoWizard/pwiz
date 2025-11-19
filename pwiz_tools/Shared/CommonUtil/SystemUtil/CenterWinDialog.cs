// https://stackoverflow.com/a/2576220
// Winforms-How can I make MessageBox appear centered on MainForm?

using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

// DO NOT DELETE without due diligence on how this is used across all ProteoWizard projects.
// As of 12/31/2024, it is not used in Skyline so VisualStudio will say it's unreferenced,
// but it is used in MSConvertGUI which is separate from the Skyline solution.
//
// CONSIDER: move to pwiz_tools/MSConvertGUI
namespace pwiz.Common.SystemUtil
{
    public class CenterWinDialog : IDisposable
    {
        private int mTries;
        private Form mOwner;

        public CenterWinDialog(Form owner)
        {
            mOwner = owner;
            owner.BeginInvoke(new MethodInvoker(findDialog));
        }

        private void findDialog()
        {
            // Enumerate windows to find the message box
            if (mTries < 0) return;
            User32.EnumThreadWindowsProc callback = checkWindow;
            if (User32.EnumThreadWindows(Kernel32.GetCurrentThreadId(), callback, IntPtr.Zero))
            {
                if (++mTries < 10) mOwner.BeginInvoke(new MethodInvoker(findDialog));
            }
        }
        private bool checkWindow(IntPtr hWnd, IntPtr lp)
        {
            // Checks if <hWnd> is a dialog
            StringBuilder sb = new StringBuilder(260);
            User32.GetClassName(hWnd, sb, sb.Capacity);
            if (sb.ToString() != @"#32770") return true;
            // Got it
            Rectangle frmRect = new Rectangle(mOwner.Location, mOwner.Size);

            var dlgRect = new User32.RECT();
            User32.GetWindowRect(hWnd, ref dlgRect);
            User32.MoveWindow(hWnd,
                frmRect.Left + (frmRect.Width - dlgRect.right + dlgRect.left) / 2,
                frmRect.Top + (frmRect.Height - dlgRect.bottom + dlgRect.top) / 2,
                dlgRect.right - dlgRect.left,
                dlgRect.bottom - dlgRect.top, true);
            return false;
        }
        public void Dispose()
        {
            mTries = -1;
        }
    }
}