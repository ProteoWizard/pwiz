using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SharedBatch.Properties;

namespace SharedBatch
{
    public class FileUtil
    {

        public const string DOWNLOADS_FOLDER = "\\Downloads";
        public const int ONE_GB = 1000000000;


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

        public static long GetTotalFreeSpace(string driveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name == driveName)
                {
                    return drive.TotalFreeSpace;
                }
            }
            return -1;
        }

    }
}
