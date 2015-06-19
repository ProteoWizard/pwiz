using System;
using System.Collections.Generic;

namespace AutoQC
{
    public abstract class TabSettings
    {
        public static AutoQCForm MainForm;

        public abstract void InitializeFromDefaultSettings();
        public abstract bool IsSelected();
        public abstract bool ValidateSettings();
        public abstract void SaveSettings();

        /// <summary>
        /// Returns a list of command-line arguments to be passed to SkylineRunner.
        /// </summary>
        /// <param name="importContext">Contains information about what we are importing</param>
        /// <param name="toPrint">True if the arguments will be logged</param>
        /// <returns></returns>
        public abstract IEnumerable<string> SkylineRunnerArgs(ImportContext importContext, bool toPrint = false);

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
}