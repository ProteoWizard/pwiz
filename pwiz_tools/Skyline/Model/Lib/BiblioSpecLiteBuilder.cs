/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
// ReSharper disable InconsistentNaming
    public enum LibraryBuildAction { Create, Append }
// ReSharper restore InconsistentNaming

    public sealed class BiblioSpecLiteBuilder : ILibraryBuilder
    {
        public const string EXE_BLIB_BUILD = "BlibBuild";
        public const string EXE_BLIB_FILTER = "BlibFilter";

        public const string EXT_PEP_XML = ".pep.xml";
        public const string EXT_PEP_XML_ONE_DOT = ".pepXML";
        public const string EXT_IDP_XML = ".idpXML";
        public const string EXT_SQT = ".sqt";
        public const string EXT_DAT = ".dat";
        public const string EXT_XTAN_XML = ".xtan.xml";
        public const string EXT_PILOT_XML = ".group.xml";
        public const string EXT_SQLITE_JOURNAL = "-journal";

        private ReadOnlyCollection<string> _inputFiles;

        public BiblioSpecLiteBuilder(string name, string outputPath, IList<string> inputFiles)
        {
            LibrarySpec = new BiblioSpecLiteSpec(name, outputPath);

            InputFiles = inputFiles;
        }

        public LibrarySpec LibrarySpec { get; private set; }
        public string OutputPath { get { return LibrarySpec.FilePath; } }

        public LibraryBuildAction Action { get; set; }
        public bool KeepRedundant { get; set; }
        public double? CutOffScore { get; set; }
        public string Authority { get; set; }
        public string Id { get; set; }

        public IList<string> InputFiles
        {
            get { return _inputFiles; }
            private set { _inputFiles = value as ReadOnlyCollection<string> ?? new ReadOnlyCollection<string>(value); }
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            string message = string.Format("Building {0} library", Path.GetFileName(OutputPath));
            ProgressStatus status = new ProgressStatus(message);

            progress.UpdateProgress(status);

            // Arguments for BlibBuild
            List<string> argv = new List<string> { "-s" };  // Read from stdin
            if (Action == LibraryBuildAction.Create)
                argv.Add("-o");
            if (CutOffScore.HasValue)
            {
                argv.Add("-c");
                argv.Add(CutOffScore.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrEmpty(Authority))
            {
                argv.Add("-a");
                argv.Add(Authority);
            }
            if (!string.IsNullOrEmpty(Id))
            {
                argv.Add("-i");
                argv.Add(Id);
            }
            // TODO: Need to allow files on stdin to avoid overflowing command-line length limit
            //       For now, use a working directory as close to the input files as possible
            string dirCommon = PathEx.GetCommonRoot(InputFiles);
            var stdinBuilder = new StringBuilder();
            foreach (string fileName in InputFiles)
                stdinBuilder.AppendLine(fileName.Substring(dirCommon.Length));

            string outputDir = Path.GetDirectoryName(OutputPath);
            string outputBaseName = Path.GetFileNameWithoutExtension(OutputPath);
            string redundantLibrary = Path.Combine(outputDir, outputBaseName + BiblioSpecLiteSpec.EXT_REDUNDANT);

            argv.Add("\"" + redundantLibrary + "\"");

            var psiBlibBuilder = new ProcessStartInfo(EXE_BLIB_BUILD)
                                     {
                                         CreateNoWindow = true,
                                         UseShellExecute = false,
                                         // Common directory includes the directory separator
                                         WorkingDirectory = dirCommon.Substring(0, dirCommon.Length - 1),
                                         Arguments = string.Join(" ", argv.ToArray()),
                                         RedirectStandardOutput = true,
                                         RedirectStandardError = true,
                                         RedirectStandardInput = true
                                     };
            try
            {
                psiBlibBuilder.RunProcess(stdinBuilder.ToString(), null, progress, ref status);
            }
            catch (IOException x)
            {
                progress.UpdateProgress(status = status.ChangeErrorException(x));
                return false;
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
                progress.UpdateProgress(status = status.ChangeErrorException(new Exception(string.Format("Failed trying to build the redundant library {0}.",redundantLibrary))));
                return false;
            }
            finally
            {
                // If something happened (error or cancel) to end processing, then
                // get rid of the possibly partial redundant library.
                if (!status.IsComplete)
                {
                    File.Delete(redundantLibrary);
                    File.Delete(redundantLibrary + EXT_SQLITE_JOURNAL);                    
                }
            }

            status = status.ChangeMessage(message).ChangePercentComplete(0);
            progress.UpdateProgress(status);

            // Write the non-redundant library to a temporary file first
            using (var saver = new FileSaver(OutputPath))
            {
                // Arguments for BlibFilter
                argv.Clear();

                argv.Add("\"" + redundantLibrary + "\"");
                argv.Add("\"" + saver.SafeName + "\"");

                var psiBlibFilter = new ProcessStartInfo(EXE_BLIB_FILTER)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    // Common directory includes the directory separator
                    WorkingDirectory = outputDir,
                    Arguments = string.Join(" ", argv.ToArray()),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                try
                {
                    psiBlibFilter.RunProcess(progress, ref status);
                    saver.Commit();
                }
                catch (IOException x)
                {
                    progress.UpdateProgress(status.ChangeErrorException(x));
                    return false;
                }
                catch
                {
                    progress.UpdateProgress(status.ChangeErrorException(new Exception(string.Format("Failed trying to build the library {0}.", OutputPath))));
                    return false;
                }
                finally
                {
                    if (!status.IsComplete)
                        File.Delete(saver.SafeName + "-journal");
                    if (!KeepRedundant)
                        File.Delete(redundantLibrary);
                }
            }

            return status.IsComplete;
        }
    }
}
