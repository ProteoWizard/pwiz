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
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
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
            : this(t.Title, t.Command, t.Arguments, t.InitialDirectory, t.OutputToImmediateWindow, t.ReportTitle)
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
        {
            Title = title;
            Command = command;
            Arguments = arguments ?? string.Empty;
            InitialDirectory = initialDirectory ?? string.Empty;
            OutputToImmediateWindow = outputToImmediateWindow;
            ReportTitle = reportTitle;

//            Validate();
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

        public bool IsWebPage { get { return IsWebPageCommand(Command); } }

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
                var webHelpers = WebHelpers;
                if (webHelpers == null)
                    webHelpers = new WebHelpers();

                if (String.IsNullOrEmpty(ReportTitle))
                {
                    webHelpers.OpenLink(Command);
                }
                else // It has a selected report that must be posted. 
                {
                    string report = ToolDescriptionHelpers.GetReport(doc, ReportTitle, Title);
                    if (report != null)
                        webHelpers.PostToLink(Command, report);                    
                }                              
            }
            else // Not a website. Needs its own thread.
            {                
                // To eliminate a cross thread error make a copy of the IToolMacroProvider.
                IToolMacroProvider newToolMacroProvider = new CopyToolMacroProvider(toolMacroProvider);
                RunExecutable(doc, newToolMacroProvider, textWriter, exceptionHandler);                
            }           
        }    

        public Thread RunExecutable(SrmDocument doc, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IExceptionHandler exceptionHandler)
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
            string args = GetArguments(doc, toolMacroProvider, exceptionHandler);
            string initDir = GetInitialDirectory(doc, toolMacroProvider, exceptionHandler); // If either of these fails an Exception is thrown.
            if (args != null && initDir != null)
            {
                ProcessStartInfo startInfo = OutputToImmediateWindow
                                          ? new ProcessStartInfo(Command, args) { WorkingDirectory = initDir, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true}
                                          : new ProcessStartInfo(Command, args) { WorkingDirectory = initDir };

                // if it has a selected report title and its doesn't have a InputReportTempPath macro then the report needs to be piped to stdin.
                string reportCsv = null;
                if (!String.IsNullOrEmpty(ReportTitle) && !containsInputReportTempPath) // Then pipe to stdin.
                {
                    reportCsv = ToolDescriptionHelpers.GetReport(doc, ReportTitle, Title);
                    startInfo.RedirectStandardInput = true;
                }

                Process p = new Process { StartInfo = startInfo };

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
            report_title
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
                    Equals(ReportTitle, tool.ReportTitle);
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
                return result;
            }
        }

        #endregion
    }

    public static class ToolMacros
    {
        public const string INPUT_REPORT_TEMP_PATH = "$(InputReportTempPath)";

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
                new Macro(Resources.ToolMacros__listArguments_Input_Report_Temp_Path, INPUT_REPORT_TEMP_PATH, GetReportTempPath , Resources.ToolMacros_listArguments_This_tool_requires_a_selected_report)
            };

        // Macros for InitialDirectory.
        public static Macro[] _listInitialDirectory = new[]
            {
                new Macro(Resources.ToolMacros__listArguments_Document_Directory, "$(DocumentDir)", GetDocumentDir, Resources.ToolMacros__listArguments_This_tool_requires_a_Document_Directory_to_run) 
            };

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

            string arguments = tool.Arguments;
            foreach (Macro macro in _listArguments)
            {
                if (arguments.Contains(macro.ShortText))
                {
                    string contents = macro.GetContents(new ToolMacroInfo(toolMacroProvider, tool.Title, tool.ReportTitle, doc));
                    if (contents == null)
                    {
                        exceptionHandler.HandleException(new Exception(macro.ErrorMessage));
                        return null;
                    }
                    else
                    {
                        arguments = arguments.Replace(macro.ShortText, contents);    
                    }     
                }                               
            }
            return arguments; 
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
            string initialDirectory = tool.InitialDirectory;
            foreach (Macro macro in _listInitialDirectory)
            {
                if (initialDirectory.Contains(macro.ShortText))
                {
                    string contents;
                    if (macro.PlainText == Resources.ToolMacros__listArguments_Input_Report_Temp_Path)
                    {
                        try // InputReportTempPath throws more specific exceptions, this case deals with those.
                        {
                             contents = macro.GetContents(new ToolMacroInfo(toolMacroProvider, tool.Title, tool.ReportTitle, doc));
                            tool.ReportTempPath_toDelete = contents;                            
                        }
                        catch(Exception e)
                        {
                            exceptionHandler.HandleException(e);
                            return null;
                        }                        
                    }
                    else
                    {
                        contents = macro.GetContents(new ToolMacroInfo(toolMacroProvider, tool.Title, tool.ReportTitle, doc));                        
                    }
                    if (contents == null)
                    {
                        exceptionHandler.HandleException(new Exception(macro.ErrorMessage));
                        return null;
                    }    
                    initialDirectory = initialDirectory.Replace(macro.ShortText, contents);                    
                }
            }
            return initialDirectory;
        }

        //Save the report to a temp file and return the path to that file. 
        private static string GetReportTempPath(ToolMacroInfo toolMacroInfo)
        {
            SrmDocument doc = toolMacroInfo.Doc;            
            string reportName = toolMacroInfo.ReportName;
            string toolTitle = toolMacroInfo.ToolTitle;
            if (String.IsNullOrEmpty(reportName))
            {                
                throw new Exception(String.Format(Resources.ToolMacros_GetReportTempPath_The_selected_tool_0_requires_a_selected_report_Please_select_a_report_for_this_tool_, toolTitle));
            }

            string reportFileName = reportName.Replace(' ', '_');
            string toolFileName = toolTitle.Replace(' ', '_');

            string tempFilePath = Path.Combine(Path.GetTempPath(), toolFileName + "_" + reportFileName + ".csv");


            string report = ToolDescriptionHelpers.GetReport(doc, reportName, toolTitle);
            
            if (report!=null)
            {
                try
                {
                    using (var saver = new FileSaver(tempFilePath))
                    {
                        if (!saver.CanSave(false))
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

        public ToolMacroInfo(IToolMacroProvider macroProvider, string toolTitle, string reportName, SrmDocument document)
        {
            _macroProvider = macroProvider;
            ToolTitle = toolTitle;
            ReportName = reportName;
            Doc = document;
        }

        public string ToolTitle { get; private set; }
        public string ReportName { get; private set; }
        public SrmDocument Doc { get; private set; }

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
        }

        #region Implementation of IToolMacroProvider

        public string DocumentFilePath { get; private set; }

        public string SelectedProteinName { get; private set; }

        public string SelectedPeptideSequence { get; private set; }

        public string SelectedPrecursor { get; private set; }

        public string ResultNameCurrent { get; private set; }

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
        /// <param name="doc"> Document to create the report from. </param>                
        /// <param name="reportTitle"> Title of the reportSpec to make a report from. </param>
        /// <param name="toolTitle"> Title of tool for exception error message. </param>
        /// <returns> Returns a string representation of the ReportTitle report, or throws an error that the reportSpec no longer exist. </returns>
        public static string GetReport(SrmDocument doc, string reportTitle, string toolTitle)
        {
            return GetReport(doc, reportTitle, toolTitle, null);
        }

        public static string GetReport(SrmDocument doc, string reportTitle, string toolTitle, IExceptionHandler exceptionHandler)
        {
            ReportSpec reportSpec = Settings.Default.GetReportSpecByName(reportTitle);
            if (reportSpec == null)
            {
                // Complain that the report no longer exist.
                Exception e = new Exception(string.Format(
                        Resources.
                            ToolDescriptionHelpers_GetReport_Error_0_requires_a_report_titled_1_which_no_longer_exists__Please_select_a_new_report_or_import_the_report_format,
                        toolTitle, reportTitle));
                if (exceptionHandler == null)
                {
                    throw e;
                }
                else exceptionHandler.HandleException(e);
            }
            // At this point the null case should have been dealt with.
            Debug.Assert(reportSpec != null, "reportSpec != null"); // Change to Util.Assume?
            if (reportSpec != null) return reportSpec.ReportToCsvString(doc);
            return null;
        }
    }
}