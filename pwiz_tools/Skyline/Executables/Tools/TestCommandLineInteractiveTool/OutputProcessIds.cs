﻿/*
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
using System;
using System.Diagnostics;
using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class OutputProcessIds : AbstractCommand
    {
        public OutputProcessIds(SkylineToolClient skylineToolClient) : base(skylineToolClient)
        {
        }

        public override void RunCommand()
        {
            Console.Out.WriteLine("{0} process id: {1} Skyline process id: {2}", typeof(OutputProcessIds).Assembly.GetName().Name, Process.GetCurrentProcess().Id, SkylineToolClient.GetProcessId());
        }
    }
}
