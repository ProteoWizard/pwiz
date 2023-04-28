/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Tools
{

    // A common interface for returning the relevant string for Tool Macros
    public interface IToolMacroProvider
    {
        string DocumentFilePath { get; }
        string SelectedProteinName { get; }
        string SelectedPeptideSequence { get; }       
        string SelectedPrecursor { get; }
        string ResultNameCurrent { get; }
        string FindProgramPath(ProgramPathContainer programPathContainer);
    }

    [XmlRoot("ToolDescription")]
    public class ToolDescription : IXmlSerializable, IKeyContainer<string>
    {
        public const string EXT_INSTALL = ".zip";

        public static readonly ToolDescription EMPTY = new ToolDescription(string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty);

        public static bool IsWebPageCommand(string command)
        {
            return command.StartsWith(@"http:") || command.StartsWith(@"https:");
        }

        /// <summary>
        /// Supported extensions
        /// <para>Changes to this array require corresponding changes to the FileDialogFiltersAll call below</para>
        /// </summary>
        public static readonly string[] EXTENSIONS = { @".exe", @".com", @".pif", @".cmd", @".bat", @".py", @".pl" };

        public static bool CheckExtension(string path)
        {
            // Avoid Path.GetExtension() because it throws an exception for an invalid path
            return EXTENSIONS.Any(extension => PathEx.HasExtension(path, extension));
        }

        public ToolDescription(ToolDescription t)
            : this(t.Title, t.Command, t.Arguments, t.InitialDirectory, t.OutputToImmediateWindow, t.ReportTitle, t.ArgsCollectorDllPath, t.ArgsCollectorClassName, t.ToolDirPath, t.Annotations, t.PackageVersion, t.PackageIdentifier, t.PackageName, t.UpdateAvailable )
        {            
        }

        public ToolDescription(string title, string command, string reportTitle)
            : this(title, command, string.Empty, string.Empty, false, reportTitle)
        {
        }

        public ToolDescription(string title, string command, string arguments, string initialDirectory,
                               bool outputToImmediateWindow, string reportTitle)
            : this( title,  command,  arguments,  initialDirectory,
                                outputToImmediateWindow,  reportTitle, string.Empty, string.Empty, null,new List<AnnotationDef>(), null, null, null)
        {            
        }

        public ToolDescription(string title, string command, string arguments, string initialDirectory,
                               bool outputToImmediateWindow, string reportTitle, string argsCollectorDllPath, 
                               string argsCollectorClassName, string toolDirPath, List<AnnotationDef> annotations, string packageVersion, string packageIdentifier, string packageName, bool updateAvailable = false )
        {
            Title = title;
            Command = command;
            Arguments = arguments ?? string.Empty;
            InitialDirectory = initialDirectory ?? string.Empty;
            OutputToImmediateWindow = outputToImmediateWindow;
            ReportTitle = reportTitle;
            ArgsCollectorDllPath = argsCollectorDllPath;
            ArgsCollectorClassName = argsCollectorClassName;
            ToolDirPath = toolDirPath;
            Annotations = annotations;
            PackageVersion = packageVersion;
            PackageIdentifier = packageIdentifier;
            PackageName = packageName;
            UpdateAvailable = updateAvailable;

            //Validate();  //Not immutable
        }

        public string GetKey()
        {
            return Title;
        }

        // CONSIDER: These properties get changed without being validated, which could result in an invalid ToolDescription
        public string Title { get; set; }
        public string Command { get; set; }
        public string Arguments { get; set; }
        public string InitialDirectory { get; set; }
        public bool OutputToImmediateWindow { get; set; }
        public string ReportTitle { get; set; }
        public string ArgsCollectorDllPath { get; set; }
        public string ArgsCollectorClassName { get; set; }
        public string ToolDirPath { get; set; }
        public List<AnnotationDef> Annotations { get; set; }
        public string PackageVersion { get; set; }
        public string PackageIdentifier { get; set; }
        public string PackageName { get; set; }
        public bool UpdateAvailable { get; set; }

        public bool IsWebPage { get { return IsWebPageCommand(Command); } }

        // The most recent command line arguments generated by the tool
        private string _lastCommandLineArgsGenerated;

        public string PreviousCommandLineArgs
        {
            get
            {
                lock (this)
                {
                    return _lastCommandLineArgsGenerated;
                }
            }
            set
            {
                lock (this)
                {
                    _lastCommandLineArgsGenerated = value;
                }
            }
        }

        public string GetUrl(SrmDocument doc, IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor)
        {
            if (!IsWebPage)
                return null;
            string url = Command;
            const string querySep = "?";
            const string paramSep = "&";
            if (!string.IsNullOrEmpty(Arguments))
            {
                string query = GetArguments(doc, toolMacroProvider, progressMonitor);
                if (query == null)
                    return null;

                url += (!url.Contains(querySep) ? querySep : paramSep) + query;
            }
            return url;
        }

        /// <summary>
        ///  Return a string that is the Arguments string with the macros replaced.
        /// </summary>
        /// <param name="doc"> Document for report data. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <param name="progressMonitor">Progress monitor. </param>
        /// <returns> Arguments with macros replaced or null if one of the macros was missing 
        /// (eg. no selected peptide for $(SelPeptide) then the return value is null </returns>
        public string GetArguments(SrmDocument doc, IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor)
        {
            return ToolMacros.ReplaceMacrosArguments(doc, toolMacroProvider, this, progressMonitor);
        }

        /// <summary>
        ///  Return a string that is the InitialDirectory string with the macros replaced.
        /// </summary>
        /// <param name="doc"> Document for report data. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <param name="progressMonitor">Progress monitor. </param>
        /// <returns> InitialDirectory with macros replaced or null if one of the macros was missing 
        /// (eg. no document for $(DocumentDir) then the return value is null </returns>
        public string GetInitialDirectory(SrmDocument doc, IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor)
        {
            return ToolMacros.ReplaceMacrosInitialDirectory(doc, toolMacroProvider, this, progressMonitor);
        }

        public string GetCommand(SrmDocument doc, IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor)
        {
            return ToolMacros.ReplaceMacrosCommand(doc, toolMacroProvider, this, progressMonitor);            
        }
        

        public static bool EquivalentAnnotations(AnnotationDef existingAnnotation, AnnotationDef annotationDef)
        {
            // if both annotations are value lists, we want to ignore the values in their lists in the comparison
            if (existingAnnotation.Type.Equals(AnnotationDef.AnnotationType.value_list) &&
                annotationDef.Type.Equals(AnnotationDef.AnnotationType.value_list))
            {
                return Equals(existingAnnotation.ChangeItems(new string[0]),
                              annotationDef.ChangeItems(new string[0]));
            }

            return existingAnnotation.Equals(annotationDef);
        }

        public IWebHelpers WebHelpers { get; set; }
        public string ReportTempPath_toDelete { get; set; }

        #region Implementation of IXmlSerializable

        private  ToolDescription()
        {
        }

        public static ToolDescription Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ToolDescription());
        }

        private enum ATTR
        {
            title,
            command,
            arguments,
            initial_directory,
            output_to_immediate_window,
            report_title,
            argument_generated,
            argscollector_dll_path,
            argscollector_class_name,
            tool_dir_path,
            package_version,
            package_identifier,
            package_name,
            update_available
        }
        
        private enum EL
        {
            annotation
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(Title))
                throw new InvalidDataException(Resources.ToolDescription_Validate_Tools_must_have_a_title);
            if (string.IsNullOrEmpty(Command))
                throw new InvalidDataException(Resources.ToolDescription_Validate_Tools_must_have_a_command_line);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Title = reader.GetAttribute(ATTR.title);
            Command = reader.GetAttribute(ATTR.command);
            Arguments = reader.GetAttribute(ATTR.arguments) ?? string.Empty;
            InitialDirectory = reader.GetAttribute(ATTR.initial_directory) ?? string.Empty;
            OutputToImmediateWindow = reader.GetBoolAttribute(ATTR.output_to_immediate_window);
            ReportTitle = reader.GetAttribute(ATTR.report_title);
            PreviousCommandLineArgs = reader.GetAttribute(ATTR.argument_generated) ?? string.Empty;
            ArgsCollectorDllPath = reader.GetAttribute(ATTR.argscollector_dll_path) ?? string.Empty;
            ArgsCollectorClassName = reader.GetAttribute(ATTR.argscollector_class_name) ?? string.Empty;
            ToolDirPath = reader.GetAttribute(ATTR.tool_dir_path) ?? string.Empty;
            PackageVersion = reader.GetAttribute(ATTR.package_version) ?? string.Empty;
            PackageIdentifier = reader.GetAttribute(ATTR.package_identifier) ?? string.Empty;
            PackageName = reader.GetAttribute(ATTR.package_name) ?? string.Empty;
            UpdateAvailable = reader.GetBoolAttribute(ATTR.update_available);
            Annotations = new List<AnnotationDef>();
            if (!reader.IsEmptyElement)
            {
                reader.ReadStartElement();
                reader.ReadElementList(EL.annotation, Annotations);
                reader.ReadEndElement();
            }
            else
            {
                reader.Read();
            }
            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.title, Title);
            writer.WriteAttribute(ATTR.command, Command);
            writer.WriteAttributeIfString(ATTR.arguments, Arguments);
            writer.WriteAttributeIfString(ATTR.initial_directory, InitialDirectory);
            writer.WriteAttribute(ATTR.output_to_immediate_window, OutputToImmediateWindow);
            writer.WriteAttributeIfString(ATTR.report_title, ReportTitle);
            writer.WriteAttributeIfString(ATTR.argument_generated, PreviousCommandLineArgs);
            writer.WriteAttributeIfString(ATTR.argscollector_dll_path, ArgsCollectorDllPath);
            writer.WriteAttributeIfString(ATTR.argscollector_class_name, ArgsCollectorClassName);
            writer.WriteAttributeIfString(ATTR.tool_dir_path, ToolDirPath);
            writer.WriteAttributeIfString(ATTR.package_version, PackageVersion);
            writer.WriteAttributeIfString(ATTR.package_identifier, PackageIdentifier);
            writer.WriteAttributeIfString(ATTR.package_name, PackageName);
            writer.WriteAttribute(ATTR.update_available, UpdateAvailable);
            writer.WriteElementList(EL.annotation, Annotations ?? new List<AnnotationDef>());
        }


        #endregion
        
        #region object overrides

        public override string ToString()
        {
            return Title;
        }

        public bool Equals(ToolDescription tool)
        {
            return (Equals(Title, tool.Title) &&
                    Equals(Command, tool.Command) &&
                    Equals(Arguments, tool.Arguments) &&
                    Equals(InitialDirectory, tool.InitialDirectory) &&
                    Equals(OutputToImmediateWindow, tool.OutputToImmediateWindow)) &&
                    Equals(ReportTitle, tool.ReportTitle) &&
                    Equals(ArgsCollectorDllPath, tool.ArgsCollectorDllPath) &&
                    Equals(ArgsCollectorClassName, tool.ArgsCollectorClassName) &&
                    Equals(ToolDirPath, tool.ToolDirPath) &&
                    Equals(PackageVersion, tool.PackageVersion) &&
                    Equals(PackageIdentifier, tool.PackageIdentifier) &&
                    Equals(PackageName, tool.PackageName);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Title.GetHashCode();
                result = (result * 397) ^ Command.GetHashCode();
                result = (result * 397) ^ Arguments.GetHashCode();
                result = (result * 397) ^ InitialDirectory.GetHashCode();
                result = (result * 397) ^ OutputToImmediateWindow.GetHashCode();
                result = (result * 397) ^ ReportTitle.GetHashCode();
                result = (result * 397) ^ ArgsCollectorDllPath.GetHashCode();
                result = (result * 397) ^ ArgsCollectorClassName.GetHashCode();
                result = (result * 397) ^ ToolDirPath.GetHashCode();
                result = (result * 397) ^ (PackageVersion == null ? 0 : PackageVersion.GetHashCode());
                result = (result * 397) ^ (PackageIdentifier == null ? 0 : PackageIdentifier.GetHashCode());
                result = (result * 397) ^ (PackageName == null ? 0 : PackageName.GetHashCode());
                return result;
            }
        }

        #endregion
    }

    // A ToolArgsCollector collects command line arguments for an external tool
    public interface IToolArgsCollector
    {      
        /// <summary>
        /// Returns the argument string to pass to a tool's command line
        /// </summary>
        /// <param name="report">The input report as a string</param>
        /// <param name="args">The arguments most recently generated by the tool</param>
        /// <returns></returns>
        string[] CollectArgs(string report, string[] args);
    }


    /// <summary>
    /// An exception to be thrown when a WebTool fails to open.
    /// </summary>
    public class WebToolException : Exception
    {
        /// <summary>
        /// Returns an instance of WebToolException.
        /// An exception to be thrown when a WebTool fails to open.
        /// </summary>
        public WebToolException (string errorMessage, string link)
        {
            _link = link;
            _errorMessage = errorMessage;
        }
        
        private readonly string _errorMessage;
        private readonly string _link;

        public string ErrorMessage
        {
            get { return _errorMessage; }
        }

        public string Link
        {
            get { return _link; }
        }
    }

    public class ToolExecutionException : Exception
    {
        public ToolExecutionException(string message) : base(message){}

        public ToolExecutionException(string message, Exception innerException) : base(message, innerException){}
    }

    public class ToolDeprecatedException : Exception
    {
        public ToolDeprecatedException(string message) : base(message) { }

        public ToolDeprecatedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class ToolDescriptionHelpers
    {
        /// <summary>
        ///  A helper function that generates the report with reportTitle from the SrmDocument.
        ///  Throws an error if the reportSpec no longer exists in Settings.Default.
        /// </summary>
        /// <param name="doc">Document to create the report from.</param>
        /// <param name="reportTitle">Title of the reportSpec to make a report from.</param>
        /// <param name="toolTitle">Title of tool for exception error message.</param>
        /// <param name="progressMonitor">Progress monitor.</param>
        /// <param name="writer">TextWriter that the report should be written to.</param>
        /// <returns> Returns a string representation of the ReportTitle report, or throws an error that the reportSpec no longer exist. </returns>
        public static void GetReport(SrmDocument doc, string reportTitle, string toolTitle, IProgressMonitor progressMonitor, TextWriter writer)
        {
            var container = new MemoryDocumentContainer();
            container.SetDocument(doc, container.Document);
            var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
            var viewContext = new DocumentGridViewContext(dataSchema);
            ViewInfo viewInfo = viewContext.GetViewInfo(PersistedViews.ExternalToolsGroup.Id.ViewName(reportTitle));
            if (null == viewInfo)
            {
                throw new ToolExecutionException(
                    string.Format(
                        Resources.ToolDescriptionHelpers_GetReport_Error_0_requires_a_report_titled_1_which_no_longer_exists__Please_select_a_new_report_or_import_the_report_format,
                        toolTitle, reportTitle));
            }
            IProgressStatus status =
                new ProgressStatus(string.Format(Resources.ReportSpec_ReportToCsvString_Exporting__0__report,
                    reportTitle));
            progressMonitor.UpdateProgress(status);
            if (!viewContext.Export(CancellationToken.None, progressMonitor, ref status, viewInfo, writer,
                    TextUtil.SEPARATOR_CSV))
            {
                throw new OperationCanceledException();
            }
        }


        // Long test names make for long Tools directory names, which can make for long command lines - maybe too long. So limit that directory name length by
        // shortening to acronym and original length (e.g. "Foo7WithBar" => "F7WB10", "Foo7WithoutBar" => "F7WB13"))
        private static string LimitDirectoryNameLength()
        {
            var testName = Program.TestName.Length > 10 // Arbitrary cutoff, but too little is likely to lead to ambiguous names
                ? string.Concat(Program.TestName.Replace(@"Test", string.Empty).Where(c => char.IsUpper(c) || char.IsDigit(c))) + Program.TestName.Length
                : Program.TestName;
            return $@"{testName}_{Thread.CurrentThread.CurrentCulture.Name}";
        }

        /// <summary>
        /// Get a name for the Skyline Tools directory - if we are running a test, make that name unique to the test in case tests are executing in parallel
        /// </summary>
        public static string GetToolsDirectory()
        {
            var skylineDirPath = GetSkylineInstallationPath();

            // Use a unique tools path when running tests to allow tests to run in parallel
            // ReSharper disable once AssignNullToNotNullAttribute
            return Path.Combine(skylineDirPath, Program.UnitTest ? $@"Tools_{LimitDirectoryNameLength()}" : @"Tools");
        }

        /// <summary>
        /// Gets the current installation directory, where we would expect to find Tools directory etc
        /// </summary>
        public static string GetSkylineInstallationPath()
        {
            var skylinePath = Assembly.GetExecutingAssembly().Location;
            Assume.IsFalse(string.IsNullOrEmpty(skylinePath), @"Could not determine Skyline installation location");
            var skylineDirPath = Path.GetDirectoryName(skylinePath);
            Assume.IsFalse(string.IsNullOrEmpty(skylineDirPath), @"Could not determine Skyline installation directory name");
            return skylineDirPath;
        }
    }
}
