/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Controls.Startup;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.Esp;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using DatabaseOpeningException = pwiz.Skyline.Model.Irt.DatabaseOpeningException;

namespace pwiz.Skyline
{
    public partial class SkylineWindow
    {
        public static string GetViewFile(string fileName)
        {
            return fileName + @".view";
        }

        private void fileMenu_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = fileToolStripMenuItem;
            List<string> mruList = Settings.Default.MruList;
            string curDir = Settings.Default.ActiveDirectory;

            int start = menu.DropDownItems.IndexOf(mruBeforeToolStripSeparator) + 1;
            while (!ReferenceEquals(menu.DropDownItems[start], mruAfterToolStripSeparator))
                menu.DropDownItems.RemoveAt(start);
            int len = Math.Min(mruList.Count, Settings.Default.MruLength);
            for (int i = 0; i < len; i++)
            {
                MruChosenHandler handler = new MruChosenHandler(this, mruList[i]);
                ToolStripMenuItem item = new ToolStripMenuItem(GetMruName(i, mruList[i], curDir), null,
                    handler.ToolStripMenuItemClick);
                if (!item.Text.EndsWith(mruList[i]))
                {
                    item.ToolTipText = mruList[i];
                }
                menu.DropDownItems.Insert(start + i, item);
            }
            mruAfterToolStripSeparator.Visible = (mruList.Count > 0);
        }

        private static string GetMruName(int index, string path, string curDir)
        {
            string name = path;
            if (curDir == Path.GetDirectoryName(path))
                name = Path.GetFileName(path);
            // Make index 1-based
            index++;
            if (index < 9)
                name = string.Format(@"&{0} {1}", index, name);
            return name;
        }

        private class MruChosenHandler
        {
            private readonly SkylineWindow _skyline;
            private readonly string _path;

            public MruChosenHandler(SkylineWindow skyline, string path)
            {
                _skyline = skyline;
                _path = path;
            }

            public void ToolStripMenuItemClick(object sender, EventArgs e)
            {
                if (!_skyline.CheckSaveDocument())
                    return;
                _skyline.OpenFile(_path);
            }
        }

        private void startPageMenuItem_Click(object sender, EventArgs e) { OpenStartPage(); }

        private void newMenuItem_Click(object sender, EventArgs e) { NewDocument(); }

        public void OpenStartPage()
        {
            if (!CheckSaveDocument())
                return;

            using (var startupForm = new StartPage())
            {
                if (startupForm.ShowDialog(this) == DialogResult.OK)
                {
                    startupForm.Action(this);
                }
            }
        }

        public void NewDocument()
        {
            NewDocument(false);
        }

        public void NewDocument(bool forced)
        {
            if (!forced && !CheckSaveDocument())
                return;

            // Create a new document with the default settings.
            SrmDocument document = ConnectDocument(this, new SrmDocument(Settings.Default.SrmSettingsList[0]), null) ??
                                   new SrmDocument(SrmSettingsList.GetDefault());

            if (document.Settings.DataSettings.AuditLogging)
            {
                var entry = AuditLogEntry.GetAuditLoggingStartExistingDocEntry(document, ModeUI);
                document = entry?.AppendEntryToDocument(document) ?? document;
            }

            // Make sure settings lists contain correct values for
            // this document.
            document.Settings.UpdateLists(null);

            // Switch over to the new document
            SwitchDocument(document, null);
        }

        private void openContainingFolderMenuItem_Click(object sender, EventArgs e)
        {
            string args = string.Format(@"/select, ""{0}""", DocumentFilePath);
            Process.Start(@"explorer.exe", args);
        }

