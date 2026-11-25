/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Net.Http;
using System.Threading;
using pwiz.Common;
using pwiz.CommonMsData.RemoteApi;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    /// <summary>
    /// What is global to the Skyline process, for code that runs with or without the Skyline user
    /// interface: test flags, command-line import options, exit codes, the program name, and
    /// exception reporting. The Skyline UI's own globals are in its Program class, which installs
    /// <see cref="UI"/> when it starts.
    /// <para>As with Program, anything here is better stored on some more local object; should
    /// anything else need to be added, it should be clearly described why it is necessary.</para>
    /// </summary>
    public static class ProgramContext
    {
        public const int EXIT_CODE_SUCCESS = 0;
        public const int EXIT_CODE_FAILURE_TO_START = 1;
        public const int EXIT_CODE_RAN_WITH_ERRORS = 2;

        private static string _name;

        /// <summary>
        /// The Skyline user interface, or null when there is none, as in SkylineCmd.
        /// </summary>
        public static IProgramUI UI { get; set; }

        public static string MainToolServiceName { get; set; }

        // Parameters for testing.
        public static bool UnitTest { get; set; }                   // Set to true by AbstractUnitTest and AbstractFunctionalTest
        public static bool FunctionalTest
        {
            get { return CommonApplicationSettings.FunctionalTest; }
            set { CommonApplicationSettings.FunctionalTest = value; }
        }
        public static string TestName { get; set; }                 // Set during unit and functional tests
        public static bool DoNotTestUnicodeHandling { get; set; }   // Set true to skip unicode handling tests, either because the platform doesn't support it or test attribute forbids it
        public static bool UseOriginalURLs { get; set; }            // Set true to use original URLs for downloading tools instead of our S3 copies
        public static string ExtraRawFileSearchFolder { get; set; } // Perf test support for avoiding extra copying of large raw files
        public static List<Exception> TestExceptions { get; set; }  // To avoid showing unexpected exception UI during tests and instead log them as failures

        // Command-line results import support
        public static string ReplicateCachePath { get; set; }
        public static string ImportProgressPipe { get; set; }
        public static bool MultiProcImport { get; set; }

        /// <summary>
        /// Shortcut to the application name stored in <see cref="Settings"/>
        /// </summary>
        public static string Name
        {
            get
            {
                if (Settings.Default.TutorialMode)
                    _name = Settings.Default.ProgramName;
                else if (_name == null)
                {
                    _name = Settings.Default.ProgramName;
                    if (Install.Type == Install.InstallType.daily)
                        _name += @"-daily";
                }

                return _name;
            }
        }

        /// <summary>
        /// The current UI mode (proteomic / small molecule / mixed): the main window's when the
        /// Skyline UI has a document open, otherwise the one saved in <see cref="Settings"/>.
        /// </summary>
        public static SrmDocument.DOCUMENT_TYPE ModeUI
        {
            get
            {
                var mode = UI?.ModeUI;
                if (mode.HasValue)
                    return mode.Value;
                return Enum.TryParse(Settings.Default.UIMode, out SrmDocument.DOCUMENT_TYPE settingsMode)
                    ? settingsMode
                    : SrmDocument.DOCUMENT_TYPE.proteomic; // No saved setting, default to tradition
            }
        }

        /// <summary>
        /// Reports an exception caught off the UI thread: a test failure during tests, otherwise
        /// shown to the user by the Skyline UI, and ignored where there is none.
        /// </summary>
        public static void ReportException(Exception exception)
        {
            if (TestExceptions != null)
            {
                AddTestException(exception);
                return;
            }

            UI?.ReportException(exception, new StackTrace(1, true));
        }

        /// <summary>
        /// Runs a long operation, showing its progress in the Skyline UI when there is one.
        /// </summary>
        public static void RunLongOperation(string jobTitle, Action<ILongWaitBroker> action)
        {
            if (UI != null)
            {
                UI.RunLongOperation(jobTitle, action);
            }
            else
            {
                action(new SilentLongWaitBroker(CancellationToken.None));
            }
        }

        public static void AddTestException(Exception exception)
        {
            lock (TestExceptions)
            {
                TestExceptions.Add(exception);
            }
        }
    }

    /// <summary>
    /// What code that also runs without a user interface asks of the Skyline UI when there is one.
    /// See <see cref="ProgramContext.UI"/>.
    /// </summary>
    public interface IProgramUI
    {
        /// <summary>
        /// The main window's UI mode, or null when no document is open.
        /// </summary>
        SrmDocument.DOCUMENT_TYPE? ModeUI { get; }

        /// <summary>
        /// The main window, or null before it is created.
        /// </summary>
        ISkylineDocumentWindow MainWindow { get; }

        /// <summary>
        /// The main window's document, or null before it is created.
        /// </summary>
        SrmDocument ActiveDocument { get; }

        void ReportException(Exception exception, StackTrace stackTrace);

        /// <summary>
        /// Runs a long operation on this thread while showing its progress.
        /// </summary>
        void RunLongOperation(string jobTitle, Action<ILongWaitBroker> action);

        /// <summary>
        /// Asks the user to log in to a remote account.
        /// </summary>
        Func<HttpClient> UserLogin(RemoteAccount account);
    }
}
