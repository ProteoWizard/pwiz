/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
//using pwiz.Skyline.Util;
//WARNING: Including TestUtil in this project causes a strange build problem, where the first
//         build from Visual Studio after a full bjam build removes all of the Skyline project
//         root files from the Skyline bin directory, leaving it un-runnable until a full
//         rebuild is performed.  Do not commit a reference to TestUtil to this project without
//         testing this case and getting someone else to validate that you have fixed this
//         problem.
//using pwiz.SkylineTestUtil;
using TestRunnerLib;

namespace TestRunner
{
    internal static class Program
    {
        private static readonly string[] TEST_DLLS = { "Test.dll", "TestA.dll", "TestFunctional.dll", "TestTutorial.dll", "CommonTest.dll" };
        private static readonly string[] FORMS_DLLS = { "TestFunctional.dll", "TestTutorial.dll" };
        private const int LeakThreshold = 2000;
        private const int CrtLeakThreshold = 1000;
        private const int LeakCheckIterations = 4;

        [STAThread]
        static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ThreadExceptionEventHandler;

            // Parse command line args and initialize default values.
            const string commandLineOptions =
                "?;/?;-?;help;skylinetester;debug;results;" +
                "test;skip;filter;form;" +
                "loop=0;repeat=1;pause=0;random=on;offscreen=on;multi=1;demo=off;status=off;buildcheck=0;" +
                "quality=off;pass0=on;pass1=on;" +
                "clipboardcheck=off;profile=off;vendors=on;language=fr-FR,en-US;" +
                "log=TestRunner.log;report=TestRunner.log";
            var commandLineArgs = new CommandLineArgs(args, commandLineOptions);

            switch (commandLineArgs.SearchArgs("?;/?;-?;help;report"))
            {
                case "?":
                case "/?":
                case "-?":
                case "help":
                    Help();
                    return 0;

                case "report":
                    Report(commandLineArgs.ArgAsString("report"));
                    return 0;
            }

            Console.WriteLine();
            if (!commandLineArgs.ArgAsBool("status") && !commandLineArgs.ArgAsBool("buildcheck"))
            {
                Console.WriteLine("TestRunner " + string.Join(" ", args) + "\n");
                Console.WriteLine("Process: {0}\n", Process.GetCurrentProcess().Id);
            }

            if (commandLineArgs.HasArg("debug"))
            {
                Console.WriteLine("*** Launching debugger ***\n\n");

                // NOTE: For efficient debugging of Skyline, it is most useful to choose a debugger
                // that already has Skyline.sln loaded.  Otherwise, you might not be able to set
                // breakpoints.
                Debugger.Break();
            }

            var skylineDirectory = GetSkylineDirectory();

            // Create log file.
            var logStream = new FileStream(
                commandLineArgs.ArgAsString("log"),
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);
            var log = new StreamWriter(logStream);

            bool allTestsPassed = true;

            try
            {
                // Load list of tests.
                var unfilteredTestList = LoadTestList(commandLineArgs);

                // Filter test list.
                var testList = unfilteredTestList;
                if (commandLineArgs.HasArg("filter"))
                {
                    testList = new List<TestInfo>();
                    var filterRanges = commandLineArgs.ArgAsString("filter").Split(',');
                    foreach (var range in filterRanges)
                    {
                        var bounds = range.Split('-');
                        if (bounds.Length < 1 || bounds.Length > 2)
                        {
                            throw new ArgumentException("Unrecognized filter parameter: {0}", range);
                        }
                        int low;
                        if (!int.TryParse(bounds[0], out low))
                        {
                            throw new ArgumentException("Unrecognized filter parameter: {0}", range);
                        }
                        int high = low;
                        if (bounds.Length == 2 && !int.TryParse(bounds[1], out high))
                        {
                            throw new ArgumentException("Unrecognized filter parameter: {0}", range);
                        }
                        for (var i = low-1; i <= high-1; i++)
                        {
                            testList.Add(unfilteredTestList[i]);
                        }
                    }
                }

                if (testList.Count == 0)
                {
                    Console.WriteLine("No tests found");
                    return 1;
                }

                var passes = commandLineArgs.ArgAsLong("loop");
                var repeat = commandLineArgs.ArgAsLong("repeat");
                if (commandLineArgs.ArgAsBool("buildcheck"))
                {
                    passes = 1;
                    repeat = 1;
                }

                // Prevent system sleep.
                using (new SystemSleep())
                {
                    // Pause before first test for profiling.
                    bool profiling = commandLineArgs.ArgAsBool("profile");
                    if (profiling)
                    {
                        Console.WriteLine("\nRunning each test once to warm up memory...\n");
                        allTestsPassed = RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, 1, 1);
                        Console.WriteLine("\nTaking memory snapshot...\n");
//                        MemoryProfiler.Snapshot();
                        if (passes == 0)
                            passes = 1;
                    }

                    allTestsPassed = RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, passes, repeat) && allTestsPassed;