        private void openMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckSaveDocument())
                return;
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                SupportMultiDottedExtensions = true,
                DefaultExt = SrmDocument.EXT,
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC_AND_SKY_ZIP, SrmDocumentSharing.FILTER_SHARING, SkypFile.FILTER_SKYP)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    if (dlg.FileName.EndsWith(SrmDocumentSharing.EXT))
                    {
                        OpenSharedFile(dlg.FileName);
                    }
                    else if (dlg.FileName.EndsWith(SkypFile.EXT))
                    {
                        OpenSkypFile(dlg.FileName);
                    }
                    else
                    {
                        OpenFile(dlg.FileName);
                    }
                }
            }
        }

        public bool OpenSharedFile(string zipPath, FormEx parentWindow = null)
        {
            try
            {
                var sharing = new SrmDocumentSharing(zipPath);

                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.SkylineWindow_OpenSharedFile_Extracting_Files,
                })
                {
                    longWaitDlg.PerformWork(parentWindow ?? this, 1000, sharing.Extract);
                    if (longWaitDlg.IsCanceled)
                        return false;
                }

                // Remember the directory containing the newly extracted file
                // as the active directory for the next open command.
                Settings.Default.ActiveDirectory = Path.GetDirectoryName(sharing.DocumentPath);

                return OpenFile(sharing.DocumentPath, parentWindow);
            }
            catch (ZipException zipException)
            {
                MessageDlg.ShowWithException(parentWindow ?? this, string.Format(Resources.SkylineWindow_OpenSharedFile_The_zip_file__0__cannot_be_read,
                                                    zipPath), zipException);
                return false;
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(string.Format(
                        Resources.SkylineWindow_OpenSharedFile_Failure_extracting_Skyline_document_from_zip_file__0__,
                        zipPath), e.Message);
                MessageDlg.ShowWithException(parentWindow ?? this, message, e);
                return false;
            }
        }

        public bool OpenSkypFile(string skypPath, FormEx parentWindow = null)
        {
            var skypSupport = new SkypSupport(this);
            return skypSupport.Open(skypPath, Settings.Default.ServerList, parentWindow);
        }

        private AuditLogEntry AskForLogEntry(FormEx parentWindow)
        {
            AuditLogEntry result = null;
            Invoke((Action)(() =>
            {
                using (var alert = new AlertDlg(
                    AuditLogStrings
                        .SkylineWindow_AskForLogEntry_The_audit_log_does_not_match_the_current_document__Would_you_like_to_add_a_log_entry_describing_the_changes_made_to_the_document_,
                    MessageBoxButtons.YesNo))
                {
                    if (alert.ShowDialog(parentWindow ?? this) == DialogResult.Yes)
                    {
                        using (var docChangeEntryDlg = new DocumentChangeLogEntryDlg())
                        {
                            docChangeEntryDlg.ShowDialog(parentWindow ?? this);
                            result = docChangeEntryDlg.Entry;
                            return;
                        }
                    }

                    result = AuditLogEntry.CreateUndocumentedChangeEntry();
                }
            }));
            return result;
        }

        /// <summary>
        /// Used in testing to know whether a document changed event comes from opening a file.
        /// </summary>
        private bool IsOpeningFile { get; set; }

        public bool OpenFile(string path, FormEx parentWindow = null)
        {
            // Remove any extraneous temporary chromatogram spill files.
            // ReSharper disable LocalizableElement
            var spillDirectory = Path.Combine(Path.GetDirectoryName(path) ?? "", "xic");
            // ReSharper restore LocalizableElement
            if (Directory.Exists(spillDirectory))
                DirectoryEx.SafeDelete(spillDirectory);

            Exception exception = null;
            SrmDocument document = null;

            // A fairly common support question is "why won't this Skyline file open?" when they are actually
            // trying to open a .skyd file or somesuch.  Probably an artifact of Windows hiding file extensions.
            // Try to work around it by finding a plausible matching .sky file when asked to open a .sky? file.
            if (!path.EndsWith(SrmDocument.EXT) && !SrmDocument.IsSkylineFile(path, out _)) // Tolerate rename, eg foo.ski
            {
                path = SrmDocument.FindSiblingSkylineFile(path);
            }

            try
            {
                using (var longWaitDlg = new LongWaitDlg(this)
                {
                    Text = Resources.SkylineWindow_OpenFile_Loading___,
                    Message = Path.GetFileName(path),
                    ProgressValue = 0
                })
                {
                    longWaitDlg.PerformWork(parentWindow ?? this, 500, progressMonitor =>
                    {
                        string skylineDocumentHash;
                        using (var hashingStreamReader = new HashingStreamReaderWithProgress(path, progressMonitor))
                        {
                            // Wrap stream in XmlReader so that BaseUri is known
                            var reader = XmlReader.Create(hashingStreamReader,
                                new XmlReaderSettings() { IgnoreWhitespace = true },
                                path);

                            XmlSerializer ser = new XmlSerializer(typeof (SrmDocument));
                            document = (SrmDocument) ser.Deserialize(reader);
                            skylineDocumentHash = hashingStreamReader.Stream.Done();
                        }

                        try
                        {
                            document = document.ReadAuditLog(path, skylineDocumentHash, ()=>AskForLogEntry(parentWindow));
                        }
                        catch (Exception e)
                        {
                            throw new AuditLogException(
                                string.Format(AuditLogStrings.AuditLogException_Error_when_loading_document_audit_log__0, path), e);

                        }
                    });

                    if (longWaitDlg.IsCanceled)
                        document = null;
                }
            }
            catch (Exception x)
            {
                var ex = x;
                if (AuditLogException.IsAuditLogInvolved(x))
                {
                    MessageDlg.ShowWithException(parentWindow ?? this, 
                        AuditLogException.GetMultiLevelMessage(x),
                        x);
                }
                else
                {
                    exception = x;
                    // Was that even a Skyline file?
                    if (!SrmDocument.IsSkylineFile(path, out var explained))
                    {
                        exception = new IOException(
                            explained); // Offer a more helpful explanation than that from the failed XML parser
                    }
                }
            }

            if (exception == null)
            {
                if (document == null)
                    return false;

                try
                {
                    document = ConnectDocument(parentWindow ?? this, document, path);
                    if (document == null || !CheckResults(document, path, parentWindow))
                        return false;

                    // Make sure settings lists contain correct values for
                    // this document.
                    // ReSharper disable once PossibleNullReferenceException
                    document.Settings.UpdateLists(path);
                }
                catch (Exception x)
                {
                    exception = x;
                }
            }

            if (exception == null)
            {
                try
                {
                    IsOpeningFile = true;

                    using (new SequenceTreeForm.LockDoc(_sequenceTreeForm))
                    {
                        // Switch over to the opened document
                        SwitchDocument(document, path);
                    }
                    // Locking the sequenceTree can throw off the node count status
                    UpdateNodeCountStatus();
                }
                catch (Exception x)
                {
                    exception = x;
                }
                finally
                {
                    IsOpeningFile = false;
                }
            }

            if (exception != null)
            {
                new MessageBoxHelper(parentWindow ?? this).ShowXmlParsingError(
                    string.Format(Resources.SkylineWindow_OpenFile_Failure_opening__0__, path), path, exception);
                return false;
            }

            if (SequenceTree != null && SequenceTree.Nodes.Count > 0 && !SequenceTree.RestoredFromPersistentString)
                SequenceTree.SelectedNode = SequenceTree.Nodes[0];

            // Once user has opened an existing document, stop reminding them to set a default UI mode
            if (string.IsNullOrEmpty(Settings.Default.UIMode))
            {
                // ReSharper disable PossibleNullReferenceException
                var mode = document.DocumentType == SrmDocument.DOCUMENT_TYPE.none
                    ? SrmDocument.DOCUMENT_TYPE.proteomic
                    : document.DocumentType;
                // ReSharper restore PossibleNullReferenceException
                Settings.Default.UIMode = mode.ToString();
            }
            
            return true;
        }

        private SrmDocument ConnectDocument(IWin32Window parent, SrmDocument document, string path)
        {
            document = ConnectLibrarySpecs(parent, document, path);
            if (document != null)
                document = ConnectBackgroundProteome(parent, document, path);
            if (document != null)
                document = ConnectIrtDatabase(parent, document, path);
            if (document != null)
                document = ConnectOptimizationDatabase(parent, document, path);
            if (document != null)
                document = ConnectIonMobilityLibrary(parent, document, path);
            return document;
        }

        private SrmDocument ConnectLibrarySpecs(IWin32Window parent, SrmDocument document, string documentPath)
        {
            string docLibFile = null;
            if (!string.IsNullOrEmpty(documentPath) && document.Settings.PeptideSettings.Libraries.HasDocumentLibrary)
            {
                docLibFile = BiblioSpecLiteSpec.GetLibraryFileName(documentPath);
                if (!File.Exists(docLibFile))
                {
                    MessageDlg.Show(parent, string.Format(Resources.SkylineWindow_ConnectLibrarySpecs_Could_not_find_the_spectral_library__0__for_this_document__Without_the_library__no_spectrum_ID_information_will_be_available_, docLibFile));
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs((library, librarySpec) =>
                {
                    string name = library != null ? library.Name : librarySpec.Name;
                    LibrarySpec spec;
                    if (Settings.Default.SpectralLibraryList.TryGetValue(name, out spec))
                    {
                        if (File.Exists(spec.FilePath))
                            return spec;                        
                    }
                    if (documentPath == null)
                        return null;

                    string fileName = library != null ? library.FileNameHint : Path.GetFileName(librarySpec.FilePath);
                    if (fileName != null)
                    {
                        // First look for the file name in the document directory
                        string pathLibrary = PathEx.FindExistingRelativeFile(documentPath, fileName);
                        if (pathLibrary != null)
                            return CreateLibrarySpec(library, librarySpec, pathLibrary, true);
                        // In the user's default library directory
                        pathLibrary = Path.Combine(Settings.Default.LibraryDirectory ?? string.Empty, fileName);
                        if (File.Exists(pathLibrary))
                            return CreateLibrarySpec(library, librarySpec, pathLibrary, false);
                    }

                    using (var dlg = new MissingFileDlg
                                  {
                                      ItemName = name,
                                      ItemType = Resources.SkylineWindow_ConnectLibrarySpecs_Spectral_Library,
                                      Filter = library != null ? library.SpecFilter : librarySpec.Filter,
                                      FileHint = fileName,
                                      FileDlgInitialPath = Path.GetDirectoryName(documentPath),
                                      Title = Resources.SkylineWindow_ConnectLibrarySpecs_Find_Spectral_Library
                                  })
                    {
                        if (dlg.ShowDialog(parent) == DialogResult.OK)
                        {
                            Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FilePath);
                            return CreateLibrarySpec(library, librarySpec, dlg.FilePath, false);
                        }
                    }

                    return null;
                }, docLibFile);

            if (settings == null)
                return null; // User cancelled

            if (ReferenceEquals(settings, document.Settings))
                return document;
            
            // If the libraries were moved to disconnected state, then avoid updating
            // the document tree for this change, or it will strip all the library
            // information off the document nodes.
            if (settings.PeptideSettings.Libraries.DisconnectedLibraries != null)
                return document.ChangeSettingsNoDiff(settings);

            return document.ChangeSettings(settings);
        }

        private static LibrarySpec CreateLibrarySpec(Library library, LibrarySpec librarySpec, string pathLibrary, bool local)
        {
            var newLibrarySpec = library != null
                ? library.CreateSpec(pathLibrary)
                : librarySpec.ChangeFilePath(pathLibrary);
            if (local)
                newLibrarySpec = newLibrarySpec.ChangeDocumentLocal(true);
            return newLibrarySpec;
        }

        private SrmDocument ConnectIrtDatabase(IWin32Window parent, SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIrtDatabase(calc => FindIrtDatabase(parent, documentPath, calc));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }


        private RCalcIrt FindIrtDatabase(IWin32Window parent, string documentPath, RCalcIrt irtCalc)
        {

            RetentionScoreCalculatorSpec result;
            if (Settings.Default.RTScoreCalculatorList.TryGetValue(irtCalc.Name, out result))
            {
                var calc = result as RCalcIrt;
                if (calc != null && File.Exists(calc.DatabasePath))
                    return calc;
            }
            if (documentPath == null)
                return null;

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, irtCalc.DatabasePath);
            if (filePath != null)
            {
                try
                {
                    return irtCalc.ChangeDatabasePath(filePath);
                }
                catch (CalculatorException)
                {
                    //Todo: should this fail silenty or raise another dialog box?
                }
            }

            do
            {
                using (var dlg = new MissingFileDlg
                         {
                             ItemName = irtCalc.Name,
                             ItemType = Resources.SkylineWindow_FindIrtDatabase_iRT_Calculator,
                             Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_FindIrtDatabase_iRT_Database_Files, IrtDb.EXT),
                             FileHint = Path.GetFileName(irtCalc.DatabasePath),
                             FileDlgInitialPath = Path.GetDirectoryName(documentPath),
                             Title = Resources.SkylineWindow_FindIrtDatabase_Find_iRT_Calculator
                         })
                {
                    if (dlg.ShowDialog(parent) == DialogResult.OK)
                    {
                        if (dlg.FilePath == null)
                            return RCalcIrt.NONE;
                        
                        try
                        {
                            return irtCalc.ChangeDatabasePath(dlg.FilePath);
                        }
                        catch (DatabaseOpeningException e)
                        {
                            var message = TextUtil.SpaceSeparate(
                                Resources.SkylineWindow_FindIrtDatabase_The_database_file_specified_could_not_be_opened,
                                e.Message);
                            MessageDlg.Show(parent, message);
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            while (true);
        }

        private SrmDocument ConnectOptimizationDatabase(IWin32Window parent, SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectOptimizationDatabase(lib => FindOptimizationDatabase(parent, documentPath, lib));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }


        private OptimizationLibrary FindOptimizationDatabase(IWin32Window parent, string documentPath, OptimizationLibrary optLib)
        {
            if (optLib.IsNone)
                return optLib;

            OptimizationLibrary lib;
            if (Settings.Default.OptimizationLibraryList.TryGetValue(optLib.Name, out lib))
            {
                if (lib != null && File.Exists(lib.DatabasePath))
                    return lib;
            }
            if (documentPath == null)
                return null;

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, optLib.DatabasePath);
            if (filePath != null)
            {
                try
                {
                    return optLib.ChangeDatabasePath(filePath);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                    //Todo: should this fail silenty or raise another dialog box?
                }
            }

            do
            {
                using (var dlg = new MissingFileDlg
                {
                    ItemName = optLib.Name,
                    ItemType = Resources.SkylineWindow_FindOptimizationDatabase_Optimization_Library,
                    Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_FindOptimizationDatabase_Optimization_Library_Files, OptimizationDb.EXT),
                    FileHint = Path.GetFileName(optLib.DatabasePath),
                    FileDlgInitialPath = Path.GetDirectoryName(documentPath),
                    Title = Resources.SkylineWindow_FindOptimizationDatabase_Find_Optimization_Library
                })
                {
                    if (dlg.ShowDialog(parent) == DialogResult.OK)
                    {
                        if (dlg.FilePath == null)
                            return OptimizationLibrary.NONE;

                        try
                        {
                            return optLib.ChangeDatabasePath(dlg.FilePath);
                        }
                        catch (OptimizationsOpeningException e)
                        {
                            var message = TextUtil.SpaceSeparate(
                                Resources.SkylineWindow_FindOptimizationDatabase_The_database_file_specified_could_not_be_opened_,
                                e.Message);
                            MessageDlg.Show(parent, message);
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            while (true);
        }

        private SrmDocument ConnectIonMobilityLibrary(IWin32Window parent, SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIonMobilityLibrary(imsdb => FindIonMobilityLibrary(parent, documentPath, imsdb));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private IonMobilityLibrary FindIonMobilityLibrary(IWin32Window parent, string documentPath, IonMobilityLibrary ionMobilityLibrary)
        {

            IonMobilityLibrary result;
            if (Settings.Default.IonMobilityLibraryList.TryGetValue(ionMobilityLibrary.Name, out result))
            {
                if (result != null && File.Exists(result.FilePath))
                    return result;
            }
            if (documentPath == null)
                return null;

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, ionMobilityLibrary.FilePath);
            if (filePath != null)
            {
                try
                {
                    return ionMobilityLibrary.ChangeDatabasePath(filePath);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch 
                {
                    //Todo: should this fail silenty or raise another dialog box?
                }
            }

            do
            {
                using (var dlg = new MissingFileDlg
                {
                    ItemName = ionMobilityLibrary.Name,
                    ItemType = Resources.SkylineWindow_FindIonMobilityLibrary_Ion_Mobility_Library,
                    Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_FindIonMobilityDatabase_ion_mobility_library_files, IonMobilityDb.EXT),
                    FileHint = Path.GetFileName(ionMobilityLibrary.FilePath),
                    FileDlgInitialPath = Path.GetDirectoryName(documentPath),
                    Title = Resources.SkylineWindow_FindIonMobilityLibrary_Find_Ion_Mobility_Library
                })
                {
                    if (dlg.ShowDialog(parent) == DialogResult.OK)
                    {
                        if (dlg.FilePath == null)
                            return IonMobilityLibrary.NONE;

                        try
                        {
                            return ionMobilityLibrary.ChangeDatabasePath(dlg.FilePath);
                        }
                        catch (DatabaseOpeningException e)
                        {
                            var message = TextUtil.SpaceSeparate(
                                Resources.SkylineWindow_FindIonMobilityDatabase_The_ion_mobility_library_specified_could_not_be_opened_,
                                e.Message); 
                            MessageDlg.Show(parent, message);
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            while (true);
        }

        private SrmDocument ConnectBackgroundProteome(IWin32Window parent ,SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectBackgroundProteome(backgroundProteomeSpec =>
                FindBackgroundProteome(parent, documentPath, backgroundProteomeSpec));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private BackgroundProteomeSpec FindBackgroundProteome(IWin32Window parent, string documentPath, BackgroundProteomeSpec backgroundProteomeSpec)
        {
            var result = Settings.Default.BackgroundProteomeList.GetBackgroundProteomeSpec(backgroundProteomeSpec.Name);
            if (result != null)
            {
                if (File.Exists(result.DatabasePath))
                    return result;
            }
            if (documentPath == null)
                return null;

            // Is the saved path correct?  Then just use that.
            if (File.Exists(backgroundProteomeSpec.DatabasePath))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, backgroundProteomeSpec.DatabasePath);

            // First look for the file name in the document directory
            string pathBackgroundProteome = PathEx.FindExistingRelativeFile(documentPath, backgroundProteomeSpec.DatabasePath);
            if (pathBackgroundProteome != null)
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            pathBackgroundProteome = Path.Combine(Settings.Default.ProteomeDbDirectory ?? string.Empty, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            using (var dlg = new MissingFileDlg
                    {
                        FileHint = fileName,
                        ItemName = backgroundProteomeSpec.Name,
                        ItemType = Resources.SkylineWindow_FindBackgroundProteome_Background_Proteome,
                        Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_FindBackgroundProteome_Proteome_File, ProteomeDb.EXT_PROTDB),
                        FileDlgInitialPath = Settings.Default.ProteomeDbDirectory,
                        Title = Resources.SkylineWindow_FindBackgroundProteome_Find_Background_Proteome
                    })
            {
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    if (dlg.FilePath == null)
                    {
                        return BackgroundProteomeList.GetDefault();
                    }
                    
                    Settings.Default.ProteomeDbDirectory = Path.GetDirectoryName(dlg.FilePath);
                    
                    return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, dlg.FilePath);
                }
            }
            return null;
        }

        private bool CheckResults(SrmDocument document, string path, FormEx parent)
        {
            string pathCache = ChromatogramCache.FinalPathForName(path, null);
            if (!document.Settings.HasResults)
            {
                // On open, make sure a document with no results does not have a
                // data cache file, since one may have been left behind on a Save As.
                FileEx.SafeDelete(pathCache, true);
            }
            else if (!File.Exists(pathCache) &&
                // For backward compatibility, check to see if any per-replicate
                // cache files exist.
                !File.Exists(ChromatogramCache.FinalPathForName(path,
                    document.Settings.MeasuredResults.Chromatograms[0].Name)))
            {
                // It has become clear that showing a message box about rebuilding
                // the cache on open is shocking to people, and they immediately
                // worry that a "rebuild" will cause them to lose work.  So, first
                // figure out if any of the sample files are missing from places
                // Skyline will find them.
                var missingFiles = new List<string>();
                //var foundFiles = new List<string>();
                foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (var pathFileSample in chromSet.MSDataFilePaths)
                    {
                        var msDataFilePath = pathFileSample as MsDataFilePath;
                        if (null == msDataFilePath)
                        {
                            continue;
                        }
                        string pathFile = msDataFilePath.FilePath;
                        if (missingFiles.Contains(pathFile))
                            continue;
                        string pathPartCache = ChromatogramCache.PartPathForName(path,  pathFileSample);
                        if (File.Exists(pathFile) ||
                            Directory.Exists(pathFile) || // some sample "files" are actually directories (.d etc)
                            File.Exists(pathPartCache) ||
                            File.Exists(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileName(pathFile) ?? string.Empty)))
                        {
                            //foundFiles.Add(pathFile);
                        }
                        else
                        {
                            missingFiles.Add(pathFile);
                        }
                    }
                }
                // If all necessary data is present, just start rebuilding without asking
                // to avoid shocking the user.
                if (missingFiles.Count == 0)
                    return true;

                // TODO: Ask the user to locate the missing data files
                string missingFilesString = TextUtil.LineSeparate(missingFiles);
                string message = TextUtil.LineSeparate(string.Format(
                                    Resources.SkylineWindow_CheckResults_The_data_file___0___is_missing__and_the_following_original_instrument_output_could_not_be_found_,
                                    ChromatogramCache.FinalPathForName(path, null)),
                                    string.Empty,
                                    missingFilesString,
                                    string.Empty,
                                    Resources.SkylineWindow_CheckResults_Click_OK_to_open_the_document_anyway);

                if (MultiButtonMsgDlg.Show(parent ?? this, message, MultiButtonMsgDlg.BUTTON_OK) == DialogResult.Cancel)
                {
                    return false;
                }                    
            }

            return true;
        }

        private void saveMenuItem_Click(object sender, EventArgs e)
        {
            SaveDocument();
        }

        private void saveAsMenuItem_Click(object sender, EventArgs e)
        {
            SaveDocumentAs();
        }

        private bool CheckSaveDocument()
        {
            if (Dirty)
            {
                var result = MultiButtonMsgDlg.Show(this,
                    Resources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                    Resources.SkylineWindow_CheckSaveDocument_Yes, Resources.SkylineWindow_CheckSaveDocument_No, true);
                switch (result)
                {
                    case DialogResult.Yes:
                        return SaveDocument();
                    case DialogResult.Cancel:
                        return false;
                }
            }
            return true;
        }

        public bool SaveDocument()
        {
            string fileName = DocumentFilePath;
            if (string.IsNullOrEmpty(fileName))
                return SaveDocumentAs();
            
            return SaveDocument(fileName);
        }

        private bool SaveDocumentAs()
        {
            // Make sure results are loaded before performaing a Save As,
            // since the results cache must be copied to the new location.
            if (!DocumentUI.IsSavable)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_SaveDocumentAs_The_document_must_be_fully_loaded_before_it_can_be_saved_to_a_new_name);
                return false;
            }

            using (var dlg = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.ActiveDirectory,
                OverwritePrompt = true,
                DefaultExt = SrmDocument.EXT,
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC)
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                    dlg.FileName = Path.GetFileName(DocumentFilePath);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (SaveDocument(dlg.FileName))
                        return true;
                }
            }
            return false;
        }

        public bool SaveDocument(String fileName, bool includingCacheFile = true)
        {
            if (string.IsNullOrEmpty(DocumentUI.Settings.DataSettings.DocumentGuid) ||
                !Equals(DocumentFilePath, fileName))
            {
                SrmDocument docOriginal;
                SrmDocument docNew;
                do
                {
                    docOriginal = Document;
                    docNew =
                        docOriginal.ChangeSettings(
                            docOriginal.Settings.ChangeDataSettings(
                                docOriginal.Settings.DataSettings.ChangeDocumentGuid()));
                } while (!SetDocument(docNew, docOriginal));
            }

            SrmDocument document = Document;

            try
            {
                using (var saver = new FileSaver(fileName))
                {
                    saver.CheckException();

                    using (var longWaitDlg = new LongWaitDlg(this)
                        {
                            Text = Resources.SkylineWindow_SaveDocument_Saving___,
                            Message = Path.GetFileName(fileName)
                        })
                    {
                        longWaitDlg.PerformWork(this, 800, progressMonitor =>
                        {
                            document.SerializeToFile(saver.SafeName, fileName, SkylineVersion.CURRENT, progressMonitor);
                            // If the user has chosen "Save As", and the document has a
                            // document specific spectral library, copy this library to 
                            // the new name.
                            if (!Equals(DocumentFilePath, fileName))
                                SaveDocumentLibraryAs(fileName);

                            saver.Commit();
                        });

                        // Sometimes this catches a cancellation that doesn't throw an OperationCanceledException.
                        if (longWaitDlg.IsCanceled)
                            return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex) 
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_SaveDocument_Failed_writing_to__0__, fileName), ex.Message);
                MessageDlg.ShowWithException(this, message, ex);
                return false;
            }

            DocumentFilePath = fileName;
            _savedVersion = document.UserRevisionIndex;
            SavedDocumentFormat = DocumentFormat.CURRENT;
            SetActiveFile(fileName);

            // Make sure settings lists contain correct values for this document.
            document.Settings.UpdateLists(DocumentFilePath);

            try
            {
                SaveLayout(fileName);

                // CONSIDER: Is this really optional?
                if (includingCacheFile)
                {
                    using (var longWaitDlg = new LongWaitDlg(this)
                    {
                        Text = Resources.SkylineWindow_SaveDocument_Optimizing_data_file___,
                        Message = Path.GetFileName(fileName)
                    })
                    {
                        longWaitDlg.PerformWork(this, 800, () =>
                            OptimizeCache(fileName, longWaitDlg));
                    }
                }
            }

            // We allow silent failures because it is OK for the cache to remain unoptimized
            // or the layout to not be saved.  These aren't critical as long as the document
            // was saved correctly.
            catch (UnauthorizedAccessException) {}
            catch (IOException) {}
            catch (OperationCanceledException) {}
            catch (TargetInvocationException) {}

            return true;
        }

        private void OptimizeCache(string fileName, ILongWaitBroker progress)
        {
            // Optimize the results cache to get rid of any unnecessary
            // chromatogram data.
            var settings = Document.Settings;
            if (settings.HasResults)
            {
                var results = settings.MeasuredResults;
                if (results.IsLoaded)
                {
                    var resultsNew = results.OptimizeCache(fileName, _chromatogramManager.StreamManager, progress);
                    if (!ReferenceEquals(resultsNew, results))
                    {
                        SrmDocument docNew, docCurrent;
                        do
                        {
                            docCurrent = Document;
                            docNew = docCurrent.ChangeMeasuredResults(resultsNew);
                        }
                        while (!SetDocument(docNew, docCurrent));
                    }
                }
            }
            else
            {
                string cachePath = ChromatogramCache.FinalPathForName(DocumentFilePath, null);
                FileEx.SafeDelete(cachePath, true);
            }
        }

        private void SaveDocumentLibraryAs(string newDocFilePath)
        {
            string oldDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath);
            string oldRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(oldDocLibFile);
            // If the document has a document-specific library, and the files for it
            // exist on disk, and it's not stale due to conversion of document to small molecule representation
            var document = Document;
            string newDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(newDocFilePath);
            if (document.Settings.PeptideSettings.Libraries.HasDocumentLibrary
                && File.Exists(oldDocLibFile)
                && !Equals(newDocLibFile.Replace(BiblioSpecLiteSpec.DotConvertedToSmallMolecules, string.Empty), oldDocLibFile))
            {
                using (var saverLib = new FileSaver(newDocLibFile))
                {
                    FileSaver saverRedundant = null;
                    if (File.Exists(oldRedundantDocLibFile))
                    {
                        string newRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(newDocFilePath);
                        saverRedundant = new FileSaver(newRedundantDocLibFile);
                    }
                    using (saverRedundant)
                    {
                        saverLib.CopyFile(oldDocLibFile);
                        if (saverRedundant != null)
                        {
                            saverRedundant.CopyFile(oldRedundantDocLibFile);
                        }
                        saverLib.Commit();
                        if (saverRedundant != null)
                        {
                            saverRedundant.Commit();
                        }
                    }
                }

                // Update the document library settings to point to the new library.
                SrmDocument docOriginal, docNew;
                do
                {
                    docOriginal = Document;
                    docNew = docOriginal.ChangeSettingsNoDiff(docOriginal.Settings.ChangePeptideLibraries(libraries =>
                        libraries.ChangeDocumentLibraryPath(newDocFilePath)));                        
                }
                while (!SetDocument(docNew, docOriginal));
            }
        }

        private void SaveLayout(string fileName)
        {
            using (var saverUser = new FileSaver(GetViewFile(fileName)))
            {
                if (saverUser.CanSave())
                {
                    dockPanel.SaveAsXml(saverUser.SafeName);
                    saverUser.Commit();
                }
            }
        }

        private void SetActiveFile(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                // Remember the active directory.
                Settings.Default.ActiveDirectory = Path.GetDirectoryName(path);

                // Store the path in the MRU.
                List<string> mruList = Settings.Default.MruList;
                if (mruList.Count == 0 || !Equals(path, mruList[0]))
                {
                    mruList.Remove(path);
                    mruList.Insert(0, path);
                    int len = Settings.Default.MruMemoryLength;
                    if (mruList.Count > len)
                        mruList.RemoveRange(len, mruList.Count - len);
                }
            }

            UpdateTitle();
        }

        private void shareDocumentMenuItem_Click(object sender, EventArgs e)
        {
            ShareDocument();
        }

        public void ShareDocument()
        {
            ShareDocument(null);
        }

        public void ShareDocument(string skyZipFileName)
        {
            var document = DocumentUI;
            if (!document.IsLoaded)
            {
                try
                {
                    // Get the description of what is not loaded into the "More Info" section of the message box
                    // This is helpful for diagnosis, but not yet presented in a form intended for the user
                    throw new IOException(TextUtil.LineSeparate(document.NonLoadedStateDescriptions));
                }
                catch (Exception e)
                {
                    MessageDlg.ShowWithException(this, Resources.SkylineWindow_shareDocumentMenuItem_Click_The_document_must_be_fully_loaded_before_it_can_be_shared, e);
                }
                return;
            }

            if (!CheckSaveDocument())
            {
                return;
            }
            document = DocumentUI;
            string fileName = DocumentFilePath;
            ShareType shareType;
            DocumentFormat? fileFormatOnDisk = GetFileFormatOnDisk();
            using (var dlgType = new ShareTypeDlg(document, fileFormatOnDisk))
            {
                if (dlgType.ShowDialog(this) == DialogResult.Cancel)
                    return;
                shareType = dlgType.ShareType;
            }

            if (skyZipFileName == null)
            {
                using (var dlg = new SaveFileDialog
                {
                    Title = Resources.SkylineWindow_shareDocumentMenuItem_Click_Share_Document,
                    OverwritePrompt = true,
                    DefaultExt = SrmDocumentSharing.EXT_SKY_ZIP,
                    SupportMultiDottedExtensions = true,
                    Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_shareDocumentMenuItem_Click_Skyline_Shared_Documents, SrmDocumentSharing.EXT),
                })
                {
                    if (fileName != null)
                    {
                        dlg.InitialDirectory = Path.GetDirectoryName(fileName);
                        dlg.FileName = Path.GetFileNameWithoutExtension(fileName) + SrmDocumentSharing.EXT_SKY_ZIP;
                    }
                    if (dlg.ShowDialog(this) == DialogResult.Cancel)
                        return;
                    skyZipFileName = dlg.FileName;
                }
            }
            ShareDocument(skyZipFileName, shareType);
        }

        private DocumentFormat? GetFileFormatOnDisk()
        {
            return !Dirty && null != DocumentFilePath ? SavedDocumentFormat : (DocumentFormat?) null;
        }

        public bool ShareDocument(string fileDest, ShareType shareType)
        {
            try
            {
                bool success;
                using (var longWaitDlg = new LongWaitDlg { Text = Resources.SkylineWindow_ShareDocument_Compressing_Files, })
                {
                    var sharing = new SrmDocumentSharing(DocumentUI, DocumentFilePath, fileDest, shareType);
                    if (shareType.MustSaveNewDocument)
                    {
                        var tempDocumentPath = Path.Combine(sharing.EnsureTempDir().DirPath,
                            sharing.GetDocumentFileName());
                        SaveLayout(tempDocumentPath);
                        sharing.ViewFilePath = GetViewFile(tempDocumentPath);
                    }
                    else if (DocumentFilePath != null)
                    {
                        string viewFilePath = GetViewFile(DocumentFilePath);
                        if (File.Exists(viewFilePath))
                        {
                            sharing.ViewFilePath = viewFilePath;
                        }
                    }

                    longWaitDlg.PerformWork(this, 1000, sharing.Share);
                    success = !longWaitDlg.IsCanceled;
                }
                return success;
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ShareDocument_Failed_attempting_to_create_sharing_file__0__, fileDest),
                                                    x.Message); 
                MessageDlg.ShowWithException(this, message, x);
            }
            return false;
        }

        private void exportTransitionListMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportMethodDialog(ExportFileType.List);
        }

        private void exportIsolationListMenuItem_Click(object sender, EventArgs e)
        {
            var isolationScheme = DocumentUI.Settings.TransitionSettings.FullScan.IsolationScheme;
            if (Document.MoleculeCount == 0 && (isolationScheme == null || isolationScheme.FromResults))
            {
                MessageDlg.Show(this,
                    Resources.SkylineWindow_exportIsolationListMenuItem_Click_There_is_no_isolation_list_data_to_export);
                return;
            }
            ShowExportMethodDialog(ExportFileType.IsolationList);
        }

        private void exportMethodMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportMethodDialog(ExportFileType.Method);
        }

        public DialogResult ShowExportMethodDialog(ExportFileType fileType)
        {
            using (ExportMethodDlg dlg = new ExportMethodDlg(DocumentUI, fileType))
            {
                return dlg.ShowDialog(this);
            }
        }

        private void exportSpectralLibraryMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportSpectralLibraryDialog();
        }

        public void ShowExportSpectralLibraryDialog()
        {
            if (Document.MoleculeTransitionGroupCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_at_least_one_peptide_precursor_to_export_a_spectral_library_);
                return;
            }
            else if (!Document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowExportSpectralLibraryDialog_The_document_must_contain_results_to_export_a_spectral_library_);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Export_Spectral_Library,
                OverwritePrompt = true,
                DefaultExt = BiblioSpecLiteSpec.EXT,
                Filter = TextUtil.FileDialogFiltersAll(BiblioSpecLiteSpec.FILTER_BLIB)
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);

                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                try
                {
                    using (var longWaitDlg = new LongWaitDlg
                    {
                        Text = Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Export_Spectral_Library,
                        Message = string.Format(Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Exporting_spectral_library__0____, Path.GetFileName(dlg.FileName))
                    })
                    {
                        longWaitDlg.PerformWork(this, 800, monitor =>
                            new SpectralLibraryExporter(Document, DocumentFilePath).ExportSpectralLibrary(dlg.FileName, monitor));
                    }
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, TextUtil.LineSeparate(string.Format(
                        Resources.SkylineWindow_ShowExportSpectralLibraryDialog_Failed_exporting_spectral_library_to__0__, dlg.FileName), x.Message), x);
                }
            }
        }


        private void exportReportMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportReportDialog();
        }

        public void ShowExportReportDialog()
        {
            using (var dlg = new ExportLiveReportDlg(this))
            {
                dlg.ShowDialog(this);
            }
        }

        private void espFeaturesMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportEspFeaturesDialog();
        }

        public void ShowExportEspFeaturesDialog()
        {
            if (DocumentUI.MoleculeCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowExportEspFeaturesDialog_The_document_must_contain_targets_for_which_to_export_features_);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.SkylineWindow_ShowExportEspFeaturesDialog_Export_ESP_Features,
                OverwritePrompt = true,
                DefaultExt = EspFeatureCalc.EXT,
                Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_ShowExportEspFeaturesDialog_ESP_Feature_Files,EspFeatureCalc.EXT),
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                    dlg.FileName = Path.GetFileNameWithoutExtension(DocumentFilePath) + EspFeatureCalc.EXT;
                }
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                try
                {
                    EspFeatureCalc.WriteFeatures(dlg.FileName,
                        DocumentUI.Molecules.Select(nodePep => nodePep.Peptide.Target), LocalizationHelper.CurrentCulture);
                }
                catch (IOException x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ShowExportEspFeaturesDialog_Failed_attempting_to_save_ESP_features_to__0__, dlg.FileName),
                                    x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                }
            }
        }


        private void chromatogramsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowChromatogramFeaturesDialog();
        }

        public void ShowChromatogramFeaturesDialog()
        {
            if (!DocumentUI.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowChromatogramFeaturesDialog_The_document_must_have_imported_results_);
                return;
            }
            if (DocumentUI.MoleculeCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowChromatogramFeaturesDialog_The_document_must_have_targets_for_which_to_export_chromatograms_);
                return;
            }

            using (var dlg = new ExportChromatogramDlg(DocumentUI, DocumentFilePath))
            {
                dlg.ShowDialog(this);
            }
        }

        public void ShowReintegrateDialog()
        {
            RefineMenu.ShowReintegrateDialog();
        }

        private void mProphetFeaturesMenuItem_Click(object sender, EventArgs e)
        {
            ShowMProphetFeaturesDialog();
        }

        public void ShowMProphetFeaturesDialog()
        {
            if (!DocumentUI.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowMProphetFeaturesDialog_The_document_must_have_imported_results_);
                return;
            }
            if (DocumentUI.MoleculeCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowMProphetFeaturesDialog_The_document_must_contain_targets_for_which_to_export_features_);
                return;
            }

            using (var dlg = new MProphetFeaturesDlg(DocumentUI, DocumentFilePath))
            {
                dlg.ShowDialog(this);
            }
        }

        
        private void peakBoundariesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!DocumentUI.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowChromatogramFeaturesDialog_The_document_must_have_imported_results_);
            }
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_ImportPeakBoundaries_Import_PeakBoundaries,
                CheckPathExists = true
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ImportPeakBoundariesFile(dlg.FileName);
                }
            }
        }

        public void ImportPeakBoundariesFile(string peakBoundariesFile)
        {
            try
            {
                long lineCount = Helpers.CountLinesInFile(peakBoundariesFile);
                ImportPeakBoundaries(peakBoundariesFile, lineCount, Resources.SkylineWindow_ImportPeakBoundaries_Import_PeakBoundaries);
            }
            catch (Exception x)
            {
                // Specify that we want a MessageDlg that ignores UI mode
                MessageDlg.ShowWithException(this, TextUtil.LineSeparate(
                    string.Format(Resources.SkylineWindow_ImportPeakBoundariesFile_Failed_reading_the_file__0__,
                        peakBoundariesFile), x.Message), x, true); // "true" here means that we want to ignore the UI mode in the context of the MessageDlg
            }
        }

        private static void AddMessageInfo<T>(IList<MessageInfo> messageInfos, MessageType type, SrmDocument.DOCUMENT_TYPE docType, IEnumerable<T> items)
        {
            messageInfos.AddRange(items.Select(item => new MessageInfo(type, docType, item)));
        }

        private void ImportPeakBoundaries(string fileName, long lineCount, string description)
        {
            var docCurrent = DocumentUI;
            SrmDocument docNew = null;

            var peakBoundaryImporter = new PeakBoundaryImporter(docCurrent);
            using (var longWaitDlg = new LongWaitDlg(this) { Text = description })
            {       
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                           docNew = peakBoundaryImporter.Import(fileName, longWaitBroker, lineCount));


                if (docNew == null)
                    return;
                if (!peakBoundaryImporter.UnrecognizedPeptidesCancel(this))
                    return;
                if (longWaitDlg.IsDocumentChanged(docCurrent))
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_ImportPeakBoundaries_Unexpected_document_change_during_operation);
                    return;
                }                
            }

            ModifyDocument(description, doc =>
            {
                if (!ReferenceEquals(doc, docCurrent))
                    throw new InvalidDataException(Resources.SkylineWindow_ImportPeakBoundaries_Unexpected_document_change_during_operation);
                return docNew;
            }, docPair =>
            {
                var allInfo = new List<MessageInfo>();
                AddMessageInfo(allInfo, MessageType.removed_unrecognized_peptide, docPair.OldDocumentType, peakBoundaryImporter.UnrecognizedPeptides);
                AddMessageInfo(allInfo, MessageType.removed_unrecognized_file, docPair.OldDocumentType,
                    peakBoundaryImporter.UnrecognizedFiles.Select(AuditLogPath.Create));
                AddMessageInfo(allInfo, MessageType.removed_unrecognized_charge_state, docPair.OldDocumentType, peakBoundaryImporter.UnrecognizedChargeStates);

                return AuditLogEntry.CreateSimpleEntry(MessageType.imported_peak_boundaries, docPair.OldDocumentType,
                        Path.GetFileName(fileName))
                    .AppendAllInfo(allInfo);
            });
        }

        private void importFASTAMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_ImportFastaFile_Import_FASTA,
                InitialDirectory = Settings.Default.FastaDirectory,
                CheckPathExists = true
                // FASTA files often have no extension as well as .fasta and others
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.FastaDirectory = Path.GetDirectoryName(dlg.FileName);
                    ImportFastaFile(dlg.FileName);
                }
            }
        }

        public void ImportFastaFile(string fastaFile)
        {
            try
            {
                long lineCount = Helpers.CountLinesInFile(fastaFile);
                using (var readerFasta = new StreamReader(fastaFile))
                {
                    ImportFasta(readerFasta, lineCount, false, Resources.SkylineWindow_ImportFastaFile_Import_FASTA, new ImportFastaInfo(true, fastaFile));
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                                                    fastaFile, x.Message), x);
            }
        }


        public class ImportFastaInfo
        {
            public ImportFastaInfo(bool file, string text)
            {
                File = file;
                Text = text;
            }

            public bool File { get; private set; }
            public string Text { get; private set;}
        }

        public void ImportFasta(TextReader reader, long lineCount, bool peptideList, string description, ImportFastaInfo importInfo)
        {
            SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;
            IdentityPath selectPath = null;
            var to = nodePaste != null ? nodePaste.Path : null;
            
            var docCurrent = DocumentUI;

            ModificationMatcher matcher = null;
            if(peptideList)
            {
                matcher = new ModificationMatcher();
                List<string> sequences = new List<string>();
                string line;
                var header = reader.ReadLine(); // Read past header
                while ((line = reader.ReadLine()) != null)
                {
                    string sequence = FastaSequence.NormalizeNTerminalMod(line.Trim());
                    sequences.Add(sequence);
                }
                try
                {
                    matcher.CreateMatches(docCurrent.Settings, sequences, Settings.Default.StaticModList, Settings.Default.HeavyModList);
                    var strNameMatches = matcher.FoundMatches;
                    if (!string.IsNullOrEmpty(strNameMatches))
                    {
                        var message = TextUtil.LineSeparate(Resources.SkylineWindow_ImportFasta_Would_you_like_to_use_the_Unimod_definitions_for_the_following_modifications,
                                                            string.Empty, strNameMatches);
                        if (DialogResult.Cancel == MultiButtonMsgDlg.Show(
                            this,
                            string.Format(message), Resources.SkylineWindow_ImportFasta_OK))
                        {
                            return;
                        }
                    }
                }
                catch(FormatException x)
                {
                    MessageDlg.ShowException(this, x);
                    return;
                }
                reader = new StringReader(TextUtil.LineSeparate(header, TextUtil.LineSeparate(sequences.ToArray())));
            }

            SrmDocument docNew = null;
            int emptyPeptideGroups = 0;
            using (var longWaitDlg = new LongWaitDlg(this) { Text = description })
            {
                IdentityPath nextAdded;
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                    docNew = docCurrent.ImportFasta(reader, longWaitBroker, lineCount, matcher, to, out selectPath, out nextAdded, out emptyPeptideGroups));

                if (docNew == null)
                    return;

                if (!ReferenceEquals(Document, docCurrent))
                {
                    MessageDlg.ShowWithException(this, Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation, new DocumentChangedException(Document, docCurrent));
                    return;
                }
            }

            var entryCreatorList = new AuditLogEntryCreatorList();
            // If importing the FASTA produced any childless proteins
            docNew = ImportFastaHelper.HandleEmptyPeptideGroups(this, emptyPeptideGroups, docNew, entryCreatorList);
            if (docNew == null || Equals(docCurrent, docNew))
                return;

            selectPath = null;
            using (var enumGroupsCurrent = docCurrent.MoleculeGroups.GetEnumerator())
            {
                // ReSharper disable once PossibleNullReferenceException
                foreach (PeptideGroupDocNode nodePepGroup in docNew.MoleculeGroups)
                {
                    if (enumGroupsCurrent.MoveNext() &&
                        !ReferenceEquals(nodePepGroup, enumGroupsCurrent.Current))
                    {
                        selectPath = new IdentityPath(nodePepGroup.Id);
                        break;
                    }
                }
            }

            ModifyDocument(description, doc =>
            {
                if (!ReferenceEquals(doc, docCurrent))
                    throw new InvalidDataException(
                        Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation,
                        new DocumentChangedException(doc, docCurrent));
                if (matcher != null)
                {
                    var pepModsNew = matcher.GetDocModifications(docNew);
                    // ReSharper disable PossibleNullReferenceException
                    docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                    docNew.Settings.UpdateDefaultModifications(false);
                    // ReSharper restore PossibleNullReferenceException
                }

                return docNew;
            }, docPair =>
            {
                if (importInfo == null)
                    return null;

                MessageInfo info;
                string extraInfo = null;
                if (importInfo.File)
                {
                    info = new MessageInfo(MessageType.imported_fasta, docPair.NewDocumentType, importInfo.Text);
                }
                else
                {
                    info = new MessageInfo(peptideList
                        ? MessageType.imported_peptide_list
                        : MessageType.imported_fasta_paste, 
                        docPair.NewDocumentType);
                    extraInfo = importInfo.Text;
                }

                return AuditLogEntry.CreateSingleMessageEntry(info, extraInfo)
                    .Merge(docPair, entryCreatorList);
            });

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
        }

        /// <summary>
        /// More diagnostic information to try to catch cause of failing tests
        /// </summary>
        public class DocumentChangedException : Exception
        {
            public DocumentChangedException(SrmDocument docNow, SrmDocument docOriginal)
                : base(GetMessage(docNow, docOriginal))
            {
            }

            private static string GetMessage(SrmDocument docNow, SrmDocument docOriginal)
            {
                // ReSharper disable LocalizableElement
                return TextUtil.LineSeparate(string.Format("DocRevision: before = {0}, after = {1}", docOriginal.RevisionIndex, docNow.RevisionIndex),
                    "Loaded before:", TextUtil.LineSeparate(docOriginal.NonLoadedStateDescriptionsFull),
                    "Loaded after:", TextUtil.LineSeparate(docNow.NonLoadedStateDescriptionsFull));
                // ReSharper restore LocalizableElement
            }
        }

        public void InsertSmallMoleculeTransitionList(string csvText, string description, List<string> columnPositions = null)
        {
            IdentityPath selectPath = null;
            Exception modifyingDocumentException = null;
            var transitionCount = 0;
            ModifyDocument(description, doc =>
            {
                try
                {
                    SrmDocument docNew = null;
                    selectPath = null;
                    using (var longWaitDlg = new LongWaitDlg(this) {Text = description})
                    {
                        var smallMoleculeTransitionListReader = new SmallMoleculeTransitionListCSVReader(MassListInputs.ReadLinesFromText(csvText), columnPositions);
                        if (smallMoleculeTransitionListReader.RowCount == 0)
                        {
                            throw new InvalidDataException(Resources.MassListImporter_Import_Empty_transition_list);
                        }
                        IdentityPath firstAdded;
                        longWaitDlg.PerformWork(this, 1000,
                            () => docNew = smallMoleculeTransitionListReader.CreateTargets(doc, null, out firstAdded));
                            // CONSIDER: cancelable / progress monitor ?  This is normally pretty quick.

                        transitionCount = smallMoleculeTransitionListReader.RowCount - 1;
                        if (docNew == null)
                            return doc;
                    }

                    using (var enumGroupsCurrent = doc.MoleculeGroups.GetEnumerator())
                    {
                        foreach (PeptideGroupDocNode nodePepGroup in docNew.MoleculeGroups)
                        {
                            if (enumGroupsCurrent.MoveNext() &&
                                !ReferenceEquals(nodePepGroup, enumGroupsCurrent.Current))
                            {
                                selectPath = new IdentityPath(nodePepGroup.Id);
                                break;
                            }
                        }
                    }

                    return docNew;
                }
                catch (Exception x)
                {
                    modifyingDocumentException = x;
                    return doc;
                }
            }, docPair => AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(
                transitionCount == 1
                    ? MessageType.pasted_single_small_molecule_transition
                    : MessageType.pasted_small_molecule_transition_list, docPair.NewDocumentType, transitionCount), csvText));

            if (modifyingDocumentException != null)
            {
                // If the exception is an IOException, we rethrow it in case it has line/col information
                if (modifyingDocumentException is IOException)
                {
                    throw modifyingDocumentException;
                }
                // Otherwise, we wrap the exception to preserve the callstack
                throw new AggregateException(modifyingDocumentException);
            }

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
        }

        private void importAssayLibraryMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_importAssayLibraryMenuItem_Click_Import_Assay_Library,
                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                SupportMultiDottedExtensions = true,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importAssayLibraryMenuItem_Click_Assay_Library, TextUtil.EXT_CSV, TextUtil.EXT_TSV))
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    ImportAssayLibrary(dlg.FileName);
                }
            }
        }

        public void ImportAssayLibrary(string fileName)
        {
            try
            {
                ImportAssayLibrary(new MassListInputs(fileName), Resources.SkylineWindow_importAssayLibraryMenuItem_Click_Import_Assay_Library);
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, fileName, x.Message), x);
            }
        }

        private void ImportAssayLibrary(MassListInputs inputs, string description)
        {
            if (DocumentFilePath == null &&
                (MultiButtonMsgDlg.Show(this,
                     Resources.SkylineWindow_ImportAssayLibrary_You_must_save_the_Skyline_document_in_order_to_import_an_assay_library_, MultiButtonMsgDlg.BUTTON_OK) == DialogResult.Cancel ||
                 !SaveDocumentAs()))
            {
                return;
            }

            if (File.Exists(AssayLibraryFileName) &&
                MultiButtonMsgDlg.Show(this,
                    string.Format(Resources.SkylineWindow_ImportAssayLibrary_There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created__Overwrite_, AssayLibraryName),
                    MultiButtonMsgDlg.BUTTON_OK) == DialogResult.Cancel)
            {
                return;
            }
            else
            {
                FileEx.SafeDelete(AssayLibraryFileName);
                FileEx.SafeDelete(Path.ChangeExtension(AssayLibraryFileName, BiblioSpecLiteSpec.EXT_REDUNDANT));
            }
            ImportMassList(inputs, description, true);
        }

        private void importMassListMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_importMassListMenuItem_Click_Import_Transition_List_title,
                InitialDirectory = Settings.Default.ActiveDirectory,    // TODO: Better value?
                CheckPathExists = true,
                SupportMultiDottedExtensions = true,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importMassListMenuItem_Click_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV)),
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    ImportMassList(dlg.FileName);
                }
            }
        }

        public void ImportMassList(string fileName)
        {
            try
            {
                ImportMassList(new MassListInputs(fileName), Resources.SkylineWindow_importMassListMenuItem_Click_Import_transition_list, false);
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, fileName, x.Message), x);
            }
        }

        /// <summary>
        /// Process and then add the mass list to the document
        /// </summary>
        /// <param name="inputs">Inputs to import</param>
        /// <param name="description">Description of action</param>
        /// <param name="assayLibrary">True if input is an assay library</param>
        /// <param name="inputType">"None" means "don't know if it's peptides or small molecules, go figure it out".</param>
        /// <param name="forceDlg">True if we want to display a column select form, even if we think we know all the columns we need</param>
        public void ImportMassList(MassListInputs inputs, string description, bool assayLibrary, 
            SrmDocument.DOCUMENT_TYPE inputType = SrmDocument.DOCUMENT_TYPE.none, bool forceDlg = false)
        {
            SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;
            IdentityPath insertPath = nodePaste != null ? nodePaste.Path : null;
            IdentityPath selectPath = null;
            bool isSmallMoleculeList = true;
            bool useColSelectDlg = true;
            bool hasHeaders = true;
            bool isAssociateProteins = false;
            List<MeasuredRetentionTime> irtPeptides = new List<MeasuredRetentionTime>();
            List<SpectrumMzInfo> librarySpectra = new List<SpectrumMzInfo>();
            List<TransitionImportErrorInfo> errorList = new List<TransitionImportErrorInfo>();
            List<PeptideGroupDocNode> peptideGroups = new List<PeptideGroupDocNode>();
            List<string> columnPositions = null;
            var docCurrent = DocumentUI;
            SrmDocument docNew = null;
            MassListImporter importer = null;
            var analyzingMessage = string.Format(Resources.SkylineWindow_ImportMassList_Analyzing_input__0_, inputs.InputFilename ?? string.Empty);
            using (var longWaitDlg0 = new LongWaitDlg(this)
            {
                Text = analyzingMessage,
            })
            {
                var current = docCurrent;
                var status = longWaitDlg0.PerformWork(this, 1000, longWaitBroker =>
                {
                    // PreImport of mass list
                    importer = current.PreImportMassList(inputs, longWaitBroker, true, SrmDocument.DOCUMENT_TYPE.none, true, ModeUI);                  
                });
                if (importer == null || status.IsCanceled)
                {
                    return;
                }
            }
            hasHeaders = importer.RowReader.Indices.Headers != null;
            if (importer.InputType == SrmDocument.DOCUMENT_TYPE.small_molecules 
                && !forceDlg) // We can skip this check if we will use the dialog regardless
            {
                List<TransitionImportErrorInfo> testErrorList = new List<TransitionImportErrorInfo>();
                var input = new MassListInputs(inputs.Lines.Take(100).ToArray());
                // Try importing that list to check for errors
                docCurrent.ImportMassList(input, importer, null,
                    insertPath, out selectPath, out irtPeptides,
                    out librarySpectra, out testErrorList, out peptideGroups, null, SrmDocument.DOCUMENT_TYPE.none, hasHeaders);
                if (!testErrorList.Any())
                {
                    useColSelectDlg = false; // We should be able to import without consulting the user for column identities
                }
            }

            useColSelectDlg |= forceDlg;
            string gridValues = null;
            if (useColSelectDlg)
            {
                // Allow the user to assign column types
                using (var columnDlg = new ImportTransitionListColumnSelectDlg(importer, docCurrent, inputs, insertPath, assayLibrary))
                {
                    if (columnDlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    var insParams = columnDlg.InsertionParams;
                    docNew = insParams.Document;
                    selectPath = insParams.SelectPath;
                    irtPeptides = insParams.IrtPeptides;
                    librarySpectra = insParams.LibrarySpectra;
                    peptideGroups = insParams.PeptideGroups;
                    columnPositions = insParams.ColumnHeaderList;
                    isSmallMoleculeList = insParams.IsSmallMoleculeList;
                    isAssociateProteins = columnDlg.checkBoxAssociateProteins.Checked;

                    // Grab the final grid contents (may have been altered by Associate Proteins, or user additions/deletions
                    var sb = new StringBuilder();
                    if (columnDlg.Importer.RowReader.Indices.Headers != null &&
                        columnDlg.Importer.RowReader.Indices.Headers.Any())
                    {
                        // Show the headers as the user sees them
                        sb.AppendLine(string.Join(columnDlg.Importer.RowReader.Separator.ToString(), columnDlg.Importer.RowReader.Indices.Headers));
                    }
                    foreach (var line in columnDlg.Importer.RowReader.Lines.Where(l => !string.IsNullOrEmpty(l)))
                    {
                        // Show the input lines as the user sees them
                        sb.AppendLine(line);
                    }
                    gridValues = sb.ToString();
                }
            }

            if (isSmallMoleculeList && useColSelectDlg || importer.InputType == SrmDocument.DOCUMENT_TYPE.small_molecules && !useColSelectDlg)
            {
                // We should have all the column header info we need, proceed with the import
                docCurrent = docCurrent.ImportMassList(inputs, importer, null,
                    insertPath, out selectPath, out irtPeptides, out librarySpectra, out errorList,
                    out peptideGroups, columnPositions, SrmDocument.DOCUMENT_TYPE.none, hasHeaders);
            }
            if (importer.InputType == SrmDocument.DOCUMENT_TYPE.small_molecules)
            {
                if (errorList.Any())
                {
                    // Currently small molecules show just one error with no ability to continue.
                    using (var errorDlg = new ImportTransitionListErrorDlg(errorList, true, false))
                    {
                        errorDlg.ShowDialog(this);
                        return;
                    }
                }
            }

            if (assayLibrary)
            {
                var missingMessage = new List<string>();
                if (!irtPeptides.Any())
                    missingMessage.Add(TextUtil.LineSeparate(Resources.SkylineWindow_ImportMassList_The_file_does_not_contain_iRTs__Valid_column_names_for_iRTs_are_,
                                                             TextUtil.LineSeparate(ColumnIndices.IrtColumnNames)));
                if (!librarySpectra.Any())
                    missingMessage.Add(TextUtil.LineSeparate(Resources.SkylineWindow_ImportMassList_The_file_does_not_contain_intensities__Valid_column_names_for_intensities_are_,
                                                             TextUtil.LineSeparate(ColumnIndices.LibraryColumnNames)));
                if (missingMessage.Any())
                {
                    MessageDlg.Show(this, TextUtil.LineSeparate(missingMessage));
                    return;
                }
            }

            bool isDocumentSame = ReferenceEquals(docNew, docCurrent);
            // If nothing was imported (e.g. operation was canceled or zero error-free transitions) and also no errors, just return
            if (isDocumentSame)
                return;

            // Formerly this is where we would show any errors and give the user the option to proceed with just the non-error transitions.
            // Now we do that during the import window's close event. This affords the user the additional option of going back and fixing
            // any issues like bad column selection rather than having to go through the whole process again.

            RetentionTimeRegression retentionTimeRegressionStore;
            MassListInputs irtInputs;
            if (!ImportMassListIrts(ref docNew, irtPeptides, peptideGroups, librarySpectra, assayLibrary, out irtInputs, out retentionTimeRegressionStore))
                return;

            BiblioSpecLiteSpec docLibrarySpec = null;
            BiblioSpecLiteLibrary docLibrary = null;
            var indexOldLibrary = -1;

            var entryCreators = new AuditLogEntryCreatorList();

            var importIntensities = true;
            if (librarySpectra.Any())
            {
                if (!assayLibrary)
                {
                    var addLibraryMessage = Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_;
                    var addLibraryResult = MultiButtonMsgDlg.Show(this, addLibraryMessage,
                        Resources.SkylineWindow_ImportMassList__Create___, Resources.SkylineWindow_ImportMassList__Skip, true);
                    if (addLibraryResult == DialogResult.Cancel)
                        return;
                    importIntensities = addLibraryResult == DialogResult.Yes;

                    if(importIntensities)
                        entryCreators.Add(new MessageInfo(MessageType.imported_spectral_library_intensities, docNew.DocumentType));

                }
                if (importIntensities && !ImportMassListIntensities(ref docNew, librarySpectra, assayLibrary, out docLibrarySpec, out docLibrary, out indexOldLibrary))
                    return;
            }

            ModifyDocument(description, doc =>
            {
                if (ReferenceEquals(doc, docCurrent))
                    return docNew;
                try
                {
                    // If the document was changed during the operation, try all the changes again
                    // using the information given by the user.
                    docCurrent = DocumentUI;
                    doc = doc.ImportMassList(inputs, importer, insertPath, out selectPath, columnPositions, hasHeaders);
                    if (irtInputs != null)
                    {
                        var iRTimporter = doc.PreImportMassList(irtInputs, null, false);
                        doc = doc.ImportMassList(irtInputs, iRTimporter, null, out selectPath, columnPositions, hasHeaders);
                    }
                    var newSettings = doc.Settings;
                    if (retentionTimeRegressionStore != null)
                    {
                        newSettings = newSettings.ChangePeptidePrediction(prediction => 
                            prediction.ChangeRetentionTime(retentionTimeRegressionStore));
                    }
                    if (docLibrarySpec != null)
                    {
                        newSettings = newSettings.ChangePeptideLibraries(libs => 
                            libs.ChangeLibrary(docLibrary, docLibrarySpec, indexOldLibrary));
                    }
                    if (!ReferenceEquals(doc.Settings, newSettings))
                        doc = doc.ChangeSettings(newSettings);
                }
                catch (Exception x)
                {
                    throw new InvalidDataException(string.Format(Resources.SkylineWindow_ImportMassList_Unexpected_document_change_during_operation___0_, x.Message, x));
                }
                return doc;
            }, docPair =>
            {
                MessageType msgType;
                object[] args;

                // Log the column assignments
                var columnsUsed = (columnPositions == null || columnPositions.Count == 0)
                    ? null
                    : string.Format(Resources.SkylineWindow_ImportMassList_Columns_identified_as__0_, TextUtil.ToCsvLine(columnPositions.Select(s => $@"'{s}'")));

                var extraInfo = new List<string>
                {
                    string.Format(Resources.SkylineWindow_ImportMassList__0__transitions_added,
                        docPair.NewDoc.MoleculeTransitionCount - docPair.OldDoc.MoleculeTransitionCount)
                };
                if (isAssociateProteins)
                {
                    extraInfo.Add(Resources.SkylineWindow_ImportMassList_Associate_Proteins_enabled);
                }
                extraInfo.Add(columnsUsed);

                // Imported from file
                if (inputs.InputFilename != null)
                {
                    msgType = assayLibrary ? MessageType.imported_assay_library_from_file : MessageType.imported_transition_list_from_file;
                    args = new object[] { AuditLogPath.Create(inputs.InputFilename) };
                    extraInfo.Add(gridValues);
                }
                else
                {
                    msgType = assayLibrary ? MessageType.imported_assay_library : MessageType.imported_transition_list;
                    args = new object[0];
                    extraInfo.Add(gridValues ?? inputs.InputText);
                }

                return AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(msgType, docPair.NewDocumentType, args), 
                    TextUtil.LineSeparate(extraInfo.Where(s => !string.IsNullOrEmpty(s)))).Merge(docPair, entryCreators);
            });

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
            if (retentionTimeRegressionStore != null)
            {
                Settings.Default.RetentionTimeList.Add(retentionTimeRegressionStore);
                Settings.Default.RTScoreCalculatorList.Add(retentionTimeRegressionStore.Calculator);
            }
            if (docLibrarySpec != null)
            {
                Settings.Default.SpectralLibraryList.Insert(0, docLibrarySpec);
            }
        }

        public string AssayLibraryFileName
        {
            get
            {
                var docLib = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath);
                // ReSharper disable once AssignNullToNotNullAttribute
                return Path.Combine(Path.GetDirectoryName(docLib), Path.GetFileNameWithoutExtension(docLib) + BiblioSpecLiteSpec.ASSAY_NAME + BiblioSpecLiteSpec.EXT);
            }
        }

        public string AssayLibraryName
        {
            get { return Path.GetFileNameWithoutExtension(DocumentFilePath) + BiblioSpecLiteSpec.ASSAY_NAME; }
        }

        private bool ImportMassListIrts(ref SrmDocument doc, IEnumerable<MeasuredRetentionTime> irtPeptides,
            IEnumerable<PeptideGroupDocNode> peptideGroups, List<SpectrumMzInfo> librarySpectra, bool assayLibrary,
            out MassListInputs irtInputs, out RetentionTimeRegression retentionTimeRegressionStore)
        {
            irtInputs = null;
            retentionTimeRegressionStore = null;

            var retentionTimeRegression = DocumentUI.Settings.PeptideSettings.Prediction.RetentionTime;
            var calcIrt = retentionTimeRegression != null ? retentionTimeRegression.Calculator as RCalcIrt : null;
            var dbIrtPeptides = irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, false, TimeSource.scan)).ToList();
            if (!assayLibrary)
            {
                dbIrtPeptides = ImportAssayLibraryHelper.GetUnscoredIrtPeptides(dbIrtPeptides, calcIrt);
                // If there are no iRT peptides or none with different values than the database, don't import any iRT's
                if (!dbIrtPeptides.Any())
                    return true;
            }

            IrtDb db;
            if (!assayLibrary)
            {
                // Ask whether or not to include iRT peptides in the paste
                var useIrtMessage = calcIrt == null
                    ? Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_
                    : Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_;
                var yesButton = calcIrt == null
                    ? Resources.SkylineWindow_ImportMassList__Create___
                    : Resources.SkylineWindow_ImportMassList_Add;
                switch (MultiButtonMsgDlg.Show(this, useIrtMessage, yesButton, Resources.SkylineWindow_ImportMassList__Skip, true))
                {
                    case DialogResult.No:
                        return true;
                    case DialogResult.Cancel:
                        return false;
                }
                if (calcIrt == null)
                {
                    // If there is no iRT calculator, ask the user to create one
                    using (var dlg = new CreateIrtCalculatorDlg(doc, DocumentFilePath, Settings.Default.RTScoreCalculatorList, peptideGroups))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return false;

                        doc = dlg.Document;
                        calcIrt = (RCalcIrt) doc.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
                        dlg.UpdateLists(librarySpectra, dbIrtPeptides);
                        if (!string.IsNullOrEmpty(dlg.IrtFile))
                            irtInputs = new MassListInputs(dlg.IrtFile);
                    }
                }
                var dbPath = calcIrt.DatabasePath;
                db = File.Exists(dbPath) ? IrtDb.GetIrtDb(dbPath, null) : IrtDb.CreateIrtDb(dbPath);
            }
            else
            {
                db = IrtDb.CreateIrtDb(AssayLibraryFileName);

                var matchingStandards = IrtStandard.BestMatch(librarySpectra);

                if (matchingStandards.Count == 1)
                {
                    IrtPeptidePicker.SetStandards(dbIrtPeptides, matchingStandards[0]);
                }
                else
                {
                    // Ask for standards
                    using (var dlg = new ChooseIrtStandardPeptidesDlg(doc, DocumentFilePath, dbIrtPeptides, peptideGroups))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return false;

                        const double slopeTolerance = 0.05;
                        var rescale = false;
                        if (dlg.Regression != null && !(1 - slopeTolerance <= dlg.Regression.Slope && dlg.Regression.Slope <= 1 + slopeTolerance))
                        {
                            using (var scaleDlg = new MultiButtonMsgDlg(
                                Resources.SkylineWindow_ImportMassListIrts_The_standard_peptides_do_not_appear_to_be_on_the_iRT_C18_scale__Would_you_like_to_recalibrate_them_to_this_scale_,
                                MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                            {
                                if (scaleDlg.ShowDialog(this) == DialogResult.Yes)
                                    rescale = true;
                            }
                        }

                        doc = dlg.Document;
                        dlg.UpdateLists(librarySpectra, dbIrtPeptides, rescale);
                        if (!string.IsNullOrEmpty(dlg.IrtFile))
                            irtInputs = new MassListInputs(dlg.IrtFile);
                    }
                }

                var calculator = new RCalcIrt(AssayLibraryName, AssayLibraryFileName);
                // CONSIDER: Probably can't use just a static default like 10 below
                retentionTimeRegression = new RetentionTimeRegression(calculator.Name, calculator, null, null, RetentionTimeRegression.DEFAULT_WINDOW, new List<MeasuredRetentionTime>());
                doc = doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(prediction => prediction.ChangeRetentionTime(retentionTimeRegression)));
            }
            
            var oldPeptides = db.GetPeptides().ToList();
            IList<DbIrtPeptide.Conflict> conflicts;
            dbIrtPeptides = DbIrtPeptide.MakeUnique(dbIrtPeptides);
            DbIrtPeptide.FindNonConflicts(oldPeptides, dbIrtPeptides, null, out conflicts);
            // Ask whether to keep or overwrite peptides that are present in the import and already in the database
            var overwriteExisting = false;
            if (conflicts.Any())
            {
                var messageOverwrite = string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_, conflicts.Count);
                var overwriteResult = MultiButtonMsgDlg.Show(this,
                    TextUtil.LineSeparate(messageOverwrite, conflicts.Count == 1
                        ? Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_
                        : Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_values_or_overwrite_with_imported_values_),
                    Resources.SkylineWindow_ImportMassList__Keep, Resources.SkylineWindow_ImportMassList__Overwrite,
                    true);
                if (overwriteResult == DialogResult.Cancel)
                    return false;
                overwriteExisting = overwriteResult == DialogResult.No;
            }
            using (var longWaitDlg = new LongWaitDlg(this) { Text = Resources.SkylineWindow_ImportMassList_Adding_iRT_values_ })
            {
                var newDoc = doc;
                longWaitDlg.PerformWork(this, 100, progressMonitor => newDoc = newDoc.AddIrtPeptides(dbIrtPeptides, overwriteExisting, progressMonitor));
                doc = newDoc;
            }
            if (doc == null)
                return false;
            retentionTimeRegressionStore = doc.Settings.PeptideSettings.Prediction.RetentionTime;
            return true;
        }

        private bool ImportMassListIntensities(ref SrmDocument doc, List<SpectrumMzInfo> librarySpectra, bool assayLibrary,
            out BiblioSpecLiteSpec docLibrarySpec, out BiblioSpecLiteLibrary docLibrary, out int indexOldLibrary)
        {
            docLibrarySpec = null;
            docLibrary = null;
            indexOldLibrary = -1;

            // Can't name a library after the document if the document is unsaved
            // In this case, prompt to save
            if (DocumentFilePath == null &&
                (MultiButtonMsgDlg.Show(this,
                     Resources.SkylineWindow_ImportMassList_You_must_save_the_Skyline_document_in_order_to_create_a_spectral_library_from_a_transition_list_,
                     MultiButtonMsgDlg.BUTTON_OK) == DialogResult.Cancel ||
                 !SaveDocumentAs()))
            {
                return false;
            }

            librarySpectra = SpectrumMzInfo.RemoveDuplicateSpectra(librarySpectra);

            indexOldLibrary = doc.Settings.PeptideSettings.Libraries.LibrarySpecs.IndexOf(spec => spec != null && spec.FilePath == AssayLibraryFileName);
            var libraryLinkedToDoc = indexOldLibrary != -1;
            if (libraryLinkedToDoc)
            {
                var oldName = doc.Settings.PeptideSettings.Libraries.LibrarySpecs[indexOldLibrary].Name;
                var libraryOld = doc.Settings.PeptideSettings.Libraries.GetLibrary(oldName);
                var additionalSpectra = SpectrumMzInfo.GetInfoFromLibrary(libraryOld);
                additionalSpectra = SpectrumMzInfo.RemoveDuplicateSpectra(additionalSpectra);
                librarySpectra = SpectrumMzInfo.MergeWithOverwrite(librarySpectra, additionalSpectra);
                foreach (var stream in libraryOld.ReadStreams)
                    stream.CloseStream();
            }

            var libraryExists = File.Exists(AssayLibraryFileName);
            if (!assayLibrary && libraryExists && !libraryLinkedToDoc)
            {
                var replaceLibraryMessage = string.Format(Resources.SkylineWindow_ImportMassList_There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created___Overwrite_this_library_or_skip_import_of_library_intensities_, AssayLibraryName);
                // If the document does not have an assay library linked to it, then ask if user wants to delete the one that we have found
                var replaceLibraryResult = MultiButtonMsgDlg.Show(this, replaceLibraryMessage,
                    Resources.SkylineWindow_ImportMassList__Overwrite, Resources.SkylineWindow_ImportMassList__Skip, true);
                if (replaceLibraryResult == DialogResult.Cancel)
                    return false;
                if (replaceLibraryResult == DialogResult.No)
                    librarySpectra.Clear();
            }
            if (!librarySpectra.Any())
                return true;

            // Delete the existing library; either it's not tied to the document or we've already extracted the spectra
            if (!assayLibrary && libraryExists)
            {
                FileEx.SafeDelete(AssayLibraryFileName);
                FileEx.SafeDelete(Path.ChangeExtension(AssayLibraryFileName, BiblioSpecLiteSpec.EXT_REDUNDANT));
            }
            using (var blibDb = BlibDb.CreateBlibDb(AssayLibraryFileName))
            {
                docLibrarySpec = new BiblioSpecLiteSpec(AssayLibraryName ?? Path.GetFileNameWithoutExtension(AssayLibraryFileName), AssayLibraryFileName);
                using (var longWaitDlg = new LongWaitDlg(this) { Text = Resources.SkylineWindow_ImportMassListIntensities_Creating_Spectral_Library })
                {
                    var docNew = doc;
                    BiblioSpecLiteLibrary docLibraryNew = null;
                    var docLibrarySpec2 = docLibrarySpec;
                    var indexOldLibrary2 = indexOldLibrary;
                    longWaitDlg.PerformWork(this, 1000, progressMonitor =>
                    {
                        IProgressStatus status = new ProgressStatus(Resources .BlibDb_CreateLibraryFromSpectra_Creating_spectral_library_for_imported_transition_list);
                        docLibraryNew = blibDb.CreateLibraryFromSpectra(docLibrarySpec2, librarySpectra, AssayLibraryName ?? Path.GetFileNameWithoutExtension(AssayLibraryFileName), progressMonitor, ref status);
                        if (docLibraryNew == null)
                            return;
                        var newSettings = docNew.Settings.ChangePeptideLibraries(libs => libs.ChangeLibrary(docLibraryNew, docLibrarySpec2, indexOldLibrary2));
                        progressMonitor.UpdateProgress(status = status.ChangeMessage(Resources.SkylineWindow_ImportMassList_Finishing_up_import).ChangePercentComplete(0));
                        docNew = docNew.ChangeSettings(newSettings, new SrmSettingsChangeMonitor(progressMonitor, Resources.LibraryManager_LoadBackground_Updating_library_settings_for__0_, status));
                    });
                    doc = docNew;
                    docLibrary = docLibraryNew;
                    if (docLibrary == null)
                        return false;
                }
            }
            return true;
        }

        private void importDocumentMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.SkylineWindow_importDocumentMenuItem_Click_Import_Skyline_Document,

                InitialDirectory = Settings.Default.ActiveDirectory,
                CheckPathExists = true,
                Multiselect = true,
                SupportMultiDottedExtensions = true,
                DefaultExt = SrmDocument.EXT,
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC),
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ImportFiles(dlg.FileNames);
                    }
                    catch (Exception x)
                    {
                        var failedImportingFiles = TextUtil.LineSeparate(Resources.SkylineWindow_importDocumentMenuItem_Click_Failed_importing_files, string.Empty,
                                                                           TextUtil.LineSeparate(dlg.FileNames), string.Empty, x.Message);
                        string message = dlg.FileNames.Length == 1
                            ? string.Format(Resources.SkylineWindow_importDocumentMenuItem_Click_Failed_importing_file__0__1__, dlg.FileNames[0], x.Message)
                            : failedImportingFiles;
                        MessageDlg.ShowWithException(this, message, x);
                    }
                }
            }
        }

        public void ImportFiles(params string[] filePaths)
        {
            var resultsAction = MeasuredResults.MergeAction.remove;
            var mergePeptides = false;
          
            var entryCreatorList = new AuditLogEntryCreatorList();
            if (MeasuredResults.HasResults(filePaths))
            {
                using (var dlgResults = new ImportDocResultsDlg(!string.IsNullOrEmpty(DocumentFilePath)))
                {
                    if (dlgResults.ShowDialog(this) != DialogResult.OK)
                        return;
                    resultsAction = dlgResults.Action;
                    mergePeptides = dlgResults.IsMergePeptides;

                    entryCreatorList.Add(dlgResults.FormSettings.EntryCreator);
                }
            }
            SrmTreeNode nodeSel = SequenceTree.SelectedNode as SrmTreeNode;
            IdentityPath selectPath = null;

            var docCurrent = DocumentUI;
            SrmDocument docNew = null;
            using (var longWaitDlg = new LongWaitDlg(this)
                {
                    Text = Resources.SkylineWindow_ImportFiles_Import_Skyline_document_data,
                })
            {
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                    docNew = ImportFiles(docCurrent,
                                         longWaitBroker,
                                         filePaths,
                                         resultsAction,
                                         mergePeptides,
                                         nodeSel != null ? nodeSel.Path : null,
                                         out selectPath));

                if (docNew == null || ReferenceEquals(docNew, docCurrent))
                    return;

                if (longWaitDlg.IsDocumentChanged(docCurrent))
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                    return;
                }
            }

            ModifyDocument(Resources.SkylineWindow_ImportFiles_Import_Skyline_document_data, doc =>
            {
                docNew.ValidateResults();
                if (!ReferenceEquals(doc, docCurrent))
                    throw new InvalidDataException(Resources
                        .SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                return docNew;
            }, docPair =>
            {
                var entry = AuditLogEntry.CreateCountChangeEntry(MessageType.imported_doc,
                    MessageType.imported_docs, docPair.NewDocumentType, filePaths.Select(AuditLogPath.Create), filePaths.Length,
                    MessageArgs.DefaultSingular, null);

                if (filePaths.Length > 1)
                    entry.AppendAllInfo(filePaths.Select(file =>
                        new MessageInfo(MessageType.imported_doc, docPair.NewDocumentType, AuditLogPath.Create(file))));

                return entry.Merge(docPair, entryCreatorList, false);
            });

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
        }

        private SrmDocument ImportFiles(SrmDocument docOrig,
                                               ILongWaitBroker longWaitBroker,
                                               IList<string> filePaths,
                                               MeasuredResults.MergeAction resultsAction,
                                               bool mergePeptides,
                                               IdentityPath to,
                                               out IdentityPath firstAdded)
        {
            firstAdded = null;

            var docResult = docOrig;
            int filesRead = 0;

            // Add files in reverse order, so their nodes will end up in the right order.
            IdentityPath first = null;
            foreach (var filePath in filePaths)
            {
                if (longWaitBroker != null)
                {
                    if (longWaitBroker.IsCanceled || longWaitBroker.IsDocumentChanged(docOrig))
                        return docOrig;
                    longWaitBroker.ProgressValue = filesRead*100/filePaths.Count;
                    longWaitBroker.Message = string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(filePath));
                }

                using (var reader = new StreamReader(PathEx.SafePath(filePath)))
                {
                    IdentityPath firstAddedForFile, nextAdd;
                    docResult = docResult.ImportDocumentXml(reader,
                                                filePath,
                                                resultsAction,
                                                mergePeptides,
                                                FindSpectralLibrary,
                                                Settings.Default.StaticModList,
                                                Settings.Default.HeavyModList,
                                                to,
                                                out firstAddedForFile,
                                                out nextAdd,
                                                false);
                    // Add the next document at the specified location
                    to = nextAdd;
                    // Store the first added node only for the first document
                    if (first == null)
                        first = firstAddedForFile;
                }

                filesRead++;
            }
            firstAdded = first;
            return docResult;
        }

        public string FindSpectralLibrary(string libraryName, string fileName)
        {
            string result = null;
            RunUIAction(() =>
                            {
                                using (var dlg = new MissingFileDlg
                                {
                                    ItemName = libraryName,
                                    FileHint = fileName,
                                    ItemType = Resources.SkylineWindow_ConnectLibrarySpecs_Spectral_Library,
                                    Title = Resources.SkylineWindow_ConnectLibrarySpecs_Find_Spectral_Library
                                })
                                {
                                    if (dlg.ShowDialog(this) == DialogResult.OK)
                                        result = dlg.FilePath;
                                }
                            });
            return result;
        }

        private void importResultsMenuItem_Click(object sender, EventArgs e)
        {
            if (ImportingResultsWindow != null)
            {
                ShowAllChromatogramsGraph();
                return;
            }
            ImportResults();
        }

        public void ImportResults()
        {
            if (DocumentUI.MoleculeTransitionCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ImportResults_You_must_add_at_least_one_target_transition_before_importing_results_);
                return;
            }
            if (!CheckDocumentExists(Resources.SkylineWindow_ImportResults_You_must_save_this_document_before_importing_results))
            {
                return;
            }

            var entryCreatorList = new AuditLogEntryCreatorList();
            if (!CheckRetentionTimeFilter(DocumentUI, entryCreatorList))
            {
                return;
            }

            var missingIrtPeptides = CheckMissingIrtPeptides(DocumentUI).ToArray();
            if (missingIrtPeptides.Any())
            {
                var numStandards = RCalcIrt.IrtPeptides(DocumentUI).Count();
                var numDocument = numStandards - missingIrtPeptides.Length;
                var numRequired = RCalcIrt.MinStandardCount(numStandards);
                var message = TextUtil.LineSeparate(
                    Resources.SkylineWindow_ImportResults_The_following_iRT_standard_peptides_are_missing_from_the_document_,
                    string.Empty,
                    TextUtil.LineSeparate(missingIrtPeptides.Select(t=>t.ToString())),
                    string.Empty,
                    string.Format(Resources.SkylineWindow_ImportResults_With__0__standard_peptides___1__are_required_with_a_correlation_of__2__,
                                  numStandards, numRequired, RCalcIrt.MIN_IRT_TO_TIME_CORRELATION));
                if (numDocument < numRequired)
                {
                    message = TextUtil.LineSeparate(
                        message,
                        numDocument > 0
                            ? string.Format(Resources.SkylineWindow_ImportResults_The_document_only_contains__0__of_these_iRT_standard_peptides_, numDocument)
                            : Resources.SkylineWindow_ImportResults_The_document_does_not_contain_any_of_these_iRT_standard_peptides_,
                        string.Empty,
                        Resources.SkylineWindow_ImportResults_Add_missing_iRT_standard_peptides_to_your_document_or_change_the_retention_time_predictor_);
                    MessageDlg.Show(this, message);
                    return;
                }
                else
                {
                    var numExceptions = numDocument - numRequired;
                    message = TextUtil.LineSeparate(
                        message,
                        string.Format(Resources.SkylineWindow_ImportResults_The_document_contains__0__of_these_iRT_standard_peptides_, numDocument),
                        numExceptions > 0
                            ? string.Format(Resources.SkylineWindow_ImportResults_A_maximum_of__0__may_be_missing_and_or_outliers_for_a_successful_import_, numExceptions)
                            : Resources.SkylineWindow_ImportResults_None_may_be_missing_or_outliers_for_a_successful_import_,
                        string.Empty,
                        Resources.SkylineWindow_ImportResults_Do_you_want_to_continue_);
                    using (var dlg = new MultiButtonMsgDlg(message, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.No)
                            return;
                    }
                }
            }

            var decoyGroup = DocumentUI.PeptideGroups.FirstOrDefault(group => group.IsDecoy);
            var isDia = Equals(DocumentUI.Settings.TransitionSettings.FullScan.AcquisitionMethod, FullScanAcquisitionMethod.DIA);
            if (decoyGroup != null)
            {
                decoyGroup.CheckDecoys(DocumentUI, out var numNoSource, out var numWrongTransitionCount, out var proportionDecoysMatch);
                if ((!decoyGroup.ProportionDecoysMatch.HasValue && proportionDecoysMatch <= 0.99) || // over 99% of decoys must match targets if proportion is not set
                    (decoyGroup.ProportionDecoysMatch.HasValue && proportionDecoysMatch < decoyGroup.ProportionDecoysMatch)) // proportion of decoys matching targets has decreased since generation
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(decoyGroup.PeptideCount == 1
                        ? Resources.SkylineWindow_ImportResults_The_document_contains_a_decoy_that_does_not_match_the_targets_
                        : string.Format(Resources.SkylineWindow_ImportResults_The_document_contains_decoys_that_do_not_match_the_targets__Out_of__0__decoys_, decoyGroup.PeptideCount));

                    sb.AppendLine(string.Empty);
                    if (numNoSource == 1)
                        sb.AppendLine(Resources.SkylineWindow_ImportResults_1_decoy_does_not_have_a_matching_target);
                    else if (numNoSource > 1)
                        sb.AppendLine(string.Format(Resources.SkylineWindow_ImportResults__0__decoys_do_not_have_a_matching_target, numNoSource));
                    if (numWrongTransitionCount == 1)
                        sb.AppendLine(Resources.SkylineWindow_ImportResults_1_decoy_does_not_have_the_same_number_of_transitions_as_its_matching_target);
                    else if (numWrongTransitionCount > 0)
                        sb.AppendLine(string.Format(Resources.SkylineWindow_ImportResults__0__decoys_do_not_have_the_same_number_of_transitions_as_their_matching_targets, numWrongTransitionCount));
                    sb.AppendLine(string.Empty);
                    sb.AppendLine(Resources.SkylineWindow_ImportResults_Do_you_want_to_generate_new_decoys_or_continue_with_the_current_decoys_);
                    using (var dlg = new MultiButtonMsgDlg(sb.ToString(),
                        Resources.SkylineWindow_ImportResults_Generate, Resources.SkylineWindow_ImportResults_Continue, true))
                    {
                        switch (dlg.ShowDialog(this))
                        {
                            case DialogResult.Yes:
                                if (!ShowGenerateDecoysDlg(dlg))
                                    return;
                                break;
                            case DialogResult.No:
                                using (var dlg2 = new MultiButtonMsgDlg(
                                    Resources.SkylineWindow_ImportResults_Are_you_sure__Peak_scoring_models_trained_with_non_matching_targets_and_decoys_may_produce_incorrect_results_,
                                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false))
                                {
                                    if (dlg2.ShowDialog(dlg) == DialogResult.No)
                                        return;
                                }
                                break;
                            case DialogResult.Cancel:
                                return;
                        }
                    }
                }
            }
            else if (ShouldPromptForDecoys(DocumentUI))
            {
                using (var dlg = new MultiButtonMsgDlg(
                    Resources.SkylineWindow_ImportResults_This_document_does_not_contain_decoy_peptides__Would_you_like_to_add_decoy_peptides_before_extracting_chromatograms__After_chromatogram_extraction_is_finished__Skyline_will_use_the_decoy_and_target_chromatograms_to_train_a_peak_scoring_model_in_order_to_choose_better_peaks_,
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true))
                {
                    switch (dlg.ShowDialog(this))
                    {
                        case DialogResult.Yes:
                            if (!ShowGenerateDecoysDlg(dlg))
                                return;
                            break;
                        case DialogResult.No:
                            break;
                        case DialogResult.Cancel:
                            return;
                    }
                }
            }

            using (ImportResultsDlg dlg = new ImportResultsDlg(DocumentUI, DocumentFilePath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // No idea how this could happen, but it has caused unexpected errors
                    // so just return and do nothing if it does.
                    if (dlg.NamedPathSets == null)
                    {
//                        throw new NullReferenceException("Unexpected null path sets in ImportResults.");
                        return;
                    }
                    var namedResults = dlg.NamedPathSets.ToList();
                    string description = Resources.SkylineWindow_ImportResults_Import_results;
                    if (namedResults.Count == 1)
                        description = string.Format(Resources.SkylineWindow_ImportResults_Import__0__, namedResults[0].Key); 

                    // Check with user for Waters lockmass settings if any, results written to Settings.Default
                    // If lockmass correction is desired, MsDataFileUri values in namedResults are modified by this call.
                    if (!ImportResultsLockMassDlg.UpdateNamedResultsParameters(this, DocumentUI, ref namedResults))
                        return; // User cancelled, no change

                    ModifyDocument(description, doc =>
                        {
                            if (isDia && doc.Molecules.Any(m => m.IsDecoy))
                            {
                                doc = doc.ChangeSettings(doc.Settings.ChangePeptideIntegration(i =>
                                    i.ChangeAutoTrain(PeptideIntegration.AutoTrainType.default_model)));
                            }
                            return ImportResults(doc, namedResults, dlg.OptimizationName);
                        },
                        docPair => dlg.FormSettings.EntryCreator.Create(docPair).Merge(docPair, entryCreatorList));

                    // Select the first replicate to which results were added.
                    if (ComboResults.Visible)
                        ComboResults.SelectedItem = namedResults[0].Key;
                }
            }
        }

        public static bool ShouldPromptForDecoys(SrmDocument doc)
        {
            return Equals(doc.Settings.TransitionSettings.FullScan.AcquisitionMethod, FullScanAcquisitionMethod.DIA) &&
                   !doc.PeptideGroups.Any(nodePepGroup => nodePepGroup.IsDecoy) &&
                   !doc.Settings.HasResults;
        }

        /// <summary>
        /// If the Transition Full Scan settings are such that the time window for extracting
        /// chromatograms depends on a set of replicates, then this function shows the
        /// ChooseSchedulingReplicatesDlg.
        /// Returns false if the user cancels the dialog, or cannot import chromatograms.
        /// </summary>
        public bool CheckRetentionTimeFilter(SrmDocument document, AuditLogEntryCreatorList creatorList)
        {
            var settings = document.Settings;
            var fullScan = settings.TransitionSettings.FullScan;
            if (!fullScan.IsEnabled)
            {
                return true;
            }
            if (fullScan.RetentionTimeFilterType != RetentionTimeFilterType.scheduling_windows)
            {
                return true;
            }
            if (!fullScan.IsEnabledMsMs && !document.MoleculeTransitions.Any(transition => transition.IsMs1))
            {
                return true;
            }
            var prediction = settings.PeptideSettings.Prediction;
            if (prediction.RetentionTime != null && prediction.RetentionTime.IsAutoCalculated)
            {
                return true;
            }
            bool anyImportedResults = settings.HasResults && settings.MeasuredResults.Chromatograms.Any();
            bool canChooseReplicatesForCalibration = anyImportedResults &&
                                                        (prediction.UseMeasuredRTs ||
                                                         prediction.RetentionTime != null &&
                                                         prediction.RetentionTime.IsAutoCalculated);
            if (null == prediction.RetentionTime)
            {
                if (!prediction.UseMeasuredRTs || !anyImportedResults)
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_CheckRetentionTimeFilter_NoPredictionAlgorithm);
                    return false;
                }
            }
            else if (!prediction.RetentionTime.IsUsable)
            {
                if (!canChooseReplicatesForCalibration)
                {
                    if (MultiButtonMsgDlg.Show(this, Resources.SkylineWindow_CheckRetentionTimeFilter_NoReplicatesAvailableForPrediction,
                        MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        return false;
                    }
                }
            }
            if (!canChooseReplicatesForCalibration)
            {
                return true;
            }
            using (var dlg = new ChooseSchedulingReplicatesDlg(this))
            {
                var ok = dlg.ShowDialog(this) == DialogResult.OK;
                if(ok)
                    creatorList.Add(dlg.FormSettings.EntryCreator);
                return ok;
            }
        }

        private static IEnumerable<Target> CheckMissingIrtPeptides(SrmDocument document)
        {
            var existingPeptides = new LibKeyIndex(document.Molecules.Select(pep=>new LibKey(pep.ModifiedTarget, Adduct.EMPTY).LibraryKey));
            return RCalcIrt.IrtPeptides(document)
                .Where(target => !existingPeptides.ItemsMatching(new LibKey(target, Adduct.EMPTY).LibraryKey, false).Any());
        }

        public SrmDocument ImportResults(SrmDocument doc, List<KeyValuePair<string, MsDataFileUri[]>> namedResults, string optimize)
        {

            OptimizableRegression optimizationFunction = doc.Settings.TransitionSettings.Prediction.GetOptimizeFunction(optimize);

            if (namedResults.Count == 1)
                return ImportResults(doc, namedResults[0].Key, namedResults[0].Value, optimizationFunction);

            // Add all chosen files as separate result sets.
            var results = doc.Settings.MeasuredResults;
            var listChrom = new List<ChromatogramSet>();
            if (results != null)
                listChrom.AddRange(results.Chromatograms);

            foreach (var namedResult in namedResults)
            {
                string nameResult = namedResult.Key;

                // Skip results that have already been loaded.
                if (GetChromatogramByName(nameResult, results) != null)
                    continue;

                // Delete caches that will be overwritten
                FileEx.SafeDelete(ChromatogramCache.FinalPathForName(DocumentFilePath, nameResult), true);

                listChrom.Add(new ChromatogramSet(nameResult, namedResult.Value, Annotations.EMPTY, optimizationFunction));
            }

            var arrayChrom = listChrom.ToArray();
            if (arrayChrom.Length == 0)
            {
                results = null;
            }
            else
            {
                if (results == null)
                {
                    results = new MeasuredResults(arrayChrom);
                }
                else
                {
                    results = results.ChangeChromatograms(arrayChrom);
                }
            }

            if (results != null && Program.DisableJoining)
                results = results.ChangeIsJoiningDisabled(true);

            return doc.ChangeMeasuredResults(results);
        }

        private SrmDocument ImportResults(SrmDocument doc, string nameResult, IEnumerable<MsDataFileUri> dataSources,
            OptimizableRegression optimizationFunction)
        {
            var results = doc.Settings.MeasuredResults;
            var chrom = GetChromatogramByName(nameResult, results);
            if (chrom == null)
            {
                // If the chromatogram, is not in the current set, then delete the cache
                // file to make sure it is not on disk before starting.
                FileEx.SafeDelete(ChromatogramCache.FinalPathForName(DocumentFilePath, nameResult), true);

                chrom = new ChromatogramSet(nameResult, dataSources, Annotations.EMPTY, optimizationFunction);

                if (results == null)
                    results = new MeasuredResults(new[] {chrom});
                else
                {
                    // Add the new result to the end.
                    var listChrom = new List<ChromatogramSet>(results.Chromatograms) {chrom};
                    results = results.ChangeChromatograms(listChrom.ToArray());
                }
            }
            else
            {
                // Append to an existing chromatogram set
                var dataFilePaths = new List<MsDataFileUri>(chrom.MSDataFilePaths);
                foreach (var sourcePath in dataSources)
                {
                    if (!dataFilePaths.Contains(sourcePath))
                        dataFilePaths.Add(sourcePath);
                }
                // If no new paths added, just return without changing.
                if (dataFilePaths.Count == chrom.FileCount)
                    return doc;

                int replaceIndex = results.Chromatograms.IndexOf(chrom);
                var arrayChrom = results.Chromatograms.ToArray();
                arrayChrom[replaceIndex] = chrom.ChangeMSDataFilePaths(dataFilePaths);

                results = results.ChangeChromatograms(arrayChrom);
            }

            if (results != null && Program.DisableJoining)
                results = results.ChangeIsJoiningDisabled(true);

            return doc.ChangeMeasuredResults(results);
        }

        private static ChromatogramSet GetChromatogramByName(string name, MeasuredResults results)
        {
            return (results == null ? null :
                results.Chromatograms.FirstOrDefault(set => Equals(name, set.Name)));
        }

        public void ManageResults()
        {
            var documentUI = DocumentUI;
            if (!documentUI.Settings.HasResults && !documentUI.Settings.HasDocumentLibrary)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ManageResults_The_document_must_contain_mass_spec_data_to_manage_results_);
                return;                
            }

            using (ManageResultsDlg dlg = new ManageResultsDlg(this, _libraryManager))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Remove from the cache chromatogram data to be reimported.  This done before changing
                    // anything else to avoid making other changes to the results cause cache changes before
                    // the document is saved.
                    try
                    {
                        ReimportChromatograms(documentUI, dlg.ReimportChromatograms);
                    }
                    catch (Exception exception)
                    {
                        MessageDlg.ShowWithException(this, Resources.SkylineWindow_ManageResults_A_failure_occurred_attempting_to_reimport_results, exception);
                    }

                    // And update the document to reflect real changes to the results structure
                    ModifyDocument(Resources.SkylineWindow_ManageResults_Manage_results, doc =>
                    {
                        if (dlg.IsRemoveAllLibraryRuns)
                        {
                            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib =>
                            {
                                var libSpecs = new List<LibrarySpec>(lib.LibrarySpecs);
                                var libs = new List<Library>(lib.Libraries);
                                for (int i = 0; i < libSpecs.Count; i++)
                                {
                                    if (libSpecs[i].IsDocumentLibrary || libSpecs[i] is MidasLibSpec)
                                    {
                                        libSpecs.RemoveAt(i);
                                        libs.RemoveAt(i);
                                    }
                                }
                                return lib.ChangeDocumentLibrary(false)
                                          .ChangeLibraries(libSpecs.ToArray(), libs.ToArray());
                            }));
                        }
                        else if (dlg.LibraryRunsRemovedList.Count > 0)
                        {
                            var releaseLibraries = false;
                            BiblioSpecLiteLibrary docBlib;
                            if (DocumentUI.Settings.PeptideSettings.Libraries.TryGetDocumentLibrary(out docBlib))
                            {
                                try
                                {
                                    docBlib.DeleteDataFiles(dlg.LibraryRunsRemovedList.ToArray(), this);
                                    releaseLibraries = true;
                                }
                                catch (Exception x)
                                {
                                    throw new IOException(TextUtil.LineSeparate(Resources.SkylineWindow_ManageResults_Failed_to_remove_library_runs_from_the_document_library_, x.Message));
                                }
                            }

                            foreach (var midasLib in DocumentUI.Settings.PeptideSettings.Libraries.MidasLibraries)
                            {
                                try
                                {
                                    midasLib.RemoveResultsFiles(dlg.LibraryRunsRemovedList.ToArray());
                                    releaseLibraries = true;
                                }
                                catch (Exception x)
                                {
                                    throw new IOException(TextUtil.LineSeparate(Resources.SkylineWindow_ManageResults_Failed_to_remove_library_runs_from_the_MIDAS_library_, x.Message));
                                }
                            }

                            if (releaseLibraries)
                            {
                                var libSpecs = dlg.DocumentLibrarySpecs.ToArray();
                                var libSpecNames = libSpecs.Select(libSpec => libSpec.Name);
                                _libraryManager.ReleaseLibraries(libSpecs);
                                var settings = doc.Settings.ChangePeptideLibraries(lib =>
                                {
                                    var listLib = new List<Library>(lib.Libraries);
                                    var i = lib.LibrarySpecs.IndexOf(spec => libSpecNames.Contains(spec.Name));
                                    if (i != -1)
                                        listLib[i] = null;
                                    return lib.ChangeLibraries(listLib);
                                });
                                doc = doc.ChangeSettings(settings);
                            }
                        }

                        var results = doc.Settings.MeasuredResults;
                        var listChrom = dlg.Chromatograms.ToArray();
                        if (results == null && listChrom.Length == 0)
                            return doc;

                        // Set HasMidasSpectra = false for file infos
                        listChrom = MidasLibrary.UnflagFiles(listChrom, dlg.LibraryRunsRemovedList.Select(Path.GetFileName)).ToArray();

                        if (ArrayUtil.ReferencesEqual(results?.Chromatograms, listChrom))
                            return doc;

                        MeasuredResults resultsNew = null;
                        if (listChrom.Length > 0)
                        {
                            if (results == null)
                                resultsNew = new MeasuredResults(listChrom);
                            else
                                resultsNew = results.ChangeChromatograms(listChrom.ToArray());
                        }
                        doc = doc.ChangeMeasuredResults(resultsNew);
                        doc.ValidateResults();

                        return doc;
                    }, dlg.FormSettings.EntryCreator.Create);

                    // Modify document will have closed the streams by now.  So, it is safe to delete the files.
                    if (dlg.IsRemoveAllLibraryRuns)
                    {
                        try
                        {
                            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath);
                            FileEx.SafeDelete(docLibPath);

                            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
                            FileEx.SafeDelete(redundantDocLibPath);

                            string docLibCachePath = BiblioSpecLiteLibrary.GetLibraryCachePath(docLibPath);
                            FileEx.SafeDelete(docLibCachePath);

                            string midasLibPath = MidasLibSpec.GetLibraryFileName(DocumentFilePath);
                            FileEx.SafeDelete(midasLibPath);
                        }
                        catch (FileEx.DeleteException deleteException)
                        {
                            MessageDlg.ShowException(this, deleteException);
                        }
                    }
                }
            }
        }

        public void ReimportChromatograms(SrmDocument document, IEnumerable<ChromatogramSet> chromatogramSets)
        {
            var setReimport = new HashSet<ChromatogramSet>(chromatogramSets);
            if (setReimport.Count == 0)
                return;

            new LongOperationRunner
                {
                    ParentControl = this,
                    JobTitle = Resources.SkylineWindow_ReimportChromatograms_Reimporting_chromatograms
                }
                .Run(longWaitBroker =>
                {
                    // Remove all replicates to be re-imported
                    var results = document.Settings.MeasuredResults;
                    var chromRemaining = results.Chromatograms.Where(chrom => !setReimport.Contains(chrom)).ToArray();
                    MeasuredResults resultsNew = results.ChangeChromatograms(chromRemaining);
                    if (chromRemaining.Length > 0)
                    {
                        // Optimize the cache using this reduced set to remove their data from the cache
                        resultsNew = resultsNew.OptimizeCache(DocumentFilePath, _chromatogramManager.StreamManager, longWaitBroker);
                    }
                    else
                    {
                        // Or remove the cache entirely, if everything is being reimported
                        foreach (var readStream in results.ReadStreams)
                            readStream.CloseStream();

                        string cachePath = ChromatogramCache.FinalPathForName(DocumentFilePath, null);
                        FileEx.SafeDelete(cachePath, true);
                    }
                    // Restore the original set unchanged
                    resultsNew = resultsNew.ChangeChromatograms(results.Chromatograms);

                    // Update the document without adding an undo record, because the only information
                    // to change should be cache related.
                    SrmDocument docNew, docCurrent;
                    do
                    {
                        docCurrent = Document;
                        docNew = docCurrent.ChangeMeasuredResults(resultsNew);
                    } while (!SetDocument(docNew, docCurrent));
                });
        }
    
        private void importPeptideSearchMenuItem_Click(object sender, EventArgs e)
        {
            ShowImportPeptideSearchDlg();
        }

        public void ShowImportPeptideSearchDlg(ImportPeptideSearchDlg.Workflow? workflowType)
        {
            if (!CheckDocumentExists(Resources.SkylineWindow_ShowImportPeptideSearchDlg_You_must_save_this_document_before_importing_a_peptide_search_))
            {
                return;
            }
            else if (!Document.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowImportPeptideSearchDlg_The_document_must_be_fully_loaded_before_importing_a_peptide_search_);
                return;
            }

            using (var dlg = !workflowType.HasValue
                   ? new ImportPeptideSearchDlg(this, _libraryManager)
                   : new ImportPeptideSearchDlg(this, _libraryManager, workflowType.Value))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Nothing to do; the dialog does all the work.
                }
            }
        }

        public void ShowImportPeptideSearchDlg()
        {
            ShowImportPeptideSearchDlg(null);
        }

        private bool CheckDocumentExists(String errorMsg)
        {
            if (string.IsNullOrEmpty(DocumentFilePath))
            {
                if (MultiButtonMsgDlg.Show(this,errorMsg,Resources.OK) == DialogResult.Cancel)
                    return false;
                if (!SaveDocument())
                    return false;
            }

            return true;
        }

        private void publishMenuItem_Click(object sender, EventArgs e)
        {
            ShowPublishDlg(null);
        }

        public void ShowPublishDlg(IPanoramaPublishClient publishClient)
        {
            if (publishClient == null)
                publishClient = new WebPanoramaPublishClient();

            var document = DocumentUI;
            if (!document.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowPublishDlg_The_document_must_be_fully_loaded_before_it_can_be_uploaded_);
                return;
            }

            string fileName = DocumentFilePath;
            if (string.IsNullOrEmpty(fileName))
            {
                if (MultiButtonMsgDlg.Show(this, Resources.SkylineWindow_ShowPublishDlg_The_document_must_be_saved_before_it_can_be_uploaded_,
                    MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    return;

                if (!SaveDocumentAs())
                    return;

                fileName = DocumentFilePath;
            }

            // Issue 866: Provide option in Skyline to upload a minimized library to Panorama
            // Save the document but only if it is "dirty". If the user is uploading an older document with a newer
            // version of Skyline they may want to preserve the version in the .sky file. The version is preserved only with a 
            // "complete" share, however. 
            if (Dirty)
            {
                SaveDocument();
            }

            var servers = Settings.Default.ServerList;
            if (servers.Count == 0)
            {
                DialogResult buttonPress = MultiButtonMsgDlg.Show(
                    this,
                    TextUtil.LineSeparate(
                        Resources.SkylineWindow_ShowPublishDlg_There_are_no_Panorama_servers_to_upload_to,
                        Resources.SkylineWindow_ShowPublishDlg_Press_Register_to_register_for_a_project_on_PanoramaWeb_,
                        Resources.SkylineWindow_ShowPublishDlg_Press_Continue_to_use_the_server_of_your_choice_),
                    Resources.SkylineWindow_ShowPublishDlg_Register, Resources.SkylineWindow_ShowPublishDlg_Continue,
                    true);
                if (buttonPress == DialogResult.Cancel)
                    return;

                object tag = null;
                if (buttonPress == DialogResult.Yes)
                {
                    // person intends to register                   
                    WebHelpers.OpenLink(this, @"https://panoramaweb.org/signup.url"); 
                    tag = true;
                }

                var serverPanoramaWeb = new Server(PanoramaUtil.PANORAMA_WEB, string.Empty, string.Empty);
                var newServer = servers.EditItem(this, serverPanoramaWeb, null, tag);
                if (newServer == null)
                    return;

                servers.Add(newServer);
            }
            var panoramaSavedUri = document.Settings.DataSettings.PanoramaPublishUri;
            var showPublishDocDlg = true;

            // if the document has a saved uri prompt user for acton, check servers, and permissions, then publish
            // if something fails in the attempt to publish to the saved uri will bring up the usual PublishDocumentDlg
            if (panoramaSavedUri != null && !string.IsNullOrEmpty(panoramaSavedUri.ToString()))
            {
                showPublishDocDlg = !PublishToSavedUri(publishClient, panoramaSavedUri, fileName, servers);
            }

            // if no uri was saved to publish to or user chose to view the dialog show the dialog
            if (showPublishDocDlg)
            {
                using (var publishDocumentDlg = new PublishDocumentDlg(this, servers, fileName, GetFileFormatOnDisk()))
                {
                    publishDocumentDlg.PanoramaPublishClient = publishClient;
                    if (publishDocumentDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (ShareDocument(publishDocumentDlg.FileName, publishDocumentDlg.ShareType))
                            publishDocumentDlg.Upload(this);
                    }
                }
            }
        }

        private bool PublishToSavedUri(IPanoramaPublishClient publishClient, Uri panoramaSavedUri, string fileName,
            ServerList servers)
        {
            var message = TextUtil.LineSeparate(Resources.SkylineWindow_PublishToSavedUri_This_file_was_last_uploaded_to___0_,
                Resources.SkylineWindow_PublishToSavedUri_Upload_to_the_same_location_);
            var result = MultiButtonMsgDlg.Show(this, string.Format(message, panoramaSavedUri),
                MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, true);
            switch (result)
            {
                case DialogResult.No:
                    return false;
                case DialogResult.Cancel:
                    return true;
            }

            var server = servers.FirstOrDefault(s => s.URI.Host.Equals(panoramaSavedUri.Host));
            if (server == null)
                return false;

            JToken folders;
            var folderPath = panoramaSavedUri.AbsolutePath;
            var folderPathNoCtx = PanoramaServer.getFolderPath(server, panoramaSavedUri); // get folder path without the context path
            try
            {
                folders = publishClient.GetInfoForFolders(server, folderPathNoCtx.TrimEnd('/').TrimStart('/'));
            }
            catch (WebException ex)
            {
                // Handle this only for PanoramaWeb.  For the specific case where Skyline was upgraded
                // to a version that does not assume the '/labkey' context path, BEFORE PanoramaWeb was
                // re-configured to run as the ROOT webapp. In this case the panoramaSavedUri will contain '/labkey'
                // but the server is no longer deployed at that context path.
                if (!server.URI.Host.Contains(@"panoramaweb") || !folderPath.StartsWith(@"/labkey"))
                {
                    return false;
                }

                var response = ex.Response as HttpWebResponse;

                if (response == null || response.StatusCode != HttpStatusCode.NotFound) // 404
                {
                    return false;
                }

                folderPathNoCtx = folderPath.Remove(0, @"/labkey".Length); 
                try
                {
                    folders =
                        publishClient.GetInfoForFolders(server, folderPathNoCtx.TrimEnd('/').TrimStart('/')); 
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (PanoramaServerException)
            {
                return false;
            }

            // must escape uri string as panorama api does not and strings are escaped in schema
            if (folders == null || !folderPath.Contains(Uri.EscapeUriString(folders[@"path"].ToString()))) 
                return false;

            if (!PanoramaUtil.CheckFolderPermissions(folders) || !PanoramaUtil.CheckFolderType(folders))
                return false;

            var fileInfo = new FolderInformation(server, true);
            ShareType shareType;
            try
            {
                var cancelled = false;
                shareType = publishClient.GetShareType(fileInfo, DocumentUI, GetFileFormatOnDisk(), this, ref cancelled);
                if (cancelled)
                {
                    return true;
                }
            }
            catch (PanoramaServerException pse)
            {
                MessageDlg.ShowWithException(this, pse.Message, pse);
                return false;
            }

            var zipFilePath = FileEx.GetTimeStampedFileName(fileName);
            if (!ShareDocument(zipFilePath, shareType))
                return false;

            var serverRelativePath = folders[@"path"].ToString() + '/'; 
            serverRelativePath = serverRelativePath.TrimStart('/'); 
            publishClient.UploadSharedZipFile(this, server, zipFilePath, serverRelativePath);
            return true; // success!
        }


        private void exportAnnotationsMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportAnnotationsDlg();
        }

        public void ShowExportAnnotationsDlg()
        {
            using (var exportAnnotationsDlg =
                new ExportAnnotationsDlg(new SkylineDataSchema(this, DataSchemaLocalizer.INVARIANT)))
            {
                exportAnnotationsDlg.ShowDialog(this);
            }
        }

        public void ImportAnnotations(string filename)
        {
            try
            {
                lock (GetDocumentChangeLock())
                {
                    var originalDocument = Document;
                    SrmDocument newDocument = null;
                    using (var longWaitDlg = new LongWaitDlg(this))
                    {
                        longWaitDlg.PerformWork(this, 1000, broker =>
                        {
                            var documentAnnotations = new DocumentAnnotations(originalDocument);
                            newDocument = documentAnnotations.ReadAnnotationsFromFile(broker.CancellationToken, filename);
                        });
                    }
                    if (newDocument != null)
                    {
                        ModifyDocument(Resources.SkylineWindow_ImportAnnotations_Import_Annotations, doc =>
                        {
                            if (!ReferenceEquals(doc, originalDocument))
                            {
                                throw new ApplicationException(Resources
                                    .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                            }
                            return newDocument;
                        }, docPair => AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(MessageType.imported_annotations, docPair.NewDocumentType, filename)));
                    }
                }
            }
            catch (Exception exception)
            {
                MessageDlg.ShowException(this, exception);
            }
        }


        private void importAnnotationsMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FILTER_CSV),
                InitialDirectory = Settings.Default.ExportDirectory,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                ImportAnnotations(dlg.FileName);
            }
        }


        #region Functional Test Support

        public void ShowExportTransitionListDlg()
        {
            ShowExportMethodDialog(ExportFileType.List);
        }

        #endregion
    }
}
