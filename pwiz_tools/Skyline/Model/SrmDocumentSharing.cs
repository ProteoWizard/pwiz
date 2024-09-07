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
using Ionic.Zip;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class SrmDocumentSharing
    {
        public const string EXT = ".zip";
        public const string EXT_SKY_ZIP = ".sky.zip";
        private TemporaryDirectory _tempDir;
        public static string FILTER_SHARING
        {
            get { return TextUtil.FileDialogFilter(ModelResources.SrmDocumentSharing_FILTER_SHARING_Shared_Files, EXT); }
        }

        public SrmDocumentSharing(string sharedPath)
        {
            SharedPath = sharedPath;
            ShareType = ShareType.DEFAULT;
        }

        public SrmDocumentSharing(SrmDocument document, string documentPath, string sharedPath, ShareType shareType) : this(sharedPath)
        {
            Document = document;
            DocumentPath = documentPath;
            ShareType = shareType;
        }

        public SrmDocument Document { get; private set; }
        public string DocumentPath { get; private set; }
        public string ViewFilePath { get; set; }

        public string SharedPath { get; private set; }

        public ShareType ShareType { get; set; }
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
                        ? ModelResources.SrmDocumentSharing_DefaultMessage_Compressing_files_for_sharing_archive__0__
                        : ModelResources.SrmDocumentSharing_DefaultMessage_Extracting_files_from_sharing_archive__0__,
                    Path.GetFileName(SharedPath));
            }
        }

        public void Extract(IProgressMonitor progressMonitor)
        {
            ProgressMonitor = progressMonitor;
            ProgressMonitor.UpdateProgress(_progressStatus = new ProgressStatus(DefaultMessage));

            var extractDir = ExtractDir(SharedPath);

            using (ZipFile zip = ZipFile.Read(SharedPath))
            {
                CountEntries = zip.Entries.Count;
                ExpectedSize = zip.Entries.Select(entry => entry.UncompressedSize).Sum();

                zip.ExtractProgress += SrmDocumentSharing_ExtractProgress;

                string documentName = FindSharedSkylineFile(zip);

                string parentDir = Path.GetDirectoryName(SharedPath);
                if (!string.IsNullOrEmpty(parentDir))
                    extractDir = Path.Combine(parentDir, extractDir);
                extractDir = GetNonExistentDir(extractDir);
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

        public static string ExtractDir(string sharedPath)
        {
            string extractDir = Path.GetFileName(sharedPath) ?? string.Empty;
            if (PathEx.HasExtension(extractDir, EXT_SKY_ZIP))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT_SKY_ZIP.Length);
            else if (PathEx.HasExtension(extractDir, EXT))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT.Length);
            return extractDir;
        }

        private static string GetNonExistentDir(string dirPath)
        {
            int count = 1;
            string dirResult = dirPath;

            while (Directory.Exists(dirResult))
            {
                // If a directory with the given name already exists, add
                // a suffix to create a unique folder name.
                dirResult = dirPath + @"(" + count + @")";
                count++;
            }
            return dirResult;
        }

        private static string FindSharedSkylineFile(ZipFile zip)
        {
            // Review the top level entries primarily to find a single Skyline file to open
            string skylineFile = null;

            var topLevelDirectories = GetTopLevelDirectories(zip);
            var topLevelZipEntries = zip.Entries.Where(e => e != null && e.FileName.Count(ch => ch == '/') <= 2).ToArray();
            foreach (var entry in topLevelZipEntries)
            {
                var file = entry.FileName;

                if (file == null) continue; // ReSharper

                ValidateEntry(entry, topLevelDirectories, topLevelZipEntries);

                // Shared files must have exactly one Skyline Document(.sky).
                if (!file.EndsWith(SrmDocument.EXT)) continue;

                if (!string.IsNullOrEmpty(skylineFile))
                    throw new IOException(ModelResources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_contains_multiple_Skyline_documents);

                skylineFile = file;
            }

            if (string.IsNullOrEmpty(skylineFile))
            {
                throw new IOException(ModelResources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_does_not_contain_any_Skyline_documents);
            }
            return skylineFile;
        }

        /// <summary>
        /// Gets the top level folders in a .zip file when it is not a .sky.zip file.
        /// </summary>
        private static string[] GetTopLevelDirectories(ZipFile zip)
        {
            // If the archive is not a .sky.zip file (e.g. just .zip) then extra checking is
            // done to make sure the ZIP file looks like a valid shared file, which means it
            // should not contain folders that are not instrument vendor data folders.
            // (e.g. "agilentfile.d/", "watersdata.raw/", "watersdata.raw/_FUNC01.DAT, "agilentdata.d/AcqData/"")
            return !PathEx.HasExtension(zip.Name, EXT_SKY_ZIP)
                ? zip.Entries.Where(e => e is { IsDirectory: true } && e.FileName.Count(ch => ch == '/') == 1)
                    .Select(e => e.FileName).ToArray()
                : Array.Empty<string>();
        }

        private static void ValidateEntry(ZipEntry entry, string[] topLevelDirectories, ZipEntry[] topLevelZipEntries)
        {
            // Shared files should not have subfolders unless they're data sources (e.g. Bruker .d, Waters .raw).
            // But this check is not enforced for .sky.zip files, since already this level of enforcement
            // proved problematic when we added data folders to the possible contents of a .sky.zip
            // Not checking seems more future proof and it is not really a huge deal if this happens.
            // It was originally meant to keep people from trying to open just any .zip file.
            if (entry.IsDirectory && topLevelDirectories.Contains(entry.FileName))
            {
                // Mimic System.IO.DirectoryInfo - only deals with top level of directory
                var directoryName = entry.FileName;
                var files =
                    topLevelZipEntries
                        .Where(e => e.FileName.StartsWith(directoryName) && !e.IsDirectory &&
                                    e.FileName.Count(ch => ch == '/') == 1).Select(e => e.FileName.Split('/')[1]).ToArray();
                var subdirectories =
                    topLevelZipEntries
                        .Where(e => e.FileName.StartsWith(directoryName) && e.IsDirectory && !Equals(e.FileName, directoryName))
                        .Select(e => e.FileName.Split('/')[1]).ToArray();
                if (DataSourceUtil.GetSourceType(directoryName.Trim('/'), files, subdirectories) == DataSourceUtil.FOLDER_TYPE)
                {
                    throw new IOException(Resources.SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file);
                }
            }
        }

        public void Share(IProgressMonitor progressMonitor)
        {
            ProgressMonitor = progressMonitor;
            ProgressMonitor.UpdateProgress(_progressStatus = new ProgressStatus(DefaultMessage));

            using (var zip = new ZipFileShare())
            {
                try
                {
                    if (ShareType.Complete)
                        ShareComplete(zip);
                    else
                        ShareMinimal(zip);
                }
                finally
                {
                    DeleteTempDir();
                }
            }
        }

        public static bool ShouldShareAuditLog(SrmDocument doc, ShareType shareType)
        {
            return doc.Settings.DataSettings.AuditLogging &&
                   (shareType.SkylineVersion?.SrmDocumentVersion ?? doc.FormatVersion) >=
                   DocumentFormat.VERSION_4_13;
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
                zip.AddFile(transitionSettings.IonMobilityFiltering.IonMobilityLibrary.FilePath);
                
            var libFiles = new HashSet<string>();
            foreach (var librarySpec in pepSettings.Libraries.LibrarySpecs)
            {
                if (libFiles.Add(librarySpec.FilePath))
                    // Sometimes the same .blib file is referred to by different library specs
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
            if (ShareType.MustSaveNewDocument || string.IsNullOrEmpty(DocumentPath))
            {
                SaveDocToTempFile(zip);
            }
            else
            {
                AddDocumentAndAuditLog(zip, DocumentPath);
            }
            Save(zip);
        }

        public string GetDocumentFileName()
        {
            if (!string.IsNullOrEmpty(DocumentPath))
            {
                return Path.GetFileName(DocumentPath);
            }

            return Path.ChangeExtension(Path.GetFileNameWithoutExtension(SharedPath), SrmDocument.EXT);
        }

        private void ShareMinimal(ZipFileShare zip)
        {
            if (Document.Settings.HasBackgroundProteome)
            {
                // Remove any background proteome reference
                Document = Document.ChangeSettings(Document.Settings.ChangePeptideSettings(
                    set => set.ChangeBackgroundProteome(BackgroundProteome.NONE)));
            }
            if (Document.Settings.HasRTCalcPersisted)
            {
                // Minimize any persistable retention time calculator
                string tempDbPath = Document.Settings.PeptideSettings.Prediction.RetentionTime
                    .Calculator.PersistMinimized(EnsureTempDir().DirPath, Document);
                if (tempDbPath != null)
                    zip.AddFile(tempDbPath);
            }
            if (Document.Settings.HasOptimizationLibraryPersisted)
            {
                string tempDbPath = Document.Settings.TransitionSettings.Prediction.OptimizedLibrary
                    .PersistMinimized(EnsureTempDir().DirPath, Document);
                if (tempDbPath != null)
                    zip.AddFile(tempDbPath);
            }
            if (Document.Settings.HasIonMobilityLibraryPersisted)
            {
                // Minimize any persistable ion mobility predictor
                string tempDbPath = Document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary
                    .PersistMinimized(EnsureTempDir().DirPath, Document, null, out _);
                if (tempDbPath != null)
                    zip.AddFile(tempDbPath);
            }
            if (Document.Settings.HasLibraries)
            {
                // Minimize all libraries in a temporary directory, and add them
                Document = BlibDb.MinimizeLibraries(Document, EnsureTempDir().DirPath,
                    Path.GetFileNameWithoutExtension(DocumentPath),
                    null,
                    ProgressMonitor);
                if (ProgressMonitor != null && ProgressMonitor.IsCanceled)
                    return;

                foreach (var librarySpec in Document.Settings.PeptideSettings.Libraries.LibrarySpecs)
                {
                    var tempLibPath = Path.Combine(EnsureTempDir().DirPath, Path.GetFileName(librarySpec.FilePath) ?? string.Empty);
                    zip.AddFile(tempLibPath);

                    // If there is a .redundant.blib file that corresponds to a .blib file
                    // in the temp temporary directory, add that as well
                    IncludeRedundantBlib(librarySpec, zip, tempLibPath);
                }
            }

            ShareDataAndView(zip);
            SaveDocToTempFile(zip);
            Save(zip);
        }

        private void DeleteTempDir()
        {
            if (_tempDir != null)
            {
                try
                {
                    _tempDir.Dispose();
                }
                catch (IOException x)
                {
                    var message = TextUtil.LineSeparate(string.Format(
                            ModelResources.SrmDocumentSharing_ShareMinimal_Failure_removing_temporary_directory__0__,
                            _tempDir.DirPath),
                        x.Message);
                    throw new IOException(message);
                }

                _tempDir = null;
            }
        }

        public TemporaryDirectory EnsureTempDir()
        {
            if (_tempDir == null)
            {
                _tempDir = new TemporaryDirectory();
            }

            return _tempDir;
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
            ShareSkydFile(zip);
            if (null != ViewFilePath)
            {
                zip.AddFile(ViewFilePath);
            }

            // If user selected any raw data files for inclusion, add those now
            if (ShareType.AuxiliaryFiles != null)
            {
                foreach (var path in ShareType.AuxiliaryFiles)
                {
                    zip.AddFile(path);
                }
            }
        }

        private void SaveDocToTempFile(ZipFileShare zip)
        {
            string entryName = GetDocumentFileName();
            string docFilePath = Path.Combine(EnsureTempDir().DirPath, entryName);
            Document.SerializeToFile(docFilePath, docFilePath, ShareType.SkylineVersion ?? SkylineVersion.CURRENT, ProgressMonitor);
            AddDocumentAndAuditLog(zip, docFilePath);
        }

        private void AddDocumentAndAuditLog(ZipFileShare zip, string docFilePath)
        {
            zip.AddFile(docFilePath);
            var auditLogPath = SrmDocument.GetAuditLogPath(docFilePath);
            if (File.Exists(auditLogPath))
            {
                zip.AddFile(auditLogPath);
            }
        }

        private void ShareSkydFile(ZipFileShare zip)
        {
            if (DocumentPath == null)
            {
                return;
            }
            string pathCache = ChromatogramCache.FinalPathForName(DocumentPath, null);
            if (!File.Exists(pathCache))
            {
                return;
            }
            if (ShareType.SkylineVersion != null && Document.Settings.HasResults)
            {
                var measuredResults = Document.Settings.MeasuredResults;
                if (measuredResults.CacheVersion.HasValue &&
                    !measuredResults.CacheVersion.Equals(ShareType.SkylineVersion.CacheFormatVersion))
                {
                    String cacheFileName = Path.GetFileName(pathCache);
                    if (cacheFileName != null)
                    {
                        String newCachePath = Path.Combine(EnsureTempDir().DirPath, cacheFileName);
                        MinimizeToFile(newCachePath, CacheFormat.FromVersion(ShareType.SkylineVersion.CacheFormatVersion));
                        zip.AddFile(newCachePath);
                        return;
                    }
                }
            }
            zip.AddFile(pathCache);
        }

        public void MinimizeToFile(string targetFile, CacheFormat cacheFormat)
        {
            var targetSkydFile = ChromatogramCache.FinalPathForName(targetFile, null);
            using (var skydSaver = new FileSaver(targetSkydFile, true))
            using (var scansSaver = new FileSaver(targetSkydFile + ChromatogramCache.SCANS_EXT, true))
            using (var peaksSaver = new FileSaver(targetSkydFile + ChromatogramCache.PEAKS_EXT, true))
            using (var scoreSaver = new FileSaver(targetSkydFile + ChromatogramCache.SCORES_EXT, true))
            {
                var minimizer = Document.Settings.MeasuredResults.GetChromCacheMinimizer(Document);
                var settings = new ChromCacheMinimizer.Settings().ChangeCacheFormat(cacheFormat);
                var lockObject = new object();
                ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangeMessage(ModelResources.SrmDocumentSharing_MinimizeToFile_Writing_chromatograms));
                minimizer.Minimize(settings, stats =>
                {
                    if (ProgressMonitor.IsCanceled)
                    {
                        throw new OperationCanceledException();
                    }
                    lock (lockObject)
                    {
                        ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(stats.PercentComplete));
                    }
                    
                }, skydSaver.FileStream, scansSaver.FileStream, peaksSaver.FileStream, scoreSaver.FileStream);
                skydSaver.Commit();
            }
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

                if (e.CurrentEntry != null && !Equals(CurrentEntry, e.CurrentEntry.FileName))
                {
                    if (CurrentEntry != null)
                        EntriesSaved++;
                    CurrentEntry = e.CurrentEntry.FileName;
                }

                // Consider: More accurate total byte progress
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
                        var message = string.Format(
                            ModelResources.SrmDocumentSharing_SrmDocumentSharing_SaveProgress_Compressing__0__,
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
                _zip = new ZipFile (Encoding.UTF8) { UseZip64WhenSaving = Zip64Option.AsNecessary };

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
                        throw new IOException(TextUtil.LineSeparate(string.Format(ModelResources.ZipFileShare_AddFile_Failed_attempting_to_add_the_file__0_, path),
                            string.Format(ModelResources.ZipFileShare_AddFile_The_name___0___is_already_in_use_from_the_path__1_, fileName, existingPath)));
                    }

                    // No need to add exactly the same path twice
                    return;
                }
                _dictNameToPath.Add(fileName, path);
                if (Directory.Exists(path)) // Some mass spec data "files" are really directories
                {
                    _zip.AddDirectory(path, Path.GetFileName(path));
                }
                else
                {
                    _zip.AddFile(path, string.Empty);
                }
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

    public class ShareType : Immutable
    {
        public static readonly ShareType COMPLETE = new ShareType(true, null);
        public static readonly ShareType MINIMAL = new ShareType(false, null);
        public static readonly ShareType DEFAULT = COMPLETE;
        public ShareType(bool complete, SkylineVersion skylineVersion, IEnumerable<string> auxiliaryFiles = null)
        {
            Complete = complete;
            SkylineVersion = skylineVersion;
            AuxiliaryFiles = auxiliaryFiles;
        }
        public bool Complete { get; private set; }

        public ShareType ChangeComplete(bool complete)
        {
            return ChangeProp(ImClone(this), im=>im.Complete = complete);
        }
        public SkylineVersion SkylineVersion { get; private set; }

        public IEnumerable<string> AuxiliaryFiles { get; private set; } // Usually mass spec data files

        public ShareType ChangeSkylineVersion(SkylineVersion skylineVersion)
        {
            return ChangeProp(ImClone(this), im => im.SkylineVersion = skylineVersion);
        }

        public bool MustSaveNewDocument
        {
            get { return SkylineVersion != null; }
        }

        protected bool Equals(ShareType other)
        {
            return Complete == other.Complete && Equals(SkylineVersion, other.SkylineVersion);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ShareType) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Complete.GetHashCode()*397) ^ (SkylineVersion != null ? SkylineVersion.GetHashCode() : 0);
            }
        }
    }
}
