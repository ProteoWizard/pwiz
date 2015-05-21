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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public static class ToolInstallUI
    {
        public delegate string InstallProgram(ProgramPathContainer pathContainer,
            ICollection<ToolPackage> toolPackages,
            string pathToPackageInstallScrip);
     
        public static void InstallZipFromFile(Control parent, InstallProgram install)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.ConfigureToolsDlg_AddFromFile_Zip_Files, ".zip")), // Not L10N
                Multiselect = false
            })
            {
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                    InstallZipTool(parent, dlg.FileName, install);
            }
        }

        public static void InstallZipFromWeb(Control parent, InstallProgram install)
        {
            try
            {
                IList<ToolStoreItem> toolStoreItems = null;
                using (var dlg = new LongWaitDlg { Message = Resources.ConfigureToolsDlg_AddFromWeb_Contacting_the_server })
                {
                    dlg.PerformWork(parent, 1000, () =>
                    {
                        toolStoreItems = ToolStoreUtil.ToolStoreClient.GetToolStoreItems();
                    });
                }
                if (toolStoreItems == null || toolStoreItems.Count == 0)
                {
                    MessageDlg.Show(parent, Resources.ConfigureToolsDlg_AddFromWeb_Unknown_error_connecting_to_the_tool_store);
                }
                using (var dlg = new ToolStoreDlg(ToolStoreUtil.ToolStoreClient, toolStoreItems))
                {
                    if (dlg.ShowDialog(parent) == DialogResult.OK)
                        InstallZipTool(parent, dlg.DownloadPath, install);
                }
            }
            catch (TargetInvocationException x)
            {
                if (x.InnerException.GetType() == typeof(ToolExecutionException) || x.InnerException is WebException)
                    MessageDlg.Show(parent, String.Format(Resources.ConfigureToolsDlg_GetZipFromWeb_Error_connecting_to_the_Tool_Store___0_, x.Message));
                else
                    throw;
            }
        }

        public class InstallZipToolHelper : IUnpackZipToolSupport
        {
            public InstallZipToolHelper(InstallProgram installProgram)
            {
                _installProgram = installProgram;
            }

            private InstallProgram _installProgram { get; set; }

            public bool? ShouldOverwrite(string toolCollectionName,
                                         string toolCollectionVersion,
                                         List<ReportOrViewSpec> reportList,
                                         string foundVersion,
                                         string newCollectionName)
            {
                return OverwriteOrInParallel(toolCollectionName, toolCollectionVersion, reportList, foundVersion, newCollectionName);
            }

            public string InstallProgram(ProgramPathContainer programPathContainer,
                                         ICollection<ToolPackage> packages,
                                         string pathToInstallScript)
            {
                return _installProgram(programPathContainer, packages, pathToInstallScript);
            }

            public bool? ShouldOverwriteAnnotations(List<AnnotationDef> annotations)
            {
                return OverwriteAnnotations(annotations);
            }
        }

        /// <summary>
        /// Copy a zip file's contents to the tools folder and loop through its .properties
        /// files adding the tools to the tools menu.
        /// </summary>
        /// <param name="parent">Parent window for alerts and forms</param>
        /// <param name="fullpath">The full path to the zipped folder containing the tools</param>
        /// <param name="install">Installation function</param>
        public static void InstallZipTool(Control parent, string fullpath, InstallProgram install)
        {
            ToolInstaller.UnzipToolReturnAccumulator result = null;
            var toolListStart = ToolList.CopyTools(Settings.Default.ToolList);
            Exception xFailure = null;
            try
            {
                result = ToolInstaller.UnpackZipTool(fullpath, new InstallZipToolHelper(install));
            }
            catch (ToolExecutionException x)
            {
                MessageDlg.ShowException(parent, x);
            }
            catch (Exception x)
            {
                xFailure = x;
            }
            if (xFailure != null)
            {
                MessageDlg.ShowWithException(parent, TextUtil.LineSeparate(String.Format(Resources.ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool_from__0_, Path.GetFileName(fullpath)), xFailure.Message), xFailure);                
            }

            if (result != null)
            {
                foreach (var message in result.MessagesThrown)
                {
                    MessageDlg.Show(parent, message);
                }
            }
            else
            {
                // If result is Null, then we want to discard changes made to the ToolsList
                Settings.Default.ToolList = toolListStart;
            }
        }

        public enum RelativeVersion
        {
            upgrade,
            reinstall,
            olderversion,
            unknown
        }

        public static bool? OverwriteOrInParallel(string toolCollectionName,
                                                  string toolCollectionVersion,
                                                  List<ReportOrViewSpec> reportList,
                                                  string foundVersion,
                                                  string newCollectionName)
        {
            string message;
            string buttonText;
            if (toolCollectionName != null)
            {
                RelativeVersion relativeVersion = DetermineRelativeVersion(toolCollectionVersion, foundVersion);
                string toolMessage;
                switch (relativeVersion)
                {
                    case RelativeVersion.upgrade:
                        toolMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_currently_installed_, String.Empty,
                            String.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_upgrade_to__0__or_install_in_parallel_, foundVersion));
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Upgrade;
                        break;
                    case RelativeVersion.olderversion:
                        toolMessage =
                            TextUtil.LineSeparate(
                                String.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_is_an_older_installation_v_0__of_the_tool__1_, foundVersion, "{0}"), // Not L10N
                                String.Empty,
                                String.Format(Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_with_the_older_version__0__or_install_in_parallel_,
                                foundVersion));
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite;
                        break;
                    case RelativeVersion.reinstall:
                        toolMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_already_installed_,
                            String.Empty,
                            Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_reinstall_or_install_in_parallel_);
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Reinstall;
                        break;
                    default:
                        toolMessage =
                            TextUtil.LineSeparate(
                                Resources.ConfigureToolsDlg_OverwriteOrInParallel_The_tool__0__is_in_conflict_with_the_new_installation,
                                String.Empty,
                                Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_);
                        buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite; // Or update?
                        break;
                }
                message = String.Format(toolMessage, toolCollectionName);
            }
            else //Warn about overwritng report.
            {
                List<string> reportTitles = reportList.Select(sp => sp.GetKey()).ToList();

                string reportMultiMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_following_reports, String.Empty,
                                                              "{0}", String.Empty); // Not L10N
                string reportSingleMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteOrInParallel_This_installation_would_modify_the_report_titled__0_, String.Empty);

                string reportMessage = reportList.Count == 1 ? reportSingleMessage : reportMultiMessage;
                string question = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Do_you_wish_to_overwrite_or_install_in_parallel_;
                buttonText = Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite;
                string reportMessageFormat = TextUtil.LineSeparate(reportMessage, question);
                message = String.Format(reportMessageFormat, TextUtil.LineSeparate(reportTitles));
            }

            DialogResult result = MultiButtonMsgDlg.Show(
                null, message, buttonText, Resources.ConfigureToolsDlg_OverwriteOrInParallel_In_Parallel, true);
            switch (result)
            {
                case DialogResult.Cancel:
                    return null;
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
            }
            return false;
        }

        public static RelativeVersion DetermineRelativeVersion(string versionToCompare, string foundVersion)
        {
            if (!String.IsNullOrEmpty(foundVersion) && !String.IsNullOrEmpty(versionToCompare))
            {
                Version current = new Version(versionToCompare);
                Version found = new Version(foundVersion);
                if (current > found) //Installing an olderversion.
                {
                    return RelativeVersion.olderversion;
                }
                else if (current == found) // Installing the same version.
                {
                    return RelativeVersion.reinstall;
                }
                else if (found > current)
                {
                    return RelativeVersion.upgrade;
                }
            }
            return RelativeVersion.unknown;
        }

        public static bool? OverwriteAnnotations(List<AnnotationDef> annotations)
        {
            List<string> annotationTitles = annotations.Select(annotation => annotation.GetKey()).ToList();

            string annotationMultiMessage = TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteAnnotations_Annotations_with_the_following_names_already_exist_, String.Empty,
                                                    "{0}", String.Empty); // Not L10N

            string annotationSingleMessage =
                TextUtil.LineSeparate(Resources.ConfigureToolsDlg_OverwriteAnnotations_An_annotation_with_the_following_name_already_exists_, String.Empty, "{0}", String.Empty); // Not L10N

            string annotationMessage = annotations.Count == 1 ? annotationSingleMessage : annotationMultiMessage;
            string question = Resources.ConfigureToolsDlg_OverwriteAnnotations_Do_you_want_to_overwrite_or_keep_the_existing_annotations_;

            string messageFormat = TextUtil.LineSeparate(annotationMessage, question);

            DialogResult result = MultiButtonMsgDlg.Show(
                null,
                String.Format(messageFormat, TextUtil.LineSeparate(annotationTitles)),
                Resources.ConfigureToolsDlg_OverwriteOrInParallel_Overwrite,
                Resources.ConfigureToolsDlg_OverwriteAnnotations_Keep_Existing, true);
            switch (result)
            {
                case DialogResult.Cancel:
                    return null;
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
            }
            return false;
        }
    }
}
