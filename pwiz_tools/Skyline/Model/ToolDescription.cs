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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Ionic.Zip;
using Kajabity.Tools.Java;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
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

//            Validate();  Not immutable
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

        // the most recent command line arguments generated by the tool
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
                                          ? new ProcessStartInfo(command, args) { WorkingDirectory = initDir, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true}
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

                try
                {
                    p.Start();

                    // write the reportCsv string to stdin.
                    // need to only check one of these conditions.
                    if (startInfo.RedirectStandardInput && (reportCsv != null))
                    {
                        StreamWriter streamWriter = p.StandardInput;
                        streamWriter.Write(reportCsv);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }

                    // Return the output to be written to the ImmediateWindow.
                    if (OutputToImmediateWindow)
                    {
                        textWriter.Write(p.StandardOutput.ReadToEnd());
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

    public static class ToolMacros
    {
        public const string INPUT_REPORT_TEMP_PATH = "$(InputReportTempPath)"; //Not L10N
        public const string PROGRAM_PATH = @"\$\(ProgramPath\((.*)\)\)"; //Not L10N
        public const string TOOL_DIR = "$(ToolDir)"; //Not L10N
        public const string COLLECTED_ARGS = "$(CollectedArgs)"; //Not L10N

        // Macros for Arguments.
        public static Macro[] _listArguments = new[]
            {
                new Macro(Resources.ToolMacros__listArguments_Document_Path, "$(DocumentPath)", GetDocumentFilePath, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_Path_to_run), 
                new Macro(Resources.ToolMacros__listArguments_Document_Directory, "$(DocumentDir)", GetDocumentDir, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_Directory_to_run),
                new Macro(Resources.ToolMacros__listArguments_Document_File_Name, "$(DocumentFileName)", GetDocumentFileName, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_File_Name_to_run),
                new Macro(Resources.ToolMacros__listArguments_Document_File_Name_Without_Extension, "$(DocumentBaseName)", GetDocumentFileNameWithoutExtension, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_File_Name__to_run_),
                new Macro(Resources.ToolMacros__listArguments_Selected_Protein_Name, "$(SelProtein)", GetSelectedProteinName, TextUtil.LineSeparate(Resources.ToolMacros__listArguments_This_tool_requires_a_Selected_Protein_to_run_,Resources.ToolMacros__listArguments_Please_select_a_protein_before_running_this_tool_)),
                new Macro(Resources.ToolMacros__listArguments_Selected_Peptide_Sequence, "$(SelPeptide)", GetSelectedPeptideSequence, TextUtil.LineSeparate(Resources.ToolMacros__listArguments_This_tool_requires_a_Selected_Peptide_Sequence_to_run, Resources.ToolMacros__listArguments_Please_select_a_peptide_sequence_before_running_this_tool_ )),
                new Macro(Resources.ToolMacros__listArguments_Selected_Precursor, "$(SelPrecursor)", GetSelectedPrecursor, TextUtil.LineSeparate(Resources.ToolMacros_listArguments_This_tool_requires_a_Selected_Precursor_to_run,Resources.ToolMacros_listArguments_Please_select_a_precursor_before_running_this_tool_)),
                new Macro(Resources.ToolMacros__listArguments_Active_Replicate_Name, "$(ReplicateName)", GetActiveReplicateName, Resources.ToolMacros_listArguments_This_tool_requires_an_Active_Replicate_Name_to_run),                
                new Macro(Resources.ToolMacros__listArguments_Input_Report_Temp_Path, INPUT_REPORT_TEMP_PATH, GetReportTempPath , Resources.ToolMacros_listArguments_This_tool_requires_a_selected_report),
                new Macro(Resources.ToolMacros__listArguments_Collected_Arguments, COLLECTED_ARGS, null , Resources.ToolMacros__listArguments_This_tool_does_not_provide_the_functionality_for_the_Collected_Arguments_macro__Please_edit_the_tool_),
                new Macro(Resources.ToolMacros__listArguments_Tool_Directory, TOOL_DIR, GetToolDirectory, Resources.ToolMacros__listArguments_This_tool_is_not_an_installed_tool_so_ToolDir_cannot_be_used_as_a_macro__Please_edit_the_tool_)
            };

        // Macros for InitialDirectory.
        public static Macro[] _listInitialDirectory = new[]
            {
                new Macro(Resources.ToolMacros__listArguments_Document_Directory, "$(DocumentDir)", GetDocumentDir, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_Directory_to_run) 
            };

        // Macros for Command.
        public static Macro[] _listCommand = new[]
            {
                new Macro(Resources.ToolMacros__listCommand_Program_Path, PROGRAM_PATH, GetProgramPath, TextUtil.LineSeparate(Resources.ToolMacros__listCommand_This_tool_requires_a_Program_Path_to_run_,Resources.ToolMacros__listCommand__No_Path_Provided__Tool_execution_cancled_)), 
                new Macro(Resources.ToolMacros__listArguments_Tool_Directory, TOOL_DIR, GetToolDirectory, Resources.ToolMacros__listArguments_This_tool_is_not_an_installed_tool_so_ToolDir_cannot_be_used_as_a_macro__Please_edit_the_tool_)
            };

        /// <summary>
        ///  Get the path to the version of the program executable. 
        /// </summary>
        /// <param name="toolMacroInfo">Wrapper that has a valid ProgramPathContainer.</param>
        /// <returns>Path to program executable that was saved in settings.</returns>
        private static string GetProgramPath(ToolMacroInfo toolMacroInfo)
        {
            ProgramPathContainer ppc = toolMacroInfo.programPathContainer;
            string path = null;
            if (ppc != null)
            {
                if (Settings.Default.FilePaths.ContainsKey(ppc))
                    path = Settings.Default.FilePaths[ppc];

                if (path == null)
                {
                    path = toolMacroInfo.FindProgramPath(ppc);

                }    
            }                        
            return path;
        }

        public static string ReplaceMacrosCommand(SrmDocument doc, IToolMacroProvider toolMacroProvider, ToolDescription toolDescription, IExceptionHandler exceptionHandler)
        {
            string workingString = toolDescription.Command;
            foreach (Macro macro in _listCommand)
            {
                if (macro.ShortText == PROGRAM_PATH)
                {
                    ProgramPathContainer ppc = IsProgramPathMacro(workingString);
                    if (ppc == null)
                    {
                        // Leave command as is.
                    }
                    else
                    {
                        string path = macro.GetContents(new ToolMacroInfo(toolMacroProvider, toolDescription.Title,
                                                        toolDescription.ReportTitle, doc, exceptionHandler, ppc, toolDescription.ToolDirPath));
                        if (path == null)
                        {                            
                            exceptionHandler.HandleException(new Exception(macro.ErrorMessage));
                            return null;
                        }
                        workingString = path;
                    }
                }
                if (macro.ShortText == TOOL_DIR)
                {
                    if (workingString.Contains(TOOL_DIR))
                    {
                        if (string.IsNullOrEmpty(toolDescription.ToolDirPath))
                        {
                            exceptionHandler.HandleException(new Exception(macro.ErrorMessage));
                            return null;
                        }
                        workingString = workingString.Replace(TOOL_DIR, toolDescription.ToolDirPath);
                    }
                }
            }
            return workingString;
        }

        /// <summary>
        /// Checks the string arguments of the tool for the ShortText of each macro in the macro list.
        /// If the short text is present, get the actual value and replace it. 
        /// If the actual value turns out to be null an exception will be thrown.
        /// </summary>        
        /// <param name="doc"> A SrmDocument to base reports off of </param>
        /// <param name="toolMacroProvider"> Method provider for getting macro actual values </param>
        /// <param name="tool"> The tool to run this on. </param>
        /// <param name="exceptionHandler">InterfaceProvider for throwing exceptions on different threads. </param>
        /// <returns> Arguments string with macros replaced or a thrown exception with error message. </returns>
        public static string ReplaceMacrosArguments(SrmDocument doc, IToolMacroProvider toolMacroProvider, ToolDescription tool, IExceptionHandler exceptionHandler)
        {
            return ReplaceMacrosHelper(doc, toolMacroProvider, tool, exceptionHandler, tool.Arguments, _listArguments);
        }

        /// <summary>
        /// Checks the string initialDirectory of the tool for the ShortText of each macro in the macro list.
        /// If the short text is present, get the actual value and replace it. 
        /// If the actual value turns out to be null an exception will be thrown.
        /// </summary>        
        /// <param name="doc"> A SrmDocument to base reports off of </param>
        /// <param name="toolMacroProvider"> Method provider for getting macro actual values </param>
        /// <param name="tool"> The tool to run this on. </param>
        /// <param name="exceptionHandler"> Interface for throwing exceptions across threads. </param>
        /// <returns> InitialDirectory string with macros replaced or a thrown exception with error message. </returns>
        public static string ReplaceMacrosInitialDirectory(SrmDocument doc, IToolMacroProvider toolMacroProvider, ToolDescription tool, IExceptionHandler exceptionHandler)
        {
            return ReplaceMacrosHelper(doc, toolMacroProvider, tool, exceptionHandler, tool.InitialDirectory, _listInitialDirectory);
        }

        public static string ReplaceMacrosHelper(SrmDocument doc, IToolMacroProvider toolMacroProvider, ToolDescription tool, IExceptionHandler exceptionHandler, string replacein, Macro[] macros)
        {
            string wokingString = replacein;
            foreach (Macro macro in macros)
            {
                if (wokingString.Contains(macro.ShortText))
                {
                    string contents;
                    if (macro.PlainText == Resources.ToolMacros__listArguments_Input_Report_Temp_Path)
                    {
                        try // InputReportTempPath throws more specific exceptions, this case deals with those.
                        {
                            contents = macro.GetContents(new ToolMacroInfo(toolMacroProvider, tool.Title, tool.ReportTitle, doc, exceptionHandler));
                            tool.ReportTempPath_toDelete = contents;
                        }
                        catch (Exception e)
                        {
                            exceptionHandler.HandleException(e);
                            return null;
                        }
                    }
                    else if (macro.ShortText == COLLECTED_ARGS)
                    {
                        //Do Nothing. (this gets replaced later after we actually run the args collector.
                        continue;
                    }
                    else
                    {
                        /* null is fine for the ProgramPathContainer argument because ProgramPathContainer
                         * is only used when working with the command text and this function is only used for
                         * arguments and initial directory. */
                        contents = macro.GetContents(new ToolMacroInfo(toolMacroProvider, tool.Title, tool.ReportTitle, doc, exceptionHandler, null, tool.ToolDirPath));
                    }
                    if (contents == null)
                    {
                        exceptionHandler.HandleException(new Exception(macro.ErrorMessage));
                        return null;
                    }
                    wokingString = wokingString.Replace(macro.ShortText, contents);
                }
            }
            return wokingString;
        }

        /// <summary>
        /// Save the report to a temp file 
        /// </summary>
        /// <returns>The path to the saved temp file.</returns>
        private static string GetReportTempPath(ToolMacroInfo toolMacroInfo)
        {
            SrmDocument doc = toolMacroInfo.Doc;            
            string reportName = toolMacroInfo.ReportName;
            string toolTitle = toolMacroInfo.ToolTitle;
            if (String.IsNullOrEmpty(reportName))
            {                
                throw new Exception(string.Format(Resources.ToolMacros_GetReportTempPath_The_selected_tool_0_requires_a_selected_report_Please_select_a_report_for_this_tool_, toolTitle));
            }

            string reportFileName = reportName.Replace(' ', '_');
            string toolFileName = toolTitle.Replace(' ', '_').Replace('\\','_');            
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
            {                
                reportFileName = reportFileName.Replace(c.ToString(CultureInfo.InvariantCulture), String.Empty);
                toolFileName = toolFileName.Replace(c.ToString(CultureInfo.InvariantCulture), String.Empty);
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), toolFileName + "_" + reportFileName + ".csv");

            string report = ToolDescriptionHelpers.GetReport(doc, reportName, toolTitle, toolMacroInfo.ExceptionHandler);
            
            if (report!=null)
            {
                try
                {
                    using (var saver = new FileSaver(tempFilePath))
                    {
                        if (!saver.CanSave())
                        {
                            throw new IOException();
                        }
                        using (var writer = new StreamWriter(saver.SafeName))
                        {                            
                            writer.Write(report);
                            writer.Flush();
                            writer.Close();
                        }
                        saver.Commit();
                        return tempFilePath;                        
                    }
                }
                catch (Exception)
                {                    
                    throw new IOException(Resources.ToolMacros_GetReportTempPath_Error_exporting_the_report__tool_execution_canceled_);                    
                }     
            }
            return null;
        }

        private static string GetDocumentFilePath(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.DocumentFilePath;
        }

        private static string GetDocumentDir(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetDirectoryName(toolMacroProvider.DocumentFilePath);
        }

        private static string GetDocumentFileName(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetFileName(toolMacroProvider.DocumentFilePath);
        }

        private static string GetDocumentFileNameWithoutExtension(IToolMacroProvider toolMacroProvider)
        {
            return Path.GetFileNameWithoutExtension(toolMacroProvider.DocumentFilePath);
        }

        private static string GetSelectedProteinName (IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedProteinName;
        }

        private static string GetSelectedPeptideSequence(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedPeptideSequence;
        }

        private static string GetSelectedPrecursor(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.SelectedPrecursor;
        }

        private static string GetActiveReplicateName(IToolMacroProvider toolMacroProvider)
        {
            return toolMacroProvider.ResultNameCurrent;
        }

        private static string GetToolDirectory(ToolMacroInfo arg)
        {
            return String.IsNullOrEmpty(arg.ToolDirPath) ? null : arg.ToolDirPath;
        }

        /// <summary>
        /// Helper function to match on the ProgramPath Macro and extract the program title and version where relevant. 
        /// </summary>
        /// <param name="command">Command string to match on</param>
        /// <returns>Internal Matchings.</returns>
        public static ProgramPathContainer IsProgramPathMacro(string command)
        {
            // Example String "$(ProgramPath(R,2.12.2))
            // Want to extract R,2.12.2 and then separate the two things.
            Match match = Regex.Match(command, PROGRAM_PATH);
            ProgramPathContainer ppc = null;
            if (match.Groups.Count == 2)
            {
                string paramValues = match.Groups[1].Value;
                string[] values = paramValues.Split(',');
                string programName = values[0];
                string programVersion = null;
                if (values.Length > 1)
                {
                    // Extract the version if specified.
                    programVersion = paramValues.Replace(programName, "").Trim().Substring(1);
                }
                ppc = new ProgramPathContainer(programName, programVersion);
            }

            return ppc;            
        }
    }

    [XmlRoot("ProgramPathContainer")]
    public class ProgramPathContainer : IXmlSerializable, IKeyContainer<int>
    {
        public string ProgramName { get; private set; }
        public string ProgramVersion { get; private set; }

        public ProgramPathContainer(string programName, string programVersion)
        {
            ProgramName = programName;
            ProgramVersion = programVersion;
        }

        #region object overrides 

        public override bool Equals(object obj)
        {
            var other = obj as ProgramPathContainer;
            if (other == null)
                return false;
            return other.ProgramName == ProgramName && other.ProgramVersion == ProgramVersion;           
        }
        public override int GetHashCode()
        {
            return (ProgramName + ProgramVersion).GetHashCode();
        }

        public int GetKey()
        {
            return GetHashCode();
        }

        #endregion // object overrides 

        #region Implementation of IXmlSerializable

        private ProgramPathContainer()
        {
        }

        public static ProgramPathContainer Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ProgramPathContainer());
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        private enum ATTR
        {
            program_name,
            program_version
        }

        private void Validate()
        {
            if (string.IsNullOrEmpty(ProgramName))            
                throw new InvalidDataException(Resources.ProgramPathContainer_Validate_ProgramPathCollectors_must_have_a_program_name);
        }

        public void ReadXml(XmlReader reader)
        {
            ProgramName = reader.GetAttribute(ATTR.program_name);
            ProgramVersion = reader.GetAttribute(ATTR.program_version);
            reader.Read();
            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.program_name, ProgramName);
            writer.WriteAttributeIfString(ATTR.program_version, ProgramVersion);
        }
        #endregion
    }

    public class Macro
    {
        /// <summary>
        ///  A decription for Macros
        /// </summary>
        /// <param name="plainText"> The text that shows up on the drop down menu (eg. "Document Path")</param>
        /// <param name="shortText"> The text that shows up in the text box (eg. "$(DocumentPath)")</param>
        /// <param name="getContents"> A function that when passed an ToolMacroInfo returns the actual string value the macro represents. </param>
        /// <param name="errorMessage">The message that will be displayed if GetContents returns null in the replace macro methods. (eg. When there is no document to get the path of) </param>
        public Macro(string plainText, string shortText, Func<ToolMacroInfo, string> getContents, string errorMessage)
        {
            PlainText = plainText;
            ShortText = shortText;
            GetContents = getContents;
            ErrorMessage = errorMessage;
        }        

        public string PlainText { get; set; }
        public string ShortText { get; set; }
        public string ErrorMessage { get; set; }       
        public Func<ToolMacroInfo, string> GetContents { get; set; }
    }

    public class ToolMacroInfo : IToolMacroProvider
    {
        private readonly IToolMacroProvider _macroProvider;

        public ToolMacroInfo(SkylineWindow sw, ToolDescription td) :
            this(sw, td.Title, td.ReportTitle, sw.Document, sw, null, td.ToolDirPath)
        {            
        }

        public ToolMacroInfo(IToolMacroProvider macroProvider,
                             string toolTitle,
                             string reportName,
                             SrmDocument document,
                             IExceptionHandler exceptionHandler)
            : this(macroProvider, toolTitle, reportName, document, exceptionHandler, null, null)
        {
        }

        public ToolMacroInfo(IToolMacroProvider macroProvider,
                     string toolTitle,
                     string reportName,
                     SrmDocument document,
                     IExceptionHandler exceptionHandler,
                     ProgramPathContainer pathContainer,
                     string toolDirPath)
        {
            _macroProvider = macroProvider;
            ToolTitle = toolTitle;
            ReportName = reportName;
            Doc = document;
            ExceptionHandler = exceptionHandler;
            programPathContainer = pathContainer;
            ToolDirPath = toolDirPath;
        }

        public string ToolTitle { get; private set; }
        public string ReportName { get; private set; }
        public SrmDocument Doc { get; private set; }
        public IExceptionHandler ExceptionHandler { get; private set; }
        public ProgramPathContainer programPathContainer { get; private set; }
        public string ToolDirPath { get; private set; }
        
        #region Implementation of IToolMacroProvider

        public string DocumentFilePath
        {
            get { return _macroProvider.DocumentFilePath; }
        }

        public string SelectedProteinName
        {
            get { return _macroProvider.SelectedProteinName; }
        }

        public string SelectedPeptideSequence
        {
            get { return _macroProvider.SelectedPeptideSequence; }
        }

        public string SelectedPrecursor
        {
            get { return _macroProvider.SelectedPrecursor; }
        }

        public string ResultNameCurrent
        {
            get { return _macroProvider.ResultNameCurrent; }
        }

        public string FindProgramPath(ProgramPathContainer pcc)
        {
            return _macroProvider.FindProgramPath(pcc);
        }

        #endregion
    }

    public class CopyToolMacroProvider : IToolMacroProvider
    {
        public CopyToolMacroProvider(IToolMacroProvider iToolMacroProvider)
        {
            DocumentFilePath = iToolMacroProvider.DocumentFilePath;
            SelectedProteinName = iToolMacroProvider.SelectedProteinName;
            SelectedPeptideSequence = iToolMacroProvider.SelectedPeptideSequence;
            SelectedPrecursor = iToolMacroProvider.SelectedPrecursor;
            ResultNameCurrent = iToolMacroProvider.ResultNameCurrent;
            _getProgramPath = iToolMacroProvider.FindProgramPath;
        }

        private Func<ProgramPathContainer, string> _getProgramPath { get; set; } 

        #region Implementation of IToolMacroProvider

        public string DocumentFilePath { get; private set; }

        public string SelectedProteinName { get; private set; }

        public string SelectedPeptideSequence { get; private set; }

        public string SelectedPrecursor { get; private set; }

        public string ResultNameCurrent { get; private set; }

        public string FindProgramPath(ProgramPathContainer programPathContainer)
        {
            return _getProgramPath(programPathContainer);
        }

        #endregion
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


    /// <summary>
    /// Class for reading .properties files that describe external tools. 
    /// </summary>
    public class ExternalToolReadIn
    {
        /// <summary>
        /// Constants for key values in a .properties file that describe a tool.
        /// </summary>
        static class PropertiesConstants
        {
            //Required
            public const string TITLE = "Title";                                        //Not L10N
            public const string COMMAND = "Command";                                    //Not L10N
            // Optional
            public const string ARGUMENTS = "Arguments";                                //Not L10N
            public const string INITIAL_DIRECTORY = "InitialDirectory";                 //Not L10N
            public const string OUTPUT_TO_IMMEDIATE_WINDOW = "OutputToImmediateWindow"; //Not L10N
            public const string INPUT_REPORT_NAME = "InputReportName";                  //Not L10N
            public const string REPORT_SKYR_FILE = "ReportSkyrFile";                    //Not L10N
            public const string ARGS_COLLECTOR_DLL = "ArgsCollectorDll";                //Not L10N
            public const string ARGS_COLLECTOR_TYPE = "ArgsCollectorType";              //Not L10N
            public const string DESCRIPTION = "Description";                            //Not L10N
            
            // Defaults for optional values in the .properties file
            public static readonly Hashtable DEFAULTS = new Hashtable
                {
                    {ARGUMENTS, string.Empty},
                    {INITIAL_DIRECTORY, string.Empty},
                    {OUTPUT_TO_IMMEDIATE_WINDOW, string.Empty},
                    {INPUT_REPORT_NAME, string.Empty},
                    {REPORT_SKYR_FILE, string.Empty},
                    {ARGS_COLLECTOR_DLL, string.Empty},
                    {ARGS_COLLECTOR_TYPE, string.Empty},
                    {DESCRIPTION, string.Empty}
                };
        }

        private readonly JavaProperties _properties;


        public ExternalToolReadIn(FileStream fileStream)
        {
            _properties = new JavaProperties(PropertiesConstants.DEFAULTS);
            _properties.Load(fileStream);
        }
        public void Load(FileStream fileStream)
        {
            _properties.Clear();
            _properties.Load(fileStream);
        }

        public string lookup(string key)
        {
            return _properties.GetProperty(key);
        }

        #region Tool Attribut Accessors
        public string Title
        {
            get {return _properties.GetProperty(PropertiesConstants.TITLE) != null ? _properties.GetProperty(PropertiesConstants.TITLE).Trim() : null;}
        }
        public string Command
        {
            get {return _properties.GetProperty(PropertiesConstants.COMMAND) != null ? _properties.GetProperty(PropertiesConstants.COMMAND).Trim() : null;}
        }
        public string Arguments
        {
            get { return _properties.GetProperty(PropertiesConstants.ARGUMENTS); }
        }
        public string Initial_Directory
        {
            get { return _properties.GetProperty(PropertiesConstants.INITIAL_DIRECTORY); }
        }
        public string Output_to_Immediate_Window
        {
            get { return _properties.GetProperty(PropertiesConstants.OUTPUT_TO_IMMEDIATE_WINDOW); }
        }
        public string Input_Report_Name
        {
            get {return _properties.GetProperty(PropertiesConstants.INPUT_REPORT_NAME) != null ? _properties.GetProperty(PropertiesConstants.INPUT_REPORT_NAME).Trim() : null;}            
        }
        public string Report_Skyr_File
        {
            get { return _properties.GetProperty(PropertiesConstants.REPORT_SKYR_FILE); }
        }
        public string Args_Collector_Dll
        {
            get { return _properties.GetProperty(PropertiesConstants.ARGS_COLLECTOR_DLL); }
        }
        public string Args_Collector_Type
        {
            get { return _properties.GetProperty(PropertiesConstants.ARGS_COLLECTOR_TYPE); }
        }
        public string Description
        {
            get { return _properties.GetProperty(PropertiesConstants.DESCRIPTION); }
        }
        #endregion //Accessors
    }

    public class UnpackZipToolHelper
    {
        /// <summary>
        /// Function for unpacking zipped External tools.
        /// </summary>
        /// <param name="pathToZip">Path to the zipped file that contains the tool and all its assicaited files.</param>        
        /// <param name="shouldOverwrite">Function that when given a list of tools and a list of reports that would be removed by an installation
        ///  it returns a bool, true for overwrite, false for in parallel and null for cancel installation</param>
        /// <param name="findProgramPath">Function that finds a program path given a program path container, right now prompts the user for the path to a program
        /// later will be a function that automaticall installs the external program.</param>
        /// <returns></returns>
        public static UnzipToolReturnAccumulator UnpackZipTool(string pathToZip,
            Func<List<string>, List<ReportSpec>, bool?> shouldOverwrite,
            Func<ProgramPathContainer,string> findProgramPath)
        {
            UnzipToolReturnAccumulator retval = new UnzipToolReturnAccumulator();
            string name = Path.GetFileNameWithoutExtension(pathToZip);
            if (name == null)
            {                
                throw new Exception(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_file_selected__No_tools_added_);
            }

            string outerToolsFolderPath = ToolDescriptionHelpers.GetToolsDirectory();
            if (string.IsNullOrEmpty(outerToolsFolderPath))
            {
                throw new Exception(Resources.ConfigureToolsDlg_unpackZipTool_Error_unpacking_zipped_tools);
            }
            string tempFolderPath = Path.Combine(outerToolsFolderPath, "Temp"); //Not L10N

            ZipFile zipFile = new ZipFile(pathToZip);
            
            DirectoryInfo toolDir = new DirectoryInfo(tempFolderPath);
            if (!toolDir.Exists)
                toolDir.Create();

            
            string tempToolPath = Path.Combine(tempFolderPath, name);
            if (Directory.Exists(tempToolPath))
                tempToolPath = GetNewDirName(tempToolPath); //This naming conflict shouldn't happen. The temp file should be empty.
            //Consider: Try to delete the existing directory in the temp directory.
            try
            {
                zipFile.ExtractAll(tempToolPath, ExtractExistingFileAction.OverwriteSilently);                
            }
            catch (Exception)
            {
                zipFile.Dispose();
                throw new Exception(Resources.ConfigureToolsDlg_unpackZipTool_There_is_a_naming_conflict_in_unpacking_the_zip__Tool_importing_cancled_);

            }
            zipFile.Dispose();
            
            string permToolPath = Path.Combine(outerToolsFolderPath, name);


            var toolsToBeOverwrittenTitles = new List<string>();
            var toolsToBeOverwritten = new List<ToolDescription>();
            if (Directory.Exists(permToolPath))
            {
                //Warn about deleting the tools in the folder associted with permToolPath.
                foreach (var tool in Settings.Default.ToolList)
                {
                    if (tool.ToolDirPath == permToolPath)
                    {
                        toolsToBeOverwrittenTitles.Add(tool.Title);
                        toolsToBeOverwritten.Add(tool);
                    }
                } // We have not acumulated the list of Tools that would be over written.
            }            

            DirectoryInfo dirInfo = new DirectoryInfo(tempToolPath);
            if (!dirInfo.Exists) //Case where they try to load tools from an empty zipfile then the folder is never created.
                dirInfo.Create();

            DirectoryInfo toolInf = new DirectoryInfo(Path.Combine(tempToolPath, "tool-inf"));
            if (!toolInf.Exists)
                toolInf.Create();

            
            var existingReports = new List<ReportSpec>();
            var newReports = new List<ReportSpec>();
            var xmlSerializer = new XmlSerializer(typeof(ReportSpecList));
            foreach (FileInfo file in toolInf.GetFiles("*.skyr"))
            {
                ReportSpecList loadedItems;
                try
                {
                    using (var stream = File.OpenRead(file.FullName))
                    {                        
                        loadedItems = (ReportSpecList)xmlSerializer.Deserialize(stream);
                    }
                }
                catch (Exception exception)
                {
                    throw new IOException(string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__, file.FullName), exception);
                }
                
                foreach (var reportSpec in loadedItems)
                {
                    if (Settings.Default.ReportSpecList.ContainsKey(reportSpec.GetKey()))
                        existingReports.Add(reportSpec);
                    else
                    {
                        newReports.Add(reportSpec);
                    }
                }
                //We now have the list of Reports that would have a naming conflict. 
            }
            bool? overwrite;
            if (toolsToBeOverwrittenTitles.Count >0 || existingReports.Count > 0)
                overwrite = shouldOverwrite(toolsToBeOverwrittenTitles, existingReports);
            else
            {                
                overwrite = true;
            }
            if (overwrite == null)
            {
                DirectoryEx.SafeDelete(tempToolPath);
                return null;
            }
                
            if (overwrite == true)
            {
                //Delete the tools and their containing folder
                foreach (var tool in toolsToBeOverwritten)
                {
                    Settings.Default.ToolList.Remove(tool);
                }
                DirectoryEx.SafeDelete(permToolPath);
                
                //Import all reports. 
                foreach (ReportSpec item in existingReports)
                {
                    Settings.Default.ReportSpecList.RemoveKey(item.GetKey());
                    Settings.Default.ReportSpecList.Add(item);
                }
            }
            foreach (ReportSpec item in newReports)
            {
                Settings.Default.ReportSpecList.Add(item); //What if they have two  files with conflicting report titles. //Todo: (danny) make sure we handle this.
            }
            var reportRenameMapping = new Dictionary<string, string>();
            if (overwrite == false) // Dont overwrite so rename reports.
            {
                permToolPath = GetNewDirName(permToolPath);
                //Deal with renaming reports!
                foreach (ReportSpec item in existingReports)
                {
                    string oldname = item.GetKey();
                    string newname = GetUniqueReportName(oldname);
                    reportRenameMapping.Add(oldname,newname);
                    Settings.Default.ReportSpecList.Add((ReportSpec) item.ChangeName(newname));
                }
            }

            var permToolDir = new DirectoryInfo(permToolPath);
            if (!permToolDir.Exists)
                permToolDir.Create();
             
            foreach (FileInfo file in toolInf.GetFiles("*.properties")) // not L10N
            {
                FileStream fileStream = null;
                try
                {
                    fileStream = new FileStream(file.FullName, FileMode.Open);
                }
                catch (Exception)
                {
                    //Error opening the .properties file
                    retval.AddMessage(string.Format(Resources.ConfigureToolsDlg_unpackZipTool_Failed_to_read_file_0_The_tool_described_failed_to_import, file.Name));
                    if (fileStream != null)
                        fileStream.Dispose();
                    continue;
                }

                ExternalToolReadIn readin;
                try
                {
                    readin = new ExternalToolReadIn(fileStream);
                }
                catch (Exception)
                {
                    //Failed to read the .properties file
                    retval.AddMessage(string.Format(Resources.ConfigureToolsDlg_unpackZipTool_Failed_to_process_file_0_The_tool_described_failed_to_import,
                                                  file.Name));
                    fileStream.Dispose();
                    continue;
                }
                if (readin.Title == null || readin.Command == null)
                {
                    retval.AddMessage(string.Format(TextUtil.LineSeparate(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                        Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                        Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), file.Name));
                    fileStream.Dispose();
                    continue;
                }

                string command = readin.Command.Trim();
                var programPathContainer = ToolMacros.IsProgramPathMacro(command);
                if (!ToolDescription.IsWebPageCommand(command) && programPathContainer == null)
                {
                    //Consider: They might put $(ToolDir)\\<PathToExe> for the command and this wouldn't work with that.
                    try
                    {
                        command = CopyinFile(command, tempToolPath, permToolPath, readin.Title);
                    }
                    catch (Exception e)
                    {
                        fileStream.Dispose();
                        retval.AddMessage(e.Message);
                        continue;
                    }
                    command = command.Replace(permToolPath, ToolMacros.TOOL_DIR);                    
                }
                else if (programPathContainer != null)
                {
                    string path = null;
                    if (Settings.Default.FilePaths.ContainsKey(programPathContainer))
                        path = Settings.Default.FilePaths[programPathContainer];

                    if (path == null)
                    {
                        path = findProgramPath(programPathContainer);
                        // Path gets saved in the locate file dlg.
                    }
                    if (path == null)
                        return null;
                }

                string reportTitle = readin.Input_Report_Name;
                // Check we have the relevant report
                if (!string.IsNullOrWhiteSpace(reportTitle))
                {
                    if (reportRenameMapping.ContainsKey(reportTitle))
                    {
                        //Apply report renaming if install in parallel was selectedd
                        reportTitle = reportRenameMapping[reportTitle];                        
                    }
                    // Check if they are still missing the report they want
                    if (!Settings.Default.ReportSpecList.ContainsKey(reportTitle))
                    {
                        retval.AddMessage(string.Format("The tool \"{0}\" requires report type titled \"{1}\" and it is not provided. Import canceled.", readin.Title, reportTitle));
                        fileStream.Dispose();
                        continue;
                    }
                }

                string args = readin.Arguments;
                if (args.Contains("$(ToolDir)"))
                {
                    Regex r = new Regex(@"\$\(ToolDir\)(\S*)");
                    MatchCollection matches = r.Matches(args);
                    bool broke = false;
                    foreach (Match match in matches)
                    {
                        if (match.Groups[1].Value[0] == '\\')
                        {
                            //Import the file!
                            string filename = match.Groups[1].Value.TrimStart('\\');
                            try
                            {
                                CopyinFile(filename, tempToolPath, permToolPath, readin.Title);
                            }
                            catch (Exception e)
                            {
                                retval.AddMessage(e.Message);
                                fileStream.Dispose();
                                broke = true;
                                break;
                            }
                        }
                    }
                    if (broke)
                        continue;
                }
               
                //Import ArgsCollector Dll.
                string dllPath = readin.Args_Collector_Dll;
                if (!string.IsNullOrEmpty(dllPath))
                {
                    try
                    {
                        dllPath = CopyinFile(dllPath, tempToolPath, permToolPath, readin.Title);
                    }
                    catch (Exception e)
                    {
                        //Consider: How to handle this scenario (.dll file not provided in zip foler).
                        retval.AddMessage(e.Message);
                        fileStream.Dispose();
                        continue;
                    }
                }
                //Make sure tools get a unique title.
                string uniqueTitle = GetUniqueToolTitle(readin.Title);

                //Append each tool to the return value
                retval.AddTool(new ToolDescription(uniqueTitle, command, args, readin.Initial_Directory,
                    readin.Output_to_Immediate_Window.Contains("True"), reportTitle, dllPath, readin.Args_Collector_Type, permToolPath)); //Not L10N
                                
                Settings.Default.ToolList.Add(new ToolDescription(uniqueTitle, command, args,
                                                                    readin.Initial_Directory,
                                                                    readin.Output_to_Immediate_Window.Contains("True"), //Not L10N
                                                                    reportTitle, dllPath,
                                                                    readin.Args_Collector_Type, permToolPath));                                  
                
                fileStream.Close();

            } // Done looping through .properties files.

            // Remove the folder in tempfolder.
            if (tempToolPath.Contains("Temp")) // Minor sanity check //Not L10N
            {
                DirectoryEx.SafeDelete(tempToolPath);
            }

            if (retval.ValidToolsFound.Count == 0)
            {
                // No valid tools were found
                if (Directory.Exists(permToolPath))
                {
                    DirectoryEx.SafeDelete(permToolPath);                    
                }
            }

            /* Consider: Add a bool for sucessful import and add a dialog box that 
             * offers to remove the zip if the import was successful. */
            return retval;
        }

        public static string GetUniqueVersion(string formatString, Func<string, bool> isUnique)
        {           
            int i = 1;
            do
            {
                if (isUnique(string.Format(formatString, i)))
                {
                    return string.Format(formatString, i);
                }
                i++;                
            } while (true);           
        }

        private static string GetUniqueToolTitle(string s)
        {
            if (Settings.Default.ToolList.All(item => item.Title != (s)))
                return s;
            string formatstring = string.Concat(s, "{0}");
            return GetUniqueVersion(formatstring, value => Settings.Default.ToolList.All(item => item.Title != (value)));
        }

        private static string GetUniqueReportName(string key)
        {
            if (!Settings.Default.ReportSpecList.ContainsKey(key))
            {
                return key;
            }
            string formatstring = string.Concat(key, "{0}");
            return GetUniqueVersion(formatstring, value => !Settings.Default.ReportSpecList.ContainsKey(value));
        }
        
        private static string GetNewDirName(string permToolPath)
        {
            if (!Directory.Exists(permToolPath))
            {
                return permToolPath;
            }
            string formatstring = string.Concat(permToolPath, "{0}");
            return GetUniqueVersion(formatstring, value => !Directory.Exists(value));
        }        

        public class UnzipToolReturnAccumulator
        {
            public List<ToolDescription> ValidToolsFound { get; private set; }
            public List<string> MessagesThrown { get; private set; }

            public UnzipToolReturnAccumulator()
            {
                ValidToolsFound = new List<ToolDescription>();
                MessagesThrown = new List<string>();
            }

            public void AddMessage(string s)
            {
                MessagesThrown.Add(s);
            }

            public void AddTool(ToolDescription t)
            {
                ValidToolsFound.Add(t);
            }
        }

        private static string CopyinFile(string filename, string filedir, string desdir, string toolTitle)
        {
            string filescr = Path.Combine(filedir, filename);

            if (!File.Exists(filescr))
            {                
                throw new Exception(string.Format(Resources.ConfigureToolsDlg_CopyinFile_Missing_the_file_0_Tool_1_Import_Failed, filename, toolTitle));             
            }
            string filedest = Path.Combine(desdir, filename);
            try
            {
                File.Copy(filescr, filedest);
            }
            catch (IOException)
            {
                FileInfo dest = new FileInfo(filedest);
                FileInfo src = new FileInfo(filescr);

                if (!FilesAreEqual_OneByte(dest, src))
                {                    
                    throw new Exception(string.Format(TextUtil.LineSeparate(
                        Resources.ConfigureToolsDlg_CopyinFile_A_file_named_0_already_exists_that_isn_t_identical_to_the_one_for_tool__1,
                        Resources.ConfigureToolsDlg_CopyinFile_Not_importing_this_tool),
                        filename, toolTitle));
                }
            }
            return filedest;
        }

        /// <summary>
        /// Check if two files are the same byte for byte.
        /// </summary>
        static bool FilesAreEqual_OneByte(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                for (int i = 0; i < first.Length; i++)
                {
                    if (fs1.ReadByte() != fs2.ReadByte())
                        return false;
                }
            }
            return true;
        }        
    }
}