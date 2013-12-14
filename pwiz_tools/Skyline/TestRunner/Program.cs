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
using System.Text;
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
        private static readonly string[] TEST_DLLS = {"Test.dll", "TestA.dll", "TestFunctional.dll", "TestTutorial.dll"};
        private static readonly string[] FORMS_DLLS = { "TestFunctional.dll", "TestTutorial.dll" };
        private static int _failureCount;

        [STAThread]
        static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ThreadExceptionEventHandler;

            // Parse command line args and initialize default values.
            const string commandLineOptions =
                "?;/?;-?;help;skylinetester;debug;results;" +
                "test;skip;filter;form;" +
                "loop=0;repeat=1;pause=0;random=on;offscreen=on;multi=1;demo=off;" +
                "clipboardcheck=off;profile=off;vendors=on;culture=fr-FR,en-US;" +
                "log=TestRunner.log;report=TestRunner.log;summary";
            var commandLineArgs = new CommandLineArgs(args, commandLineOptions);

            switch (commandLineArgs.SearchArgs("?;/?;-?;help;report"))
            {
                case "?":
                case "/?":
                case "-?":
                case "help":
                    Help();
                    return;

                case "report":
                    Report(commandLineArgs.ArgAsString("report"));
                    return;
            }

            Console.WriteLine("\nTestRunner " + string.Join(" ", args) + "\n");
            //Console.WriteLine("Process: {0}\n", Process.GetCurrentProcess().Id);

            if (commandLineArgs.HasArg("debug"))
            {
                Console.WriteLine("*** Launching debugger ***\n\n");

                // NOTE: For efficient debugging of Skyline, it is most useful to choose a debugger
                // that already has Skyline.sln loaded.  Otherwise, you might not be able to set
                // breakpoints.
                Debugger.Break();
            }

            var skylineDirectory = GetSkylineDirectory();

            // Get current SVN revision info.
            var startDate = DateTime.Now.ToShortDateString();
            int revision = 0;
            try
            {
                if (skylineDirectory != null)
                {
                    Process svn = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            FileName = @"c:\Program Files (x86)\VisualSVN\bin\svn.exe",
                            Arguments = @"info -r HEAD " + skylineDirectory.FullName
                        }
                    };
                    svn.Start();
                    string svnOutput = svn.StandardOutput.ReadToEnd();
                    svn.WaitForExit();
                    var revisionString = Regex.Match(svnOutput, @".*Revision: (\d+)").Groups[1].Value;
                    revision = int.Parse(revisionString);
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
            }

            // Create log file.
            var logStream = new FileStream(
                commandLineArgs.ArgAsString("log"),
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);
            var log = new StreamWriter(logStream);

            int elapsedMinutes = 0;
            int testCount = 0;
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

                testCount = testList.Count;
                if (testCount == 0)
                {
                    Console.WriteLine("No tests found");
                    return;
                }

                var passes = commandLineArgs.ArgAsLong("loop");
                var repeat = commandLineArgs.ArgAsLong("repeat");

                // Prevent system sleep.
                using (new SystemSleep())
                {
                    // Pause before first test for profiling.
                    bool profiling = commandLineArgs.ArgAsBool("profile");
                    if (profiling)
                    {
                        Console.WriteLine("\nRunning each test once to warm up memory...\n");
                        RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, 1, 1);
                        Console.WriteLine("\nTaking memory snapshot...\n");
//                        MemoryProfiler.Snapshot();
                        if (passes == 0)
                            passes = 1;
                    }

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

                    RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, passes, repeat);

                    stopwatch.Stop();
                    elapsedMinutes = (int) (stopwatch.ElapsedMilliseconds/1000/60);

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
            }

            // Display report.
            log.Close();
            Console.WriteLine("\n");
            Report(commandLineArgs.ArgAsString("log"));

            if (commandLineArgs.HasArg("summary"))
            {
                var summaryFile = commandLineArgs.ArgAsString("summary");
                var sb = new StringBuilder();
                if (!File.Exists(summaryFile))
                    sb.AppendLine("Date,Revision,Run time (minutes),Number of tests,Number of failures,Managed memory,Unmanaged memory");

                const double mb = 1024 * 1024;
                var managedMemory = (int) Math.Round(GC.GetTotalMemory(true) / mb);
                var totalMemory = (int) Math.Round(Process.GetCurrentProcess().PrivateMemorySize64 / mb);

                sb.Append(startDate);
                sb.Append(",");
                sb.Append(revision);
                sb.Append(",");
                sb.Append(elapsedMinutes);
                sb.Append(",");
                sb.Append(testCount);
                sb.Append(",");
                sb.Append(_failureCount);
                sb.Append(",");
                sb.Append(managedMemory);
                sb.Append(",");
                sb.Append(totalMemory);
                sb.AppendLine();

                var originalSummaryFile = summaryFile;
                for (int i = 1; i < 1000; i++)
                {
                    try
                    {
                        File.AppendAllText(summaryFile, sb.ToString());
                        break;
                    }
// ReSharper disable EmptyGeneralCatchClause
                    catch
// ReSharper restore EmptyGeneralCatchClause
                    {
                    }
                    summaryFile = Path.GetFileNameWithoutExtension(originalSummaryFile) + "-" + i + Path.GetExtension(originalSummaryFile);
                }
            }

            // If the forms/tests cache was regenerated, copy it to Skyline directory.
            if (commandLineArgs.ArgAsString("form") == "__REGEN__" && skylineDirectory != null)
            {
                var testRunnerDirectory = Path.Combine(skylineDirectory.FullName, "TestRunner");
                if (Directory.Exists(testRunnerDirectory))
                    FormLookup.CopyCacheFile(testRunnerDirectory);
            }

            // Ungraceful exit to avoid unwinding errors
            Process.GetCurrentProcess().Kill();
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
        private static void RunTestPasses(List<TestInfo> testList, List<TestInfo> unfilteredTestList, CommandLineArgs commandLineArgs, StreamWriter log, long loopCount, long repeat)
        {
            bool randomOrder = commandLineArgs.ArgAsBool("random");
            bool demoMode = commandLineArgs.ArgAsBool("demo");
            bool offscreen = commandLineArgs.ArgAsBool("offscreen");
            bool useVendorReaders = commandLineArgs.ArgAsBool("vendors");
            int timeoutMultiplier = (int) commandLineArgs.ArgAsLong("multi");
            int pauseSeconds = (int) commandLineArgs.ArgAsLong("pause");
            var formList = commandLineArgs.ArgAsString("form");
            var pauseDialogs = (string.IsNullOrEmpty(formList)) ? null : formList.Split(',');
            var results = commandLineArgs.ArgAsString("results");

            var runTests = new RunTests(demoMode, offscreen, pauseDialogs, pauseSeconds, useVendorReaders, timeoutMultiplier, results, log);

            if (commandLineArgs.ArgAsBool("clipboardcheck"))
            {
                runTests.TestContext.Properties["ClipboardCheck"] = "TestRunner clipboard check";
                Console.WriteLine("Checking clipboard use for {0} tests...\n", testList.Count);
                loopCount = 1;
                randomOrder = false;
            }
            else
            {
                Console.WriteLine("Running {0}{1} tests{2}{3}...\n",
                    testList.Count,
                    testList.Count < unfilteredTestList.Count ? "/" + unfilteredTestList.Count : "",
                    (loopCount == 0) ? " forever" : (loopCount == 1) ? "" : " in " + loopCount + " loops",
                    (repeat <= 1) ? "" : ", repeated " + repeat + " times each");
            }

            // Get list of cultures
            var cultures = commandLineArgs.ArgAsString("culture").Split(',');

            List<string> shownForms = (formList == "__REGEN__") ? new List<string>() : null;
            runTests.Skyline.Set("ShownForms", shownForms);

            // Prepare for showing specific forms, if desired.
            var formLookup = new FormLookup();

            // Run all test passes.
            for (var pass = 1; pass <= loopCount || loopCount == 0; pass++)
            {
                runTests.Culture = new CultureInfo(cultures[(pass-1) % cultures.Length]);

                // Run each test in this test pass.
                int testNumber = 0;
                var testPass = randomOrder ? testList.RandomOrder() : testList;
                foreach (var test in testPass)
                {
                    testNumber++;

                    for (int repeatCounter = 1; repeatCounter <= repeat; repeatCounter++)
                        runTests.Run(test, pass, testNumber);

                    // Record which forms the test showed.
                    if (shownForms != null)
                    {
                        formLookup.AddForms(test.TestMethod.Name, runTests.LastTestDuration, shownForms);
                        shownForms.Clear();
                    }
                }
            }

            _failureCount = runTests.FailureCount;
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
                testDict.Add(testNames[i], i);
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

        // Generate a summary report of errors and memory leaks from a log file.
        private static void Report(string logFile)
        {
            var logStream = new StreamReader(logFile);
            var errorList = new Dictionary<string, int>();
            var managedMemoryUse = new Dictionary<string, List<double>>();
            var totalMemoryUse = new Dictionary<string, List<double>>();

            var test = "";
            var managedMemory = 0.0;
            var totalMemory = 0.0;
            int pass = 1;

            while (true)
            {
                var line = logStream.ReadLine();
                if (line == null) break;
                line = Regex.Replace(line, @"\s+", " ").Trim();
                var parts = line.Split(' ');

                // Is it an error line?
                if (parts[0] == "***")
                {
                    var error = test + "  # {0} failures ({1:0.##}%)\n";
                    while (true)
                    {
                        line = logStream.ReadLine();
                        if (line == null || line.StartsWith("***")) break;
                        error += "# " + line + "\n";
                    }
                    if (line == null) break;

                    if (errorList.ContainsKey(error))
                    {
                        errorList[error]++;
                    }
                    else
                    {
                        errorList[error] = 1;
                    }
                }

                // Test information line.
                else if (line.StartsWith("[") && parts.Length > 6)
                {
                    // Save previous memory use to calculate memory used by this test.
                    var lastManagedMemory = managedMemory;
                    var lastTotalMemory = totalMemory;

                    pass = int.Parse(parts[1].Split('.')[0]);
                    var testParts = parts[2].Split('.');
                    test = testParts[testParts.Length - 1];
                    managedMemory = Double.Parse(parts[6].Split('/')[0]);
                    totalMemory = Double.Parse(parts[6].Split('/')[1]);

                    // Only collect memory leak information starting on pass 2.
                    if (pass < 2)
                    {
                        managedMemoryUse[test] = new List<double>();
                        totalMemoryUse[test] = new List<double>();
                    }
                    else
                    {
                        managedMemoryUse[test].Add(managedMemory - lastManagedMemory);
                        totalMemoryUse[test].Add(totalMemory - lastTotalMemory);
                    }
                }
            }

            // Print list of errors sorted in descending order of frequency.
            if (errorList.Count == 0)
            {
                Console.WriteLine("# No failures.\n");
            }
            foreach (KeyValuePair<string, int> item in errorList.OrderByDescending(x => x.Value))
            {
                var errorInfo = item.Key;
                var errorCount = item.Value;
                Console.WriteLine(errorInfo, errorCount, 100.0 * errorCount / pass);
            }

            // Print top memory leaks, unless they are less than 0.1 MB.
            ReportLeaks(managedMemoryUse, "# Top managed memory leaks (in MB per execution):");
            ReportLeaks(totalMemoryUse, "# Top total memory leaks (in MB per execution):");
        }

        private static void ReportLeaks(Dictionary<string, List<double>> memoryUse, string title)
        {
            var leaks = "";
            int leakCount = 0;
            foreach (var item in memoryUse.OrderByDescending(x => x.Value.Count > 0 ? x.Value.Average() : 0.0))
            {
                if (item.Value.Count == 0) break;
                var min = Math.Max(0, item.Value.Min());
                var max = item.Value.Max();
                var mean = item.Value.Average();
                if (++leakCount > 5 || mean < 0.1) break;
                leaks += string.Format("  {0,-40} #  min={1:0.00}  max={2:0.00}  mean={3:0.00}\n",
                                       item.Key, min, max, mean);
            }
            if (leaks != "")
            {
                Console.WriteLine(title);
                Console.WriteLine(leaks);
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

    culture=[culture1,culture2,...] Choose a random culture from this list before executing
                                    each test.  Default value is ""en-US,fr-FR"".  You can
                                    specify just one culture if you want all tests to run
                                    in that culture.

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
