/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using SkylineTool;
using System;

namespace TestCommandLineInteractiveTool
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: {0} <connection> (OutputProcessIds|MonitorSelection|SetNoteOnSelectedNode)...");
                return -1;
            }
            var toolConnection = args[0];
            // Open connection to Skyline.
            using (var toolClient = new SkylineToolClient(toolConnection, "Test Interactive Tool")) // Not L10N
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string commandName = args[i];
                    if (commandName == "OutputProcessIds")
                    {
                        new OutputProcessIds(toolClient).RunCommand();
                    }
                    else if (commandName == "MonitorSelection")
                    {
                        new MonitorSelection(toolClient).RunCommand();
                    }
                    else if (commandName == "SetNoteOnSelectedNode")
                    {
                        new SetNoteOnSelectedNode(toolClient).RunCommand();
                    }
                    else if (commandName == "DeleteSelectedNode")
                    {
                        new DeleteSelectedNode(toolClient).RunCommand();
                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown command: {0}", commandName);
                    }
                }
            }

            return 0;
        }
    }
}
