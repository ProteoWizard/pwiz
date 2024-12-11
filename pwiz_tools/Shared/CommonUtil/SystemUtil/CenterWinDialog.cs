// https://stackoverflow.com/a/2576220
// Winforms-How can I make MessageBox appear centered on MainForm?

using System;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

// TODO (ekoneil): this class is unused and can be removed. Leaving win32 refs here for now.
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
            EnumThreadWndProc callback = checkWindow;
            if (EnumThreadWindows(DllImport.Kernel32.GetCurrentThreadId(), callback, IntPtr.Zero))
            {
                if (++mTries < 10) mOwner.BeginInvoke(new MethodInvoker(findDialog));
            }
        }
        private bool checkWindow(IntPtr hWnd, IntPtr lp)
        {
            // Checks if <hWnd> is a dialog
            StringBuilder sb = new StringBuilder(260);
            DllImport.User32.GetClassName(hWnd, sb, sb.Capacity);
            if (sb.ToString() != @"#32770") return true;
            // Got it
            Rectangle frmRect = new Rectangle(mOwner.Location, mOwner.Size);

            var dlgRect = new DllImport.User32.RECT();
            DllImport.User32.GetWindowRect(hWnd, ref dlgRect);
            DllImport.User32.MoveWindow(hWnd,
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

        // P/Invoke declarations
        private delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lp);
        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(int tid, EnumThreadWndProc callback, IntPtr lp);
    }
}
