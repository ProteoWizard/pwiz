using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AutoQCStarter.Properties;

namespace AutoQCStarter
{
    static class Program
    {
        private const string APP_NAME = "AutoQCStarter";
        private const string PUBLISHER_NAME = "University of Washington";
        private static string _autoqcAppName = "AutoQC";
        private static string _autoqcAppPath;

        private static readonly string LOG_FILE = "AutoQCStarter.log";
        
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            // Handle exceptions on the non-UI thread. 
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // https://saebamini.com/Allowing-only-one-instance-of-a-C-app-to-run/
            using (var mutex = new Mutex(false, $"{PUBLISHER_NAME} {APP_NAME}"))
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    ShowError($"Another instance of {APP_NAME} is already running.");
                    return;
                }

                if (!InitLogging())
                {
                    mutex.ReleaseMutex();
                    return;
                }

                Log($"Starting {APP_NAME}...");

                try
                {
                    _autoqcAppPath = GetAutoQcPath(args);
                }
                catch (ArgumentException e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                    ShowError(e.Message);
                    // Delete shortcut in the Startup folder, if it exists
                    DeleteShortcut();

                    mutex.ReleaseMutex();
                    return;
                }
                
                var stateRunning = false;
                while (true)
                {
                    if (!StartAutoQc(ref stateRunning))
                    {
                        break;
                    }
                    Thread.Sleep(TimeSpan.FromMinutes(2));
                }
                mutex.ReleaseMutex();
            }
        }

        private static string GetAutoQcPath(IReadOnlyList<string> args)
        {
            if (args.Count > 0)
            {
                // Assume path to the AutoQC Loader exe is given
                var path = args[0].Trim();
                if (!File.Exists(path))
                {
                    throw new ArgumentException($"Given path to AutoQC Loader executable does not exist: {path}");     
                }

                if(!path.EndsWith("AutoQC.exe") && !path.EndsWith("AutoQC-daily.exe"))
                {
                    throw new ArgumentException($"Given path is not to an AutoQC.exe or AutoQC-daily.exe: {path}");
                }

                return path;
            }
            
            return GetAppRefPath();
        }

        private static void DeleteShortcut()
        {
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var shortcutPath = Path.Combine(startupDir, $"{APP_NAME}.lnk");
            if (File.Exists(shortcutPath))
            {
                Log($"Deleting shortcut {shortcutPath}");
                try
                {
                    File.Delete(shortcutPath);
                }
                catch (Exception e)
                {
                    Log($"Unable to delete {shortcutPath}");
                    Log(e.Message);
                }
            }
        }

        private static bool InitLogging()
        {
            var logLocation = GetLogLocation();
            var logFile = Path.Combine(logLocation, LOG_FILE);

            try
            {
                using (new FileStream(logFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                }
            }
            catch (Exception e)
            {
                ShowError($"Cannot create or write to log file: {logFile}." + Environment.NewLine + $"Error was: {e.Message}");
                return false;
            }

            Trace.Listeners.Add(new TextWriterTraceListener(logFile, $"{APP_NAME} Log"));
            Trace.AutoFlush = true;
            return true;
        }

        private static string GetLogLocation()
        {
            // Why use CodeBase instead of Location?
            // CodeBase: The location of the assembly as specified originally (https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly.codebase?)
            // Location: The location of the loaded file that contains the manifest. If the loaded file was shadow-copied, the location is that of the file after being shadow-copied (https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly.location?)
            // Using Location can be a problem in some unit testing scenarios (https://corengen.wordpress.com/2011/08/03/assembly-location-and-codebase/)
            var file = Assembly.GetExecutingAssembly().CodeBase;

            // How to convert CodeBase to filesystem path: https://stackoverflow.com/questions/4107625/how-can-i-convert-assembly-codebase-into-a-filesystem-path-in-c
            // Ended up using the code below from the SkylineNightlyShim project
            if (file.StartsWith(@"file:"))
            {
                file = file.Substring(5);
            }
            while (file.StartsWith(@"/"))
            {
                file = file.Substring(1);
            }
            return Path.GetDirectoryName(file);
        }

        private static string GetAppRefPath()
        {
            var allProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var apprefName = _autoqcAppName + ".appref-ms";
            var paths = new[] {
                                //e.g. %APPDATA%\Microsoft\Windows\Start Menu\Programs\University of Washington\AutoQC.appref-ms
                                Path.Combine(Path.Combine(allProgramsPath, PUBLISHER_NAME), apprefName),
                                //e.g. %APPDATA%\Microsoft\Windows\Start Menu\Programs\AutoQC\AutoQC.appref-ms
                                Path.Combine(Path.Combine(allProgramsPath, _autoqcAppName), apprefName) 
                            };
            var appRefPath = paths.FirstOrDefault(File.Exists);

            if (appRefPath == null)
            {
                var err = new StringBuilder(
                    $"Could not find application reference {apprefName}. Looked at the following locations: ");
                
                foreach (var path in paths)
                {
                    err.AppendLine();
                    err.AppendLine(path);
                }
                throw new ArgumentException(err.ToString());
            }

            return appRefPath;
        }

        private static bool StartAutoQc(ref bool stateRunning)
        {
            if (Process.GetProcessesByName(_autoqcAppName).Length == 0)
            {
                Log($"Starting {_autoqcAppName}.");

                if (!File.Exists(_autoqcAppPath))
                {
                    var err = $"{_autoqcAppPath} no longer exists. Stopping.";
                    Log(err);
                    ShowError(err);
                    return false;
                }

                // Run AutoQC
                var procInfo = new ProcessStartInfo
                {
                    UseShellExecute =
                        true, // Set to true otherwise there is an exception: The specified executable is not a valid application for this OS platform.
                    FileName = _autoqcAppPath,
                    CreateNoWindow = true
                };
                try
                {
                    using (Process.Start(procInfo))
                    {
                        Log($"Started {_autoqcAppPath}.");
                    }
                }
                catch (Exception e)
                {
                    var err = $"Could not start {_autoqcAppPath}: {e.Message}";
                    Log(err);
                    Log(e.StackTrace);
                    ShowError(err);
                    return false;
                }
            }

            if (!stateRunning)
            {
                Log($"{_autoqcAppName} is running.");
                stateRunning = true;
            }

            return true;
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, string.Format(Resources.Program_AppName__0__Error, APP_NAME), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void Log(string message)
        {
            Trace.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {message}");
        }

        private static void Application_ThreadException(Object sender, ThreadExceptionEventArgs e)
        {
            Log($"Unhandled exception on UI thread: {e.Exception}");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Log($"{APP_NAME} encountered an unexpected error.");
                Log(((Exception)e.ExceptionObject).Message);
                ShowError($"{APP_NAME} encountered an unexpected error. " + Environment.NewLine + $"Error was: {((Exception)e.ExceptionObject).Message}");
            }
            finally
            {
                Application.Exit();
            }
        }
    }
}
