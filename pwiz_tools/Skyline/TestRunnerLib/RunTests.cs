/*
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
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;

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

        public TestInfo(Type testClass, MethodInfo testMethod, MethodInfo testInitializeMethod, MethodInfo testCleanupMethod)
        {
            TestClassType = testClass;
            TestMethod = testMethod;
            SetTestContext = testClass.GetMethod("set_TestContext");
            TestInitialize = testInitializeMethod;
            TestCleanup = testCleanupMethod;
            IsPerfTest = (testClass.Namespace ?? String.Empty).Equals("TestPerf");
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
        public int LastGdiHandleCount { get; private set; }
        public int LastUserHandleCount { get; private set; }
        public bool AccessInternet { get; set; }
        public bool RunPerfTests { get; set; }
        public bool AddSmallMoleculeNodes{ get; set; }
        public bool RunsSmallMoleculeVersions { get; set; }
        public bool LiveReports { get; set; }

        public RunTests(
            bool demoMode,
            bool buildMode,
            bool offscreen,
            bool internet,
            bool showStatus,
            bool perftests,
            bool addsmallmoleculenodes,
            bool runsmallmoleculeversions,
            IEnumerable<string> pauseForms,
            int pauseSeconds = 0,
            bool useVendorReaders = true,
            int timeoutMultiplier = 1,
            string results = null,
            StreamWriter log = null)
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
            Skyline.Set("PauseForms", pauseForms != null ? pauseForms.ToList() : null);
            try
            {
                Skyline.Get<string>("Name");
            }
            catch (Exception getNameException)
            {
                // ReSharper disable NonLocalizedString
                StringBuilder message = new StringBuilder();
                message.AppendLine("Error initializing settings");
                var exeConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.None);
                message.AppendLine("Exe Config:" + exeConfig.FilePath);
                var localConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
                message.AppendLine("Local Config:" + localConfig.FilePath);
                var roamingConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.PerUserRoaming);
                message.AppendLine("Roaming Config:" + roamingConfig.FilePath);
                throw new Exception(message.ToString(), getNameException);
                // ReSharper restore NonLocalizedString
            }
            Skyline.Run("Init");

            AccessInternet = internet;
            RunPerfTests = perftests;
            AddSmallMoleculeNodes= addsmallmoleculenodes;  // Add the magic small molecule test node to all documents?
            RunsSmallMoleculeVersions = runsmallmoleculeversions;  // Run the small molecule version of various tests?
            LiveReports = true;

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

        public bool Run(TestInfo test, int pass, int testNumber)
        {
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
                var time = DateTime.Now;
                Log("[{0}:{1}] {2,3}.{3,-3} {4,-46} ({5}) ",
                    time.Hour.ToString("D2"),
                    time.Minute.ToString("D2"),
                    pass,
                    testNumber,
                    test.TestMethod.Name,
                    Language.TwoLetterISOLanguageName);
            }

            Exception exception = null;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var saveCulture = Thread.CurrentThread.CurrentCulture;
            var saveUICulture = Thread.CurrentThread.CurrentUICulture;
            long crtLeakedBytes = 0;

            try
            {
                // Create test class.
                var testObject = Activator.CreateInstance(test.TestClassType);

                // Set the TestContext.
                TestContext.Properties["AccessInternet"] = AccessInternet.ToString();
                TestContext.Properties["RunPerfTests"] = RunPerfTests.ToString();
                TestContext.Properties["TestSmallMolecules"] = AddSmallMoleculeNodes.ToString(); // Add the magic small molecule test node to every document?
                TestContext.Properties["RunSmallMoleculeTestVersions"] = RunsSmallMoleculeVersions.ToString(); // Run the AsSmallMolecule version of tests when available?
                TestContext.Properties["LiveReports"] = LiveReports.ToString();
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
            LastTestDuration = (int) (stopwatch.ElapsedMilliseconds/1000);

            // Restore culture.
            Thread.CurrentThread.CurrentCulture = saveCulture;
            Thread.CurrentThread.CurrentUICulture = saveUICulture;

            MemoryManagement.FlushMemory();

            const int mb = 1024*1024;
            var managedMemory = (double) GC.GetTotalMemory(true) / mb;

            LastGdiHandleCount = GetHandleCount(HandleType.total);
            LastUserHandleCount = GetHandleCount(HandleType.user) + GetHandleCount(HandleType.gdi);

            if (exception == null)
            {
                // Test succeeded.
                Log(
                    "{0,3} failures, {1:F2}/{2:F1} MB, {3}/{4} handles, {5} sec.\r\n",
                    FailureCount, 
                    managedMemory, 
                    TotalMemory,
                    LastUserHandleCount,
                    LastGdiHandleCount,
                    LastTestDuration);
                if (crtLeakedBytes > CheckCrtLeaks)
                    Log("!!! {0} CRT-LEAKED {1} bytes\r\n", test.TestMethod.Name, crtLeakedBytes);
//                if (LastGdiHandleDelta != 0 || LastUserHandleDelta != 0)
//                    Console.Write(@"!!! {0} HANDLES-LEAKED {1} gdi, {2} user\r\n", test.TestMethod.Name, LastGdiHandleDelta, LastUserHandleDelta);

                using (var writer = new FileStream("TestRunnerMemory.log", FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var stringWriter = new StreamWriter(writer))
                {
                    stringWriter.WriteLine(TotalMemory.ToString("F1"));
                }
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

            Log(
                "{0,3} failures, {1:F2}/{2:F1} MB, {3}/{4} handles, {5} sec.\r\n\r\n!!! {6} FAILED\r\n{7}\r\n{8}\r\n!!!\r\n\r\n",
                FailureCount, managedMemory, TotalMemory, LastUserHandleCount, LastGdiHandleCount, LastTestDuration,
                test.TestMethod.Name,
                message,
                exception);
            return false;
        }

        static class MemoryManagement
        {
            [DllImportAttribute("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, CharSet =
                CharSet.Ansi, SetLastError = true)]

            private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int
                maximumWorkingSetSize);

            public static void FlushMemory()
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
        }

        [DllImport("User32")]
        private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

        private enum HandleType { total = -1, gdi = 0, user = 1 }

        private static int GetHandleCount(HandleType handleType)
        {
            using (var process = Process.GetCurrentProcess())
            {
                if (handleType == HandleType.total)
                    return process.HandleCount;

                return GetGuiResources(process.Handle, (int)handleType);
            }
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

        public double TotalMemory
        {
            get
            {
                const int mb = 1024*1024;
                return (double) TotalMemoryBytes / mb;
            }
        }

        public long TotalMemoryBytes
        {
            get
            {
                MemoryManagement.FlushMemory();
                _process.Refresh();
                return _process.PrivateMemorySize64;
            }
        }

        public void Log(string info, params object[] args)
        {
            Console.Write(info, args);
            Console.Out.Flush(); // Get this info to TeamCity or SkylineTester ASAP
            if (_log != null)
            {
                _log.Write(info, args);
                _log.Flush();
            }
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
                        Console.WriteLine("WARNING: " + type.Name + " does not derive from AbstractUnitTest!"); // Not L10N
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

        // Determine if the given class or method from an assembly has the given attribute.
        private static bool HasAttribute(MemberInfo info, string attributeName)
        {
            var attributes = info.GetCustomAttributes(false);
            return attributes.Any(attribute => attribute.ToString().EndsWith(attributeName));
        }

    }
}
