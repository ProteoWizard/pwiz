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
// Performs custom code inspections that ReSharper can't, particularly in generated code which ReSharper ignores by design.
// 


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using AssortResources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
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
            const string runDlgOkDlgExemptionComment = @"// Purposely using RunUI instead of OkDialog here";
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null,  // There are no parts of the codebase that should skip this check
                "AbstractFunctionalTest", // Only files containing this string get inspected for this
                @"RunUI\(.*(Ok|Cancel)Dialog[^_].*", // Forbidden pattern - match RunUI(()=>foo.OkDialog()), RunUI(foo.CancelDialog) etc
                true, // Pattern is a regular expression
                @"use OkDialog() or CancelDialog() instead of RunUI() to close dialogs in a test - this waits for the dialog to actually close, which avoids race conditions e.g. ""OkDialog(colDlg, colDlg.CancelDialog)"" instead of ""RunUI(() => colDlg.CancelDialog())"". If this really is a legitimate use (to test error handling etc) add this comment to the offending line: '" + runDlgOkDlgExemptionComment + @"'", // Explanation for prohibition, appears in report
                runDlgOkDlgExemptionComment); // There are one or two legitimate uses of this, look for this comment and ignore the violation when found

            // Looking for uses of MessageBox where we should really be using MessageDlg
            const string messageBoxExemptionComment = @"// Purposely using MessageBox here";
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonSkylineDirectories(), // We only care about this in Skyline code
                string.Empty, // No file content required for inspection
                @"MessageBox.Show", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"use MessageDlg.Show instead - this ensures proper interaction with automated tests, small molecule interface operation, and other enhancements. If this really is a legitimate use add this comment to the offending line: '" + messageBoxExemptionComment + @"'", // Explanation for prohibition, appears in report
                messageBoxExemptionComment); // There are one or two legitimate uses of this, look for this comment and ignore the violation when found

            // Looking for forgotten PauseTest() calls that will mess up automated tests
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                new[] { @"TestFunctional.cs" }, // Only these files should contain this
                string.Empty, // No file content required for inspection
                @"^\s*PauseTest(UI)?\(", // Forbidden pattern (uncommented PauseTest or PauseTestUI)
                true, // Pattern is a regular expression
                @"This appears to be temporary debugging code that should not be checked in. Or perhaps you meant to use PauseForManualTutorialStep()?"); // Explanation for prohibition, appears in report

            // Looking for forgotten SourceLevel.Information use that writes debug messages to console/ImmediateWindow
            AddTextInspection(@"TraceWarningListener.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null, // Only these files should contain this
                string.Empty, // No file content required for inspection
                @"SourceLevels.Information", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"This appears to be temporary debugging code that should not be checked in. Normally we don't want users to see this level of Trace messages."); // Explanation for prohibition, appears in report

            // Looking for bare use of Trace calls that should be UserMessage or DebugMessage calls
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonSkylineDirectories().Append(@"Messages.cs").ToArray(), // Only these files should contain this
                string.Empty, // No file content required for inspection
                @"Trace.Trace", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"Trace should not be used directly. The Messages class is the proper way to produce non-blocking user-facing messages, and permanent dev-facing messages."); // Explanation for prohibition, appears in report

            // Looking for forgotten "RunPerfTests=true" statements that will force running possibly unintended tests
            AddTextInspection(@"*.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null,  // There are no parts of the codebase that should skip this check
                string.Empty, // No file content required for inspection
                @"^\s*RunPerfTests\s*\=\s*true", // Forbidden pattern (uncommented enabling of perftests in IDE)
                true, // Pattern is a regular expression
                @"This appears to be temporary debugging code that should not be checked in. PerfTests are normally enabled/disabled by the automated test framework."); // Explanation for prohibition, appears in report

            // Looking for non-standard image scaling
            AddTextInspection(@"*.Designer.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                null, // There are no parts of the codebase that should skip this check
                string.Empty, // No file content required for inspection
                @".ImageScalingSize = new System.Drawing.Size(", // Forbidden pattern
                false, // Pattern is not a regular expression
                @"causes blurry icon issues on HD monitors"); // Explanation for prohibition, appears in report

            // Looking for forms that haven't been declared localizable (those that have won't have direct assignments to Text in designer files)
            AddTextInspection(@"*.Designer.cs", // Examine files with this mask
                Inspection.Forbidden, // This is a test for things that should NOT be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonLocalizedFiles(), // Ignore violations in files or directories we don't localize
                string.Empty, // No file content required for inspection
                new[] { ".Text = \"", ".HeaderText = \"" }, // Forbidden patterns
                false, // Patterns are not regular expressions
                @"form should be declared localizable"); // Explanation for prohibition, appears in report

            // Looking for unlocalized dialogs
            AddTextInspection(@"*.Designer.cs", // Examine files with this mask
                Inspection.Required,  // This is a test for things that MUST be in such files
                Level.Error, // Any failure is treated as an error, and overall test fails
                NonLocalizedFiles(), // Ignore violations in files or directories we don't localize
                string.Empty, // No file content required for inspection
                @".*(new global::System.Resources.ResourceManager|new System.Resources.ResourceManager|System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager).*", // Required pattern
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
            void AddForbiddenUIInspection(string fileMask, string cue, string why, int numberToleratedAsWarnings = 0)
            {
                AddTextInspection(fileMask, // Examine files with this mask
                    Inspection.Forbidden, // This is a test for things that should NOT be in such files
                    Level.Error, // Any failure is treated as an error, and overall test fails
                    null, // There are no parts of the codebase that should skip this check
                    cue, // If the file contains this, then check for forbidden pattern
                    @"using.*(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)", 
                    true, // Pattern is a regular expression
                    why, // Explanation for prohibition, appears in report
                    null, // No explicit exceptions to this rule
                    numberToleratedAsWarnings); // Number of existing known failures that we'll tolerate as warnings instead of errors, so no more get added while we wait to fix the rest
            }

            AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model", @"Skyline model code must not depend on UI code", 37);
            // Looking for CommandLine.cs and CommandArgs.cs code depending on UI code
            AddForbiddenUIInspection(@"CommandLine.cs", @"namespace pwiz.Skyline", @"CommandLine code must not depend on UI code", 2);
            AddForbiddenUIInspection(@"CommandArgs.cs", @"namespace pwiz.Skyline", @"CommandArgs code must not depend on UI code");

            // Check for using DataGridView.
            AddTextInspection("*.designer.cs", Inspection.Forbidden, Level.Error, NonSkylineDirectories(), null,
                "new System.Windows.Forms.DataGridView()", false,
                "Must use subclass CommonDataGridView or DataGridViewEx instead of DataGridView.");

            AddTextInspection("*.cs", Inspection.Forbidden, Level.Error,
                new[] {"TestFunctional", "TestTutorial", "TestPerf", "Executables", "UtilUIExtra.cs", "ClipboardEx.cs"}, 
                null, "Clipboard(Ex)?\\.SetText", true, 
                "Use ClipboardHelper.SetClipboardText instead since it handles exceptions");

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

            var panoramaHint = typeof(PanoramaClient.PanoramaFolderBrowser); // Bit of a hack to get the test to look in that namespace

            // Collect forms that should be exercised in tutorials
            var foundForms = FindForms(new[]
                {
                    panoramaHint, // Bit of a hack to get the test to look in that namespace
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
                panoramaHint.Name, // Bit of a hack to get the test to look in that namespace
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
                    missing.Add(string.Format("Form \"{0}\" referenced in TestRunnerLib\\TestRunnerFormLookup.csv is unknown or has unanticipated parent form type (maybe using Form instead of FormEx or CommonFormEx?)", name));
                }
            }

            return missing;
        }

        /// <summary>
        /// We have some code, especially in commandline, that expects certain error messages to start with CommandStatusWriter.ERROR_MESSAGE_HINT (i.e. "Error:") or the L10N equivalent
        /// </summary>
        void InspectConsistentErrorMessages(List<string> errors)
        {
            var currentCulture = Thread.CurrentThread.CurrentUICulture; // Preserve current test culture
            try
            {
                Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("en"); // We want to compare against the "en" resources
                var resourceSetEnglish = Skyline.Properties.Resources.ResourceManager.GetResourceSet(Thread.CurrentThread.CurrentUICulture, true, true);

                // Before we proceed, make sure that the hardcoded english language hint still agrees with the englsh language resource
                AssertEx.IsTrue(Skyline.Properties.Resources.CommandStatusWriter_WriteLine_Error_.StartsWith(CommandStatusWriter.ERROR_MESSAGE_HINT));

                // Now work through each resource, checking for L10N consistency of strings that, in English, start with CommandStatusWriter.ERROR_MESSAGE_HINT (i.e. "Error:")
                foreach (var resource in resourceSetEnglish)
                {
                    var pair = resource as DictionaryEntry? ?? new DictionaryEntry(); // For strings, resource object is a pair [resource name, L10N string]
                    var englishString = pair.Value as string ?? string.Empty;
                    if (englishString.StartsWith(CommandStatusWriter.ERROR_MESSAGE_HINT))
                    {
                        var resourceName = pair.Key.ToString();
                        // Now work through the other supported L10N languages, verifying that the L10N string is also properly marked as an error hint.
                        // That is, either starts with localized Skyline.Properties.Resources.CommandStatusWriter_WriteLine_Error_, or starts with
                        // the english language string constant CommandStatusWriter.ERROR_MESSAGE_HINT (i.e. hasn't been localized yet).
                        foreach (var culture in new[] {@"zh-CHS", @"ja"}) 
                        {
                            var tryCulture = new CultureInfo(culture);
                            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = tryCulture;
                            var commandStatusWriterWriteLineError = Skyline.Properties.Resources.CommandStatusWriter_WriteLine_Error_;
                            var localized = Skyline.Properties.Resources.ResourceManager.GetString(resourceName) ?? String.Empty;
                            if (!localized.StartsWith(commandStatusWriterWriteLineError, StringComparison.CurrentCulture) &&
                                !localized.StartsWith(CommandStatusWriter.ERROR_MESSAGE_HINT, StringComparison.InvariantCulture)) // Maybe not yet localized
                            {
                                // Report mismatch
                                errors.Add(string.Format("The {0} language version of resource string {1} does not begin with the localized version of \"{2}\" (see Skyline.Properties.Resources.CommandStatusWriter_WriteLine_Error_)", 
                                    culture, resourceName, CommandStatusWriter.ERROR_MESSAGE_HINT));
                            }
                        }
                        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = currentCulture;
                    }
                }
            }

            finally
            {
                Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = currentCulture;
            }
        }

        /// <summary>
        /// Just a quick smoke test to remind devs to add audit log tests where needed, and to catch cases where its obvious that one or
        /// more languages need updating because line counts don't agree
        /// </summary>
        void InspectTutorialAuditLogs(string root, List<string> errors)
        {
            var logsDir = Path.Combine(root, @"TestTutorial", @"TutorialAuditLogs");
            var logs = Directory.GetFiles(logsDir, "*.log", SearchOption.AllDirectories).ToList();
            var languages = logs.Select(l => l.Replace(logsDir, string.Empty).Split(Path.DirectorySeparatorChar)[1]).Distinct().ToList();
            var tests = logs.Select(l => l.Replace(logsDir, string.Empty).Split(Path.DirectorySeparatorChar)[2]).Distinct().ToList();
            foreach (var test in tests)
            {
                var results = new List<string>();
                foreach (var language in languages)
                {
                    var lPath = Path.Combine(logsDir, language, test);
                    if (!logs.Contains(lPath))
                    {
                        results.Add(string.Format("Did not find {0} version. This needs to be created and added to source control.", language));
                    }
                }

                var english = @"en";
                var enVersion = Path.Combine(logsDir, english, test);
                if (File.Exists(enVersion))
                {
                    var lines = File.ReadAllLines(enVersion);
                    var badLang = new List<string>();
                    foreach (var language in languages.Where(l => l != english))
                    {
                        var l10nVersion = Path.Combine(logsDir, language, test);
                        if (!File.Exists(l10nVersion))
                        {
                            continue; // Already noted
                        }

                        var l10nLines = File.ReadAllLines(l10nVersion);
                        if (lines.Length != l10nLines.Length)
                        {
                            badLang.Add(language);
                        }
                    }

                    if (badLang.Any())
                    {
                        results.Add(string.Format(
                            @"Line count for {0} does not match {1}. Tutorial audit logs should be regenerated.",
                            english, string.Join(@", ", badLang)));
                    }
                }

                if (results.Any())
                {
                    errors.Add(string.Format(@"{0} Error: {1}", test, string.Join(@", ", results)));
                }
            }
        }

        /// <summary>
        ///  Look for conflicts between settings.settings and app.config
        /// </summary>
        void InspectInconsistentSetting(string root, List<string> errors) 
        {
            var EMPTY_STRING_ARRAY = "</ArrayOfString>";

            // Extract key-value pairs from settings.settings
            var settingsValues = ExtractSettingsSettings();

            // Extract key-value pairs from app.config
            var configValues = ExtractAppConfigSettings();

            // Find keys that exist in both but have different values
            foreach (var key in settingsValues.Keys.Intersect(configValues.Keys).Where(k => (settingsValues[k] != configValues[k])))
            {
                var settingsValue = settingsValues[key];
                var configValue = configValues[key];
                // An empty string array in settings.settings is just empty text in app.config
                if (!(settingsValue.Equals(EMPTY_STRING_ARRAY) && string.IsNullOrEmpty(configValue)))
                {
                    NoteMismatch(key, settingsValue, configValue);
                }
            }


            // Find settings that exist only in one of the files
            foreach (var key in settingsValues.Keys.Except(configValues.Keys))
            {
                var settingsValue = settingsValues[key];
                // IDE seems to be OK with missing value in app.config if the setting is an empty string array
                if (!settingsValue.Equals(EMPTY_STRING_ARRAY))
                {
                    NoteMismatch(key, settingsValue, null);
                }
            }
            foreach (var key in configValues.Keys.Except(settingsValues.Keys))
            {
                NoteMismatch(key, null, configValues[key]);
            }

            return;


            void NoteMismatch(string name, string settingsValue, string configValue)
            {
                errors.Add($"Settings mismatch for \"{name}\": {FormatValue(settingsValue)} in Settings.settings, {FormatValue(configValue)} in app.config.");
                return;
                string FormatValue(string s) => s == null ? "not found" : $"\"{s}\"";
            }

            string CheckStringArray(string value)
            {
                if (value.Contains("<ArrayOfString"))
                {
                    var result = string.Empty;
                    foreach (var part in value.Split(new[] { "<string>" }, StringSplitOptions.None))
                    {
                        if (part.Contains("</string>"))
                        {
                            var val = part.Trim().Split(new[] { "</string>" }, StringSplitOptions.None).FirstOrDefault();
                            if (!string.IsNullOrEmpty(val))
                            {
                                result += val.Trim();
                            }
                        }
                    }
                    return result;
                }
                else if (string.IsNullOrEmpty(value))
                {
                    // Special case - settings designer won't update app.config if the value is empty (that is, not even an empty description in XML - just blank text)
                    value = EMPTY_STRING_ARRAY;
                }

                return value;
            }

            Dictionary<string, string> ExtractSettingsSettings()
            {
                var settingsPath = Path.Combine(root, @"Properties\Settings.settings");
                var doc = XDocument.Load(settingsPath);
                var settings = new Dictionary<string, string>();
                XNamespace settingsNamespace = "http://schemas.microsoft.com/VisualStudio/2004/01/settings";
                var settingsElements = doc.Descendants(settingsNamespace + "Settings")
                    .Descendants(settingsNamespace + "Setting")
                    .ToArray();
                AssertEx.AreNotEqual(settingsElements.Length, 0, "trouble reading settings.settings");
                foreach (var setting in settingsElements)
                {
                    var key = setting.Attribute("Name")?.Value;
                    if (key != null)
                    {
                        var value = CheckStringArray(setting.Value);
                        settings[key] = value;
                    }
                }
                return settings;
            }

            Dictionary<string, string> ExtractAppConfigSettings()
            {
                var configPath = Path.Combine(root, @"app.config");
                var doc = XDocument.Load(configPath);
                var settings = new Dictionary<string, string>();
                var appSettingsElements = doc.Descendants(@"setting").ToArray();
                AssertEx.AreNotEqual(appSettingsElements.Length, 0, "trouble reading app.config");
                foreach (var setting in appSettingsElements)
                {
                    var key = setting.Attribute("name")?.Value;
                    if (key != null)
                    {
                        var value = setting.Value;
                        settings[key] = value;
                    }
                }
                return settings;
            }
        }

        /// <summary>
        /// Look for strings which have been localized but not moved from main Resources.resx to more appropriate locations 
        /// </summary>
        void InspectMisplacedResources(string root, List<string> errors) 
        {
            // Look for any .csproj in the immediate subfolder of the main project
            // Strings which are referenced by these other .csproj files will not get moved
            var otherProjectPaths = new List<string>();
            // ReSharper disable once AssignNullToNotNullAttribute
            foreach (var subfolder in Directory.GetDirectories(root))
            {
                var otherProjectPath = Path.Combine(subfolder, Path.GetFileNameWithoutExtension(subfolder) + ".csproj");
                if (File.Exists(otherProjectPath))
                {
                    otherProjectPaths.Add(otherProjectPath);
                }
            }

            var resourceFilePath = Path.Combine(root, "Properties\\Resources.resx");
            var csProjPath = Path.Combine(root, "Skyline.csproj");
            var resourceAssorter = new ResourceAssorter(csProjPath, resourceFilePath, true, otherProjectPaths.ToArray());
            var initialErrors = errors.Count;
            resourceAssorter.DoWork(errors);
            if (errors.Count > initialErrors)
            {
                var exePath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)??string.Empty, "AssortResources.exe");

                errors.Add($"\nThis can be done with command:\n\"{exePath}\" --resourcefile \"{resourceFilePath}\" --projectfile \"{csProjPath}\"\nBefore running this command, save all your changes in the IDE.");
            }
        }


        // Looking for uses of Form where we should really be using FormEx
        private static void FindIllegalForms(List<string> results) // Looks for uses of Form rather than FormEx
        {
            var bareForms = new HashSet<Type>();

            // List of classes which actually do inherit directly from Form
            var acceptableDirectUsesOfFormClass = new[]
            {
                typeof(FormEx),
                typeof(CommonFormEx),
                typeof(DockableFormEx),
                typeof(PauseAndContinueForm),
            };

            try
            {

                var assembly = Assembly.GetAssembly(typeof(FormEx));
                var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Form)) && 
                                                           !acceptableDirectUsesOfFormClass.Any(t.IsSubclassOf) &&
                                                           !acceptableDirectUsesOfFormClass.Any(t.Equals) &&
                                                           t.FullName != null && !t.FullName.StartsWith("System"));
                foreach (var type in types)
                {
                    bareForms.Add(type);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                var errMessage = new StringBuilder();
                errMessage.AppendLine("Error in FindIllegalForms");
                errMessage.AppendLine(ex.StackTrace);
                errMessage.AppendLine();
                errMessage.AppendLine(string.Format(ex.Message));
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    errMessage.AppendLine();
                    errMessage.AppendLine(loaderException.Message);
                }
                Console.WriteLine(errMessage);
                throw new Exception(errMessage.ToString(), ex);
            }

            results.AddRange(bareForms.Select(bareForm => $@"Error: class {bareForm.FullName} illegally inherits directly from Form instead of FormEx. Using FormEx ensures proper interaction with automated tests, small molecule interface operation, and other enhancements. If this really is intentional, add ""typeof({bareForm.Name})"" to the variable ""acceptableDirectUsesOfFormClass"" in method ""FindIllegalForms"" in CodeInspectionTest.cs"));
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

            try
            {
                foreach (var formType in formTypes)
                {
                    // Now find all forms that inherit from formType
                    // Search for forms in the same assembly as the base class with a few exceptions
                    Assembly assembly;
                    if (formType == typeof(Form))
                    {
                        assembly = typeof(SkylineWindow).Assembly;
                    }
                    else if (formType == typeof(CommonFormEx))
                    {
                        assembly = typeof(ViewEditor).Assembly;
                    }
                    else
                    {
                        assembly = formType.Assembly;
                    }
                    foreach (var form in assembly.GetTypes()
                                 .Where(t => (t.IsClass && !t.IsAbstract && 
                                              (t.IsSubclassOf(formType) || // Form type match
                                               t.IsSubclassOf(typeof(FormEx)) || t.IsSubclassOf(typeof(CommonFormEx)))) // Or acceptably subclassed Form
                                             || formType.IsAssignableFrom(t))) // Interface type match
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
            }
            catch (ReflectionTypeLoadException ex)
            {
                var errMessage = new StringBuilder();
                errMessage.AppendLine("Error in FindForms");
                errMessage.AppendLine("(Perhaps Visual Studio menu item \"Test | Configure Run Settings | Select Solution Wide runsettings File\" is not set to \"TestSettings_x64.runsettings\"?)");
                errMessage.AppendLine(ex.StackTrace);
                errMessage.AppendLine();
                errMessage.AppendLine(string.Format(ex.Message));
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    errMessage.AppendLine();
                    errMessage.AppendLine(loaderException.Message);
                }
                Console.WriteLine(errMessage);
                throw new Exception(errMessage.ToString(), ex);
            }

            return forms;
        }

        private string GetCodeBaseRoot(out string thisFile)
        {
            thisFile = new StackTrace(true).GetFrame(0).GetFileName();
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

            // Looking for uses of Form where we should really be using FormEx
            FindIllegalForms(results);

            // Make sure that anything that should start with the L10N equivalent of CommandStatusWriter.ERROR_MESSAGE_HINT (i.e. "Error:") does so
            InspectConsistentErrorMessages(results);

            InspectTutorialAuditLogs(root, results);

            InspectMisplacedResources(root, results); // Look for strings which have been localized but not moved from main Resources.resx to more appropriate locations

            InspectInconsistentSetting(root, results); // Look for conflicts between settings.settings and app.config

            var errorCounts = new Dictionary<PatternDetails, int>();

            foreach (var fileMask in allFileMasks)
            {
                var filenames = Directory.GetFiles(root, fileMask, SearchOption.AllDirectories).ToList();
                filenames.AddRange(Directory.GetFiles(Path.Combine(root, @"..", @"Shared", @"Common"), fileMask, SearchOption.AllDirectories));

                foreach (var filename in filenames)
                {
                    if (Equals(filename, thisFile))
                    {
                        continue; // Can't inspect yourself!
                    }

                    var content = File.ReadAllText(filename);
                    var lines = content.Split('\n');

                    var lineNum = 0;
                    var requiredPatternsObservedInThisFile = requiredPatternsByFileMask.TryGetValue(fileMask, out var value)
                        ? value.Where(kvp =>
                        {
                            // Do we need to worry about this pattern for this file?
                            var patternDetails = kvp.Value;
                            return !patternDetails.IgnorePath(filename) &&
                                   (string.IsNullOrEmpty(patternDetails.Cue) // No requirements for file content
                                    || lines.Any(l => l.Contains(patternDetails.Cue))); // File contains required cue
                        }).ToDictionary(k => k.Key, k => false)
                        : null;
                    var forbiddenPatternsForThisFile =
                        forbiddenPatternsByFileMask.TryGetValue(fileMask, out var patterns) // Are there any forbidden patterns for this filemask?
                            ? patterns.Where(kvp =>
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
                    var crlfCount = 0; // Look for inconsistent line endings

                    foreach (var line in lines)
                    {
                        // Look for inconsistent line endings
                        if (line.EndsWith("\r")) 
                        {
                            crlfCount++;
                        }
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

                    if (crlfCount != 0 && crlfCount < lines.Length-1)
                    {
                        results.Add($@"Inconsistent line endings in {filename}");
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
                        var previousWarning = string.Empty;
                        foreach (var warning in warnings)
                        {
                            if (string.IsNullOrEmpty(previousWarning))
                            {
                                Console.Write(@"WARNING: ");
                            }
                            Console.WriteLine(previousWarning = warning);
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
                    results.Add(string.Format("The inspection \"{0}\" is configured to tolerate exactly {1} existing incidents, but only {2} were encountered. To prevent new incidents, the tolerance count must be set to {2} in CodeInspectionTest.cs",
                        pattern.Reason, pattern.NumberOfToleratedIncidents, incidents));
                }
                patternsWithToleranceCounts.Remove(pattern); // This has been noted
            }
            results.AddRange(patternsWithToleranceCounts.Select(pattern => $"The inspection \"{pattern.Reason}\" is configured to tolerate exactly {pattern.NumberOfToleratedIncidents} existing incidents, but none were encountered. To prevent new incidents, the tolerance count must be removed in CodeInspectionTest.cs"));

            if (results.Any())
            {
                var commentCues = new[] // Things that may appear in error list that are not themselves errors
                {
                    "non-shared resource(s) should be moved from", 
                    "This can be done with command", 
                    "AssortResources.exe"
                };

                var resultsCount = results.Count(r => !string.IsNullOrEmpty(r) && !commentCues.Any(r.Contains));
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
                RegExPattern = isRegEx ? new Regex(patternString, RegexOptions.CultureInvariant | RegexOptions.CultureInvariant | RegexOptions.Compiled) : null;
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
        private readonly HashSet<PatternDetails> patternsWithToleranceCounts = new HashSet<PatternDetails>();

        private enum Inspection { Forbidden, Required }
        public enum Level { Warn, Error }

        private HashSet<string> allFileMasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            result.Add("MsGraphExtension.designer.cs");
            result.Add("QuantificationStrings.Designer.cs");
            result.Add("ColorGrid.Designer.cs");
            result.Add("ColumnCaptions.Designer.cs");
            result.Add("ColumnToolTips.Designer.cs");
            result.Add("FormulaBox.Designer.cs"); // Has special handling for L10N in FormulaBox.cs
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

        void AddTextInspection(string fileMask, // Which files?
            Inspection inspectionType, // Required, or forbidden?
            Level failureType, // Is a failure an error, or a warning
            string[] ignoredDirectories, // Areas to disregard
            string cue, // If non-empty, only perform the inspection if file contains this cue
            string[] patterns, // What we're looking out for (may contain \n)
            bool isRegEx, // Is the pattern a regular expression?
            string reason, // Explanation on failure
            string patternException = null, // Optional string which exempts a pattern match if found in matching line
            int numberToleratedAsWarnings = 0) // Some inspections we won't fix yet, but we don't want to see any new ones either
        {
            foreach (var pattern in patterns)
            {
                AddTextInspection(fileMask, inspectionType, failureType, ignoredDirectories, cue, pattern, isRegEx, reason, patternException, numberToleratedAsWarnings);
            }
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
            var patternDetails = new PatternDetails(cue, reason, ignoredDirectories, failureType, numberToleratedAsWarnings);
            patterns.Add(new Pattern(pattern, isRegEx, patternException), patternDetails);
            if (numberToleratedAsWarnings > 0)
            {
                patternsWithToleranceCounts.Add(patternDetails); // Track these so we know when more are tolerated than necessary
            }
        }
    }
}

