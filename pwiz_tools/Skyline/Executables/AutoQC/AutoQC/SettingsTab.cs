using System;

namespace AutoQC
{
    public abstract class SettingsTab
    {
        public static AutoQCForm MainForm;

        public abstract void InitializeFromDefaultSettings();
        public abstract bool IsSelected();
        public abstract bool ValidateSettings();
        public abstract void SaveSettings();

        /// <summary>
        /// Returns the command-line arguments to be passed to SkylineRunner.
        /// </summary>
        /// <param name="importContext">Contains information about what we are importing</param>
        /// <param name="toPrint">True if the arguments will be logged</param>
        /// <returns></returns>
        public abstract string SkylineRunnerArgs(ImportContext importContext, bool toPrint = false);

        /// <summary>
        /// Returns information about a process that should be run before running SkylineRunner.
        /// </summary>
        /// <param name="importContext"></param>
        /// <returns></returns>
        public abstract ProcessInfo RunBefore(ImportContext importContext);

        /// <summary>
        /// Returns information about a process that should be run after running SkylineRunner.
        /// </summary>
        /// <param name="importContext"></param>
        /// <returns></returns>
        public abstract ProcessInfo RunAfter(ImportContext importContext);

        public void LogOutput(string message)
        {
            MainForm.LogOutput(message);
        }

        public void LogErrorOutput(string error)
        {
            MainForm.LogErrorOutput(error);
        }

        public void Log(string message, params Object[] args)
        {
            MainForm.Log(string.Format(message, args));
        }
    }

    public class ProcessInfo
    {
        public string Executable { get; private set; }
        public string ExeName { get; private set; }
        public string Args { get; private set; }
        public string ArgsToPrint { get; private set; }

        private bool _doRetry;
        private int _tryCount;

        public ProcessInfo(string exe, string args)
        {
            Executable = exe;
            ExeName = Executable;
            Args = args;
            ArgsToPrint = args;
        }

        public ProcessInfo(string exe, string exeName, string args, string argsToPrint) : this (exe, args)
        {
            ExeName = exeName;
            ArgsToPrint = argsToPrint;
        }

        public int GetTryCount()
        {
            return _tryCount;
        }

        public void incrementTryCount()
        {
            _tryCount++;
        }

        public void allowRetry()
        {
            _doRetry = true;
        }

        public bool canRetry()
        {
            return _doRetry && _tryCount < AutoQCForm.MAX_TRY_COUNT;
        }
    }
}