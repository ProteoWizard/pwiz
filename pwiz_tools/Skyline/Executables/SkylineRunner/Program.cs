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
using System.Threading;

namespace SkylineRunner
{
    class Program
    {
        public static readonly object SERVER_CONNECTION_LOCK = new object();
        private bool _connected;

        static void Main(string[] args)
        {
            new Program(args);
        }

        public Program(IEnumerable<string> args)
        {
            const string skylineAppName = "Skyline-daily";
            string skylinePath = "\"" + Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            skylinePath += "\\Programs\\MacCoss Lab, UW\\" + skylineAppName + ".appref-ms" + "\"";

            var psiExporter = new ProcessStartInfo(@"cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = String.Format("/c {0} CMD", skylinePath)
            };

            Process.Start(psiExporter);

            using (var serverStream = new NamedPipeServerStream("SkylineInputPipe"))
            {
                if(!WaitForConnection(serverStream))
                {
                    Console.WriteLine("Error: Could not connect to Skyline.");
                    Console.WriteLine("Make sure you have a valid {0} installation.", skylineAppName);
                    return;
                }

                using (StreamWriter sw = new StreamWriter(serverStream))
                {
                    foreach (string arg in args)
                    {
                        sw.WriteLine(arg);
                    }
                }
            }

            using (var pipeStream = new NamedPipeClientStream("SkylineOutputPipe"))
            {
                // The connect function will wait 5s for the pipe to become available
                // If that is not acceptable specify a maximum waiting time (in ms)
                try
                {
                    pipeStream.Connect(5 * 1000);
                }
                catch (Exception)
                {
                    Console.WriteLine("Error: Could not connect to Skyline.");
                    Console.WriteLine("Make sure you have a valid {0} installation.", skylineAppName);
                    return;
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
        }

        private bool WaitForConnection(NamedPipeServerStream serverStream)
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
                    using (var pipeFake = new NamedPipeClientStream("SkylineInputPipe"))
                    {
                        pipeFake.Connect(10);
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
