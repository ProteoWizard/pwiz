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
using System.Diagnostics;
using System.IO;
using System.Text;

namespace pwiz.Skyline.Util.Extensions
{
    public static class UtilProcess
    {
        public static void RunProcess(this ProcessStartInfo psi, IProgressMonitor progress, ref ProgressStatus status)
        {
            psi.RunProcess(null, progress, ref status);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin)
        {
            var statusTemp = new ProgressStatus("");
            psi.RunProcess(stdin, null, ref statusTemp);
        }

        public static void RunProcess(this ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref ProgressStatus status)
        {
            var procBlibBuilder = Process.Start(psi);
            if (procBlibBuilder == null)
                throw new IOException(string.Format("Failure starting {0} command.", psi.FileName));
            if (stdin != null)
            {
                try
                {
                    procBlibBuilder.StandardInput.Write(stdin);
                }
                finally
                {
                    procBlibBuilder.StandardInput.Close();
                }
            }

            StreamReader error = procBlibBuilder.StandardError;
            StringBuilder sbError = new StringBuilder();
            string line;
            while ((line = error.ReadLine()) != null)
            {
                if (progress == null || line.ToLower().StartsWith("error"))
                {
                    sbError.Append(line);
                }
                else // if (progress != null)
                {
                    if (progress.IsCanceled)
                    {
                        procBlibBuilder.Kill();
                        progress.UpdateProgress(status = status.Cancel());
                        return;
                    }

                    if (line.EndsWith("%"))
                    {
                        int percent;
                        string[] parts = line.Split(' ');
                        string percentPart = parts[parts.Length - 1];
                        if (int.TryParse(percentPart.Substring(0, percentPart.Length - 1), out percent))
                        {
                            status = status.ChangePercentComplete(percent);
                            progress.UpdateProgress(status);
                        }
                    }
                    else
                    {
                        status = status.ChangeMessage(line);
                        progress.UpdateProgress(status);
                    }                    
                }
            }
            procBlibBuilder.WaitForExit();
            int exit = procBlibBuilder.ExitCode;
            if (exit != 0)
            {
                line = procBlibBuilder.StandardError.ReadLine();
                if (line != null)
                    sbError.AppendLine(line);
                if (sbError.Length == 0)
                    throw new Exception("Error occurred running process.");
                throw new IOException(sbError.ToString());
            }
        }
    }
}
