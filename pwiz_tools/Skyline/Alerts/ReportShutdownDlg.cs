/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Show an error reporting dialog after Skyline had an unhandled exception on a non-UI thread.
    /// </summary>
    public class ReportShutdownDlg : ReportErrorDlg
    {
        private const string EXCEPTION_FILE = "SkylineException.txt";

        public ReportShutdownDlg()
        {
            // Get details of unhandled exception from file written prior to shutdown.
            string[] lines;
            string exceptionFile = GetExceptionFile();
            try
            {
                lines = File.ReadAllLines(exceptionFile);
            }
            finally
            {
                Helpers.TryTwice(() => File.Delete(exceptionFile));
            }

            string exceptionType = lines.ElementAtOrDefault(0) ?? string.Empty;
            string exceptionMessage = lines.ElementAtOrDefault(1) ?? string.Empty;
            Init(exceptionType, exceptionMessage, TextUtil.LineSeparate(lines));

            // Change the title and intro text of the dialog.
            SetTitleAndIntroText(
                AlertsResources.ReportShutdownDlg_ReportShutdownDlg_Unexpected_Shutdown,
                AlertsResources.ReportShutdownDlg_ReportShutdownDlg_Skyline_had_an_unexpected_error_the_last_time_you_ran_it_,
                AlertsResources.ReportShutdownDlg_ReportShutdownDlg_Report_the_error_to_help_improve_Skyline_);
            StartPosition = FormStartPosition.CenterScreen;
            // This dialog should be shown in the taskbar because it does not have a parent
            ShowInTaskbar = true;
        }

        /// <summary>
        /// Save details of an unhandled exception in a file prior to Skyline shutdown.
        /// </summary>
        /// <param name="exception">The unhandled exception.</param>
        /// <param name="forced">Use true to save a file even when in a functional test</param>
        public static void SaveExceptionFile(Exception exception, bool forced = false)
        {
            var exceptionInfo = Environment.NewLine + exception + Environment.NewLine;

            if (!forced)
            {
                Messages.WriteAsyncDebugMessage(exceptionInfo);
                Console.WriteLine(exceptionInfo);
            }

            if (forced || !Program.FunctionalTest)
            {
                var exceptionInfo2 = new StringBuilder();
                exceptionInfo2.AppendLine(exception.GetType().FullName);
                exceptionInfo2.AppendLine(exception.Message);
                exceptionInfo2.AppendLine(ExceptionUtil.GetExceptionText(exception, null));
                File.WriteAllText(GetExceptionFile(), exceptionInfo2.ToString());
            }
        }

        /// <summary>
        /// Return true if Skyline had an unexpected shutdown during a previous run.
        /// </summary>
        public static bool HadUnexpectedShutdown(bool forced = false)
        {
            var exceptionFile = GetExceptionFile();
            if (File.Exists(exceptionFile))
            {
                if (forced || !Program.StressTest)
                {
                    // Ignore unhandled exception if it occurred more than 1 day ago.
                    var fileInfo = new FileInfo(exceptionFile);
                    if (fileInfo.LastWriteTime < DateTime.Now + TimeSpan.FromDays(1))
                        return true;
                }
                Helpers.TryTwice(() => File.Delete(exceptionFile));
            }
            return false;
        }

        /// <summary>
        /// Add prefix to report title so that these errors stand out in the exception logs.
        /// </summary>
        protected override string PostTitle
        {
            get { return @"SHUTDOWN: " + base.PostTitle; }
        }

        private static string GetExceptionFile()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(assemblyDir ?? string.Empty, EXCEPTION_FILE);
        }
    }
}
