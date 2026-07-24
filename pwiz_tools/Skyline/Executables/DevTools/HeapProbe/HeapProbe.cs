// Standalone probe: does showing/dismissing a native common dialog (and a native MessageBox)
// grow the process's Win32 heaps, with no Skyline code involved at all?
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class HeapProbe
{
    // ---- heap measurement: same technique TestRunner uses (walk every process heap, sum BUSY blocks) ----
    [DllImport("kernel32.dll")] static extern int GetProcessHeaps(int count, IntPtr[] heaps);
    [DllImport("kernel32.dll")] static extern bool HeapLock(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool HeapUnlock(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool HeapWalk(IntPtr h, ref PROCESS_HEAP_ENTRY e);

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_HEAP_ENTRY
    {
        public IntPtr lpData;
        public int cbData;
        public byte cbOverhead;
        public byte iRegionIndex;
        public ushort wFlags;
        public IntPtr hMem;
        public int dwReserved1, dwReserved2, dwReserved3;
    }
    const ushort BUSY = 0x0004;

    static long CommittedHeapBytes()
    {
        int count = GetProcessHeaps(0, null);
        var buffer = new IntPtr[count];
        GetProcessHeaps(count, buffer);
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            var h = buffer[i];
            HeapLock(h);
            var e = new PROCESS_HEAP_ENTRY();
            while (HeapWalk(h, ref e))
                if ((e.wFlags & BUSY) != 0)
                    total += e.cbData + e.cbOverhead;
            HeapUnlock(h);
        }
        return total;
    }

    // ---- window plumbing to auto-dismiss whatever dialog we put up ----
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, StringBuilder buf, int len);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    const uint WM_CLOSE = 0x0010;

    static IntPtr FindDialog()
    {
        IntPtr found = IntPtr.Zero;
        uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        EnumWindows((hwnd, lp) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            if (sb.ToString() != "#32770") return true;
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid != me) return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    // Dismisses the dialog once it appears, from a thread that is not the one running the modal loop.
    static void DismissWhenShown()
    {
        for (int i = 0; i < 400; i++)
        {
            var hwnd = FindDialog();
            if (hwnd != IntPtr.Zero) { Thread.Sleep(30); SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); return; }
            Thread.Sleep(10);
        }
        Console.WriteLine("  (no dialog found to dismiss)");
    }

    static void ShowSaveDialog()
    {
        var t = new Thread(DismissWhenShown); t.IsBackground = true; t.Start();
        using (var dlg = new SaveFileDialog())
        {
            dlg.InitialDirectory = System.IO.Path.GetTempPath();
            dlg.FileName = "probe.txt";
            dlg.ShowDialog();
        }
        t.Join(8000);
    }

    static void ShowMessageBox()
    {
        var t = new Thread(DismissWhenShown); t.IsBackground = true; t.Start();
        MessageBox.Show("probe body text", "Probe", MessageBoxButtons.OKCancel);
        t.Join(8000);
    }

    [STAThread]
    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "save";
        int iterations = args.Length > 1 ? int.Parse(args[1]) : 30;
        Action show = mode == "msgbox" ? (Action)ShowMessageBox : ShowSaveDialog;

        Console.WriteLine("mode=" + mode + " iterations=" + iterations);
        // Warm up: first invocation loads the shell/comdlg machinery, which is a one-time cost we do not want
        // to read as a leak.
        for (int i = 0; i < 3; i++) show();

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long baseline = CommittedHeapBytes();
        Console.WriteLine("baseline heap = " + (baseline / 1024) + " KB");

        var samples = new List<long>();
        for (int i = 1; i <= iterations; i++)
        {
            show();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long now = CommittedHeapBytes();
            samples.Add(now);
            Console.WriteLine(string.Format("{0,3}: heap = {1,8} KB   delta-from-baseline = {2,7} KB",
                i, now / 1024, (now - baseline) / 1024));
        }
        long growth = samples[samples.Count - 1] - baseline;
        Console.WriteLine();
        Console.WriteLine(string.Format("TOTAL growth over {0} iterations = {1} KB  ({2:F1} KB/iteration)",
            iterations, growth / 1024, growth / 1024.0 / iterations));
    }
}
