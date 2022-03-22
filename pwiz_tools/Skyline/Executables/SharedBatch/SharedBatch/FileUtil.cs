using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Ionic.Zip;
using Microsoft.Win32;
using SharedBatch.Properties;

namespace SharedBatch
{
    public class FileUtil
    {

        public const string DOWNLOADS_FOLDER = "\\Downloads";
        public const long ONE_GB = 1000000000;


        public static void ValidateNotEmptyPath(string input, string name)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException(string.Format("Please specify a path to {0}", name));
            }
        }

        public static void ValidateNotInDownloads(string input, string name)
        {
            if (input.Contains(DOWNLOADS_FOLDER))
            {
                throw new ArgumentException(string.Format("The {0} cannot be in the \"Downloads\" folder. Please specify another location on your computer.", name));
            }
        }

        public static string GetNextFolder(string root, string path)
        {
            if (root.EndsWith("\\")) root = root.Substring(0, root.Length - 1);
            if (path.EndsWith("\\")) path = path.Substring(0, path.Length - 1);
            var rootFolders = root.Split('\\');
            var pathFolders = path.Split('\\');
            int i = 0;
            while (i < rootFolders.Length && i < pathFolders.Length)
            {
                if (rootFolders[i] != pathFolders[i])
                    throw new Exception("Paths do not match");
                i++;
            }
            return pathFolders[i];
        }

        // Extension of Path.GetDirectoryName that handles null file paths
        public static string GetDirectory(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path), Resources.TextUtil_GetDirectory_Could_not_get_the_directory_of_a_null_file_path_);
            return Path.GetDirectoryName(path);
        }

        // Extension of Path.GetDirectoryName that handles null file paths and returns an empty string if the directory cannot be found
        public static string GetDirectorySafe(string path)
        {
            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static bool DirectoryExists(string path)
        {
            var exists = false;
            try
            {
                exists = Directory.Exists(Path.GetDirectoryName(path));
            }
            catch (Exception)
            {
                // pass incorrectly formatted paths
            }
            return exists;
        }

        public static bool PathHasDriveName(string path)
        {
            var driveRegex = new Regex(@"^[A-Z]|[a-z]:\\");
            return driveRegex.IsMatch(path);
        }

        // Find an existing initial directory to use in a file/folder browser dialog, can be null (dialog will use a default)
        public static string GetInitialDirectory(string directory, string lastEnteredPath = "")
        {
            if (Directory.Exists(directory))
                return directory;

            string directoryName;
            try
            {
                directoryName = Path.GetDirectoryName(directory);
            }
            catch (Exception)
            {
                directoryName = null;
            }
            if (directoryName == null)
            {
                if (!string.IsNullOrEmpty(lastEnteredPath))
                    return GetInitialDirectory(lastEnteredPath);
                return null;
            }
            return GetInitialDirectory(directoryName);
        }

        // Returns the directory of the path regardless of whether it exists.
        // If the path is already a directory it returns the path, if it does not have a directory it returns an empty string
        public static string GetPathDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if (!Path.HasExtension(path)) return path;
            try
            {
                return Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static void CreateDirectory(string path)
        {
            string directory = null;
            try
            {
                directory = GetDirectory(path);
            }
            catch (Exception)
            {
                // pass
            }
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        public static string ForceReplaceRoot(string oldText, string newText, string originalString)
        {
            if (!originalString.StartsWith(oldText))
                throw new ArgumentException(string.Format("The path to be replaced {0} did not start with the expected root {1}", originalString, oldText));
            if (!Directory.Exists(newText))
                throw new ArgumentException(string.Format("The folder {0} does not exist.", newText));

            var newPath = newText + originalString.Substring(oldText.Length);
            CreateDirectory(newPath);
            return newPath;
        }

        public static List<string> GetFilesInFolder(string folder, string fileType)
        {
            var filesWithType = new List<string>();
            var allFiles = new DirectoryInfo(folder).GetFiles();
            foreach (var file in allFiles)
            {
                if (file.Name.EndsWith(fileType))
                    filesWithType.Add(file.FullName);
            }

            return filesWithType;
        }

        public static string GetSafeName(string name)
        {
            var invalidChars = new List<char>();
            invalidChars.AddRange(Path.GetInvalidFileNameChars());
            invalidChars.AddRange(Path.GetInvalidPathChars());
            var safeName = string.Join("_", name.Split(invalidChars.ToArray()));
            return safeName; // .TrimStart('.').TrimEnd('.');
        }

        public static string GetSafeNameForDir(string name)
        {
            // Trailing periods and spaces are ignored when creating a directory
            return GetSafeName(name).TrimStart('.', ' ').TrimEnd('.', ' ');
        }

        public static void AddFileTypeClickOnce(string extension, string id, string description, string applicationReference, string iconPath)
        {
            // Register ClickOnce exe/icon/description associations.
            var launchExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaunchBatch.exe");
            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}\shell\open\command", null,
                $"\"{launchExe}\" \"{applicationReference}\" \"\\\"%1\\\"\"");
            AddFileType(extension, id, description, iconPath);
        }

        public static void AddFileTypeAdminInstall(string extension, string id, string description, string applicationExe, string iconPath)
        {
            // Register admin installation exe/icon/description associations.

            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}\shell\open\command", null,
                $"\"{applicationExe}\" \"%1\"");
            AddFileType(extension, id, description, iconPath);
        }

        private static void AddFileType(string extension, string id, string description, string iconPath)
        {
            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}", null, description);

            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}\DefaultIcon", null,
                $"\"{iconPath}\"");

            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{extension}", null, id);
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static long? SimulatedDriveSpace { get; set; }
        public static long GetTotalFreeSpace(string driveName)
        {
            if (SimulatedDriveSpace.HasValue)
                return SimulatedDriveSpace.Value;

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name == driveName)
                {
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
        }

        /// <summary>
        /// Determines whether the file name in the given path has a
        /// specific extension.  Because this is Windows, the comparison
        /// is case insensitive.  Note that ToLowerInvariant() is used to make
        /// sure it works for Turkish with extensions containing the letter i
        /// (e.g. .wiff).  This does, however, make this function only work
        /// for extenstions with only lower ASCII characters, which all of
        /// ours are.
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="ext">Extension to check for (only lower ASCII)</param>
        /// <returns>True if the path has the extension</returns>
        public static bool HasExtension(string path, string ext)
        {
            return path.ToLowerInvariant().EndsWith(ext.ToLowerInvariant());
        }


        public static string ExtractDir(string sharedPath)
        {
            string extractDir = Path.GetFileName(sharedPath) ?? string.Empty;
            if (HasExtension(extractDir, TextUtil.EXT_SKY_ZIP))
                extractDir = extractDir.Substring(0, extractDir.Length - TextUtil.EXT_SKY_ZIP.Length);
            else if (HasExtension(extractDir, TextUtil.EXT_SKYP))
                extractDir = extractDir.Substring(0, extractDir.Length - TextUtil.EXT_SKYP.Length);
            return extractDir;
        }

    }



    public class SrmDocumentSharing
    {
        public const string EXT = ".zip";
        public const string EXT_SKY_ZIP = ".sky.zip";
        public static string FILTER_SHARING
        {
            get { return TextUtil.FileDialogFilter("SrmDocumentSharing_FILTER_SHARING_Shared_Files", EXT); }
        }

        public SrmDocumentSharing(string sharedPath)
        {
            SharedPath = sharedPath;
            ShareType = ShareType.DEFAULT;
        }

        private Update _progressHandler;

        private CancellationToken _cancellationToken;
        public string DocumentPath { get; private set; }
        public string ViewFilePath { get; set; }

        public string SharedPath { get; private set; }

        public ShareType ShareType { get; set; }
        //private IProgressMonitor ProgressMonitor { get; set; }
        //private IProgressStatus _progressStatus;
        private long ExpectedSize { get; set; }
        private long ExtractedSize { get; set; }


        public string Extract(Update progressHandler, CancellationToken token)
        {
            _progressHandler = progressHandler;
            _cancellationToken = token;

            var extractDir = Path.GetDirectoryName(SharedPath) ?? string.Empty;

            using (ZipFile zip = ZipFile.Read(SharedPath))
            {
                ExpectedSize = zip.Entries.Select(entry => entry.UncompressedSize).Sum();

                zip.ExtractProgress += SrmDocumentSharing_ExtractProgress;

                string documentName = FindSharedSkylineFile(zip);

                DocumentPath = Path.Combine(extractDir, documentName);

                foreach (var entry in zip.Entries)
                {
                    if (_cancellationToken.IsCancellationRequested)
                        break;
                    var filePath = Path.Combine(extractDir, entry.FileName);
                    if (File.Exists(filePath))
                    {
                        if (Math.Abs(entry.UncompressedSize - new FileInfo(filePath).Length) < 0.0001)
                            continue;
                        File.Delete(Path.Combine(extractDir, entry.FileName));
                    }
                    try
                    {
                        entry.Extract(extractDir);

                        ExtractedSize += entry.UncompressedSize;
                    }
                    catch (Exception)
                    {
                        if (!_cancellationToken.IsCancellationRequested)
                            throw;
                    }
                }
                if (_cancellationToken.IsCancellationRequested)
                {
                    foreach (var entry in zip.Entries)
                    {
                        var file = Path.Combine(extractDir, entry.FileName);
                        if (File.Exists(file)) File.Delete(file);
                    }
                }
            }

            return DocumentPath;
        }

        public static string ExtractDir(string sharedPath)
        {
            string extractDir = Path.GetFileName(sharedPath) ?? string.Empty;
            if (FileUtil.HasExtension(extractDir, EXT_SKY_ZIP))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT_SKY_ZIP.Length);
            else if (FileUtil.HasExtension(extractDir, EXT))
                extractDir = extractDir.Substring(0, extractDir.Length - EXT.Length);
            return extractDir;
        }

        private static string FindSharedSkylineFile(ZipFile zip)
        {
            string skylineFile = null;

            foreach (var file in zip.EntryFileNames)
            {
                if (file == null) continue; // ReSharper

                // Shared files should not have subfolders.
                if (Path.GetFileName(file) != file)
                    throw new IOException("SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file");

                // Shared files must have exactly one Skyline Document(.sky).
                if (!file.EndsWith(TextUtil.EXT_SKY)) continue;

                if (!string.IsNullOrEmpty(skylineFile))
                    throw new IOException("SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_contains_multiple_Skyline_documents");

                skylineFile = file;
            }

            if (string.IsNullOrEmpty(skylineFile))
            {
                throw new IOException("SrmDocumentSharing_FindSharedSkylineFile_The_zip_file_is_not_a_shared_file_The_file_does_not_contain_any_Skyline_documents");
            }
            return skylineFile;
        }


        private void SrmDocumentSharing_ExtractProgress(object sender, ExtractProgressEventArgs e)
        {
            if (_progressHandler != null)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    e.Cancel = true;
                    return;
                }

                int progressValue = (int)Math.Round((ExtractedSize + e.BytesTransferred) * 100.0 / ExpectedSize);

                _progressHandler(progressValue, null);
            }
        }


        #region Functional testing support

        public IEnumerable<string> ListEntries()
        {
            _progressHandler = null;
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

    public class ShareType //: Immutable
    {
        public static readonly ShareType COMPLETE = new ShareType(true);
        public static readonly ShareType MINIMAL = new ShareType(false);
        public static readonly ShareType DEFAULT = COMPLETE;
        public ShareType(bool complete)
        {
            Complete = complete;
            //SkylineVersion = skylineVersion;
        }
        public bool Complete { get; private set; }

        public ShareType ChangeComplete(bool complete)
        {
            return new ShareType(complete);
        }

        protected bool Equals(ShareType other)
        {
            return Complete == other.Complete;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ShareType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Complete.GetHashCode() * 397);
            }
        }
    }

    public class TemporaryDirectory : IDisposable
    {
        public const string TEMP_PREFIX = "~SK";

        public TemporaryDirectory(string dirPath = null, string tempPrefix = TEMP_PREFIX)
        {
            if (string.IsNullOrEmpty(dirPath))
                DirPath = Path.Combine(Path.GetTempPath(), tempPrefix + Path.GetRandomFileName());
            else
                DirPath = dirPath;
            Directory.CreateDirectory(DirPath);
        }

        public string DirPath { get; private set; }

        public void Dispose()
        {
            Directory.Delete(DirPath);
        }
    }

    public class UiFileUtil
    {

        public static string OpenFile(string initialDirectory, string filter, bool saveFileDialog)
        {
            FileDialog dialog = saveFileDialog ? (FileDialog)new SaveFileDialog() : new OpenFileDialog();
            dialog.InitialDirectory = initialDirectory;
            dialog.Filter = filter;
            DialogResult result = dialog.ShowDialog();
            return result == DialogResult.OK ? dialog.FileName : null;
        }

        public static string OpenFolder(string initialPath)
        {
            var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = initialPath;
            var dialogOpenedTime = DateTime.Now;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var folderDirectory = Path.GetDirectoryName(dialog.SelectedPath);
                if (!Directory.Exists(dialog.SelectedPath) && folderDirectory != null)
                {
                    var folders = Directory.GetDirectories(folderDirectory);
                    var lastCreatedFolder = folders[0];
                    var lastCreationTime = Directory.GetCreationTime(lastCreatedFolder);
                    foreach (var folder in folders)
                    {
                        if (Directory.GetCreationTime(folder) > lastCreationTime)
                        {
                            lastCreatedFolder = folder;
                            lastCreationTime = Directory.GetCreationTime(folder);
                        }
                    }
                    if (lastCreationTime > dialogOpenedTime)
                        dialog.SelectedPath = lastCreatedFolder;
                }
                return dialog.SelectedPath;
            }
            return null;
        }
    }

}
