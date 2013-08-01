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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Ionic.Zip;
using Kajabity.Tools.Java;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Tools
{
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
            public const string PACKAGE = "Package{0}";                                 //Not L10N
            public const string ANNOTATION = "Annotation{0}";                           //Not L10N
            
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
        public string GetPackage(int p)
        {
            string lookup = string.Format(PropertiesConstants.PACKAGE, p);
            string package = _properties.GetProperty(lookup);
            return package != null ? package.Trim() : null;
        }
        public string GetAnnotation(int a)
        {
            string lookup = string.Format(PropertiesConstants.ANNOTATION, a);
            string annotation = _properties.GetProperty(lookup);
            return annotation != null ? annotation.Trim() : null;
        }
        #endregion //Accessors
    }

    public class ToolInstaller
    {
        private const string TOOL_INF = "tool-inf"; // Not L10N

        /// <summary>
        /// Function for unpacking zipped External tools.
        /// </summary>
        /// <param name="pathToZip">Path to the zipped file that contains the tool and all its assicaited files.</param> 
        /// <param name="shouldOverwriteAnnotations">Function that when given a list of annotations that would be overwritten by an installation,
        /// returns true or false for overwrite, and null for cancel installation</param>       
        /// <param name="shouldOverwrite">Function that when given a list of tools and a list of reports that would be removed by an installation
        ///  it returns a bool, true for overwrite, false for in parallel and null for cancel installation</param>
        /// <param name="installProgram">Function that finds a program path given a program path container.</param>
        /// <returns></returns>
        public static UnzipToolReturnAccumulator UnpackZipTool(string pathToZip,
                                                                Func<List<AnnotationDef>, bool?> shouldOverwriteAnnotations,                                                   
                                                                Func<List<string>, List<ReportSpec>, bool?> shouldOverwrite,
                                                                Func<ProgramPathContainer, ICollection<string>,string> installProgram)
        {
            UnzipToolReturnAccumulator retval = new UnzipToolReturnAccumulator();
            string name = Path.GetFileNameWithoutExtension(pathToZip);
            if (name == null)
            {                
                throw new MessageException(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_file_selected__No_tools_added_);
            }
            //This helps with zipfiles that have spaces in the titles. 
            //Consider: We may want to add quotes around usages of the $(ToolDir) macro incase the Tool directory has spaces in one of its directory names.
            name = name.Replace(' ', '_'); 

            string outerToolsFolderPath = ToolDescriptionHelpers.GetToolsDirectory();
            if (string.IsNullOrEmpty(outerToolsFolderPath))
            {
                throw new MessageException(Resources.ConfigureToolsDlg_unpackZipTool_Error_unpacking_zipped_tools);
            }
            string tempFolderPath = Path.Combine(outerToolsFolderPath, "Temp"); //Not L10N

            ZipFile zipFile = new ZipFile(pathToZip);
            
            DirectoryInfo toolDir = new DirectoryInfo(tempFolderPath);
            if (!toolDir.Exists)
                toolDir.Create();

            
            string tempToolPath = Path.Combine(tempFolderPath, name);
            if (Directory.Exists(tempToolPath))
                tempToolPath = GetNewDirName(tempToolPath); 
            //This naming conflict shouldn't happen. The temp file should be empty.
            //Consider: Try to delete the existing directory in the temp directory.
            try
            {
                zipFile.ExtractAll(tempToolPath, ExtractExistingFileAction.OverwriteSilently);                
            }
            catch (Exception)
            {
                zipFile.Dispose();
                if (Directory.Exists(tempToolPath))
                    DirectoryEx.SafeDelete(tempToolPath);
                throw new MessageException(Resources.ConfigureToolsDlg_unpackZipTool_There_is_a_naming_conflict_in_unpacking_the_zip__Tool_importing_canceled_);
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

            DirectoryInfo toolInf = new DirectoryInfo(Path.Combine(tempToolPath, TOOL_INF));
            if (!toolInf.Exists)
            {
                DirectoryEx.SafeDelete(tempToolPath);
                throw new MessageException(TextUtil.LineSeparate(Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_an_installable_tool_, Resources.ToolInstaller_UnpackZipTool_Error__It_does_not_contain_the_required_tool_inf_directory_));
            }

            var srmSerialzer = new XmlSerializer(typeof (SrmDocument));
            var conflictAnnotations = new List<AnnotationDef>();
            var newAnnotations = new List<AnnotationDef>();
            // Need to filter for .sky files only, as .GetFiles(".sky") also returns .skyr files
            foreach (FileInfo file in toolInf.GetFiles().Where(file => file.Extension.Equals(SrmDocument.EXT)))
            {
                SrmDocument document;
                try
                {
                    using (var stream = File.OpenRead(file.FullName))
                    {
                        document = (SrmDocument) srmSerialzer.Deserialize(stream);
                    }
                }
                catch (Exception exception)
                {
                    DirectoryEx.SafeDelete(tempToolPath);
                    throw new IOException(string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__, file.FullName), exception);
                }

                foreach (AnnotationDef annotationDef in document.Settings.DataSettings.AnnotationDefs)
                {
                    if (Settings.Default.AnnotationDefList.ContainsKey(annotationDef.GetKey()))
                    {
                        var existingAnnotation = Settings.Default.AnnotationDefList[annotationDef.GetKey()];
                        if (!existingAnnotation.Equals(annotationDef))
                        {
                            conflictAnnotations.Add(annotationDef);
                        }
                    }
                    else
                    {
                        newAnnotations.Add(annotationDef);
                    }
                }
            }

            bool? overwriteAnnotations;
            if (conflictAnnotations.Count != 0)
            {
                overwriteAnnotations = shouldOverwriteAnnotations(conflictAnnotations);
            }
            else
            {
                overwriteAnnotations = false;
            }

            if (overwriteAnnotations == null)
            {
                // Cancelled by user
                DirectoryEx.SafeDelete(tempToolPath);
                return null;
            }

            if (overwriteAnnotations == true)
            {
                newAnnotations.AddRange(conflictAnnotations);
            }

            foreach (AnnotationDef annotation in newAnnotations)
            {
                Settings.Default.AnnotationDefList.SetValue(annotation);
            }

            var existingReports = new List<ReportSpec>();
            var newReports = new List<ReportSpec>();
            var xmlSerializer = new XmlSerializer(typeof(ReportSpecList));
            foreach (FileInfo file in toolInf.GetFiles("*" + ReportSpecList.EXT_REPORTS))
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
                    DirectoryEx.SafeDelete(tempToolPath);
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
            if (toolsToBeOverwrittenTitles.Count > 0 || existingReports.Count > 0)
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
                Settings.Default.ReportSpecList.Add(item);
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
                else if (programPathContainer != null) // If it is a ProgramPath macro
                {
                    List<string> packages = new List<string>();
                    int i = 1;
                    string package = readin.GetPackage(i);
                    while (!string.IsNullOrEmpty(package))
                    {
                        // if the package is not a uri, it is stored locally in the tool-inf directory
                        if (!package.StartsWith("http")) // Not L10N
                            package = Path.Combine(tempToolPath, TOOL_INF, package);
                        
                        packages.Add(package);
                        package = readin.GetPackage(++i);
                    }

                    if (!Settings.Default.ToolFilePaths.ContainsKey(programPathContainer) || packages.Count > 0)
                        retval.AddInstallation(programPathContainer,packages);
                }

                string reportTitle = readin.Input_Report_Name;
                List<AnnotationDef> annotations = new List<AnnotationDef>();
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
                        retval.AddMessage(string.Format(Resources.UnpackZipToolHelper_UnpackZipTool_The_tool___0___requires_report_type_titled___1___and_it_is_not_provided__Import_canceled_, readin.Title, reportTitle));
                        fileStream.Dispose();
                        continue;
                    }
                    // Get annotations for this specific tool
                    for (int i = 1; ; i++)
                    {
                        string annotation = readin.GetAnnotation(i);
                        if (string.IsNullOrEmpty(annotation))
                            break;
                        annotations.Add(Settings.Default.AnnotationDefList[annotation]);
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
                                                   readin.Output_to_Immediate_Window.Contains("True"), reportTitle, dllPath, readin.Args_Collector_Type, permToolPath, annotations)); //Not L10N
                fileStream.Close();

            } // Done looping through .properties files.

            //Check if we need to install a program
            if (retval.Installations.Count > 0)
            {
                foreach (var ppc in retval.Installations.Keys)
                {
                    string path = installProgram(ppc, retval.Installations[ppc]);
                    if (path == null)
                    {
                        //Cancel installation
                        DirectoryEx.SafeDelete(permToolPath);
                        return null;
                    }
                    else if (path != string.Empty)
                    {
                        if (Settings.Default.ToolFilePaths.ContainsKey(ppc))
                            Settings.Default.ToolFilePaths.Remove(ppc);
                        Settings.Default.ToolFilePaths.Add(ppc, path);
                    }
                }
            }

            // Remove the folder in tempfolder.
            if (tempToolPath.Contains("Temp")) // Minor sanity check //Not L10N
            {
                DirectoryEx.SafeDelete(tempToolPath);
            }

            foreach (var tool in retval.ValidToolsFound)
            {
                Settings.Default.ToolList.Add(tool);
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
            public Dictionary<ProgramPathContainer,List<string>> Installations { get; private set; }
            
            public UnzipToolReturnAccumulator()
            {
                ValidToolsFound = new List<ToolDescription>();
                MessagesThrown = new List<string>();
                Installations = new Dictionary<ProgramPathContainer, List<string>>();
            }

            public void AddMessage(string s)
            {
                MessagesThrown.Add(s);
            }

            public void AddTool(ToolDescription t)
            {
                ValidToolsFound.Add(t);
            }

            public void AddInstallation(ProgramPathContainer ppc, List<string> packages )
            {
                List<string> listPackages;
                if (!Installations.TryGetValue(ppc, out listPackages))
                {
                    Installations.Add(ppc,packages);
                }
                else
                {
                    listPackages.AddRange(packages.Where(p => !listPackages.Contains(p)));
                }
            }
        }

        private static string CopyinFile(string filename, string filedir, string desdir, string toolTitle)
        {
            string filescr = Path.Combine(filedir, filename);

            if (!File.Exists(filescr))
            {                
                throw new MessageException(string.Format(Resources.ConfigureToolsDlg_CopyinFile_Missing_the_file_0_Tool_1_Import_Failed, filename, toolTitle));             
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
                    throw new MessageException(string.Format(TextUtil.LineSeparate(
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