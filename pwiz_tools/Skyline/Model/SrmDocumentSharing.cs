/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010-2011 University of Washington - Seattle, WA
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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Ionic.Zip;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class SrmDocumentSharing
    {
        public const string EXT = ".zip"; // Not L10N
        public const string EXT_SKY_ZIP = ".sky.zip"; // Not L10N

        public static string FILTER_SHARING
        {
            get { return TextUtil.FileDialogFilter(Resources.SrmDocumentSharing_FILTER_SHARING_Shared_Files, EXT); }
        }

        public SrmDocumentSharing(string sharedPath)
        {
            SharedPath = sharedPath;
        }

        public SrmDocumentSharing(SrmDocument document, string documentPath, string sharedPath, bool completeSharing)
        {
            Document = document;
            DocumentPath = documentPath;
            SharedPath = sharedPath;
            CompleteSharing = completeSharing;
        }

        public SrmDocument Document { get; private set; }
        public string DocumentPath { get; private set; }
        public string SharedPath { get; private set; }
        public bool CompleteSharing { get; private set; }

        private IProgressMonitor ProgressMonitor { get; set; }
        private IProgressStatus _progressStatus;
        private int CountEntries { get; set; }
        private int EntriesSaved { get; set; }
        private string CurrentEntry { get; set; }
        private long ExpectedSize { get; set; }
        private long ExtractedSize { get; set; }

        private string DefaultMessage
        {
            get
            {
                return string.Format(Document != null
                                         ? Resources.SrmDocumentSharing_DefaultMessage_Compressing_files_for_sharing_archive__0__
                                         : Resources.SrmDocumentSharing_DefaultMessage_Extracting_files_from_sharing_archive__0__,
                                     Path.GetFileName(SharedPath));
            }
        }

        public void Extract(IProgressMonitor progressMonitor)
        {
            ProgressMonitor = progressMonitor;
            ProgressMonitor.UpdateProgress(_progressStatus = new ProgressStatus(DefaultMessage));

            string extractDir = Path.GetFileName(SharedPath) ?? string.Empty;
            if (PathEx.HasExtension(extractDir, EXT_SKY_ZIP))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT_SKY_ZIP.Length);
            else if (PathEx.HasExtension(extractDir, EXT))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT.Length);

            using (ZipFile zip = ZipFile.Read(SharedPath))
            {
                CountEntries = zip.Entries.Count;
                ExpectedSize = zip.Entries.Select(entry => entry.UncompressedSize).Sum();

                zip.ExtractProgress += SrmDocumentSharing_ExtractProgress;

                string documentName = FindSharedSkylineFile(zip);

                string parentDir = Path.GetDirectoryName(SharedPath);
                if (!string.IsNullOrEmpty(parentDir))
                    extractDir = Path.Combine(parentDir, extractDir);
                extractDir = GetNonExistantDir(extractDir);
                DocumentPath = Path.Combine(extractDir, documentName);

                foreach (var entry in zip.Entries)
                {
                    if (ProgressMonitor.IsCanceled)
                        break;

                    try
                    {
                        entry.Extract(extractDir);

                        ExtractedSize += entry.UncompressedSize;
                    }
                    catch (Exception)
                    {
                        if (!ProgressMonitor.IsCanceled)
                            throw;
                    }
                }
            }

            if (ProgressMonitor.IsCanceled)
            {
                DirectoryEx.SafeDelete(extractDir);
            }
        }

        private static string GetNonExistantDir(string dirPath)
        {
            int count = 1;
            string dirResult = dirPath;

            while (Directory.Exists(dirResult))
            {
                // If a directory with the given name already exists, add
                // a suffix to create a unique folder name.
                dirResult = dirPath + "(" + count + ")"; // Not L10N
                count++;
            }
            return dirResult;
        }

        private static string FindSharedSkylineFile(ZipFile zip)
        {
            string skylineFile = null;

            foreach (var file in zip.EntryFileNames)
            {
                if (file == null) continue; // ReSharper

                // Shared files should not have subfolders.
                if (Path.GetFileName(file) != file)
                    throw new IOException(Resources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file);

                // Shared files must have exactly one Skyline Document(.sky).
                if (!file.EndsWith(SrmDocument.EXT)) continue;

                if (!string.IsNullOrEmpty(skylineFile))
                    throw new IOException(Resources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_contains_multiple_Skyline_documents);

                skylineFile = file;
            }

            if (string.IsNullOrEmpty(skylineFile))
            {
                throw new IOException(Resources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_does_not_contain_any_Skyline_documents);
            }
            return skylineFile;
        }

        public void Share(IProgressMonitor progressMonitor)
        {
            ProgressMonitor = progressMonitor;
            ProgressMonitor.UpdateProgress(_progressStatus = new ProgressStatus(DefaultMessage));

            using (var zip = new ZipFileShare())
            {
                if (CompleteSharing)
                    ShareComplete(zip);
                else
                    ShareMinimal(zip);
            }
        }

        private void ShareComplete(ZipFileShare zip)
        {
            // If complete sharing, just zip up existing files
            var pepSettings = Document.Settings.PeptideSettings;
            var transitionSettings = Document.Settings.TransitionSettings;
            if (Document.Settings.HasBackgroundProteome)
                zip.AddFile(pepSettings.BackgroundProteome.BackgroundProteomeSpec.DatabasePath);
            if (Document.Settings.HasRTCalcPersisted)
                zip.AddFile(pepSettings.Prediction.RetentionTime.Calculator.PersistencePath);
            if (Document.Settings.HasOptimizationLibraryPersisted)
                zip.AddFile(transitionSettings.Prediction.OptimizedLibrary.PersistencePath);
            if (Document.Settings.HasIonMobilityLibraryPersisted)
                zip.AddFile(pepSettings.Prediction.DriftTimePredictor.IonMobilityLibrary.PersistencePath);
            var libfiles = new HashSet<string>();
            foreach (var librarySpec in pepSettings.Libraries.LibrarySpecs)
            {
                if (libfiles.Add(librarySpec.FilePath)) // Sometimes the same .blib file is referred to by different library specs
                {
                    zip.AddFile(librarySpec.FilePath);

                    if (Document.Settings.TransitionSettings.FullScan.IsEnabledMs)
                    {
                        // If there is a .redundant.blib file that corresponds 
                        // to a .blib file, add that as well
                        IncludeRedundantBlib(librarySpec, zip, librarySpec.FilePath);
                    }
                }
            }

            ShareDataAndView(zip);
            zip.AddFile(DocumentPath);
            Save(zip);
        }

        private void ShareMinimal(ZipFileShare zip)
        {
            TemporaryDirectory tempDir = null;
            try
            {
                var docOriginal = Document;
                if (Document.Settings.HasBackgroundProteome)
                {
                    // Remove any background proteome reference
                    Document = Document.ChangeSettings(Document.Settings.ChangePeptideSettings(
                        set => set.ChangeBackgroundProteome(BackgroundProteome.NONE)));
                }
                if (Document.Settings.HasRTCalcPersisted)
                {
                    // Minimize any persistable retention time calculator
                    tempDir = new TemporaryDirectory();
                    string tempDbPath = Document.Settings.PeptideSettings.Prediction.RetentionTime
                        .Calculator.PersistMinimized(tempDir.DirPath, Document);
                    if (tempDbPath != null)
                        zip.AddFile(tempDbPath);
                }
                if (Document.Settings.HasOptimizationLibraryPersisted)
                {
                    if (tempDir == null)
                        tempDir = new TemporaryDirectory();
                    string tempDbPath = Document.Settings.TransitionSettings.Prediction.OptimizedLibrary.PersistMinimized(
                            tempDir.DirPath, Document);
                    if (tempDbPath != null)
                        zip.AddFile(tempDbPath);
                }
                if (Document.Settings.HasIonMobilityLibraryPersisted)
                {
                    // Minimize any persistable drift time predictor
                    if (tempDir == null)
                        tempDir = new TemporaryDirectory();
                    string tempDbPath = Document.Settings.PeptideSettings.Prediction.DriftTimePredictor
                        .IonMobilityLibrary.PersistMinimized(tempDir.DirPath, Document);
                    if (tempDbPath != null)
                        zip.AddFile(tempDbPath);
                }
                if (Document.Settings.HasLibraries)
                {
                    // Minimize all libraries in a temporary directory, and add them
                    if (tempDir == null)
                        tempDir = new TemporaryDirectory();
                    Document = BlibDb.MinimizeLibraries(Document, tempDir.DirPath, 
                                                        Path.GetFileNameWithoutExtension(DocumentPath),
                                                        ProgressMonitor);
                    if (ProgressMonitor != null && ProgressMonitor.IsCanceled)
                        return;

                    foreach (var librarySpec in Document.Settings.PeptideSettings.Libraries.LibrarySpecs)
                    {
                        var tempLibPath = Path.Combine(tempDir.DirPath, Path.GetFileName(librarySpec.FilePath) ?? string.Empty);
                        zip.AddFile(tempLibPath);

                        // If there is a .redundant.blib file that corresponds to a .blib file
                        // in the temp temporary directory, add that as well
                        IncludeRedundantBlib(librarySpec, zip, tempLibPath);
                    }
                }

                ShareDataAndView(zip);
                if (ReferenceEquals(docOriginal, Document))
                    zip.AddFile(DocumentPath);
                else
                {
                    // If minimizing changed the document, then serialize and archive the new document
                    if (tempDir == null)
                        tempDir = new TemporaryDirectory();
                    string fileName = Path.GetFileName(DocumentPath) ?? string.Empty;
                    string tempDocPath = Path.Combine(tempDir.DirPath, fileName);
                    using (var writer = new XmlWriterWithProgress(tempDocPath, fileName, Encoding.UTF8,
                        Document.MoleculeTransitionCount, ProgressMonitor)
                    {
                        Formatting = Formatting.Indented
                    })
                    {
                        XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                        ser.Serialize(writer, Document);
                        zip.AddFile(tempDocPath);
                    }
                }
                Save(zip);
            }
            finally
            {
                if (tempDir != null)
                {
                    try
                    {
                        tempDir.Dispose();
                    }
                    catch (IOException x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.SrmDocumentSharing_ShareMinimal_Failure_removing_temporary_directory__0__,
                                                                          tempDir.DirPath),
                                                            x.Message);
                        throw new IOException(message);
                    }
                }
            }
        }

        private void IncludeRedundantBlib(LibrarySpec librarySpec, ZipFileShare zip, string blibPath)
        {
            if (librarySpec is BiblioSpecLiteSpec)
            {
                var redundantBlibPath = BiblioSpecLiteSpec.GetRedundantName(blibPath);
                if (File.Exists(redundantBlibPath))
                {
                    zip.AddFile(redundantBlibPath);
                }
            }
        }

        private void ShareDataAndView(ZipFileShare zip)
        {
            string pathCache = ChromatogramCache.FinalPathForName(DocumentPath, null);
            if (File.Exists(pathCache))
                zip.AddFile(pathCache);
            string viewPath = SkylineWindow.GetViewFile(DocumentPath);
            if (File.Exists(viewPath))
                zip.AddFile(viewPath);
        }

        private void Save(ZipFileShare zip)
        {
            CountEntries = zip.CountEntries;

            using (var saver = new FileSaver(SharedPath))
            {
                zip.Save(saver.SafeName, SrmDocumentSharing_SaveProgress);
                ProgressMonitor.UpdateProgress(_progressStatus.Complete());
                saver.Commit();
            }
        }

        private void SrmDocumentSharing_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                {
                    e.Cancel = true;
                    return;
                }

                int progressValue = (int)Math.Round((ExtractedSize + e.BytesTransferred) * 100.0 / ExpectedSize);

                if (progressValue != _progressStatus.PercentComplete)
                {
                    var message = (e.CurrentEntry != null
                                       ? string.Format(Resources.SrmDocumentSharing_SrmDocumentSharing_ExtractProgress_Extracting__0__,
                                                       e.CurrentEntry.FileName)
                                       : DefaultMessage);
                    ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangeMessage(message).ChangePercentComplete(progressValue));
                }
            }
        }

        private void SrmDocumentSharing_SaveProgress(object sender, SaveProgressEventArgs e)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                {
                    e.Cancel = true;
                    return;
                }

                // TODO: More accurate total byte progress
                double percentCompressed = (e.TotalBytesToTransfer > 0 ?
                    1.0 * e.BytesTransferred / e.TotalBytesToTransfer : 0);
                int progressValue = (int)Math.Round((EntriesSaved + percentCompressed) * 100 / CountEntries);

                if (progressValue != _progressStatus.PercentComplete)
                {
                    _progressStatus = _progressStatus.ChangePercentComplete(progressValue);
                    
                    if (e.CurrentEntry == null)
                    {
                        ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangeMessage(DefaultMessage));
                    }
                        
                    else
                    {
                        if (!Equals(CurrentEntry, e.CurrentEntry.FileName))
                        {
                            if (CurrentEntry != null)
                                EntriesSaved++;
                            CurrentEntry = e.CurrentEntry.FileName;
                        }
                        var message = string.Format(Resources.SrmDocumentSharing_SrmDocumentSharing_SaveProgress_Compressing__0__,
                                                           e.CurrentEntry.FileName);
                        ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangeMessage(message));
                    }
                }
            }
        }

        private class ZipFileShare : IDisposable
        {
            private readonly ZipFile _zip;
            private readonly IDictionary<string, string> _dictNameToPath;

            public ZipFileShare()
            {
                // Make sure large files don't cause this to fail.
                _zip = new ZipFile { UseZip64WhenSaving = Zip64Option.AsNecessary };

                _dictNameToPath = new Dictionary<string, string>();
            }

            public int CountEntries { get { return _zip.Entries.Count; } }

            public void AddFile(string path)
            {
                string existingPath;
                string fileName = Path.GetFileName(path) ?? string.Empty;
                if (_dictNameToPath.TryGetValue(fileName, out existingPath))
                {
                    if (path != existingPath)
                    {
                        throw new IOException(TextUtil.LineSeparate(string.Format(Resources.ZipFileShare_AddFile_Failed_attempting_to_add_the_file__0_, path),
                            string.Format(Resources.ZipFileShare_AddFile_The_name___0___is_already_in_use_from_the_path__1_, fileName, existingPath)));
                    }

                    // No need to add exactly the same path twice
                    return;
                }
                _dictNameToPath.Add(fileName, path);
                _zip.AddFile(path, string.Empty);
            }

            public void Save(string path, EventHandler<SaveProgressEventArgs> progressEvent)
            {
                _zip.SaveProgress += progressEvent;
                _zip.Save(path);
            }

            public void Dispose()
            {
                if (_zip != null) _zip.Dispose();
            }
        }


        #region Functional testing support

        public IEnumerable<string> ListEntries()
        {
            ProgressMonitor = null;
            var entries = new List<string>();

            using (ZipFile zip = ZipFile.Read(SharedPath))
            {
                foreach (var entry in zip.Entries)
                {
                    entries.Add(entry.FileName);
                }
            }

            return entries;
        }

        #endregion
    }
}
