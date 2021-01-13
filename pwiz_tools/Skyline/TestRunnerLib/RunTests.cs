﻿/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using Exception = System.Exception;

namespace TestRunnerLib
{
    public class TestInfo
    {
        public readonly Type TestClassType;
        public readonly MethodInfo TestMethod;
        public readonly MethodInfo SetTestContext;
        public readonly MethodInfo TestInitialize;
        public readonly MethodInfo TestCleanup;
        public readonly bool IsPerfTest;
        public readonly int? MinidumpLeakThreshold;

        public TestInfo(Type testClass, MethodInfo testMethod, MethodInfo testInitializeMethod, MethodInfo testCleanupMethod)
        {
            TestClassType = testClass;
            TestMethod = testMethod;
            SetTestContext = testClass.GetMethod("set_TestContext");
            TestInitialize = testInitializeMethod;
            TestCleanup = testCleanupMethod;
            IsPerfTest = (testClass.Namespace ?? String.Empty).Equals("TestPerf");

            var minidumpAttr = RunTests.GetAttribute(testMethod, "MinidumpLeakThresholdAttribute");
            MinidumpLeakThreshold = minidumpAttr != null
                ? (int?) minidumpAttr.GetType().GetProperty("ThresholdMB")?.GetValue(minidumpAttr)
                : null;
        }
    }

    public class RunTests
    {
        private readonly Process _process;
        private readonly StreamWriter _log;
        private readonly bool _showStatus;
        private readonly bool _buildMode;

        public readonly TestContext TestContext;
        public CultureInfo Language = new CultureInfo("en-US");
        public long CheckCrtLeaks;
        public int FailureCount { get; private set; }
        public readonly Dictionary<string, int> ErrorCounts = new Dictionary<string, int>();
        public readonly Dictionary<string, int> FailureCounts = new Dictionary<string, int>();
        public InvokeSkyline Skyline { get; private set; }
        public int LastTestDuration { get; private set; }
        public int LastTotalHandleCount { get; private set; }
        public int LastGdiHandleCount { get; private set; }
        public int LastUserHandleCount { get; private set; }
        public long TotalMemoryBytes { get; private set; }
        public long CommittedMemoryBytes { get; private set; }
        public long ManagedMemoryBytes { get; private set; }
        public bool AccessInternet { get; set; }
        public bool RunPerfTests { get; set; }
        public bool RetryDataDownloads { get; set; }
        public bool RecordAuditLogs { get; set; }
        public bool RunsSmallMoleculeVersions { get; set; }
        public bool LiveReports { get; set; }
        public bool TeamCityTestDecoration { get; set; }
        public bool Verbose { get; set; }

        public bool ReportSystemHeaps
        {
            get { return !RunPerfTests; }   // 12-hour perf runs get much slower with system heap reporting
        }

        public static bool WriteMiniDumps
        {
            get { return false; }
        }

