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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
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
        ///  Return a string that is the InitialDirectoy string with the macros replaced.
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

        private string GetCommand(SrmDocument doc, IToolMacroProvider toolMacroProvider, IProgressMonitor progressMonitor)
        {
            return ToolMacros.ReplaceMacrosCommand(doc, toolMacroProvider, this, progressMonitor);            
        }
        
        /// <summary>
        /// Run the tool. When you call run tool. call it on a different thread. 
        /// </summary>       
        /// <param name="document"> The document to base reports off of. </param>
        /// <param name="toolMacroProvider"> Interface for replacing Tool Macros with the correct strings. </param>
        /// <param name="textWriter"> A textWriter to write to when the tool redirects stdout. (eg. Outputs to an Immediate Window) </param>
        /// <param name="progressMonitor"> Progress monitor. </param>
        /// <param name="parent">A parent control to invoke to display args collectors in, if necessary. Can be null. </param>
        public void RunTool(SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent) 
        {
            if (Annotations != null && Annotations.Count != 0)
            {
                VerifyAnnotations(document);
            }

            if (IsWebPage)
            {
                if (Equals(Title, ToolList.DEPRECATED_QUASAR.Title) && Equals(Command, ToolList.DEPRECATED_QUASAR.Command))
                {
                    throw new ToolDeprecatedException(TextUtil.LineSeparate(
                        Resources.ToolDescription_RunTool_Support_for_the_GenePattern_version_of_QuaSAR_has_been_discontinued_,
                        Resources.ToolDescription_RunTool_Please_check_the_External_Tools_Store_on_the_Skyline_web_site_for_the_most_recent_version_of_the_QuaSAR_external_tool_));
                }
                var webHelpers = WebHelpers ?? new WebHelpers();

                string url = GetUrl(document, toolMacroProvider, progressMonitor);
                if (string.IsNullOrEmpty(url))
                    return;

                if (string.IsNullOrEmpty(ReportTitle))
                {
                    webHelpers.OpenLink(url);
                }
                else // It has a selected report that must be posted. 
                {
                    PostToLink(url, document, progressMonitor, webHelpers);
                }
            }
            else // Not a website. Needs its own thread.
            {
                if (Arguments.Contains(@"$(SkylineConnection)"))
                {
                    Program.StartToolService();
                }

                // To eliminate a cross thread error make a copy of the IToolMacroProvider.
                IToolMacroProvider newToolMacroProvider = new CopyToolMacroProvider(toolMacroProvider);
                RunExecutable(document, newToolMacroProvider, textWriter, progressMonitor, parent);                
            }           
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

        private void VerifyAnnotations(SrmDocument document)
        {
            var missingAnnotations = new List<string>();
            var uncheckedAnnotations = new List<string>();
            foreach (AnnotationDef annotationDef in Annotations)
            {
                AnnotationDef existingAnnotation;
                if (!Settings.Default.AnnotationDefList.TryGetValue(annotationDef.GetKey(), out existingAnnotation))
                {
                    missingAnnotations.Add(annotationDef.GetKey());
                }
                else
                {
                    existingAnnotation = document.Settings.DataSettings.AnnotationDefs.FirstOrDefault(
                        a => Equals(a.GetKey(), annotationDef.GetKey()));
                    // Assume annotations are equivalent, if they exist in the document, based on the
                    // test above in the if () clause
                    if (existingAnnotation == null)
                        uncheckedAnnotations.Add(annotationDef.GetKey());
                }
            }

            if (missingAnnotations.Count != 0)
            {
                throw new ToolExecutionException(TextUtil.LineSeparate(Resources.ToolDescription_VerifyAnnotations_This_tool_requires_the_use_of_the_following_annotations_which_are_missing_or_improperly_formatted, 
                                                                  string.Empty, 
                                                                  TextUtil.LineSeparate(missingAnnotations), 
                                                                  string.Empty, 
                                                                  Resources.ToolDescription_VerifyAnnotations_Please_re_install_the_tool_and_try_again_));

            }

            if (uncheckedAnnotations.Count != 0)
            {
                throw new ToolExecutionException(TextUtil.LineSeparate(Resources.ToolDescription_VerifyAnnotations_This_tool_requires_the_use_of_the_following_annotations_which_are_not_enabled_for_this_document,
                                                                 string.Empty, 
                                                                 TextUtil.LineSeparate(uncheckedAnnotations), 
                                                                 string.Empty,
                                                                 Resources.ToolDescription_VerifyAnnotations_Please_enable_these_annotations_and_fill_in_the_appropriate_data_in_order_to_use_the_tool_));
            }
        }

        private void PostToLink(string url, SrmDocument doc, IProgressMonitor progressMonitor, IWebHelpers webHelpers)
        {
            ActionUtil.RunAsync(() =>
            {
                try
                {
                    PostToLinkBackground(url, doc, progressMonitor, webHelpers);
                }
                catch (Exception exception)
                {
                    progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(exception));
                }
            }, @"Post To Link");
        }

        private void PostToLinkBackground(string url, SrmDocument doc, IProgressMonitor progressMonitor, IWebHelpers webHelpers)
        {
            StringWriter report = new StringWriter();
            ToolDescriptionHelpers.GetReport(doc, ReportTitle, Title, progressMonitor, report);
            webHelpers.PostToLink(url, report.ToString());
        }

        private void RunExecutable(SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent)
        {
            ActionUtil.RunAsync(() =>
            {
                try
                {
                    RunExecutableBackground(document, toolMacroProvider, textWriter, progressMonitor, parent);
                }
                catch (Exception e)
                {
                    progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(e));
                }
            }, @"Run Executable");
        }

        /// <summary>
        ///  Method used to encapsulate the running of a executable for threading.
        /// </summary>
        /// <param name="document"> Contains the document to base reports off of, as well as to serve as the parent for args collector forms. </param>
        /// <param name="toolMacroProvider"> Interface for determining what to replace macros with. </param>
        /// <param name="textWriter"> A textWriter to write to if outputting to the immediate window. </param>
        /// <param name="progressMonitor"> Progress monitor. </param>
        /// <param name="parent">If there is an Args Collector form, it will be showed on this control. Can be null. </param>
        private void RunExecutableBackground(SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent)
        {                                                
            // Need to know if $(InputReportTempPath) is an argument to determine if a report should be piped to stdin or not.
            bool containsInputReportTempPath = Arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH);
            string command = GetCommand(document, toolMacroProvider, progressMonitor);
            if (command == null) // Has already thrown the error.
                return;
            string args = GetArguments(document, toolMacroProvider, progressMonitor);
            string initDir = GetInitialDirectory(document, toolMacroProvider, progressMonitor); // If either of these fails an Exception is thrown.
                                    
            if (args != null && initDir != null)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(command, args) {WorkingDirectory = initDir};
                if (OutputToImmediateWindow)
                {
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.CreateNoWindow = true;
                    startInfo.UseShellExecute = false;
                    startInfo.StandardOutputEncoding = Encoding.UTF8;
                    startInfo.StandardErrorEncoding = Encoding.UTF8;
                }

                // if it has a selected report title and its doesn't have a InputReportTempPath macro then the report needs to be piped to stdin.
                TextReader reportReader;
                string reportCsvPath = null;
                if (string.IsNullOrEmpty(ReportTitle))
                {
                    reportReader = null;
                }
                else
                {
                    if (containsInputReportTempPath)
                    {
                        reportCsvPath = ToolMacros.GetReportTempPath(ReportTitle, Title);
                        reportReader = new StreamReader(reportCsvPath);
                    }
                    else
                    {
                        var stringWriter = new StringWriter();
                        ToolDescriptionHelpers.GetReport(document, ReportTitle, Title, progressMonitor, stringWriter);
                        startInfo.RedirectStandardInput = true;
                        reportReader = new StringReader(stringWriter.ToString());
                    }
                }
                using (reportReader)
                {
                    //If there is an IToolArgsCollector run it!
                    if (!string.IsNullOrEmpty(ArgsCollectorDllPath) && !string.IsNullOrEmpty(ArgsCollectorClassName))
                    {
                        if (!CallArgsCollector(parent, args, reportReader, startInfo))
                            return;
                    }


                    Process p = new Process {StartInfo = startInfo};
                    if (OutputToImmediateWindow)
                    {
                        p.EnableRaisingEvents = true;
                        TextBoxStreamWriterHelper boxStreamWriterHelper = textWriter as TextBoxStreamWriterHelper;
                        if (boxStreamWriterHelper == null)
                        {
                            p.OutputDataReceived += (sender, dataReceivedEventArgs)
                                => textWriter.WriteLine(p.Id + @">" + dataReceivedEventArgs.Data);
                            p.ErrorDataReceived += (sender, dataReceivedEventArgs)
                                => textWriter.WriteLine(p.Id + @">" + dataReceivedEventArgs.Data);
                        }
                        else
                        {
                            p.OutputDataReceived += (sender, dataReceivedEventArgs) =>
                                boxStreamWriterHelper.WriteLineWithIdentifier(p.Id, dataReceivedEventArgs.Data);
                            p.ErrorDataReceived += (sender, dataReceivedEventArgs) =>
                                boxStreamWriterHelper.WriteLineWithIdentifier(p.Id, dataReceivedEventArgs.Data);
                            //p.Refresh();
                            p.Exited += (sender, processExitedEventArgs) =>
                                boxStreamWriterHelper.HandleProcessExit(p.Id);
                        }
                        // ReSharper disable LocalizableElement
                        textWriter.WriteLine("\"" + p.StartInfo.FileName + "\" " + p.StartInfo.Arguments);
                        // ReSharper restore LocalizableElement
                    }
                    try
                    {
                        p.StartInfo.UseShellExecute = false;
                        p.Start();
                        if (OutputToImmediateWindow)
                        {
                            p.BeginOutputReadLine();
                            p.BeginErrorReadLine();
                        }

                        // write the reportCsv string to stdin.
                        // need to only check one of these conditions.
                        if (startInfo.RedirectStandardInput && reportReader != null)
                        {
                            StreamWriter streamWriter = p.StandardInput;
                            if (reportCsvPath != null)
                            {
                                using (var newReader = new StreamReader(reportCsvPath))
                                {
                                    streamWriter.Write(newReader.ReadToEnd());
                                }
                            }
                            else
                            {
                                streamWriter.Write(reportReader.ReadToEnd());
                            }
                            streamWriter.Flush();
                            streamWriter.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is Win32Exception)
                        {
                            throw new ToolExecutionException(
                                TextUtil.LineSeparate(
                                    Resources.ToolDescription_RunTool_File_not_found_,
                                    command,
                                    Resources
                                        .ToolDescription_RunTool_Please_check_the_command_location_is_correct_for_this_tool_),
                                ex);
                        }
                        else
                        {
                            throw new ToolExecutionException(
                                TextUtil.LineSeparate(
                                    Resources.ToolDescription_RunTool_Please_reconfigure_that_tool__it_failed_to_execute__,
                                    ex.Message),
                                ex);
                        }
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

        private bool CallArgsCollector(Control parent, string args, TextReader reportReader, ProcessStartInfo startInfo)
        {
            string oldArgs = PreviousCommandLineArgs;
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(ArgsCollectorDllPath);
            }
            catch (Exception x)
            {
                throw new ToolExecutionException(
                    string.Format(
                        Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool_0_It_seems_to_be_missing_a_file__Please_reinstall_the_tool_and_try_again_,
                        Title),
                    x);
            }

            Type type = assembly.GetType(ArgsCollectorClassName);
            if (type == null)
            {
                throw new ToolExecutionException(
                    string.Format(Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool__0___It_seems_to_have_an_error_in_one_of_its_files__Please_reinstall_the_tool_and_try_again,
                        Title));
            }


            var methodInfo = FindArgsCollectorMethod(type);
            var parameterInfos = methodInfo.GetParameters();
            if (parameterInfos.Length != 3)
            {
                throw new ToolExecutionException(
                    string.Format(Resources.ToolDescription_CallArgsCollector_Error_running_the_installed_tool__0___The_method___1___has_the_wrong_signature_,
                    Title, methodInfo.Name));
            }
            object reportArgument;
            if (parameterInfos[1].ParameterType == typeof(string))
            {
                reportArgument = reportReader == null ? null : reportReader.ReadToEnd();
            }
            else
            {
                reportArgument = reportReader;
            }
            object[] collectorArgs =
            {
                parent,
                reportArgument,
                oldArgs != null ? CommandLine.ParseArgs(oldArgs) : null
            };
            object answer;

            try
            {
                if (parent == null)
                {
                    answer = methodInfo.Invoke(null, collectorArgs);
                }
                else
                {
                    // if there is a control given, use it to invoke the args collector form with that control as its parent. 
                    // Otherwise just invoke the form by itself
                    answer = parent.Invoke(new Func<string[]>(() => (string[]) methodInfo.Invoke(null, collectorArgs)));
                }
            }
            catch (Exception x)
            {
                string message = x.Message;
                if (string.IsNullOrEmpty(message))
                {
                    throw new ToolExecutionException(
                        string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error_, Title),
                        x);
                }
                else
                {
                    throw new ToolExecutionException(
                        TextUtil.LineSeparate(
                            string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error__it_returned_the_message_, Title),
                            message),
                        x);
                }
            }
            string[] commandLineArguments = answer as string[];
            if (commandLineArguments != null)
            {
                // Parse
                string argString = PreviousCommandLineArgs = CommandLine.JoinArgs(commandLineArguments);
                // Append to end of argument string
                if (args.Contains(ToolMacros.COLLECTED_ARGS))
                {
                    startInfo.Arguments = args.Replace(ToolMacros.COLLECTED_ARGS, argString);
                }
                else
                {
                    startInfo.Arguments = args + @" " + argString;
                }
            }
            else
            {
                /*Establish an expectation that if an args collector returns null then there was some error
                         * and the args collector displayed the relevant error and our job is to just terminate tool execution
                         * If they would like the tool to run with no extra args they could return String.Empty
                         */
                return false;
            }
            return true;
        }

        public MethodInfo FindArgsCollectorMethod(Type type)
        {
            // ReSharper disable LocalizableElement
            var textReaderArgs = new[] { typeof(IWin32Window), typeof(TextReader), typeof(string[]) };
            var stringArgs = new[] {typeof(IWin32Window), typeof(string), typeof(string[])};
            MethodInfo methodInfo = SafeGetMethod(type, "CollectArgs", textReaderArgs)
                   ?? SafeGetMethod(type, "CollectArgsReader", textReaderArgs)
                   ?? SafeGetMethod(type, "CollectArgs", stringArgs);
            if (methodInfo != null)
            {
                return methodInfo;
            }
            Exception innerException = null;
            try
            {
                methodInfo = type.GetMethod("CollectArgs");
            }
            catch (Exception e)
            {
                innerException = e;
            }
            // ReSharper restore LocalizableElement
            if (methodInfo != null)
            {
                return methodInfo;
            }

            throw new ToolExecutionException(
                TextUtil.LineSeparate(
                    string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error__it_returned_the_message_,
                        Title),
                    Resources
                        .ToolDescription_FindArgsCollectorMethod_Unable_to_find_any_CollectArgs_method_to_call_on_class___0___), innerException);
        }

        private static MethodInfo SafeGetMethod(Type type, string methodName, Type[] args)
        {
            try
            {
                return type.GetMethod(methodName, args);
            }
            catch
            {
                return null;
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
                viewContext.GetCsvWriter()))
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
