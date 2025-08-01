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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ionic.Zip;
using Microsoft.Win32;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
//WARNING: Including TestUtil in this project causes a strange build problem, where the first
//         build from Visual Studio after a full bjam build removes all of the Skyline project
//         root files from the Skyline bin directory, leaving it un-runnable until a full
//         rebuild is performed.  Do not commit a reference to TestUtil to this project without
//         testing this case and getting someone else to validate that you have fixed this
//         problem.
//using pwiz.SkylineTestUtil;
using TestRunnerLib;
using TestRunnerLib.PInvoke;


namespace TestRunner
{
    internal static class Program
    {
        private static readonly string[] TEST_DLLS = { "Test.dll", "TestData.dll", "TestConnected.dll", "TestFunctional.dll", "TestTutorial.dll", "CommonTest.dll", "TestPerf.dll" };

        private static readonly string executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string[] allLanguages = new FindLanguages(executingDirectory, "en-US", "fr-FR", "tr-TR").Enumerate().ToArray(); // Languages used in pass 1, and in pass 2 perftets
        private static readonly string[] qualityLanguages = allLanguages.Where(l => !l.StartsWith("tr")).ToArray(); // "fr" and "tr" pretty much test the same thing, so just use fr in pass 2

        private const int LeakTrailingDeltas = 7;   // Number of trailing deltas to average and check against thresholds below
        // CONSIDER: Ideally these thresholds would be zero, but memory and handle retention are not stable enough to support that
        //           The problem is that we don't reliably return to exactly the same state during EndTest and these numbers go both up and down
        private const int KB = 1024;
        private static LeakTracking LeakThresholds = new LeakTracking
        {
            // Average delta per test between 8 runs (7 deltas)
            TotalMemory = 150 * KB, // Too much variance to track leaks in just 12 runs
            HeapMemory = 20 * KB,
            ManagedMemory = 8 * KB,
            TotalHandles = 2,
            UserGdiHandles = 1
        };
        private const int CrtLeakThreshold = 1000;  // No longer used
        private const int LeakCheckIterations = 24; // Maximum number of runs to try to achieve below thresholds for trailing deltas
        private static bool IsFixedLeakIterations { get { return false; } } // CONSIDER: It would be nice to make this true to reduce test run count variance

        class ExpandedLeakCheck
        {
            public ExpandedLeakCheck(int? iterations = null, bool reportLeakEarly = false)
            {
                Iterations = iterations ?? LeakCheckIterations * 2;
                ReportLeakEarly = reportLeakEarly;
            }

            /// <summary>
            /// Only if <see cref="ReportLeakEarly"/> is false, the number of
            /// iterations used to detect a leak is extended to this value.
            /// </summary>
            public int Iterations { get; set; }

            /// <summary>
            /// Leaks get reported at the normal number of iterations. If a leak
            /// is detected testing begins an infinite loop on the failing test
            /// to get more information on whether it is truly leaking.
            /// </summary>
            public bool ReportLeakEarly { get; set; }
        }

        // These tests get extra runs to meet the leak thresholds
        private static Dictionary<string, ExpandedLeakCheck> LeakCheckIterationsOverrideByTestName = new Dictionary<string, ExpandedLeakCheck>
        {
            // These tests check leaks at the normal time and then start an infinite
            // loop when a leak is detected.
            {"TestGroupedStudiesTutorialDraft", new ExpandedLeakCheck(LeakCheckIterations * 4, true)},
            {"TestInstrumentInfo", new ExpandedLeakCheck(LeakCheckIterations * 2, true)},
            // These tests expand the number of iterations before reporting a leak
            // with no potential for infinite looping on a detected leak.
            {"TestLibraryExplorer", new ExpandedLeakCheck()},
            {"TestLibraryExplorerAsSmallMolecules", new ExpandedLeakCheck()},
        };

        //  These tests only need to be run once, regardless of language, so they get turned off in pass 0 after a single invocation
        public static string[] RunOnceTestNames = { "AaantivirusTestExclusion", "CodeInspection" };

        // These tests are allowed to fail the total memory leak threshold, and extra iterations are not done to stabilize a spiky total memory distribution
        public static string[] MutedTotalMemoryLeakTestNames = { "TestMs1Tutorial", "TestGroupedStudiesTutorialDraft", "TestPermuteIsotopeModifications" };

        // These tests are allowed to fail the heap memory leak threshold, and extra iterations are not done to stabilize a spiky total memory distribution
        public static string[] MutedHeapMemoryLeakTestNames = { };

        // These tests are allowed to fail the total handle leak threshold, and extra iterations are not done to stabilize a spiky total handle distribution
        public static string[] MutedTotalHandleLeakTestNames = { };

        // These tests are allowed to fail the user/GDI handle leak threshold, and extra iterations are not done to stabilize a spiky handle distribution
        public static string[] MutedUserGdiHandleLeakTestNames = { };

        private static int GetLeakCheckIterations(TestInfo test)
        {
            return LeakCheckIterationsOverrideByTestName.TryGetValue(test.TestMethod.Name, out var value)
                ? value.Iterations
                : LeakCheckIterations;
        }

        private static bool GetLeakCheckReportEarly(TestInfo test)
        {
            return LeakCheckIterationsOverrideByTestName.ContainsKey(test.TestMethod.Name) &&
                   LeakCheckIterationsOverrideByTestName[test.TestMethod.Name].ReportLeakEarly;
        }


        private struct LeakTracking
        {
            public LeakTracking(RunTests runTests) : this()
            {
                TotalMemory = runTests.TotalMemoryBytes;
                HeapMemory = runTests.CommittedMemoryBytes;
                ManagedMemory = runTests.ManagedMemoryBytes;
                TotalHandles = runTests.LastTotalHandleCount;
                UserGdiHandles = runTests.LastUserHandleCount + runTests.LastGdiHandleCount;
            }

            public double TotalMemory { get; set; }
            public double HeapMemory { get; set; }
            public double ManagedMemory { get; set; }
            public double TotalHandles { get; set; }
            public double UserGdiHandles { get; set; }

            public bool BelowThresholds(LeakTracking leakThresholds, string testName)
            {
                return (TotalMemory < leakThresholds.TotalMemory || MutedTotalMemoryLeakTestNames.Contains(testName)) &&
                       (HeapMemory < leakThresholds.HeapMemory || MutedHeapMemoryLeakTestNames.Contains(testName)) &&
                       ManagedMemory < leakThresholds.ManagedMemory &&
                       (TotalHandles < leakThresholds.TotalHandles || MutedTotalHandleLeakTestNames.Contains(testName)) &&
                       (UserGdiHandles < leakThresholds.UserGdiHandles || MutedUserGdiHandleLeakTestNames.Contains(testName));
            }

            public static LeakTracking MeanDeltas(List<LeakTracking> values)
            {
                return new LeakTracking
                {
                    TotalMemory = MeanDelta(values, l => l.TotalMemory),
                    HeapMemory = MeanDelta(values, l => l.HeapMemory),
                    ManagedMemory = MeanDelta(values, l => l.ManagedMemory),
                    TotalHandles = MeanDelta(values, l => l.TotalHandles),
                    UserGdiHandles = MeanDelta(values, l => l.UserGdiHandles)
                };
            }
            private static double MeanDelta(List<LeakTracking> values, Func<LeakTracking, double> getValue)
            {
                var listDelta = new List<double>();
                for (int i = 1; i < values.Count; i++)
                    listDelta.Add(getValue(values[i]) - getValue(values[i - 1]));
                return listDelta.Average();
            }

            public string GetLeakMessage(LeakTracking leakThresholds, string testName)
            {
                var message = string.Empty;
                if (ManagedMemory >= leakThresholds.ManagedMemory)
                    message += string.Format("!!! {0} LEAKED {1:0.#} Managed bytes\r\n", testName, ManagedMemory);
                if (HeapMemory >= leakThresholds.HeapMemory && !MutedHeapMemoryLeakTestNames.Contains(testName))
                    message += string.Format("!!! {0} LEAKED {1:0.#} Heap bytes\r\n", testName, HeapMemory);
                if (TotalMemory >= leakThresholds.TotalMemory && !MutedTotalMemoryLeakTestNames.Contains(testName))
                    message += string.Format("!!! {0} LEAKED {1:0.#} bytes\r\n", testName, TotalMemory);
                if (UserGdiHandles >= leakThresholds.UserGdiHandles && !MutedUserGdiHandleLeakTestNames.Contains(testName))
                    message += string.Format("!!! {0} HANDLE-LEAKED {1:0.#} User+GDI\r\n", testName, UserGdiHandles);
                if (TotalHandles >= leakThresholds.TotalHandles && !MutedTotalHandleLeakTestNames.Contains(testName))
                    message += string.Format("!!! {0} HANDLE-LEAKED {1:0.#} Total\r\n", testName, TotalHandles);
                return string.IsNullOrEmpty(message) ? null : message;
            }

            public string GetLogMessage(string testName, int passedCount)
            {
                // Report the final mean average deltas over the passing or final 8 runs (7 deltas)
                return string.Format("# {0} deltas ({1}): {2}\r\n", testName, passedCount, this);
            }

            public override string ToString()
            {
                return string.Format("managed = {0:0.#} KB, heap = {1:0.#} KB, memory = {2:0.#} KB, user-gdi = {3:0.#}, total = {4:0.#}",
                    ManagedMemory / KB, HeapMemory / KB, TotalMemory / KB, UserGdiHandles, TotalHandles);
            }

            public LeakTracking Max(LeakTracking lastDeltas)
            {
                return new LeakTracking
                {
                    TotalMemory = Math.Max(TotalMemory, lastDeltas.TotalMemory),
                    HeapMemory = Math.Max(HeapMemory, lastDeltas.HeapMemory),
                    ManagedMemory = Math.Max(ManagedMemory, lastDeltas.ManagedMemory),
                    TotalHandles = Math.Max(TotalHandles, lastDeltas.TotalHandles),
                    UserGdiHandles = Math.Max(UserGdiHandles, lastDeltas.UserGdiHandles)
                };
            }

            public LeakTracking Min(LeakTracking lastDeltas)
            {
                return new LeakTracking
                {
                    TotalMemory = Math.Min(TotalMemory, lastDeltas.TotalMemory),
                    HeapMemory = Math.Min(HeapMemory, lastDeltas.HeapMemory),
                    ManagedMemory = Math.Min(ManagedMemory, lastDeltas.ManagedMemory),
                    TotalHandles = Math.Min(TotalHandles, lastDeltas.TotalHandles),
                    UserGdiHandles = Math.Min(UserGdiHandles, lastDeltas.UserGdiHandles)
                };
            }
        }

