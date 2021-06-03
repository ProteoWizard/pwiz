using System;
using System.Diagnostics;
using System.IO;
using IWshRuntimeLibrary;
using SharedBatch;
using File = System.IO.File;

namespace AutoQC
{
    internal class StartupManager
    {
        private const string AUTOQCSTARTER = Program.AUTO_QC_STARTER;
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
            catch (StartupManagerException)
            {
                throw;
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
                    ProgramLog.Error($"Error removing {AUTOQCSTARTEREXE} shortcut from the Startup folder.", ex);
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
            ProgramLog.Info($"Found {procs.Length} {AUTOQCSTARTER} {(procs.Length > 1 ? "processes" : "process")}.");
            return procs;
        }

        private static void SaveAutoQcStarterInStartup(ShortCutInfo shortcutInfo, bool overwrite = false)
        {
            if (overwrite && File.Exists(shortcutInfo.ShortcutPath))
            {
                ProgramLog.Info($"Deleting old shortcut {shortcutInfo.ShortcutPath}");
                File.Delete(shortcutInfo.ShortcutPath);
                
                if (File.Exists(shortcutInfo.ShortcutPath))
                {
                    ProgramLog.Error($"Could not delete {shortcutInfo.ShortcutPath}");
                }
            }
            
            if (!File.Exists(shortcutInfo.ShortcutPath))
            {
                ProgramLog.Info($"Adding {AUTOQCSTARTEREXE} shortcut to Startup folder.");

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
                    ProgramLog.Error($"Could not create a shortcut to {AUTOQCSTARTEREXE}.");
                }
            }
            else
            {
                ProgramLog.Info($"Shortcut to {AUTOQCSTARTEREXE} already exists in the Startup folder.");
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
            if (exeDirInfo == null)
            {
                throw new StartupManagerException($"Could not get directory path for executable '{exeLocation}'");
            }
            
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
            ProgramLog.Info($"Removing {AUTOQCSTARTER} shortcut from Startup folder");
            var shortcutPath = GetShortcutPath();
            ProgramLog.Info($"Shortcut path is {shortcutPath}");
            
            //Remove the shortcut
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
                ProgramLog.Info($"Shortcut removed: {shortcutPath}");
            }
            else
            {
                ProgramLog.Info($"Shortcut {shortcutPath} does not exist in Startup folder.");
            }
        }

        private static void StopAutoQcStarter()
        { 
            // Stop AutoQCStarter if it is running
            var procs = GetAutoQcStarterProcesses();

            if (procs.Length > 0)
            {
                ProgramLog.Info($"Stopping {AUTOQCSTARTER}");
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
                ProgramLog.Info($"{AUTOQCSTARTER} is already running");
            }
        }

        private static void StartAutoQcStarter(string shortcutPath)
        {
            ProgramLog.Info($"Starting {AUTOQCSTARTER} at {shortcutPath}");
            var procInfo = new ProcessStartInfo
            {
                UseShellExecute =
                    true, // Set to true otherwise there is an exception: "The specified executable is not a valid application for this OS platform".
                FileName = shortcutPath,
                CreateNoWindow = true
            };

            using (Process.Start(procInfo))
            {
                ProgramLog.Info($"{AUTOQCSTARTER} has been started.");
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
