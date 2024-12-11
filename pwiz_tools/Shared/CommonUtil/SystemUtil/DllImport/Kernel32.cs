using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace pwiz.Common.SystemUtil.DllImport
{
    public static class Kernel32
    {
        private const int ATTACH_PARENT_PROCESS = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(int dwProcessId);

        public static void AttachConsoleToParentProcess()
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
        }

        [DllImport("kernel32.dll")]
        public static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        public static extern SafeWaitHandle CreateWaitableTimer(IntPtr lpTimerAttributes,
            bool bManualReset,
            string lpTimerName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWaitableTimer(SafeWaitHandle hTimer,
            [In] ref long pDueTime,
            int lPeriod,
            IntPtr pfnCompletionRoutine,
            IntPtr lpArgToCompletionRoutine,
            bool fResume);
    }
}