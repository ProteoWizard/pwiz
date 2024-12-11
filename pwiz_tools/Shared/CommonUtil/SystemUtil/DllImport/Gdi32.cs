using System;
using System.Runtime.InteropServices;

namespace pwiz.Common.SystemUtil.DllImport
{
    public static class Gdi32
    { 
        public enum GDC
        {
            // ReSharper disable IdentifierTypo
            VERTRES = 10,
            DESKTOPVERTRES = 117
            // ReSharper restore IdentifierTypo
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    
        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern bool DeleteDC(IntPtr hDC);

        public static int GetDeviceCaps(IntPtr hdc, GDC flag)
        {
            return GetDeviceCaps(hdc, (int)flag);
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    }
}