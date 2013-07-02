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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
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

    public interface IExceptionHandler
    {        
        void HandleException(Exception e);
    }

    [XmlRoot("ToolDescription")]
    public class ToolDescription : IXmlSerializable, IKeyContainer<string>
    {
        public static readonly ToolDescription EMPTY = new ToolDescription(string.Empty, string.Empty, string.Empty, string.Empty);

        public static bool IsWebPageCommand(string command)
        {
            return command.StartsWith("http:") || command.StartsWith("https:"); // Not L10N
        }

        public ToolDescription(ToolDescription t)
            : this(t.Title, t.Command, t.Arguments, t.InitialDirectory, t.OutputToImmediateWindow, t.ReportTitle, t.ArgsCollectorDllPath, t.ArgsCollectorClassName, t.ToolDirPath)
        {            
        }

        public ToolDescription(string title, string command)
            : this(title, command, string.Empty, string.Empty, false, string.Empty)
        {
        }

        public ToolDescription(string title, string command, string reportTitle)
            : this(title, command, string.Empty, string.Empty, false, reportTitle)
        {
        }

        public ToolDescription(string title, string command, string arguments, string initialDirectory)
            : this(title, command, arguments, initialDirectory, false, string.Empty)
        {
        }

        public ToolDescription(string title, string command, string arguments, string initialDirectory,
                               bool outputToImmediateWindow, string reportTitle)
            : this( title,  command,  arguments,  initialDirectory,
                                outputToImmediateWindow,  reportTitle, string.Empty, string.Empty, null)
        {            
        }

        public ToolDescription(string title, string command, string arguments, string initialDirectory,
                               bool outputToImmediateWindow, string reportTitle, string argsCollectorDllPath, string argsCollectorClassName, string toolDirPath)
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

        /// <summary>
        ///  Return a string that is the Arguments string with the macros replaced.
        /// </summary>
        /// <param name="doc"> Document for report data. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <param name="exceptionHandler">Interface for handling exceptions across threads. </param>
        /// <returns> Arguments with macros replaced or null if one of the macros was missing 
        /// (eg. no selected peptide for $(SelPeptide) then the return value is null </returns>
        public string GetArguments(SrmDocument doc, IToolMacroProvider toolMacroProvider, IExceptionHandler exceptionHandler)
        {
            return ToolMacros.ReplaceMacrosArguments(doc, toolMacroProvider, this, exceptionHandler);
        }

        /// <summary>
        ///  Return a string that is the InitialDirectoy string with the macros replaced.
        /// </summary>
        /// <param name="doc"> Document for report data. </param>
        /// <param name="toolMacroProvider"> Interface to use to get the current macro values </param>
        /// <param name="exceptionHandler"> Interface for throwing exceptions across threads. </param>
        /// <returns> InitialDirectory with macros replaced or null if one of the macros was missing 
        /// (eg. no document for $(DocumentDir) then the return value is null </returns>
        public string GetInitialDirectory(SrmDocument doc, IToolMacroProvider toolMacroProvider, IExceptionHandler exceptionHandler)
        {
            return ToolMacros.ReplaceMacrosInitialDirectory(doc, toolMacroProvider, this, exceptionHandler);
        }

        private string GetCommand(SrmDocument doc, IToolMacroProvider toolMacroProvider, IExceptionHandler exceptionHandler)
        {
            return ToolMacros.ReplaceMacrosCommand(doc, toolMacroProvider, this, exceptionHandler);            
        }
        
        /// <summary>
        /// Run the tool. When you call run tool. call it on a different thread. 
        /// </summary>       
        /// <param name="doc"> Document to base reports off of. </param>
        /// <param name="toolMacroProvider"> Interface for replacing Tool Macros with the correct strings. </param>
        /// <param name="textWriter"> A textWriter to write to when the tool redirects stdout. (eg. Outputs to an Immediate Window) </param>
        /// <param name="exceptionHandler"> An interface for throwing exceptions to be delt with on different threads. </param>
        public void RunTool(SrmDocument doc, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IExceptionHandler exceptionHandler)
        {
            if (IsWebPage)
            {
                var webHelpers = WebHelpers ?? new WebHelpers();

                if (String.IsNullOrEmpty(ReportTitle))
                {
                    webHelpers.OpenLink(Command);
                }
                else // It has a selected report that must be posted. 
                {
                    PostToLink(doc, exceptionHandler, webHelpers);
                }
            }
            else // Not a website. Needs its own thread.
            {                
                // To eliminate a cross thread error make a copy of the IToolMacroProvider.
                IToolMacroProvider newToolMacroProvider = new CopyToolMacroProvider(toolMacroProvider);
                RunExecutable(doc, newToolMacroProvider, textWriter, exceptionHandler);                
            }           
        }

        private Thread PostToLink(SrmDocument doc, IExceptionHandler exceptionHandler, IWebHelpers webHelpers)
        {
            var thread = new Thread(() => PostToLinkBackground(doc, exceptionHandler, webHelpers));
            thread.Start();
            return thread;
        }

        private void PostToLinkBackground(SrmDocument doc, IExceptionHandler exceptionHandler, IWebHelpers webHelpers)
        {
            string report = ToolDescriptionHelpers.GetReport(doc, ReportTitle, Title, exceptionHandler);
            if (report != null)
                webHelpers.PostToLink(Command, report);
        }

        private Thread RunExecutable(SrmDocument doc, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IExceptionHandler exceptionHandler)
        {
            var thread = new Thread(() => RunExecutableBackground(doc, toolMacroProvider, textWriter, exceptionHandler));
            thread.Start();
            return thread;
        }

        /// <summary>
        ///  Method used to encapsolate the running of a executable for threading.
        /// </summary>
        /// <param name="doc"> Document to base reports off of. </param>
        /// <param name="toolMacroProvider"> Interface for determining what to replace macros with. </param>
        /// <param name="textWriter"> A textWriter to write to if outputting to the immediate window. </param>
        /// <param name="exceptionHandler"> Interface to enable throwing exceptions on other threads. </param>
        private void RunExecutableBackground(SrmDocument doc, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IExceptionHandler exceptionHandler)
        {                                                
            // Need to know if $(InputReportTempPath) is an argument to determine if a report should be piped to stdin or not.
            bool containsInputReportTempPath = Arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH);
            string command = GetCommand(doc, toolMacroProvider, exceptionHandler);
            if (command == null) // Has already thrown the error.
                return;
            string args = GetArguments(doc, toolMacroProvider, exceptionHandler);
            string initDir = GetInitialDirectory(doc, toolMacroProvider, exceptionHandler); // If either of these fails an Exception is thrown.
                                    
            if (args != null && initDir != null)
            {
                ProcessStartInfo startInfo = OutputToImmediateWindow
                                          ? new ProcessStartInfo(command, args) { WorkingDirectory = initDir, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true}
                                          : new ProcessStartInfo(command, args) { WorkingDirectory = initDir };

                // if it has a selected report title and its doesn't have a InputReportTempPath macro then the report needs to be piped to stdin.
                string reportCsv = null;
                if (!String.IsNullOrEmpty(ReportTitle) && !containsInputReportTempPath) // Then pipe to stdin.
                {
                    reportCsv = ToolDescriptionHelpers.GetReport(doc, ReportTitle, Title, exceptionHandler);
                    startInfo.RedirectStandardInput = true;
                }

                //Consider: Maybe throw an error if one is not null but the other is?
                //If there is an IToolArgsCollector run it!
                if (!string.IsNullOrEmpty(ArgsCollectorDllPath) && !string.IsNullOrEmpty(ArgsCollectorClassName))
                {
                    Match file = Regex.Match(args, @"[^ \t]+\.csv"); //Not L10N
                    string csvToParse = reportCsv;
                    if (csvToParse == null && Match.Empty != file)
                    {
                        csvToParse = File.ReadAllText(file.Value);   
                    }
                    
                    string oldArgs = PreviousCommandLineArgs;
                    Assembly assembly;
                    try
                    {
                        assembly = Assembly.LoadFrom(ArgsCollectorDllPath);
                    }
                    catch (Exception x)
                    {
                        exceptionHandler.HandleException(new Exception(string.Format(TextUtil.LineSeparate(
                            Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool_0_It_seems_to_be_missing_a_file__Please_reinstall_the_tool_and_try_again_), Title), x));
                        return;                        
                    }
                    
                    Type type = assembly.GetType(ArgsCollectorClassName);
                    if (type == null)
                    {
                        exceptionHandler.HandleException(new Exception(string.Format(TextUtil.LineSeparate(
                            Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool__0___It_seems_to_have_an_error_in_one_of_its_files__Please_reinstall_the_tool_and_try_again),Title)));
                        return;             
                    }


                    object[] collectorArgs = new object[] { csvToParse, (oldArgs != null) ? CommandLine.ParseInput(oldArgs) : null };
                    object answer = null;
                    try
                    {
                        answer = type.GetMethod("CollectArgs").Invoke(null, collectorArgs); //Not L10N
                    }
                    catch (Exception x)
                    {
                        string message = x.Message;
                        if (string.IsNullOrEmpty(message))
                        {
                            exceptionHandler.HandleException(new Exception(string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error_, Title)));    
                        }
                        else
                        {
                            exceptionHandler.HandleException(new Exception(string.Format(TextUtil.LineSeparate(
                                Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error__it_returned_the_message_,
                                message), Title)));    
                        }                        
                    }
                    string[] commandLineArguments = answer as string[];
                    if (commandLineArguments != null)
                    {
                        // Parse
                        string argString = PreviousCommandLineArgs = CommandLine.ParseCommandLineArray(commandLineArguments);
                        // Append to end of argument string
                        if (args.Contains(ToolMacros.COLLECTED_ARGS))
                        {
                            startInfo.Arguments = args.Replace(ToolMacros.COLLECTED_ARGS, argString);
                        }
                        else
                        {
                            startInfo.Arguments = args + " " + argString;
                        }                        
                    }
                    else
                    {
                        /*Establish an expectation that if an args collector returns null then there was some error
                         * and the args collector displayed the relevant error and our job is to just terminate tool execution
                         * If they would like the tool to run with no extra args they could return String.Empty
                         */
                        return; 
                    } 
                }
               
                Process p = new Process {StartInfo = startInfo};
                if (OutputToImmediateWindow)
                {
                    p.EnableRaisingEvents = true;
                    TextBoxStreamWriterHelper boxStreamWriterHelper = textWriter as TextBoxStreamWriterHelper;
                    if (boxStreamWriterHelper == null)
                    {
                        p.OutputDataReceived += (sender, dataReceivedEventArgs) => textWriter.WriteLine(p.Id + 
                                                                                                        ">" + dataReceivedEventArgs.Data);
                        p.ErrorDataReceived += (sender, dataReceivedEventArgs) => textWriter.WriteLine(p.Id +
                                                                                                       ">" + dataReceivedEventArgs.Data);
                    }                    
                    else
                    {
                        p.OutputDataReceived += (sender, dataReceivedEventArgs) => boxStreamWriterHelper.WriteLineWithIdentifier(p.Id, dataReceivedEventArgs.Data);
                        p.ErrorDataReceived += (sender, dataReceivedEventArgs) => boxStreamWriterHelper.WriteLineWithIdentifier(p.Id, dataReceivedEventArgs.Data);
                        //p.Refresh();
                        p.Exited += (sender, processExitedEventArgs) => boxStreamWriterHelper.HandleProcessExit(sender, processExitedEventArgs, p.Id);
                    }
                    
                }

                try
                {
                    p.Start();
                    if (OutputToImmediateWindow)
                    {
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                    }

                    // write the reportCsv string to stdin.
                    // need to only check one of these conditions.
                    if (startInfo.RedirectStandardInput && (reportCsv != null))
                    {
                        StreamWriter streamWriter = p.StandardInput;
                        streamWriter.Write(reportCsv);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException || ex is Win32Exception)
                    {
                        exceptionHandler.HandleException(new Exception(TextUtil.LineSeparate(Resources.ToolDescription_RunTool_File_not_found_,
                                    Resources.ToolDescription_RunTool_Please_check_the_command_location_is_correct_for_this_tool_)));
                    }
                    else
                    {
                        exceptionHandler.HandleException(new Exception(Resources.ToolDescription_RunTool_Please_reconfigure_that_tool__it_failed_to_execute__));
                    }
                }

                // CONSIDER: We don't delete the temp path here, because the file may be open
                //           in a long running application like Excel.
//                if (ReportTempPath_toDelete != null)
//                {
//                    FileEx.SafeDelete(ReportTempPath_toDelete, true);
//                    ReportTempPath_toDelete = null;
//                }  
            }      
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
            tool_dir_path
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
            reader.Read();
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
            writer.WriteAttributeIfString(ATTR.tool_dir_path,ToolDirPath);
        }
        #endregion
        
        #region object overrides

        public bool Equals(ToolDescription tool)
        {
            return (Equals(Title, tool.Title) &&
                    Equals(Command, tool.Command) &&
                    Equals(Arguments, tool.Arguments) &&
                    Equals(InitialDirectory, tool.InitialDirectory) &&
                    Equals(OutputToImmediateWindow, tool.OutputToImmediateWindow)) &&
                    Equals(ReportTitle, tool.ReportTitle)&&
                    Equals(ArgsCollectorDllPath, tool.ArgsCollectorDllPath) &&
                    Equals(ArgsCollectorClassName, tool.ArgsCollectorClassName) &&
                    Equals(ToolDirPath, tool.ToolDirPath);
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

    public class MessageException : Exception
    {
        public MessageException(string message) : base(message){}
    }

    public class ToolDescriptionHelpers
    {
        /// <summary>
        ///  A helper function that generates the report with reportTitle from the SrmDocument.
        ///  Throws an error if the reportSpec no longer exists in Settings.Default.
        /// </summary>
        /// <param name="doc">Document to create the report from.</param>                
        /// <param name="reportTitle">Title of the reportSpec to make a report from.</param>
        /// <param name="toolTitle">Title of tool for exception error message.</param>
        /// <param name="exceptionHandler">Handler for any exception thrown or null, if exceptions are to be thrown directly to caller</param>
        /// <returns> Returns a string representation of the ReportTitle report, or throws an error that the reportSpec no longer exist. </returns>
        public static string GetReport(SrmDocument doc, string reportTitle, string toolTitle, IExceptionHandler exceptionHandler)
        {
            ReportSpec reportSpec = Settings.Default.GetReportSpecByName(reportTitle);
            if (reportSpec == null)
            {
                // Complain that the report no longer exist.
                var x = new Exception(string.Format(
                        Resources.ToolDescriptionHelpers_GetReport_Error_0_requires_a_report_titled_1_which_no_longer_exists__Please_select_a_new_report_or_import_the_report_format,
                        toolTitle, reportTitle));
                if (exceptionHandler == null)
                    throw x;

                exceptionHandler.HandleException(x);
                return null;
            }

            return reportSpec.ReportToCsvString(doc, exceptionHandler as IProgressMonitor);
        }

        public static string GetToolsDirectory()
        {
            string skylinePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(skylinePath))
                return null;
            string skylineDirPath = Path.GetDirectoryName(skylinePath);
            if (string.IsNullOrEmpty(skylineDirPath))
                return null;
            return Path.Combine(skylineDirPath, "Tools"); //Not L10N            
        }
    }
}