                    // Pause for profiling
                    if (profiling)
                    {
                        Console.WriteLine("\nTaking second memory snapshot...\n");
//                        MemoryProfiler.Snapshot();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\n\n" + e.Message);
                Console.WriteLine(e.StackTrace);
                if (e.InnerException != null)
                {
                    Console.WriteLine("\nInner exception:");
                    Console.WriteLine(e.InnerException.Message);
                    Console.WriteLine(e.InnerException.StackTrace);
                }
                allTestsPassed = false;
            }

            // Display report.
            log.Close();
            Console.WriteLine("\n");
            if (!commandLineArgs.ArgAsBool("status"))
                Report(commandLineArgs.ArgAsString("log"));

            // If the forms/tests cache was regenerated, copy it to Skyline directory.
            if (commandLineArgs.ArgAsString("form") == "__REGEN__" && skylineDirectory != null)
            {
                var testRunnerDirectory = Path.Combine(skylineDirectory.FullName, "TestRunner");
                if (Directory.Exists(testRunnerDirectory))
                    FormLookup.CopyCacheFile(testRunnerDirectory);
            }

            // Ungraceful exit to avoid unwinding errors
            //Process.GetCurrentProcess().Kill();

            return allTestsPassed ? 0 : 1;
        }

        private static DirectoryInfo GetSkylineDirectory()
        {
            string skylinePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var skylineDirectory = skylinePath != null ? new DirectoryInfo(skylinePath) : null;
            while (skylineDirectory != null && skylineDirectory.Name != "Skyline")
                skylineDirectory = skylineDirectory.Parent;
            return skylineDirectory;
        }

