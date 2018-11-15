/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Reflection;

namespace pwiz.SkylineCmd
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Assembly assembly;
            // SkylineCmd and Skyline must be in the same directory
            string dirPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            try
            {
                assembly = Assembly.LoadFrom(Path.Combine(dirPath, "Skyline-daily.exe")); // Not L10N : Keep -daily
            }
            catch (Exception e1)
            {
                try
                {
                    assembly = Assembly.LoadFrom(Path.Combine(dirPath, "Skyline.exe")); // Not L10N
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e1, e2);
                }
            }
            var programClass = assembly.GetType("pwiz.Skyline.Program"); // Not L10N
            var mainFunction = programClass.GetMethod("Main"); // Not L10N
            // ReSharper disable once PossibleNullReferenceException
            return (int) mainFunction.Invoke(null, new object[]{args});
        }
    }
}
