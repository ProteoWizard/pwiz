using System.Runtime.InteropServices;

namespace pwiz.Common.SystemUtil.DllImport
{
    public static class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();
    }
}