        // Run all test passes.
        private static bool RunTestPasses(List<TestInfo> testList, List<TestInfo> unfilteredTestList, CommandLineArgs commandLineArgs, StreamWriter log, long loopCount, long repeat)
        {
            bool buildMode = commandLineArgs.ArgAsBool("buildcheck");
            bool randomOrder = commandLineArgs.ArgAsBool("random");
            bool demoMode = commandLineArgs.ArgAsBool("demo");
            bool offscreen = commandLineArgs.ArgAsBool("offscreen");
            bool useVendorReaders = commandLineArgs.ArgAsBool("vendors");
            bool showStatus = commandLineArgs.ArgAsBool("status");
            bool qualityMode = commandLineArgs.ArgAsBool("quality");
            bool pass0 = commandLineArgs.ArgAsBool("pass0");
            bool pass1 = commandLineArgs.ArgAsBool("pass1");
            int timeoutMultiplier = (int) commandLineArgs.ArgAsLong("multi");
            int pauseSeconds = (int) commandLineArgs.ArgAsLong("pause");
            var formList = commandLineArgs.ArgAsString("form");
            var pauseDialogs = (string.IsNullOrEmpty(formList)) ? null : formList.Split(',');
            var results = commandLineArgs.ArgAsString("results");

            if (buildMode)
            {
                randomOrder = false;
                demoMode = false;
                offscreen = true;
                useVendorReaders = true;
                showStatus = false;
                qualityMode = false;
            }

            var runTests = new RunTests(demoMode, buildMode, offscreen, showStatus, pauseDialogs, pauseSeconds, useVendorReaders, timeoutMultiplier, results, log);

            if (commandLineArgs.ArgAsBool("clipboardcheck"))
            {
                runTests.TestContext.Properties["ClipboardCheck"] = "TestRunner clipboard check";
                Console.WriteLine("Checking clipboard use for {0} tests...\n", testList.Count);
                loopCount = 1;
                randomOrder = false;
            }
            else
            {
                runTests.Log("Running {0}{1} tests{2}{3}...\r\n",
                    testList.Count,
                    testList.Count < unfilteredTestList.Count ? "/" + unfilteredTestList.Count : "",
                    (loopCount <= 0) ? " forever" : (loopCount == 1) ? "" : " in " + loopCount + " loops",
                    (repeat <= 1) ? "" : ", repeated " + repeat + " times each");
            }

            // Get list of languages
            var languages = buildMode 
                ? new[] {"en"} 
                : commandLineArgs.ArgAsString("language").Split(',');

            List<string> shownForms = (formList == "__REGEN__") ? new List<string>() : null;
            runTests.Skyline.Set("ShownForms", shownForms);

            // Prepare for showing specific forms, if desired.
            var formLookup = new FormLookup();

            var executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var qualityLanguages = new FindLanguages(executingDirectory, "en", "fr").Enumerate().ToArray();
            var removeList = new List<TestInfo>();

            if (qualityMode)
            {
                int testNumber = 0;

                if (pass0)
                {
                    // Pass zero tests French number format with no vendor readers.
                    runTests.Log("\r\n");
                    runTests.Log("# Pass 0: Run with French number format, vendor readers disabled.\r\n");

                    runTests.Language = new CultureInfo("fr");
                    runTests.CheckCrtLeaks = CrtLeakThreshold;
                    runTests.Skyline.Set("NoVendorReaders", true);
                    foreach (var test in testList)
                    {
                        testNumber++;

                        if (!runTests.Run(test, 0, testNumber))
                            removeList.Add(test);
                    }
                    runTests.CheckCrtLeaks = 0;
                    runTests.Skyline.Set("NoVendorReaders", false);

                    foreach (var removeTest in removeList)
                        testList.Remove(removeTest);
                    removeList.Clear();
                }

                if (pass1)
                {
                    // Pass 1 looks for cumulative leaks when test is run multiple times.
                    runTests.Log("\r\n");
                    runTests.Log("# Pass 1: Run tests multiple times to detect memory leaks.\r\n");

                    int qualityPasses = Math.Max(LeakCheckIterations, qualityLanguages.Length);
                    testNumber = 0;
                    foreach (var test in testList)
                    {
                        testNumber++;

                        runTests.Language = new CultureInfo("en");
                        runTests.CheckCrtLeaks = CrtLeakThreshold;
                        if (!runTests.Run(test, 1, testNumber))
                        {
                            removeList.Add(test);
                            continue;
                        }
                        runTests.CheckCrtLeaks = 0;

                        // Check for memory leak by noting memory size increase over multiple runs.
                        // Check leaks up to 3 times to make sure the test is actually leaking, because
                        // memory reporting is pretty squishy.
                        long leakSize = 0;
                        for (int leakCheck = 0; leakCheck < 3; leakCheck++)
                        {
                            long memorySize = runTests.TotalMemoryBytes;
                            bool failed = false;
                            for (int i = 1; i <= qualityPasses; i++)
                            {
                                runTests.Language = new CultureInfo(qualityLanguages[i%qualityLanguages.Length]);
                                if (!runTests.Run(test, 1, testNumber))
                                {
                                    failed = true;
                                    break;
                                }
                            }
                            if (failed)
                            {
                                removeList.Add(test);
                                break;
                            }

                            long leakedPerPass = (runTests.TotalMemoryBytes - memorySize)/qualityPasses;
                            if (leakedPerPass < LeakThreshold)
                            {
                                leakSize = 0;
                                break;
                            }
                            if (leakSize == 0)
                                leakSize = leakedPerPass;
                            if (leakSize > leakedPerPass)
                                leakSize = leakedPerPass;
                        }

                        if (leakSize > LeakThreshold)
                        {
                            runTests.Log("!!! {0} LEAKED {1} bytes\r\n", test.TestMethod.Name, leakSize);
                            removeList.Add(test);
                        }
                    }

                    foreach (var removeTest in removeList)
                        testList.Remove(removeTest);
                    removeList.Clear();
                }
            }

            runTests.CheckCrtLeaks = 0;

            // Run all test passes.
            int pass = 1;
            int passEnd = pass + (int) loopCount;
            if (qualityMode)
            {
                pass++;
                passEnd++;
                if (loopCount < 0)
                    passEnd = int.MaxValue;
                languages = qualityLanguages;
            }
            else if (loopCount == 0)
            {
                passEnd = int.MaxValue;
            }

            if (pass == 2 && pass < passEnd && testList.Count > 0)
            {
                runTests.Log("\r\n");
                runTests.Log("# Pass 2+: Run tests in each selected language.\r\n");
            }

            for (; pass < passEnd; pass++)
            {
                if (testList.Count == 0)
                    break;

                // Run each test in this test pass.
                int testNumber = 0;
                var testPass = randomOrder || qualityMode ? testList.RandomOrder() : testList;
                foreach (var test in testPass)
                {
                    testNumber++;

                    // Run once (or repeat times) for each language.
                    foreach (var language in languages)
                    {
                        runTests.Language = new CultureInfo(language);
                        for (int repeatCounter = 1; repeatCounter <= repeat; repeatCounter++)
                        {
                            if (!runTests.Run(test, pass, testNumber))
                                removeList.Add(test);
                        }
                    }

                    // Record which forms the test showed.
                    if (shownForms != null)
                    {
                        formLookup.AddForms(test.TestMethod.Name, runTests.LastTestDuration, shownForms);
                        shownForms.Clear();
                    }
                }

                foreach (var removeTest in removeList)
                    testList.Remove(removeTest);
                removeList.Clear();
            }

            return runTests.FailureCount == 0;
        }

