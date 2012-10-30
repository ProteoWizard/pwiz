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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
        private const string KEY_COMPASSXPORT = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\CompassXport.exe";

        private const string EXE_GROUP2_XML = "group2xml";
        private const string KEY_PROTEIN_PILOT = @"SOFTWARE\Classes\groFile\shell\open\command";

        public static string CreateTempFileSubstitute(string filePath, int sampleIndex,
            LoadingTooSlowlyException slowlyException, ILoadMonitor loader, ref ProgressStatus status)
        {
            string tempFileSubsitute = Path.GetTempFileName();
            // Bruker CompassXport may append .mzML to the name we give it
            string tempFileMzml = tempFileSubsitute + ".mzML";

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
                    case LoadingTooSlowlyException.Solution.bruker_conversion:
                        ConvertBrukerToMzml(filePath, tempFileSubsitute, loader, status);
                        loader.UpdateProgress(status = status.ChangeMessage(
                            string.Format(Resources.SkylineWindow_ImportResults_Import__0__,    // Bruker prefers the conversion go unnoted
                                          filePath)));
                        if (File.Exists(tempFileMzml))
                        {
                            // Handle the case where bruker refuses to export to the name it is given
                            // and insists on appending .mzML
                            FileEx.DeleteIfPossible(tempFileSubsitute);
                            tempFileSubsitute = tempFileMzml;
                        }
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
                if (slowlyException.WorkAround == LoadingTooSlowlyException.Solution.bruker_conversion)
                    FileEx.DeleteIfPossible(tempFileMzml);
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

                string message = string.Format(Resources.VendorIssueHelper_ConvertPilotFiles_Converting__0__to_xml, Path.GetFileName(inputFile));
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
                        throw new IOException(Resources.VendorIssueHelper_ConvertPilotFiles_ProteinPilot_software__trial_or_full_version__must_be_installed_to_convert___group__files_to_compatible___group_xml__files_);
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
                {
                    if (progress.IsCanceled)
                    {
                        proc.Kill();
                        throw new LoadCanceledException(status.Cancel());
                    }

                    sbOut.AppendLine(line);
                }

                while (!proc.WaitForExit(200))
                {
                    if (progress.IsCanceled)
                    {
                        proc.Kill();
                        return inputFilesPilotConverted;
                    }
                }

                if (proc.ExitCode != 0)
                {
                    throw new IOException(TextUtil.LineSeparate(string.Format(Resources.VendorIssueHelper_ConvertPilotFiles_Failure_attempting_to_convert_file__0__to__group_xml_,
                                                        inputFile), string.Empty, sbOut.ToString()));
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
            {
                if (monitor.IsCanceled)
                {
                    proc.Kill();
                    throw new LoadCanceledException(new ProgressStatus(string.Empty).Cancel());
                }

                sbOut.AppendLine(line);
            }

            while (!proc.WaitForExit(200))
            {
                if (monitor.IsCanceled)
                {
                    proc.Kill();
                    throw new LoadCanceledException(new ProgressStatus(string.Empty).Cancel());
                }
            }

            // Exit code -4 is a compatibility warning but not necessarily an error
            if (proc.ExitCode != 0 && !IsCompatibilityWarning(proc.ExitCode))
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

        private static void ConvertBrukerToMzml(string filePathBruker,
            string outputPath, IProgressMonitor monitor, ProgressStatus status)
        {
            // We use CompassXport, if it is installed, to convert a Bruker raw file to mzML.  This solves two
            // issues: the Bruker reader can't be called on any thread other than the main thread, and there
            // is no 64-bit version of the reader.  So we start CompassXport in its own 32-bit process, 
            // and use it to convert the raw data to mzML in a temporary file, which we read back afterwards.
            var key = Registry.LocalMachine.OpenSubKey(KEY_COMPASSXPORT, false);
            string compassXportExe = (key != null) ? (string)key.GetValue(string.Empty) : null;
            if (compassXportExe == null)
                throw new IOException(Resources.VendorIssueHelper_ConvertBrukerToMzml_CompassXport_software_must_be_installed_to_import_Bruker_raw_data_files_);

            // CompassXport arguments
            var argv = new[]
                           {
                               "-a \"" + filePathBruker + "\"",     // input file (directory)
                               "-o \"" + outputPath + "\"",         // output file (directory)
                               "-mode 2",                           // mode 2 (mzML)
                               "-raw 0"                             // export line spectra (profile data is HUGE and SLOW!)
                           };

            // Start CompassXport in its own process.
            var psi = new ProcessStartInfo(compassXportExe)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                // Common directory includes the directory separator
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                Arguments = string.Join(" ", argv),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var proc = Process.Start(psi);

            // CompassXport starts by calculating a hash of the input file.  This takes a long time, and there is
            // no intermediate output during this time.  So we set the progress bar some fraction of the way and
            // let it sit there and animate while we wait for the start of spectra processing.
            const int hashPercent = 25; // percentage of import time allocated to calculating the input file hash
            
            int spectrumCount = 0;

            var sbOut = new StringBuilder();
            var reader = new ProcessStreamReader(proc);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (monitor.IsCanceled)
                {
                    proc.Kill();
                    throw new LoadCanceledException(status.Cancel());
                }

                sbOut.AppendLine(line);
                line = line.Trim();

                // The main part of conversion starts with the hash calculation.
                if (line.StartsWith("Calculating hash")) // Not L10N 
                {
                    status = status.ChangeMessage(Resources.VendorIssueHelper_ConvertBrukerToMzml_Calculating_hash_of_input_file)
                        .ChangePercentComplete(hashPercent);
                    monitor.UpdateProgress(status);
                    continue;
                }

                // Determine how many spectra will be converted so we can track progress.
                var match = Regex.Match(line, @"Converting (\d+) spectra"); // Not L10N 
                if (match.Success)
                {
                    spectrumCount = int.Parse(match.Groups[1].Value);
                    continue;
                }

                // Update progress as each spectra batch is converted.
                match = Regex.Match(line, @"Spectrum \d+ - (\d+)");
                if (match.Success)
                {
                    var spectrumEnd = int.Parse(match.Groups[1].Value);
                    var percentComplete = hashPercent + (100-hashPercent)*spectrumEnd/spectrumCount;
                    status = status.ChangeMessage(line).ChangePercentComplete(percentComplete);
                    monitor.UpdateProgress(status);
                }
            }

            while (!proc.WaitForExit(200))
            {
                if (monitor.IsCanceled)
                {
                    proc.Kill();
                    throw new LoadCanceledException(status.Cancel());
                }
            }

            if (proc.ExitCode != 0)
            {
                throw new IOException(TextUtil.LineSeparate(string.Format(Resources.VendorIssueHelper_ConvertBrukerToMzml_Failure_attempting_to_convert__0__to_mzML_using_CompassXport_,
                    filePathBruker), string.Empty, sbOut.ToString()));
            }
        }
    }

    internal class LoadingTooSlowlyException : IOException
    {
        public enum Solution { local_file, mzwiff_conversion, bruker_conversion }

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
