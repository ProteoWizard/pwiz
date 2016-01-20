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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Esp;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
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
            int len = Math.Min(mruList.Count, Settings.Default.MruLength);
            for (int i = 0; i < len; i++)
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

            // Make sure settings lists contain correct values for
            // this document.
            document.Settings.UpdateLists(null);

            // Switch over to the new document
            SwitchDocument(document, null);
        }

        private void openContainingFolderMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", @"/select, " + DocumentFilePath); // Not L10N
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
                Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC_AND_SKY_ZIP, SrmDocumentSharing.FILTER_SHARING)
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);

                    if (dlg.FileName.EndsWith(SrmDocumentSharing.EXT))
                    {
                        OpenSharedFile(dlg.FileName);
                    }
                    else
                    {
                        OpenFile(dlg.FileName);
                    }
                }
            }
        }

        public bool OpenSharedFile(string zipPath)
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
                        return false;
                }

                // Remember the directory containing the newly extracted file
                // as the active directory for the next open command.
                Settings.Default.ActiveDirectory = Path.GetDirectoryName(sharing.DocumentPath);

                return OpenFile(sharing.DocumentPath);
            }
            catch (ZipException zipException)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_OpenSharedFile_The_zip_file__0__cannot_be_read,
                                                    zipPath), zipException);
                return false;
            }
            catch (Exception e)
            {
                var message = TextUtil.LineSeparate(string.Format(
                        Resources.SkylineWindow_OpenSharedFile_Failure_extracting_Skyline_document_from_zip_file__0__,
                        zipPath), e.Message);
                MessageDlg.ShowWithException(this, message, e);
                return false;
            }
        }

        public bool OpenFile(string path, FormEx parentWindow = null)
        {
            // Remove any extraneous temporary chromatogram spill files.
            var spillDirectory = Path.Combine(Path.GetDirectoryName(path) ?? "", "xic");    // Not L10N
            if (Directory.Exists(spillDirectory))
                DirectoryEx.SafeDelete(spillDirectory);

            Exception exception = null;
            SrmDocument document = null;

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
                        using (var reader = new StreamReaderWithProgress(path, progressMonitor))
                        {
                            XmlSerializer ser = new XmlSerializer(typeof (SrmDocument));
                            document = (SrmDocument) ser.Deserialize(reader);
                        }
                    });

                    if (longWaitDlg.IsCanceled)
                        document = null;
                }
            }
            catch (Exception x)
            {
                exception = x;
            }

            if (exception == null)
            {
                if (document == null)
                    return false;

                try
                {
                    document = ConnectDocument(parentWindow ?? this, document, path);
                    if (document == null || !CheckResults(document, path))
                        return false;

                    // Make sure settings lists contain correct values for
                    // this document.
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
            }

            if (exception != null)
            {
                new MessageBoxHelper(parentWindow ?? this).ShowXmlParsingError(
                    string.Format(Resources.SkylineWindow_OpenFile_Failure_opening__0__, path), path, exception);
                return false;
            }

            if (SequenceTree != null && SequenceTree.Nodes.Count > 0 && !SequenceTree.RestoredFromPersistentString)
                SequenceTree.SelectedNode = SequenceTree.Nodes[0];

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
                    MessageDlg.Show(this, string.Format(Resources.SkylineWindow_ConnectLibrarySpecs_Could_not_find_the_spectral_library__0__for_this_document__Without_the_library__no_spectrum_ID_information_will_be_available_, docLibFile));
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs(library =>
                {
                    LibrarySpec spec;
                    if (Settings.Default.SpectralLibraryList.TryGetValue(library.Name, out spec))
                    {
                        if (File.Exists(spec.FilePath))
                            return spec;                        
                    }
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
                        pathLibrary = Path.Combine(Settings.Default.LibraryDirectory ?? string.Empty, fileName);
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
                        if (dlg.ShowDialog(parent) == DialogResult.OK)
                        {
                            Settings.Default.LibraryDirectory = Path.GetDirectoryName(dlg.FilePath);
                            return library.CreateSpec(dlg.FilePath);
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
            string fileName = Path.GetFileName(optLib.DatabasePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
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

        private SrmDocument ConnectIonMobilityLibrary(IWin32Window parent, SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIonMobilityLibrary(imdb => FindIonMobilityLibrary(parent, documentPath, imdb));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private IonMobilityLibrarySpec FindIonMobilityLibrary(IWin32Window parent, string documentPath, IonMobilityLibrarySpec ionMobilityLibrarySpec)
        {

            IonMobilityLibrarySpec result;
            if (Settings.Default.IonMobilityLibraryList.TryGetValue(ionMobilityLibrarySpec.Name, out result))
            {
                if (result != null && File.Exists(result.PersistencePath))
                    return result;
            }
            if (documentPath == null)
                return null;

            // First look for the file name in the document directory
            string fileName = Path.GetFileName(ionMobilityLibrarySpec.PersistencePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
            {
                try
                {
                    var ionMobilityLib = ionMobilityLibrarySpec as IonMobilityLibrary;
                    if (ionMobilityLib != null)
                        return ionMobilityLib.ChangeDatabasePath(filePath);
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
                    ItemName = ionMobilityLibrarySpec.Name,
                    ItemType = Resources.SkylineWindow_FindIonMobilityLibrary_Ion_Mobility_Library,
                    Filter = TextUtil.FileDialogFilterAll(Resources.SkylineWindow_FindIonMobilityDatabase_ion_mobility_library_files, IonMobilityDb.EXT),
                    FileHint = Path.GetFileName(ionMobilityLibrarySpec.PersistencePath),
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
                            var ionMobilityLib = ionMobilityLibrarySpec as IonMobilityLibrary;
                            if (ionMobilityLib != null)
                                return ionMobilityLib.ChangeDatabasePath(dlg.FilePath);
                        }
                        catch (DatabaseOpeningException e)
                        {
                            var message = TextUtil.SpaceSeparate(
                                Resources.SkylineWindow_FindIonMobilityDatabase_The_ion_mobility_library_specified_could_not_be_opened_,
                                e.Message); 
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

            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            // First look for the file name in the document directory
            string pathBackgroundProteome = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
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
                            using (var writer = new XmlWriterWithProgress(saver.SafeName, fileName, Encoding.UTF8,
                                document.MoleculeTransitionCount, progressMonitor)
                            {
                                Formatting = Formatting.Indented
                            })
                            {
                                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                                ser.Serialize(writer, document);

                                writer.Flush();
                                writer.Close();

                                // If the user has chosen "Save As", and the document has a
                                // document specific spectral library, copy this library to 
                                // the new name.
                                if (!Equals(DocumentFilePath, fileName))
                                    SaveDocumentLibraryAs(fileName);

                                saver.Commit();
                            }
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
                MessageBox.Show(message);
                return false;
            }

            DocumentFilePath = fileName;
            _savedVersion = document.UserRevisionIndex;
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
            // exist on disk
            var document = Document;
            if (document.Settings.PeptideSettings.Libraries.HasDocumentLibrary
                && File.Exists(oldDocLibFile))
            {
                string newDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(newDocFilePath);
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
                        CopyFile(oldDocLibFile, saverLib);
                        if (saverRedundant != null)
                        {
                            CopyFile(oldRedundantDocLibFile, saverRedundant);
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

        private void CopyFile(string source, FileSaver destSaver)
        {
            // Copy the specified file to the new name using a FileSaver
            destSaver.CheckException();
            File.Copy(source, destSaver.SafeName, true);
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
            var document = DocumentUI;
            if (!document.IsLoaded)
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
                FileName = Path.GetFileNameWithoutExtension(fileName) + SrmDocumentSharing.EXT_SKY_ZIP,
                OverwritePrompt = true,
                DefaultExt = SrmDocumentSharing.EXT_SKY_ZIP,
                SupportMultiDottedExtensions = true,
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
                        DocumentUI.Molecules.Select(nodePep => nodePep.Peptide.Sequence), LocalizationHelper.CurrentCulture);
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

        private void reintegrateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowReintegrateDialog();
        }
        
        public void ShowReintegrateDialog()
        {
            var documentOrig = DocumentUI;
            if (!documentOrig.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_imported_results_);
                return;
            }
            if (documentOrig.MoleculeCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_targets_in_order_to_reintegrate_chromatograms_);
                return;
            }
            if (!documentOrig.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_be_fully_loaded_before_it_can_be_re_integrated_);
                return;                
            }
            using (var dlg = new ReintegrateDlg(documentOrig))
            {
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;
                ModifyDocument(Resources.SkylineWindow_ShowReintegrateDialog_Reintegrate_peaks, doc =>
                    {
                        if (!ReferenceEquals(documentOrig, doc))
                            throw new InvalidDataException(
                                Resources.SkylineWindow_ShowReintegrateDialog_Unexpected_document_change_during_operation_);
                        return dlg.Document;
                    });
            }
        }

        private void compareModelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowCompareModelsDlg();
        }

        public void ShowCompareModelsDlg()
        {
            var document = DocumentUI;
            if (!document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowReintegrateDialog_The_document_must_have_imported_results_);
                return;
            }
            if (document.MoleculeCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowCompareModelsDlg_The_document_must_have_targets_in_order_to_compare_model_peak_picking_);
                return;
            }
            if (!document.IsLoaded)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ShowCompareModelsDlg_The_document_must_be_fully_loaded_in_order_to_compare_model_peak_picking_);
                return;
            }
            using (var dlg = new ComparePeakPickingDlg(document))
            {
                dlg.ShowDialog(this);
            }
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
                MessageDlg.ShowWithException(this,
                                TextUtil.LineSeparate(
                                string.Format(Resources.SkylineWindow_ImportPeakBoundariesFile_Failed_reading_the_file__0__,
                                              peakBoundariesFile), x.Message), x);
            }         
        }

        private void ImportPeakBoundaries(string fileName, long lineCount, string description)
        {
            var docCurrent = DocumentUI;
            SrmDocument docNew = null;
            using (var longWaitDlg = new LongWaitDlg(this) { Text = description })
            {
                var peakBoundaryImporter = new PeakBoundaryImporter(docCurrent);
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
                    ImportFasta(readerFasta, lineCount, false, Resources.SkylineWindow_ImportFastaFile_Import_FASTA);
                }
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                                                    fastaFile, x.Message), x);
            }
        }

        public void ImportFasta(TextReader reader, long lineCount, bool peptideList, string description)
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

                if (longWaitDlg.IsDocumentChanged(docCurrent))
                {
                    MessageDlg.Show(this, Resources.SkylineWindow_ImportFasta_Unexpected_document_change_during_operation);
                    return;
                }
            }

            // If importing the FASTA produced any childless proteins
            docNew = ImportFastaHelper.HandleEmptyPeptideGroups(this, emptyPeptideGroups, docNew);
            if (docNew == null || Equals(docCurrent, docNew))
                return;

            selectPath = null;
            var enumGroupsCurrent = docCurrent.MoleculeGroups.GetEnumerator();
            foreach (PeptideGroupDocNode nodePepGroup in docNew.MoleculeGroups)
            {
                if (enumGroupsCurrent.MoveNext() &&
                    !ReferenceEquals(nodePepGroup, enumGroupsCurrent.Current))
                {
                    selectPath = new IdentityPath(nodePepGroup.Id);
                    break;
                }
            }

            ModifyDocument(description, doc =>
            {
                if (!ReferenceEquals(doc, docCurrent))
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
                    ImportMassList(dlg.FileName);
                }
            }
        }

        public void ImportMassList(string fileName)
        {
            try
            {
                ImportMassList(new MassListInputs(fileName), Resources.SkylineWindow_importMassListMenuItem_Click_Import_transition_list);
            }
            catch (Exception x)
            {
                MessageDlg.ShowWithException(this, string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__, fileName, x.Message), x);
            }
        }

        private void ImportMassList(MassListInputs inputs, string description)
        {
            SrmTreeNode nodePaste = SequenceTree.SelectedNode as SrmTreeNode;
            IdentityPath insertPath = nodePaste != null ? nodePaste.Path : null;
            IdentityPath selectPath = null;
            List<MeasuredRetentionTime> irtPeptides = null;
            List<SpectrumMzInfo> librarySpectra = null;
            List<TransitionImportErrorInfo> errorList = null;
            List<PeptideGroupDocNode> peptideGroups = null;
            RetentionTimeRegression retentionTimeRegressionStore = null;
            var docCurrent = DocumentUI;
            SrmDocument docNew = null;
            var retentionTimeRegression = docCurrent.Settings.PeptideSettings.Prediction.RetentionTime;
            RCalcIrt calcIrt = retentionTimeRegression != null ? (retentionTimeRegression.Calculator as RCalcIrt) : null;
            using (var longWaitDlg = new LongWaitDlg(this) {Text = description})
            {
                longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                {
                    docNew = docCurrent.ImportMassList(inputs, longWaitBroker,
                        insertPath, out selectPath, out irtPeptides, out librarySpectra, out errorList, out peptideGroups);
                });
            }
            bool isDocumentSame = ReferenceEquals(docNew, docCurrent);
            // If nothing was imported (e.g. operation was canceled or zero error-free transitions) and also no errors, just return
            if (isDocumentSame && !errorList.Any())
                return;
            // Show the errors, giving the option to accept the transitions without errors,
            // if there are any
            if (errorList.Any())
            {
                using (var errorDlg = new ImportTransitionListErrorDlg(errorList, isDocumentSame))
                {
                    if (errorDlg.ShowDialog(this) == DialogResult.Cancel || isDocumentSame)
                    {
                        return;
                    }   
                }
            }

            var dbIrtPeptides = irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, false, TimeSource.scan)).ToList();
            var dbIrtPeptidesFilter = ImportAssayLibraryHelper.GetUnscoredIrtPeptides(dbIrtPeptides, calcIrt);
            bool overwriteExisting = false;
            MassListInputs irtInputs = null;
            // If there are no iRT peptides or none with different values than the database, don't import any iRT's
            if (dbIrtPeptidesFilter.Any())
            {
                // Ask whether or not to include iRT peptides in the paste
                string useIrtMessage = calcIrt == null
                    ? Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_values__but_the_document_does_not_have_an_iRT_calculator___Create_a_new_calculator_and_add_these_iRT_values_
                    : Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_iRT_library_values___Add_these_iRT_values_to_the_iRT_calculator_;
                string yesButton = calcIrt == null
                    ? Resources.SkylineWindow_ImportMassList__Create___ 
                    : Resources.SkylineWindow_ImportMassList_Add;
                var useIrtResult = MultiButtonMsgDlg.Show(this, useIrtMessage,
                    yesButton, Resources.SkylineWindow_ImportMassList__Skip, true);
                if (useIrtResult == DialogResult.Cancel)
                {
                    return;
                }
                if (useIrtResult == DialogResult.Yes)
                {
                    if (calcIrt == null)
                    {
                        // If there is no iRT calculator, ask the user to create one
                        using (var dlg = new CreateIrtCalculatorDlg(docNew, DocumentFilePath, Settings.Default.RTScoreCalculatorList, peptideGroups))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK)
                            {
                                return;
                            }

                            docNew = dlg.Document;
                            calcIrt = (RCalcIrt)docNew.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
                            dlg.UpdateLists(librarySpectra, dbIrtPeptidesFilter);
                            if (!string.IsNullOrEmpty(dlg.IrtFile))
                                irtInputs = new MassListInputs(dlg.IrtFile);
                        }
                    }
                    string dbPath = calcIrt.DatabasePath;
                    IrtDb db = File.Exists(dbPath) ? IrtDb.GetIrtDb(dbPath, null) : IrtDb.CreateIrtDb(dbPath);
                    var oldPeptides = db.GetPeptides().ToList();
                    IList<DbIrtPeptide.Conflict> conflicts;
                    dbIrtPeptidesFilter = DbIrtPeptide.MakeUnique(dbIrtPeptidesFilter);
                    DbIrtPeptide.FindNonConflicts(oldPeptides, dbIrtPeptidesFilter, null, out conflicts);
                    // Ask whether to keep or overwrite peptides that are present in the import and already in the database
                    if (conflicts.Any())
                    {
                        string messageOverwrite = string.Format(Resources.SkylineWindow_ImportMassList_The_iRT_calculator_already_contains__0__of_the_imported_peptides_,
                                                                conflicts.Count);
                        var overwriteResult = MultiButtonMsgDlg.Show(this,
                            TextUtil.LineSeparate(messageOverwrite, conflicts.Count == 1
                                ? Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_value_or_overwrite_with_the_imported_value_
                                : Resources.SkylineWindow_ImportMassList_Keep_the_existing_iRT_values_or_overwrite_with_imported_values_),
                            Resources.SkylineWindow_ImportMassList__Keep, Resources.SkylineWindow_ImportMassList__Overwrite, true);
                        if (overwriteResult == DialogResult.Cancel)
                        {
                            return;
                        }
                        overwriteExisting = overwriteResult == DialogResult.No;
                    }
                    using (var longWaitDlg = new LongWaitDlg(this) { Text = Resources.SkylineWindow_ImportMassList_Adding_iRT_values_})
                    {
                        longWaitDlg.PerformWork(this, 100, progressMonitor => 
                            docNew = docNew.AddIrtPeptides(dbIrtPeptidesFilter, overwriteExisting, progressMonitor));
                    }
                    if (docNew == null)
                        return;
                    retentionTimeRegressionStore = docNew.Settings.PeptideSettings.Prediction.RetentionTime;
                }
            }
            BiblioSpecLiteSpec docLibrarySpec = null;
            BiblioSpecLiteLibrary docLibrary = null;
            int indexOldLibrary = -1;
            if (librarySpectra.Any())
            {
                string addLibraryMessage = Resources.SkylineWindow_ImportMassList_The_transition_list_appears_to_contain_spectral_library_intensities___Create_a_document_library_from_these_intensities_;
                var addLibraryResult = MultiButtonMsgDlg.Show(this, addLibraryMessage,
                    Resources.SkylineWindow_ImportMassList__Create___, Resources.SkylineWindow_ImportMassList__Skip,
                    true);
                if (addLibraryResult == DialogResult.Cancel)
                {
                    return;
                }
                if (addLibraryResult == DialogResult.Yes)
                {
                    // Can't name a library after the document if the document is unsaved
                    // In this case, prompt to save
                    if (DocumentFilePath == null)
                    {
                        string saveDocumentMessage = Resources.SkylineWindow_ImportMassList_You_must_save_the_Skyline_document_in_order_to_create_a_spectral_library_from_a_transition_list_;
                        var saveDocumentResult = MultiButtonMsgDlg.Show(this, saveDocumentMessage, MultiButtonMsgDlg.BUTTON_OK);
                        if (saveDocumentResult == DialogResult.Cancel)
                        {
                            return;
                        }
                        else if (!SaveDocumentAs())
                        {
                            return;
                        }
                    }

                    librarySpectra = SpectrumMzInfo.RemoveDuplicateSpectra(librarySpectra);

                    string documentLibrary = BiblioSpecLiteSpec.GetLibraryFileName(DocumentFilePath);
// ReSharper disable once AssignNullToNotNullAttribute
                    string outputPath = Path.Combine(Path.GetDirectoryName(documentLibrary), 
                                                     Path.GetFileNameWithoutExtension(documentLibrary) + BiblioSpecLiteSpec.ASSAY_NAME + BiblioSpecLiteSpec.EXT);
                    bool libraryExists = File.Exists(outputPath);
                    string name = Path.GetFileNameWithoutExtension(DocumentFilePath) + BiblioSpecLiteSpec.ASSAY_NAME;
                    indexOldLibrary = docNew.Settings.PeptideSettings.Libraries.LibrarySpecs.IndexOf(spec => spec != null && spec.FilePath == outputPath);
                    bool libraryLinkedToDoc = indexOldLibrary != -1;
                    if (libraryLinkedToDoc)
                    {
                        string oldName = docNew.Settings.PeptideSettings.Libraries.LibrarySpecs[indexOldLibrary].Name;
                        var libraryOld = docNew.Settings.PeptideSettings.Libraries.GetLibrary(oldName);
                        var additionalSpectra = SpectrumMzInfo.GetInfoFromLibrary(libraryOld);
                        additionalSpectra = SpectrumMzInfo.RemoveDuplicateSpectra(additionalSpectra);
                        librarySpectra = SpectrumMzInfo.MergeWithOverwrite(librarySpectra, additionalSpectra);
                        foreach (var stream in libraryOld.ReadStreams)
                            stream.CloseStream();
                    }
                    if (libraryExists && !libraryLinkedToDoc)
                    {
                        string replaceLibraryMessage = string.Format(Resources.SkylineWindow_ImportMassList_There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created___Overwrite_this_library_or_skip_import_of_library_intensities_,
                                          name);
                        // If the document does not have an assay library linked to it, then ask if user wants to delete the one that we have found
                        var replaceLibraryResult = MultiButtonMsgDlg.Show(this, replaceLibraryMessage,
                            Resources.SkylineWindow_ImportMassList__Overwrite, Resources.SkylineWindow_ImportMassList__Skip, true);
                        if (replaceLibraryResult == DialogResult.Cancel)
                            return;
                        if (replaceLibraryResult == DialogResult.No)
                            librarySpectra.Clear();
                    }
                    if (librarySpectra.Any())
                    {
                        // Delete the existing library; either it's not tied to the document or we've already extracted the spectra
                        if (libraryExists)
                        {
                            FileEx.SafeDelete(outputPath);
                            FileEx.SafeDelete(Path.ChangeExtension(outputPath, BiblioSpecLiteSpec.EXT_REDUNDANT));
                        }
                        using (var blibDb = BlibDb.CreateBlibDb(outputPath))
                        {
                            docLibrarySpec = new BiblioSpecLiteSpec(name, outputPath);
                            using (var longWaitDlg = new LongWaitDlg(this) { Text = Resources.SkylineWindow_ImportMassList_Creating_Spectral_Library })
                            {
                                longWaitDlg.PerformWork(this, 1000, progressMonitor =>
                                {
                                    docLibrary = blibDb.CreateLibraryFromSpectra(docLibrarySpec, librarySpectra, name, progressMonitor);
                                    if (docLibrary == null)
                                        return;
                                    var newSettings = docNew.Settings.ChangePeptideLibraries(libs => libs.ChangeLibrary(docLibrary, docLibrarySpec, indexOldLibrary));
                                    var status = new ProgressStatus(Resources.SkylineWindow_ImportMassList_Finishing_up_import);
                                    progressMonitor.UpdateProgress(status);
                                    progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
                                    docNew = docNew.ChangeSettings(newSettings);
                                });
                                if (docLibrary == null)
                                    return;
                            }
                        }
                    }
                }
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
                    doc = doc.ImportMassList(inputs, insertPath, out selectPath);
                    if (irtInputs != null)
                    {
                        doc = doc.ImportMassList(irtInputs, null, out selectPath);
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
                catch (UnauthorizedAccessException) {}
                catch (IOException) {}
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
            if (DocumentUI.MoleculeTransitionCount == 0)
            {
                MessageDlg.Show(this, Resources.SkylineWindow_ImportResults_You_must_add_at_least_one_target_transition_before_importing_results_);
                return;
            }
            if (!CheckDocumentExists(Resources.SkylineWindow_ImportResults_You_must_save_this_document_before_importing_results))
            {
                return;
            }
            if (!CheckRetentionTimeFilter(DocumentUI))
            {
                return;
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
                        description = string.Format(Resources.SkylineWindow_ImportResults_Import__0__, namedResults[0].Key); // Not L10N

                    // Check with user for Waters lockmass settings if any, results written to Settings.Default
                    // If lockmass correction is desired, MsDataFileUri values in namedResults are modified by this call.
                    if (!ImportResultsLockMassDlg.UpdateNamedResultsParameters(this, DocumentUI, ref namedResults))
                        return; // User cancelled, no change

                    ModifyDocument(description,
                                   doc => ImportResults(doc, namedResults, dlg.OptimizationName));

                    // Select the first replicate to which results were added.
                    if (ComboResults.Visible)
                        ComboResults.SelectedItem = namedResults[0].Key;
                }
            }
        }

        /// <summary>
        /// If the Transition Full Scan settings are such that the time window for extracting
        /// chromatograms depends on a set of replicates, then this function shows the
        /// ChooseSchedulingReplicatesDlg.
        /// Returns false if the user cancels the dialog, or cannot import chromatograms.
        /// </summary>
        public bool CheckRetentionTimeFilter(SrmDocument document)
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
                    if (MessageBox.Show(this, Resources.SkylineWindow_CheckRetentionTimeFilter_NoReplicatesAvailableForPrediction,
                        Program.Name, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
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
                return dlg.ShowDialog(this) == DialogResult.OK;
            }
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

        private void manageResultsMenuItem_Click(object sender, EventArgs e)
        {
            ManageResults();
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
                    catch (Exception)
                    {
                        MessageDlg.Show(this, Resources.SkylineWindow_ManageResults_A_failure_occurred_attempting_to_reimport_results);
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
                                    if (libSpecs[i].IsDocumentLibrary)
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
                            BiblioSpecLiteLibrary docBlib;
                            if (DocumentUI.Settings.PeptideSettings.Libraries.TryGetDocumentLibrary(out docBlib))
                            {
                                try
                                {
                                    docBlib.DeleteDataFiles(dlg.LibraryRunsRemovedList.ToArray(), this);
                                    _libraryManager.ReloadLibrary(this, dlg.DocumentLibrarySpec);
                                }
                                catch (Exception x)
                                {
                                    throw new IOException(TextUtil.LineSeparate(Resources.SkylineWindow_ManageResults_Failed_to_remove_library_runs_from_the_document_library_, x.Message));
                                }
                            }
                        }

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
                        }
                        catch (FileEx.DeleteException deleteException)
                        {
                            MessageDlg.ShowException(this, deleteException);
                        }
                    }
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

        public void ShowImportPeptideSearchDlg(ImportPeptideSearchDlg.Workflow? workflowType)
        {
            if (!CheckDocumentExists(Resources.SkylineWindow_ShowImportPeptideSearchDlg_You_must_save_this_document_before_importing_a_peptide_search_))
            {
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

            if (!SaveDocument())
                return;

            var servers = Settings.Default.ServerList;
            if (servers.Count == 0)
            {
                DialogResult buttonPress = MultiButtonMsgDlg.Show(
                    this,
                    TextUtil.LineSeparate(
                        Resources.SkylineWindow_ShowPublishDlg_There_are_no_Panorama_servers_to_publish_to,
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
                    WebHelpers.OpenLink(this, "http://proteome.gs.washington.edu/software/Skyline/panoramaweb-signup.html"); // Not L10N
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
                using (var publishDocumentDlg = new PublishDocumentDlg(this, servers, fileName))
                {
                    publishDocumentDlg.PanoramaPublishClient = publishClient;
                    if (publishDocumentDlg.ShowDialog(this) == DialogResult.OK)
                    {
                        if (ShareDocument(publishDocumentDlg.FileName, false))
                            publishDocumentDlg.Upload(this);
                    }
                }
            }
        }

        private bool PublishToSavedUri(IPanoramaPublishClient publishClient, Uri panoramaSavedUri, string fileName,
            ServerList servers)
        {
            var message = TextUtil.LineSeparate(Resources.SkylineWindow_ShowPublishDlg_This_file_was_last_published_to___0_,
                Resources.SkylineWindow_ShowPublishDlg_Publish_to_the_same_location_);
            if (MultiButtonMsgDlg.Show(this, string.Format(message, panoramaSavedUri),
                    MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false) != DialogResult.Yes)
                return false;

            var server = servers.FirstOrDefault(s => s.URI.Host.Equals(panoramaSavedUri.Host));
            if (server == null)
                return false;

            JToken folders;
            var folderPath = panoramaSavedUri.AbsolutePath;
            try
            {
                folders = publishClient.GetInfoForFolders(server, folderPath.TrimEnd('/').TrimStart('/'));
            }
            catch (WebException)
            {
                return false;
            }

            // must escape uri string as panorama api does not and strings are escaped in schema
            if (folders == null || !folderPath.Contains(Uri.EscapeUriString(folders["path"].ToString()))) // Not L10N
                return false;

            if (!PanoramaUtil.CheckFolderPermissions(folders) || !PanoramaUtil.CheckFolderType(folders))
                return false;

            var fileInfo = new FolderInformation(server, true);
            if (!publishClient.ServerSupportsSkydVersion(fileInfo, this, this))
                return false;

            var zipFilePath = FileEx.GetTimeStampedFileName(fileName);
            if (!ShareDocument(zipFilePath, false))
                return false;

            var serverRelativePath = folders["path"].ToString() + '/'; // Not L10N
            serverRelativePath = serverRelativePath.TrimStart('/');
            publishClient.UploadSharedZipFile(this, server, zipFilePath, serverRelativePath);
            return true; // success!
        }


        private void chorusRequestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new ExportChorusRequestDlg(DocumentUI, Path.GetFileNameWithoutExtension(DocumentFilePath)))
            {
                dlg.ShowDialog(this);
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