        public RunTests(
            bool demoMode,
            bool buildMode,
            bool offscreen,
            bool internet,
            bool showStatus,
            bool perftests,
            bool runsmallmoleculeversions,
            bool recordauditlogs,
            bool teamcityTestDecoration,
            bool retrydatadownloads,
            IEnumerable<string> pauseForms,
            int pauseSeconds = 0,
            int pauseStartingPage = 1,
            bool useVendorReaders = true,
            int timeoutMultiplier = 1,
            string results = null,
            StreamWriter log = null,
            bool verbose = false)
        {
            _buildMode = buildMode;
            _log = log;
            _process = Process.GetCurrentProcess();
            _showStatus = showStatus;
            TestContext = new TestRunnerContext();
            SetTestDir(TestContext, results);

            // Set Skyline state for unit testing.
            Skyline = new InvokeSkyline();
            Skyline.Set("StressTest", true);
            Skyline.Set("FunctionalTest", true);
            Skyline.Set("SkylineOffscreen", !demoMode && offscreen);
            Skyline.Set("DemoMode", demoMode);
            Skyline.Set("NoVendorReaders", !useVendorReaders);
            Skyline.Set("NoSaveSettings", true);
            Skyline.Set("UnitTestTimeoutMultiplier", timeoutMultiplier);
            Skyline.Set("PauseSeconds", pauseSeconds);
            Skyline.Set("PauseStartingPage", pauseStartingPage);
            Skyline.Set("PauseForms", pauseForms != null ? pauseForms.ToList() : null);
            Skyline.Set("Log", (Action<string>)(s => Log(s)));
            Skyline.Run("Init");

            AccessInternet = internet;
            RunPerfTests = perftests;
            RetryDataDownloads = retrydatadownloads; // When true, try re-downloading data files on test failure, in case the failure is due to stale data
            RunsSmallMoleculeVersions = runsmallmoleculeversions;  // Run the small molecule version of various tests?
            RecordAuditLogs = recordauditlogs; // Replace or create audit logs for tutorial tests
            LiveReports = true;
            TeamCityTestDecoration = teamcityTestDecoration;
            Verbose = verbose;

            // Disable logging.
            LogManager.GetRepository().Threshold = LogManager.GetRepository().LevelMap["OFF"];
        }

        private void SetTestDir(TestContext testContext, string resultsDir)
        {
            if (string.IsNullOrEmpty(resultsDir))
                resultsDir = Path.Combine(GetProjectPath("TestResults"), "TestRunner results");
            testContext.Properties["TestDir"] = resultsDir;
            if (Directory.Exists(resultsDir))
                Try<Exception>(() => Directory.Delete(resultsDir, true), 4, false);
            if (Directory.Exists(resultsDir))
                Log("!!! Couldn't delete results directory: {0}\n", resultsDir);
        }

        private static string GetProjectPath(string relativePath)
        {
            for (string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                 directory != null && directory.Length > 10;
                 directory = Path.GetDirectoryName(directory))
            {
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return Path.Combine(directory, relativePath);
            }
            return Path.GetFullPath(relativePath);
        }

        private static string GetAssemblyPath(string assembly)
        {
            var runnerExeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (runnerExeDirectory == null)
                throw new ApplicationException("Can't find path to TestRunner.exe");
            return Path.Combine(runnerExeDirectory, assembly);
        }

