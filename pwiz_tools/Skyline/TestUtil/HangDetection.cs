using Microsoft.Diagnostics.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace pwiz.SkylineTestUtil
{
    public static class HangDetection
    {
        public static void InterruptWhenHung(Action action)
        {
            InterruptAfter(action, TimeSpan.FromSeconds(1), 1800);
        }
        
        public static void InterruptAfter(Action action, TimeSpan cycleDuration, int cycleCount)
        {
            bool[] completed = new bool[1];
            var currentThread = Thread.CurrentThread;
            try
            {
                ActionUtil.RunAsync(() =>
                    InterruptThreadAfter(completed, currentThread, cycleDuration, cycleCount), nameof(HangDetection));
                try
                {
                    action();
                }
                finally
                {
                    lock (completed)
                    {
                        completed[0] = true;
                        Monitor.Pulse(completed);
                    }
                }
            }
            catch (ThreadInterruptedException interruptedException)
            {
                var stringWriter = new StringWriter();
                stringWriter.WriteLine();
                stringWriter.WriteLine(GetThreadDump(Process.GetCurrentProcess().Id));
                stringWriter.WriteLine("*** End of thread dump");
                Console.Out.WriteLine("*** Hang detected. Thread dump:{0}", stringWriter);
                throw new AssertFailedException(string.Format("Timeout waiting {0} times {1} for action to complete", cycleDuration, cycleCount), interruptedException);
            }
        }

        private static void InterruptThreadAfter(bool[] completed, Thread thread, TimeSpan cycleDuration, int cycleCount)
        {
            lock (completed)
            {
                for (int i = 0; i < cycleCount; i++)
                {
                    lock (completed)
                    {
                        if (completed[0])
                        {
                            return;
                        }

                        Monitor.Wait(completed, cycleDuration);
                    }
                }

                if (!completed[0])
                {
                    thread.Interrupt();
                }
            }
        }

        public static string GetThreadDump(int processId)
        {
            var stringWriter = new StringWriter();
            try
            {
                using var dataTarget = DataTarget.AttachToProcess(processId, 60000, AttachFlag.Passive);
                var runtime = dataTarget.ClrVersions[0].CreateRuntime();

                foreach (var thread in runtime.Threads)
                {
                    if (!thread.IsAlive) continue;

                    stringWriter.WriteLine($"Thread {thread.OSThreadId:X} (Managed ID: {thread.ManagedThreadId})");

                    foreach (var frame in thread.EnumerateStackTrace())
                    {
                        stringWriter.WriteLine($"  {frame.Method?.Type?.Name}.{frame.Method?.Name ?? "[Unknown]"}");
                    }
                    stringWriter.WriteLine();
                }
            }
            catch (Exception exception)
            {
                stringWriter.WriteLine("Unable to dump all threads: {0}", exception);
            }

            return stringWriter.ToString();
        }
    }
}