        // Load list of tests to be run into TestList.
        private static List<TestInfo> LoadTestList(CommandLineArgs commandLineArgs)
        {
            List<string> testNames;
            var testList = new List<TestInfo>();

            // Clear forms/tests cache if desired.
            var formArg = commandLineArgs.ArgAsString("form");

            // Load lists of tests to run.
            if (string.IsNullOrEmpty(formArg))
                testNames = LoadList(commandLineArgs.ArgAsString("test"));

            // Find which tests best cover the desired forms.
            else
            {
                if (formArg == "__REGEN__")
                    FormLookup.ClearCache();
                var formLookup = new FormLookup();
                if (formLookup.IsEmpty)
                    return GetTestList(FORMS_DLLS).OrderBy(e => e.TestMethod.Name).ToList();

                List<string> uncoveredForms;
                testNames = formLookup.FindTests(LoadList(formArg), out uncoveredForms);
                if (uncoveredForms.Count > 0)
                    MessageBox.Show("No tests found to show these Forms: " + string.Join(", ", uncoveredForms), "Warning");
            }

            // Maintain order in list of explicitly specified tests
            var testDict = new Dictionary<string, int>();
            for (int i = 0; i < testNames.Count; i++)
            {
                if (testDict.ContainsKey(testNames[i]))
                {
                    MessageBox.Show("Duplicate test name: " + testNames[i]);
                    throw new ArgumentException("Duplicate test name: " + testNames[i]);
                }
                testDict.Add(testNames[i], i);
            }

            var testArray = new TestInfo[testNames.Count];

            var skipList = LoadList(commandLineArgs.ArgAsString("skip"));

            // Find tests in the test dlls.
            foreach (var testDll in TEST_DLLS)
            {
                foreach (var testInfo in RunTests.GetTestInfos(testDll))
                {
                    var testName = testInfo.TestClassType.Name + "." + testInfo.TestMethod.Name;
                    if (testNames.Count == 0 || testNames.Contains(testName) ||
                        testNames.Contains(testInfo.TestMethod.Name))
                    {
                        if (!skipList.Contains(testName) && !skipList.Contains(testInfo.TestMethod.Name))
                        {
                            if (testNames.Count == 0)
                                testList.Add(testInfo);
                            else
                            {
                                string lookup = testNames.Contains(testName) ? testName : testInfo.TestMethod.Name;
                                testArray[testDict[lookup]] = testInfo;
                            }
                        }
                    }
                }
            }
            if (testNames.Count > 0)
                testList.AddRange(testArray.Where(testInfo => testInfo != null));

            // Sort tests alphabetically.
            return testList.OrderBy(e => e.TestMethod.Name).ToList();
        }

        private static List<TestInfo> GetTestList(IEnumerable<string> dlls)
        {
            var testList = new List<TestInfo>();

            // Find tests in the test dlls.
            foreach (var testDll in dlls)
            {
                testList.AddRange(RunTests.GetTestInfos(testDll));
            }

            // Sort tests alphabetically.
            testList.Sort((x, y) => String.CompareOrdinal(x.TestMethod.Name, y.TestMethod.Name));

            return testList;
        }

