using System;
using System.Diagnostics;
using System.IO;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace AutoQC
{
    internal class StartupManager
    {
        private const string AUTOQCSTARTER = Program.AutoQcStarter;
        private static readonly string AUTOQCSTARTEREXE = Program.AutoQcStarterExe;

        public static void EnableKeepRunning()
        {
            EnableKeepRunning(false);
        }
        public static void UpdateAutoQcStarterInStartup()
        {
            EnableKeepRunning(true);
        }

        private static void EnableKeepRunning(bool updateShortcut)
        {
            if (updateShortcut)
            {
                DisableKeepRunning();
            }

            ShortCutInfo shortcut;
            try
            {
                shortcut = GetShortcut();
            }
            catch (Exception e)
            {
                throw new StartupManagerException($"Unable to get shortcut path for {AUTOQCSTARTEREXE}", e);
            }

            if (!File.Exists(shortcut.TargetPath))
            {
                throw new StartupManagerException($"Target path for shortcut does not exist: {shortcut.TargetPath}.");
            }

            if (updateShortcut || !File.Exists(shortcut.ShortcutPath))
            {
                try
                {
                    SaveAutoQcStarterInStartup(shortcut, updateShortcut);
                }
                catch(Exception e)
                {
                    throw new StartupManagerException($"Unable to create a shortcut for {AUTOQCSTARTEREXE}", e);
                }
            }

            try
            {
                StartAutoQcStarterIfNotRunning(shortcut.ShortcutPath);
            }
            catch (Exception e)
            {
                try
                {
                    RemoveAutoQcStarterFromStartup(); // Remove the shortcut
                }
                catch (Exception ex)
                {
                    Program.LogError($"Error removing {AUTOQCSTARTEREXE} shortcut from the Startup folder.", ex);
                }
                throw new StartupManagerException($"Unable to start {AUTOQCSTARTEREXE}. Error was: {e.Message}", e);
            }
        }

        public static void DisableKeepRunning()
        {
            try
            {
                StopAutoQcStarter();
            }
            catch (Exception e)
            {
                throw new StartupManagerException($"Could not stop {AUTOQCSTARTER}", e);
            }

            try
            {
                RemoveAutoQcStarterFromStartup(); // Remove the shortcut
            }
            catch (Exception e)
            {
                throw new StartupManagerException($"Could not remove {AUTOQCSTARTEREXE} shortcut from the Startup folder", e);
            }
        }

        public static bool IsAutoQcStarterRunning()
        {
            return GetAutoQcStarterProcesses().Length > 0;
        }

        private static Process[] GetAutoQcStarterProcesses()
        {
            var procs = Process.GetProcessesByName(AUTOQCSTARTER);
            Program.LogInfo($"Found {procs.Length} {AUTOQCSTARTER} {(procs.Length > 1 ? "processes" : "process")}.");
            return procs;
        }

        private static void SaveAutoQcStarterInStartup(ShortCutInfo shortcutInfo, bool overwrite = false)
        {
            if (overwrite && File.Exists(shortcutInfo.ShortcutPath))
            {
                Program.LogInfo($"Deleting old shortcut {shortcutInfo.ShortcutPath}");
                File.Delete(shortcutInfo.ShortcutPath);
                
                if (File.Exists(shortcutInfo.ShortcutPath))
                {
                    Program.LogError($"Could not delete {shortcutInfo.ShortcutPath}");
                }
            }
            
            if (!File.Exists(shortcutInfo.ShortcutPath))
            {
                Program.LogInfo($"Adding {AUTOQCSTARTEREXE} shortcut to Startup folder.");

                // http://softvernow.com/2018/07/30/create-shortcut-using-c/
                WshShell wsh = new WshShell();
                var shortcut =
                    wsh.CreateShortcut(shortcutInfo.ShortcutPath) as IWshShortcut;
             
                if (shortcut != null)
                {
                    shortcut.Description = $"Shortcut to {AUTOQCSTARTEREXE}";
                    shortcut.TargetPath = shortcutInfo.TargetPath;
                    shortcut.Save();
                }
                else
                {
                    Program.LogError($"Could not create a shortcut to {AUTOQCSTARTEREXE}.");
                }
            }
            else
            {
                Program.LogInfo($"Shortcut to {AUTOQCSTARTEREXE} already exists in the Startup folder.");
            }
        }

        private static ShortCutInfo GetShortcut()
        {
            var exeLocation = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            if (exeLocation.StartsWith(@"file:"))
            {
                exeLocation = exeLocation.Substring(5);
            }
            while (exeLocation.StartsWith(@"/"))
            {
                exeLocation = exeLocation.Substring(1);
            }
           
            var exeDirInfo = Directory.GetParent(exeLocation);
            var targetPath = Path.Combine(exeDirInfo.FullName, $"{AUTOQCSTARTEREXE}");
            return new ShortCutInfo(GetShortcutPath(), targetPath);
        }

        private static string GetShortcutPath()
        {
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startupDir, $"{AUTOQCSTARTER}.lnk");
        }

        private static void RemoveAutoQcStarterFromStartup()
        {
            Program.LogInfo($"Removing {AUTOQCSTARTER} shortcut from Startup folder");
            var shortcutPath = GetShortcutPath();
            Program.LogInfo($"Shortcut path is {shortcutPath}");
            
            //Remove the shortcut
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                Program.LogInfo($"Shortcut removed: {shortcutPath}");
            }
            else
            {
                Program.LogInfo($"Shortcut {shortcutPath} does not exist in Startup folder.");
            }
        }

        private static void StopAutoQcStarter()
        { 
            // Stop AutoQCStarter if it is running
            var procs = GetAutoQcStarterProcesses();

            if (procs.Length > 0)
            {
                Program.LogInfo($"Stopping {AUTOQCSTARTER}");
                foreach (var process in procs)
                {
                    process.Kill();
                }
            }
        }
        
        private static void StartAutoQcStarterIfNotRunning(string shortcutPath)
        {
            var procs = GetAutoQcStarterProcesses();
            if (procs.Length == 0)
            {
                StartAutoQcStarter(shortcutPath);
            }
            else
            {
                Program.LogInfo($"{AUTOQCSTARTER} is already running");
            }
        }

        private static void StartAutoQcStarter(string shortcutPath)
        {
            Program.LogInfo($"Starting {AUTOQCSTARTER} at {shortcutPath}");
            var procInfo = new ProcessStartInfo
            {
                UseShellExecute =
                    true, // Set to true otherwise there is an exception: "The specified executable is not a valid application for this OS platform".
                FileName = shortcutPath,
                CreateNoWindow = true
            };

            using (Process.Start(procInfo))
            {
                Program.LogInfo($"Started {AUTOQCSTARTER}.");
            }
        }     
    }

    public class ShortCutInfo
    {
        public string ShortcutPath { get; }
        public string TargetPath { get; }

        public ShortCutInfo(string shortcutPath, string targetPath)
        {
            ShortcutPath = shortcutPath;
            TargetPath = targetPath;
        }
    }

    public class StartupManagerException : SystemException
    {
        public StartupManagerException(string message) : base(message)
        {
        }
        public StartupManagerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
