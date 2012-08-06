/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Text;
using Microsoft.Win32;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal static class VendorIssueHelper
    {
        private const string EXE_MZ_WIFF = "mzWiff"; // Not L10N
        private const string EXT_WIFF_SCAN = ".scan"; // Not L10N

        private const string EXE_GROUP2_XML = "group2xml";
        private const string KEY_PROTEIN_PILOT = @"SOFTWARE\Classes\groFile\shell\open\command";

        public static string CreateTempFileSubstitute(string filePath, int sampleIndex,
            LoadingTooSlowlyException slowlyException, ILoadMonitor loader, ref ProgressStatus status)
        {
            string tempFileSubsitute = Path.GetTempFileName();

            try
            {
                switch (slowlyException.WorkAround)
                {
                    case LoadingTooSlowlyException.Solution.local_file:
                        loader.UpdateProgress(
                            status = status.ChangeMessage(
                                string.Format(Resources.VendorIssueHelper_CreateTempFileSubstitute_Local_copy_work_around_for__0__,
                                              Path.GetFileName(filePath))));
                        File.Copy(filePath, tempFileSubsitute, true);
                        break;
                    // This is a legacy solution that should no longer ever be invoked.  The mzWiff.exe has
                    // been removed from the installation.
                    // TODO: This code should be removed also.
                    case LoadingTooSlowlyException.Solution.mzwiff_conversion:
                        loader.UpdateProgress(
                            status = status.ChangeMessage(
                                string.Format(Resources.VendorIssueHelper_CreateTempFileSubstitute_Convert_to_mzXML_work_around_for__0__,
                                              Path.GetFileName(filePath))));
                        ConvertWiffToMzxml(filePath, sampleIndex, tempFileSubsitute, slowlyException, loader);
                        break;
                }

                return tempFileSubsitute;
            }
            catch (Exception)
            {
                FileEx.DeleteIfPossible(tempFileSubsitute);
                throw;
            }
        }

        public static List<string> ConvertPilotFiles(IList<string> inputFiles, IProgressMonitor progress, ProgressStatus status)
        {
            string group2XmlPath = null;
            var inputFilesPilotConverted = new List<string>();

            for (int index = 0; index < inputFiles.Count; index++)
            {
                string inputFile = inputFiles[index];
                if (!inputFile.EndsWith(BiblioSpecLiteBuilder.EXT_PILOT))
                {
                    inputFilesPilotConverted.Add(inputFile);
                    continue;
                }
                string outputFile = Path.ChangeExtension(inputFile, BiblioSpecLiteBuilder.EXT_PILOT_XML);
                // Avoid re-converting files that have already been converted
                if (File.Exists(outputFile))
                {
                    // Avoid duplication, in case the user accidentally adds both .group and .group.xml files
                    // for the same results
                    if (!inputFiles.Contains(outputFile))
                        inputFilesPilotConverted.Add(outputFile);
                    continue;
                }

                string message = string.Format("Converting {0} to xml", Path.GetFileName(inputFile));
                int percent = index * 100 / inputFiles.Count;
                progress.UpdateProgress(status = status.ChangeMessage(message).ChangePercentComplete(percent));

                if (group2XmlPath == null)
                {
                    var key = Registry.LocalMachine.OpenSubKey(KEY_PROTEIN_PILOT, false);
                    if (key != null)
                    {
                        string proteinPilotCommandWithArgs = (string)key.GetValue(string.Empty);

                        var proteinPilotCommandWithArgsSplit =
                            proteinPilotCommandWithArgs.Split(new[] { "\" \"" }, StringSplitOptions.RemoveEmptyEntries);     // Remove " "%1"
                        string path = Path.GetDirectoryName(proteinPilotCommandWithArgsSplit[0].Trim(new[] { '\\', '\"' })); // Remove preceding "
                        if (path != null)
                            group2XmlPath = Path.Combine(path, EXE_GROUP2_XML);
                    }

                    if (group2XmlPath == null)
                    {
                        throw new IOException("ProteinPilot software (trial or full version) must be installed to convert '.group' files to compatible '.group.xml' files.");
                    }                    
                }

                // run group2xml
                var argv = new[]
                               {
                                   "XML",
                                   "\"" + inputFile + "\"",
                                   "\"" + outputFile + "\""
                               };

                var psi = new ProcessStartInfo(group2XmlPath)
                              {
                                  CreateNoWindow = true,
                                  UseShellExecute = false,
                                  // Common directory includes the directory separator
                                  WorkingDirectory = Path.GetDirectoryName(group2XmlPath) ?? string.Empty,
                                  Arguments = string.Join(" ", argv.ToArray()),
                                  RedirectStandardError = true,
                                  RedirectStandardOutput = true,
                              };

                var sbOut = new StringBuilder();
                var proc = Process.Start(psi);

                var reader = new ProcessStreamReader(proc);
                string line;
                while ((line = reader.ReadLine()) != null)
                    sbOut.AppendLine(line);

                while (!proc.WaitForExit(200))
                {
                    if (progress.IsCanceled)
                    {
                        proc.Kill();
                        return inputFilesPilotConverted;
                    }
                }

                if (proc == null || (proc.ExitCode != 0))
                {
                    throw new IOException(string.Format("Failure attempting to convert file {0} to .group.xml.\n\n{1}",
                                                        inputFile, sbOut));
                }

                inputFilesPilotConverted.Add(outputFile);
            }
            progress.UpdateProgress(status.ChangePercentComplete(100));
            return inputFilesPilotConverted;
        }

        private static void ConvertWiffToMzxml(string filePathWiff, int sampleIndex,
            string outputPath, LoadingTooSlowlyException slowlyException, IProgressMonitor monitor)
        {
            if (AdvApi.GetPathFromProgId("Analyst.ChromData") == null) // Not L10N
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.VendorIssueHelper_ConvertWiffToMzxml_The_file__0__cannot_be_imported_by_the_AB_SCIEX_WiffFileDataReader_library_in_a_reasonable_time_frame_1_F02_min,
                                                                  filePathWiff, slowlyException.PredictedMinutes),
                                                    string.Format(Resources.VendorIssueHelper_ConvertWiffToMzxml_To_work_around_this_issue_requires_Analyst_to_be_installed_on_the_computer_running__0__,
                                                                  Program.Name),
                                                    Resources.VendorIssueHelper_ConvertWiffToMzxml_Please_install_Analyst__or_run_this_import_on_a_computure_with_Analyst_installed);
                throw new IOException(message);
            }

            // The WIFF file needs to be on the local file system for the conversion
            // to work.  So, just in case it is on a network share, copy it to the
            // temp directory.
            string tempFileSource = Path.GetTempFileName();
            try
            {
                File.Copy(filePathWiff, tempFileSource, true);
                string filePathScan = GetWiffScanPath(filePathWiff);
                if (File.Exists(filePathScan))
                    File.Copy(filePathScan, GetWiffScanPath(tempFileSource));
                ConvertLocalWiffToMzxml(tempFileSource, sampleIndex, outputPath, monitor);
            }
            finally
            {
                FileEx.DeleteIfPossible(tempFileSource);
                FileEx.DeleteIfPossible(GetWiffScanPath(tempFileSource));
            }
        }

        private static string GetWiffScanPath(string filePathWiff)
        {
            return filePathWiff + EXT_WIFF_SCAN;
        }

        private static void ConvertLocalWiffToMzxml(string filePathWiff, int sampleIndex,
            string outputPath, IProgressMonitor monitor)
        {
            var argv = new[]
                           {
                               "--mzXML", // Not L10N
                               "-s" + (sampleIndex + 1), // Not L10N
                               "\"" + filePathWiff + "\"", // Not L10N
                               "\"" + outputPath + "\"", // Not L10N
                           };

            var psi = new ProcessStartInfo(EXE_MZ_WIFF)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                // Common directory includes the directory separator
                WorkingDirectory = Path.GetDirectoryName(filePathWiff) ?? string.Empty,
                Arguments = string.Join(" ", argv.ToArray()),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            var sbOut = new StringBuilder();
            var proc = Process.Start(psi);

            var reader = new ProcessStreamReader(proc);
            string line;
            while ((line = reader.ReadLine()) != null)
                sbOut.AppendLine(line);

            while (!proc.WaitForExit(200))
            {
                if (monitor.IsCanceled)
                {
                    proc.Kill();
                    throw new LoadCanceledException(new ProgressStatus(string.Empty).Cancel());
                }
            }

            // Exit code -4 is a compatibility warning but not necessarily an error
            if (proc == null || (proc.ExitCode != 0 && !IsCompatibilityWarning(proc.ExitCode)))
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.VendorIssueHelper_ConvertLocalWiffToMzxml_Failure_attempting_to_convert_sample__0__in__1__to_mzXML_to_work_around_a_performance_issue_in_the_AB_Sciex_WiffFileDataReader_library,
                                                                  sampleIndex, filePathWiff),
                                                    string.Empty,
                                                    sbOut.ToString());
                throw new IOException(message);
            }
        }

        /// <summary>
        /// True if the mzWiff exit code is non-zero, but only for a warning
        /// that does not necessarily mean anything is wrong with the output.
        /// </summary>
        private static bool IsCompatibilityWarning(int exitCode)
        {
            return exitCode == -2 ||
                   exitCode == -3 ||
                   exitCode == -4;
        }
    }

    internal class LoadingTooSlowlyException : IOException
    {
        public enum Solution { local_file, mzwiff_conversion }

        public LoadingTooSlowlyException(Solution solution, ProgressStatus status, double predictedMinutes, double maximumMinutes)
            : base(string.Format(Resources.LoadingTooSlowlyException_LoadingTooSlowlyException_Data_import_expected_to_consume__0__minutes_with_maximum_of__1__mintues,
                                 predictedMinutes, maximumMinutes))
        {
            WorkAround = solution;
            Status = status;
            PredictedMinutes = predictedMinutes;
            MaximumMinutes = maximumMinutes;
        }

        public Solution WorkAround { get; private set; }
        public ProgressStatus Status { get; private set; }
        public double PredictedMinutes { get; private set; }
        public double MaximumMinutes { get; private set; }
    }
}
