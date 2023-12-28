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
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public static class VendorIssueHelper
    {
        // ReSharper disable LocalizableElement
        private const string EXE_GROUP2_XML = "group2xml.exe";
        private const string EXE_GROUP_FILE_EXTRACTOR = "GroupFileExtractor.exe";
        private const string KEY_PROTEIN_PILOT = @"SOFTWARE\Classes\groFile\shell\open\command";
        // ReSharper restore LocalizableElement

        public static List<string> ConvertPilotFiles(IList<string> inputFiles, IProgressMonitor progress, IProgressStatus status)
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

                string message = string.Format(ResultsResources.VendorIssueHelper_ConvertPilotFiles_Converting__0__to_xml, Path.GetFileName(inputFile));
                int percent = index * 100 / inputFiles.Count;
                progress.UpdateProgress(status = status.ChangeMessage(message).ChangePercentComplete(percent));

                if (groupConverterExePath == null)
                {
                    var key = Registry.LocalMachine.OpenSubKey(KEY_PROTEIN_PILOT, false);
                    if (key != null)
                    {
                        string proteinPilotCommandWithArgs = (string)key.GetValue(string.Empty);

                        var proteinPilotCommandWithArgsSplit =
                            // ReSharper disable LocalizableElement
                            proteinPilotCommandWithArgs.Split(new[] { "\" \"" }, StringSplitOptions.RemoveEmptyEntries);     // Remove " "%1"
                            // ReSharper restore LocalizableElement
                        string path = Path.GetDirectoryName(proteinPilotCommandWithArgsSplit[0].Trim(new[] { '\\', '\"' })); // Remove preceding "
                        if (!string.IsNullOrEmpty(path))
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
                                    string errorMessage = string.Format(ResultsResources.VendorIssueHelper_ConvertPilotFiles_Unable_to_find__0__or__1__in_directory__2____Please_reinstall_ProteinPilot_software_to_be_able_to_handle__group_files_,
                                        EXE_GROUP_FILE_EXTRACTOR, EXE_GROUP2_XML, path);
                                    throw new IOException(errorMessage);
                                }
                            }
                        }
                    }

                    if (groupConverterExePath == null)
                    {
                        throw new IOException(ResultsResources.VendorIssueHelper_ConvertPilotFiles_ProteinPilot_software__trial_or_full_version__must_be_installed_to_convert___group__files_to_compatible___group_xml__files_);
                    }                    
                }

                // run group2xml
                // ReSharper disable LocalizableElement
                var argv = new[]
                               {
                                   "XML",
                                   "\"" + inputFile + "\"",
                                   "\"" + outputFile + "\""
                               };
                // ReSharper restore LocalizableElement

                var psi = new ProcessStartInfo(groupConverterExePath)
                              {
                                  CreateNoWindow = true,
                                  UseShellExecute = false,
                                  // Common directory includes the directory separator
                                  // ReSharper disable once ConstantNullCoalescingCondition
                                  WorkingDirectory = Path.GetDirectoryName(groupConverterExePath) ?? string.Empty,
                                  Arguments = string.Join(@" ", argv.ToArray()),
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
                    throw new IOException(TextUtil.LineSeparate(string.Format(ResultsResources.VendorIssueHelper_ConvertPilotFiles_Failure_attempting_to_convert_file__0__to__group_xml_,
                                                        inputFile), string.Empty, sbOut.ToString()));
                }

                inputFilesPilotConverted.Add(outputFile);
            }
            progress.UpdateProgress(status.ChangePercentComplete(100));
            return inputFilesPilotConverted;
        }
    }
}
