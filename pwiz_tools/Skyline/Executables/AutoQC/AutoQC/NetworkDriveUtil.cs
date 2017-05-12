using System;
using System.Diagnostics;
using System.IO;
using System.Management;

namespace AutoQC
{
    class NetworkDriveUtil
    {
        private readonly AutoQCFileSystemWatcher _fileSystemWatcher;
        private readonly IAutoQcLogger _logger;
        private string _networkDriveLetter;
        private string _networkDrivePath ;
        private DateTime? _timeDisconnected;

        public NetworkDriveUtil(AutoQCFileSystemWatcher fileSystemWatcher, IAutoQcLogger logger)
        {
            _fileSystemWatcher = fileSystemWatcher;
            _logger = logger;
        }

        public bool EnsureDrive(string path)
        {
            if (Directory.Exists(path))
            {
                if (_timeDisconnected != null)
                {
                    // Network drive was disconnected last time we were here, and we couldn't re-map it successfully
                    _fileSystemWatcher.Restart((DateTime) _timeDisconnected);   
                }
                _timeDisconnected = null;
                ReadNetworkDriveProperties(path);
                return true;
            }

            _timeDisconnected = _timeDisconnected ?? DateTime.Now.AddMilliseconds(-(ConfigRunner.WAIT_FOR_NEW_FILE));

            _fileSystemWatcher.Pause();
            if (!string.IsNullOrWhiteSpace(_networkDrivePath))
            {
                _logger.LogProgramError("Attempting to reconnect to mapped network drive " + path);
                // Attempt to reconnect to a mapped network drive
                var process = Process.Start("net.exe", @"USE " + _networkDriveLetter + " " + _networkDrivePath);
                if (process != null)
                {
                    process.WaitForExit();
                }
            }
            if (Directory.Exists(path))
            {
                _logger.LogError("Reconnected to mapped network drive " + path);
                _fileSystemWatcher.Restart((DateTime) _timeDisconnected);
                _timeDisconnected = null;
                return true;
            }

            Program.LogError(
                string.Format(
                    "Unable to reconnect to network drive. Network deive letter: {0}. Network drive path: {1}",
                    _networkDriveLetter, _networkDrivePath));
            return false;
        }

        private void ReadNetworkDriveProperties(string path)
        {
            if (!string.IsNullOrEmpty(_networkDriveLetter) && !(string.IsNullOrEmpty(_networkDrivePath)))
            {
                return;
            }

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