        // Load a list of tests specified on the command line as a comma-separated list.  Any name prefixed with '@'
        // is a file containing test names separated by white space or new lines, with '#' indicating a comment.
        private static List<string> LoadList(string testList)
        {
            var inputList = testList.Split(',');
            var outputList = new List<string>();

            // Check for empty list.
            if (inputList.Length == 1 && inputList[0] == "")
            {
                return outputList;
            }

            foreach (var name in inputList)
            {
                if (name.StartsWith("@"))
                {
                    var file = name.Substring(1);
                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        // remove comments
                        var lineParts = line.Split('#');
                        if (lineParts.Length > 0 && lineParts[0] != "")
                        {
                            // split multiple test names in one line
                            outputList.AddRange(lineParts[0].Trim().Split(' ', '\t'));
                        }
                    }
                }
                else if (name.EndsWith(".dll", StringComparison.CurrentCultureIgnoreCase))
                {
                    foreach (var testInfo in RunTests.GetTestInfos(name))
                        outputList.Add(testInfo.TestClassType.Name + "." + testInfo.TestMethod.Name);
                }
                else
                {
                    outputList.Add(name);
                }
            }

            return outputList;
        }

        private class LeakingTest
        {
            public string TestName;
            public long LeakSize;
        }

        // Generate a summary report of errors and memory leaks from a log file.
        private static void Report(string logFile)
        {
            var logLines = File.ReadAllLines(logFile);

            var errorList = new List<string>();
            var leakList = new List<LeakingTest>();
            var crtLeakList = new List<LeakingTest>();

            string error = null;
            foreach (var line in logLines)
            {
                if (error != null)
                {
                    if (line == "!!!")
                    {
                        errorList.Add(error);
                        error = null;
                    }
                    else
                    {
                        error += "# " + line + "\n";
                    }
                    continue;
                }

                var parts = Regex.Replace(line, @"\s+", " ").Trim().Split(' ');

                // Is it an error line?
                if (parts[0] == "!!!")
                {
                    var test = parts[1];
                    var failureType = parts[2];
                   
                    if (failureType == "LEAKED")
                    {
                        var leakSize = long.Parse(parts[3]);
                        leakList.Add(new LeakingTest { TestName = test, LeakSize = leakSize });
                        continue;
                    }
                   
                    if (failureType == "CRT-LEAKED")
                    {
                        var leakSize = long.Parse(parts[3]);
                        crtLeakList.Add(new LeakingTest { TestName = test, LeakSize = leakSize });
                        continue;
                    }
                    
                    error = "# " + test + " FAILED:\n";
                }
            }

            // Print list of errors sorted in descending order of frequency.
            Console.WriteLine();
            if (errorList.Count == 0)
                Console.WriteLine("# No failures.\n");
            foreach (var failure in errorList)
                Console.WriteLine(failure);

            if (leakList.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("# Leaking tests (bytes leaked per run):");
                ReportLeaks(leakList);
            }

            if (crtLeakList.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("# Tests leaking unmanaged memory:");
                ReportLeaks(crtLeakList);
            }
        }

        private static void ReportLeaks(IEnumerable<LeakingTest> leakList)
        {
            foreach (var leakTest in leakList.OrderByDescending(test => test.LeakSize))
            {
                Console.WriteLine("#    {0,-36} {1,10:N0}",
                    leakTest.TestName.Substring(0, Math.Min(36, leakTest.TestName.Length)),
                    leakTest.LeakSize);
            }
        }

        // Display help documentation.
        private static void Help()
        {
            Console.WriteLine(@"
TestRunner with no parameters runs all Skyline unit tests (marked [TestMethod])
in random order until the process is killed.  It produces a log file (TestRunner.log)
in the current directory.  You can get a summary of errors and memory leaks by running
""TestRunner report"".

Here is a list of recognized arguments:

    test=[test1,test2,...]          Run one or more tests by name (separated by ',').
                                    Test names can be just the method name, or the method
                                    name prefixed by the class name and a period
                                    (such as IrtTest.IrtFunctionalTest).  Tests must belong
                                    to a class marked [TestClass], although the method does
                                    not need to be marked [TestMethod] to be included in a
                                    test run.  A name prefixed by '@' (such as ""@fail.txt"")
                                    refers to a text file containing test names separated by
                                    white space or new lines.  These files can also include
                                    single-line comments starting with a '#' character.

    skip=[test1,test2,...]          Skip the tests specified by name, using the same scheme
                                    as the test option described above.  You can specify
                                    tests by name or by file (prefixed by the '@' character).

    filter=[a-b,c-d,...]            Once the list of tests has been generated using the test
                                    and/or skip options, filter allows ranges of tests to be
                                    run.  This can be useful in narrowing down a problem that
                                    occurred somewhere in a large test set.  For example,
                                    filter=1-10 will run the first 10 tests in the alphabetized
                                    list. Multiple ranges are allowed, such as 
                                    filter=3-7,9,13-19.

    loop=[n]                        Run the tests ""n"" times, where n is a non-negative
                                    integer.  A value of 0 will run the tests forever
                                    (or until the process is killed).  That is the default
                                    setting if the loop argument is not specified.

    repeat=[n]                      Repeat each test ""n"" times, where n is a positive integer.
                                    This can help diagnose consistent memory leaks, in contrast
                                    with a leak that occurs only the first time a test is run.

    random=[on|off]                 Run the tests in random order (random=on, the default)
                                    or alphabetic order (random=off).  Each test is run
                                    exactly once per loop, regardless of the order.
                                    
    offscreen=[on|off]              Set offscreen=on (the default) to keep Skyline windows
                                    from flashing on the desktop during a test run.

    language=[language1,language2,...]  Choose a random language from this list before executing
                                    each test.  Default value is ""en-US,fr-FR"".  You can
                                    specify just one language if you want all tests to run
                                    in that language.

    demo=[on|off]                   Set demo=on to pause slightly at PauseForScreenshot() calls
                                    maximize the main window and show all-chromatograms graph
                                    in lower-right corner

    multi=[n]                       Multiply timeouts in unit tests by a factor of ""n"".
                                    This is necessary when running multiple instances of 
                                    TestRunner simultaneously.

    log=[file]                      Writes log information to the specified file.  The
                                    default log file is TestRunner.log in the current
                                    directory.

    report=[file]                   Displays a summary of the errors and memory leaks
                                    recorded in the log file produced during a prior
                                    run of TestRunner.  If you don't specify a file,
                                    it will use TestRunner.log in the current directory.
                                    The report is formatted so it can be used as an input
                                    file for the ""test"" or ""skip"" options in a subsequent
                                    run.

    profile=[on|off]                Set profile=on to enable memory profiling mode.
                                    TestRunner will pause for 10 seconds after the first
                                    test is run to allow you to take a memory snapshot.
                                    After the test run it will sleep instead of terminating
                                    to allow you to take a final memory snapshot.

    vendors=[on|off]                If vendors=on, Skyline's tests will use vendor readers to
                                    read data files.  If vendors=off, tests will read data using
                                    the mzML format.  This is useful to isolate memory leaks or
                                    other problems that might occur in the vendor readers.

    clipboardcheck                  When this argument is specified, TestRunner runs
                                    each test once, and makes sure that it did not use
                                    the system clipboard.  If a test uses the clipboard,
                                    stress testing might be compromised on a computer
                                    which is running other processes simultaneously.
");
        }

        private static void ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
            Console.WriteLine(e.Exception.StackTrace);
        }

        public static IEnumerable<TItem> RandomOrder<TItem>(this IList<TItem> list)
        {
            int count = list.Count;
            var indexOrder = new int[count];
            for (int i = 0; i < count; i++)
                indexOrder[i] = i;
            Random r = new Random();
            for (int i = 0; i < count; i++)
            {
                int index = r.Next(count);
                int swap = indexOrder[0];
                indexOrder[0] = indexOrder[index];
                indexOrder[index] = swap;
            }
            foreach (int i in indexOrder)
            {
                yield return list[i];
            }
        }

    }

    public class SystemSleep : IDisposable
    {
        public SystemSleep()
        {
            // Prevent system sleep.
            SetThreadExecutionState(
                EXECUTION_STATE.ES_AWAYMODE_REQUIRED |
                EXECUTION_STATE.ES_CONTINUOUS |
                EXECUTION_STATE.ES_SYSTEM_REQUIRED);
        }


        public void Dispose()
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_SYSTEM_REQUIRED = 0x00000001
        }
    }
}
