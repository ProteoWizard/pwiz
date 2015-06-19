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
using SkylineRunner.Properties;

namespace SkylineRunner
{
    class Program
    {
        public static readonly object SERVER_CONNECTION_LOCK = new object();
        private bool _connected;

        static int Main(string[] args)
        {
            return new Program().run(args);
        }

        private int run(IEnumerable<string> args)
        {
            const string skylineAppName = "Skyline"; // Not L10N
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
            string guidSuffix = string.Format("-{0}", Guid.NewGuid()); // Not L10N
            var psiExporter = new ProcessStartInfo(@"cmd.exe") // Not L10N
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = String.Format("/c \"{0}\" CMD{1}", skylinePath, guidSuffix) // Not L10N
            };
            Process.Start(psiExporter);

            string inPipeName = "SkylineInputPipe" + guidSuffix; // Not L10N
            using (var serverStream = new NamedPipeServerStream(inPipeName))
            {
                if(!WaitForConnection(serverStream, inPipeName))
                {
                    Console.WriteLine(Resources.Program_Program_Error__Could_not_connect_to_Skyline_);
                    Console.WriteLine(Resources.Program_Program_Make_sure_you_have_a_valid__0__installation_, skylineAppName);
                    return 1;
                }

                using (StreamWriter sw = new StreamWriter(serverStream))
                {
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

                using (StreamReader sr = new StreamReader(pipeStream))
                {
                    string line;
                    //While (!done reading)
                    while ((line = sr.ReadLine()) != null)
                    {
                        Console.WriteLine(line);
                    }
                }
            }
            return 0;
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
            lock (SERVER_CONNECTION_LOCK)
            {
                Monitor.Wait(SERVER_CONNECTION_LOCK, 5 * 1000);
                connected = _connected;
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
                catch (Exception ignored)
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
    }
}
