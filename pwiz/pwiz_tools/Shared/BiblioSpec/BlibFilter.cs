/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using pwiz.Common.SystemUtil;

namespace pwiz.BiblioSpec
{
    public class BlibFilter
    {
        public const string EXE_BLIB_FILTER = "BlibFilter"; // Not L10N
        public bool Filter(string sourceFile, string destinationFile, IProgressMonitor progressMonitor, ref ProgressStatus status)
        {
            // ReSharper disable NonLocalizedString
            var argv = new List<string>
                           {
                               "-b true",
                               "\"" + sourceFile + "\"",
                               "\"" + destinationFile + "\""
                           };
            // ReSharper restore NonLocalizedString


            var psiBlibFilter = new ProcessStartInfo(EXE_BLIB_FILTER)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(destinationFile) ?? string.Empty,
                Arguments = string.Join(" ", argv.ToArray()), // Not L10N
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var processRunner = new ProcessRunner();
            processRunner.Run(psiBlibFilter, null, progressMonitor, ref status);
            return status.IsComplete;
        }
    }
}