        public bool Run(TestInfo test, int pass, int testNumber, string dmpDir, bool heapOutput)
        {
            TeamCityStartTest(test);

            if (_showStatus)
                Log("#@ Running {0} ({1})...\n", test.TestMethod.Name, Language.TwoLetterISOLanguageName);

            if (_buildMode)
            {
                Log("{0,3}. {1,-46} ",
                    testNumber,
                    test.TestMethod.Name);
            }
            else
            {
                string testName;
                if (Verbose)
                    testName = test.TestClassType.Name + "." + test.TestMethod.Name;
                else
                    testName = test.TestMethod.Name;

                var time = DateTime.Now;
                Log("[{0}:{1}] {2,3}.{3,-3} {4,-46} ({5}) ",
                    time.Hour.ToString("D2"),
                    time.Minute.ToString("D2"),
                    pass,
                    testNumber,
                    testName,
                    Language.TwoLetterISOLanguageName);
            }

            Exception exception = null;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var saveCulture = Thread.CurrentThread.CurrentCulture;
            var saveUICulture = Thread.CurrentThread.CurrentUICulture;
            long crtLeakedBytes = 0;
            var testResultsDir = Path.Combine(TestContext.TestDir, test.TestClassType.Name);

            var dumpFileName = string.Format("{0}.{1}_{2}_{3}_{4:yyyy_MM_dd__hh_mm_ss_tt}.dmp", pass, testNumber, test.TestMethod.Name, Language.TwoLetterISOLanguageName, DateTime.Now);

            if (WriteMiniDumps && test.MinidumpLeakThreshold != null)
            {
                try
                {
                    if (string.IsNullOrEmpty(dmpDir))
                    {
                        dmpDir = Path.Combine(testResultsDir, "Minidumps");
                        Log("[WARNING] No log path provided - using test results dir ({0})", dmpDir);
                    }

                    Directory.CreateDirectory(dmpDir);

                    var path = Path.Combine(dmpDir, "pre_" + dumpFileName);
                    if (!MiniDump.WriteMiniDump(path))
                        Log("[WARNING] Failed to write pre mini dump to '{0}' (GetLastError() = {1})", path, Marshal.GetLastWin32Error());
                }
                catch(Exception ex)
                {
                    Log("[WARNING] Exception thrown when creating memory dump: {0}\r\n{1}\r\n", ex.InnerException?.Message ?? ex.Message, ex.InnerException?.StackTrace ?? ex.StackTrace);
                }
            }
                
            try
            {
                // Create test class.
                var testObject = Activator.CreateInstance(test.TestClassType);

                // Set the TestContext.
                TestContext.Properties["AccessInternet"] = AccessInternet.ToString();
                TestContext.Properties["RunPerfTests"] = RunPerfTests.ToString();
                TestContext.Properties["RetryDataDownloads"] = RetryDataDownloads.ToString();
                TestContext.Properties["RunSmallMoleculeTestVersions"] = RunsSmallMoleculeVersions.ToString(); // Run the AsSmallMolecule version of tests when available?
                TestContext.Properties["LiveReports"] = LiveReports.ToString();
                TestContext.Properties["TestName"] = test.TestMethod.Name;
                TestContext.Properties["TestRunResultsDirectory"] = testResultsDir;
                TestContext.Properties["RecordAuditLogs"] = RecordAuditLogs.ToString();

                if (test.SetTestContext != null)
                {
                    var context = new object[] { TestContext };
                    test.SetTestContext.Invoke(testObject, context);
                }

                // Switch to selected culture.
                LocalizationHelper.CurrentCulture = LocalizationHelper.CurrentUICulture = Language;
                LocalizationHelper.InitThread();

                // Run the test and time it.
                if (test.TestInitialize != null)
                    test.TestInitialize.Invoke(testObject, null);

                if (CheckCrtLeaks > 0)
                {
                    // TODO: CrtDebugHeap class used to be provided by Crawdad.dll
                    // If we ever want to enable this funcationality again, we need to find another .dll
                    // to put this in.
                    //CrtDebugHeap.Checkpoint();
                }
                test.TestMethod.Invoke(testObject, null);
                if (CheckCrtLeaks > 0)
                {
                    //crtLeakedBytes = CrtDebugHeap.DumpLeaks(true);
                }

                if (test.TestCleanup != null)
                    test.TestCleanup.Invoke(testObject, null);
            }
            catch (Exception e)
            {
                exception = e;
            }
            stopwatch.Stop();
            LastTestDuration = (int) stopwatch.ElapsedMilliseconds;
            // Allow as much to be garbage collected as possible

            // Restore culture.
            Thread.CurrentThread.CurrentCulture = saveCulture;
            Thread.CurrentThread.CurrentUICulture = saveUICulture;

            MemoryManagement.FlushMemory();
            _process.Refresh();
            var heapCounts = ReportSystemHeaps
                ? MemoryManagement.GetProcessHeapSizes(heapOutput ? dmpDir : null)
                : new MemoryManagement.HeapAllocationSizes[1];
            var processBytes = heapCounts[0].Committed; // Process heap : useful for debugging - though included in committed bytes
            var managedBytes = GC.GetTotalMemory(true); // Managed heap
            var committedBytes = heapCounts.Sum(h => h.Committed);
            ManagedMemoryBytes = managedBytes;
            CommittedMemoryBytes = committedBytes;
            var previousPrivateBytes = TotalMemoryBytes;
            TotalMemoryBytes = _process.PrivateMemorySize64;
            LastTotalHandleCount = GetHandleCount(HandleType.total);
            LastUserHandleCount = GetHandleCount(HandleType.user);
            LastGdiHandleCount = GetHandleCount(HandleType.gdi);

            if (WriteMiniDumps && test.MinidumpLeakThreshold != null)
            {
                try
                {
                    var leak = (TotalMemoryBytes - previousPrivateBytes) / MB;
                    if (leak > test.MinidumpLeakThreshold.Value)
                    {
                        var path = Path.Combine(dmpDir, "post_" + dumpFileName);
                        if (!MiniDump.WriteMiniDump(path))
                            Log("[WARNING] Failed to write post mini dump to '{0}' (GetLastError() = {1})", path, Marshal.GetLastWin32Error());
                    }
                    else
                    {
                        var prePath = Path.Combine(dmpDir, "pre_" + dumpFileName);
                      
                        var i = 5;
                        while (i-- > 0)
                        {
                            File.Delete(prePath);
                            if (!File.Exists(prePath))
                                break;
                            Thread.Sleep(200);
                        }
                    }
                }
                catch(Exception ex)
                {
                    Log("[WARNING] Exception thrown when creating memory dump: {0}\r\n{1}\r\n", ex.InnerException?.Message ?? ex.Message, ex.InnerException?.StackTrace ?? ex.StackTrace);
                }
            }

//            var handleInfos = HandleEnumeratorWrapper.GetHandleInfos();
//            var handleCounts = handleInfos.GroupBy(h => h.Type).OrderBy(g => g.Key);

            if (exception == null)
            {
                // Test succeeded.
                Log(ReportSystemHeaps
                        ? "{0,3} failures, {1:F2}/{2:F2}/{3:F1} MB, {4}/{5} handles, {6} sec.\r\n"
                        : "{0,3} failures, {1:F2}/{3:F1} MB, {4}/{5} handles, {6} sec.\r\n",
                    FailureCount, 
                    ManagedMemory,
                    CommittedMemory,
                    TotalMemory,
                    LastUserHandleCount + LastGdiHandleCount,
                    LastTotalHandleCount,
                    LastTestDuration/1000);
//                Log("# Heaps " + string.Join("\t", heapCounts.Select(s => s.ToString())) + Environment.NewLine);
//                Log("# Handles " + string.Join("\t", handleCounts.Where(c => c.Count() > 14).Select(c => c.Key + ": " + c.Count())) + Environment.NewLine);
                if (crtLeakedBytes > CheckCrtLeaks)
                    Log("!!! {0} CRT-LEAKED {1} bytes\r\n", test.TestMethod.Name, crtLeakedBytes);

                if (heapOutput && ReportSystemHeaps)
                {
                    const int sizeOutputs = 50;
                    const int stringOutputs = 50;
                    var allSizes = new List<Tuple<int, long, int>>();
                    var allStrings = new List<Tuple<int, string, int>>();
                    for (int i = 0; i < heapCounts.Length; i++)
                    {
                        var heapCount = heapCounts[i];
                        allSizes.AddRange(heapCount.CommittedSizes.Take(sizeOutputs).Select(p =>
                            new Tuple<int, long, int>(i, p.Key, p.Value)));
                        allStrings.AddRange(heapCount.StringCounts.Take(stringOutputs).Select(p =>
                            new Tuple<int, string, int>(i, p.Key, p.Value)));
                    }

                    var sizeText = allSizes.OrderByDescending(s => s.Item3).Take(sizeOutputs)
                        .Select(s => string.Format("{0}:{1}:{2}", s.Item1, s.Item2, s.Item3));
                    Log("# HEAP SIZES (top {0}) - {1}\r\n", sizeOutputs, string.Join(", ", sizeText));
                    var stringText = allStrings.OrderByDescending(s => s.Item3).Take(stringOutputs)
                        .Select(s => string.Format("{0}:\"{1}\":{2}", s.Item1, s.Item2, s.Item3));
                    Log("# HEAP STRINGS (top {0}) - {1}\r\n", stringOutputs, string.Join(", ", stringText));
                }

                TeamCityFinishTest(test);

                return true;
            }

            // Save failure information.
            FailureCount++;
            if (FailureCounts.ContainsKey(test.TestMethod.Name))
                FailureCounts[test.TestMethod.Name]++;
            else
                FailureCounts[test.TestMethod.Name] = 1;
            var message = exception.InnerException == null ? exception.Message : exception.InnerException.Message;
            var stackTrace = exception.InnerException == null ? exception.StackTrace : exception.InnerException.StackTrace;
            var failureInfo = "# " + test.TestMethod.Name + "FAILED:\n" +
                message + "\n" +
                stackTrace;
            if (ErrorCounts.ContainsKey(failureInfo))
                ErrorCounts[failureInfo]++;
            else
                ErrorCounts[failureInfo] = 1;

            TeamCityFinishTest(test, message + '\n' + stackTrace);

            Log("{0,3} failures, {1:F2}/{2:F2}/{3:F1} MB, {4}/{5} handles, {6} sec.\r\n\r\n!!! {7} FAILED\r\n{8}\r\n{9}\r\n!!!\r\n\r\n",
                FailureCount,
                ManagedMemory,
                CommittedMemory,
                TotalMemory,
                LastUserHandleCount + LastGdiHandleCount,
                LastTotalHandleCount,
                LastTestDuration/1000,
                test.TestMethod.Name,
                message,
                exception);
            return false;
        }

