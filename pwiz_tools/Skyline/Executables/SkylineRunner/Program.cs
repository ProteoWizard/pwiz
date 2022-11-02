/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using pwiz.SkylineRunner.Properties;

namespace pwiz.SkylineRunner
{
    class Program
    {
        private static readonly object SERVER_CONNECTION_LOCK = new object();

        private bool _connected;

        static int Main(string[] args)
        {
            return new Program().Run(args);
        }

        private int Run(IEnumerable<string> args)
        {
            const string skylineAppName = "Skyline-daily"; // Not L10N
            string[] possibleSkylinePaths = ListPossibleSkylineShortcutPaths(skylineAppName);
            string skylinePath = possibleSkylinePaths.FirstOrDefault(File.Exists);
            if (null == skylinePath)
            {
                Console.WriteLine(Resources.Program_Program_Error__Unable_to_find_Skyline_program_at_any_of_the_following_locations_);
                foreach (var path in possibleSkylinePaths)
                {
                    Console.WriteLine(path);
                }
                return 1;
            }

            skylinePath = EscapeIfNecessary(skylinePath);
            string guidSuffix = string.Format("-{0}", Guid.NewGuid()); // Not L10N
            var psiExporter = new ProcessStartInfo(@"cmd.exe") // Not L10N
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = string.Format("/c \"{0}\" CMD{1}", skylinePath, guidSuffix) // Not L10N
            };
            Process.Start(psiExporter);

            string inPipeName = "SkylineInputPipe" + guidSuffix; // Not L10N
            using (var serverStream = new NamedPipeServerStream(inPipeName))
            {
                if(!WaitForConnection(serverStream, inPipeName))
                {
                    Console.WriteLine(Resources.Program_Program_Error__Could_not_connect_to_Skyline_);
                    Console.WriteLine(@"    cmd.exe {0}", psiExporter.Arguments);
                    Console.WriteLine(Resources.Program_Program_Make_sure_you_have_a_valid__0__installation_, skylineAppName);
                    return 1;
                }

                using (StreamWriter sw = new StreamWriter(serverStream))
                {
                    // Send the console width for SkylineRunner to Skyline
                    try
                    {
                        sw.WriteLine("--sw=" + (Console.BufferWidth - 1));
                    }
                    catch
                    {
                        // Rely on the default width. The command is being run in an invironment without a screen width
                    }
                    // Send the directory of SkylineRunner to Skyline
                    sw.WriteLine("--dir=" + Directory.GetCurrentDirectory()); // Not L10N

                    foreach (string arg in args)
                    {
                        sw.WriteLine(arg);
                    }
                }
            }

            using (var pipeStream = new NamedPipeClientStream("SkylineOutputPipe" + guidSuffix)) // Not L10N
            {
                // The connect function will wait 5s for the pipe to become available
                // If that is not acceptable specify a maximum waiting time (in ms)
                try
                {
                    pipeStream.Connect(5 * 1000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(Resources.Program_Program_Error__Could_not_connect_to_Skyline_);
                    Console.Write(e.Message);
                    return 1;
                }

                var exitCode = 0;

                using (StreamReader sr = new StreamReader(pipeStream))
                {
                    string line;
                    //While (!done reading)
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (ErrorChecker.IsErrorLine(line))   // In case of Skyline-daily with untranslated text
                        {
                            exitCode = 2;
                        }
                        Console.WriteLine(line);
                    }
                }
                return exitCode;
            }
        }

        private bool WaitForConnection(NamedPipeServerStream serverStream, string inPipeName)
        {
            Thread connector = new Thread(() =>
            {
                serverStream.WaitForConnection();
                lock (SERVER_CONNECTION_LOCK)
                {
                    _connected = true;
                    Monitor.Pulse(SERVER_CONNECTION_LOCK);
                }
            });

            connector.Start();

            bool connected;
            var wait = 5;
            lock (SERVER_CONNECTION_LOCK)
            {
                Monitor.Wait(SERVER_CONNECTION_LOCK, wait * 1000);
                connected = _connected;
            }

            if (!connected)
            {
                // Wait another 10 seconds for a total of 15 seconds.
                Console.Write(Resources.Program_WaitForConnection_Waiting_for_Skyline);

                wait++;
                var timer = new System.Timers.Timer(1000);
                timer.Elapsed += (sender, e) =>
                {
                    Console.Write(@".");
                    wait++;
                };
                timer.Start();
                lock (SERVER_CONNECTION_LOCK)
                {
                    Monitor.Wait(SERVER_CONNECTION_LOCK, 10 * 1000);
                    connected = _connected;
                } 
                timer.Stop();
                timer.Dispose();

                Console.Write($@" {wait}s");
                Console.WriteLine();
            }

            if (!connected)
            {
                // Clear the waiting thread.
                try
                {
                    using (var pipeFake = new NamedPipeClientStream(inPipeName))
                    {
                        pipeFake.Connect(10);
                    }
                    return false;
                }
                // ReSharper disable once UnusedVariable
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }

        private static string[] ListPossibleSkylineShortcutPaths(string skylineAppName)
        {
            string programsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string shortcutFilename = skylineAppName + ".appref-ms"; // Not L10N
            return new[]
            {
                Path.Combine(Path.Combine(programsFolderPath, "MacCoss Lab, UW"), shortcutFilename), // Not L10N
                Path.Combine(Path.Combine(programsFolderPath, skylineAppName), shortcutFilename),
            };
        }

        /// <summary>
        /// Apply Windows command-line escaping if necessary. The command is quoted, but
        /// this doesn't seem to work if special characters are present, e.g. in the user name.
        /// This problem was found with a username that contained an ampersand "V&amp;V...".
        /// Once escaping is applied, it must be applied even to spaces, which do not cause
        /// issues for the simple quoted version without escaping:
        ///
        /// C:\Users\A&amp;B\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Skyline\Skyline.appref-ms
        ///
        /// must become:
        ///
        /// C:\Users\A^&amp;B\AppData\Roaming\Microsoft\Windows\Start^ Menu\Programs\Skyline\Skyline.appref-ms
        ///
        /// Note: User names can't contain these characters: /\[]:&lt;>+=;,?"*%
        /// Note: Paths can't contain these characters: \/:*?"&lt;>|
        ///
        /// In the end, it seems like only &amp; and ^ (the escape character itself are
        /// possible and problematic)
        /// </summary>
        private string EscapeIfNecessary(string path)
        {
            var escapeChars = "^&".ToCharArray();   // The caret (^) must be first to avoid duplication
            if (path.IndexOfAny(escapeChars) != -1)
            {
                foreach (var escapeChar in escapeChars.Append(' ')) // add space to characters that need escaping
                    path = path.Replace(escapeChar.ToString(), "^" + escapeChar);
            }
            return path;
        }
    }
}
