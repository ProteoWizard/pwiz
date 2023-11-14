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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace pwiz.Skyline.ToolsUI
{
    public static class ToolDescriptionRunUI
    {
        /// <summary>
        /// Run the tool. When you call run tool. call it on a different thread. 
        /// </summary>
        /// <param name="toolDesc">The <see cref="ToolDescription"/> object to run.</param>
        /// <param name="document"> The document to base reports off of. </param>
        /// <param name="toolMacroProvider"> Interface for replacing Tool Macros with the correct strings. </param>
        /// <param name="textWriter"> A textWriter to write to when the tool redirects stdout. (eg. Outputs to an Immediate Window) </param>
        /// <param name="progressMonitor"> Progress monitor. </param>
        /// <param name="parent">A parent control to invoke to display args collectors in, if necessary. Can be null. </param>
        public static void RunTool(this ToolDescription toolDesc, SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent)
        {
            if (toolDesc.Annotations != null && toolDesc.Annotations.Count != 0)
            {
                toolDesc.VerifyAnnotations(document);
            }

            if (toolDesc.IsWebPage)
            {
                if (Equals(toolDesc.Title, ToolList.DEPRECATED_QUASAR.Title) && Equals(toolDesc.Command, ToolList.DEPRECATED_QUASAR.Command))
                {
                    throw new ToolDeprecatedException(TextUtil.LineSeparate(
                        Resources.ToolDescription_RunTool_Support_for_the_GenePattern_version_of_QuaSAR_has_been_discontinued_,
                        Resources.ToolDescription_RunTool_Please_check_the_External_Tools_Store_on_the_Skyline_web_site_for_the_most_recent_version_of_the_QuaSAR_external_tool_));
                }
                var webHelpers = toolDesc.WebHelpers ?? new WebHelpers();

                string url = toolDesc.GetUrl(document, toolMacroProvider, progressMonitor);
                if (string.IsNullOrEmpty(url))
                    return;

                if (string.IsNullOrEmpty(toolDesc.ReportTitle))
                {
                    webHelpers.OpenLink(url);
                }
                else // It has a selected report that must be posted. 
                {
                    toolDesc.PostToLink(url, document, progressMonitor, webHelpers);
                }
            }
            else // Not a website. Needs its own thread.
            {
                if (toolDesc.Arguments.Contains(@"$(SkylineConnection)"))
                {
                    Program.StartToolService();
                }

                // To eliminate a cross thread error make a copy of the IToolMacroProvider.
                IToolMacroProvider newToolMacroProvider = new CopyToolMacroProvider(toolMacroProvider);
                toolDesc.RunExecutable(document, newToolMacroProvider, textWriter, progressMonitor, parent);
            }
        }

        private static void VerifyAnnotations(this ToolDescription toolDesc, SrmDocument document)
        {
            var missingAnnotations = new List<string>();
            var uncheckedAnnotations = new List<string>();
            foreach (AnnotationDef annotationDef in toolDesc.Annotations)
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

        private static void PostToLink(this ToolDescription toolDesc, string url, SrmDocument doc, IProgressMonitor progressMonitor, IWebHelpers webHelpers)
        {
            ActionUtil.RunAsync(() =>
            {
                try
                {
                    toolDesc.PostToLinkBackground(url, doc, progressMonitor, webHelpers);
                }
                catch (Exception exception)
                {
                    progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(exception));
                }
            }, @"Post To Link");
        }

        private static void PostToLinkBackground(this ToolDescription toolDesc, string url, SrmDocument doc, IProgressMonitor progressMonitor, IWebHelpers webHelpers)
        {
            StringWriter report = new StringWriter();
            ToolDescriptionHelpers.GetReport(doc, toolDesc.ReportTitle, toolDesc.Title, progressMonitor, report);
            webHelpers.PostToLink(url, report.ToString());
        }

        private static void RunExecutable(this ToolDescription toolDesc, SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent)
        {
            ActionUtil.RunAsync(() =>
            {
                try
                {
                    toolDesc.RunExecutableBackground(document, toolMacroProvider, textWriter, progressMonitor, parent);
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
        /// <param name="toolDesc">The <see cref="ToolDescription"/> object to run.</param>
        /// <param name="document"> Contains the document to base reports off of, as well as to serve as the parent for args collector forms. </param>
        /// <param name="toolMacroProvider"> Interface for determining what to replace macros with. </param>
        /// <param name="textWriter"> A textWriter to write to if outputting to the immediate window. </param>
        /// <param name="progressMonitor"> Progress monitor. </param>
        /// <param name="parent">If there is an Args Collector form, it will be showed on this control. Can be null. </param>
        private static void RunExecutableBackground(this ToolDescription toolDesc, SrmDocument document, IToolMacroProvider toolMacroProvider, TextWriter textWriter, IProgressMonitor progressMonitor, Control parent)
        {
            // Need to know if $(InputReportTempPath) is an argument to determine if a report should be piped to stdin or not.
            bool containsInputReportTempPath = toolDesc.Arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH);
            string command = toolDesc.GetCommand(document, toolMacroProvider, progressMonitor);
            if (command == null) // Has already thrown the error.
                return;
            string args = toolDesc.GetArguments(document, toolMacroProvider, progressMonitor);
            string initDir = toolDesc.GetInitialDirectory(document, toolMacroProvider, progressMonitor); // If either of these fails an Exception is thrown.

            if (args != null && initDir != null)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(command, args) { WorkingDirectory = initDir };
                if (toolDesc.OutputToImmediateWindow)
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
                if (string.IsNullOrEmpty(toolDesc.ReportTitle))
                {
                    reportReader = null;
                }
                else
                {
                    if (containsInputReportTempPath)
                    {
                        reportCsvPath = ToolMacros.GetReportTempPath(toolDesc.ReportTitle, toolDesc.Title);
                        reportReader = new StreamReader(reportCsvPath);
                    }
                    else
                    {
                        var stringWriter = new StringWriter();
                        ToolDescriptionHelpers.GetReport(document, toolDesc.ReportTitle, toolDesc.Title, progressMonitor, stringWriter);
                        startInfo.RedirectStandardInput = true;
                        reportReader = new StringReader(stringWriter.ToString());
                    }
                }
                using (reportReader)
                {
                    //If there is an IToolArgsCollector run it!
                    if (!string.IsNullOrEmpty(toolDesc.ArgsCollectorDllPath) && !string.IsNullOrEmpty(toolDesc.ArgsCollectorClassName))
                    {
                        if (!toolDesc.CallArgsCollector(parent, args, reportReader, startInfo))
                            return;
                    }


                    Process p = new Process { StartInfo = startInfo };
                    if (toolDesc.OutputToImmediateWindow)
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
                            boxStreamWriterHelper.AddFileForDeleteOnExit(toolDesc.ReportTempPath_toDelete);
                            toolDesc.ReportTempPath_toDelete = null;
                        }
                        // ReSharper disable LocalizableElement
                        textWriter.WriteLine("\"" + p.StartInfo.FileName + "\" " + p.StartInfo.Arguments);
                        // ReSharper restore LocalizableElement
                    }
                    try
                    {
                        p.StartInfo.UseShellExecute = false;
                        p.Start();
                        if (toolDesc.OutputToImmediateWindow)
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
                // (bspratt note Feb 2023) Actually the concern is for a race condition - the process that's using this
                // file might not get a chance to read it before this code deletes it.
                // Use TextBoxStreamWriterHelper.AddFileForDeleteOnExit instead.
                //                if (ReportTempPath_toDelete != null)
                //                {
                //                    FileEx.SafeDelete(ReportTempPath_toDelete, true);
                //                    ReportTempPath_toDelete = null;
                //                }  
            }
        }

        private static bool CallArgsCollector(this ToolDescription toolDesc, Control parent, string args, TextReader reportReader, ProcessStartInfo startInfo)
        {
            string oldArgs = toolDesc.PreviousCommandLineArgs;
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(toolDesc.ArgsCollectorDllPath);
            }
            catch (Exception x)
            {
                throw new ToolExecutionException(
                    string.Format(
                        Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool_0_It_seems_to_be_missing_a_file__Please_reinstall_the_tool_and_try_again_,
                        toolDesc.Title),
                    x);
            }

            Type type = assembly.GetType(toolDesc.ArgsCollectorClassName);
            if (type == null)
            {
                throw new ToolExecutionException(
                    string.Format(Resources.ToolDescription_RunExecutableBackground_Error_running_the_installed_tool__0___It_seems_to_have_an_error_in_one_of_its_files__Please_reinstall_the_tool_and_try_again,
                        toolDesc.Title));
            }


            var methodInfo = toolDesc.FindArgsCollectorMethod(type);
            var parameterInfos = methodInfo.GetParameters();
            if (parameterInfos.Length != 3)
            {
                throw new ToolExecutionException(
                    string.Format(Resources.ToolDescription_CallArgsCollector_Error_running_the_installed_tool__0___The_method___1___has_the_wrong_signature_,
                    toolDesc.Title, methodInfo.Name));
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
                    answer = parent.Invoke(new Func<string[]>(() => (string[])methodInfo.Invoke(null, collectorArgs)));
                }
            }
            catch (Exception x)
            {
                string message = x.Message;
                if (string.IsNullOrEmpty(message))
                {
                    throw new ToolExecutionException(
                        string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error_, toolDesc.Title),
                        x);
                }
                else
                {
                    throw new ToolExecutionException(
                        TextUtil.LineSeparate(
                            string.Format(Resources.ToolDescription_RunExecutableBackground_The_tool__0__had_an_error__it_returned_the_message_, toolDesc.Title),
                            message),
                        x);
                }
            }
            string[] commandLineArguments = answer as string[];
            if (commandLineArguments != null)
            {
                // Parse
                string argString = toolDesc.PreviousCommandLineArgs = CommandLine.JoinArgs(commandLineArguments);
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

        public static MethodInfo FindArgsCollectorMethod(this ToolDescription toolDesc, Type type)
        {
            // ReSharper disable LocalizableElement
            var textReaderArgs = new[] { typeof(IWin32Window), typeof(TextReader), typeof(string[]) };
            var stringArgs = new[] { typeof(IWin32Window), typeof(string), typeof(string[]) };
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
                        toolDesc.Title),
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
    }
}
