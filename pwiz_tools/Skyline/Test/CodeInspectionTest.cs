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
// Performs code inspections that ReSharper misses, particularly in generated code which ReSharper ignores by design.
// 



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Inspect files by mask, searching for prohibited strings.
    /// For now, at least, this only looks at generated code to pick out problems that ReSharper ignores.
    /// </summary>
    [TestClass]
    public class CodeInspectionTest : AbstractUnitTest
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

        [TestMethod]
        public void CodeInspection()
        {
            var thisFile = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                AssertEx.Fail("Could not get Skyline directory name for code inspection");
            }
            else
            {
                var root = thisFile.Replace("\\Test\\CodeInspectionTest.cs",string.Empty);
                var result = string.Empty;
                foreach (var fileMask in forbidden.Keys)
                {
                    foreach (var filename in Directory.GetFiles(root, fileMask, SearchOption.AllDirectories))
                    {
                        var file = new StreamReader(filename);
                        string line;
                        var lineNum = 0;
                        while ((line = file.ReadLine()) != null)
                        {
                            lineNum++;
                            foreach (var pattern in forbidden[fileMask].Keys.Where(p => line.Contains(p)))
                            {
                                var why = forbidden[fileMask][pattern];
                                var error = @"Code inspection failure: prohibited use of" + Environment.NewLine + 
                                            @"""" + pattern + @"""" + Environment.NewLine +
                                            "(" + why + ") at" + Environment.NewLine + filename + "(" + lineNum + @"):" + Environment.NewLine;
                                result += error ;
                                result += line + Environment.NewLine + Environment.NewLine;
                            }
                        }
                        file.Close();
                    }
                }
                if (!string.IsNullOrEmpty(result))
                {
                    AssertEx.Fail(result);
                }
            }
        }
    }
}

