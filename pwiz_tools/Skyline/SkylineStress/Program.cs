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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SkylineStress
{
    internal class Program
    {
        private static readonly string[] TestDlls = {"Test.dll", "TestA.dll", "TestFunctional.dll", "TestTutorial.dll"};
        private static List<TestInfo> _testList = new List<TestInfo>(); 
        private static Type _program;

        private class TestInfo
        {
            public readonly Type TestClass;
            public readonly MethodInfo TestMethod;
            public readonly MethodInfo SetTestContext;

            public TestInfo(Type testClass, MethodInfo testMethod)
            {
                TestClass = testClass;
                TestMethod = testMethod;
                SetTestContext = testClass.GetMethod("set_TestContext");
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                // Parse command line args and initialize default values.
                CommandLineArgs.ParseArgs(args,
                                          "?;/?;-?;help;test;skip;filter;clipboardcheck=off;log=SkylineStress.log;report=SkylineStress.log;loop=0;random=on;offscreen=on");

                switch (CommandLineArgs.SearchArgs("?;/?;-?;help;report"))
                {
                    case "?":
                    case "/?":
                    case "help":
                        Help();
                        return;

                    case "report":
                        Report(CommandLineArgs.ArgAsString("report"));
                        return;
                }

                // Load list of tests.
                LoadTestList();
                if (_testList.Count == 0)
                {
                    Console.WriteLine("No tests found");
                    return;
                }

                var skyline = Assembly.LoadFrom(GetAssemblyPath("Skyline.exe"));
                _program = skyline.GetType("pwiz.Skyline.Program");
                _program.GetMethod("set_SkylineOffscreen").Invoke(null, new object[] { CommandLineArgs.ArgAsBool("offscreen") });

                RunTests();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        // Run all test passes.
        private static void RunTests()
        {
            var passes = CommandLineArgs.ArgAsLong("loop");
            var randomOrder = CommandLineArgs.ArgAsBool("random");
            
            if (CommandLineArgs.ArgAsBool("clipboardcheck"))
            {
                Console.WriteLine("Checking clipboard use for {0} tests...\n", _testList.Count);
                passes = 1;
                randomOrder = false;
            }
            else if (passes == 0)
            {
                Console.WriteLine("Running {0} tests forever...\n", _testList.Count);
            }
            else
            {
                Console.WriteLine("Running {0} tests {1} times each...\n", _testList.Count, passes);
            }

            // Create log file.
            var log = new StreamWriter(CommandLineArgs.ArgAsString("log"));

            // Get test results directory and provide it to tests via TestContext.
            var now = DateTime.Now;
            var testDirName = string.Format("SkylineStress_{0}-{1:D2}-{2:D2}_{3:D2}-{4:D2}-{5:D2}",
                                            now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            var testDir = Path.Combine(GetProjectPath("TestResults"), testDirName);
            var testContext = new StressTestContext();
            testContext.Properties["TestDir"] = testDir;
            if (CommandLineArgs.ArgAsBool("clipboardcheck"))
            {
                testContext.Properties["ClipboardCheck"] = "SkylineStress clipboard check";
            }
            var context = new object[] { testContext };

            // Sort tests alphabetically.
            _testList.Sort((x, y) => String.CompareOrdinal(x.TestMethod.Name, y.TestMethod.Name));

            // Filter test list.
            if (CommandLineArgs.HasArg("filter"))
            {
                var filterList = new List<TestInfo>();
                var filterRanges = CommandLineArgs.ArgAsString("filter").Split(',');
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
                    for (var i = low; i <= high; i++)
                    {
                        filterList.Add(_testList[i]);
                    }
                }
                _testList = filterList;
            }

            // Initialize variables for all test passes.
            var failureCount = 0;
            var errorList = new Dictionary<string, int>();
            var failureList = new Dictionary<string, int>();
            var random = new Random();
            var testOrder = new List<int>();

            foreach (var testInfo in _testList)
            {
                failureList[testInfo.TestMethod.Name] = 0;
            }

            // Run all test passes.
            for (var pass = 1; pass <= passes || passes == 0; pass++)
            {
                // Create test order for this pass.
                testOrder.AddRange(_testList.Select((t, i) => i));

                // Run each test in this test pass.
                var testNumber = 0;
                while (testOrder.Count > 0)
                {
                    // Choose next test in sequential or random order (each test executes exactly once per pass).
                    var testOrderIndex = 0;
                    if (randomOrder)
                    {
                        testOrderIndex = random.Next(testOrder.Count);
                    }
                    var testIndex = testOrder[testOrderIndex];
                    testOrder.RemoveAt(testOrderIndex);
                    var test = _testList[testIndex];

                    // Record information for this test.
                    testNumber++;
                    var testName = test.TestMethod.Name;
                    var time = DateTime.Now;
                    var info = string.Format("[{0}:{1}] {2,3}.{3,-3} {4,-40}  ",
                        time.Hour.ToString("D2"), time.Minute.ToString("D2"), pass, testNumber, testName);
                    Console.Write(info);
                    log.Write(info);
                    log.Flush();

                    // Delete test directory.
                    if (Directory.Exists(testDir))
                    {
                        try
                        {
                            // Try delete 4 times to give anti-virus software a chance to finish.
                            TryLoop.Try<IOException>(() => Directory.Delete(testDir, true), 4);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    // Create test class.
                    var testObject = Activator.CreateInstance(test.TestClass);

                    // Set the TestContext.
                    if (test.SetTestContext != null)
                    {
                        test.SetTestContext.Invoke(testObject, context);
                    }

                    // Run the test.
                    var stopwatch = new Stopwatch();
                    try
                    {
                        // Run the test and time it.
                        stopwatch.Start();
                        test.TestMethod.Invoke(testObject, null);
                        stopwatch.Stop();

                        // Test succeeded.
                        var memoryUsed = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
                        info = string.Format("{0,3} failures, {1:0.0} MB, {2} sec.", failureCount, memoryUsed, stopwatch.ElapsedMilliseconds / 1000);
                        Console.WriteLine(info);
                        log.WriteLine(info);
                        log.Flush();
                    }
                    catch (Exception e)
                    {
                        // Save failure information to log.
                        failureCount++;
                        failureList[testName]++;
                        info = testName + " {0} failures ({1:0.##}%)\n" +
                            e.InnerException.Message + "\n" +
                            e.InnerException.StackTrace;
                        if (errorList.ContainsKey(info))
                        {
                            errorList[info]++;
                        }
                        else
                        {
                            errorList[info] = 1;
                        }
                        var memoryUsed = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
                        Console.WriteLine("*** FAILED {0:0.#}% ***", 100.0 * failureList[testName] / pass);
                        log.WriteLine("{0,3} failures, {1:0.0} MB\n*** failure {2}\n{3}\n{4}\n***", 
                            failureCount, memoryUsed, errorList[info], e.InnerException.Message, e.InnerException.StackTrace);
                        log.Flush();
                    }
                }
            }

            // Display report.
            log.Close();
            Console.WriteLine("\n");
            Report(CommandLineArgs.ArgAsString("log"));
        }

        // Load list of tests to be run into TestList.
        private static void LoadTestList()
        {
            // Load lists of tests to run.
            var testList = LoadList("test");
            var skipList = LoadList("skip");

            // Find tests in the test dlls.
            foreach (var testDll in TestDlls)
            {
                var assembly = Assembly.LoadFrom(GetAssemblyPath(testDll));
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    if (type.IsClass && HasAttribute(type, "TestClassAttribute"))
                    {
                        var methods = type.GetMethods();
                        foreach (var method in methods)
                        {
                            var testName = type.Name + "." + method.Name;
                            if (testList.Contains(testName) || testList.Contains(method.Name) ||
                                (testList.Count == 0 && HasAttribute(method, "TestMethodAttribute")))
                            {
                                if (!skipList.Contains(testName) && !skipList.Contains(method.Name))
                                {
                                    _testList.Add(new TestInfo(type, method));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static string GetAssemblyPath(string assembly)
        {
            var stressExeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (stressExeDirectory == null) throw new ApplicationException("Can't find path to SkylineStress.exe");
            return Path.Combine(stressExeDirectory, assembly);
        }

        public static string GetProjectPath(string relativePath)
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

        // Determine if the given class or method from an assembly has the given attribute.
        private static bool HasAttribute(MemberInfo info, string attributeName)
        {
            var attributes = info.GetCustomAttributes(false);
            return attributes.Any(attribute => attribute.ToString().EndsWith(attributeName));
        }

        // Load a list of tests specified on the command line as a comma-separated list.  Any name prefixed with '@'
        // is a file containing test names separated by white space or new lines, with '#' indicating a comment.
        private static List<string> LoadList(string optionName)
        {
            var inputList = CommandLineArgs.ArgAsString(optionName).Split(',');
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
            var memoryUse = new Dictionary<string, List<double>>();

            var test = "";
            var memory = 0.0;
            var pass = 0.0;

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
                else if (parts.Length > 6)
                {
                    // Save previous memory use to calculate memory used by this test.
                    var lastMemory = memory;

                    pass = Math.Truncate(Double.Parse(parts[1]));
                    var testParts = parts[2].Split('.');
                    test = testParts[testParts.Length - 1];
                    memory = Double.Parse(parts[5]);

                    // Only collect memory leak information starting on pass 2.
                    if (pass < 2.0)
                    {
                        memoryUse[test] = new List<double>();
                    }
                    else
                    {
                        var memoryDiff = memory - lastMemory;
                        memoryUse[test].Add(memoryDiff);
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
            var leaks = "";
            foreach (var item in memoryUse.OrderByDescending(x => x.Value.Count > 0 ? x.Value.Average() : 0.0))
            {
                if (item.Value.Count == 0) break;
                var min = Math.Max(0, item.Value.Min());
                var max = item.Value.Max();
                var mean = item.Value.Average();
                if (mean < 0.1) break;
                leaks += string.Format("  {0,-40} #  min={1:0.00}  max={2:0.00}  mean={3:0.00}\n",
                                       item.Key, min, max, mean);
            }
            if (leaks != "")
            {
                Console.WriteLine("# Top leaks (in MB per execution):\n{0}", leaks);
            }
        }

        // Display help documentation.
        private static void Help()
        {
            Console.WriteLine(@"
SkylineStress with no parameters runs all Skyline unit tests (marked [TestMethod])
in random order until the process is killed.  It produces a log file (SkylineStress.log)
in the current directory.  You can get a summary of errors and memory leaks by running
""SkylineStress report"".

Here is a list of recognized arguments:

    test=[test1,test2,...]          Run one or more tests by name (separated by ',').
                                    Test names can be just the method name, or the method
                                    name prefixed by the class name and a period
                                    (such as IrtTest.IrtFunctionalTest).  Tests must belong
                                    to a class marked [TestClass], although the method does
                                    not need to be marked [TestMethod] to be included in a
                                    stress run.  A name prefixed by '@' (such as ""@fail.txt"")
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

    random=[on|off]                 Run the tests in random order (random=on, the default)
                                    or alphabetic order (random=off).  Each test is run
                                    exactly once per loop, regardless of the order.
                                    
    offscreen=[on|off]              Set offscreen=on (the default) to keep Skyline windows
                                    from flashing on the desktop during a stress run.

    log=[file]                      Writes log information to the specified file.  The
                                    default log file is SkylineStress.log in the current
                                    directory.

    report=[file]                   Displays a summary of the errors and memory leaks
                                    recorded in the log file produced during a prior
                                    run of SkylineStress.  If you don't specify a file,
                                    it will use SkylineStress.log in the current directory.
                                    The report is formatted so it can be used as an input
                                    file for the ""test"" or ""skip"" options in a subsequent
                                    run.

    clipboardcheck                  When this argument is specified, SkylineStress runs
                                    each test once, and makes sure that it did not use
                                    the system clipboard.  If a test uses the clipboard,
                                    stress testing might be compromised on a computer
                                    which is running other processes simultaneously.
");
        }
    }
}