        public class LeakingTest
        {
            public LeakingTest(string testMethodName, int leakThresholdMb)
            {
                TestMethodName = testMethodName;
                LeakThresholdMB = leakThresholdMb;
            }

            public string TestMethodName { get; private set; }
            public int LeakThresholdMB { get; private set; }
        }

        public static class MemoryManagement
        {
            [DllImportAttribute("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, CharSet =
                CharSet.Ansi, SetLastError = true)]
            private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int
                maximumWorkingSetSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern UInt32 GetProcessHeaps(
                UInt32 NumberOfHeaps,
                IntPtr[] ProcessHeaps);

            [Flags]
            public enum PROCESS_HEAP_ENTRY_WFLAGS : ushort
            {
                PROCESS_HEAP_ENTRY_BUSY = 0x0004,
                PROCESS_HEAP_ENTRY_DDESHARE = 0x0020,
                PROCESS_HEAP_ENTRY_MOVEABLE = 0x0010,
                PROCESS_HEAP_REGION = 0x0001,
                PROCESS_HEAP_UNCOMMITTED_RANGE = 0x0002,
            }
            [StructLayoutAttribute(LayoutKind.Explicit)]
            public struct UNION_BLOCK
            {
                [FieldOffset(0)]
                public STRUCT_BLOCK Block;

