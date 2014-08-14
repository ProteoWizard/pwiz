/*
 * Original author: Yuval Boss <yuval .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml;

namespace OutputParser
{
    class Program
    {
        static int Main(string[] args)
        {
            const int EXIT_SUCCESS = 0;
            const int EXIT_ERROR = 1;
            bool containsNonLocalizedStrings = false;
            var totalIssueCounter = 0;
            XmlDocument xmlDoc = new XmlDocument(); // Create an XML document object
            xmlDoc.Load(args[0]); // Load the XML document from the specified file, args[0] is xml output file.
            Console.WriteLine("Parsing");
            // Get elements
            XmlNodeList ids = xmlDoc.GetElementsByTagName("IssueType");
            // Display the results
            Console.WriteLine("Code contains the following issues:");
            var severities = ids.Cast<XmlNode>().ToDictionary(id => id.Attributes["Id"].Value, id => id.Attributes["Severity"].Value);

            XmlNodeList projects = xmlDoc.GetElementsByTagName("Project");
            Console.WriteLine("Checking projects for issues...");
            var concatIssues = new Dictionary<string, List<string>>(); // Dictionary of issue type with value of string list of all files with that issue.
            var issueCounter = new Dictionary<string, int>();
            foreach (XmlNode project in projects)
            {
                    Console.WriteLine("Project: " + project.Attributes["Name"].Value);
                    XmlNodeList issues = project.ChildNodes;
                    foreach (XmlNode issue in issues)
                    {
                        // Will only add to dictionary if the type is rated as a WARNING or and ERROR.
                        if (severities[issue.Attributes["TypeId"].Value].Equals("WARNING") || severities[issue.Attributes["TypeId"].Value].Equals("ERROR"))
                        {
                            if (!concatIssues.ContainsKey(issue.Attributes["TypeId"].Value))
                            {
                                concatIssues.Add(issue.Attributes["TypeId"].Value,new List<string>());
                                issueCounter.Add(issue.Attributes["TypeId"].Value, 0);
                            }
                            if (!concatIssues[issue.Attributes["TypeId"].Value].Contains(issue.Attributes["File"].Value))
                            {
                                concatIssues[issue.Attributes["TypeId"].Value].Add(issue.Attributes["File"].Value);
                                issueCounter[issue.Attributes["TypeId"].Value]++;
                                totalIssueCounter++;
                                if (issue.Attributes["TypeId"].Value.Equals("NonLocalizedString"))
                                {
                                    containsNonLocalizedStrings = true;
                                }
                            }
                        }
                    }  
            }
            foreach (var val in concatIssues)
            {
                Console.WriteLine(issueCounter[val.Key] + " " + val.Key + " " + severities[val.Key] + "/s in file/s:");
                foreach (var value in val.Value)
                {
                    Console.WriteLine("*"+value);
                }
                Console.WriteLine(string.Empty);
            }
            Console.WriteLine("This tool will fail the build if there are more than 33 issues or any NonLocalizedStrings.");
            Console.WriteLine("Total issues in solution: {0}", totalIssueCounter);
            if (totalIssueCounter > 33 || containsNonLocalizedStrings)
            {
                Console.WriteLine("\r\n InspectCode Failed.");
                return EXIT_ERROR;
            }
                Console.WriteLine("\r\n InspectCode Passed.");
                return EXIT_SUCCESS;
        }
    }
}
