using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace AutoQC
{
    class NetworkDriveUtil
    {
        
        public static void EnsureDrive(DriveInfo driveInfo, IAutoQcLogger logger, out bool reconnected)
        {
            if (Directory.Exists(driveInfo.DriveLetter + Path.DirectorySeparatorChar))
            {
                reconnected = false;
                return; // Drive root already exists.
            }

            // TODO: Do we need to unmount first? 

            if (driveInfo.IsNetworkDrive())
            {
                logger.LogProgramError(string.Format("Lost connection to network drive. Attempting to reconnect to {0}.", driveInfo.DriveLetter));
                // Attempt to reconnect to a mapped network drive
                var process = Process.Start("net.exe", @"USE " + driveInfo.DriveLetter + " " + driveInfo.NetworkDrivePath);
                if (process != null)
                {
                    process.WaitForExit();
                }
            }
            if (Directory.Exists(driveInfo.DriveLetter + Path.DirectorySeparatorChar))
            {
                reconnected = true;
                logger.Log(
                    string.Format("Network drive was temporarily disconnected. Successfully remapped network drive {0}.", driveInfo.DriveLetter));
                return;
            }

            throw new FileWatcherException(string.Format("Unable to re-connect to drive {0}", driveInfo.DriveLetter));
        }

        public static string ReadNetworkDrivePath(string driveLetter)
        {
            if (driveLetter != null)
            {
                // Query WMI if the drive letter is a network drive
                using (ManagementObject mo = new ManagementObject())
                {
                    mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", driveLetter));
                    var driveType = (DriveType)((uint)mo["DriveType"]);
                    if (driveType == DriveType.Network)
                    {
                        return Convert.ToString(mo["ProviderName"]);
                    }
                }
            }
            return null;
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
