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
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    internal static class VendorIssueHelper
    {
        // ReSharper disable NonLocalizedString
        private const string EXE_MZ_WIFF = "mzWiff";
        private const string EXT_WIFF_SCAN = ".scan";
        private const string KEY_COMPASSXPORT = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\CompassXport.exe";

        private const string EXE_GROUP2_XML = "group2xml.exe";
        private const string EXE_GROUP_FILE_EXTRACTOR = "GroupFileExtractor.exe";
        private const string KEY_PROTEIN_PILOT = @"SOFTWARE\Classes\groFile\shell\open\command";
        // ReSharper restore NonLocalizedString

        public static List<string> ConvertPilotFiles(IList<string> inputFiles, IProgressMonitor progress, ProgressStatus status)
        {
            string groupConverterExePath = null;
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

                if (groupConverterExePath == null)
                {
                    var key = Registry.LocalMachine.OpenSubKey(KEY_PROTEIN_PILOT, false);
                    if (key != null)
                    {
                        string proteinPilotCommandWithArgs = (string)key.GetValue(string.Empty);

                        var proteinPilotCommandWithArgsSplit =
                            proteinPilotCommandWithArgs.Split(new[] { "\" \"" }, StringSplitOptions.RemoveEmptyEntries);     // Remove " "%1" // Not L10N
                        string path = Path.GetDirectoryName(proteinPilotCommandWithArgsSplit[0].Trim(new[] { '\\', '\"' })); // Remove preceding "
                        if (path != null)
                        {
                            var groupFileExtractorPath = Path.Combine(path, EXE_GROUP_FILE_EXTRACTOR);
                            if (File.Exists(groupFileExtractorPath))
                            {
                                groupConverterExePath = groupFileExtractorPath;
                            }
                            else
                            {
                                var group2XmlPath = Path.Combine(path, EXE_GROUP2_XML);
                                if (File.Exists(group2XmlPath))
                                {
                                    groupConverterExePath = group2XmlPath;
                                }
                                else
                                {
                                    string errorMessage = string.Format(Resources.VendorIssueHelper_ConvertPilotFiles_Unable_to_find__0__or__1__in_directory__2____Please_reinstall_ProteinPilot_software_to_be_able_to_handle__group_files_,
                                        EXE_GROUP_FILE_EXTRACTOR, EXE_GROUP2_XML, path);
                                    throw new IOException(errorMessage);
                                }
                            }
                        }
                    }

                    if (groupConverterExePath == null)
                    {
                        throw new IOException(Resources.VendorIssueHelper_ConvertPilotFiles_ProteinPilot_software__trial_or_full_version__must_be_installed_to_convert___group__files_to_compatible___group_xml__files_);
                    }                    
                }

                // run group2xml
                // ReSharper disable NonLocalizedString
                var argv = new[]
                               {
                                   "XML",
                                   "\"" + inputFile + "\"",
                                   "\"" + outputFile + "\""
                               };
                // ReSharper restore NonLocalizedString

                var psi = new ProcessStartInfo(groupConverterExePath)
                              {
                                  CreateNoWindow = true,
                                  UseShellExecute = false,
                                  // Common directory includes the directory separator
                                  WorkingDirectory = Path.GetDirectoryName(groupConverterExePath) ?? string.Empty,
                                  Arguments = string.Join(" ", argv.ToArray()), // Not L10N
                                  RedirectStandardError = true,
                                  RedirectStandardOutput = true,
                              };

                var sbOut = new StringBuilder();
                var proc = new Process {StartInfo = psi};
                proc.Start();

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
                Arguments = string.Join(" ", argv.ToArray()), // Not L10N
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            var sbOut = new StringBuilder();
            var proc = new Process { StartInfo = psi };
            proc.Start();

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
            // ReSharper disable NonLocalizedString
            var argv = new[]
                           {
                               "-a \"" + filePathBruker + "\"",     // input file (directory)
                               "-o \"" + outputPath + "\"",         // output file (directory)
                               "-mode 2",                           // mode 2 (mzML)
                               "-raw 0"                             // export line spectra (profile data is HUGE and SLOW!)
                           };
            // ReSharper restore NonLocalizedString

            // Start CompassXport in its own process.
            var psi = new ProcessStartInfo(compassXportExe)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                // Common directory includes the directory separator
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                Arguments = string.Join(" ", argv), // Not L10N
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var proc = new Process { StartInfo = psi };
            proc.Start();

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
                match = Regex.Match(line, @"Spectrum \d+ - (\d+)"); // Not L10N
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
}