                [FieldOffset(0)]
                public STRUCT_REGION Region;
            }
            [StructLayoutAttribute(LayoutKind.Sequential)]
            public struct STRUCT_BLOCK
            {
                public IntPtr hMem;
                public uint dwReserved1_1;
                public uint dwReserved1_2;
                public uint dwReserved1_3;
            }
            [StructLayoutAttribute(LayoutKind.Sequential)]
            public struct STRUCT_REGION
            {
                public uint dwCommittedSize;
                public uint dwUnCommittedSize;
                public IntPtr lpFirstBlock;
                public IntPtr lpLastBlock;
            }
            [StructLayoutAttribute(LayoutKind.Sequential)]
            public struct PROCESS_HEAP_ENTRY
            {
                public IntPtr lpData;
                public uint cbData;
                public byte cbOverhead;
                public byte iRegionIndex;
                public PROCESS_HEAP_ENTRY_WFLAGS wFlags;
                public UNION_BLOCK UnionBlock;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool HeapWalk(IntPtr hHeap, ref PROCESS_HEAP_ENTRY lpEntry);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool HeapLock(IntPtr hHeap);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool HeapUnlock(IntPtr hHeap);

            public static bool HeapDiagnostics { get; set; }

            public struct HeapAllocationSizes
            {
                public long Committed { get; set; }
                public long Reserved { get; set; }
                public long Unknown { get; set; }

