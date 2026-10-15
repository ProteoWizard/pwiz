// https://stackoverflow.com/a/2576220
// Winforms-How can I make MessageBox appear centered on MainForm?

using System;
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
            // Enumerate this thread's windows to find the message box (a #32770 dialog) and center it.
            if (mTries < 0) return;
            bool found = false;
            foreach (var hWnd in User32.EnumThreadWindows((uint) Kernel32.GetCurrentThreadId()))
            {
                if (User32.GetClassName(hWnd) != @"#32770")
                    continue;
                // Got it: center the dialog on the owner form.
                Rectangle frmRect = new Rectangle(mOwner.Location, mOwner.Size);
                var dlgRect = new User32.RECT();
                User32.GetWindowRect(hWnd, ref dlgRect);
                User32.MoveWindow(hWnd,
                    frmRect.Left + (frmRect.Width - dlgRect.right + dlgRect.left) / 2,
                    frmRect.Top + (frmRect.Height - dlgRect.bottom + dlgRect.top) / 2,
                    dlgRect.right - dlgRect.left,
                    dlgRect.bottom - dlgRect.top, true);
                found = true;
                break;
            }
            // Not found yet (the box may not have appeared): try again a few times.
            if (!found && ++mTries < 10)
                mOwner.BeginInvoke(new MethodInvoker(findDialog));
        }
        public void Dispose()
        {
            mTries = -1;
        }
    }
}