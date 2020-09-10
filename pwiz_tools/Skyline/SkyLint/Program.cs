/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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


//
// Small program to inspect for changes to code that ReSharper misses.
// 
// Uses git to look for new code that will be in next commit and inspects it for forbidden content
// 


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SkyLint 
{

    static class Program
    {
        private static Dictionary<string, Dictionary<string, string>> forbidden = new Dictionary<string, Dictionary<string, string>>
        {
            { @"*.Designer.cs", // File types to inspect for this error
                new Dictionary<string, string>
                {
                    {
                        @".ImageScalingSize = new System.Drawing.Size(", // Forbidden pattern
                        @"causes issues on HD monitors" // Explanation for prohibition
                    }
                }} 
        };

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                throw new Exception("SkyLint expects a single argument, which is the Skyline project directory");
            }

            var result = string.Empty;
            foreach (var fileMask in forbidden.Keys)
            {
                foreach (var filename in Directory.GetFiles(args[0], fileMask, SearchOption.AllDirectories))
                {
                    var file = new StreamReader(filename);
                    string line;
                    int lineNum = 0;
                    while ((line = file.ReadLine()) != null)
                    {
                        lineNum++;
                        foreach (var pattern in forbidden[fileMask].Keys.Where(p => line.Contains(p)))
                        {
                            var why = forbidden[fileMask][pattern];
                            var error = @"SkyLint code inspection failure: prohibited use of" + Environment.NewLine + 
                                        @"""" + pattern + @"""" + Environment.NewLine +
                                        "(" + why + ") at" + Environment.NewLine + filename + "(" + lineNum + @"):" + Environment.NewLine;
                            Console.Write(error);
                            Console.WriteLine(line);
                            result += error ;
                            result += line + Environment.NewLine + Environment.NewLine;
                        }
                    }

                    file.Close();
                }
            }
            if (!string.IsNullOrEmpty(result))
            {
                MessageBox.Show(result, "SkyLint Code Inspection", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Environment.Exit(1);
            }
        }
    }
}

