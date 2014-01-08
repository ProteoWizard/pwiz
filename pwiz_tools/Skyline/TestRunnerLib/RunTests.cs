using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Crawdad;

namespace TestRunnerLib
{
    public class TestInfo
    {
        public readonly Type TestClassType;
        public readonly MethodInfo TestMethod;
        public readonly MethodInfo SetTestContext;
        public readonly MethodInfo TestInitialize;
        public readonly MethodInfo TestCleanup;

        public TestInfo(Type testClass, MethodInfo testMethod, MethodInfo testInitializeMethod, MethodInfo testCleanupMethod)
        {
            TestClassType = testClass;
            TestMethod = testMethod;
            SetTestContext = testClass.GetMethod("set_TestContext");
            TestInitialize = testInitializeMethod;
            TestCleanup = testCleanupMethod;
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

        public RunTests(
            bool demoMode,
            bool buildMode,
            bool offscreen,
            bool showStatus,
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
            Skyline.Get<string>("Name");
            Skyline.Run("Init");

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
            if (runnerExeDirectory == null) throw new ApplicationException("Can't find path to TestRunner.exe");
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

            // Create test class.
            var testObject = Activator.CreateInstance(test.TestClassType);

            // Set the TestContext.
            if (test.SetTestContext != null)
            {
                var context = new object[] { TestContext };
                test.SetTestContext.Invoke(testObject, context);
            }

            // Switch to selected culture.
            var saveCulture = Thread.CurrentThread.CurrentCulture;
            var saveUICulture = Thread.CurrentThread.CurrentUICulture;
            LocalizationHelper.CurrentCulture = Language;
            LocalizationHelper.InitThread();

            long crtLeakedBytes = 0;

            // Run the test and time it.
            Exception exception = null;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                if (test.TestInitialize != null)
                    test.TestInitialize.Invoke(testObject, null);

                if (CheckCrtLeaks > 0)
                    CrtDebugHeap.Checkpoint();
                test.TestMethod.Invoke(testObject, null);
                if (CheckCrtLeaks > 0)
                    crtLeakedBytes = CrtDebugHeap.DumpLeaks(true);

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

            const int mb = 1024*1024;
            var managedMemory = GC.GetTotalMemory(true) / mb;

            if (exception == null)
            {
                // Test succeeded.
                Log(
                    "{0,3} failures, {1}/{2} MB, {3} sec.\r\n", 
                    FailureCount, 
                    managedMemory, 
                    TotalMemory,
                    LastTestDuration);
                if (crtLeakedBytes > CheckCrtLeaks)
                    Log("!!! {0} CRT-LEAKED {1} bytes", test.TestMethod.Name, crtLeakedBytes);

                using (var writer = new FileStream("TestRunnerMemory.log", FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var stringWriter = new StreamWriter(writer))
                {
                    stringWriter.WriteLine(TotalMemory.ToString(CultureInfo.InvariantCulture));
                }
                return true;
            }

            // Save failure information.
            FailureCount++;
            if (FailureCounts.ContainsKey(test.TestMethod.Name))
                FailureCounts[test.TestMethod.Name]++;
            else
                FailureCounts[test.TestMethod.Name] = 1;
            var failureInfo = "# " + test.TestMethod.Name + "FAILED:\n" +
                exception.InnerException.Message + "\n" +
                exception.InnerException.StackTrace;
            if (ErrorCounts.ContainsKey(failureInfo))
                ErrorCounts[failureInfo]++;
            else
                ErrorCounts[failureInfo] = 1;

            Log(
                "{0,3} failures, {1}/{2} MB\r\n\r\n!!! {3} FAILED\r\n{4}\r\n{5}\r\n!!!\r\n\r\n",
                FailureCount, managedMemory, TotalMemory, test.TestMethod.Name,
                exception.InnerException.Message,
                exception.InnerException.StackTrace);
            return false;
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

        public int TotalMemory
        {
            get
            {
                const int mb = 1024*1024;
                return (int) (TotalMemoryBytes / mb);
            }
        }

        public long TotalMemoryBytes
        {
            get
            {
                _process.Refresh();
                return _process.PrivateMemorySize64;
            }
        }

        public void Log(string info, params object[] args)
        {
            Console.Write(info, args);
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

        // Determine if the given class or method from an assembly has the given attribute.
        private static bool HasAttribute(MemberInfo info, string attributeName)
        {
            var attributes = info.GetCustomAttributes(false);
            return attributes.Any(attribute => attribute.ToString().EndsWith(attributeName));
        }

    }
}
