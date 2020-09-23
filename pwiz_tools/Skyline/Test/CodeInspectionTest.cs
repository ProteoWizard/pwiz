﻿/*
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
// Performs custom code inspections that ReSharper can't, particularly in generated code which ReSharper ignores by design.
// 


using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using Environment = System.Environment;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Inspect files by mask, searching for prohibited or required strings.
    /// </summary>
    [TestClass]
    public class CodeInspectionTest : AbstractUnitTest
    {

        [TestMethod]
        public void CodeInspection()
        {
            AddInspection(@"*.Designer.cs", Inspection.Forbidden, 
                null, // There are no parts of the codebase that should skip this check
                string.Empty, // No file content required for inspection
                @".ImageScalingSize = new System.Drawing.Size(", // Forbidden pattern
                @"causes blurry icon issues on HD monitors"); // Explanation for prohibition

            AddInspection(@"*.Designer.cs", Inspection.Required,
                NonLocalizedFiles(), // Ignore violations in files or directories we don't localize
                string.Empty, // No file content required for inspection
                @"System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager", // Required pattern
                @"ensures that every dialog is localizable"); // Explanation for requirement

            // AddInspection(@"*.Designer.cs", Inspection.Required, null, "Windows Form Designer generated code", @"DetectionsToolbar", @"fake, debug purposes only"); // Uncomment for debug purposes
            // AddInspection(@"*.cs", Inspection.Forbidden, null, string.Empty, @"DetectionsToolbar", @"fake, debug purposes only"); // Uncomment for debug purposes
            // AddInspection(@"*.xml", Inspection.Required, null, "xml",  @"encoding=""utf-8""", @"fake, debug purposes only"); // Uncomment for debug purposes

            PerformInspections();
        }

        private string GetCodeBaseRoot(out string thisFile)
        {
            thisFile = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
            if (string.IsNullOrEmpty(thisFile))
            {
                AssertEx.Fail("Could not get Skyline directory name for code inspection");
            }
            // ReSharper disable once PossibleNullReferenceException
            return thisFile.Replace("\\Test\\CodeInspectionTest.cs", string.Empty);
         }

        private void PerformInspections()
        {
            var root = GetCodeBaseRoot(out var thisFile);
            if (!Directory.Exists(root))
            {
                return; // Don't fail, this might be an install with no code in it
            }

            var results = new List<string>();
            foreach (var fileMask in allFileMasks)
            {
                foreach (var filename in Directory.GetFiles(root, fileMask, SearchOption.AllDirectories))
                {
                    if (Equals(filename, thisFile))
                    {
                        continue; // Can't inspect yourself!
                    }

                    var lines = File.ReadAllLines(filename);
                    var lineNum = 0;
                    var requiredPatternsObservedInThisFile = requiredPatternsByFileMask.ContainsKey(fileMask)
                        ? requiredPatternsByFileMask[fileMask].Where(kvp =>
                        {
                            // Do we need to worry about this pattern for this file?
                            var patternDetails = kvp.Value;
                            return !patternDetails.IgnorePath(filename) &&
                                   (string.IsNullOrEmpty(patternDetails.Cue) // No requirements for file content
                                    || lines.Any(l => l.Contains(patternDetails.Cue))); // File contains required cue
                        }).ToDictionary(k => k.Key, k => false)
                        : null;
                    var forbiddenPatternsForThisFile =
                        forbiddenPatternsByFileMask
                            .ContainsKey(fileMask) // Are there any forbidden patterns for this filemask?
                            ? forbiddenPatternsByFileMask[fileMask].Where(kvp =>
                            {
                                // Do we need to worry about this pattern for this file?
                                var patternDetails = kvp.Value;
                                return !patternDetails.IgnorePath(filename) &&
                                       (string.IsNullOrEmpty(patternDetails.Cue) // No requirements for file content
                                        || lines.Any(l => l.Contains(patternDetails.Cue))); // File contains required cue
                            }).ToDictionary(k => k.Key, k => k.Value.Reason)
                            : null;

                    var result = new List<string>();

                    foreach (var line in lines)
                    {
                        lineNum++;
                        if (forbiddenPatternsForThisFile != null)
                        {
                            foreach (var pattern in forbiddenPatternsForThisFile.Keys.Where(p => line.Contains(p)))
                            {
                                var why = forbiddenPatternsByFileMask[fileMask][pattern].Reason;
                                result.Add(@"Found prohibited use of");
                                result.Add(@"""" + pattern + @"""");
                                result.Add("(" + why + ") at");
                                result.Add(filename + "(" + lineNum + @")");
                                result.Add(line);
                                result.Add(string.Empty);
                            }
                        }

                        if (requiredPatternsObservedInThisFile != null)
                        {
                            foreach (var pattern in requiredPatternsObservedInThisFile.Keys)
                            {
                                if (line.Contains(pattern))
                                {
                                    requiredPatternsObservedInThisFile[pattern] = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (requiredPatternsObservedInThisFile != null)
                    {
                        foreach (var requirement in requiredPatternsObservedInThisFile.Where(p => !p.Value))
                        {
                            var why = requiredPatternsByFileMask[fileMask][requirement.Key].Reason;
                            result.Add(@"Did not find required use of");
                            result.Add(@"""" + requirement.Key + @"""");
                            result.Add("(" + why + ") in");
                            result.Add(filename);
                            result.Add(string.Empty);
                        }
                    }

                    if (result.Any())
                    {
                        results.Add(string.Join(Environment.NewLine, result));
                    }
                }
            }

            if (results.Any())
            {
                results.Insert(0, string.Empty);
                results.Insert(0, string.Format("{0} code inspection failures found:", results.Count));
                AssertEx.Fail(string.Join(Environment.NewLine, results));
            }
        }


        public class PatternDetails
        {
            public string Cue; // If non-empty, pattern only applies to files containing this cue
            public string Reason; // Note to show on failure
            public string[] IgnoredDirectories; // Don't flag on hits in these directories

            public bool IgnorePath(string path)
            {
                return string.IsNullOrEmpty(path) ||
                       IgnoredDirectories != null && IgnoredDirectories.Any(d => path.ToLower().Contains(d.ToLower()));
            }
        }
        private readonly Dictionary<string, Dictionary<string, PatternDetails>> forbiddenPatternsByFileMask = new Dictionary<string, Dictionary<string, PatternDetails>>();
        private readonly Dictionary<string, Dictionary<string, PatternDetails>> requiredPatternsByFileMask = new Dictionary<string, Dictionary<string, PatternDetails>>();

        private enum Inspection { Forbidden, Required }

        private HashSet<string> allFileMasks = new HashSet<string>();

        private string[] NonLocalizedFiles()
        {
            var root = GetCodeBaseRoot(out var thisFile);
            if (string.IsNullOrEmpty(root))
            {
                return null; // Not an installation with code alongside
            }

            var result = new List<string>();

            // Get the list of files/directories that we don't localize per the KeepResx utility
            foreach (var line in File.ReadAllLines(root + "\\Executables\\KeepResx\\Program.cs"))
            {
                if (line.Trim().StartsWith(@"@"""))
                {
                    var parts = line.Split('\"');
                    result.Add(parts[1].Replace("*", string.Empty));
                }
            }

            // Add any others we know don't require L10N
            result.Add("CommandArgUsage.designer.cs");
            result.Add("ActionBoxControl.Designer.cs");
            result.Add("Model\\AuditLog\\AuditLogStrings.Designer.cs");
            result.Add("Model\\AuditLog\\EnumNames.Designer.cs");
            result.Add("Model\\AuditLog\\PropertyElementNames.Designer.cs");
            result.Add("Model\\AuditLog\\PropertyNames.Designer.cs");
            result.Add("AsyncRenderControl.Designer.cs");
            result.Add("AsyncChromatogramsGraph2.designer.cs");
            result.Add("QuantificationStrings.Designer.cs");
            result.Add("ColorGrid.Designer.cs");
            result.Add("ColumnCaptions.Designer.cs");
            result.Add("ColumnToolTips.Designer.cs");
            result.Add("FormulaBox.Designer.cs");
            result.Add("GroupComparisonStrings.Designer.cs");
            result.Add("RecentFileControl.Designer.cs");
            result.Add("settings.designer.cs");
            result.Add("resources.designer.cs");
            result.Add("Executables\\KeepResxW");
            result.Add("Executables\\Tools\\MSstats");
            result.Add("Executables\\Tools\\MS1Probe");
            result.Add("Executables\\Tools\\Skyline Gadget");
            result.Add("Executables\\Tools\\QuaSAR");
            result.Add("Executables\\Tools\\SProCoP");
            result.Add("Executables\\Tools\\TestArgCollector");
            return result.ToArray();
        }

        void AddInspection(string fileMask, Inspection inspectionType, string[] ignoredDirectories, string cue, string pattern, string reason)
        {
            allFileMasks.Add(fileMask);
            var rules = inspectionType == Inspection.Forbidden ? forbiddenPatternsByFileMask : requiredPatternsByFileMask;
            if (!rules.ContainsKey(fileMask))
            {
                rules.Add(fileMask, new Dictionary<string, PatternDetails>());
            }
            var patterns = rules[fileMask];
            patterns.Add(pattern, new PatternDetails { Cue = cue, Reason = reason, IgnoredDirectories = ignoredDirectories });
        }
    }
}

