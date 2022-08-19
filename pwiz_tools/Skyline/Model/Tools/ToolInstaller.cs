/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Xml.Serialization;
using Ionic.Zip;
using Kajabity.Tools.Java;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Tools
{
    /// <summary>
    /// Class for reading .properties files that describe external tools. 
    /// </summary>
    public class ExternalToolProperties
    {
        /// <summary>
        /// Constants for key values in a .properties file that describe a tool.
        /// </summary>
        public static class PropertiesConstants
        {
            // ReSharper disable LocalizableElement
            //Required
            public const string TITLE = "Title";                                        
            public const string COMMAND = "Command";                                    
            // Optional
            public const string ARGUMENTS = "Arguments";                                
            public const string INITIAL_DIRECTORY = "InitialDirectory";                 
            public const string OUTPUT_TO_IMMEDIATE_WINDOW = "OutputToImmediateWindow"; 
            public const string INPUT_REPORT_NAME = "InputReportName";                  
            public const string REPORT_SKYR_FILE = "ReportSkyrFile";                    
            public const string ARGS_COLLECTOR_DLL = "ArgsCollectorDll";                
            public const string ARGS_COLLECTOR_TYPE = "ArgsCollectorType";              
            public const string PACKAGE = "Package{0}";                                 
            public const string PACKAGE_VERSION = "Package{0}Version";                  
            public const string ANNOTATION = "Annotation{0}";                           
            //Package attributes in info.properties
            public const string NAME = "Name";                                          
            public const string VERSION = "Version";                                    
            public const string AUTHOR = "Author";                                      
            public const string DESCRIPTION = "Description";                            
            public const string PROVIDER = "Provider";                                  
            public const string IDENTIFIER = "Identifier";
            // ReSharper restore LocalizableElement

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
                    {NAME, string.Empty},
                    {VERSION, string.Empty},
                    {AUTHOR, string.Empty},
                    {DESCRIPTION, string.Empty},
                    {PROVIDER, string.Empty},
                    {IDENTIFIER, string.Empty}
                };
        }

        private readonly JavaProperties _properties;

        public ExternalToolProperties(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                _properties = new JavaProperties(PropertiesConstants.DEFAULTS);
                _properties.Load(fileStream);
            }
        }

        public ExternalToolProperties(FileStream fileStream)
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
        public string GetPackageName(int p)
        {
            string lookup = string.Format(PropertiesConstants.PACKAGE, p);
            string package = _properties.GetProperty(lookup);
            return package != null ? package.Trim() : null;
        }
        public string GetPackageVersion(int p)
        {
            string lookup = string.Format(PropertiesConstants.PACKAGE_VERSION, p);
            string packageVersion = _properties.GetProperty(lookup);
            return packageVersion != null ? packageVersion.Trim() : null;
        }
        public ToolPackage GetPackageWithVersion(int p)
        {
            string name = GetPackageName(p);
            if (name == null)
            {
                return null;
            }
            string version = GetPackageVersion(p);
            return new ToolPackage{Name = name, Version = version};
        }
        public string Name
        {
            get { return _properties.GetProperty(PropertiesConstants.NAME); }
        }
        public string Version
        {
            get
            {
                string version = _properties.GetProperty(PropertiesConstants.VERSION);
                //Check to see if the provided version is valid, if not return string.Empty
                Version ver;
                return System.Version.TryParse(version, out ver) ? version : string.Empty;
            }
        }
        public string Identifier
        {
            get { return _properties.GetProperty(PropertiesConstants.IDENTIFIER); }
        }
        public string Author
        {
            get { return _properties.GetProperty(PropertiesConstants.AUTHOR); }
        }
        public string Provider
        {
            get { return _properties.GetProperty(PropertiesConstants.PROVIDER); }
        }
        public string GetAnnotation(int a)
        {
            string lookup = string.Format(PropertiesConstants.ANNOTATION, a);
            string annotation = _properties.GetProperty(lookup);
            return annotation != null ? annotation.Trim() : null;
        }

        #endregion //Accessors
    }
    /// <summary>
    /// ShouldOverwrite - Function that when given a list of tools and a list of reports that would be removed by an installation
    ///  it returns a bool, true for overwrite, false for in parallel and null for cancel installation.
    /// InstallProgram - Function that finds a program path given a program path container.
    /// </summary>
    public interface IUnpackZipToolSupport
    {
        bool? ShouldOverwriteAnnotations(List<AnnotationDef> annotations);
        bool? ShouldOverwrite(string toolCollectionName,
                              string toolCollectionVersion,
                              List<ReportOrViewSpec> reportList,
                              string foundVersion,
                              string newCollectionName);

        string InstallProgram(ProgramPathContainer ppc, ICollection<ToolPackage> packages, string pathToInstallScript);
    }
    
    public static class ToolInstaller
    {
        public const string TOOL_INF = "tool-inf";
        public const string INFO_PROPERTIES = "info.properties";
        private const string INSTALL_R_PACKAGES = "InstallPackages.r";

        /// <summary>
        /// Function for unpacking zipped External tools.
        /// </summary>
        /// <param name="pathToZip">Path to the zipped file that contains the tool and all its assicaited files.</param>        
        /// <param name="unpackSupport"> Interface that implements required functions that are dependent on context.</param>
        /// <returns></returns>
        public static UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport)
        {
            if (!Directory.Exists(pathToZip) && !File.Exists(pathToZip))
                throw new FileNotFoundException(pathToZip);

            //Removes any old folders that dont have Tools associated with them
            CheckToolDirConsistency();

            var retval = new UnzipToolReturnAccumulator();
            string name = Path.GetFileNameWithoutExtension(pathToZip);
            if (name == null)
            {                
                throw new ToolExecutionException(Resources.ConfigureToolsDlg_unpackZipTool_Invalid_file_selected__No_tools_added_);
            }
            // This helps with zipfiles that have spaces in the titles. 
            // Consider: We may want to add quotes around usages of the $(ToolDir) macro incase the Tool directory has spaces in one of its directory names.
            name = name.Replace(' ', '_'); 

            string outerToolsFolderPath = ToolDescriptionHelpers.GetToolsDirectory();
            if (string.IsNullOrEmpty(outerToolsFolderPath))
            {
                throw new ToolExecutionException(Resources.ConfigureToolsDlg_unpackZipTool_Error_unpacking_zipped_tools);
            }
            string tempFolderPath = Path.Combine(outerToolsFolderPath, @"Temp");

            var toolDir = new DirectoryInfo(tempFolderPath);
            if (!toolDir.Exists)
                toolDir.Create();

            // This naming conflict shouldn't happen. The temp file should be empty.
            // Consider: Try to delete the existing directory in the temp directory.
            string tempToolPath = Path.Combine(tempFolderPath, name);
            if (Directory.Exists(tempToolPath))
                tempToolPath = DirectoryEx.GetUniqueName(tempToolPath);

            using (new TemporaryDirectory(tempToolPath))
            {
                if (Directory.Exists(pathToZip))
                {
                    DirectoryCopy(pathToZip, tempToolPath);
                }
                else using (var zipFile = new ZipFile(pathToZip))
                {
                    try
                    {
                        zipFile.ExtractAll(tempToolPath, ExtractExistingFileAction.OverwriteSilently);
                    }
                    catch (Exception)
                    {
                        throw new ToolExecutionException(Resources.ConfigureToolsDlg_unpackZipTool_There_is_a_naming_conflict_in_unpacking_the_zip__Tool_importing_canceled_);
                    }
                }

                var dirInfo = new DirectoryInfo(tempToolPath);
                if (!dirInfo.Exists)
                    // Case where they try to load tools from an empty zipfile then the folder is never created.
                    dirInfo.Create();

                var toolInfDir = new DirectoryInfo(Path.Combine(tempToolPath, TOOL_INF));
                if (!toolInfDir.Exists)
                {
                    throw new ToolExecutionException(TextUtil.LineSeparate(
                            Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_an_installable_tool_,
                            string.Format(Resources.ToolInstaller_UnpackZipTool_Error__It_does_not_contain_the_required__0__directory_, TOOL_INF)));
                }

                // Handle info.properties
                var toolInfo = GetToolInfo(toolInfDir, retval);

                if (!HandleAnnotations(unpackSupport.ShouldOverwriteAnnotations, toolInfDir))
                    return null;

                HandleLegacyQuaSAR(toolInfo);

                var toolsToBeOverwritten = GetToolsToBeOverwritten(toolInfo.PackageIdentifier);

                List<ReportOrViewSpec> newReports;
                var existingReports = FindReportConflicts(toolInfDir, tempToolPath, out newReports);

                bool? overwrite = IsOverwrite(unpackSupport.ShouldOverwrite, toolsToBeOverwritten, existingReports, toolInfo);
                if (!overwrite.HasValue)
                {
                    // User canceled installation.
                    return null;
                }
                string DirectoryToRemove = null;
                if (overwrite.Value)
                {
                    // Delete the tools and their containing folder
                    if (toolsToBeOverwritten.Count > 0)
                    {
                        foreach (var tool in toolsToBeOverwritten)
                        {
                            Settings.Default.ToolList.Remove(tool);
                        }
                        // The tools are all guarenteed to be from the same directory by GetToolsToBeOverwritten
                        // and all toolDescriptions in a directory come from the same installation
                        DirectoryToRemove = toolsToBeOverwritten.First().ToolDirPath;
                    }

                    // Overwrite all existing reports. 
                    foreach (ReportOrViewSpec item in existingReports)
                    {
                        ReportSharing.SaveReport(PersistedViews.ExternalToolsGroup, item);
                    }
                }

                // Add all new reports.
                foreach (ReportOrViewSpec item in newReports)
                {
                    ReportSharing.SaveReport(PersistedViews.ExternalToolsGroup, item);
                }
                var reportRenameMapping = new Dictionary<string, string>();
                if (overwrite == false) // Dont overwrite so rename reports.
                {
                    // Deal with renaming reports!
                    foreach (ReportOrViewSpec item in existingReports)
                    {
                        string oldname = item.GetKey();
                        string newname = GetUniqueReportName(oldname);
                        reportRenameMapping.Add(oldname, newname);
                        ReportSharing.SaveReportAs(PersistedViews.ExternalToolsGroup, item, newname);
                    }
                }

                foreach (FileInfo file in toolInfDir.GetFiles(@"*.properties"))
                {
                    // We will replace the tool Directory value (null below) later when we know the import is sucessful.
                    AddToolFromProperties(file, retval, toolInfo, null, tempToolPath, reportRenameMapping);
                }

                // Check if we need to install a program
                if (retval.Installations.Count > 0)
                {
                    foreach (var ppc in retval.Installations.Keys)
                    {
                        string pathToPackageInstallScript = null;
                        if (ppc.ProgramName.Equals(@"R") && retval.Installations[ppc].Count != 0)
                        {
                            pathToPackageInstallScript = Path.Combine(tempToolPath, TOOL_INF, INSTALL_R_PACKAGES);
                            if (!File.Exists(pathToPackageInstallScript))
                            {
                                throw new ToolExecutionException(TextUtil.LineSeparate(string.Format(Resources.ToolInstaller_UnpackZipTool_Error__There_is_a_file_missing_the__0__zip, name),
                                                                        string.Empty,
                                                                        string.Format(Resources.ToolInstaller_UnpackZipTool_Tool_Uses_R_and_specifies_Packages_without_an__0__file_in_the_tool_inf_directory_, INSTALL_R_PACKAGES)));
                            }
                        }

                        string path = unpackSupport.InstallProgram(ppc, retval.Installations[ppc], pathToPackageInstallScript);
                        if (path == null)
                        {
                            // Cancel installation
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
                // We don't decide the final toolDirPath until we make it to here.
                // This will require some fixing of the tooldir and path to dll in each of the tools in retval.validtoolsfound
                // It also enables us to not delete the tools from the tool list unless we have a sucessful installation.

                // Decide the permToolPath.
                if (DirectoryToRemove != null)
                    DirectoryEx.SafeDelete(DirectoryToRemove);

                // Final Directory Location.
                string permToolPath = DirectoryEx.GetUniqueName(Path.Combine(outerToolsFolderPath, name));

                foreach (var tool in retval.ValidToolsFound)
                {
                    tool.ToolDirPath = permToolPath;
                    if (!string.IsNullOrEmpty(tool.ArgsCollectorDllPath))
                        tool.ArgsCollectorDllPath = Path.Combine(permToolPath, tool.ArgsCollectorDllPath);
                    Settings.Default.ToolList.Add(tool);
                }

                if (retval.ValidToolsFound.Count != 0)
                {
                    Helpers.TryTwice(() => Directory.Move(tempToolPath, permToolPath),
                        $@"Directory.Move({tempToolPath}, {permToolPath})");
                }
            }
            return retval;
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        /// <summary>
        /// Removes any old folders in the ToolDir that dont have tools associated with them
        /// For the case where we have used a dll and couldn't delete it in some past instance of Skyline.
        /// </summary>
        public static void CheckToolDirConsistency()
        {
            var referencedPaths = Settings.Default.ToolList.Where(t => !string.IsNullOrEmpty(t.ToolDirPath))
                                              .Select(t => t.ToolDirPath).ToArray();
            string toolsDir = ToolDescriptionHelpers.GetToolsDirectory();
            if (string.IsNullOrEmpty(toolsDir) || !Directory.Exists(toolsDir))
                return;
            foreach (var folder in Directory.EnumerateDirectories(toolsDir))
            {
                if (!referencedPaths.Contains(folder))
                {
                    DirectoryEx.SafeDelete(folder);
                }
            }
        }

        private class ToolInfo
        {
            public string PackageVersion { get; private set; }
            public string PackageName { get; private set; }
            public string PackageIdentifier { get; private set; }

            public void SetPackageVersion(string prop)
            {
                PackageVersion = GetNonEmptyValue(prop, ExternalToolProperties.PropertiesConstants.VERSION);
            }

            public void SetPackageName(string prop)
            {
                PackageName = GetNonEmptyValue(prop, ExternalToolProperties.PropertiesConstants.NAME);
            }

            public void SetPackageIdentifier(string prop)
            {
                PackageIdentifier = GetNonEmptyValue(prop, ExternalToolProperties.PropertiesConstants.IDENTIFIER);
            }

            private static string GetNonEmptyValue(string prop, string name)
            {
                if (string.IsNullOrEmpty(prop))
                {
                    throw new ToolExecutionException(TextUtil.LineSeparate(
                        Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_a_valid_installable_tool_,
                        string.Format(Resources.ToolInstaller_UnpackZipTool_Error__The__0__does_not_contain_a_valid__1__attribute_,
                                      INFO_PROPERTIES, name)));
                }
                return prop;
            }
        }

        private static ToolInfo GetToolInfo(DirectoryInfo toolInf, UnzipToolReturnAccumulator accumulator)
        {
            var toolInfo = new ToolInfo();
            string infoFile = Path.Combine(toolInf.FullName, INFO_PROPERTIES);
            if (File.Exists(infoFile))
            {
                ExternalToolProperties readin;
                try
                {
                    readin = new ExternalToolProperties(infoFile);
                }
                catch (Exception)
                {
                    // Failed to read the .properties file
                    throw new ToolExecutionException(string.Format(Resources.ToolInstaller_GetToolInfo_Failed_to_process_the__0__file, INFO_PROPERTIES));
                }

                toolInfo.SetPackageVersion(readin.Version);
                toolInfo.SetPackageIdentifier(readin.Identifier);
                toolInfo.SetPackageName(readin.Name);

                //Check for Package Installation specified in info.properties
                var ppc = ToolMacros.GetProgramPathContainer(readin.Command);
                if (ppc != null)
                {
                    FindPackagesToInstall(readin, accumulator, ppc);
                } 
            
            }
            else //No info.properties file in the tool-inf directory.
            {
                throw new ToolExecutionException(TextUtil.LineSeparate(
                    Resources.ToolInstaller_UnpackZipTool_The_selected_zip_file_is_not_a_valid_installable_tool_,
                    string.Format(Resources.ToolInstaller_GetToolInfo_Error__It_does_not_contain_the_required__0__in_the__1__directory_,
                                  INFO_PROPERTIES, TOOL_INF)));
            }
            return toolInfo;
        }

        private static bool HandleAnnotations(Func<List<AnnotationDef>, bool?> shouldOverwriteAnnotations, DirectoryInfo toolInf)
        {
            var srmSerialzer = new XmlSerializer(typeof(SrmDocument));
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
                        document = (SrmDocument)srmSerialzer.Deserialize(stream);
                    }
                }
                catch (Exception exception)
                {
                    throw new IOException(string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__,
                                                        file.FullName), exception);
                }

                foreach (AnnotationDef annotationDef in document.Settings.DataSettings.AnnotationDefs)
                {
                    AnnotationDef existingAnnotation;
                    if (!Settings.Default.AnnotationDefList.TryGetValue(annotationDef.GetKey(), out existingAnnotation))
                    {
                        newAnnotations.Add(annotationDef);
                    }
                    else if (!ToolDescription.EquivalentAnnotations(existingAnnotation, annotationDef))
                    {
                        conflictAnnotations.Add(annotationDef);
                    }
                }
            }

            bool? overwriteAnnotations = conflictAnnotations.Count != 0
                                             ? shouldOverwriteAnnotations(conflictAnnotations)
                                             : false;

            if (!overwriteAnnotations.HasValue)
            {
                // Cancelled by user
                return false;
            }

            if (overwriteAnnotations == true)
            {
                newAnnotations.AddRange(conflictAnnotations);
            }

            foreach (AnnotationDef annotation in newAnnotations)
            {
                Settings.Default.AnnotationDefList.SetValue(annotation);
            }
            return true;
        }

        private static void HandleLegacyQuaSAR(ToolInfo info)
        {
            if (info.PackageIdentifier.Equals(@"URN:LSID:carr.broadinstitute.org:quasar"))
            {
                var deprecatedQuaSAR = Settings.Default.ToolList.FirstOrDefault(toolDesc =>
                   toolDesc.Title.Equals(ToolList.DEPRECATED_QUASAR.Title) &&
                   toolDesc.Command.Equals(ToolList.DEPRECATED_QUASAR.Command));
                if (deprecatedQuaSAR != null)
                {
                    Settings.Default.ToolList.Remove(deprecatedQuaSAR);
                    Settings.Default.PersistedViews.RemoveView(PersistedViews.ExternalToolsGroup.Id, ReportSpecList.QUASAR_REPORT_NAME);
                }
            }
        }

        private static List<ToolDescription> GetToolsToBeOverwritten(string packageIdentifier)
        {
            List<ToolDescription> toolsToBeOverwritten = new List<ToolDescription>(
                Settings.Default.ToolList.Where((tool => tool.PackageIdentifier == packageIdentifier)));

            var mostRecent = FindMostRecentVersion(toolsToBeOverwritten);
            if (mostRecent != null)
            {
                string version = mostRecent.PackageVersion;
                toolsToBeOverwritten = toolsToBeOverwritten.Where(t => Equals(t.PackageVersion, version)).ToList();
            }

            if (toolsToBeOverwritten.Count > 0)
            {
                // Filter toolsToBeOverWritten so that they are all from the same toolDir.
                string overwritePath = toolsToBeOverwritten.First().ToolDirPath;
                toolsToBeOverwritten = toolsToBeOverwritten.Where(t => Equals(t.ToolDirPath, overwritePath)).ToList();
            }
            return toolsToBeOverwritten;
        }

        private static string GetPermToolPath(string basePath,
                                             IList<ToolDescription> toolsToBeOverwritten)
        {
            for (int i = 0; ; i++)
            {
                // First try with no number appended, then try with appended numbers searching for a reasonable folder name that
                // corresponds with the name of the .zip file that only contains tools with the same unique identifier (or no tools)
                string permToolPath = basePath;
                if (i > 0)
                    permToolPath += i;

                var toolsUsingDir = new List<ToolDescription>();
                if (Directory.Exists(permToolPath))
                    toolsUsingDir.AddRange(Settings.Default.ToolList.Where(tool => tool.ToolDirPath == permToolPath));

                if (toolsUsingDir.All(toolsToBeOverwritten.Contains))
                    return permToolPath;
            }
        }


        private static List<ReportOrViewSpec> GetReportsFromFiles(DirectoryInfo toolInfDir, string tempToolPath)
        {
            var srmSerialzer = new XmlSerializer(typeof(SrmDocument));
            var loadedItems = new List<ReportOrViewSpec>();
            var skyrFiles = toolInfDir.GetFiles(@"*" + ReportSpecList.EXT_REPORTS);
            if (skyrFiles.Length > 0)
            {
                foreach (FileInfo file in skyrFiles)
                {
                    try
                    {
                        using (var stream = File.OpenRead(file.FullName))
                        {
                            loadedItems.AddRange(ReportSharing.DeserializeReportList(stream));
                        }
                    }
                    catch (Exception exception)
                    {
                        DirectoryEx.SafeDelete(tempToolPath);
                        throw new IOException(
                            string.Format(Resources.SerializableSettingsList_ImportFile_Failure_loading__0__,
                                file.FullName), exception);
                    }
                }
            }
            else
            {
                foreach (FileInfo file in toolInfDir.GetFiles().Where(file => file.Extension.Equals(SrmDocument.EXT)))
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
                        throw new IOException(string.Format(
                            Resources.SerializableSettingsList_ImportFile_Failure_loading__0__,
                            file.FullName), exception);
                    }

                    var reports = document.Settings.DataSettings.ViewSpecList.ViewSpecLayouts;
                    foreach (ViewSpecLayout report in reports)
                    {
                        loadedItems.Add(new ReportOrViewSpec(report));
                    }
                }
            }

            return loadedItems;
        }


        /// <summary>
        /// Generate the list of Existing Reports that would be modified and the list of new reports
        /// </summary>
        private static List<ReportOrViewSpec> FindReportConflicts(DirectoryInfo toolInfDir, string tempToolPath, out List<ReportOrViewSpec> newReports)
        {
            var existingReports = new List<ReportOrViewSpec>();
            newReports = new List<ReportOrViewSpec>();

            var externalToolReports =
                Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.ExternalToolsGroup.Id);
            Dictionary<string, ViewSpecLayout> allExistingReports = new Dictionary<string, ViewSpecLayout>();
            if (externalToolReports != null)
            {
                foreach (var viewSpec in externalToolReports.ViewSpecLayouts)
                {
                    allExistingReports[viewSpec.Name] = viewSpec;
                }
            }

            var loadedItems = GetReportsFromFiles(toolInfDir, tempToolPath);
                
                
            foreach (var reportOrViewSpec in loadedItems)
            {
                ViewSpecLayout existingView;
                if (allExistingReports.TryGetValue(reportOrViewSpec.GetKey(), out existingView))
                {
                    if (!ReportSharing.AreEquivalent(reportOrViewSpec, new ReportOrViewSpec(existingView)))
                    {
                        existingReports.Add(reportOrViewSpec);
                    }
                    else
                    {
                        newReports.Add(reportOrViewSpec);
                    }
                }
                else
                {
                    newReports.Add(reportOrViewSpec);
                }
            }
                // We now have the list of Reports that would have a naming conflict. 
            return existingReports;
        }

        private delegate bool? ShouldOverwrite(string toolCollectionName,
                              string toolCollectionVersion,
                              List<ReportOrViewSpec> reportList,
                              string foundVersion,
                              string newCollectionName);

        private static bool? IsOverwrite(ShouldOverwrite shouldOverwrite,
                                        List<ToolDescription> toolsToBeOverwritten,
                                        List<ReportOrViewSpec> existingReports,
                                        ToolInfo toolInfo)
        {
            if (toolsToBeOverwritten.Count > 0)
            {
                // There is one weird case where the toolsToBeOverwritten could come from differnt packageNames.
                //     We have already ensured they all have the same version number and same unique identifier. If there 
                //     are different package names then they have installed incorrectly defined tools
                var tool = toolsToBeOverwritten.First();
                string toolCollectionName = tool.PackageName + @" v" + tool.PackageVersion;
                string toolCollectionVersion = tool.PackageVersion;

                return shouldOverwrite(toolCollectionName, toolCollectionVersion, existingReports,
                                            toolInfo.PackageVersion, toolInfo.PackageName);
            }
            else if (existingReports.Count > 0)
            {
                return shouldOverwrite(null, null, existingReports, toolInfo.PackageVersion, toolInfo.PackageName);
            }
            // No conflicts
            return true;
        }

        private static void AddToolFromProperties(FileInfo file,
                                                  UnzipToolReturnAccumulator accumulator,
                                                  ToolInfo toolInfo,
                                                  string placeHolderToolPath,
                                                  string tempToolPath,
                                                  IDictionary<string, string> reportRenameMapping)
        {
            if (file.Name == INFO_PROPERTIES)
                return;

            ExternalToolProperties readin;
            try
            {
                readin = new ExternalToolProperties(file.FullName);
            }
            catch (Exception)
            {
                //Failed to read the .properties file
                accumulator.AddMessage(string.Format(Resources.ConfigureToolsDlg_unpackZipTool_Failed_to_process_file_0_The_tool_described_failed_to_import,
                                                     file.Name));
                return;
            }
            if (readin.Title == null || readin.Command == null)
            {
                accumulator.AddMessage(string.Format(TextUtil.LineSeparate(
                    Resources.ConfigureToolsDlg_unpackZipTool_Invalid_Tool_Description_in_file__0__,
                    Resources.ConfigureToolsDlg_unpackZipTool_Title_and_Command_are_required,
                    Resources.ConfigureToolsDlg_unpackZipTool_skipping_that_tool_), file.Name));
                return;
            }

            string command = ProcessCommand(readin, tempToolPath, accumulator);
            if (string.IsNullOrEmpty(command))
                return;

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
                if (!ReportSharing.GetExistingReports().ContainsKey(PersistedViews.ExternalToolsGroup.Id.ViewName(reportTitle)))
                {
                    accumulator.AddMessage(string.Format(Resources.UnpackZipToolHelper_UnpackZipTool_The_tool___0___requires_report_type_titled___1___and_it_is_not_provided__Import_canceled_,
                                                         readin.Title, reportTitle));
                    return;
                }
                // Get annotations for this specific tool
                GetAnotations(readin, annotations);
            }
            //Check the ArgsCollector Dll exists.
            string dllPath = readin.Args_Collector_Dll;
            if (!string.IsNullOrEmpty(dllPath))
            {
                // Handle case where they prepended the DllPath with $(ToolDir)\\.
                // ReSharper disable once LocalizableElement
                if (dllPath.StartsWith(ToolMacros.TOOL_DIR + "\\"))
                {
                    dllPath = dllPath.Substring(ToolMacros.TOOL_DIR.Length + 1);
                }
                if (!File.Exists(Path.Combine(tempToolPath, dllPath)))
                {
                    accumulator.AddMessage(string.Format(Resources.ToolInstaller_AddToolFromProperties_Missing_the_file__0___Tool__1__import_failed, dllPath,
                                                         readin.Title));
                    return;
                }
                // Path to the dll gets renamed at the end of the UnpackZipTools Function when we 
                // finally decide the directory for the $(ToolDir)
            }
            
            //Make sure tools get a unique title.
            string uniqueTitle = GetUniqueToolTitle(readin.Title);

            //Append each tool to the return value
            accumulator.AddTool(new ToolDescription(uniqueTitle,
                                               command,
                                               readin.Arguments,
                                               readin.Initial_Directory,
                                               readin.Output_to_Immediate_Window.Contains(@"True"),
                                               reportTitle,
                                               dllPath,
                                               readin.Args_Collector_Type,
                                               placeHolderToolPath,
                                               annotations,
                                               toolInfo.PackageVersion,
                                               toolInfo.PackageIdentifier,
                                               toolInfo.PackageName));
        }

        private static string ProcessCommand(ExternalToolProperties readin, string tempToolPath, UnzipToolReturnAccumulator accumulator)
        {
            string command = readin.Command.Trim();
            var programPathContainer = ToolMacros.GetProgramPathContainer(command);
            if (!ToolDescription.IsWebPageCommand(command) && programPathContainer == null)
            {
                // ReSharper disable once LocalizableElement
                if (command.StartsWith(ToolMacros.TOOL_DIR + "\\"))
                {
                    command = command.Substring(ToolMacros.TOOL_DIR.Length + 1);
                }
                if (!File.Exists(Path.Combine(tempToolPath, command)))
                {
                    accumulator.AddMessage(string.Format(Resources.ToolInstaller_AddToolFromProperties_Missing_the_file__0___Tool__1__import_failed, command, readin.Title));
                    return null;
                }
                command = Path.Combine(ToolMacros.TOOL_DIR, command);
            }
            else if (programPathContainer != null) // If it is a ProgramPath macro
            {
                FindPackagesToInstall(readin, accumulator, programPathContainer);
            }
            return command;
        }

        private static void FindPackagesToInstall(ExternalToolProperties readin,
                                                  UnzipToolReturnAccumulator accumulator,
                                                  ProgramPathContainer programPathContainer)
        {
            var packages = new List<ToolPackage>();
            int i = 1;
            var package = readin.GetPackageWithVersion(i);
            while (package != null)
            {
                // if the package is not a uri, it is stored locally in the tool-inf directory
                //if (!package.StartsWith("http")) 
                //    package = Path.Combine(tempToolPath, TOOL_INF, package);

                packages.Add(package);
                package = readin.GetPackageWithVersion(++i);
            }

            if (!Settings.Default.ToolFilePaths.ContainsKey(programPathContainer) || packages.Count > 0)
                accumulator.AddInstallation(programPathContainer, packages);
        }

        private static void GetAnotations(ExternalToolProperties readin, List<AnnotationDef> annotations)
        {
            for (int i = 1; ; i++)
            {
                string annotation = readin.GetAnnotation(i);
                if (string.IsNullOrEmpty(annotation))
                    break;
                annotations.Add(Settings.Default.AnnotationDefList[annotation]);
            }
        }

        private static ToolDescription FindMostRecentVersion(List<ToolDescription> toolsToBeOverwritten)
        {
            if (toolsToBeOverwritten.Count == 0)
                return null;
            ToolDescription newestTool = toolsToBeOverwritten.FirstOrDefault(t => !string.IsNullOrEmpty(t.PackageVersion));
            if (newestTool == null)
                return null;

            Version newestVersion = new Version(newestTool.PackageVersion);
            foreach (var tool in toolsToBeOverwritten)
            {
                if (!string.IsNullOrEmpty(tool.PackageVersion))
                {
                    Version v = new Version(tool.PackageVersion);
                    if (v > newestVersion)
                    {
                        newestVersion = v;
                        newestTool = tool;
                    }
                }
            }
            return newestTool;
        }

        public static string GetUniqueName(string name, Func<string, bool> isUnique)
        {
            return GetUniqueFormat(name + @"{0}", isUnique); 
        }

        public static string GetUniqueFormat(string formatString, Func<string, bool> isUnique)
        {
            for (int i = 1; ; i++)
            {
                string version = string.Format(formatString, i);
                if (isUnique(version))
                {
                    return version;
                }
            }
        }
        
        private static string GetUniqueToolTitle(string title)
        {
            return Settings.Default.ToolList.Any(item => item.Title == (title))
                       ? GetUniqueName(title, value => Settings.Default.ToolList.All(item => item.Title != (value)))
                       : title;
        }

        private static string GetUniqueReportName(string key)
        {
//            var existingReports = ReportSharing.GetExistingReports();
//            return existingReports.ContainsKey(key)
//                       ? GetUniqueName(key, value => !existingReports.ContainsKey(value))
//                       : key;
            return null;
        }

        public class UnzipToolReturnAccumulator
        {
            public List<ToolDescription> ValidToolsFound { get; private set; }
            public List<string> MessagesThrown { get; private set; }
            public Dictionary<ProgramPathContainer,List<ToolPackage>> 
                Installations { get; private set; }
            
            public UnzipToolReturnAccumulator()
            {
                ValidToolsFound = new List<ToolDescription>();
                MessagesThrown = new List<string>();
                Installations = new Dictionary<ProgramPathContainer, List<ToolPackage>>();
            }

            public void AddMessage(string s)
            {
                MessagesThrown.Add(s);
            }

            public void AddTool(ToolDescription t)
            {
                ValidToolsFound.Add(t);
            }

            public void AddInstallation(ProgramPathContainer ppc, List<ToolPackage> packages )
            {
                List<ToolPackage> listPackages;
                if (Installations.TryGetValue(ppc, out listPackages))
                {
                    listPackages.AddRange(packages.Where(p => !listPackages.Contains(p)));
                }
                else
                {
                    Installations.Add(ppc, packages);
                }
            }
        }
    }

    public class ToolPackage
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