                public List<KeyValuePair<long, int>> CommittedSizes { get; set; }
                public List<KeyValuePair<string, int>> StringCounts { get; set; }

                public override string ToString()
                {
                    return string.Format("{0:F2}, {1:F2}, {2:F2}",
                        Committed / (double)MB,
                        Reserved / (double)MB,
                        Unknown / (double)MB);
                }
            }

            private static readonly SizeTracker[] TRACK_SIZES = {new SizeTracker(120), new SizeTracker(624)};

            private class SizeTracker
            {
                private static int _dumps;

                public static void NextDump()
                {
                    _dumps++;
                }

                private readonly int _size;
                private readonly HashSet<IntPtr> _seenAllocations = new HashSet<IntPtr>();
                private TextWriter _currentDump;

                public SizeTracker(int size)
                {
                    _size = size;
                }

                private string DumpFile => "SizeDump" + _size + "_" + _dumps + ".txt";

                public void OpenDumpFile(string dmpDir)
                {
                    if (!Directory.Exists(dmpDir))
                        Directory.CreateDirectory(dmpDir);
                    _currentDump = new StreamWriter(new FileStream(Path.Combine(dmpDir, DumpFile), FileMode.Create));
                }

                public void CloseDumpFile()
                {
                    if (_currentDump != null)
                    {
                        _currentDump.Close();
                        _currentDump = null;
                    }
                }

                public void DumpUnseenSize(IntPtr pData, byte[] byteData)
                {
                    if (_currentDump != null && byteData.Length == _size && !_seenAllocations.Contains(pData))
                    {
                        DumpBytes(byteData);
                        _currentDump.WriteLine();
                        _seenAllocations.Add(pData);
                    }
                }

                private const int DUMP_WIDTH = 16;

                private void DumpBytes(byte[] byteData)
                {
                    int charPrintCount = Characters(byteData.Length);
                    for (int i = 0; i < charPrintCount; i++)
                    {
                        if (i < byteData.Length)
                        {
                            var b = byteData[i];
                            _currentDump.Write(b.ToString("X2"));
                        }
                        else
                        {
                            _currentDump.Write("  ");
                        }
                        _currentDump.Write(' ');
                        if (i % DUMP_WIDTH == DUMP_WIDTH - 1)
                        {
                            for (int j = DUMP_WIDTH - 1; j >= 0; j--)
                            {
                                int byteIndex = i - j;
                                var bc = byteIndex < byteData.Length ? byteData[byteIndex] : (byte) 0x20;
                                if (IsPrintableLowerAscii(bc))
                                    _currentDump.Write((char) bc);
                                else
                                    _currentDump.Write(".");
                            }
                            _currentDump.WriteLine();
                        }
                    }
                }

                private int Characters(int byteDataLength)
                {
                    return (Math.Max(0, byteDataLength-1) / DUMP_WIDTH + 1) * DUMP_WIDTH;
                }
            }

