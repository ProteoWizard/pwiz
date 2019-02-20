using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Runtime.InteropServices;

namespace AutoQC
{
    class NetworkDriveUtil
    {
        private static readonly object LOCK = new object();

        public static bool EnsureDrive(DriveInfo driveInfo, IAutoQcLogger logger, out bool reconnected, string configName)
        {
            // Do we need a lock here? Don't want two configurations, on the same mapped (disconnected) drive, 
            // to try to re-map the drive.
            lock (LOCK)
            {
                // Program.LogInfo(string.Format("Checking drive {0} for config {1}", driveInfo, configName)); // TODO - debug

                if (IsDriveAvailable(driveInfo, configName))
                {
                    reconnected = false;
                    return true; // Drive root already exists.
                }

                if (!driveInfo.IsMappedNetworkDrive())
                {
                    Program.LogInfo(string.Format("Unable to connect to drive {0}. Config: {1}", driveInfo.DriveLetter, configName));
                    reconnected = false;
                    return false;
                }

                // TODO: Do we need to unmount first? 

                if (driveInfo.IsMappedNetworkDrive())
                {
                    logger.LogProgramError(string.Format(
                        "Lost connection to network drive. Attempting to reconnect to {0}. Config: {1}", driveInfo,
                        configName));
                    // Attempt to reconnect to a mapped network drive
                    var process = Process.Start("net.exe",
                        @"USE " + driveInfo.DriveLetter + " " + driveInfo.NetworkPath);
                    if (process != null)
                    {
                        process.WaitForExit();
                    }

                    Program.LogInfo(string.Format("After attempting to reconnect.. {0}. Config: {1}", driveInfo.DriveLetter, configName));

                    if(IsDriveAvailable(driveInfo, configName))
                    {
                        reconnected = true;
                        logger.Log(
                            string.Format(
                                "Network drive was temporarily disconnected. Successfully remapped network drive {0}. Config: {1}",
                                driveInfo, configName));
                        return true;
                    }
                }

                Program.LogInfo(string.Format("Unable to connect to network drive {0}. Config: {1}", driveInfo.DriveLetter, configName));
                reconnected = false;
                return false;
            }
        }

        private static bool IsDriveAvailable(DriveInfo driveInfo, string configName)
        {
            if (!driveInfo.IsMappedNetworkDrive())
            {
                return true;
            }
            
            var drives = System.IO.DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.Name.StartsWith(driveInfo.DriveLetter) && drive.IsReady)
                {
                    // Program.LogInfo(string.Format("Network Drive is mapped {0}. Config: {1}", driveInfo.DriveLetter, configName)); // TODO - remove
                    return true;
                }
            }

            return false;
        }

        public static string GetNetworkPath(string driveLetter)
        {
            // First determine if this is a network drive
            // https://stackoverflow.com/questions/4396634/how-can-i-determine-if-a-given-drive-letter-is-a-local-mapped-or-usb-drive
            var drives = System.IO.DriveInfo.GetDrives();
            if (drives.Any(drive => drive.Name.StartsWith(driveLetter) && drive.DriveType == DriveType.Network))
            {
                return GetUncPath(driveLetter);
            }
            return null;
        }

        

        private static string GetUncPath(string driveLetter)
        {
            string uncPath = GetUncPath1(driveLetter);
            if (uncPath == null)
            {
                uncPath = GetUncPath2(driveLetter);
            }

            return uncPath;
        }

        // https://stackoverflow.com/questions/2067075/how-do-i-determine-a-mapped-drives-actual-path (answer by Vermis)
        private static string GetUncPath1(string driveLetter)
        {
            try
            {
                // Query WMI if the drive letter is a network drive
                using (ManagementObject mo = new ManagementObject())
                {
                    mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", driveLetter));
                    return Convert.ToString(mo["ProviderName"]);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // https://stackoverflow.com/questions/2067075/how-do-i-determine-a-mapped-drives-actual-path 
        // https://ehikioya.com/get-network-path-mapped-drive/
        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WNetGetConnection(
            [MarshalAs(UnmanagedType.LPTStr)] string localName,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder remoteName,
            ref int length);
        private static string GetUncPath2(string driveLetter)
        {
            var c = driveLetter[0];
            if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')
            {
                var sb = new StringBuilder(512);
                var size = sb.Capacity;
                var error = WNetGetConnection(driveLetter, sb, ref size);
                if (error != 0)
                {
                    throw new FileWatcherException(string.Format("WNetGetConnection failed to get UNC path for {0}. Error {1}", driveLetter, error));  
                }
                return sb.ToString();
            }
            throw new FileWatcherException(string.Format("Failed to get UNC path for {0}.", driveLetter));
        }



        public static string GetDriveLetter(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length < 2 || path[1] != Path.VolumeSeparatorChar
                || !Path.IsPathRooted(path))
            {
                return null;
            }

            return Directory.GetDirectoryRoot(path).Replace(Path.DirectorySeparatorChar.ToString(), "");
        }
    }
}