        static readonly string commandLineOptions =
            "?;/?;-?;help;skylinetester;debug;results;" +
            "test;skip;filter;form;" +
            "loop=0;repeat=1;pause=0;startingshot=1;random=off;offscreen=on;multi=1;wait=off;internet=off;originalurls=off;" +
            "parallelmode=off;workercount=0;waitforworkers=off;keepworkerlogs=off;checkdocker=on;workername;queuehost;workerport;workertimeout;alwaysupcltpassword;" +
            "coverage=off;dotcoverexe=jetbrains.dotcover.commandlinetools\\2023.3.3\\tools\\dotCover.exe;" +
            "maxsecondspertest=-1;" +
            "demo=off;showformnames=off;status=off;buildcheck=0;" +
            "quality=off;pass0=off;pass1=off;pass2=on;" +
            "perftests=off;" +
            "retrydatadownloads=off;" +
            "runsmallmoleculeversions=off;" +
            "recordauditlogs=off;" +
            "clipboardcheck=off;profile=off;vendors=on;language=fr-FR,en-US;" +
            "log=TestRunner.log;report=TestRunner.log;dmpdir=Minidumps;teamcitytestdecoration=off;teamcitytestsuite=;verbose=off;listonly;showheader=on";

        private static readonly string dotCoverFilters = "/Filters=+:module=TestRunner /Filters=+:module=Skyline-daily /Filters=+:module=Skyline* /Filters=+:module=CommonTest " +
                                                         "/Filters=+:module=Test* /Filters=+:module=MSGraph /Filters=+:module=ProteomeDb /Filters=+:module=BiblioSpec " +
                                                         "/Filters=+:module=pwiz.Common* /Filters=+:module=ProteowizardWrapper* /Filters=+:module=BullseyeSharp /Filters=+:module=PanoramaClient " +
                                                         "/Filters=-:class=alglib /Filters=-:class=Inference.*";

        [STAThread, MethodImpl(MethodImplOptions.NoOptimization)]
        static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ThreadExceptionEventHandler;

            _testRunStartTime = DateTime.UtcNow;

            // Parse command line args and initialize default values.
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

            if (commandLineArgs.ArgAsString("language") != "en" && commandLineArgs.ArgAsString("language") != "en-US")
                Console.OutputEncoding = Encoding.UTF8;  // So we can send Japanese to SkylineTester, which monitors our stdout

            Console.WriteLine();
            if (!commandLineArgs.ArgAsBool("status") && !commandLineArgs.ArgAsBool("buildcheck") && !commandLineArgs.HasArg("listonly") && commandLineArgs.ArgAsBool("showheader"))
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

            // Create log file.
            var logStream = new FileStream(
                commandLineArgs.ArgAsString("log"),
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite);
            var log = new StreamWriter(logStream);

            bool allTestsPassed = true;

            // run a client that listens for messages which tell the client to run a test, or quit
            if (commandLineArgs.ArgAsString("parallelmode") == "client")
            {
                return ListenToTestQueue(log, commandLineArgs);
            }

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
                    allTestsPassed = false;
                }
                else if (commandLineArgs.HasArg("listonly"))
                {
                    foreach(var test in testList)
                        Console.WriteLine("{0}\t{1}", Path.GetFileName(test.TestClassType.Assembly.CodeBase), test.TestMethod.Name);
                    return 0;
                }
                else
                {
                    var passes = commandLineArgs.ArgAsLong("loop");
                    var repeat = commandLineArgs.ArgAsLong("repeat");
                    if (commandLineArgs.ArgAsBool("buildcheck"))
                    {
                        passes = 1;
                        repeat = 1;
                    }

                    TeamCityStartTestSuite(commandLineArgs);

                    // Prevent system sleep.
                    using (new Kernel32Test.SystemSleep())
                    {
                        // Pause before first test for profiling.
                        bool profiling = commandLineArgs.ArgAsBool("profile");
                        if (profiling)
                        {
                            Console.WriteLine("\nRunning each test once to warm up memory...\n");
                            allTestsPassed = RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, 1, 1,
                                true);
                            Console.WriteLine("\nTaking memory snapshot...\n");
                            MemoryProfiler.Snapshot("start");
                            if (passes == 0)
                                passes = 1;
                        }

                        allTestsPassed =
                            RunTestPasses(testList, unfilteredTestList, commandLineArgs, log, passes, repeat, profiling) &&
                            allTestsPassed;

                        // Pause for profiling
                        if (profiling)
                        {
                            Console.WriteLine("\nTaking second memory snapshot...\n");
                            MemoryProfiler.Snapshot("end");
                        }
                    }

