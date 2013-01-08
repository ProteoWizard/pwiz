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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Esp;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public partial class SkylineWindow
    {
        public static string GetViewFile(string fileName)
        {
            return fileName + ".view"; // Not L10N
        }

        private void fileMenu_DropDownOpening(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = fileToolStripMenuItem;
            List<string> mruList = Settings.Default.MruList;
            string curDir = Settings.Default.ActiveDirectory;

            int start = menu.DropDownItems.IndexOf(mruBeforeToolStripSeparator) + 1;
            while (!ReferenceEquals(menu.DropDownItems[start], mruAfterToolStripSeparator))
                menu.DropDownItems.RemoveAt(start);
            for (int i = 0; i < mruList.Count; i++)
            {
                MruChosenHandler handler = new MruChosenHandler(this, mruList[i]);
                ToolStripMenuItem item = new ToolStripMenuItem(GetMruName(i, mruList[i], curDir), null,
                    handler.ToolStripMenuItemClick);
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
                name = string.Format("&{0} {1}", index, name); // Not L10N
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

        private void newMenuItem_Click(object sender, EventArgs e) { NewDocument(); }
        public void NewDocument()
        {
            if (!CheckSaveDocument())
                return;

            // Create a new document with the default settings.
            SrmDocument document = ConnectDocument(new SrmDocument(Settings.Default.SrmSettingsList[0]), null) ??
                                   new SrmDocument(SrmSettingsList.GetDefault());

            // Make sure settings lists contain correct values for
            // this document.
            document.Settings.UpdateLists(null);

            // Switch over to the new document
            SwitchDocument(document, null);
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
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC, SrmDocumentSharing.FILTER_SHARING)
            })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    if (dlg.FileName.EndsWith(SrmDocumentSharing.EXT))
                    {
                        OpenSharedFile(dlg.FileName);
                    }
                    else
                    {
                        OpenFile(dlg.FileName); // Sets ActiveDirectory
                    }
                }
            }
        }

        private void OpenSharedFile(string zipPath)
        {
            try
            {
                var sharing = new SrmDocumentSharing(zipPath);

                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.SkylineWindow_OpenSharedFile_Extracting_Files,
                })
                {
                    longWaitDlg.PerformWork(this, 1000, sharing.Extract);
                    if (longWaitDlg.IsCanceled)
                        return;
                }

                // Remember the directory containing the newly extracted file
                // as the active directory for the next open command.
                Settings.Default.ActiveDirectory = Path.GetDirectoryName(sharing.DocumentPath);

                OpenFile(sharing.DocumentPath);
            }
            catch (ZipException)
            {
                MessageDlg.Show(this,
                                string.Format(Resources.SkylineWindow_OpenSharedFile_The_zip_file__0__cannot_be_read,
                                              zipPath));
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(string.Format(
                        Resources.SkylineWindow_OpenSharedFile_Failure_extracting_Skyline_document_from_zip_file__0__,
                        zipPath), e.Message);
                MessageDlg.Show(this, message);
            }
        }

        public bool OpenFile(string path)
        {
            try
            {
                using (TextReader reader = new StreamReader(path))
                using (new LongOp(this))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                    SrmDocument document = ConnectDocument((SrmDocument)ser.Deserialize(reader), path);
                    if (document == null)
                        return false;   // User cancelled

                    if (!CheckResults(document, path))
                        return false;

                    // Make sure settings lists contain correct values for
                    // this document.
                    document.Settings.UpdateLists(path);

                    using (new SequenceTreeForm.LockDoc(_sequenceTreeForm))
                    {
                        // Switch over to the opened document
                        SwitchDocument(document, path);
                    }
                    // Locking the sequenceTree can throw off the node count status
                    UpdateNodeCountStatus();
                }
            }
            catch (Exception x)
            {
                new MessageBoxHelper(this).ShowXmlParsingError(
                    string.Format(Resources.SkylineWindow_OpenFile_Failure_opening__0__, path), path, x);
                return false;
            }

            if (SequenceTree != null && SequenceTree.Nodes.Count > 0)
                SequenceTree.SelectedNode = SequenceTree.Nodes[0];

            return true;
        }

        private SrmDocument ConnectDocument(SrmDocument document, string path)
        {
            document = ConnectLibrarySpecs(document, path);
            if (document != null)
                document = ConnectBackgroundProteome(document, path);
            if (document != null)
                document = ConnectIrtDatabase(document, path);
            return document;
        }

        private SrmDocument ConnectLibrarySpecs(SrmDocument document, string documentPath)
        {
            if (!string.IsNullOrEmpty(documentPath) && document.Settings.PeptideSettings.Libraries.DocumentLibrary)
            {
                string docLibFile = BiblioSpecLiteSpec.GetLibraryFileName(documentPath);
                if (!File.Exists(docLibFile))
                {
                    MessageDlg.Show(this, string.Format(Resources.SkylineWindow_ConnectLibrarySpecs_Could_not_find_the_spectral_library__0__for_this_document__Without_the_library__no_spectrum_ID_information_will_be_available_, docLibFile));
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs(library =>
                {
                    LibrarySpec spec;
                    if (Settings.Default.SpectralLibraryList.TryGetValue(library.Name, out spec))
                        return spec;
                    if (documentPath == null)
                        return null;

                    string fileName = library.FileNameHint;
                    if (fileName != null)
                    {
                        // First look for the file name in the document directory
                        string pathLibrary = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName);
                        if (File.Exists(pathLibrary))
                            return library.CreateSpec(pathLibrary).ChangeDocumentLocal(true);
                        // In the user's default library directory
                        pathLibrary = Path.Combine(Settings.Default.LibraryDirectory, fileName);
                        if (File.Exists(pathLibrary))
                            return library.CreateSpec(pathLibrary);
                    }

                    using (var dlg = new MissingFileDlg
                                  {
                                      ItemName = library.Name,
                                      ItemType = Resources.SkylineWindow_ConnectLibrarySpecs_Spectral_Library,
                                      Filter = library.SpecFilter,
                                      FileHint = fileName,
                                      FileDlgInitialPath = Path.GetDirectoryName(documentPath),
                                      Title = Resources.SkylineWindow_ConnectLibrarySpecs_Find_Spectral_Library
                                  })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            return library.CreateSpec(dlg.FilePath);
                        }
                    }

                    return null;
                });
            
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

        private SrmDocument ConnectIrtDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIrtDatabase(calc => FindIrtDatabase(documentPath, calc));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }


        private RCalcIrt FindIrtDatabase(string documentPath, RCalcIrt irtCalc)
        {

            RetentionScoreCalculatorSpec result;
            if (Settings.Default.RTScoreCalculatorList.TryGetValue(irtCalc.Name, out result))
                return result as RCalcIrt;
            if (documentPath == null)
                return null;

            // First look for the file name in the document directory
            string fileName = Path.GetFileName(irtCalc.DatabasePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
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
                    if (dlg.ShowDialog(this) == DialogResult.OK)
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
                                e.Message); // Not L10N
                            MessageBox.Show(message);
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

        private SrmDocument ConnectBackgroundProteome(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectBackgroundProteome(backgroundProteomeSpec =>
                FindBackgroundProteome(documentPath, backgroundProteomeSpec));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private BackgroundProteomeSpec FindBackgroundProteome(string documentPath, BackgroundProteomeSpec backgroundProteomeSpec)
        {
            var result = Settings.Default.BackgroundProteomeList.GetBackgroundProteomeSpec(backgroundProteomeSpec.Name);
            if (result != null)
                return result;
            if (documentPath == null)
                return null;

            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            // First look for the file name in the document directory
            string pathBackgroundProteome = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
            pathBackgroundProteome = Path.Combine(Settings.Default.ProteomeDbDirectory, fileName ?? string.Empty);
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
                if (dlg.ShowDialog(this) == DialogResult.OK)
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

        private bool CheckResults(SrmDocument document, string path)
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
                var foundFiles = new List<string>();
                foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (string pathFileSample in chromSet.MSDataFilePaths)
                    {
                        string pathFile = SampleHelp.GetPathFilePart(pathFileSample);
                        if (missingFiles.Contains(pathFile))
                            continue;
                        if (File.Exists(pathFile) ||
                            File.Exists(Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileName(pathFile) ?? "")))
                        {
                            foundFiles.Add(pathFile);
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
                string message = TextUtil.LineSeparate((foundFiles.Count > 0
                                     ? string.Format(
                                         Resources.
                                             SkylineWindow_CheckResults_The_data_file__0__is_missing_and_some_of_the_original_instrument_output_could_not_be_found,
                                         ChromatogramCache.FinalPathForName(path, null))
                                     : string.Format(
                                         Resources.
                                             SkylineWindow_CheckResults_The_data_file__0__is_missing_and_the_original_instrument_output_could_not_be_found,
                                         ChromatogramCache.FinalPathForName(path, null))),
                                       Resources.SkylineWindow_CheckResults_Click_OK_to_open_the_document_anyway);

                if (MessageBox.Show(this, message, Program.Name,
                        MessageBoxButtons.OKCancel) == DialogResult.Cancel)
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
                using (var dlg = new MultiButtonMsgDlg(Resources.SkylineWindow_CheckSaveDocument_Do_you_want_to_save_changes,
                    Resources.SkylineWindow_CheckSaveDocument_Yes, Resources.SkylineWindow_CheckSaveDocument_No, true))
                {
                    switch (dlg.ShowDialog(this))
                    {
                        case DialogResult.Yes:
                            return SaveDocument();
                        case DialogResult.Cancel:
                            return false;
                    }
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
            if (!DocumentUI.Settings.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_SaveDocumentAs_The_document_must_be_fully_loaded_before_it_can_be_saved_to_a_new_name);
                return false;
            }

            using (SaveFileDialog dlg = new SaveFileDialog
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

        public bool SaveDocument(string fileName)
        {
            return SaveDocument(fileName, true);
        }

        public bool SaveDocument(String fileName, bool includingCacheFile)
        {
            SrmDocument document = DocumentUI;
            try
            {
                using (var saver = new FileSaver(fileName))
                {
                    if (!saver.CanSave(true))
                        return false;
                    using (var writer = new XmlTextWriter(saver.SafeName, Encoding.UTF8) { Formatting = Formatting.Indented })
                    using (new LongOp(this))
                    {

                        XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                        ser.Serialize(writer, document);

                        writer.Flush();
                        writer.Close();

                        // If the user has chosen "Save As", and the document has a
                        // document specific spectral library, copy this library to 
                        // the new name.
                        if (!Equals(DocumentFilePath, fileName))
                        {
                            if (!SaveDocumentLibraryAs(fileName))
                                return false;
                        }

                        saver.Commit();

                        DocumentFilePath = fileName;
                        _savedVersion = document.RevisionIndex;
                        SetActiveFile(fileName);


                        // Make sure settings lists contain correct values for this document.
                        document.Settings.UpdateLists(DocumentFilePath);
                    }
                }
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_SaveDocument_Failed_writing_to__0__, fileName), x.Message);
                MessageBox.Show(message);
                return false;
            }

            try
            {
                // CONSIDER: Is this really optional?
                if (includingCacheFile)
                {
                    OptimizeCache(fileName);
                }
                SaveLayout(fileName);
            }
            catch (UnauthorizedAccessException)
            {
                // Fail silently
            }
            catch (IOException)
            {
                // Fail silently
            }            

            return true;
        }

        private void OptimizeCache(string fileName)
        {
            // Optimize the results cache to get rid of any unnecessary
            // chromatogram data.
            var settings = DocumentUI.Settings;
            if (settings.HasResults)
            {
                var results = settings.MeasuredResults;
                if (results.IsLoaded)
                {
                    var resultsNew = results.OptimizeCache(fileName, _chromatogramManager.StreamManager);
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

        private bool SaveDocumentLibraryAs(string newDocFilePath)
        {
            string oldDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath);
            string oldRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(oldDocLibFile);
            // If the document has a document-specific library, and the files for it
            // exist on disk
            var document = DocumentUI;
            if (document.Settings.PeptideSettings.Libraries.DocumentLibrary
                && File.Exists(oldDocLibFile) && File.Exists(oldRedundantDocLibFile))
            {
                string newDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(newDocFilePath);
                string newRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(newDocFilePath);
                using (var saverLib = new FileSaver(newDocLibFile))
                using (var saverRedundant = new FileSaver(newRedundantDocLibFile))
                {
                    if (!CopyFile(oldDocLibFile, saverLib))
                        return false;

                    if (!CopyFile(oldRedundantDocLibFile, saverRedundant))
                        return false;
                    saverLib.Commit();
                    saverRedundant.Commit();
                }
            }

            return true;
        }

        private bool CopyFile(string source, FileSaver destSaver)
        {
            // Copy the specified file to the new name using a FileSaver
            if (!destSaver.CanSave(true))
                return false;

            File.Copy(source, destSaver.SafeName, true);
            return true;
        }

        private void SaveLayout(string fileName)
        {
            string fileNameView = GetViewFile(fileName);
            if (!HasPersistableLayout())
                FileEx.SafeDelete(fileNameView);    // caller will handle exception
            else
            {
                using (var saverUser = new FileSaver(GetViewFile(fileName)))
                {
                    if (saverUser.CanSave(false))
                    {
                        dockPanel.SaveAsXml(saverUser.SafeName);
                        saverUser.Commit();
                    }
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
                    int len = Settings.Default.MruLength;
                    if (mruList.Count > len)
                        mruList.RemoveRange(len, mruList.Count - len);
                }
            }

            UpdateTitle();
        }

        private void shareDocumentMenuItem_Click(object sender, EventArgs e)
        {
            var document = DocumentUI;
            if (!document.Settings.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_shareDocumentMenuItem_Click_The_document_must_be_fully_loaded_before_it_can_be_shared);
                return;
            }

            bool saved = false;
            string fileName = DocumentFilePath;
            if (string.IsNullOrEmpty(fileName))
            {
                if (MessageBox.Show(this, Resources.SkylineWindow_shareDocumentMenuItem_Click_The_document_must_be_saved_before_it_can_be_shared, 
                    Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    return;

                if (!SaveDocumentAs())
                    return;

                saved = true;
                fileName = DocumentFilePath;
            }

            bool completeSharing = true;
            if (document.Settings.HasLibraries ||
                document.Settings.HasBackgroundProteome ||
                document.Settings.HasRTCalcPersisted)
            {
                using (var dlgType = new ShareTypeDlg(document))
                {
                    if (dlgType.ShowDialog(this) == DialogResult.Cancel)
                        return;
                    completeSharing = dlgType.IsCompleteSharing;
                }
            }

            using (var dlg = new SaveFileDialog
            {
                Title = Resources.SkylineWindow_shareDocumentMenuItem_Click_Share_Document,
                InitialDirectory = Path.GetDirectoryName(fileName),
                FileName = Path.GetFileNameWithoutExtension(fileName) + SrmDocumentSharing.EXT,
                OverwritePrompt = true,
                DefaultExt = SrmDocumentSharing.EXT,
                Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_shareDocumentMenuItem_Click_Skyline_Shared_Documents, SrmDocumentSharing.EXT),
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                // Make sure the document is completely saved before sharing
                if (!saved && !SaveDocument())
                    return;

                ShareDocument(dlg.FileName, completeSharing);
            }
        }

        public bool ShareDocument(string fileDest, bool completeSharing)
        {
            try
            {
                bool success = false;
                Helpers.TryTwice(() =>
                {
                    using (var longWaitDlg = new LongWaitDlg { Text = Resources.SkylineWindow_ShareDocument_Compressing_Files, })
                    {
                        var sharing = new SrmDocumentSharing(DocumentUI, DocumentFilePath, fileDest,  completeSharing);
                        longWaitDlg.PerformWork(this, 1000, sharing.Share);
                        success = !longWaitDlg.IsCanceled;
                    }
                });
                return success;
            }
            catch (Exception x)
            {
                var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ShareDocument_Failed_attempting_to_create_sharing_file__0__, fileDest),
                                                    x.Message); 
                MessageDlg.Show(this, message);
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
            if (Document.PeptideCount == 0 && (isolationScheme == null || isolationScheme.FromResults))
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

        private void exportReportMenuItem_Click(object sender, EventArgs e)
        {
            ShowExportReportDialog();
        }

        public void ShowExportReportDialog()
        {
            using (ExportReportDlg dlg = new ExportReportDlg(this))
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
            if (DocumentUI.PeptideCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowExportEspFeaturesDialog_The_document_must_contain_peptides_for_which_to_export_features);
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
                    dlg.FileName = Path.GetFileNameWithoutExtension(DocumentFilePath) + "." + EspFeatureCalc.EXT; // Not L10N
                }
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                try
                {
                    EspFeatureCalc.WriteFeatures(dlg.FileName,
                        DocumentUI.Peptides.Select(nodePep => nodePep.Peptide.Sequence), CultureInfo.CurrentCulture);
                }
                catch (IOException x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.SkylineWindow_ShowExportEspFeaturesDialog_Failed_attempting_to_save_ESP_features_to__0__, dlg.FileName),
                                    x.Message);
                    MessageDlg.Show(this, message);
                }
            }
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
                    ImportFasta(readerFasta, lineCount, false, Resources.SkylineWindow_ImportFastaFile_Import_FASTA);
                }
            }
            catch (Exception x)
            {
                MessageDlg.Show(this,
                                string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                                              fastaFile, x.Message));
            }
        }

        private void ImportFasta(TextReader reader, long lineCount, bool peptideList, string description)
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
                    sequences.Add(line);
                }
                try
                {
                    matcher.CreateMatches(docCurrent.Settings, sequences, Settings.Default.StaticModList, Settings.Default.HeavyModList);
                    var strNameMatches = matcher.FoundMatches;
                    if (!string.IsNullOrEmpty(strNameMatches))
                    {
                        var message = TextUtil.LineSeparate(Resources.SkylineWindow_ImportFasta_Would_you_like_to_use_the_Unimod_definitions_for_the_following_modifications,
                                                            string.Empty, strNameMatches);
                        using (var dlg = new MultiButtonMsgDlg(
                            string.Format(message), Resources.SkylineWindow_ImportFasta_OK))
                        {
                            if (dlg.ShowDialog() == DialogResult.Cancel)
                                return;
                        }
                    }
                }
                catch(FormatException x)
                {
                    MessageDlg.Show(this, x.Message);
                    return;
                }
                reader = new StringReader(TextUtil.LineSeparate(header, TextUtil.LineSeparate(sequences.ToArray())));
            }

            var longWaitDlg = new LongWaitDlg(this)
            {
                Text = description,
            };
            SrmDocument docNew = null;
            IdentityPath nextAdded;
            longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                       docNew = docCurrent.ImportFasta(reader, longWaitBroker, lineCount, matcher, to, out selectPath, out nextAdded));

            if (docNew == null)
                return;

            if (longWaitDlg.IsDocumentChanged(docCurrent))
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);                
                return;
            }

            // If importing the FASTA produced any childless proteins
            int countEmpty = docNew.PeptideGroups.Count(nodePepGroup => nodePepGroup.Children.Count == 0);
            if (countEmpty > 0)
            {
                int countEmptyCurrent = docCurrent.PeptideGroups.Count(nodePepGroup => nodePepGroup.Children.Count == 0);
                if (countEmpty > countEmptyCurrent)
                {
                    using (var dlg = new EmptyProteinsDlg(countEmpty - countEmptyCurrent))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.Cancel)
                            return;
                        // Remove all empty proteins, if requested by the user.
                        if (!dlg.IsKeepEmptyProteins)
                        {
                            docNew = new RefinementSettings {MinPeptidesPerProtein = 1}.Refine(docNew);
                            // This may result in no change from the original, if all proteins were empty
                            if (Equals(docNew, docCurrent))
                                return;

                            selectPath = null;
                            var enumGroupsCurrent = docCurrent.PeptideGroups.GetEnumerator();
                            foreach (PeptideGroupDocNode nodePepGroup in docNew.PeptideGroups)
                            {
                                if (enumGroupsCurrent.MoveNext() &&
                                    !ReferenceEquals(nodePepGroup, enumGroupsCurrent.Current))
                                {
                                    selectPath = new IdentityPath(nodePepGroup.Id);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            ModifyDocument(description, doc =>
            {
                if (doc != docCurrent)
                    throw new InvalidDataException(Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                if (matcher != null)
                {
                    var pepModsNew = matcher.GetDocModifications(docNew);
                    docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideModifications(mods => pepModsNew));
                    docNew.Settings.UpdateDefaultModifications(false);
                }
                return docNew;
            });

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
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

                    try
                    {
                        using (new LongOp(this))
                        {
                            IFormatProvider provider;
                            char sep;

                            using (var readerLine = new StreamReader(dlg.FileName))
                            {
                                Type[] columnTypes;
                                string line = readerLine.ReadLine();
                                if (!MassListImporter.IsColumnar(line, out provider, out sep, out columnTypes))
                                    throw new IOException(Resources.SkylineWindow_importMassListMenuItem_Click_Data_columns_not_found_in_first_line);
                            }

                            using (var readerList = new StreamReader(dlg.FileName))
                            {
                                ImportMassList(readerList, provider, sep, Resources.SkylineWindow_importMassListMenuItem_Click_Import_transition_list);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        MessageBox.Show(string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, dlg.FileName, x.Message));
                    }
                }
            }
        }

        private void ImportMassList(TextReader reader, IFormatProvider provider, char separator, string description)
        {
            SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;

            IdentityPath selectPath = null;

            // TODO: Support long wait dialog
            ModifyDocument(description, doc => doc.ImportMassList(reader, null, -1, provider, separator,
                nodePaste != null ? nodePaste.Path : null, out selectPath));

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
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
                        MessageBox.Show(message);
                    }
                }
            }
        }

        public void ImportFiles(params string[] filePaths)
        {
            var resultsAction = MeasuredResults.MergeAction.remove;
            var mergePeptides = false;
            if (HasResults(filePaths))
            {
                using (var dlgResults = new ImportDocResultsDlg(!string.IsNullOrEmpty(DocumentFilePath)))
                {
                    if (dlgResults.ShowDialog(this) != DialogResult.OK)
                        return;
                    resultsAction = dlgResults.Action;
                    mergePeptides = dlgResults.IsMergePeptides;
                }
            }
            SrmTreeNode nodeSel = SequenceTree.SelectedNode as SrmTreeNode;
            IdentityPath selectPath = null;

            var docCurrent = DocumentUI;
            var longWaitDlg = new LongWaitDlg(this)
            {
                Text = Resources.SkylineWindow_ImportFiles_Import_Skyline_document_data,
            };
            SrmDocument docNew = null;
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

            ModifyDocument(Resources.SkylineWindow_ImportFiles_Import_Skyline_document_data, doc =>
            {
                docNew.ValidateResults();
                if (doc != docCurrent)
                    throw new InvalidDataException(Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                return docNew;
            });

            if (selectPath != null)
                SequenceTree.SelectedPath = selectPath;
        }

        private static bool HasResults(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            {
                try
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            // If there is a measured results tag before the settings_summary end
                            // tag, then this document contains results.  Otherwise not.
                            if (line.Contains("<measured_results")) // Not L10N
                                return true;
                            if (line.Contains("</settings_summary>")) // Not L10N
                                return false;
                        }
                    }
                }
                catch (IOException)
                {
                }
            }
            return false;
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

                using (var reader = new StreamReader(filePath))
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

        private string FindSpectralLibrary(string libraryName, string fileName)
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
            ImportResults();
        }

        public void ImportResults()
        {
            if (DocumentUI.TransitionCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ImportResults_You_must_add_at_least_one_target_transition_before_importing_results_);
                return;
            }
            if (!CheckDocumentExists(Resources.SkylineWindow_ImportResults_You_must_save_this_document_before_importing_results))
            {
                return;
            }

            using (ImportResultsDlg dlg = new ImportResultsDlg(DocumentUI, DocumentFilePath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var namedResults = dlg.NamedPathSets;
                    // No idea how this could happen, but it has caused unexpected errors
                    // so just return and do nothing if it does.
                    if (namedResults == null)
                    {
//                        throw new NullReferenceException("Unexpected null path sets in ImportResults.");
                        return;
                    }
                    string description = Resources.SkylineWindow_ImportResults_Import_results;
                    if (namedResults.Length == 1)
                        description = string.Format(Resources.SkylineWindow_ImportResults_Import__0__, namedResults[0].Key); // Not L10N

                    ModifyDocument(description,
                                   doc => ImportResults(doc, namedResults, dlg.OptimizationName));

                    // Select the first replicate to which results were added.
                    if (ComboResults.Visible)
                        ComboResults.SelectedItem = namedResults[0].Key;
                }
            }
        }

        public SrmDocument ImportResults(SrmDocument doc, KeyValuePair<string, string[]>[] namedResults, string optimize)
        {
            OptimizableRegression optimizationFunction = null;
            var prediction = doc.Settings.TransitionSettings.Prediction;
            if (Equals(optimize, ExportOptimize.CE))
                optimizationFunction = prediction.CollisionEnergy;
            else if (Equals(optimize, ExportOptimize.DP))
            {
                if (prediction.DeclusteringPotential == null)
                    throw new InvalidDataException(
                        Resources.
                            SkylineWindow_ImportResults_A_regression_for_declustering_potention_must_be_selected_in_the_Prediction_tab_of_the_Transition_Settings_in_order_to_import_optimization_data_for_decluserting_potential);

                optimizationFunction = prediction.DeclusteringPotential;
            }

            if (namedResults.Length == 1)
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
            return doc.ChangeMeasuredResults(results == null ?
                                                                 new MeasuredResults(arrayChrom) : results.ChangeChromatograms(arrayChrom));
        }

        private SrmDocument ImportResults(SrmDocument doc, string nameResult, IEnumerable<string> dataSources,
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
                var dataFilePaths = new List<string>(chrom.MSDataFilePaths);
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
            return doc.ChangeMeasuredResults(results);
        }

        private static ChromatogramSet GetChromatogramByName(string name, MeasuredResults results)
        {
            return (results == null ? null :
                results.Chromatograms.FirstOrDefault(set => Equals(name, set.Name)));
        }

        private void manageResultsMenuItem_Click(object sender, EventArgs e)
        {
            ManageResults();
        }

        public void ManageResults()
        {
            var documentUI = DocumentUI;
            if (!documentUI.Settings.HasResults)
                return;

            using (ManageResultsDlg dlg = new ManageResultsDlg(this))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    // Remove from the cache chromatogram data to be reimported.  This done before changing
                    // anything else to avoid making other changes to the results cause cache changes before
                    // the document is saved.
                    try
                    {
                        ReimportChromatograms(documentUI, dlg.ReimportChromatograms);
                    }
                    catch (Exception)
                    {
                        MessageDlg.Show(this, Resources.SkylineWindow_ManageResults_A_failure_occurred_attempting_to_reimport_results);
                    }

                    // And update the document to reflect real changes to the results structure
                    ModifyDocument(Resources.SkylineWindow_ManageResults_Manage_results, doc =>
                    {
                        var results = doc.Settings.MeasuredResults;
                        if (results == null)
                            return doc;
                        var listChrom = new List<ChromatogramSet>(dlg.Chromatograms);
                        if (ArrayUtil.ReferencesEqual(results.Chromatograms, listChrom))
                            return doc;
                        results = listChrom.Count > 0 ? results.ChangeChromatograms(listChrom.ToArray()) : null;
                        doc = doc.ChangeMeasuredResults(results);
                        doc.ValidateResults();
                        return doc;
                    });
                }
            }
        }

        private void ReimportChromatograms(SrmDocument document, IEnumerable<ChromatogramSet> chromatogramSets)
        {
            var setReimport = new HashSet<ChromatogramSet>(chromatogramSets);
            if (setReimport.Count == 0)
                return;
            
            using (new LongOp(this))
            {
                // Remove all replicates to be re-imported
                var results = document.Settings.MeasuredResults;
                var chromRemaining = results.Chromatograms.Where(chrom => !setReimport.Contains(chrom)).ToArray();
                var resultsNew = results.ChangeChromatograms(chromRemaining);
                if (chromRemaining.Length > 0)
                {
                    // Optimize the cache using this reduced set to remove their data from the cache
                    resultsNew = resultsNew.OptimizeCache(DocumentFilePath, _chromatogramManager.StreamManager);
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
                }
                while (!SetDocument(docNew, docCurrent));
            }
        }

        private void importPeptideSearchMenuItem_Click(object sender, EventArgs e)
        {
            ShowImportPeptideSearchDlg();
        }

        public void ShowImportPeptideSearchDlg()
        {
            if (!CheckDocumentExists(Resources.SkylineWindow_ShowImportPeptideSearchDlg_You_must_save_this_document_before_importing_a_peptide_search_))
            {
                return;
            }

            var dlg = new ImportPeptideSearchDlg(this, _libraryManager);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Nothing to do; the dialog does all the work.
            }
        }

        private bool CheckDocumentExists(String errorMsg)
        {
            if (string.IsNullOrEmpty(DocumentFilePath))
            {
                if (MessageBox.Show(errorMsg, Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
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
            var document = DocumentUI;
            if (!document.Settings.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_publishToolStripMenuItem_Click_The_document_must_be_fully_loaded_before_it_can_be_published);
                return;
            }

            string fileName = DocumentFilePath;
            if (string.IsNullOrEmpty(fileName))
            {
                if (MessageBox.Show(this, Resources.SkylineWindow_publishToolStripMenuItem_Click_The_document_must_be_saved_before_it_can_be_published,
                    Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    return;

                if (!SaveDocumentAs())
                    return;

                fileName = DocumentFilePath;
            }

            var servers = Settings.Default.ServerList;
            if (servers == null || servers.Count == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_Publish_There_are_no_Panorama_servers_to_publish_to_Please_add_a_server_under_Tools);
                return;
            }

            if (!SaveDocument())
                return;

            using (var publishDocumentDlg = new PublishDocumentDlg(servers, fileName))
            {
                publishDocumentDlg.PanoramaPublishClient = publishClient;
                if (publishDocumentDlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (ShareDocument(publishDocumentDlg.FileName, false))
                        publishDocumentDlg.UploadSharedZipFile(this);
                }
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
