/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
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
using System.IO.Pipes;
using System.Threading;

namespace SkylineRunner
{
    class Program
    {
        static void Main(string[] args)
        {

            string skylinePath = "\"" + Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            skylinePath += "\\Programs\\MacCoss Lab, UW\\Skyline-daily.appref-ms" + "\"";

            string arguments = CommaSeparate(args);

            //Console.WriteLine(arguments);

            var psiExporter = new ProcessStartInfo(@"cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = String.Format("/c {0} {1}", skylinePath, arguments)
            };

            Process.Start(psiExporter); 

            Thread clientThread = new Thread(ThreadStartClient);

            clientThread.Start();

            return;
        }


        /// <summary>
        /// This function will concatenate the given strings, separating them
        /// with commas. No prefix or suffix comma will be added.
        /// </summary>
        /// <param name="args">See summary</param>
        /// <returns>See summary</returns>
        private static string CommaSeparate(string[] args)
        {
            if (args.Length == 0)
                return "";

            string output = "";
            for (int i = 0; i < args.Length - 1; i++)
            {
                output += args[i] + ',';
            }

            return output + args[args.Length - 1];
        }


        /// <summary>
        /// This function will try for 3 seconds to open a named pipe ("SkylinePipe").
        /// If this operation is not successful, the function will exit. Otherwise,
        /// the function will print each line received from the pipe
        /// out to the console and then wait for a newline from the user.
        /// </summary>
        public static void ThreadStartClient()
        {

            using (NamedPipeClientStream pipeStream = new NamedPipeClientStream("SkylinePipe"))
            {
                // The connect function will wait 10 seconds for the pipe to become available
                // If that is not acceptable specify a maximum waiting time (in ms)
                try
                {
                    pipeStream.Connect(10*1000);
                } 
                catch(Exception)
                {
                    Console.WriteLine("Error: Couldn't connect to Skyline process.");
                    return;
                }
                //Console.WriteLine("[Client] Pipe connection established");
                using (StreamReader sw = new StreamReader(pipeStream))
                {
                    string temp;
                    while ((temp = sw.ReadLine()) != null)
                    {
                        Console.WriteLine(temp);
                    }
                }

                Console.ReadLine();
            }
        }
    }
}
