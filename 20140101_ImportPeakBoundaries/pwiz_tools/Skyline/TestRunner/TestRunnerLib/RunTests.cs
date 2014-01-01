using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        public void DisplayStartTest(StreamWriter log, int pass, int testNumber, CultureInfo culture)
        {
            var time = DateTime.Now;
            var info = string.Format(
                "[{0}:{1}] {2,3}.{3,-3} {4,-46} ({5}) ",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                pass,
                testNumber,
                TestMethod.Name,
                culture);
            Console.Write(info);
            log.Write(info);
            log.Flush();
        }
    }

    public class RunTests
    {
        private string _testDir;
        private TestContext _testContext;

        public RunTests()
        {
            _testContext = new TestRunnerContext();
            _testDir = SetTestDir(_testContext, 1, Process.GetCurrentProcess());
        }

        private static string SetTestDir(TestContext testContext, int testDirectoryCount, Process process)
        {
            var now = DateTime.Now;
            var testDirName = string.Format("TestRunner_{0}-{1:D2}-{2:D2}_{3:D2}-{4:D2}_{5}-{6}",
                                            now.Year, now.Month, now.Day, now.Hour, now.Minute, process.Id, testDirectoryCount);
            var testDir = Path.Combine(GetProjectPath("TestResults"), testDirName);
            testContext.Properties["TestDir"] = testDir;
            return testDir;
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
            return null;
        }

        public void Run(TestInfo test, CultureInfo culture, Stopwatch stopwatch = null)
        {
            // Delete test directory.
            int testDirectoryCount = 1;
            while (Directory.Exists(_testDir))
            {
                try
                {
                    // Try delete 4 times to give anti-virus software a chance to finish.
// ReSharper disable AccessToModifiedClosure
                    TryLoop.Try<IOException>(() => Directory.Delete(_testDir, true), 4);
// ReSharper restore AccessToModifiedClosure
                }
                catch (Exception e)
                {
                    Console.WriteLine("\n\n" + e.Message);
                    _testDir = SetTestDir(_testContext, ++testDirectoryCount, Process.GetCurrentProcess());
                }
            }

            // Create test class.
            var testObject = Activator.CreateInstance(test.TestClassType);

            // Set the TestContext.
            if (test.SetTestContext != null)
            {
                var context = new object[] { _testContext };
                test.SetTestContext.Invoke(testObject, context);
            }

            // Switch to selected culture.
            LocalizationHelper.CurrentCulture = culture;
            LocalizationHelper.InitThread();

            // Run the test and time it.
            Exception exception = null;
            if (stopwatch != null)
                stopwatch.Start();
            long totalLeakedBytes = 0;
            try
            {
                if (test.TestInitialize != null)
                    test.TestInitialize.Invoke(testObject, null);

                if (pass > 1 || repeatCounter > 1)
                    CrtDebugHeap.Checkpoint();
                test.TestMethod.Invoke(testObject, null);
                if (pass > 1 || repeatCounter > 1)
                {
                    long leakedBytes = CrtDebugHeap.DumpLeaks(true);
                    totalLeakedBytes += leakedBytes;
                }

                if (test.TestCleanup != null)
                    test.TestCleanup.Invoke(testObject, null);
            }
            catch (Exception e)
            {
                exception = e;
            }

            if (stopwatch != null)
                stopwatch.Stop();

            // Restore culture.
            Thread.CurrentThread.CurrentCulture = saveCulture;
            Thread.CurrentThread.CurrentUICulture = saveUICulture;

            var managedMemory = GC.GetTotalMemory(false) / mb;
            process.Refresh();
            var totalMemory = process.PrivateMemorySize64 / mb;

            if (exception == null)
            {
                // Test succeeded.
                info = string.Format(
                    "{0,3} failures, {1:0.0}/{2:0.0} MB{3}, {4} sec.", 
                    _failureCount, 
                    managedMemory, 
                    totalMemory,
                    totalLeakedBytes > 0 ? string.Format("  *** LEAKED {0} bytes ***", totalLeakedBytes) : "",
                    stopwatch.ElapsedMilliseconds/1000);
                Console.WriteLine(info);
                log.WriteLine(info);
            }
            else
            {
                // Save failure information.
                _failureCount++;
                failureList[testName]++;
                info = testName + " {0} failures ({1:0.##}%)\n" +
                        exception.InnerException.Message + "\n" +
                        exception.InnerException.StackTrace;
                if (errorList.ContainsKey(info))
                {
                    errorList[info]++;
                }
                else
                {
                    errorList[info] = 1;
                }
                Console.WriteLine("*** FAILED {0:0.#}% ***", 100.0*failureList[testName]/pass);
                log.WriteLine("{0,3} failures, {1:0.0}/{2:0.0} MB\n*** failure {3}\n{4}\n{5}\n***",
                                _failureCount, managedMemory, totalMemory, errorList[info], exception.InnerException.Message,
                                exception.InnerException.StackTrace);
            }
            log.Flush();

            if (totalLeakedBytes > 0)
                Trace.WriteLine(string.Format("\n*** {0} leaked ***\n", testName));
        }
    }
}