            public static HeapAllocationSizes[] GetProcessHeapSizes(string dmpDir)
            {
                if (HeapDiagnostics && dmpDir != null)
                {
                    SizeTracker.NextDump();
                    TRACK_SIZES.ForEach(t => t.OpenDumpFile(dmpDir));
                }
                var count = GetProcessHeaps(0, null);
                var buffer = new IntPtr[count];
                GetProcessHeaps(count, buffer);
                var sizes = new HeapAllocationSizes[count];
                for (int i = 0; i < count; i++)
                {
                    var committedSizes = HeapDiagnostics ? new Dictionary<long, int>() : null;
                    var stringCounts = HeapDiagnostics ? new Dictionary<string, int>() : null;

                    var h = buffer[i];
                    HeapLock(h);
                    var e = new PROCESS_HEAP_ENTRY();
                    while (HeapWalk(h, ref e))
                    {
                        if ((e.wFlags & PROCESS_HEAP_ENTRY_WFLAGS.PROCESS_HEAP_ENTRY_BUSY) != 0)
                        {
                            sizes[i].Committed += e.cbData + e.cbOverhead;

                            if (committedSizes != null)
                            {
                                // Update count
                                if (!committedSizes.ContainsKey(e.cbData))
                                    committedSizes[e.cbData] = 0;
                                committedSizes[e.cbData]++;
                            }

                            if (stringCounts != null)
                            {
                                // Find string(s)
                                var byteData = new byte[e.cbData];
                                IntPtr pData = e.lpData;
                                Marshal.Copy(pData, byteData, 0, byteData.Length);
                                TRACK_SIZES.ForEach(t => t.DumpUnseenSize(pData, byteData));
                                foreach (var byteString in FindStrings(byteData))
                                {
                                    if (!stringCounts.ContainsKey(byteString))
                                        stringCounts[byteString] = 0;
                                    stringCounts[byteString]++;
                                }
                            }
                        }
                        else if ((e.wFlags & PROCESS_HEAP_ENTRY_WFLAGS.PROCESS_HEAP_UNCOMMITTED_RANGE) != 0)
                            sizes[i].Reserved += e.cbData + e.cbOverhead;
                        else
                            sizes[i].Unknown += e.cbData + e.cbOverhead;

                    }
                    HeapUnlock(h);

                    if (committedSizes != null)
                        sizes[i].CommittedSizes = committedSizes.OrderByDescending(p => p.Value).ToList();
                    if (stringCounts != null)
                        sizes[i].StringCounts = stringCounts.OrderByDescending(p => p.Value).ToList();
                }
                if (HeapDiagnostics)
                    TRACK_SIZES.ForEach(t => t.CloseDumpFile());
                return sizes;
            }

            private static IEnumerable<string> FindStrings(byte[] byteData)
            {
                const int MIN_STRING_LENGTH = 8;
                var byteSb = new StringBuilder();
                bool inString = false;
                for (var j = 0; j < byteData.Length; j++)
                {
                    var b = byteData[j];
                    if (IsPrintableLowerAscii(b))
                    {
                        inString = true;
                        byteSb.Append((char) b);
                    }
                    else if (inString && b == 0)
                    {
                        // Support for Unicode strings where characters will
                        // be alternating printable with zeros
                        inString = false;   // If the next character is not printable, accumulation will end
                    }
                    else
                    {
                        if (byteSb.Length >= MIN_STRING_LENGTH)
                            yield return byteSb.ToString();
                        byteSb.Clear();
                        inString = false;
                    }
                }

                if (byteSb.Length >= MIN_STRING_LENGTH)
                    yield return byteSb.ToString(); // Add the last string
            }

            private static bool IsPrintableLowerAscii(byte b)
            {
                return 32 <= b && b <= 126;
            }

            public static void FlushMemory()
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
        }

        [DllImport("User32")]
        private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

        private enum HandleType { total = -1, gdi = 0, user = 1 }

        private int GetHandleCount(HandleType handleType)
        {
            if (handleType == HandleType.total)
                return _process.HandleCount;

            return GetGuiResources(_process.Handle, (int)handleType);
        }

        private static void Try<TEx>(Action action, int loopCount, bool throwOnFailure = true, int milliseconds = 500) 
            where TEx : Exception
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (TEx)
                {
                    Thread.Sleep(milliseconds);
                }
            }

