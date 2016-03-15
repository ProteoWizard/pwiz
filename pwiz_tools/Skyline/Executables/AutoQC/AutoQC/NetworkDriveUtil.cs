using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace AutoQC
{
    class NetworkDriveUtil
    {
        private readonly AutoQCFileSystemWatcher _fileSystemWatcher;
        private readonly IAutoQCLogger _logger;
        private string _networkDriveLetter;
        private string _networkDrivePath ;

        public NetworkDriveUtil(AutoQCFileSystemWatcher fileSystemWatcher, IAutoQCLogger logger)
        {
            _fileSystemWatcher = fileSystemWatcher;
            _logger = logger;
        }

        public void EnsureDrive(string path)
        {
            if (Directory.Exists(path))
            {
                ReadNetworkDriveProperties(path);
                return;
            }

            var timeDisconnected = DateTime.Now.AddMilliseconds(-(AutoQCBackgroundWorker.WAIT_FOR_NEW_FILE));

            _fileSystemWatcher.Pause();
            if (!string.IsNullOrWhiteSpace(_networkDrivePath))
            {
                _logger.Log("Waiting to reconnect to mapped network drive " + path);
                // Attempt to reconnect to a mapped network drive
                var process = Process.Start("net.exe", @"USE " + _networkDriveLetter + " " + _networkDrivePath);
                if (process != null)
                {
                    process.WaitForExit();
                }
            }
            _fileSystemWatcher.Restart(timeDisconnected);
        }

        private void ReadNetworkDriveProperties(string path)
        {
            var driveLetter = GetDriveLetter(path);

            if (driveLetter != null)
            {
                // Query WMI if the drive letter is a network drive
                using (ManagementObject mo = new ManagementObject())
                {
                    mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", driveLetter));
                    var driveType = (DriveType)((uint)mo["DriveType"]);
                    if (driveType == DriveType.Network)
                    {
                        _networkDrivePath = Convert.ToString(mo["ProviderName"]);
                        _networkDriveLetter = driveLetter;
                    }
                }
            }
        }

        private static string GetDriveLetter(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!Path.IsPathRooted(path))
            {
                return null;
            }

            return Directory.GetDirectoryRoot(path).Replace(Path.DirectorySeparatorChar.ToString(), "");
        }
    }
}
