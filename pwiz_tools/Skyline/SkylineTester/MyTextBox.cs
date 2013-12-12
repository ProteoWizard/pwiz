using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SkylineTester
{
    public class MyTextBox : RichTextBox
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetScrollPos(IntPtr hWnd, int nBar);

        [DllImport("user32.dll")]
        private static extern int SetScrollPos(IntPtr hWnd, int nBar, int nPos, bool bRedraw);

        private const int SB_HORZ = 0x0;
        private const int SB_VERT = 0x1;

        public int HorizontalPosition
        {
            get { return GetScrollPos(Handle, SB_HORZ); }
            set { SetScrollPos(Handle, SB_HORZ, value, true); }
        }

        public int VerticalPosition
        {
            get { return GetScrollPos(Handle, SB_VERT); }
            set { SetScrollPos(Handle, SB_VERT, value, true); }
        }
    }
}