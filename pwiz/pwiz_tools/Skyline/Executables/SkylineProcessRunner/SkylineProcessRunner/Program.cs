/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.IO.Pipes;
using System.Text;

namespace SkylineProcessRunner
{
    class Program
    {
        static int Main(string[] args)
        {
            // grab the GUID and then remove it from the arguments to be passed to the command prompt
            string guidSuffix = args[0];
            var cmdArgs = new List<string>(args);
            cmdArgs.RemoveAt(0);

            using (var clientStream = new NamedPipeClientStream("SkylineProcessRunnerPipe" + guidSuffix)) // Not L10N
            {
                try
                {
                    clientStream.Connect(10000);
                }
                catch (TimeoutException)
                {
                    // if we cannot connect to the NamedPipe, the installation fails
                    Console.WriteLine("Error: Could not connect to Skyline"); // Not L10N
                    return 1;
                }

                var startInfo = new ProcessStartInfo("cmd.exe") // Not L10N
                {
                    // Windows 8 needs the entire command line for cmd.exe to be quoted as a single argument
                    Arguments = "/C \"" + JoinArgs(cmdArgs.ToArray()) + "\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                // write output of the package install onto the pipe AND this window
                using (var writer = new StreamWriter(clientStream))
                {
                    var installer = new Process { StartInfo = startInfo };
                    // ReSharper disable AccessToDisposedClosure
                    installer.OutputDataReceived += (sender, eventArgs) => WriteLine(writer, eventArgs.Data);
                    installer.ErrorDataReceived += (sender, eventArgs) => WriteLine(writer, eventArgs.Data);
                    // ReSharper restore AccessToDisposedClosure

                    installer.Start();
                    installer.BeginOutputReadLine();
                    installer.BeginErrorReadLine();
                    installer.WaitForExit();

                    int exitCode = installer.ExitCode;
                    installer.Close();
                    return exitCode;
                }
            }
        }

        private static void WriteLine(TextWriter writer, string data)
        {
            Console.WriteLine(data);
            writer.WriteLine(data);
        }

        public static string JoinArgs(string[] arguments)
        {
            var commandLineArguments = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (argument.Contains(" ") || argument.Contains("\t") || argument.Equals(string.Empty))
                {
                    commandLineArguments.Append(" \"" + argument + "\"");
                }
                else
                {
                    commandLineArguments.Append(" " + argument);
                }
            }
            commandLineArguments.Remove(0, 1);
            return commandLineArguments.ToString();
        }
    }
}
