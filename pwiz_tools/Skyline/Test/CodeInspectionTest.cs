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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Controls;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Util;
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
            // Looking for uses of MessageBox where we should really be using MessageDlg
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonSkylineDirectories(), // We only care about this in Skyline code
                string.Empty, // No file content required for inspection
                @"MessageBox.Show", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"use MessageDlg.Show instead - this ensures proper interaction with automated tests, small molecule interface operation, and other enhancements", // Explanation for prohibition, appears in report
                @"// Purposely using MessageBox here"); // There is one legitimate use of this, look for this comment and ignore the violation when found

            // Looking for forgotten PauseTest() calls that will mess up automated tests
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                new[] { @"TestFunctional.cs" }, // Only these files should contain this
                string.Empty, // No file content required for inspection
                @"^\s*PauseTest\(", // Forbidden pattern (uncommented PauseTest)
                true, // Pattern is not a regular expression
                @"This appears to be temporary debugging code that should not be checked in."); // Explanation for prohibition, appears in report

            // Looking for non-standard image scaling
            AddTextInspection(@"*.Designer.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null, // There are no parts of the codebase that should skip this check
                string.Empty, // No file content required for inspection
                @".ImageScalingSize = new System.Drawing.Size(", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"causes blurry icon issues on HD monitors"); // Explanation for prohibition, appears in report

            // Looking for unlocalized dialogs
            AddTextInspection(@"*.Designer.cs", // Examine files with this mask
                Inspection.Required,  // This is a test for things that MUST be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonLocalizedFiles(), // Ignore violations in files or directories we don't localize
                string.Empty, // No file content required for inspection
                @".*(new global::System.Resources.ResourceManager|System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager).*", // Required pattern
                true, // Pattern is a regular expression
                @"ensures that every dialog is localizable"); // Explanation for requirement, appears in report

            // Looking for non-standard scaling in form designer
            AddTextInspection(@"*.resx", // Examine files with this mask
                Inspection.Required,  // This is a test for things that MUST be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null, // There are no parts of the codebase that should skip this check
                "<data name=\"$this.AutoScaleDimensions\" type=\"System.Drawing.SizeF, System.Drawing\">", // Only worry about it when this appears in file
                "<data name=\"$this.AutoScaleDimensions\" type=\"System.Drawing.SizeF, System.Drawing\">\n<value>6, 13</value>", // Required pattern (two lines)
                false, // Pattern is not a regular expression
                @"nonstandard {0} found instead"); // Explanation for requirement, appears in report

            // Looking for Model code depending on UI code
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null, // There are no parts of the codebase that should skip this check
                @"namespace pwiz.Skyline.Model", // If the file contains this, then check for forbidden pattern
                @".*using.*pwiz\.Skyline\.(Alerts|Controls|.*UI);.*", // Forbidden pattern
                true, // Pattern is a regular expression
                @"Skyline model code must not depend on UI code", // Explanation for prohibition, appears in report
                null, // No explicit exceptions to this rule
                13); // Number of existing known failures that we'll tolerate as warnings instead of errors, so no more get added while we wait to fix the rest

            // A few lines of fake tests that can be useful in development of this mechanism
            // AddInspection(@"*.Designer.cs", Inspection.Required, Level.Error, null, "Windows Form Designer generated code", @"DetectionsToolbar", @"fake, debug purposes only"); // Uncomment for debug purposes
            // AddInspection(@"*.cs", Inspection.Forbidden, Level.Error, null, string.Empty, @"DetectionsToolbar", @"fake, debug purposes only"); // Uncomment for debug purposes
            // AddInspection(@"*.xml", Inspection.Required, Level.Error, null, "xml",  @"encoding=""utf-8""", @"fake, debug purposes only"); // Uncomment for debug purposes

            PerformInspections();
        }

        private HashSet<string> TODOs = new HashSet<string>();

        /// <summary>
        /// Examine the CSV file containing the list that we use to conveniently summon run time images of forms in SkylineTester,
        /// especially for L10N development.  Look for forms that are not mentioned, and forms that are mentioned but no longer exist.
        /// </summary>
        List<string> CheckFormsWithoutTestRunnerLookups()
        {
            var lines = File.ReadAllLines(GetCodeBaseRoot(out _) + "\\TestRunnerLib\\TestRunnerFormLookup.csv");
            var missing = new List<string>();
            var declaredForms = new HashSet<string>();
            foreach (var line in lines)
            {
                // Pick out EditPeakScoringModelDlg.ModelTab from "EditPeakScoringModelDlg.ModelTab,TestPeakScoringModel"
                var parts = line.Split(',');
                var formName = parts[0].Trim();
                declaredForms.Add(formName);

                // Look for forms with no associated test, or acknowledged as TODO
                var testName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                if (string.IsNullOrEmpty(testName) || testName.StartsWith(@"TODO"))
                {
                    TODOs.Add(line);
                }
            }

            // Collect forms that should be exercised in tutorials
            var foundForms = FindForms(new[]
                {
                    typeof(Form),
                    typeof(FormEx),
                    typeof(DockableFormEx),
                    typeof(DataboundGridForm),
                    typeof(FoldChangeForm),
                    typeof(CommonFormEx),
                    typeof(ModeUIInvariantFormEx),
                    typeof(CreateHandleDebugBase),
                    typeof(GraphSummary.IController),
                    typeof(GraphSummary.IControllerSplit),
                    typeof(ResultsGridForm),
                },
                new[] // Classes that get used directly as well as in derived form
                {
                    typeof(DocumentGridForm)
                });
            // Forms that we don't expect to see in any test
            var FormNamesNotExpectedInTutorialTests = new[]
            {
                "DetectionsGraphController", // An intermediate type, actually exercised in DetectionsPlotTest
                "MassErrorGraphController", // An intermediate type, actually exercised in MassErrorGraphsTest 
                "PeptideSettingsUI.TabWithPage", // An intermediate type
                "BackgroundEventThreadsTestForm" // Used in tests itself, not a UI thing    
            };

            foreach (var formName in foundForms)
            {
                if (!declaredForms.Contains(formName))
                {
                    if (!FormNamesNotExpectedInTutorialTests.Contains(formName) &&
                        !declaredForms.Any(n => n.StartsWith(formName + ".")) && // Perhaps this is a parent to tab types
                        !declaredForms.Any(n => n.EndsWith("." + formName))) // Or perhaps lookup list declares parent.child
                    {
                        missing.Add(string.Format("Form \"{0}\" is not listed in TestRunnerLib\\TestRunnerFormLookup.csv", formName));
                    }
                }
            }

            // Sanity check - are there any names in TestRunnerLib\TestRunnerFormLookup.csv that we didn't find in forms search?
            foreach (var name in declaredForms)
            {
                if (!foundForms.Contains(name) && 
                    !FormNamesNotExpectedInTutorialTests.Contains(name) && // Known exclusion?
                    !foundForms.Any(f => name.EndsWith("." + f))) // Or perhaps lookup list declares parent.child
                {
                    missing.Add(string.Format("Form \"{0}\" referenced in TestRunnerLib\\TestRunnerFormLookup.csv is unknown or has unanticipated parent form type", name));
                }
            }

            return missing;
        }

        private static HashSet<string> FindForms(Type[] inUseFormTypes,
            Type[] directParentTypes) // Types directly referenced in addition to their derived types
        {
            var forms = new HashSet<string>();
            var formTypes = new HashSet<Type>(inUseFormTypes);
            foreach (var t in directParentTypes)
            {
                formTypes.Add(t);
            }

            foreach (var formType in formTypes)
            {
                // Now find all forms that inherit from formType
                var assembly = formType == typeof(Form) ? Assembly.GetAssembly(typeof(FormEx)) : Assembly.GetAssembly(formType);
                foreach (var form in assembly.GetTypes()
                    .Where(t => (t.IsClass && !t.IsAbstract && t.IsSubclassOf(formType)) || // Form type match
                                formType.IsAssignableFrom(t))) // Interface type match
                {
                    var formName = form.Name;
                    // Watch out for form types which are just derived from other form types (e.g FormEx -> ModeUIInvariantFormEx)
                    if (directParentTypes.Any(ft => Equals(formName, ft.Name)) ||
                        !formTypes.Any(ft => Equals(formName, ft.Name)))
                    {
                        forms.Add(formName);
                    }

                    // Look for tabs etc
                    var typeIFormView = typeof(IFormView);
                    foreach (var nested in form.GetNestedTypes().Where(t => typeIFormView.IsAssignableFrom(t)))
                    {
                        var nestedName = formName + "." + nested.Name;
                        forms.Add(nestedName);
                    }
                }
            }

            return forms;
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
            List<string> CheckForToleratedError(PatternDetails patternDetails, List<string> errors, List<string> warnings, Dictionary<PatternDetails, int> counts,
                 out string tolerated)
            {
                var result = patternDetails.FailureType == Level.Error ? errors : warnings;
                tolerated = null;
                if (patternDetails.FailureType == Level.Error && patternDetails.NumberOfToleratedIncidents > 0)
                {
                    // Track errors that are tolerated to a degree
                    if (!counts.ContainsKey(patternDetails))
                    {
                        counts.Add(patternDetails, 1);
                    }
                    else
                    {
                        counts[patternDetails] = counts[patternDetails] + 1;
                    }

                    if (counts[patternDetails] <= patternDetails.NumberOfToleratedIncidents)
                    {
                        result = warnings;
                        tolerated = @"This is an error, but is tolerated for the moment.";
                    }
                    else
                    {
                        tolerated =
                            @"A certain number of existing cases of this are tolerated for the moment, there appears to be a new one which must be corrected.";
                    }
                }

                return result;
            }

            var root = GetCodeBaseRoot(out var thisFile);
            if (!Directory.Exists(root))
            {
                return; // Don't fail, this might be an install with no code in it
            }

            var results = CheckFormsWithoutTestRunnerLookups();
            var errorCounts = new Dictionary<PatternDetails, int>();

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

                    var errors = new List<string>();
                    var warnings = new List<string>();
                    var multiLinePatternFaults = new Dictionary<Pattern, string>();
                    var multiLinePatternFaultLocations = new Dictionary<Pattern, int>();

                    foreach (var line in lines)
                    {
                        lineNum++;
                        if (forbiddenPatternsForThisFile != null)
                        {
                            foreach (var pattern in forbiddenPatternsForThisFile.Keys.Where(p => p.IsMatch(line)))
                            {
                                var parts = pattern.PatternString.Split('\n');
                                var matched = true;
                                // Watch for multi-line forbidden patterns
                                for (var i = 1; i < parts.Length; i++)
                                {
                                    if (!lines[lineNum + i - 1].Contains(parts[i].Trim()))
                                    {
                                        matched = false;
                                        break;
                                    }
                                }
                                if (matched)
                                {
                                    var patternDetails = forbiddenPatternsByFileMask[fileMask][pattern];
                                    var why = patternDetails.Reason;
                                    var result = CheckForToleratedError(patternDetails, errors, warnings, errorCounts, out var tolerated);
                                    result.Add(@"Found prohibited use of");
                                    result.Add(@"""" + pattern.PatternString.Replace("\n", "\\n") + @"""");
                                    result.Add("(" + why + ") at");
                                    result.Add(filename + "(" + lineNum + @")");
                                    result.Add(line);
                                    if (tolerated != null)
                                    {
                                        result.Add(tolerated);
                                    }
                                    result.Add(string.Empty);
                                }
                            }
                        }

                        if (requiredPatternsObservedInThisFile != null)
                        {
                            foreach (var pattern in requiredPatternsObservedInThisFile.Keys)
                            {
                                if (pattern.IsMatch(line))
                                {
                                    // Watch for multi-line patterns - we may match first line of pattern but not next, and we want to report that
                                    var parts = pattern.PatternString.Split('\n');
                                    requiredPatternsObservedInThisFile[pattern] = true;
                                    for (var i = 1; i < parts.Length; i++)
                                    {
                                        if (!lines[lineNum + i - 1].Contains(parts[i].Trim()))
                                        {
                                            multiLinePatternFaults[pattern] = lines[lineNum + i - 1].Trim();
                                            multiLinePatternFaultLocations[pattern] = lineNum + 1;
                                            requiredPatternsObservedInThisFile[pattern] = false;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    if (requiredPatternsObservedInThisFile != null)
                    {
                        foreach (var requirement in requiredPatternsObservedInThisFile.Where(p => !p.Value))
                        {
                            var pattern = requirement.Key;
                            multiLinePatternFaults.TryGetValue(pattern, out var fault);
                            var patternDetails = requiredPatternsByFileMask[fileMask][pattern];
                            var why = string.Format(patternDetails.Reason, fault ?? String.Empty);
                            var result = CheckForToleratedError(patternDetails, errors, warnings, errorCounts, out var tolerated);

                            result.Add(@"Did not find required use of");
                            result.Add(@"""" + pattern.PatternString.Replace("\n","\\n") + @"""");
                            if (multiLinePatternFaultLocations.TryGetValue(pattern, out var lineNumber))
                            {
                                result.Add("(" + why + ") at");
                                result.Add(filename + "(" + lineNumber + ")");
                            }
                            else
                            {
                                result.Add("(" + why + ") in");
                                result.Add(filename);
                            }

                            if (tolerated != null)
                            {
                                result.Add(tolerated);
                            }
                            result.Add(string.Empty);
                        }
                    }

                    if (errors.Any())
                    {
                        results.Add(string.Join(Environment.NewLine, errors));
                    }

                    if (warnings.Any())
                    {
                        Console.WriteLine();
                        Console.Write(@"WARNING: ");
                        foreach (var warning in warnings)
                        {
                            Console.WriteLine(warning);
                        }
                    }
                }
            }

            if (TODOs.Any())
            {
                Console.WriteLine();
                Console.WriteLine(@"WARNING: Found these TODO entries (forms acknowledged as not yet appearing in any test) in TestRunnerLib\TestRunnerFormLookup.csv:");
                foreach (var todo in TODOs)
                {
                    Console.WriteLine(todo);
                }
            }

            // Make sure that we tighten the restrictions as tolerated errors are resolved
            foreach (var toleratedError in errorCounts)
            {
                var pattern = toleratedError.Key;
                var incidents = toleratedError.Value;
                if (incidents < pattern.NumberOfToleratedIncidents)
                {
                    results.Add(string.Format("The inspection \"{0}\" is configured to tolerate {1} existing incidents, but only {2} were encountered. To prevent new incidents, the tolerance count must be reduced to {2} in CodeInspectionTest.cs",
                        pattern.Reason, pattern.NumberOfToleratedIncidents, incidents));
                }
            }

            if (results.Any())
            {
                var resultsCount = results.Count;
                results.Insert(0, string.Empty);
                results.Insert(0, string.Format("{0} code inspection failures found:", resultsCount));
                results.Add(string.Empty);
                results.Add(
                    "Help may be available on the Skyline developer Wiki at https://skyline.ms/wiki/home/development/page.view?name=Skyline%20Custom%20Code%20Inspections");
                AssertEx.Fail(string.Join(Environment.NewLine, results));
            }
        }

        public class Pattern
        {
            public string PatternString;
            public Regex RegExPattern;
            public string PatternExceptionString; // Pattern does not hit if the line includes this string

            public Pattern(string patternString, bool isRegEx, string patternExceptionString)
            {
                PatternString = patternString;
                RegExPattern = isRegEx ? new Regex(patternString, RegexOptions.CultureInvariant | RegexOptions.CultureInvariant) : null;
                PatternExceptionString = patternExceptionString;
            }

            public bool IsRegEx => RegExPattern != null;

            public bool IsMatch(string s)
            {
                if (RegExPattern?.IsMatch(s) ?? s.Contains(PatternString.Split('\n')[0].Trim())) // Watch for multiline string matches
                {
                    return string.IsNullOrEmpty(PatternExceptionString) || !s.Contains(PatternExceptionString);
                }

                return false;
            }
        }

        public class PatternDetails
        {
            public string Cue; // If non-empty, pattern only applies to files containing this cue
            public string Reason; // Note to show on failure
            public string[] IgnoredFileMasks; // Don't flag on hits in files that contain these strings in their full paths
            public Level FailureType;  // Is failure an error, or just a warning?
            public int NumberOfToleratedIncidents; // Some inspections we won't fix yet, but we don't want to see any new ones either

            public PatternDetails(string cue,string reason, string[] ignoredFileMasks, Level failureType, int numberOfToleratedIncidents) 
            {
                Cue = cue;
                Reason = reason;
                IgnoredFileMasks = ignoredFileMasks;
                FailureType = failureType;
                NumberOfToleratedIncidents = numberOfToleratedIncidents;
            }

            public bool IgnorePath(string path)
            {
                return string.IsNullOrEmpty(path) ||
                       IgnoredFileMasks != null && IgnoredFileMasks.Any(d => path.ToLowerInvariant().Contains(d.ToLowerInvariant()));
            }
        }
        private readonly Dictionary<string, Dictionary<Pattern, PatternDetails>> forbiddenPatternsByFileMask = new Dictionary<string, Dictionary<Pattern, PatternDetails>>();
        private readonly Dictionary<string, Dictionary<Pattern, PatternDetails>> requiredPatternsByFileMask = new Dictionary<string, Dictionary<Pattern, PatternDetails>>();

        private enum Inspection { Forbidden, Required }
        public enum Level { Warn, Error }

        private HashSet<string> allFileMasks = new HashSet<string>();

        // Return a list of directories that we don't care about from a strictly Skyline point of view
        private string[] NonSkylineDirectories()
        {
            return new[] {@"TestRunner", @"SkylineTester", @"SkylineNightly", "Executables", "CommonTest" };
        }

        // Prepare a list of files that we never need to deal with for L10N
        // Uses the information found in our KeepResx L10N development tool,
        // along with a hardcoded list herein.

        private string[] NonLocalizedFiles()
        {
            var root = GetCodeBaseRoot(out var thisFile);
            if (string.IsNullOrEmpty(root) || !File.Exists(root + "\\Executables\\KeepResx\\Program.cs"))
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
            result.Add("Executables\\Tools\\ExampleInteractiveTool");
            return result.ToArray();
        }


        void AddTextInspection(string fileMask,  // Which files?
            Inspection inspectionType, // Required, or forbidden?
            Level failureType, // Is a failure an error, or a warning
            string[] ignoredDirectories, // Areas to disregard
            string cue, // If non-empty, only perform the inspection if file contains this cue
            string pattern,  // What we're looking out for (may contain \n)
            bool isRegEx, // Is the pattern a regular expression?
            string reason, // Explanation on failure
            string patternException = null, // Optional string which exempts a pattern match if found in matching line
            int numberToleratedAsWarnings = 0) // Some inspections we won't fix yet, but we don't want to see any new ones either
        {
            allFileMasks.Add(fileMask);
            var rules = inspectionType == Inspection.Forbidden ? forbiddenPatternsByFileMask : requiredPatternsByFileMask;
            if (!rules.ContainsKey(fileMask))
            {
                rules.Add(fileMask, new Dictionary<Pattern, PatternDetails>());
            }
            var patterns = rules[fileMask];
            patterns.Add(new Pattern(pattern, isRegEx, patternException), new PatternDetails(cue, reason, ignoredDirectories, failureType, numberToleratedAsWarnings));
        }
    }
}

