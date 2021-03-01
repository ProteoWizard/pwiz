﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace SharedBatch
{

    // Interface for a configuration that will appear in the list of configurations
    public interface IConfig
    {
        // Throws an ArgumentException if the configuration does not have enough
        // information to run, or if the information is not applicable to this computer
        // (ie: incorrect file paths)
        void Validate();

        // Returns the name of the configuration
        string GetName();

        // Returns the last time the configuration was modified
        DateTime GetModified();

        // Tries to replace oldRoot with newRoot for all the file paths in the configuration.
        // Returns false if oldRoot is not present in all the file paths, or if the replaced paths do not exist.
        // 
        // IConfig replaced -> the new configuration with the replaced file paths iff TryPathReplace returns true,
        //                      otherwise replaced is the same as the current configuration (this)
        bool TryPathReplace(string oldRoot, string newRoot, out IConfig replaced);

        // Writes the configuration as xml
        void WriteXml(XmlWriter writer);

        // Returns a listViewItem displaying information about the configuration for the UI
        ListViewItem AsListViewItem(IConfigRunner runner);
    }

    // Possible status' the ConfigRunner may have. A ConfigRunner does not need to use every status, 
    // only ones that are applicable
    public enum RunnerStatus
    {
        Waiting,
        Starting,
        Running,
        Cancelling,
        Cancelled,
        Stopping,
        Stopped,
        Completed,
        Disconnected,
        Error
    }

    public interface IConfigRunner
    {
        string GetConfigName();

        IConfig GetConfig();

        RunnerStatus GetStatus();

        string GetDisplayStatus();

        Color GetDisplayColor();

        bool IsBusy();
        bool IsRunning();
        void Cancel();
    }

    // Validates a string variable, throws ArgumentException if invalid
    public delegate void Validator(string variable);

    // UserControl interface to validate value of an input
    public interface IValidatorControl
    {
        object GetVariable();

        // Uses Validator to determine if variable is valid
        bool IsValid(out string errorMessage);
    }

    // Possible actions a user is taking when opening a configuration in the edit configuration form 
    public enum ConfigAction
    {
        Add, Edit, Copy
    }

    // Interface to control parts of the MainForm user interface programatically
    public interface IMainUiControl
    {
        // Updates the displayed configurations based on the configManager
        void UpdateUiConfigurations();

        // Updates the log files in the dropdown list based on the configManager
        void UpdateUiLogFiles();

        // Updates the Ui running buttons
        void UpdateRunningButtons(bool canStart, bool canStop);

        void AddConfiguration(IConfig config);
        void ReplaceSelectedConfig(IConfig config);

        void LogToUi(string filePath, string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string filePath, string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(string filePath, List<string> lines);
        void LogErrorLinesToUi(string filePath, List<string> lines);

        void DisplayError(string message);
        void DisplayWarning(string message);
        void DisplayInfo(string message);
        void DisplayErrorWithException(string message, Exception exception);
        DialogResult DisplayQuestion(string message);
        DialogResult DisplayLargeQuestion(string message);
    }
}
