/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Util;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Tools
{
    public static class ToolDescriptionHelpers
    {
        /// <summary>
        ///  A helper function that generates the report with reportTitle from the SrmDocument.
        ///  Throws an error if the reportSpec no longer exists in Settings.Default.
        /// </summary>
        /// <param name="doc">Document to create the report from.</param>
        /// <param name="reportTitle">Title of the reportSpec to make a report from.</param>
        /// <param name="toolTitle">Title of tool for exception error message.</param>
        /// <param name="progressMonitor">Progress monitor.</param>
        /// <param name="writer">TextWriter that the report should be written to.</param>
        /// <returns> Returns a string representation of the ReportTitle report, or throws an error that the reportSpec no longer exist. </returns>
        public static void GetReport(SrmDocument doc, string reportTitle, string toolTitle,
            IProgressMonitor progressMonitor, TextWriter writer)
        {
            var container = new MemoryDocumentContainer();
            container.SetDocument(doc, container.Document);
            var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
            var viewContext = new DocumentGridViewContext(dataSchema);
            ViewInfo viewInfo = viewContext.GetViewInfo(PersistedViews.ExternalToolsGroup.Id.ViewName(reportTitle));
            if (null == viewInfo)
            {
                throw new ToolExecutionException(
                    string.Format(
                        Resources
                            .ToolDescriptionHelpers_GetReport_Error_0_requires_a_report_titled_1_which_no_longer_exists__Please_select_a_new_report_or_import_the_report_format,
                        toolTitle, reportTitle));
            }

            IProgressStatus status =
                new ProgressStatus(string.Format(Resources.ReportSpec_ReportToCsvString_Exporting__0__report,
                    reportTitle));
            progressMonitor.UpdateProgress(status);
            if (!viewContext.Export(CancellationToken.None, progressMonitor, ref status, viewInfo, writer,
                    TextUtil.SEPARATOR_CSV))
            {
                throw new OperationCanceledException();
            }
        }


        // Long test names make for long Tools directory names, which can make for long command lines - maybe too long. So limit that directory name length by
        // shortening to acronym and original length (e.g. "Foo7WithBar" => "F7WB10", "Foo7WithoutBar" => "F7WB13"))
        private static string LimitDirectoryNameLength()
        {
            var testName =
                Program.TestName.Length > 10 // Arbitrary cutoff, but too little is likely to lead to ambiguous names
                    ? string.Concat(Program.TestName.Replace(@"Test", string.Empty)
                        .Where(c => char.IsUpper(c) || char.IsDigit(c))) + Program.TestName.Length
                    : Program.TestName;
            return $@"{testName}_{Thread.CurrentThread.CurrentCulture.Name}";
        }

        /// <summary>
        /// Get a name for the Skyline Tools directory - if we are running a test, make that name unique to the test in case tests are executing in parallel
        /// </summary>
        public static string GetToolsDirectory()
        {
            var skylineDirPath = GetSkylineInstallationPath();

            // Use a unique tools path when running tests to allow tests to run in parallel
            // ReSharper disable once AssignNullToNotNullAttribute
            return Path.Combine(skylineDirPath, Program.UnitTest ? $@"Tools_{LimitDirectoryNameLength()}" : @"Tools");
        }

        /// <summary>
        /// Gets the current installation directory, where we would expect to find Tools directory etc
        /// </summary>
        public static string GetSkylineInstallationPath()
        {
            var skylinePath = Assembly.GetExecutingAssembly().Location;
            Assume.IsFalse(string.IsNullOrEmpty(skylinePath), @"Could not determine Skyline installation location");
            var skylineDirPath = Path.GetDirectoryName(skylinePath);
            Assume.IsFalse(string.IsNullOrEmpty(skylineDirPath),
                @"Could not determine Skyline installation directory name");
            return skylineDirPath;
        }
    }
    public class ToolExecutionException : Exception
    {
        public ToolExecutionException(string message) : base(message) { }

        public ToolExecutionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