                    TeamCityFinishTestSuite(commandLineArgs);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\nCaught exception in TestRunnner.Program.Main:\n" + e.Message);
                if (string.IsNullOrEmpty(e.StackTrace))
                    Console.WriteLine("No stacktrace");
                else
                    Console.WriteLine(e.StackTrace);
                if (e.InnerException != null)
                {
                    var inner = e.InnerException;
                    int i = 0;
                    while (inner != null)
                    {
                        Console.WriteLine($"Inner exception {++i}:");
                        Console.WriteLine(inner.Message);
                        if (string.IsNullOrEmpty(inner.StackTrace))
                            Console.WriteLine("No stacktrace");
                        else
                            Console.WriteLine(inner.StackTrace);
                        inner = inner.InnerException;
                    }
                }
                else
                {
                    Console.WriteLine("No inner exception.");
                }
                Console.Out.Flush(); // Get this info to TeamCity or SkylineTester ASAP
                allTestsPassed = false;
            }

            // Display report.
            log.Close();
            Console.WriteLine("\n");
            if (!commandLineArgs.ArgAsBool("status"))
                Report(commandLineArgs.ArgAsString("log"));

            // Ungraceful exit to avoid unwinding errors
            //Process.GetCurrentProcess().Kill();

            if (commandLineArgs.ArgAsBool("wait"))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Console.Out.WriteLine("Press <enter> to continue");
                Console.ReadLine();
            }

            // delete per-process tools directory
            if (Path.GetFileName(ToolDescriptionHelpers.GetToolsDirectory()) != "Tools")
                DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());

            return allTestsPassed ? 0 : 1;
        }

        // from https://stackoverflow.com/questions/13634868/get-the-default-gateway
        public static IPAddress GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties()?.GatewayAddresses)
                .Select(g => g?.Address)
                .FirstOrDefault(a => a?.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6);
                // .Where(a => Array.FindIndex(a.GetAddressBytes(), b => b != 0) >= 0)
        }

        private static int ListenToTestQueue(StreamWriter log, CommandLineArgs commandLineArgs)
        {
            bool allTestsPassed = true;
            int CLIENT_WAIT_TIMEOUT = 10; // time to wait for a message from server before exiting, in seconds

            Action<string> TeeLog = msg =>
            {
                Console.WriteLine(msg);
                log.WriteLine(msg);
            };

            string host;
            if (commandLineArgs.HasArg("queuehost"))
                host = commandLineArgs.ArgAsString("queuehost");
            else
                host = GetDefaultGateway()?.ToString() ?? "localhost";

            string workerName = commandLineArgs.ArgAsString("workername") ?? throw new InvalidOperationException("parallelmode=client processes must have workername parameter set");
            int workerPort = Convert.ToInt32(commandLineArgs.ArgAsString("workerport") ?? throw new InvalidOperationException("parallelmode=client processes must have workerport parameter set"));

            Console.WriteLine($"Connecting to host at {host}:{workerPort}");
            using (var sender = new PushSocket($">tcp://{host}:{workerPort}"))
            using (var receiver = new PullSocket())
            using (var sender2 = new PushSocket())
            using (var cts = new CancellationTokenSource())
            {
                var factory = new TaskFactory(cts.Token);

                int heartbeatPort = 0;

                // start heartbeat thread from server
                void SendWorkerHeartbeat()
                {
                    using (var heartbeatReceiver = new PushSocket())
                    {
                        Interlocked.Add(ref heartbeatPort, heartbeatReceiver.BindRandomPort("tcp://*"));

                        const int maxAttempts = 3;
                        while (!cts.IsCancellationRequested)
                        {
                            for (int attempts = 0; attempts < maxAttempts; ++attempts)
                            {
                                if (!heartbeatReceiver.TrySendFrameEmpty(TimeSpan.FromSeconds(15)))
                                {
                                    TeeLog("Server heartbeat could not be sent.");
                                    if (attempts == maxAttempts-1)
                                    {
                                        cts.Cancel();
                                        break;
                                    }
                                    continue;
                                }
                                break;
                            }
                            Thread.Sleep(3000);
                        }
                    }
                }

                factory.StartNew(() => {
                    try
                    {
                        SendWorkerHeartbeat();
                    }
                    catch (Exception e)
                    {
                        TeeLog("Error sending worker heartbeat: " + e);
                        Environment.Exit(1);
                    }
                }, TaskCreationOptions.LongRunning);

                while(heartbeatPort == 0)
                    Thread.Sleep(500);

                int tasksPort = receiver.BindRandomPort("tcp://*"); // port for receiving tasks from server
                int resultsPort = sender2.BindRandomPort("tcp://*"); // port for sending results to server

                string ip = workerName.Contains("hostWorker") ? "localhost" : Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .First(a => a.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6).ToString();
                string workerId = $"{workerName}/{ip}/{tasksPort}/{resultsPort}/{heartbeatPort}";
                TeeLog($"Sending worker name and IP: {workerId}");
                sender.SendFrame(workerId);


                receiver.ReceiveReady += (s, args) =>
                {
                    var msg = receiver.ReceiveFrameString();

                    // first check for a quit message 
                    if (msg == "TestRunnerQuit")
                    {
                        cts.Cancel();
                        return;
                    }

                    string testName = msg.Split('/')[0];
                    string testLanguage = msg.Split('/')[1];
                    int testPass = Convert.ToInt32(msg.Split('/')[2]);
                    int[] passEnabled = { 0, 0, 0 };
                    passEnabled[testPass] = 1;
                    var cargs = new CommandLineArgs(new[] { "test=" + testName }, commandLineOptions);
                    commandLineArgs.SetArg("language", testLanguage);
                    for (var pass = 0; pass < passEnabled.Length; pass++)
                    {
                        var enabled = passEnabled[pass];
                        commandLineArgs.SetArg("pass" + pass, enabled.ToString());
                    }

                    var testList = LoadTestList(cargs);
                    TeeLog($"Starting test {testName}-{testLanguage}-{testPass}");
                    using (var testLogStream = new MemoryStream())
                    using (var testLog = new StreamWriter(testLogStream, new UTF8Encoding(false)))
                    {
                        bool passed = RunTestPasses(testList, testList, commandLineArgs, testLog, 1, 1);
                        allTestsPassed &= passed;
                        TeeLog(Encoding.UTF8.GetString(testLogStream.GetBuffer()));
                        var resultBuffer = testLogStream.GetBuffer().Prepend(Convert.ToByte(passed));
                        if (!sender2.TrySendFrame(TimeSpan.FromSeconds(CLIENT_WAIT_TIMEOUT), resultBuffer.ToArray(), (int) testLogStream.Length+1))
                        {
                            TeeLog($"Exiting due to no response from server in {CLIENT_WAIT_TIMEOUT} seconds.");
                            cts.Cancel();
                        }
                    }

                    //sender.SendFrame(passed.ToString());
                };

                Thread.Sleep(1000);

                while (!cts.IsCancellationRequested)
                {
                    // This should be redundant with the new "Starting test" message, but may help debugging if there are connection issues
                    // TeeLog("Waiting for message");
                    if (!sender2.TrySignalOK())
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    if (!receiver.Poll(TimeSpan.FromSeconds(CLIENT_WAIT_TIMEOUT)))
                    {
                        TeeLog($"Exiting due to no response from server in {CLIENT_WAIT_TIMEOUT} seconds.");
                        cts.Cancel();
                    }
                }
            }
            return allTestsPassed ? 0 : 1;
        }

        private static long MinBytesPerNormalWorker => MemoryInfo.Gibibyte * 2;
        private static long MinBytesPerBigWorker => MemoryInfo.Gibibyte * 6;
        //static bool testRequeue = true;

        private static string GetPasswordFromConsole()
        {
            ConsoleKey key;
            string pass = string.Empty;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    Console.Write("\b \b");
                    pass = pass.Substring(0, pass.Length - 1);
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    pass += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);

            return pass;
        }

        private static void CheckDocker(CommandLineArgs commandLineArgs)
        {
            var dockerVersionOutput = RunTests.RunCommand("docker", "version -f \"{{json .}}\"", RunTests.IS_DOCKER_RUNNING_MESSAGE);
            var dockerVersionJson = JObject.Parse(dockerVersionOutput);
            if (dockerVersionJson["Server"]["Os"].Value<string>() != "windows")
            {
                Console.WriteLine("Switching Docker engine to Windows containers (this will stop any running containers)...");
                RunTests.RunCommand($"{Environment.GetEnvironmentVariable("ProgramFiles")}\\Docker\\Docker\\DockerCli.exe",
                    "-SwitchWindowsEngine",
                    "Are Hyper-V and Container features enabled?");
            }

            //RunCommand("netsh", $"advfirewall firewall add rule name=\"TestRunner\" dir=in action=allow protocol=tcp program=\"{Assembly.GetExecutingAssembly().Location}\"", string.Empty, true, true);

            var dockerImagesOutput = RunTests.RunCommand("docker", $"images {RunTests.DOCKER_IMAGE_NAME}", RunTests.IS_DOCKER_RUNNING_MESSAGE);
            bool hasImage = dockerImagesOutput.Contains(RunTests.DOCKER_IMAGE_NAME);
            if (hasImage && commandLineArgs.ArgAsBool("checkdocker"))
            {
                // check that it can run (for some reason the images get stale and stop working after a period of time)
                var pwizRoot = Path.GetDirectoryName(Path.GetDirectoryName(GetSkylineDirectory().FullName));
                string workerName = $"docker_check{GetTestRunTimeStamp()}";
                string testRunnerExe = GetTestRunnerExe();
                string dockerArgs = $"run --name {workerName} --rm -v \"{pwizRoot}\":c:\\pwiz {RunTests.DOCKER_IMAGE_NAME} \"{testRunnerExe} help\"";
                Console.WriteLine("Checking that Docker always_up_runner container can run.");
                string checkOutput = RunTests.RunCommand("docker", dockerArgs, "Error checking whether always_up_runner can start");
                if (checkOutput.Contains("StartService FAILED"))
                {
                    Console.WriteLine("Check failed. Deleting and rebuilding always_up_runner image.");
                    // rebuild image
                    RunTests.RunCommand("docker", $"rmi {RunTests.DOCKER_IMAGE_NAME}", "Error deleting always_up_runner");
                    hasImage = false;
                }
            }

            if (!hasImage)
            {
                Console.WriteLine($"'{RunTests.DOCKER_IMAGE_NAME}' is missing; building it now.");
                var buildPath = RunTests.ALWAYS_UP_RUNNER_REPO;
                if (!File.Exists(RunTests.ALWAYS_UP_SERVICE_EXE))
                {
                    using var tmpDir = new TemporaryDirectory();
                    using var webClient = new WebClient();
                    string tmpFile = Path.Combine(tmpDir.DirPath, "master.zip");
                    webClient.DownloadFile("https://github.com/ProteoWizard/AlwaysUpRunner/archive/refs/heads/master.zip", tmpFile);

                    using var alwaysUpRunnerCode = new ZipFile(tmpFile);
                    alwaysUpRunnerCode.ExtractAll(Path.GetDirectoryName(buildPath)!, ExtractExistingFileAction.OverwriteSilently);

                    Console.Write("Enter password to extract AlwaysUpCLT_licensed_binaries: ");
                    var pass = commandLineArgs.ArgAsStringOrDefault("alwaysupcltpassword") ?? GetPasswordFromConsole();
                    RunTests.RunCommand(Path.Combine(buildPath, "AlwaysUpCLT_licensed_binaries.exe"), $"-y \"-p{pass}\" \"-o{buildPath}\"", "Wrong password?");
                }

                string productName = (string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null);
                var dockerBaseImage = Environment.OSVersion.Version.Build >= 22000 || productName.Contains("Server")
                    ? string.Empty
                    : "--build-arg BASE_WINDOWS_IMAGE=mcr.microsoft.com/windows:1809-amd64";
                RunTests.RunCommand("docker", $"build \"{buildPath}\" -t \"{RunTests.DOCKER_IMAGE_NAME}\" {dockerBaseImage}", RunTests.IS_DOCKER_RUNNING_MESSAGE, true);
            }
        }

        private static int LaunchHostWorker(CommandLineArgs commandLineArgs, int workerPort, StreamWriter log, ConcurrentBag<string> coverageSnapshots)
        {
            var pwizRoot = Path.GetDirectoryName(Path.GetDirectoryName(GetSkylineDirectory().FullName));
            Assume.IsNotNull(pwizRoot);
            // Adding timestamp to worker name voids conflicts between this and any previous invocation
            string workerName = $"hostWorker{GetTestRunTimeStamp()}";
            string testRunnerLog = string.Empty;
            if (commandLineArgs.ArgAsBool("keepworkerlogs"))
                testRunnerLog = @$"log={pwizRoot}\TestRunner-{workerName}.log";

            // here paths are in host space
            var testRunnerExe = Assembly.GetExecutingAssembly().Location;
            var testRunnerArgs = $"parallelmode=client showheader=0 {testRunnerLog}";
            if (commandLineArgs.ArgAsBool("coverage"))
            {
                string dotCoverExe = GetFullDotCoverExePath(commandLineArgs);
                testRunnerArgs =
                    $"cover {dotCoverFilters} /Output={pwizRoot}\\coverage-{workerName}.dcvr /ReturnTargetExitCode /AnalyzeTargetArguments=false /TargetExecutable={testRunnerExe} -- " +
                    testRunnerArgs;
                testRunnerExe = Path.Combine(pwizRoot!, dotCoverExe);
                coverageSnapshots.Add($"coverage-{workerName}.dcvr");
            }
            testRunnerArgs = AddPassThroughArguments(commandLineArgs, testRunnerArgs);
            testRunnerArgs += $" workerport={workerPort} workername={workerName} queuehost=localhost";
            Console.WriteLine($"Launching {workerName}: {testRunnerExe} {testRunnerArgs}");
            log?.WriteLine($"Launching {workerName}: {testRunnerExe} {testRunnerArgs}");
            var psi = new ProcessStartInfo(testRunnerExe, testRunnerArgs);
            psi.WindowStyle = ProcessWindowStyle.Minimized;
            psi.CreateNoWindow = false;
            psi.UseShellExecute = true;
            var proc = Process.Start(psi);
            if (proc?.WaitForExit(1000) ?? true)
            {
                throw new IOException($"Error launching host worker: {proc?.ExitCode ?? -1}");
            }
            return proc.Id;
        }

        private static string LaunchDockerWorker(int i, CommandLineArgs commandLineArgs, ref string workerNames, bool bigWorker,
            long workerBytes, int workerPort, StreamWriter log, ConcurrentBag<string> coverageSnapshots)
        {
            var pwizRoot = Path.GetDirectoryName(Path.GetDirectoryName(GetSkylineDirectory().FullName));
            // Adding timestamp to worker name voids conflicts between this and any previous invocation
            string workerName = bigWorker ? $"docker_big_worker{GetTestRunTimeStamp()}_{i}" : $"docker_worker{GetTestRunTimeStamp()}_{i}";
            string dockerRunRedirect = string.Empty;
            string testRunnerLog = @$"c:\AlwaysUpCLT\TestRunner-{workerName}.log";
            if (commandLineArgs.ArgAsBool("keepworkerlogs"))
                testRunnerLog = @$"c:\pwiz\TestRunner-{workerName}.log";

            // paths in testRunnerCmd are in container-space (c:\pwiz is mounted from pwizRoot, c:\downloads is mounted from GetDownloadsPath(), c:\AlwaysUpCLT is not copied to the host)
            var testRunnerExe = GetTestRunnerExe();
            var testRunnerCmd = $@" parallelmode=client showheader=0 results=c:\AlwaysUpCLT\TestResults_{i} log={testRunnerLog} workerport={workerPort} workername={workerName}";
            
            if (commandLineArgs.ArgAsBool("coverage"))
            {
                var dotCoverExe = commandLineArgs.ArgAsString("dotcoverexe"); // use relative path
                testRunnerCmd =
                    $@"c:\pwiz\{dotCoverExe} cover {dotCoverFilters} /Output=c:\pwiz\coverage-{workerName}.dcvr /ReturnTargetExitCode /AnalyzeTargetArguments=false /TargetExecutable={testRunnerExe} -- " +
                    testRunnerCmd;
                coverageSnapshots.Add($"coverage-{workerName}.dcvr");
            }
            else
                testRunnerCmd = testRunnerExe + " " + testRunnerCmd;
            testRunnerCmd = AddPassThroughArguments(commandLineArgs, testRunnerCmd);

            string dockerArgs = $"run --name {workerName} --rm -m {workerBytes}b -v \"{PathEx.GetDownloadsPath()}\":c:\\downloads -v \"{pwizRoot}\":c:\\pwiz {RunTests.DOCKER_IMAGE_NAME} \"{testRunnerCmd}\" {dockerRunRedirect}";
            Console.WriteLine($"Launching {workerName}: docker {dockerArgs}");
            log?.WriteLine($"Launching {workerName}: docker {dockerArgs}");
            workerNames = (workerNames ?? "") + $"{workerName} ";
            var psi = new ProcessStartInfo("docker", dockerArgs);
            psi.WindowStyle = ProcessWindowStyle.Minimized;
            psi.CreateNoWindow = false;
            psi.UseShellExecute = true;
            var proc = Process.Start(psi);
            if (proc?.WaitForExit(1000) ?? true)
            {
                Console.WriteLine($"Error launching docker worker: {proc?.ExitCode ?? -1}");
                log?.WriteLine($"Error launching docker worker: {proc?.ExitCode ?? -1}");
            }
            return workerName;
        }

        private static DateTime _testRunStartTime;
        // Avoids conflicts between this and any previous invocation that may not have torn down its workers yet
        // Helps when a run is cancelled then quickly restarted, as often happens when you realize you've forgotten
        // to select certain tests etc
        private static string GetTestRunTimeStamp()
        {
            return $"_{_testRunStartTime.ToString("yyyyMMddHHmmss")}";
        }

        private static string AddPassThroughArguments(CommandLineArgs commandLineArgs, string testRunnerCmd)
        {
            foreach (string p in new[] { "perftests", "teamcitytestdecoration", "buildcheck", "runsmallmoleculeversions", "recordauditlogs" })
                testRunnerCmd += $" {p}={commandLineArgs.ArgAsString(p)}";
            return testRunnerCmd;
        }

        private static string GetTestRunnerExe()
        {
            // paths in testRunnerCmd are in container-space (c:\pwiz is mounted from pwizRoot, c:\downloads is mounted from GetDownloadsPath(), c:\AlwaysUpCLT is not copied to the host)
            var testRunnerExe = Assembly.GetExecutingAssembly().Location;
            int iRelative = testRunnerExe.IndexOf(@"pwiz_tools\Skyline\bin", StringComparison.CurrentCultureIgnoreCase);
            testRunnerExe = iRelative != -1
                ? Path.Combine(@"c:\pwiz", testRunnerExe.Substring(iRelative))
                : @"c:\pwiz\pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe";
            // N.B. TestResults_<n> could technically just be TestResults since each VM has its own drive, but it makes for a more readable log and
            // is also used in pwiz_tools\Skyline\TestRunnerLib\RunTests.cs to determine the test client ID
            return testRunnerExe;
        }

        private static void LaunchAndWaitForDockerWorker(int i, CommandLineArgs commandLineArgs, ref string workerNames, bool bigWorker,
            long workerBytes, int workerPort, ConcurrentDictionary<string, ParallelWorkerInfo> workerInfo, StreamWriter log, ConcurrentBag<string> coverageSnapshots)
        {
            string currentWorkerNames = workerNames;
            string workerName = LaunchDockerWorker(i, commandLineArgs, ref currentWorkerNames, bigWorker, workerBytes, workerPort, log, coverageSnapshots);
            for (int attempt = 0; attempt< 10; ++attempt)
            {
                Thread.Sleep(3000);
                if (workerInfo.ContainsKey(workerName))
                {
                    workerNames = currentWorkerNames;
                    return;
                }
            }
            throw new Exception($"Worker {workerName} did not connect.");
        }

        private class QueuedTestInfo
        {
            public QueuedTestInfo(TestInfo testInfo, string language, int loopCount, int pass)
            {
                TestInfo = testInfo;
                Language = language;
                LoopCount = loopCount;
                Pass = pass;
            }
            public TestInfo TestInfo { get; }
            public string Language { get; }
            public int LoopCount { get; private set; }
            public int Pass { get; }

            public void IncrementLoopCount()
            {
                ++LoopCount;
            }
        }

        private class ParallelWorkerInfo
        {
            public bool IsAlive { get; set; } = true;
            public string CurrentTest { get; set; }
        }

        private static int HostWorkerPid { get; set; }

        private static bool PushToTestQueue(List<TestInfo> testList, List<TestInfo> unfilteredTestList, CommandLineArgs commandLineArgs, StreamWriter log)
        {
            var cts = new CancellationTokenSource();
            var factory = new TaskFactory(cts.Token);
            var testQueue = new ConcurrentQueue<QueuedTestInfo>();
            var nonParallelTestQueue = new ConcurrentQueue<QueuedTestInfo>();
            var workerInfoByName = new ConcurrentDictionary<string, ParallelWorkerInfo>();
            var coverageSnapshots = new ConcurrentBag<string>();
            var tasks = new List<Task>();
            var timer = new Stopwatch();
            int testsFailed = 0;
            int testsResultsReturned = 0;
            int workerCount = (int) commandLineArgs.ArgAsLong("workercount");
            int dockerWorkerCount = workerCount - 1;
            var dockerTimeoutSecondsOverride = Environment.GetEnvironmentVariable("SKYLINE_TESTRUNNER_DOCKER_TIMEOUT_SEC");
            int workerTimeout = Convert.ToInt32(commandLineArgs.ArgAsStringOrDefault("workertimeout", dockerTimeoutSecondsOverride ?? "60"));
            int loop = (int) commandLineArgs.ArgAsLong("loop");
            bool[] passEnabled = {
                commandLineArgs.ArgAsBool("pass0"),
                commandLineArgs.ArgAsBool("pass1"),
                commandLineArgs.ArgAsBool("pass2")
            };
            var languages = GetLanguages(commandLineArgs);
            if (commandLineArgs.ArgAsBool("buildcheck"))
            {
                loop = 1;
                languages = new[] { "en-US" };
            }

            Action<string, StreamWriter, int> LogTestOutput = (testOutput, testLog, pass) =>
            {
                testOutput = testOutput.Trim(' ', '\t', '\r', '\n');
                testOutput = Regex.Replace(testOutput, @"\d+ failures", $"{testsFailed} failures");
                testOutput = Regex.Replace(testOutput, @"^(\[\d+:\d+\])?\s*(\d+)\.(\d+)?", $" $1 {pass}.{testsResultsReturned} ", RegexOptions.Multiline);

                Console.WriteLine(testOutput);
                testLog.WriteLine(testOutput);
            };

            for (int pass=0; pass < passEnabled.Length; ++pass)
            {
                if (!passEnabled[pass])
                    continue;

                // add tests to the queue (at least once, multiple times if loop > 1 and pass2)
                int loopCount = pass == 2 ? Math.Max(1, loop) : 1;
                var passLanguages = pass > 0 ? languages : new[] { "fr" };
                for (int i = 0; i < loopCount; ++i)
                {
                    foreach (var testInfo in testList)
                    {
                        var queue = testInfo.DoNotRunInParallel ? nonParallelTestQueue : testQueue;
                        foreach (var language in passLanguages)
                            queue.Enqueue(new QueuedTestInfo(testInfo, language, i, pass));
                    }
                }
            }

            if (testQueue.Count < dockerWorkerCount)
            {
                Console.WriteLine($"There are fewer parallelizable test/language pairs ({testQueue.Count}) than the number of specified parallel workers; reducing workercount to {testQueue.Count + 1}.");
                workerCount = testQueue.Count + 1;
                dockerWorkerCount = testQueue.Count;
            }

            // check docker daemon is working and build always_up_runner if necessary
            if (dockerWorkerCount > 0)
                CheckDocker(commandLineArgs);

            // open socket that listens for workers to connect
            using (var receiver = new PullSocket())
            {
                // get system-assigned port which will passed to workers with "workerport" parameter
                int workerPort;
                if (commandLineArgs.HasArg("workerport"))
                {
                    workerPort = (int)commandLineArgs.ArgAsLong("workerport");
                }
                else
                {
                    // Select the first unused port above 9810 to communicate with the worker.
                    // The Windows server "macs2.gs.washington.edu" is configured to be able to use any port between 9810 and 9820
                    workerPort = UnusedPortFinder.FindUnusedPort(9810, 65535);
                }
                receiver.Bind($"tcp://*:{workerPort}");
                string workerNames = null;

                // try to kill docker workers if process is terminated externally (e.g. SkylineTester)
                Kernel32Test.SetConsoleCtrlHandler(c =>
                {
                    RunTests.KillParallelWorkers(HostWorkerPid, workerNames);
                    cts.Cancel();
                    Process.GetCurrentProcess().Kill();
                    return true;
                }, true);

                if (dockerWorkerCount > 0)
                {
                    long availableBytesForNormalWorkers = MemoryInfo.AvailableBytes - MinBytesPerBigWorker;

                    int normalWorkerCount = workerCount - 1;
                    long normalWorkerBytes = MinBytesPerNormalWorker * normalWorkerCount;
                    long totalWorkerBytes = normalWorkerBytes + MinBytesPerBigWorker;
                    if (availableBytesForNormalWorkers < normalWorkerBytes)
                        throw new ArgumentException($"not enough free memory ({MemoryInfo.AvailableBytes / MemoryInfo.Mebibyte} MB) for {workerCount} workers: need at least {totalWorkerBytes / MemoryInfo.Mebibyte} MB");

                    long perWorkerBytes = availableBytesForNormalWorkers / normalWorkerCount;

                    void LaunchDockerWorkers()
                    {
                        bool waitForWorkerConnect = commandLineArgs.ArgAsBool("waitforworkers");
                        if (waitForWorkerConnect)
                        {
                            for (int i = 0; i < normalWorkerCount; ++i)
                            {
                                int i2 = i;
                                TryHelper.Try<Exception>(() => LaunchAndWaitForDockerWorker(i2, commandLineArgs, ref workerNames, false, perWorkerBytes, workerPort, workerInfoByName, log, coverageSnapshots), 4, 3000);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < normalWorkerCount; ++i)
                            {
                                LaunchDockerWorker(i, commandLineArgs, ref workerNames, false, perWorkerBytes, workerPort, log, coverageSnapshots);
                                Thread.Sleep(1000);
                            }
                        }
                        //LaunchDockerWorker(normalWorkerCount, commandLineArgs, ref workerNames, true);
                    }

                    factory.StartNew(() => {
                        try
                        {
                            LaunchDockerWorkers();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("Error launching Docker workers: " + e);
                            Environment.Exit(1);
                        }
                    });
                }

                // fix this to get PID of TestRunner, not dotCover
                HostWorkerPid = LaunchHostWorker(commandLineArgs, workerPort, log, coverageSnapshots);
                bool workersFinished = false;

                // wait for workers to finish
                void WaitForWorkersToFinish()
                {
                    // server test thread will not return until all workers have finished in order to handle requeued tests
                    while (!cts.IsCancellationRequested || workerInfoByName.IsEmpty || workerInfoByName.Any(kvp => kvp.Value.IsAlive))
                    {
                        if (workerInfoByName.IsEmpty || workerInfoByName.Any(kvp => kvp.Value.IsAlive))
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        workersFinished = true;
                        cts.Cancel();
                    }
                }

                tasks.Add(factory.StartNew(() => {
                    try
                    {
                        WaitForWorkersToFinish();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Error running worker wait thread: " + e);
                        Environment.Exit(1);
                    }
                }, TaskCreationOptions.LongRunning));

                Console.WriteLine("Running {0}{1} tests{2}{3} in parallel with {4} workers...",
                    testList.Count,
                    testList.Count < unfilteredTestList.Count ? "/" + unfilteredTestList.Count : "",
                    (loop <= 0) ? " forever" : (loop == 1) ? "" : " in " + loop + " loops",
                    "", /*(repeat <= 1) ? "" : ", repeated " + repeat + " times each per language",*/
                    workerCount);

                var waitingForWorkers = new Stopwatch();
                waitingForWorkers.Start();

                // main thread listens for workers to connect
                while (!cts.IsCancellationRequested)
                {
                    // listen for workerName/IP/tasksPort/resultsPort/heartbeatPort string from a worker
                    if (!receiver.TryReceiveFrameString(TimeSpan.FromSeconds(1), out var workerId))
                    {
                        if (waitingForWorkers.Elapsed.TotalSeconds > workerTimeout)
                        {
                            Console.Error.WriteLine($"No workers connected to the server in {workerTimeout} seconds.");
                            Console.Error.WriteLine("Be sure to check BOTH public and private options if prompted to \"Allow TestRunner to communicate on these networks\".");
                            Console.Error.WriteLine("See https://skyline.ms/wiki/home/development/page.view?name=Troubleshooting_parallel_mode for troubleshooting tips.\r\n");

                            Environment.Exit(1);
                        }
                        continue;
                    }

                    waitingForWorkers.Stop();

                    string[] workerIdParts = workerId.Split('/');
                    string workerName = workerIdParts[0];
                    string workerIP = workerIdParts[1];
                    string tasksPort = workerIdParts[2];
                    string resultsPort = workerIdParts[3];
                    string heartbeatPort = workerIdParts[4];
                    bool isBigWorker = workerName.Contains("hostWorker");
                    var workerInfo = workerInfoByName[workerName] = new ParallelWorkerInfo();

                    Console.WriteLine($"Connection from worker {workerId}");
                    void HandleWorkerConnection()
                    {
                        using (var workerSender = new PushSocket($">tcp://{workerIP}:{tasksPort}"))
                        using (var workerReceiver = new PullSocket($">tcp://{workerIP}:{resultsPort}"))
                        {

                            while (!cts.IsCancellationRequested)
                            {
                                // listen for "ready" signal from worker
                                if (!workerReceiver.TryReceiveSignal(TimeSpan.FromSeconds(3), out bool signal) && workerInfo.IsAlive)
                                    continue;

                                if (!workerInfo.IsAlive)
                                    return;

                                QueuedTestInfo testInfo = null;
                                if (isBigWorker)
                                    nonParallelTestQueue.TryDequeue(out testInfo);
                                if (testInfo == null)
                                    testQueue.TryDequeue(out testInfo);

                                if (testInfo == null)
                                {
                                    // not done until all workers are done (in order to wait for possibled requeued tests)
                                    //done = true;
                                    workerSender.TrySendFrame("TestRunnerQuit");
                                    workerInfo.IsAlive = false;
                                    return;
                                }

                                string testName = testInfo.TestInfo.TestMethod.Name;
                                bool gotResult = false;
                                try
                                {
                                    //Console.WriteLine(testInfo.TestMethod.Name);
                                    workerInfo.CurrentTest = testInfo.TestInfo.TestMethod.Name + "/" + testInfo.Language + "/" + testInfo.Pass;
                                    if (!workerSender.TrySendFrame(TimeSpan.FromSeconds(5), workerInfo.CurrentTest))
                                        continue;
                                    lock (timer) timer.Start();
                                    byte[] result = null;
                                    while (!workerReceiver.TryReceiveFrameBytes(TimeSpan.FromSeconds(5), out result) && workerInfo.IsAlive) { }
                                    if (result == null)
                                        continue;
                                    gotResult = true;
                                    bool testPassed = Convert.ToBoolean(result[0]);
                                    if (!testPassed)
                                        Interlocked.Increment(ref testsFailed);
                                    string testOutput = Encoding.UTF8.GetString(result, 1, result.Length - 1);
                                    LogTestOutput(testOutput, log, testInfo.Pass + testInfo.LoopCount);
                                    Interlocked.Increment(ref testsResultsReturned);
                                    if (testInfo.Pass == 2)
                                        testInfo.IncrementLoopCount();
                                    if (loop == 0)
                                        (testInfo.TestInfo.DoNotRunInParallel ? nonParallelTestQueue : testQueue).Enqueue(testInfo);
                                }
                                finally
                                {
                                    if (/*testRequeue && testInfo.TestMethod.Name == "TestSwathIsolationLists" ||*/ !gotResult && !cts.IsCancellationRequested)
                                    {
                                        //if (testInfo.TestMethod.Name == "TestSwathIsolationLists")
                                        //    testRequeue = false;
                                        Console.Error.WriteLine($"No result for test {workerInfo.CurrentTest}; requeuing...");
                                        (testInfo.TestInfo.DoNotRunInParallel ? nonParallelTestQueue : testQueue).Enqueue(testInfo);
                                    }
                                }
                            }
                        }
                    }

                    tasks.Add(factory.StartNew(() => {
                        try
                        {
                            HandleWorkerConnection();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("Error in worker handling thread: " + e);
                            Environment.Exit(1);
                        }
                    }, TaskCreationOptions.LongRunning));

                    // start heartbeat for worker
                    void ListenForWorkerHeartbeat()
                    {
                        using (var workerHeartbeat = new PullSocket($">tcp://{workerIP}:{heartbeatPort}"))
                        {
                            var msg = new Msg();
                            msg.InitEmpty();
                            while (!cts.IsCancellationRequested)
                            {
                                if (!workerHeartbeat.TryReceive(ref msg, TimeSpan.FromSeconds(5)))
                                {
                                    workerInfo.IsAlive = false;

                                    if (testQueue.IsEmpty || cts.IsCancellationRequested)
                                        return;
                                    Console.WriteLine($"Worker {workerName} stopped responding while working on test {workerInfo.CurrentTest}.");
                                    if (commandLineArgs.ArgAsBool("coverage"))
                                    {
                                        Console.WriteLine("Aborting coverage run due to failed worker (coverage from that worker is lost).");
                                        RunTests.KillParallelWorkers(HostWorkerPid);
                                        Process.GetCurrentProcess().Kill();
                                    }

                                    if (workerCount > 0 && !cts.IsCancellationRequested && !workerInfoByName.Any(kvp => kvp.Value.IsAlive))
                                    {
                                        Console.WriteLine("No more workers alive: starting another worker.");
                                        LaunchDockerWorker(workerInfoByName.Count + 1, commandLineArgs, ref workerNames, true, MinBytesPerBigWorker, workerPort, log, coverageSnapshots);
                                    }
                                    return;
                                }
                                //Console.WriteLine($"Heartbeat from {workerIP}.");
                                Thread.Sleep(3000);
                            }
                            workerInfo.IsAlive = false;
                        }
                    }

                    factory.StartNew(() => {
                        try
                        {
                            ListenForWorkerHeartbeat();
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("Error listening for worker heartbeat: " + e);
                            Environment.Exit(1);
                        }
                    }, TaskCreationOptions.LongRunning);
                }

                if (!workersFinished && cts.IsCancellationRequested)
                    return false;
                Console.WriteLine("Waiting for worker tasks to finish.");
                foreach (var task in tasks)
                    task.Wait();
            }
            Console.WriteLine($"Parallel testing finished in {timer.Elapsed} ({timer.Elapsed.TotalSeconds}s)");
            if (coverageSnapshots.Any())
                GenerateCoverageReport(commandLineArgs, coverageSnapshots);
            return testsFailed == 0;
        }

        private static string GetFullDotCoverExePath(CommandLineArgs commandLineArgs)
        {
            var pwizRoot = Path.GetDirectoryName(Path.GetDirectoryName(GetSkylineDirectory().FullName));
            if (pwizRoot == null)
                throw new InvalidOperationException("Unable to determine path to ProteoWizard root directory.");
            return Path.Combine(pwizRoot, commandLineArgs.ArgAsString("dotcoverexe"));
        }

        private static void GenerateCoverageReport(CommandLineArgs commandLineArgs, ConcurrentBag<string> coverageSnapshots)
        {
            var pwizRoot = Path.GetDirectoryName(Path.GetDirectoryName(GetSkylineDirectory().FullName))!;
            string dotCoverExe = GetFullDotCoverExePath(commandLineArgs);
            string mergedCoverageFilepath = Path.Combine(pwizRoot, $"coverage{GetTestRunTimeStamp()}.dcvr");
            var snapshotsWithFullPath = coverageSnapshots.Select(s => Path.Combine(pwizRoot, s)).ToList();
            string mergeLogPath = Path.Combine(pwizRoot, "merge.log");
            string mergeErrLogPath = Path.Combine(pwizRoot, "merge.err.log");
            // wait for the snapshots to be saved
            foreach (var snapshot in snapshotsWithFullPath)
            {
                for (int retry = 0; retry < 10 && !File.Exists(snapshot); ++retry)
                    Thread.Sleep(500);
                if (!File.Exists(snapshot))
                    throw new FileNotFoundException($"coverage snapshot {snapshot} did not get saved to disk");
            }
            var psi = new ProcessStartInfo(dotCoverExe, $"merge /LogFile={mergeLogPath} /Output={mergedCoverageFilepath} /Source=" + string.Join(";", snapshotsWithFullPath));
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            var mergeProcess = Process.Start(psi);
            if (mergeProcess == null || mergeProcess.WaitForExit(10000) && mergeProcess.ExitCode != 0)
            {
                Console.WriteLine($"Error merging coverage snapshots: {mergeProcess?.ExitCode ?? -1}");
                Console.WriteLine("Command: " + psi.FileName + " " + psi.Arguments);
                return;
            }
            File.Delete(mergeLogPath);
            File.Delete(mergeErrLogPath);

            string coverageReportFilename = Path.ChangeExtension(mergedCoverageFilepath, ".html");
            psi.Arguments = $"report /ReportType=html /Source={mergedCoverageFilepath} /Output={coverageReportFilename}";
            var reportProcess = Process.Start(psi);
            if (reportProcess == null || reportProcess.WaitForExit(10000) && reportProcess.ExitCode != 0)
            {
                Console.WriteLine($"Error generating coverage report: {reportProcess?.ExitCode ?? -1}");
                Console.WriteLine("Command: " + psi.FileName + " " + psi.Arguments);
                return;
            }

            foreach(var snapshot in snapshotsWithFullPath)
                FileEx.SafeDelete(snapshot);
            Process.Start(coverageReportFilename);
        }

        private static string[] GetLanguages(CommandLineArgs args)
        {
            string value = args.ArgAsString("language");
            if (value == "all")
                return allLanguages;
            return value.Split(',').Select(GetCanonicalLanguage).ToArray();
        }

        private static string GetCanonicalLanguage(string rawLanguage)
        {
            // If the raw language is a prefix of something from allLanguages, use
            // the full name.
            foreach (var language in allLanguages)
            {
                if (language.StartsWith(rawLanguage))
                    return language;
            }
            return rawLanguage;
        }

        private static DirectoryInfo GetSkylineDirectory()
        {
            string skylinePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var skylineDirectory = skylinePath != null ? new DirectoryInfo(skylinePath) : null;
            while (skylineDirectory != null)
            {
                skylineDirectory = skylineDirectory.Parent;
                if (skylineDirectory == null)
                    break;

                if (skylineDirectory.Name.ToLowerInvariant() == "skyline")
                    return skylineDirectory;
                else
                    foreach (var subdir in skylineDirectory.GetDirectories())
                        if (Directory.Exists(Path.Combine(subdir.FullName, "pwiz_tools", "Skyline")))
                            return new DirectoryInfo(Path.Combine(subdir.FullName, "pwiz_tools", "Skyline"));
            }

            return null;
        }

        private static void TeamCitySettings(CommandLineArgs commandLineArgs, out bool teamcityTestDecoration, out string testSpecification)
        {
            teamcityTestDecoration = commandLineArgs.ArgAsBool("teamcitytestdecoration");
            testSpecification = commandLineArgs.ArgAsStringOrDefault("teamcitytestsuite") ??
                                commandLineArgs.ArgAsStringOrDefault("test") ??
                                "all";
        }

        private static void TeamCityStartTestSuite(CommandLineArgs commandLineArgs)
        {
            TeamCitySettings(commandLineArgs, out bool teamcityTestDecoration, out string testSpecification);
            if (teamcityTestDecoration)
                Console.WriteLine($"##teamcity[testSuiteStarted name='{testSpecification}']");
        }

        private static void TeamCityFinishTestSuite(CommandLineArgs commandLineArgs)
        {
            TeamCitySettings(commandLineArgs, out bool teamcityTestDecoration, out string testSpecification);
            if (teamcityTestDecoration)
                Console.WriteLine($"##teamcity[testSuiteFinished name='{testSpecification}']");
        }

        // Run all test passes.
        private static bool RunTestPasses(
            List<TestInfo> testList, 
            List<TestInfo> unfilteredTestList, 
            CommandLineArgs commandLineArgs, 
            StreamWriter log, 
            long loopCount, 
            long repeat,
            bool profiling = false)
        {
            bool buildMode = commandLineArgs.ArgAsBool("buildcheck");
            bool randomOrder = commandLineArgs.ArgAsBool("random");
            bool demoMode = commandLineArgs.ArgAsBool("demo");
            bool offscreen = commandLineArgs.ArgAsBool("offscreen");
            bool internet = commandLineArgs.ArgAsBool("internet");
            bool useOriginalURLs = commandLineArgs.ArgAsBool("originalurls");
            bool perftests = commandLineArgs.ArgAsBool("perftests");
            bool retrydatadownloads = commandLineArgs.ArgAsBool("retrydatadownloads"); // When true, re-download data files on test failure in case its due to data staleness
            bool runsmallmoleculeversions = commandLineArgs.ArgAsBool("runsmallmoleculeversions"); // Run the various tests that are versions of other tests with the document completely converted to small molecules?
            bool recordauditlogs = commandLineArgs.ArgAsBool("recordauditlogs"); // Replace or create audit logs for tutorial tests
            bool useVendorReaders = commandLineArgs.ArgAsBool("vendors");
            bool showStatus = commandLineArgs.ArgAsBool("status");
            bool showFormNames = commandLineArgs.ArgAsBool("showformnames");
            bool qualityMode = commandLineArgs.ArgAsBool("quality");
            bool pass0 = commandLineArgs.ArgAsBool("pass0");
            bool pass1 = commandLineArgs.ArgAsBool("pass1");
            bool pass2 = commandLineArgs.ArgAsBool("pass2");
            int timeoutMultiplier = (int) commandLineArgs.ArgAsLong("multi");
            int pauseSeconds = (int) commandLineArgs.ArgAsLong("pause");
            int pauseStartingScreenshot = (int)commandLineArgs.ArgAsLong("startingshot");
            var formList = commandLineArgs.ArgAsString("form");
            if (!formList.IsNullOrEmpty())
                perftests = true;
            var pauseDialogs = (string.IsNullOrEmpty(formList)) ? null : formList.Split(',');
            var results = commandLineArgs.ArgAsString("results");
            var maxSecondsPerTest = commandLineArgs.ArgAsDouble("maxsecondspertest");
            var dmpDir = commandLineArgs.ArgAsString("dmpdir");
            bool teamcityTestDecoration = commandLineArgs.ArgAsBool("teamcitytestdecoration");
            bool verbose = commandLineArgs.ArgAsBool("verbose");
            string parallelMode = commandLineArgs.ArgAsString("parallelmode");
            bool serverMode = parallelMode == "server";
            bool clientMode = parallelMode == "client";
            bool asNightly = offscreen && qualityMode;  // While it is possible to run quality off screen from the Quality tab, this is what we use to distinguish for treatment of perf tests
            bool coverage = commandLineArgs.ArgAsBool("coverage");

            // If pausing for screenshots, make sure this process is allowed to fully activate its forms
            if (pauseSeconds != 0)
            {
                Process.GetCurrentProcess().AllowSetForegroundWindow();
            }

            // If running Nightly tests, remove any flagged for exclusion by the NoNightlyTesting custom attribute
            if (asNightly)
            {
                testList.RemoveAll(test => test.DoNotRunInNightly);
                unfilteredTestList.RemoveAll(test => test.DoNotRunInNightly);
            }

            // If we haven't been told to run perf tests, remove any from the list
            // which may have shown up by default
            if (!perftests)
            {
                testList.RemoveAll(test => test.IsPerfTest);
                unfilteredTestList.RemoveAll(test => test.IsPerfTest);
            }
            else
            {
                // Take advantage of the extra time available in perftest runs to do the leak tests we
                // skip in regular nightlies - but skip leak tests covered in regular nightlies
                foreach (var test in unfilteredTestList)
                {
                    test.DoNotLeakTest = !test.DoNotLeakTest;
                }
            }

            // If this is a nightly run, check the SKYLINE_NIGHTLY_TEST_EXCLUSIONS env var 
            HandleNightlyTestExclusions(testList, unfilteredTestList, log, asNightly);

            // Even if we have been told to run perftests, if none are in the list
            // then make sure we don't chat about perf tests in the log
            perftests &= testList.Any(t => t.IsPerfTest);

            if (buildMode)
            {
                randomOrder = false;
                demoMode = false;
                offscreen = true;
                useVendorReaders = true;
                showStatus = false;
                qualityMode = false;
                pauseSeconds = 0;
            }

            if (serverMode)
            {
                if (coverage)
                {
                    if (!File.Exists(GetFullDotCoverExePath(commandLineArgs)))
                        throw new ArgumentException($"The file specified by dotcoverexe ({commandLineArgs.ArgAsString("dotcoverexe")} does not exist: it must be the path to command-line dotCover.exe relative to the pwiz root");
                }

                return PushToTestQueue(testList, unfilteredTestList, commandLineArgs, log);
            }

            if (coverage)
                throw new ArgumentException("Coverage only works in parallel testing mode.");

            var runTests = new RunTests(
                demoMode, buildMode, offscreen, internet, useOriginalURLs, showStatus, perftests,
                runsmallmoleculeversions, recordauditlogs, teamcityTestDecoration,
                retrydatadownloads,
                pauseDialogs, pauseSeconds, pauseStartingScreenshot, useVendorReaders, timeoutMultiplier, 
                results, log, verbose, clientMode);

            var timer = new Stopwatch();
            timer.Start();
            using (new DebuggerListener(runTests))
            {
                if (asNightly && !string.IsNullOrEmpty(dmpDir) && Directory.Exists(dmpDir))
                {
                    runTests.Log("# Deleting memory dumps.\r\n");

                    var dmpDirInfo = new DirectoryInfo(dmpDir);
                    var memoryDumps = dmpDirInfo.GetFileSystemInfos("*.dmp")
                        .OrderBy(f => f.CreationTime)
                        .ToArray();

                    runTests.Log("# Found {0} memory dumps in {1}.\r\n", memoryDumps.Length, dmpDir);

                    // Only keep 5 pairs. If memory dumps are deleted manually it could
                    // happen that we delete a pre-dump but not a post-dump
                    if (memoryDumps.Length > 10)
                    {
                        foreach (var dmp in memoryDumps.Take(memoryDumps.Length - 10))
                        {
                            // Just to double check that we don't delete other files
                            if (dmp.Extension == ".dmp" &&
                                (dmp.Name.StartsWith("pre_") || dmp.Name.StartsWith("post_")))
                            {
                                runTests.Log("# Deleting {0}.\r\n", dmp.FullName);
                                File.Delete(dmp.FullName);

                                if (File.Exists(dmp.FullName))
                                    runTests.Log("# WARNING: {0} not deleted.\r\n", dmp.FullName);
                            }
                            else
                            {
                                runTests.Log("# Skipping deletion of {0}.\r\n", dmp.FullName);
                            }
                        }
                    }

                    runTests.Log("\r\n");
                }

                if (commandLineArgs.ArgAsBool("clipboardcheck"))
                {
                    runTests.TestContext.Properties["ClipboardCheck"] = "TestRunner clipboard check";
                    Console.WriteLine("Checking clipboard use for {0} tests...\n", testList.Count);
                    loopCount = 1;
                    randomOrder = false;
                }
                else if (commandLineArgs.ArgAsBool("showheader"))
                {
                    if (!randomOrder && formList.IsNullOrEmpty() && perftests)
                        runTests.Log("Perf tests will run last, for maximum overall test coverage.\r\n");
                        runTests.Log("Running {0}{1} tests{2}{3}...\r\n",
                        testList.Count,
                        testList.Count < unfilteredTestList.Count ? "/" + unfilteredTestList.Count : "",
                        (loopCount <= 0) ? " forever" : (loopCount == 1) ? "" : " in " + loopCount + " loops",
                        (repeat <= 1) ? "" : ", repeated " + repeat + " times each per language");
                }

                // Get list of languages
                var languages = buildMode
                    ? new[] { "en-US" }
                    : GetLanguages(commandLineArgs);

                if (showFormNames)
                    runTests.Skyline.Set("ShowFormNames", true);

                var removeList = new List<TestInfo>();

                // Pass 0: Test an interesting collection of edge cases:
                //         French number format,
                //         No vendor readers,
                //         No internet access,
                if (pass0)
                {
                    if (!clientMode)
                    {
                        runTests.Log("\r\n");
                        runTests.Log("# Pass 0: Run with French number format, no vendor readers, no internet access.\r\n");
                        if (testList.Any(t => t.IsPerfTest))
                        {
                            // These are largely about vendor and/or internet performance, so not worth doing in pass 0
                            runTests.Log("# Skipping perf tests for pass 0.\r\n");
                        }
                    }

                    runTests.Language = new CultureInfo("fr");
                    runTests.Skyline.Set("NoVendorReaders", true);
                    runTests.AccessInternet = false;
                    runTests.RunPerfTests = false;
                    runTests.CheckCrtLeaks = CrtLeakThreshold;
                    for (int testNumber = 0; testNumber < testList.Count; testNumber++)
                    {
                        var test = testList[testNumber];
                        if (test.IsPerfTest)
                        {
                            // These are largely about vendor and/or internet performance, so not worth doing in pass 0
                            continue;
                        }
                        if (!runTests.Run(test, 0, testNumber, dmpDir, false) || // No point in re-running a failed test
                            RunOnceTestNames.Contains(test.TestMethod.Name)) // No point in running certain tests more than once
                        {
                            removeList.Add(test);
                        }
                    }
                    runTests.Skyline.Set("NoVendorReaders", false);
                    runTests.AccessInternet = internet;
                    runTests.RunPerfTests = perftests;
                    runTests.CheckCrtLeaks = 0;

                    foreach (var removeTest in removeList)
                        testList.Remove(removeTest);
                    removeList.Clear();
                }

                // Pass 1: Look for cumulative leaks when test is run multiple times.
                if (pass1)
                {
                    if (!clientMode)
                    {
                        runTests.Log("\r\n");
                        runTests.Log("# Pass 1: Run tests multiple times to detect memory leaks.\r\n");
                        if (testList.Any(t => t.DoNotLeakTest))
                        {
                            // These are  too lengthy to run multiple times for leak testing, so not a good fit for pass 1
                            // But it's a shame to skip them entirely, so we flip the attribute so they run on perf test machines
                            runTests.Log("# Tests with NoLeakTesting attribute are skipped in pass 1 but prioritized in pass 2.\r\n");
                            runTests.Log("# Note that systems running perf tests invert the NoLeakTesting attribute to ensure overall coverage.\r\n");
                        }
                        if (testList.Any(t => t.IsPerfTest))
                        {
                            // These are generally too lengthy to run multiple times, so not a good fit for pass 1
                            runTests.Log("# Skipping perf tests for pass 1 leak checks.\r\n");
                        }
                    }

                    var maxDeltas = new LeakTracking();
                    int maxIterationCount = 0;

                    int pass1LoopCount = 0;
                    if (!pass2 && loopCount <= 0)
                        pass1LoopCount = int.MaxValue;

                    for (int pass1Count = 0; pass1Count <= pass1LoopCount; ++pass1Count)
                        for (int testNumber = 0; testNumber < testList.Count; testNumber++)
                        {
                            var test = testList[testNumber];
                            bool failed = false;

                            if (test.IsPerfTest)
                            {
                                // These are generally too lengthy to run multiple times, so not a good fit for pass 1
                                continue;
                            }

                            if (test.DoNotLeakTest)
                            {
                                // These are specifically too lengthy to run multiple times, so not a good fit for pass 1
                                continue;
                            }

                            if (failed)
                                continue;

                            // Run test repeatedly until we can confidently assess the leak status.
                            var numLeakCheckIterations = GetLeakCheckIterations(test);
                            var runTestForever = false;
                            var hangIteration = -1;
                            var listValues = new List<LeakTracking>();
                            LeakTracking? minDeltas = null;
                            int? passedIndex = null;
                            int iterationCount = 0;
                            string leakMessage = null;
                            var leakHanger = new LeakHanger();  // In case of a leak, this object will hang until freed by a debugger
                            for (int i = 0; i < numLeakCheckIterations || runTestForever; i++, iterationCount++)
                            {
                                // Run the test in the next language.
                                runTests.Language = new CultureInfo(allLanguages[i % allLanguages.Length]);
                                if (!runTests.Run(test, 1, testNumber, dmpDir, hangIteration >= 0 && (i - hangIteration) % 100 == 0))
                                {
                                    failed = true;
                                    removeList.Add(test);
                                    break;
                                }

                                // Run linear regression on memory size samples.
                                listValues.Add(new LeakTracking(runTests));
                                if (listValues.Count <= LeakTrailingDeltas)
                                    continue;

                                if (!runTestForever)
                                {
                                    // Stop accumulating points if all leak minimal values are below the threshold values.
                                    var lastDeltas = LeakTracking.MeanDeltas(listValues);
                                    minDeltas = minDeltas.HasValue ? minDeltas.Value.Min(lastDeltas) : lastDeltas;
                                    if (minDeltas.Value.BelowThresholds(LeakThresholds, test.TestMethod.Name))
                                    {
                                        passedIndex = passedIndex ?? i;

                                        if (!IsFixedLeakIterations && !leakHanger.IsTestMode)
                                            break;
                                    }

                                    // Report leak message at LeakCheckIterations, not the expanded count from GetLeakCheckIterations(test)
                                    if (leakHanger.IsTestMode ||
                                        GetLeakCheckReportEarly(test) && iterationCount + 1 == Math.Min(numLeakCheckIterations, LeakCheckIterations))
                                    {
                                        leakMessage = minDeltas.Value.GetLeakMessage(LeakThresholds, test.TestMethod.Name);
                                        if (leakMessage != null)
                                        {
                                            runTests.Log(leakMessage);
                                            // runTests.Log("# Entering infinite loop.");
                                            // leakHanger.Wait();

                                            if (!teamcityTestDecoration)
                                                runTestForever = true; // Once we break out of the loop, just keep running this test
                                            hangIteration = i;
                                            RunTests.MemoryManagement.HeapDiagnostics = true;
                                        }
                                    }
                                }

                                // Remove the oldest point unless this is the last iteration
                                // So that the report below will be based on the set that just
                                // failed the leak check
                                if (!passedIndex.HasValue || i < LeakCheckIterations - 1)
                                {
                                    listValues.RemoveAt(0);
                                }
                            }

                            if (failed)
                                continue;

                            if (!GetLeakCheckReportEarly(test))
                            {
                                leakMessage = minDeltas.Value.GetLeakMessage(LeakThresholds, test.TestMethod.Name);
                                if (leakMessage != null)
                                    runTests.Log(leakMessage);
                            }

                            if (leakMessage != null)
                                removeList.Add(test);
                            runTests.Log(minDeltas.Value.GetLogMessage(test.TestMethod.Name, iterationCount + 1));

                            maxDeltas = maxDeltas.Max(minDeltas.Value);
                            maxIterationCount = Math.Max(maxIterationCount, iterationCount);
                        }

                    runTests.Log(maxDeltas.GetLogMessage("MaximumLeaks", maxIterationCount));
                    foreach (var removeTest in removeList)
                        testList.Remove(removeTest);
                    removeList.Clear();
                }

                if (qualityMode)
                    languages = qualityLanguages;

                // Run all test passes.
                int pass = 2; // at this point we always start at pass 2 (pass0 and pass1 will have run already, optionally)
                int passEnd = pass + (int)loopCount;
                if (loopCount <= 0)
                {
                    passEnd = int.MaxValue;
                }

                if (!pass2)
                    return runTests.FailureCount == 0;

                if (pass < passEnd && testList.Count > 0 && !clientMode)
                {
                    runTests.Log("\r\n");
                    runTests.Log("# Pass 2+: Run tests in each selected language.\r\n");
                }

                // Move any tests with the NoLeakTesting attribute to the front of the list for pass 2, as we skipped them in pass 1
                testList = testList.Where(t => t.DoNotLeakTest)
                    .Concat(testList.Where(t => !t.DoNotLeakTest))
                    .ToList();

                int perfPass = pass; // For nightly tests, we'll run perf tests just once per language, and only in one language (dynamically chosen for coverage) if english and french (along with any others) are both enabled
                bool needsPerfTestPass2Warning = asNightly && testList.Any(t => t.IsPerfTest); // No perf tests, no warning
                var perfTestsOneLanguageOnly = asNightly && perftests && languages.Any(l => l.StartsWith("en")) && languages.Any(l => l.StartsWith("fr"));

                for (; pass < passEnd; pass++)
                {
                    if (testList.Count == 0)
                        break;

                    // Run each test in this test pass.
                    var testPass = randomOrder ? testList.RandomOrder().ToList() : testList;
                    for (int testNumber = 0; testNumber < testPass.Count; testNumber++)
                    {
                        var test = testPass[testNumber];

                        // Perf Tests are generally too lengthy to run multiple times (but non-english format check is useful, so rotate through on a per-day basis - including "tr")
                        var perfTestLanguage = allLanguages[DateTime.Now.DayOfYear % allLanguages.Length];
                        var languagesThisTest = (test.IsPerfTest && perfTestsOneLanguageOnly) ? new[] { perfTestLanguage } : languages;
                        if (perfTestsOneLanguageOnly && needsPerfTestPass2Warning)
                        {
                            // NB the phrase "# Perf tests" in a log is a key for SkylineNightly to post to a different URL - so don't mess with this.
                            runTests.Log("# Perf tests will be run only once, and only in one language, dynamically chosen (by DayOfYear%NumberOfLanguages) for coverage.  To run perf tests in specific languages, enable all but English.\r\n");
                            needsPerfTestPass2Warning = false;
                        }

                        // Run once (or repeat times) for each language.
                        for (int i = 0; i < languagesThisTest.Length; i++)
                        {
                            runTests.Language = new CultureInfo(languagesThisTest[i]);
                            var stopWatch = new Stopwatch();
                            stopWatch.Start(); // Limit the repeats in case of very long tests
                            for (int repeatCounter = 1; repeatCounter <= repeat; repeatCounter++)
                            {
                                if (asNightly && test.IsPerfTest && ((pass > perfPass) || (repeatCounter > 1)))
                                {
                                    // Perf Tests are generally too lengthy to run multiple times (but per-language check is useful)
                                    if (needsPerfTestPass2Warning)
                                    {
                                        // NB the phrase "# Perf tests" in a log is a key for SkylineNightly to post to a different URL - so don't mess with this.
                                        runTests.Log("# Perf tests will be run only once per language.\r\n");
                                        needsPerfTestPass2Warning = false;
                                    }
                                    break;
                                }
                                if (!runTests.Run(test, pass, testNumber, dmpDir, false) || // Test failed, don't rerun
                                    RunOnceTestNames.Contains(test.TestMethod.Name)) // No point in running certain tests more than once
                                {
                                    removeList.Add(test);
                                    i = languages.Length - 1;   // Don't run other languages.
                                    break;
                                }
                                if (maxSecondsPerTest > 0)
                                {
                                    var maxSecondsPerTestPerLanguage = maxSecondsPerTest / languagesThisTest.Length; // We'd like no more than 5 minutes per test across all languages when doing stess tests
                                    if (stopWatch.Elapsed.TotalSeconds > maxSecondsPerTestPerLanguage && repeatCounter <= repeat - 1)
                                    {
                                        runTests.Log("# Breaking repeat test at count {0} of requested {1} (at {2} minutes), to allow other tests and languages to run.\r\n", repeatCounter, repeat, stopWatch.Elapsed.TotalMinutes);
                                        break;
                                    }
                                }
                            }
                            if (profiling)
                                break;
                        }
                    }

                    foreach (var removeTest in removeList)
                        testList.Remove(removeTest);
                    removeList.Clear();
                }
            }

            Console.WriteLine($"Tests finished in {timer.Elapsed} ({timer.Elapsed.TotalSeconds}s)");
            return runTests.FailureCount == 0;
        }

        //
        // Check for local ban on certain tests in nightly runs
        //
        private static void HandleNightlyTestExclusions(List<TestInfo> testList, List<TestInfo> unfilteredTestList, StreamWriter log, bool asNightly)
        {
            if (asNightly)
            {
                const string EnvVarSkylineNightlyTestExclusions = "SKYLINE_NIGHTLY_TEST_EXCLUSIONS";
                var localNightlyBans = new HashSet<string>();
                var exclusions = (Environment.GetEnvironmentVariable(EnvVarSkylineNightlyTestExclusions) ?? string.Empty);
                foreach (var bannedPatterns in exclusions.Split(','))
                {
                    if (string.IsNullOrEmpty(bannedPatterns))
                    {
                        continue;
                    }
                    var pattern = new Regex(bannedPatterns.Trim());
                    foreach (var t in testList)
                    {
                        if (pattern.Match(t.TestMethod.Name).Success)
                        {
                            localNightlyBans.Add(t.TestMethod.Name);
                        }
                    }
                    foreach (var t in unfilteredTestList)
                    {
                        if (pattern.Match(t.TestMethod.Name).Success)
                        {
                            localNightlyBans.Add(t.TestMethod.Name);
                        }
                    }
                }

                testList.RemoveAll(t => localNightlyBans.Any(lb => Equals(lb, t.TestMethod.Name)));
                unfilteredTestList.RemoveAll(t => localNightlyBans.Any(lb => Equals(lb, t.TestMethod.Name)));

                if (localNightlyBans.Any())
                {
                    RunTests.Log(log, $"# Local environment variable ${EnvVarSkylineNightlyTestExclusions} is set as \"{exclusions}\", skipping these tests:\n");
                    foreach (var banned in localNightlyBans)
                    {
                        RunTests.Log(log, $"# {banned}\n");
                    }
                }
            }
        }

        /// <summary>
        /// A class that hangs indefinitely waiting for a debugger to be attached to
        /// end the wait by setting the _endWait value to true. Some complexity needed
        /// to be added to the Wait() function in order to keep the compiler from
        /// simply optimizing it away.
        /// </summary>
        private class LeakHanger
        {
            // ReSharper disable NotAccessedField.Local
            private bool _endWait;
            private long _iterationCount;
            private DateTime _startTime;
            // ReSharper restore NotAccessedField.Local

            public bool IsTestMode
            {
                get { return false; }
            }

            public bool EndWait
            {
                get { return _endWait; }
                set { _endWait = value; }
            }

            public void Wait()
            {
                _startTime = DateTime.Now;

                // Loop forever so someone can attach a debugger
                while (!EndWait)
                {
                    Thread.Sleep(5000);
                    _iterationCount++;
                }

                RunTests.MemoryManagement.HeapDiagnostics = true;
            }
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
                var formLookup = new FormLookup();
                List<string> uncoveredForms;
                testNames = formLookup.FindTests(LoadList(formArg), out uncoveredForms);
                if (uncoveredForms.Count > 0)
                {
                    MessageBox.Show("No tests found to show these Forms: " + string.Join(", ", uncoveredForms), "Warning");
                    return testList;
                }
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

            // Sort tests alphabetically, but run perf tests last for best coverage in a fixed amount of time.
            return testList.OrderBy(e => e.IsPerfTest).ThenBy(e => e.TestMethod.Name).ToList();
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
            var allTests = GetTestList(TEST_DLLS);

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
                else if (name.StartsWith("~"))
                {
                    // e.g. ~.*Waters.*
                    var testRegex = new Regex(name.Substring(1));
                    foreach (var testInfo in allTests)
                    {
                        var testName = testInfo.TestClassType.Name + "." + testInfo.TestMethod.Name;
                        if (testRegex.IsMatch(testName))
                            outputList.Add(testName);
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

        private class DebuggerListener : IDisposable
        {
            private readonly RunTests _runTests;
            private readonly AutoResetEvent _doneSignal;
            private readonly BackgroundWorker _bw;
            private static bool _debuggerAttached;

            public DebuggerListener(RunTests runTests)
            {
                _runTests = runTests;
                _doneSignal = new AutoResetEvent(false);
                _bw = new BackgroundWorker {WorkerSupportsCancellation = true};
                _bw.DoWork += ListenForDebugger;
                _bw.RunWorkerAsync(runTests);
            }

            private void ListenForDebugger(object sender, DoWorkEventArgs e)
            {
                while (!_debuggerAttached && !_bw.CancellationPending)
                {
                    if (Debugger.IsAttached)
                    {
                        _debuggerAttached = true;
                        _runTests.Log("\r\n#!!!!! DEBUGGING STARTED !!!!!\r\n");
                    }
                    Thread.Sleep(100);
                }
                _doneSignal.Set();
            }

            public void Dispose()
            {
                _bw.CancelAsync();
                _doneSignal.WaitOne();
            }
        }

        private class LeakingTest
        {
            public string TestName;
            public double LeakSize;
        }

        // Generate a summary report of errors and memory leaks from a log file.
        private static void Report(string logFile)
        {
            var logLines = File.ReadAllLines(logFile);

            var errorList = new List<string>();
            var leakList = new List<LeakingTest>();
            var handleLeakList = new List<LeakingTest>();
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
                        var leakSize = double.Parse(parts[3]);
                        leakList.Add(new LeakingTest { TestName = test, LeakSize = leakSize });
                        continue;
                    }
                    else if (failureType == "HANDLE-LEAKED")
                    {
                        var leakSize = double.Parse(parts[3]);
                        handleLeakList.Add(new LeakingTest { TestName = test, LeakSize = leakSize });
                        continue;
                    }
                    else if (failureType == "CRT-LEAKED")
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

            if (handleLeakList.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("# Leaking handles tests (handles per run):");
                ReportLeaks(handleLeakList);
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
                Console.WriteLine("#    {0,-36} {1,10:0.#}",
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

    maxsecondspertest=[n]           Used in conjunction with the repeat value, this limits the
                                    amount of time a repeated test will take to no more than ""n"" 
                                    seconds, where  n is an integer greater than 0.  If this time 
                                    is exceeded, the test will not be repeated further.

    random=[on|off]                 Run the tests in random order (random=on, the default)
                                    or alphabetic order (random=off).  Each test is selected
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

    retrydatadownloads=[on|off]     Set retrydatadownloads=on to enable retry of data downloads
                                    on test failures, with the idea that the failure might be due
                                    to stale data sets.

    originalurls=[on|off]           Set originalurls=on to use direct links to download-on-the-fly
                                    dependencies like Crux and MSFragger. Otherwise a cached
                                    location on AWS S3 will be used.

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

    parallelmode=[off|server]       When set to server, TestRunner will launch Docker workers
                                    to run the configured tests in parallel. Docker Desktop
                                    must be installed and containers must be enabled in the
                                    operating system. Ask Matt for the Getting Started guide.

    workercount=[n]                 The number of parallel workers to run. One of the workers
                                    will be on the host, so only workercount - 1 Docker
                                    containers will be launched.

    workerport=[n]                  The port the parallel server will listen for worker connections
                                    on. If unset, a random port will be used.

    keepworkerlogs=[on|off]         Set keepworkerlogs=on to persist individual logs from each 
                                    Docker worker. Useful for debugging issues related to running
                                    tests inside a container.
");
        }

        private static void ThreadExceptionEventHandler(Object sender, ThreadExceptionEventArgs e)
        {
            Console.WriteLine("Report from TestRunner.Program.ThreadExceptionEventHandler:");
            Console.WriteLine(e.Exception.Message);
            if (string.IsNullOrEmpty(e.Exception.StackTrace))
                Console.WriteLine("No stacktrace");
            else
                Console.WriteLine(e.Exception.StackTrace);
            if (e.Exception.InnerException != null)
            {
                Console.WriteLine("Inner exception:");
                Console.WriteLine(e.Exception.InnerException.Message);
                if (string.IsNullOrEmpty(e.Exception.InnerException.StackTrace))
                    Console.WriteLine("No stacktrace");
                else
                    Console.WriteLine(e.Exception.InnerException.StackTrace);
            }
            else
            {
                Console.WriteLine("No inner exception.");
            }
            Console.Out.Flush(); // Get this info to TeamCity or SkylineTester ASAP
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
}
