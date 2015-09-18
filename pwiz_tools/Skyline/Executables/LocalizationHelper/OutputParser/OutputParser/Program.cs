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
            const int MAX_ISSUES_ALLOWED = 2; // should be changed to permit more or less issues allowed before build fails
            string NON_LOCALIZED_STRING = "NonLocalizedString"; // Exact name of the error that R# will throw for missing L10Ns

            bool containsNonLocalizedStrings = false;
            var totalIssueCounter = 0;
            XmlDocument xmlDocL10N = new XmlDocument(); // Create an XML document object
            xmlDocL10N.Load(args[0]); // Load the XML document from the specified file, args[0] is xml output file with all R# 9.0 warnings.

            XmlDocument xmlDoc9 = new XmlDocument(); // Create an XML document object
            xmlDoc9.Load(args[1]); // Load the XML document from the specified file, args[1] is xml output file with only l10n warnings.

            Console.WriteLine("Parsing");
            // Get elements
            XmlNodeList ids = xmlDoc9.GetElementsByTagName("IssueType");
            var severities = ids.Cast<XmlNode>().ToDictionary(id => id.Attributes["Id"].Value, id => id.Attributes["Severity"].Value);
            severities.Add(NON_LOCALIZED_STRING, "WARNING");

            XmlNodeList projects = xmlDoc9.GetElementsByTagName("Project");
            Console.WriteLine("Checking projects for issues...");

            // Issue list of file names
            var concatIssues = new Dictionary<string, List<string>>(); // Dictionary of issue type with value of string list of all files with that issue.
            concatIssues.Add(NON_LOCALIZED_STRING, new List<string>()); // add empty L10N to issues list

            // Issue Counter
            var issueCounter = new Dictionary<string, int>();
            issueCounter.Add(NON_LOCALIZED_STRING, 0); // add L10N with coutn of 0 to issue counter

            // R# 9.0 issues inspection
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
                            var location = issue.Attributes["File"].Value + ":" + issue.Attributes["Line"].Value;
                            if (!concatIssues[issue.Attributes["TypeId"].Value].Contains(location))
                            {
                                concatIssues[issue.Attributes["TypeId"].Value].Add(location);
                            }
                            issueCounter[issue.Attributes["TypeId"].Value]++;
                            totalIssueCounter++;
                        }
                    }  
            }

            // L10N inspection
            XmlNodeList L10NProjects = xmlDocL10N.GetElementsByTagName("Project");
            Console.WriteLine("Inspecting for unmarked or non-localized strings");
            foreach (XmlNode project in L10NProjects)
            {
                Console.WriteLine("Project: " + project.Attributes["Name"].Value);
                XmlNodeList issues = project.ChildNodes;
                
                foreach (XmlNode issue in issues)
                {
                    // Will only add to dictionary if the type is rated as a WARNING or and ERROR.
                    if (issue.Attributes["TypeId"].Value.Equals(NON_LOCALIZED_STRING))
                    {
                        if (!concatIssues[issue.Attributes["TypeId"].Value].Contains(issue.Attributes["File"].Value))
                        {
                            concatIssues[issue.Attributes["TypeId"].Value].Add(issue.Attributes["File"].Value);
                        }
                        issueCounter[NON_LOCALIZED_STRING]++;
                        totalIssueCounter++;
                        containsNonLocalizedStrings = true;
                    }
                }
            }

            // prints out all issues & files containing issues
            foreach (var val in concatIssues)
            {
                Console.WriteLine(issueCounter[val.Key] + " " + val.Key + " " + severities[val.Key] + "/s in file/s:");
                foreach (var value in val.Value)
                {
                    Console.WriteLine("*"+value);
                }
                Console.WriteLine(string.Empty);
            }
            Console.WriteLine("This tool will fail the build if there are more than {0} issues or any NonLocalizedStrings.", MAX_ISSUES_ALLOWED);
            Console.WriteLine("(You can adjust this threshold by changing the value of MAX_ISSUES_ALLOWED in pwiz_tools\\Skyline\\Executables\\LocalizationHelper\\OutputParser\\OutputParser\\Program.cs, and rebuilding with pwiz_tools\\Skyline\\Executables\\LocalizationHelper\\OutputParser\\OutputParser.sln then committing both the updated source and exe files.)");
            Console.WriteLine("Total issues in solution is currently: {0}", totalIssueCounter);

            if (totalIssueCounter > MAX_ISSUES_ALLOWED || containsNonLocalizedStrings)
            {
                Console.WriteLine("\r\n InspectCode Failed.");
                return EXIT_ERROR;
            }
            Console.WriteLine("\r\n InspectCode Passed.");
            return EXIT_SUCCESS;
        }
    }
}