            // Try the last time, and let the exception go.
            if (throwOnFailure)
                action();
        }

        private const int MB = 1024 * 1024;

        public double TotalMemory { get { return TotalMemoryBytes / (double) MB; } }

        public double CommittedMemory { get { return CommittedMemoryBytes / (double)MB; } }

        public double ManagedMemory { get { return ManagedMemoryBytes / (double) MB; } }


        private static readonly object _logLock = new object();
        [StringFormatMethod("info")]
        public void Log(string info, params object[] args)
        {
            lock (_logLock)
            {
                Console.Write(info, args);
                Console.Out.Flush(); // Get this info to TeamCity or SkylineTester ASAP
                if (_log != null)
                {
                    _log.Write(info, args);
                    _log.Flush();
                }
            }
        }

        public void TeamCityStartTest(TestInfo test)
        {
            if (TeamCityTestDecoration)
                Console.WriteLine(@"##teamcity[testStarted name='{0}' captureStandardOutput='true']", test.TestMethod.Name + '-' + Language.TwoLetterISOLanguageName);
        }

        public void TeamCityFinishTest(TestInfo test, string errorMessage = null)
        {
            if (!TeamCityTestDecoration)
                return;

            if (errorMessage?.Length > 0)
            {
                // ReSharper disable LocalizableElement
                var tcMessage = new StringBuilder(errorMessage);
                tcMessage.Replace("|", "||");
                tcMessage.Replace("'", "|'");
                tcMessage.Replace("\n", "|n");
                tcMessage.Replace("\r", "|r");
                tcMessage.Replace("[", "|[");
                tcMessage.Replace("]", "|]");
                Console.WriteLine("##teamcity[testFailed name='{0}' message='{1}']", test.TestMethod.Name + '-' + Language.TwoLetterISOLanguageName, tcMessage);
                // ReSharper restore LocalizableElement
            }

            Console.WriteLine(@"##teamcity[testFinished name='{0}' duration='{1}']", test.TestMethod.Name + '-' + Language.TwoLetterISOLanguageName, LastTestDuration);
        }

        public static IEnumerable<TestInfo> GetTestInfos(string testDll)
        {
            var assembly = Assembly.LoadFrom(GetAssemblyPath(testDll));
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (type.IsClass && HasAttribute(type, "TestClassAttribute"))
                {
                    if (!DerivesFromAbstractUnitTest(type))
// ReSharper disable LocalizableElement
                        Console.WriteLine("WARNING: " + type.Name + " does not derive from AbstractUnitTest!");
// ReSharper restore LocalizableElement
                    MethodInfo testInitializeMethod = null;
                    MethodInfo testCleanupMethod = null;
                    var methods = type.GetMethods();
                    foreach (var method in methods)
                    {
                        if (HasAttribute(method, "TestInitializeAttribute"))
                            testInitializeMethod = method;
                        if (HasAttribute(method, "TestCleanupAttribute"))
                            testCleanupMethod = method;
                    }
                    foreach (var method in methods)
                    {
                        if (HasAttribute(method, "TestMethodAttribute"))
                            yield return new TestInfo(type, method, testInitializeMethod, testCleanupMethod);
                    }
                }
            }
        }

        private static bool DerivesFromAbstractUnitTest(Type type)
        {
            while (type != null)
            {
                if (type.Name == "AbstractUnitTest")
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        public static Attribute GetAttribute(MemberInfo info, string attributeName)
        {
            var attributes = info.GetCustomAttributes(false);
            return attributes.OfType<Attribute>()
                .FirstOrDefault(attribute => attribute.ToString().EndsWith(attributeName));
        }

        // Determine if the given class or method from an assembly has the given attribute.
        private static bool HasAttribute(MemberInfo info, string attributeName)
        {
            return GetAttribute(info, attributeName) != null;
        }

    }
}
